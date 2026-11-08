using FriendDex.Web.Auth;
using FriendDex.Web.Data;
using FriendDex.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace FriendDex.Web.Services;

// The real data service: talks to MongoDB, Claude and Gemini directly.
// Used by the website's pages, and by the API that the Android app calls.
// Every query is limited to the signed-in account's own friends.
public class ServerFriendDexService(
    IDbContextFactory<AppDbContext> factory,
    FriendNoteAgent agent,
    ChibiGenerator chibi,
    CurrentUser currentUser) : IFriendDexService
{
    public async Task<List<Friend>> GetFriendsAsync()
    {
        var owner = await currentUser.GetRequiredIdAsync();
        await using var db = await factory.CreateDbContextAsync();
        var friends = await db.Friends.AsNoTracking().Where(f => f.OwnerId == owner).OrderBy(f => f.Name).ToListAsync();
        friends.ForEach(ForPages);
        return friends;
    }

    public async Task<Friend?> GetFriendAsync(int id)
    {
        var owner = await currentUser.GetRequiredIdAsync();
        await using var db = await factory.CreateDbContextAsync();
        var friend = await db.Friends.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == owner);
        if (friend != null) ForPages(friend);
        return friend;
    }

    public async Task<int> SaveFriendAsync(Friend friend)
    {
        if (string.IsNullOrWhiteSpace(friend.Name)) throw new InvalidOperationException("Name is required.");

        var owner = await currentUser.GetRequiredIdAsync();
        await using var db = await factory.CreateDbContextAsync();
        if (friend.Id == 0)
        {
            var created = new Friend
            {
                OwnerId = owner,
                Name = Limit(friend.Name.Trim(), 100),
                Gender = Limit(friend.Gender?.Trim() ?? "", 50),
                Birthday = friend.Birthday,
                FavoriteColor = Blank(friend.FavoriteColor, 30),
            };
            db.Friends.Add(created);
            await db.SaveChangesAsync();
            return created.Id;
        }

        // Only copy the editable details, so a save never wipes the photo, sprite or interests.
        var tracked = await LoadTrackedAsync(db, friend.Id, owner);
        tracked.Name = Limit(friend.Name.Trim(), 100);
        tracked.Gender = Limit(friend.Gender?.Trim() ?? "", 50);
        tracked.Birthday = friend.Birthday;
        tracked.FavoriteColor = Blank(friend.FavoriteColor, 30);
        await db.SaveChangesAsync();
        return tracked.Id;
    }

    public async Task DeleteFriendAsync(int id)
    {
        var owner = await currentUser.GetRequiredIdAsync();
        await using var db = await factory.CreateDbContextAsync();
        var tracked = await db.Friends.FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == owner);
        if (tracked == null) return;
        db.Friends.Remove(tracked);
        await db.SaveChangesAsync();
    }

    public Task AddInterestAsync(int friendId, Interest interest) => ChangeAsync(friendId, f =>
    {
        if (string.IsNullOrWhiteSpace(interest.Label)) throw new InvalidOperationException("Label is required.");
        f.Interests.Add(new Interest
        {
            Label = Limit(interest.Label.Trim(), 50),
            Notes = Blank(interest.Notes, 2000),
            Kind = Enum.IsDefined(interest.Kind) ? interest.Kind : InterestKind.Like,
        });
    });

    public Task UpdateInterestAsync(int friendId, Interest interest) => ChangeAsync(friendId, f =>
    {
        var target = f.Interests.FirstOrDefault(i => i.Id == interest.Id)
            ?? throw new InvalidOperationException("That like or dislike no longer exists.");
        if (string.IsNullOrWhiteSpace(interest.Label)) throw new InvalidOperationException("Label is required.");
        target.Label = Limit(interest.Label.Trim(), 50);
        target.Notes = Blank(interest.Notes, 2000);
        if (Enum.IsDefined(interest.Kind)) target.Kind = interest.Kind;
    });

    public Task DeleteInterestAsync(int friendId, int interestId) =>
        ChangeAsync(friendId, f => f.Interests.RemoveAll(i => i.Id == interestId));

    public const int MaxPhotoBytes = 5 * 1024 * 1024;
    private static readonly string[] AllowedPhotoTypes = ["image/png", "image/jpeg", "image/webp", "image/gif"];

    public Task SetPhotoAsync(int friendId, byte[]? photo, string? contentType) => ChangeAsync(friendId, f =>
    {
        if (photo is { Length: > 0 })
        {
            if (photo.Length > MaxPhotoBytes) throw new InvalidOperationException("That image is over 5 MB. Please choose a smaller one.");
            if (contentType == null || !AllowedPhotoTypes.Contains(contentType))
            {
                throw new InvalidOperationException("Please choose a PNG, JPEG, WebP or GIF image.");
            }
            f.Photo = photo;
            f.PhotoContentType = contentType;
        }
        else
        {
            f.Photo = null;
            f.PhotoContentType = null;
        }
    });

    public async Task GenerateSpriteAsync(int friendId)
    {
        var owner = await currentUser.GetRequiredIdAsync();
        await using var db = await factory.CreateDbContextAsync();
        var tracked = await LoadTrackedAsync(db, friendId, owner);
        ChibiGenerator.GeneratedImage image;
        try
        {
            image = await chibi.GenerateAsync(tracked);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Couldn't reach Gemini. Check your connection and try again. ({ex.GetType().Name})");
        }
        tracked.Avatar = image.Bytes;
        tracked.AvatarContentType = image.ContentType;
        tracked.AvatarGeneratedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public Task ClearSpriteAsync(int friendId) => ChangeAsync(friendId, f =>
    {
        f.Avatar = null;
        f.AvatarContentType = null;
        f.AvatarGeneratedAt = null;
    });

    public async Task<FriendUpdateProposal> ProposeFromNoteAsync(int friendId, string note)
    {
        if (string.IsNullOrWhiteSpace(note)) throw new InvalidOperationException("Write something about your friend first.");
        var friend = await GetFriendAsync(friendId) ?? throw new InvalidOperationException("That friend doesn't exist.");
        try
        {
            return await agent.ProposeAsync(friend, Limit(note.Trim(), 4000));
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Couldn't reach the AI. Check that ANTHROPIC_API_KEY is set, then try again. ({ex.GetType().Name})");
        }
    }

    public Task ApplyProposalAsync(int friendId, FriendUpdateProposal proposal) =>
        ChangeAsync(friendId, f => FriendNoteAgent.Apply(f, proposal));

    // ---- helpers ----

    private async Task ChangeAsync(int friendId, Action<Friend> change)
    {
        var owner = await currentUser.GetRequiredIdAsync();
        await using var db = await factory.CreateDbContextAsync();
        var tracked = await LoadTrackedAsync(db, friendId, owner);
        change(tracked);
        await db.SaveChangesAsync();
    }

    // Someone else's friend looks exactly like a missing one, so IDs can't be probed.
    private static async Task<Friend> LoadTrackedAsync(AppDbContext db, int id, int owner) =>
        await db.Friends.FirstOrDefaultAsync(f => f.Id == id && f.OwnerId == owner)
            ?? throw new InvalidOperationException("That friend doesn't exist.");

    // Pages never need the photo bytes, only whether there is one.
    private static void ForPages(Friend friend)
    {
        friend.HasPhoto = friend.Photo is { Length: > 0 };
        friend.Photo = null;
    }

    private static string Limit(string s, int max) => s.Length <= max ? s : s[..max];
    private static string? Blank(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : Limit(s.Trim(), max);
}
