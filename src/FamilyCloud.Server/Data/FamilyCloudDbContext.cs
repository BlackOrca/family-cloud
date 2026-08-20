using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using FamilyCloud.Calendar.Domain;
using FamilyCloud.Core.Data;
using FamilyCloud.Core.Domain;
using FamilyCloud.Family.Domain;
using FamilyCloud.Lists.Domain;
// Aliased: this file lives under FamilyCloud.Server, and every FamilyCloud.* project is itself a
// child namespace of FamilyCloud — so the bare names "Calendar"/"Family" resolve to those sibling
// project namespaces (FamilyCloud.Calendar/FamilyCloud.Family), not the classes the usings above
// import, and using them unqualified is a compile error (CS0118), not just a style nit.
using CalendarEntity = FamilyCloud.Calendar.Domain.Calendar;
using FamilyEntity = FamilyCloud.Family.Domain.Family;

namespace FamilyCloud.Server.Data;

/// <summary>
/// The one composed EF Core model for the whole app. Lives in Server (not Core, and not any one
/// feature project) because it's the only project that legitimately references every feature —
/// Core stays free of a dependency on Calendar/Family this way, and feature-internal code (which
/// can't see this concrete type without a circular reference) depends on the abstract
/// <see cref="DbContext"/> base class instead, resolved via <c>Set&lt;T&gt;()</c>. See the Phase 1
/// architecture roadmap for the full reasoning.
/// </summary>
public class FamilyCloudDbContext(DbContextOptions<FamilyCloudDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<FamilyEntity> Families => Set<FamilyEntity>();

    public DbSet<FamilyMember> FamilyMembers => Set<FamilyMember>();

    public DbSet<CalendarAccount> CalendarAccounts => Set<CalendarAccount>();

    public DbSet<CalendarEntity> Calendars => Set<CalendarEntity>();

    public DbSet<CalendarPermission> CalendarPermissions => Set<CalendarPermission>();

    public DbSet<CachedEvent> CachedEvents => Set<CachedEvent>();

    public DbSet<AppSettings> AppSettings => Set<AppSettings>();

    public DbSet<SyncEvent> SyncEvents => Set<SyncEvent>();

    public DbSet<ItemList> ItemLists => Set<ItemList>();

    public DbSet<ListItem> ListItems => Set<ListItem>();

    public DbSet<ListPermission> ListPermissions => Set<ListPermission>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<FamilyEntity>(e =>
        {
            e.Property(f => f.Name).HasMaxLength(200);
        });

        builder.Entity<FamilyMember>(e =>
        {
            e.HasIndex(m => new { m.FamilyId, m.UserId }).IsUnique();

            e.HasOne(m => m.Family)
                .WithMany()
                .HasForeignKey(m => m.FamilyId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CalendarAccount>(e =>
        {
            e.Property(a => a.DisplayName).HasMaxLength(200);
        });

        builder.Entity<AppSettings>(e =>
        {
            e.Property(s => s.Title).HasMaxLength(200);
        });

        builder.Entity<SyncEvent>(e =>
        {
            e.Property(s => s.ResourceId).HasMaxLength(64);
        });

        builder.Entity<CalendarEntity>(e =>
        {
            e.Property(c => c.DisplayName).HasMaxLength(200);

            e.HasOne(c => c.CalendarAccount)
                .WithMany(a => a.Calendars)
                .HasForeignKey(c => c.CalendarAccountId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne<FamilyEntity>()
                .WithMany()
                .HasForeignKey(c => c.FamilyId)
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

        builder.Entity<ItemList>(e =>
        {
            e.Property(l => l.Name).HasMaxLength(200);

            e.HasOne<FamilyEntity>()
                .WithMany()
                .HasForeignKey(l => l.FamilyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ListItem>(e =>
        {
            e.Property(i => i.Text).HasMaxLength(500);
            e.Property(i => i.Quantity).HasMaxLength(50);

            e.HasOne(i => i.ItemList)
                .WithMany(l => l.Items)
                .HasForeignKey(i => i.ItemListId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ListPermission>(e =>
        {
            e.HasIndex(p => new { p.UserId, p.ItemListId }).IsUnique();

            e.HasOne(p => p.ItemList)
                .WithMany(l => l.Permissions)
                .HasForeignKey(p => p.ItemListId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
