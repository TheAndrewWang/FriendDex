namespace FriendDex.Web.Domain;

// A FriendDex account. Lives only on the server.
public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = "";          // stored lowercased
    public string PasswordHash { get; set; } = "";   // ASP.NET Core PasswordHasher (salted, slow hash)
    public DateTime CreatedAt { get; set; }
}

// A login token for the Android app. Only a SHA-256 hash of the token is stored,
// so a database leak doesn't reveal tokens that could be used.
public class ApiToken
{
    public string Id { get; set; } = "";              // hex SHA-256 of the token
    public int UserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
