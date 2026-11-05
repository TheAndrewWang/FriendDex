namespace FriendDex.Web.Domain;

public class Friend
{
    public int Id {get; set;}
    public string Name {get; set;} = "";
    public string Gender {get; set;} = "";
    public DateOnly? Birthday {get; set;}
    public string? FavoriteColor {get; set;}
    public List<Interest> Interests {get; set;} = new List<Interest>();
}

