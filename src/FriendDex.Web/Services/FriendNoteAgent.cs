using System.Text.Json;
using System.Text.Json.Serialization;
using Anthropic;
using Anthropic.Models.Beta;
using Anthropic.Models.Beta.Messages;
using ModelId = Anthropic.Models.Messages.Model;
using FriendDex.Web.Domain;

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
    public bool IsEmpty =>
        Gender == null && Birthday == null && FavoriteColor == null &&
        AddInterests.Count == 0 && UpdateInterests.Count == 0 && RemoveInterestIds.Count == 0;
}

public record ProposedNewInterest(string Label, string? Notes, string Kind);

public record ProposedInterestEdit(int Id, string? Notes, string? Kind);

public class FriendNoteAgent
{
    // Created on first use, so the app still starts without an API key.
    private readonly Lazy<AnthropicClient> client = new(() => new AnthropicClient());

    private const string SystemPrompt = """
        You help a person keep track of details about their friends in an app called FriendDex.
        The user writes a free-form note about one friend. Turn it into changes to that friend's record.

        Rules:
        - Only propose changes the note actually supports. Do not invent details.
        - Interests are short labels (1-3 words, lowercase, e.g. "hiking", "spicy food") with optional notes
          holding the extra context from the note. Kind is "Like" or "Dislike".
        - If an existing interest already covers the topic, update it (by id) instead of adding a duplicate.
          When updating notes, write the full new notes text, keeping useful existing context.
        - If the note says the friend's feelings flipped (used to like, now dislikes), update that interest's kind.
        - Remove an interest only if the note clearly says it is no longer true and a flip doesn't fit.
        - Set gender, birthday (yyyy-MM-dd, only when the full date is known) or favorite color only when the note
          states them. Otherwise use null.
        - summary: one short sentence describing what you changed, written to the user.
        """;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task<FriendUpdateProposal> ProposeAsync(Friend friend, string note, CancellationToken ct = default)
    {
        var current = new
        {
            name = friend.Name,
            gender = string.IsNullOrWhiteSpace(friend.Gender) ? null : friend.Gender,
            birthday = friend.Birthday?.ToString("yyyy-MM-dd"),
            favorite_color = friend.FavoriteColor,
            interests = friend.Interests.Select(i => new { id = i.Id, label = i.Label, notes = i.Notes, kind = i.Kind.ToString() }),
        };

        var userText = $"""
            Current record:
            {JsonSerializer.Serialize(current)}

            Note about {friend.Name}:
            {note}
            """;

        var response = await client.Value.Beta.Messages.Create(new MessageCreateParams
        {
            Model = "claude-opus-5-5",
            MaxTokens = 16000,
            // If the main model declines, the server retries on a fallback model.
            Betas = [AnthropicBeta.ServerSideFallback2026_06_01],
            Fallbacks = new List<BetaFallbackParam> { new(ModelId.ClaudeOpus4_8) },
            OutputConfig = new BetaOutputConfig
            {
                Effort = Effort.Low,
                Format = new BetaJsonOutputFormat { Schema = Schema },
            },
            System = SystemPrompt,
            Messages = [new BetaUserMessageParam { Content = userText }],
        }, ct);

        if (response.StopReason == "refusal")
        {
            throw new InvalidOperationException("The AI declined to process this note. Try rewording it.");
        }
        if (response.StopReason == "max_tokens")
        {
            throw new InvalidOperationException("The AI's answer was cut off. Try a shorter note.");
        }

        var json = string.Concat(response.Content.Select(b => b.Value).OfType<BetaTextBlock>().Select(t => t.Text));
        return Parse(json);
    }

