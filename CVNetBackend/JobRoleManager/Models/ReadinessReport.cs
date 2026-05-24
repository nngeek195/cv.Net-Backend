namespace CVNetBackend.JobRoleManager.Models;

public class SkillCalculationDetail
{
    public string SkillName { get; set; } = string.Empty;
    public string RequirementSource { get; set; } = string.Empty; // "Category Baseline" or "Role Specific"
    public string ExpectedLevel { get; set; } = string.Empty;
    public double ExpectedPercentage { get; set; }
    public string UserDeclaredLevel { get; set; } = "Missing";
    public double UserCalculatedPercentage { get; set; }
}

public class ReadinessReport
{
    public string JobRole { get; set; } = string.Empty;
    public string JobCategory { get; set; } = string.Empty;
    public double IndustryTargetBenchmark { get; set; } // e.g., 64.5%
    public double UserReadinessScore { get; set; }        // e.g., 42.1%
    public List<SkillCalculationDetail> Breakdown { get; set; } = new();
}