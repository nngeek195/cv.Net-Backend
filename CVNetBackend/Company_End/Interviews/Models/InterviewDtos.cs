using System;

namespace CVNetBackend.Company_End.Interviews.Models;

public class InterviewCandidateDto
{
    public string CallId { get; set; } = string.Empty;
    public string AppId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public string JobId { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public DateTime? InterviewDate { get; set; }
}

public class ScheduleInterviewDto
{
    public DateTime InterviewDate { get; set; }
}

public class RejectInterviewDto
{
    public string Reason { get; set; } = "Not a fit at this time.";
}


public class CreatePortalRequestDto
{
    public DateTime InterviewDate { get; set; }
    public List<string> JobIds { get; set; } = new();
}

public class PortalAuthRequestDto
{
    public string Password { get; set; } = string.Empty;
}

// Update in InterviewDtos.cs
public class SharedPortalDataDto
{
    public string JobId { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public List<PortalCandidateDto> Candidates { get; set; } = new(); // Changed from dynamic
}
public class ActivePortalDto
{
    public string PortalId { get; set; } = string.Empty;
    public DateTime InterviewDate { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string Password { get; set; } = string.Empty;
    public List<string> JobTitles { get; set; } = new();
    public string Link => $"/board/{PortalId}";
}
public class PortalCandidateDto
{
    public string AppId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public int IndustryScore { get; set; }
    public DateTime InterviewTime { get; set; }
}