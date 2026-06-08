using System;
using System.Collections.Generic;

namespace CVNetBackend.Company_End.ApplicationsView.Models;

public class JobSpecDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Dept { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Posted { get; set; } = string.Empty;
    public int DaysActive { get; set; }
    public string SalaryRange { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public int Openings { get; set; }
    public string EmploymentType { get; set; } = string.Empty;
    public string WorkplaceType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Responsibilities { get; set; } = string.Empty;
    public int Status { get; set; }
    public int TotalApplicants { get; set; }
    public int NewApplied { get; set; }
    public int AvgMatchScore { get; set; }
    public List<SkillReqDto> Skills { get; set; } = new();
    public List<string> Education { get; set; } = new();
    public ExpReqDto? Experience { get; set; }
}

public class SkillReqDto
{
    public string Name { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
}

public class ExpReqDto
{
    public string LevelName { get; set; } = string.Empty;
    public int MinYears { get; set; }
    public int? MaxYears { get; set; }
}

public class ApplicantDto
{
    public string AppId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; } // Added profile image mapping
    public int IndustryScore { get; set; }
    public int CompanyMatchScore { get; set; }
    public List<string> Skills { get; set; } = new();
}

public class InterviewRequestDto
{
    public DateTime? InterviewDate { get; set; }
    public string Message { get; set; } = "We would like to invite you for an interview.";
}

public class RejectRequestDto
{
    public string Reason { get; set; } = "After careful consideration, we have decided to move forward with other candidates.";
}

public class FullApplicantProfileDto
{
    public string AppId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? ProfileImageUrl { get; set; }

    public string JobRole { get; set; } = string.Empty;
    public string? CurrentOrg { get; set; }
    public string? CurrentPosition { get; set; }

    public int MatchScore { get; set; }
    public int IndustryScore { get; set; }
    public int CompanySkillMatchScore { get; set; }

    public string PersonalStatement { get; set; } = string.Empty;
    public string AboutMe { get; set; } = string.Empty;
    public float? Gpa { get; set; }

    public string CvUrl { get; set; } = string.Empty;
    public string PortfolioUrl { get; set; } = string.Empty;

    public IEnumerable<dynamic> Experience { get; set; } = new List<dynamic>();
    public IEnumerable<dynamic> Education { get; set; } = new List<dynamic>();
    public IEnumerable<dynamic> Skills { get; set; } = new List<dynamic>();
    public IEnumerable<dynamic> Projects { get; set; } = new List<dynamic>();
    public IEnumerable<dynamic> Publications { get; set; } = new List<dynamic>();
    public IEnumerable<dynamic> Certifications { get; set; } = new List<dynamic>();
    public IEnumerable<dynamic> Memberships { get; set; } = new List<dynamic>();
    public IEnumerable<dynamic> Languages { get; set; } = new List<dynamic>();
    public IEnumerable<dynamic> TeachingExperience { get; set; } = new List<dynamic>();
    public IEnumerable<dynamic> ResearchExperience { get; set; } = new List<dynamic>();
    public IEnumerable<dynamic> Awards { get; set; } = new List<dynamic>();
    public IEnumerable<dynamic> Volunteers { get; set; } = new List<dynamic>();
    public IEnumerable<dynamic> SocialLinks { get; set; } = new List<dynamic>();
    
}

public class SocialLinkDto
{
    public string PlatformName { get; set; } = "";
    public string ProfileUrl { get; set; } = "";
}

public class SnapshotSkillDto
{
    public string SkillName { get; set; } = "";
    public string Level { get; set; } = "";
}