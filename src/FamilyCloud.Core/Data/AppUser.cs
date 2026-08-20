using Microsoft.AspNetCore.Identity;

namespace FamilyCloud.Core.Data;

public class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; }
}
