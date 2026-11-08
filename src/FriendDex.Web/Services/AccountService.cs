using System.Security.Cryptography;
using System.Text;
using FriendDex.Web.Data;
using FriendDex.Web.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FriendDex.Web.Services;

// Sign-up, password checks and app login tokens.
public class AccountService(IDbContextFactory<AppDbContext> factory, IConfiguration config)
{
    public const string InviteCodeKey = "FRIENDDEX_INVITE_CODE";
    public const int MinPasswordLength = 8;
    private const int MaxTokensPerUser = 10;

    private static readonly PasswordHasher<User> Hasher = new();

    public async Task<User> RegisterAsync(string email, string password, string inviteCode)
    {
        var expectedCode = config[InviteCodeKey];
        if (string.IsNullOrWhiteSpace(expectedCode))
        {
            throw new InvalidOperationException("Sign-up is closed on this server.");
        }
        if (!SameSecret(inviteCode?.Trim() ?? "", expectedCode.Trim()))
        {
            throw new InvalidOperationException("That invite code isn't right.");
        }

        var normalized = NormalizeEmail(email);
        if (normalized.Length is < 3 or > 200 || !normalized.Contains('@'))
        {
            throw new InvalidOperationException("Enter a valid email address.");
        }
        if ((password ?? "").Length < MinPasswordLength)
        {
            throw new InvalidOperationException($"Use a password with at least {MinPasswordLength} characters.");
        }

        await using var db = await factory.CreateDbContextAsync();
        if (await db.Users.AnyAsync(u => u.Email == normalized))
        {
            throw new InvalidOperationException("An account with that email already exists. Log in instead.");
        }

        var isFirstUser = !await db.Users.AnyAsync();
        var user = new User { Email = normalized, CreatedAt = DateTime.UtcNow };
        user.PasswordHash = Hasher.HashPassword(user, password!);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Friends saved before accounts existed belong to the first person to sign up (the server's owner).
        if (isFirstUser)
        {
            var orphans = await db.Friends.Where(f => f.OwnerId == 0).ToListAsync();
            orphans.ForEach(f => f.OwnerId = user.Id);
            await db.SaveChangesAsync();
        }
        return user;
    }

    // Returns the user when the email and password match, otherwise null.
    public async Task<User?> CheckPasswordAsync(string email, string password)
    {
        var normalized = NormalizeEmail(email);
        await using var db = await factory.CreateDbContextAsync();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalized);
        if (user == null)
        {
            // Hash anyway so a missing account takes as long as a wrong password.
            Hasher.HashPassword(new User(), password ?? "");
            return null;
        }

        var result = Hasher.VerifyHashedPassword(user, user.PasswordHash, password ?? "");
        if (result == PasswordVerificationResult.Failed) return null;
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = Hasher.HashPassword(user, password!);
            await db.SaveChangesAsync();
        }
        return user;
    }

    public async Task<User?> FindAsync(int userId)
    {
        await using var db = await factory.CreateDbContextAsync();
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
    }

    // Creates a new app login token. The token itself is returned once; only its hash is stored.
    public async Task<string> CreateApiTokenAsync(int userId)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        await using var db = await factory.CreateDbContextAsync();
        db.ApiTokens.Add(new ApiToken { Id = Hash(token), UserId = userId, CreatedAt = DateTime.UtcNow });

        // Keep only the newest tokens per account (one per device is plenty).
        var old = (await db.ApiTokens.Where(t => t.UserId == userId).ToListAsync())
            .OrderByDescending(t => t.CreatedAt).Skip(MaxTokensPerUser - 1).ToList();
        db.ApiTokens.RemoveRange(old);

        await db.SaveChangesAsync();
        return token;
    }

    public async Task<int?> FindUserIdByTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        await using var db = await factory.CreateDbContextAsync();
        var hash = Hash(token);
        var row = await db.ApiTokens.AsNoTracking().FirstOrDefaultAsync(t => t.Id == hash);
        return row?.UserId;
    }

    public async Task RevokeApiTokenAsync(string token)
    {
        await using var db = await factory.CreateDbContextAsync();
        var hash = Hash(token);
        var row = await db.ApiTokens.FirstOrDefaultAsync(t => t.Id == hash);
        if (row == null) return;
        db.ApiTokens.Remove(row);
        await db.SaveChangesAsync();
    }

    public static string NormalizeEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static bool SameSecret(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
