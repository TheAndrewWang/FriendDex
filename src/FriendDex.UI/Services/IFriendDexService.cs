using FriendDex.Web.Domain;

namespace FriendDex.Web.Services;

// Everything the friend pages need. The website implements this with MongoDB directly;
// the Android app implements it by calling the website's API over HTTP.
// Friends returned here never include photo bytes (see Friend.HasPhoto); sprites are included (they're tiny).
// Errors meant for the user are thrown as InvalidOperationException with a readable message.
public interface IFriendDexService
{
    Task<List<Friend>> GetFriendsAsync();
    Task<Friend?> GetFriendAsync(int id);

    // Creates the friend when Id is 0, otherwise updates name, gender, birthday and favorite color. Returns the Id.
    Task<int> SaveFriendAsync(Friend friend);
    Task DeleteFriendAsync(int id);

    Task AddInterestAsync(int friendId, Interest interest);
    Task UpdateInterestAsync(int friendId, Interest interest);
    Task DeleteInterestAsync(int friendId, int interestId);

    // Pass null to remove the photo.
    Task SetPhotoAsync(int friendId, byte[]? photo, string? contentType);

    Task GenerateSpriteAsync(int friendId);
    Task ClearSpriteAsync(int friendId);

    Task<FriendUpdateProposal> ProposeFromNoteAsync(int friendId, string note);
    Task ApplyProposalAsync(int friendId, FriendUpdateProposal proposal);
}
