using Microsoft.EntityFrameworkCore;
using FamilyCloud.Core.Auth;
using FamilyCloud.Family.Domain;

namespace FamilyCloud.Family;

public class FamilyMembershipChecker(DbContext db) : IFamilyMembershipChecker
{
    public Task<bool> IsMemberAsync(Guid familyId, Guid userId, CancellationToken ct = default) =>
        db.Set<FamilyMember>().AnyAsync(m => m.FamilyId == familyId && m.UserId == userId, ct);
}
