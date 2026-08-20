using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OurLive.Core.Domain;

namespace OurLive.Core.Data;

public class OurLiveDbContext(DbContextOptions<OurLiveDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<CalendarAccount> CalendarAccounts => Set<CalendarAccount>();

    public DbSet<Calendar> Calendars => Set<Calendar>();

    public DbSet<CalendarPermission> CalendarPermissions => Set<CalendarPermission>();

    public DbSet<CachedEvent> CachedEvents => Set<CachedEvent>();

    public DbSet<AppSettings> AppSettings => Set<AppSettings>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<CalendarAccount>(e =>
        {
            e.Property(a => a.DisplayName).HasMaxLength(200);
        });

        builder.Entity<AppSettings>(e =>
        {
            e.Property(s => s.Title).HasMaxLength(200);
        });

        builder.Entity<Calendar>(e =>
        {
            e.Property(c => c.DisplayName).HasMaxLength(200);

            e.HasOne(c => c.CalendarAccount)
                .WithMany(a => a.Calendars)
                .HasForeignKey(c => c.CalendarAccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CalendarPermission>(e =>
        {
            e.HasIndex(p => new { p.UserId, p.CalendarId }).IsUnique();

            e.HasOne(p => p.Calendar)
                .WithMany(c => c.Permissions)
                .HasForeignKey(p => p.CalendarId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CachedEvent>(e =>
        {
            // A calendar never has two cached copies of the same iCal UID.
            e.HasIndex(ev => new { ev.CalendarId, ev.UId }).IsUnique();

            e.HasOne(ev => ev.Calendar)
                .WithMany(c => c.Events)
                .HasForeignKey(ev => ev.CalendarId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
