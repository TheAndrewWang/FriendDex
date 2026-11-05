namespace FriendDex.Web.Domain;

public class Interest
{
    public int Id {get; set;}
    public int FriendId {get; set;}
    public Friend? Friend {get; set;}
    public string Label {get; set;} = "";
    public string? Notes {get; set;}
    public InterestKind Kind {get; set;}


}
