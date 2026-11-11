using MarksApi.DTOs;

namespace MarksApi.Services;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}

public class AuthResult
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? Username { get; set; }
    public string? Role { get; set; }
    public string? Message { get; set; }
}