    public static FriendUpdateProposal Parse(string json)
    {
        var raw = JsonSerializer.Deserialize<RawProposal>(json, JsonOptions)
            ?? throw new InvalidOperationException("The AI returned an empty answer.");

        return new FriendUpdateProposal(
            raw.Summary ?? "",
            Blank(raw.Gender),
            Blank(raw.Birthday),
            Blank(raw.FavoriteColor),
            raw.AddInterests ?? [],
            raw.UpdateInterests ?? [],
            raw.RemoveInterestIds ?? []);

        static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    // Applies an approved proposal to a tracked friend (with Interests loaded).
    // Ignores anything invalid instead of failing the whole update.
    public static void Apply(Friend friend, FriendUpdateProposal p)
    {
        if (p.Gender != null) friend.Gender = Truncate(p.Gender, 50);
        if (p.FavoriteColor != null) friend.FavoriteColor = Truncate(p.FavoriteColor, 30);
        if (p.Birthday != null && DateOnly.TryParseExact(p.Birthday, "yyyy-MM-dd", out var bday))
        {
            friend.Birthday = bday;
        }

        foreach (var edit in p.UpdateInterests)
        {
            var existing = friend.Interests.FirstOrDefault(i => i.Id == edit.Id);
            if (existing == null) continue;
            if (edit.Notes != null) existing.Notes = edit.Notes;
            if (TryKind(edit.Kind, out var kind)) existing.Kind = kind;
        }

        friend.Interests.RemoveAll(i => p.RemoveInterestIds.Contains(i.Id));

        foreach (var add in p.AddInterests)
        {
            if (string.IsNullOrWhiteSpace(add.Label)) continue;
            friend.Interests.Add(new Interest
            {
                Label = Truncate(add.Label.Trim(), 50),
                Notes = string.IsNullOrWhiteSpace(add.Notes) ? null : add.Notes,
                Kind = TryKind(add.Kind, out var kind) ? kind : InterestKind.Like,
            });
        }
    }

    private static bool TryKind(string? value, out InterestKind kind) =>
        Enum.TryParse(value, ignoreCase: true, out kind) && Enum.IsDefined(kind);

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    private record RawProposal(
        string? Summary,
        string? Gender,
        string? Birthday,
        string? FavoriteColor,
        List<ProposedNewInterest>? AddInterests,
        List<ProposedInterestEdit>? UpdateInterests,
        List<int>? RemoveInterestIds);

    // "string or null", written with anyOf (the documented structured-outputs form).
    private static object NullableString(string? description = null) => description == null
        ? new { anyOf = new object[] { new { type = "string" }, new { type = "null" } } }
        : new { anyOf = new object[] { new { type = "string", description }, new { type = "null" } } };

    private static readonly Dictionary<string, JsonElement> Schema = new()
    {
        ["type"] = JsonSerializer.SerializeToElement("object"),
        ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
        ["required"] = JsonSerializer.SerializeToElement(new[]
        {
            "summary", "gender", "birthday", "favorite_color",
            "add_interests", "update_interests", "remove_interest_ids",
        }),
        ["properties"] = JsonSerializer.SerializeToElement(new Dictionary<string, object>
        {
            ["summary"] = new { type = "string" },
            ["gender"] = NullableString(),
            ["birthday"] = NullableString("yyyy-MM-dd"),
            ["favorite_color"] = NullableString(),
            ["add_interests"] = new
            {
                type = "array",
                items = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new[] { "label", "notes", "kind" },
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["label"] = new { type = "string" },
                        ["notes"] = NullableString(),
                        ["kind"] = new { type = "string", @enum = new[] { "Like", "Dislike" } },
                    },
                },
            },
            ["update_interests"] = new
            {
                type = "array",
                items = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new[] { "id", "notes", "kind" },
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["id"] = new { type = "integer" },
                        ["notes"] = NullableString(),
                        ["kind"] = new { anyOf = new object[] { new { type = "string", @enum = new[] { "Like", "Dislike" } }, new { type = "null" } } },
                    },
                },
            },
            ["remove_interest_ids"] = new { type = "array", items = new { type = "integer" } },
        }),
    };
}
