using System;

// ✅ Update the namespace to match your actual folder structure
namespace CVNetBackend.User_End.JobApply.Models;

public class JobCategoryDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class CandidateJobListingDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyLogo { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty; 
    public string Location { get; set; } = string.Empty;
    public string WorkplaceType { get; set; } = string.Empty;
    public string EmploymentType { get; set; } = string.Empty;
    public string SalaryRange { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Responsibilities { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    
    public string SkillsJson { get; set; } = "[]";
    public string EducationsJson { get; set; } = "[]";
    public string ExperienceJson { get; set; } = "{}";
}