namespace JobApplyAi.Domain.Entities;

/// <summary>Hard matching exclusion — postings from this company never surface as matches.</summary>
public class ProfileExcludedCompany
{
    public Guid Id { get; set; }
    public Guid CandidateProfileId { get; set; }
    public CandidateProfile? CandidateProfile { get; set; }

    public required string CompanyName { get; set; }
}
