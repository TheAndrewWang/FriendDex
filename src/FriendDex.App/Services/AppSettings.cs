using FriendDex.Web.Services;

namespace FriendDex.App.Services;

// The server address and who is logged in on this phone.
// The address and email are kept in normal app preferences; the login token in Android's secure storage.
public class AppSettings : IServerConnection
{
    public const string DefaultServerUrl = "https://frienddex.onrender.com";

    private const string UrlKey = "server_url";
    private const string EmailKey = "account_email";
    private const string TokenKey = "login_token";

    public string ServerUrl { get; private set; } = DefaultServerUrl;
    public string Email { get; private set; } = "";
    public string Token { get; private set; } = "";
    private bool loaded;

    public bool IsLoggedIn => ServerUrl.Length > 0 && Token.Length > 0;

    public async Task LoadAsync()
    {
        if (loaded) return;
        ServerUrl = Preferences.Default.Get(UrlKey, DefaultServerUrl);
        Email = Preferences.Default.Get(EmailKey, "");
        Token = await SecureStorage.Default.GetAsync(TokenKey) ?? "";
        loaded = true;
    }

    public async Task<(string ServerUrl, string Token)?> GetAsync()
    {
        await LoadAsync();
        return IsLoggedIn ? (ServerUrl, Token) : null;
    }

    public async Task SaveLoginAsync(string serverUrl, string email, string token)
    {
        ServerUrl = Normalize(serverUrl);
        Email = email;
        Token = token;
        Preferences.Default.Set(UrlKey, ServerUrl);
        Preferences.Default.Set(EmailKey, Email);
        await SecureStorage.Default.SetAsync(TokenKey, Token);
        loaded = true;
    }

    // Keeps the server address so logging back in is quick.
    public void ClearLogin()
    {
        Token = "";
        Email = "";
        Preferences.Default.Remove(EmailKey);
        SecureStorage.Default.Remove(TokenKey);
    }

    // "frienddex.onrender.com" -> "https://frienddex.onrender.com"; strips a trailing slash.
    public static string Normalize(string url)
    {
        var u = url.Trim().TrimEnd('/');
        if (u.Length > 0 && !u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            u = "https://" + u;
        }
        return u;
    }
}
