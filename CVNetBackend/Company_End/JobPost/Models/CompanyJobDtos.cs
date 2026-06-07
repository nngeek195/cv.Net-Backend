using System;
using System.Collections.Generic;

namespace CVNetBackend.Company_End.Models;

public class CreateJobDto
{
    public string CategoryId { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string EmploymentType { get; set; } = "FULL_TIME";
    public string WorkplaceType { get; set; } = "ONSITE";
    
    // ✅ Allowed to be null/empty safely
    public string? Location { get; set; }
    public int Openings { get; set; } = 1;
    public string? Description { get; set; }
    public string? Responsibilities { get; set; }
    public string? SalaryRange { get; set; }
    public string? Currency { get; set; } = "LKR";
    
    public DateTime ApplicationDeadline { get; set; }
    public string HrContactEmail { get; set; } = string.Empty;
    
    public List<JobSkillDto> Skills { get; set; } = new();
    public JobExperienceDto? Experience { get; set; }
    public List<string> Educations { get; set; } = new();
}

public class JobSkillDto
{
    public string Name { get; set; } = string.Empty;
    public string Level { get; set; } = "INTERMEDIATE";
    // ✅ NEW: Visibility Flags
    public bool IsVisible { get; set; } = true;
    public bool ShowLevel { get; set; } = true;
}

public class JobExperienceDto
{
    public string LevelName { get; set; } = string.Empty;
    public int MinYears { get; set; } = 0;
    public int MaxYears { get; set; } = 0;
}