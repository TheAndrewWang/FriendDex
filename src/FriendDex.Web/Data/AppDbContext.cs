using FriendDex.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace FriendDex.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {

    }

    public DbSet<Friend> Friends => Set<Friend>();
    public DbSet<Interest> Interests => Set<Interest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Friend>(friend =>
        {
            friend.Property(f => f.Name).IsRequired().HasMaxLength(100);
        });

        modelBuilder.Entity<Interest>(interest =>
        {
            interest.Property(i => i.Label).IsRequired().HasMaxLength(50);
            interest.Property(i => i.Kind).HasConversion<string>();

            interest.HasOne(i => i.Friend)
                .WithMany(f => f.Interests)
                .HasForeignKey(f => f.FriendId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}