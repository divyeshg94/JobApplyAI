namespace JobApplyAi.Domain.Entities;

public class ProfileWorkExperience
{
    public Guid Id { get; set; }
    public Guid CandidateProfileId { get; set; }
    public CandidateProfile? CandidateProfile { get; set; }

    public required string Company { get; set; }
    public required string Title { get; set; }
    public string? LocationText { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public string? DescriptionText { get; set; }
}
