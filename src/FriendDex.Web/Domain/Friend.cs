using System.ComponentModel.DataAnnotations;

namespace FriendDex.Web.Domain;

public class Friend
{
    public int Id {get; set;}

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

    // AI-generated chibi image (Gemini). If null, the page shows the drawn chibi.
    public byte[]? Avatar {get; set;}
    public string? AvatarContentType {get; set;}
    public DateTime? AvatarGeneratedAt {get; set;}
}
