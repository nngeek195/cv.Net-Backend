namespace CVNetBackend.LoginManagement.Models;

public class SignupRequest
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = "candidate"; 
    public string Agreement { get; set; } = string.Empty;
}
public class GoogleAuthRequest
{
    public string IdToken { get; set; } = string.Empty;

    public string? Agreement { get; set; }
}