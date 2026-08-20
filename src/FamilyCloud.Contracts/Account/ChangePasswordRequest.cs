namespace FamilyCloud.Contracts.Account;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
