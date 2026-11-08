using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace FriendDex.Web.Domain;

public class Friend
{
    public int Id {get; set;}

    // The account this friend belongs to. Set by the server only; never sent to or accepted from the pages.
    [JsonIgnore]
    public int OwnerId {get; set;}

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100)]
    public string Name {get; set;} = "";

    [MaxLength(50)]
    public string Gender {get; set;} = "";

    public DateOnly? Birthday {get; set;}

    [MaxLength(30)]
    public string? FavoriteColor {get; set;}

    public List<Interest> Interests {get; set;} = new List<Interest>();

    // Optional photo for the chibi avatar, stored in the database.
    public byte[]? Photo {get; set;}
    public string? PhotoContentType {get; set;}

    // Set when sending a friend to the pages: photo bytes stay on the server, the pages only need to know one exists.
    [NotMapped]
    public bool HasPhoto {get; set;}

    // AI-generated chibi image (Gemini). If null, the page shows the drawn chibi.
    public byte[]? Avatar {get; set;}
    public string? AvatarContentType {get; set;}
    public DateTime? AvatarGeneratedAt {get; set;}

    // The generated sprite as an inline image (it's a tiny 48x48 PNG), so pages work the same in the browser and the app.
    [NotMapped, JsonIgnore]
    public string? AvatarDataUrl => Avatar is { Length: > 0 }
        ? $"data:{AvatarContentType ?? "image/png"};base64,{Convert.ToBase64String(Avatar)}"
        : null;
}
