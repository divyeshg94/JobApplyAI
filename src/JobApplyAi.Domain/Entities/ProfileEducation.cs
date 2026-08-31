namespace JobApplyAi.Domain.Entities;

public class ProfileEducation
{
    public Guid Id { get; set; }
    public Guid CandidateProfileId { get; set; }
    public CandidateProfile? CandidateProfile { get; set; }

    public required string Institution { get; set; }
    public string? Degree { get; set; }
    public string? FieldOfStudy { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}
