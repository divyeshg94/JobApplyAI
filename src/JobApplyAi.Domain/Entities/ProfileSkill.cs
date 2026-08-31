namespace JobApplyAi.Domain.Entities;

public class ProfileSkill
{
    public Guid Id { get; set; }
    public Guid CandidateProfileId { get; set; }
    public CandidateProfile? CandidateProfile { get; set; }

    public required string Name { get; set; }
    public string? Category { get; set; }
}
