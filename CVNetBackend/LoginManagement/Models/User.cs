namespace CVNetBackend.LoginManagement.Models;

public class UserProfile
{
    public string Id { get; set; } = string.Empty; // Firebase UID
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string EmploymentStatus { get; set; } = "Unemployed";
}