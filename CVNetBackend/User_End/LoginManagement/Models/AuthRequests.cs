namespace CVNetBackend.LoginManagement.Models;

public class TokenAuthRequest
{
    public string IdToken { get; set; } = string.Empty;
    public string? Agreement { get; set; }
}