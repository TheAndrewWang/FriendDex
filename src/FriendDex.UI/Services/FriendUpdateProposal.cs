using System.Text.Json.Serialization;

namespace FriendDex.Web.Services;

// What the agent wants to change. Nothing is saved until the user approves it.
public record FriendUpdateProposal(
    string Summary,
    string? Gender,
    string? Birthday,          // "yyyy-MM-dd" or null
    string? FavoriteColor,
    List<ProposedNewInterest> AddInterests,
    List<ProposedInterestEdit> UpdateInterests,
    List<int> RemoveInterestIds)
{
    [JsonIgnore]
    public bool IsEmpty =>
        Gender == null && Birthday == null && FavoriteColor == null &&
        AddInterests.Count == 0 && UpdateInterests.Count == 0 && RemoveInterestIds.Count == 0;
}

public record ProposedNewInterest(string Label, string? Notes, string Kind);

public record ProposedInterestEdit(int Id, string? Notes, string? Kind);
