namespace FamilyCloud.Family.Domain;

/// <summary>A user's role within one family — deliberately a small closed enum rather than a
/// free-text role name, since nothing today needs custom roles. Scoped to the membership row (not a
/// global ASP.NET Identity role) because a role is inherently "role within this family", which starts
/// to matter once multi-family operation exists.</summary>
public enum FamilyRole
{
    Member = 0,
    Admin = 1,
}
