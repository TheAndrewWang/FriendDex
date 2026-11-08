using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace FriendDex.Web.Domain;

public class Interest
{
    public int Id {get; set;}
    public int FriendId {get; set;}
    [JsonIgnore]   // back-link to the owner; would loop forever in JSON
    public Friend? Friend {get; set;}

    [Required(ErrorMessage = "Label is required.")]
    [MaxLength(50)]
    public string Label {get; set;} = "";

    public string? Notes {get; set;}
    public InterestKind Kind {get; set;}
}
