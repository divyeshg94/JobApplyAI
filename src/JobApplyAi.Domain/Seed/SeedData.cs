namespace JobApplyAi.Domain.Seed;

/// <summary>
/// v1 runs without auth: every user-scoped row belongs to this single seeded user.
/// When real auth lands, replace reads of this constant with the authenticated user's id.
/// </summary>
public static class SeedData
{
    public static readonly Guid DefaultUserId = new("a1e0c8f0-0000-4000-8000-000000000001");

    /// <summary>Seeded Users.Email value — never a real deliverable address, used to detect "not set yet".</summary>
    public const string PlaceholderEmail = "owner@localhost";
}
