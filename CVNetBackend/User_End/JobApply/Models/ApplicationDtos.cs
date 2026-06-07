using System;

namespace CVNetBackend.User_End.JobApply.Models;

public class ApplyForJobDto
{
    public string JobId { get; set; } = string.Empty;
    public string CoverLetter { get; set; } = string.Empty; 
    public string ProfileId { get; set; } = string.Empty;
    
    // Core Profile Fields
    public string JobRole { get; set; } = string.Empty;
    public string PersonalStatement { get; set; } = string.Empty;
    public string AboutMe { get; set; } = string.Empty;
    public string PortfolioUrl { get; set; } = string.Empty;
    public string CvUrl { get; set; } = string.Empty;
    
    public int? MatchScore { get; set; }
    public int? IndustryScore { get; set; }
    // Modified Arrays (Sent from frontend as JSON strings)
    public string SkillsJson { get; set; } = "[]";
    public string ExperienceJson { get; set; } = "[]";
}

public class TargetProfileDropdownDto
{
    public string Id { get; set; } = string.Empty;
    public string JobRole { get; set; } = string.Empty;
    public string PersonalStatement { get; set; } = string.Empty;
}