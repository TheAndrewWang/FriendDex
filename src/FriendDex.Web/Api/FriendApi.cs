using FriendDex.Web.Domain;
using FriendDex.Web.Services;

namespace FriendDex.Web.Api;

// HTTP API for the Android app. Every call needs "Authorization: Bearer <token>",
// where the token comes from logging in through /api/auth/login (see Accounts.cs).
public static class FriendApi
{
    public record NoteRequest(string Note);
    public record PhotoRequest(byte[]? Data, string? ContentType);
    public record IdResponse(int Id);
    public record ErrorResponse(string Error);

    public static void MapFriendApi(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization(Accounts.AppPolicy);

        api.MapGet("/friends", (IFriendDexService s) => s.GetFriendsAsync());

        api.MapGet("/friends/{id:int}", async (int id, IFriendDexService s) =>
            await s.GetFriendAsync(id) is { } f ? Results.Ok(f) : Results.NotFound(new ErrorResponse("That friend doesn't exist.")));

        api.MapPost("/friends", (Friend friend, IFriendDexService s) =>
            Run(async () => Results.Ok(new IdResponse(await s.SaveFriendAsync(friend)))));

        api.MapDelete("/friends/{id:int}", (int id, IFriendDexService s) =>
            Run(async () => { await s.DeleteFriendAsync(id); return Results.NoContent(); }));

        api.MapPost("/friends/{id:int}/interests", (int id, Interest interest, IFriendDexService s) =>
            Run(async () => { await s.AddInterestAsync(id, interest); return Results.NoContent(); }));

        api.MapPut("/friends/{id:int}/interests/{interestId:int}", (int id, int interestId, Interest interest, IFriendDexService s) =>
            Run(async () => { interest.Id = interestId; await s.UpdateInterestAsync(id, interest); return Results.NoContent(); }));

        api.MapDelete("/friends/{id:int}/interests/{interestId:int}", (int id, int interestId, IFriendDexService s) =>
            Run(async () => { await s.DeleteInterestAsync(id, interestId); return Results.NoContent(); }));

        api.MapPut("/friends/{id:int}/photo", (int id, PhotoRequest body, IFriendDexService s) =>
            Run(async () => { await s.SetPhotoAsync(id, body.Data, body.ContentType); return Results.NoContent(); }));

        api.MapPost("/friends/{id:int}/sprite", (int id, IFriendDexService s) =>
            Run(async () => { await s.GenerateSpriteAsync(id); return Results.NoContent(); }));

        api.MapDelete("/friends/{id:int}/sprite", (int id, IFriendDexService s) =>
            Run(async () => { await s.ClearSpriteAsync(id); return Results.NoContent(); }));

        api.MapPost("/friends/{id:int}/note", (int id, NoteRequest body, IFriendDexService s) =>
            Run(async () => Results.Ok(await s.ProposeFromNoteAsync(id, body.Note))));

        api.MapPost("/friends/{id:int}/apply", (int id, FriendUpdateProposal proposal, IFriendDexService s) =>
            Run(async () => { await s.ApplyProposalAsync(id, proposal); return Results.NoContent(); }));
    }

    // User-facing problems (missing friend, bad input, AI unreachable) become a 400 with a readable message.
    private static async Task<IResult> Run(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new ErrorResponse(ex.Message));
        }
        catch (Exception ex) when (ex is TimeoutException || ex.GetType().Namespace?.StartsWith("MongoDB") == true)
        {
            return Results.Json(new ErrorResponse("FriendDex can't reach its database right now. Try again in a minute."),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
