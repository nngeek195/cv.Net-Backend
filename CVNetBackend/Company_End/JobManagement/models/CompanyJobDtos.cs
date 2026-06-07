using System;

namespace CVNetBackend.Company_End.JobManagement.Models;

public class CompanyJobListDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Dept { get; set; } = string.Empty;
    public string Posted { get; set; } = string.Empty; // Formatted date string (e.g., "2 days ago")
    public int Applicants { get; set; }
    public int NewApplicants { get; set; }
    public int MatchAvg { get; set; } // The averaged CompanySkillMatchScore
    public string Status { get; set; } = string.Empty; // Active or Closed
}