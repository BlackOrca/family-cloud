namespace FamilyCloud.Family.Domain;

/// <summary>Join row: one user's membership (and role) in one family. Every <c>AppUser</c> gets
/// exactly one of these at creation time — multi-family membership per user isn't modeled yet.</summary>
public class FamilyMember
{
    public Guid Id { get; set; }

    public Guid FamilyId { get; set; }

    public Family? Family { get; set; }

    public Guid UserId { get; set; }

    public FamilyRole Role { get; set; }

    public DateTimeOffset JoinedUtc { get; set; }
}
