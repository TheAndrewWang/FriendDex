using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FriendDex.Web.Domain;

namespace FriendDex.Web.Services;

// Generates a chibi image of a friend with Gemini "Nano Banana 2" (Interactions API).
public class ChibiGenerator(HttpClient http, IConfiguration config)
{
    public const string Model = "gemini-3.1-flash-image";
    private const string Endpoint = "https://generativelanguage.googleapis.com/v1beta/interactions";
    private const string ApiRevision = "2026-05-20";

    public record GeneratedImage(byte[] Bytes, string ContentType);

    public async Task<GeneratedImage> GenerateAsync(Friend friend, CancellationToken ct = default)
    {
        var apiKey = config["GEMINI_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("GEMINI_API_KEY isn't set. Set it in your terminal, then restart the app.");
        }

        var input = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = BuildPrompt(friend) } };
        if (friend.Photo is { Length: > 0 } && friend.PhotoContentType != null)
        {
            input.Add(new JsonObject
            {
                ["type"] = "image",
                ["mime_type"] = friend.PhotoContentType,
                ["data"] = Convert.ToBase64String(friend.Photo),
            });
        }

        var body = new JsonObject
        {
            ["model"] = Model,
            ["input"] = input,
            ["response_format"] = new JsonObject
            {
                ["type"] = "image",
                ["aspect_ratio"] = "1:1",
                ["image_size"] = "1K",
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("x-goog-api-key", apiKey);
        request.Headers.Add("Api-Revision", ApiRevision);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Gemini returned {(int)response.StatusCode}: {ErrorMessage(text)}");
        }

        var image = ExtractImage(text)
            ?? throw new InvalidOperationException("Gemini didn't return an image. Try again, or reword the friend's interests.");

        // Snap to a chunky pixel grid so every sprite matches the bead-sprite look.
        return new GeneratedImage(Pixelator.ToPixelGrid(image.Bytes), "image/png");
    }

    public static string BuildPrompt(Friend friend)
    {
        // Tiny sprites only have room for a few props.
        var likes = friend.Interests.Where(i => i.Kind == InterestKind.Like).Select(i => i.Label).Take(3).ToList();
        var hasPhoto = friend.Photo is { Length: > 0 };

        var lines = new List<string>
        {
            "A single pixel art sprite of a cute chibi character, about 48x48 pixels, in a polished 16-bit game style. " +
                "Pixels are crisp, hard-edged squares: no anti-aliasing, no blur, no smooth gradients.",
            "Framing: FULL BODY, always. Show the entire character from the top of the head down to the feet, " +
                "with both feet visible and standing on the bottom of the frame area. Never crop at the waist or chest; " +
                "never a bust, portrait or headshot.",
            "Viewing angle: straight-on front view. The character faces the viewer directly, standing upright, " +
                "symmetrical pose, arms relaxed at the sides.",
            "Proportions: chibi, a big head that is about half of the body, small body, short arms and legs.",
            "Outline: a clean dark outline 1 pixel wide around the whole character, with dark pixels for internal lines.",
            "Face: large friendly eyes with white highlight pixels, small simple mouth, light blush on the cheeks.",
            "Color: bright, cheerful colors with 3 or 4 shades per area (base, shadow, highlight) for soft, readable shading.",
            "Background: plain pure white. Only the one character, centered, with the whole body taking up about " +
                "80% of the image height and a little white space on every side. No text, no border, no ground shadow.",
        };

        if (hasPhoto)
        {
            lines.Add("The character is the person in the attached photo drawn as a human chibi sprite: keep their hairstyle, " +
                "hair color, skin tone, and glasses or other distinctive features, simplified into a few big pixels. " +
                "The photo may show only their face or upper body; still draw the complete chibi body with legs and feet.");
        }
        else if (!string.IsNullOrWhiteSpace(friend.Gender))
        {
            lines.Add($"The character is a human chibi; gender: {friend.Gender}.");
        }
        else
        {
            lines.Add("The character is a human chibi.");
        }

        if (!string.IsNullOrWhiteSpace(friend.FavoriteColor))
        {
            lines.Add($"Their clothes are mainly {friend.FavoriteColor}.");
        }
        if (likes.Count > 0)
        {
            lines.Add($"Show their interests with one or two tiny pixel props (just a few pixels each) for: {string.Join(", ", likes)}.");
        }

        // Repeat the most important rule last; models follow the end of a prompt closely.
        lines.Add("Remember: one full-body character, head to feet, front view, centered on white.");
        return string.Join("\n", lines);
    }

    // The docs don't show the raw response JSON, so search it for the first image:
    // either output_image.data, or a content block like { "type": "image", "data": "...", "mime_type": "..." }.
    public static GeneratedImage? ExtractImage(string json)
    {
        JsonNode? root;
        try { root = JsonNode.Parse(json); }
        catch (JsonException) { return null; }
        return Find(root);

        static GeneratedImage? Find(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject obj:
                    if (obj["output_image"] is JsonObject outImg && TryImage(outImg, out var a)) return a;
                    if ((string?)(obj["type"] as JsonValue)?.ToString() == "image" && TryImage(obj, out var b)) return b;
                    if (obj["inline_data"] is JsonObject inline && TryImage(inline, out var c)) return c;
                    if (obj["inlineData"] is JsonObject inline2 && TryImage(inline2, out var d)) return d;
                    foreach (var (_, child) in obj)
                    {
                        if (Find(child) is { } found) return found;
                    }
                    return null;
                case JsonArray arr:
                    foreach (var child in arr)
                    {
                        if (Find(child) is { } found) return found;
                    }
                    return null;
                default:
                    return null;
            }
        }

        static bool TryImage(JsonObject obj, out GeneratedImage image)
        {
            image = null!;
            if (obj["data"] is not JsonValue dataValue || !dataValue.TryGetValue(out string? data) || string.IsNullOrEmpty(data))
            {
                return false;
            }
            try
            {
                var bytes = Convert.FromBase64String(data);
                var mime = (obj["mime_type"] ?? obj["mimeType"])?.ToString() ?? "image/png";
                image = new GeneratedImage(bytes, mime.StartsWith("image/") ? mime : "image/png");
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }

    private static string ErrorMessage(string body)
    {
        try
        {
            var msg = JsonNode.Parse(body)?["error"]?["message"]?.ToString();
            if (!string.IsNullOrWhiteSpace(msg)) return msg;
        }
        catch (JsonException) { }
        return body.Length > 300 ? body[..300] : body;
    }
}
