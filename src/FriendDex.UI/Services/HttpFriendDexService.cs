using System.Net.Http.Json;
using System.Text.Json;
using FriendDex.Web.Domain;

namespace FriendDex.Web.Services;

// The app's data service: every call goes to the FriendDex server's API.
// Keys and the database stay on the server; the phone only holds the server address and app token.
public class HttpFriendDexService(HttpClient http, IServerConnection connection) : IFriendDexService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private record IdResponse(int Id);
    private record ErrorResponse(string? Error);

    public Task<List<Friend>> GetFriendsAsync() =>
        SendAsync<List<Friend>>(HttpMethod.Get, "api/friends");

    public async Task<Friend?> GetFriendAsync(int id)
    {
        using var response = await RawSendAsync(HttpMethod.Get, $"api/friends/{id}");
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<Friend>(Json);
    }

    public async Task<int> SaveFriendAsync(Friend friend) =>
        (await SendAsync<IdResponse>(HttpMethod.Post, "api/friends", friend)).Id;

    public Task DeleteFriendAsync(int id) => SendAsync(HttpMethod.Delete, $"api/friends/{id}");

    public Task AddInterestAsync(int friendId, Interest interest) =>
        SendAsync(HttpMethod.Post, $"api/friends/{friendId}/interests", interest);

    public Task UpdateInterestAsync(int friendId, Interest interest) =>
        SendAsync(HttpMethod.Put, $"api/friends/{friendId}/interests/{interest.Id}", interest);

    public Task DeleteInterestAsync(int friendId, int interestId) =>
        SendAsync(HttpMethod.Delete, $"api/friends/{friendId}/interests/{interestId}");

    public Task SetPhotoAsync(int friendId, byte[]? photo, string? contentType) =>
        SendAsync(HttpMethod.Put, $"api/friends/{friendId}/photo", new { data = photo, contentType });

    public Task GenerateSpriteAsync(int friendId) => SendAsync(HttpMethod.Post, $"api/friends/{friendId}/sprite");

    public Task ClearSpriteAsync(int friendId) => SendAsync(HttpMethod.Delete, $"api/friends/{friendId}/sprite");

    public Task<FriendUpdateProposal> ProposeFromNoteAsync(int friendId, string note) =>
        SendAsync<FriendUpdateProposal>(HttpMethod.Post, $"api/friends/{friendId}/note", new { note });

    public Task ApplyProposalAsync(int friendId, FriendUpdateProposal proposal) =>
        SendAsync(HttpMethod.Post, $"api/friends/{friendId}/apply", proposal);

    // ---- plumbing ----

    private async Task SendAsync(HttpMethod method, string path, object? body = null)
    {
        using var response = await RawSendAsync(method, path, body);
        await EnsureSuccessAsync(response);
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body = null)
    {
        using var response = await RawSendAsync(method, path, body);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<T>(Json)
            ?? throw new InvalidOperationException("The server sent an empty answer.");
    }

    private async Task<HttpResponseMessage> RawSendAsync(HttpMethod method, string path, object? body = null)
    {
        var server = await connection.GetAsync()
            ?? throw new InvalidOperationException("Set the server address and app token in Settings first.");

        using var request = new HttpRequestMessage(method, $"{server.ServerUrl.TrimEnd('/')}/{path}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", server.Token);
        if (body != null) request.Content = JsonContent.Create(body, options: Json);

        try
        {
            return await http.SendAsync(request);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException(
                $"Can't reach the FriendDex server at {server.ServerUrl}. Check the address in Settings and that the server is running.");
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        string? message = null;
        try { message = (await response.Content.ReadFromJsonAsync<ErrorResponse>(Json))?.Error; }
        catch (JsonException) { }
        catch (NotSupportedException) { }

        throw new InvalidOperationException(message ?? $"The server returned an error ({(int)response.StatusCode}).");
    }
}
