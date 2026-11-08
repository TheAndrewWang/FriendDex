using FriendDex.Web.Domain;
using Microsoft.EntityFrameworkCore;
using MongoDB.EntityFrameworkCore.Extensions;

namespace FriendDex.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {

    }

    public DbSet<Friend> Friends => Set<Friend>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ApiToken> ApiTokens => Set<ApiToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Friend>(friend =>
        {
            // One MongoDB document per friend, in the "friends" collection.
            friend.ToCollection("friends");
            friend.Property(f => f.Id).HasValueGenerator<RandomIdGenerator>().ValueGeneratedOnAdd();
            friend.Property(f => f.Name).IsRequired().HasMaxLength(100);

            // Interests are stored inside the friend's document (embedded), not in their own collection.
            // Deleting a friend deletes their interests with them.
            friend.OwnsMany(f => f.Interests, interest =>
            {
                interest.WithOwner(i => i.Friend).HasForeignKey(i => i.FriendId);
                interest.HasKey(i => i.Id);
                interest.Property(i => i.Id).HasValueGenerator<RandomIdGenerator>().ValueGeneratedOnAdd();
                interest.Property(i => i.Label).IsRequired().HasMaxLength(50);
                interest.Property(i => i.Kind).HasConversion<string>();
            });
        });

        modelBuilder.Entity<User>(user =>
        {
            user.ToCollection("users");
            user.Property(u => u.Id).HasValueGenerator<RandomIdGenerator>().ValueGeneratedOnAdd();
            user.Property(u => u.Email).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<ApiToken>(token =>
        {
            token.ToCollection("api_tokens");
            token.Property(t => t.Id).ValueGeneratedNever();
        });
    }
}
