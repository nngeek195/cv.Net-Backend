namespace CVNetBackend.Company_End.Models;

public class UpdateCompanyDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SiteLink { get; set; } = string.Empty;
    public string HrContactPhone { get; set; } = string.Empty;
    // Maps to EmployeeCountRange enum in database
    public string EmployeeCount { get; set; } = "SMALL_2_10"; 
}

public class CompanyProfileResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LogoUrl { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SiteLink { get; set; } = string.Empty;
    public string HrEmail { get; set; } = string.Empty;
    public string HrContactPhone { get; set; } = string.Empty;
    public string EmployeeCount { get; set; } = string.Empty;
}