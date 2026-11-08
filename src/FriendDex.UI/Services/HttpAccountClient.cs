using System.Net.Http.Json;
using System.Text.Json;

namespace FriendDex.Web.Services;

// Logs the Android app in to a FriendDex server and returns a personal login token.
public class HttpAccountClient(HttpClient http)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public record LoginResult(string Token, string Email);
    private record ErrorResponse(string? Error);

    public Task<LoginResult> LoginAsync(string serverUrl, string email, string password) =>
        PostAsync(serverUrl, "api/auth/login", new { email, password });

    public Task<LoginResult> RegisterAsync(string serverUrl, string email, string password, string inviteCode) =>
        PostAsync(serverUrl, "api/auth/register", new { email, password, inviteCode });

    // Tells the server to forget this device's token. Failures are ignored: the app forgets it either way.
    public async Task LogoutAsync(string serverUrl, string token)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{serverUrl.TrimEnd('/')}/api/auth/logout");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            using var _ = await http.SendAsync(request);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { }
    }

    private async Task<LoginResult> PostAsync(string serverUrl, string path, object body)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync($"{serverUrl.TrimEnd('/')}/{path}", body, Json);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            throw new InvalidOperationException($"Can't reach a FriendDex server at {serverUrl}. Check the address.");
        }

        using (response)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                throw new InvalidOperationException("Too many attempts. Wait a minute and try again.");
            }
            if (!response.IsSuccessStatusCode)
            {
                string? message = null;
                try { message = (await response.Content.ReadFromJsonAsync<ErrorResponse>(Json))?.Error; }
                catch (JsonException) { }
                catch (NotSupportedException) { }
                throw new InvalidOperationException(message ?? $"The server returned an error ({(int)response.StatusCode}).");
            }
            return await response.Content.ReadFromJsonAsync<LoginResult>(Json)
                ?? throw new InvalidOperationException("The server sent an empty answer.");
        }
    }
}
