namespace FamilyCloud.Contracts.Lists;

/// <summary>Grants (or updates) another family member's access to a list. <c>CanWrite = null</c>
/// (via a separate DELETE) revokes access entirely — see <c>DELETE /api/lists/{id}/share/{userId}</c>.</summary>
public sealed record ShareListRequest(Guid UserId, bool CanWrite);
