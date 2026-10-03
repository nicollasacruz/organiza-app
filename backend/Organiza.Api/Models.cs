using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Organiza.Domain;
namespace Organiza.Api;

public class Member : IdentityUser
{
    public string DisplayName { get; set; } = "";
    public bool IsAdmin { get; set; }
    public bool IsActive { get; set; } = true;
    public string Accent { get; set; } = "#e6b8c5";
    public string Theme { get; set; } = "system";
}
public class Earning
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly Date { get; set; }
    public EarningKind Kind { get; set; }
    public decimal Quantity { get; set; }
    public int? Capacity { get; set; }
    public int Cents { get; set; }
    public string? SourceId { get; set; }
    public int Revision { get; set; }
}
public class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SeriesId { get; set; } = Guid.NewGuid();
    public Guid? PreviousId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string? AssigneeId { get; set; }
    public DateOnly? Due { get; set; }
    public string Status { get; set; } = "todo";
    public int? IntervalDays { get; set; }
    public int? IntervalCount { get; set; }
    public string? IntervalUnit { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? CompletedBy { get; set; }
    public bool Archived { get; set; }
    public int Revision { get; set; }
}
public class EarningTarget
{
    public DateOnly EffectiveMonth { get; set; }
    public int TargetCents { get; set; }
    public int Revision { get; set; }
}
public class Invitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public DateTimeOffset Expires { get; set; }
    public bool Used { get; set; }
    public int Revision { get; set; }
}
public class GoogleConnection
{
    public string MemberId { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string ProtectedTokens { get; set; } = "";
}
public class CalendarCopy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public string MemberId { get; set; } = "";
    public string AccountId { get; set; } = "";
    public string CalendarId { get; set; } = "";
    public string EventId { get; set; } = "";
    public string Url { get; set; } = "";
}
public class AppDb(DbContextOptions<AppDb> options) : IdentityDbContext<Member>(options)
{
    public DbSet<Earning> Earnings => Set<Earning>();
    public DbSet<EarningTarget> EarningTargets => Set<EarningTarget>();
    public DbSet<TaskItem> TaskItems => Set<TaskItem>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<GoogleConnection> GoogleConnections => Set<GoogleConnection>();
    public DbSet<CalendarCopy> CalendarCopies => Set<CalendarCopy>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<EarningTarget>().HasKey(x=>x.EffectiveMonth);
        b.Entity<EarningTarget>().Property(x=>x.Revision).IsConcurrencyToken();
        b.Entity<Earning>().Property(x=>x.Quantity).HasPrecision(8,2);
        b.Entity<Earning>().HasIndex(x=>x.SourceId).IsUnique();
        b.Entity<Earning>().Property(x=>x.Revision).IsConcurrencyToken();
        b.Entity<TaskItem>().Property(x=>x.Revision).IsConcurrencyToken();
        b.Entity<TaskItem>().HasIndex(x=>x.PreviousId).IsUnique();
        b.Entity<TaskItem>().HasIndex(x=>x.SeriesId).IsUnique().HasFilter("\"Status\" <> 'done' AND NOT \"Archived\"");
        b.Entity<Invitation>().HasIndex(x=>x.TokenHash).IsUnique();
        b.Entity<Invitation>().Property(x=>x.Revision).IsConcurrencyToken();
        b.Entity<GoogleConnection>().HasKey(x=>x.MemberId);
        b.Entity<CalendarCopy>().HasIndex(x=>new{x.TaskId,x.MemberId,x.AccountId,x.CalendarId}).IsUnique();
    }
}
public record LoginInput(string Email, string Password);
public record SignupInput(string Token, string Name, string Password);
public record CredentialInput(string CredentialJson, string? Name);
public record EarningWrite(DateOnly Date, EarningKind Kind, decimal Quantity, int? Capacity, int Revision = 0);
public record ImportedEarning(DateOnly Date, EarningKind Kind, decimal Quantity, int? Capacity, string SourceId, int ExpectedCents, Guid? ExistingId = null);
public record TaskWrite(string Title, string? Description, string? AssigneeId, DateOnly? Due, int? IntervalCount, string? IntervalUnit, int Revision = 0);
public record StatusWrite(string Status, int Revision);
public record PreferenceWrite(string Accent, string Theme);
public record TargetWrite(int TargetCents, int Revision);
public record InviteWrite(string Email);
public record CalendarWrite(Guid TaskId, string CalendarId, DateOnly Date, bool AllDay, string? Time, int DurationMinutes = 30);
