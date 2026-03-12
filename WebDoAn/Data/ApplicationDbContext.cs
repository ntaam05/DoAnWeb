using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WebDoAn.Models;

namespace WebDoAn.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<RoomPost> RoomPosts { get; set; } = default!;
    public DbSet<Like> Likes { get; set; } = default!;
    public DbSet<RoomTenant> RoomTenants { get; set; } = default!;
    public DbSet<UserAccount> UserAccounts { get; set; } = default!;
    public DbSet<SavedRoom> SavedRooms { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<RoomPost>()
            .Property(x => x.Price)
            .HasPrecision(18, 2);

        builder.Entity<RoomPost>()
            .HasMany(x => x.Tenants)
            .WithOne(x => x.RoomPost)
            .HasForeignKey(x => x.RoomPostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SavedRoom>()
            .HasOne(x => x.RoomPost)
            .WithMany()
            .HasForeignKey(x => x.RoomPostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}