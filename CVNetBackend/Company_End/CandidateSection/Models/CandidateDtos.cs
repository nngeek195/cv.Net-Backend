using System.Collections.Generic;

namespace CVNetBackend.Company_End.CandidateSection.Models;

public class CandidateListDto
{
    public string AppId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    
    // Ensure this is properly named and saved!
    public int IndustryScore { get; set; } 
    
    public string Status { get; set; } = string.Empty;
    public List<string> Skills { get; set; } = new();
}

public class JobFilterDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}