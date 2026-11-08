namespace FriendDex.Web.Services;

// Where the app finds the FriendDex server. The Android app stores this in its settings.
public interface IServerConnection
{
    Task<(string ServerUrl, string Token)?> GetAsync();
}
