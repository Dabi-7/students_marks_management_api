using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

using MarksApi.Data;
using MarksApi.DTOs;
using MarksApi.Entities;
using Microsoft.AspNetCore.Identity;

namespace MarksApi.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly PasswordHasher<User> _passwordHasher;

    public AuthService(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
        _passwordHasher = new PasswordHasher<User>();
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return new AuthResult
            {
                Success = false,
                Message = "Invalid login request."
            };
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);

        if (user is null)
        {
            return new AuthResult
            {
                Success = false,
                Message = "Invalid username or password."
            };
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return new AuthResult
            {
                Success = false,
                Message = "Invalid username or password."
            };
        }

        var jwtKey = _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT key is not configured.");
        var jwtIssuer = _configuration["Jwt:Issuer"] ?? "MarksApi";
        var jwtAudience = _configuration["Jwt:Audience"] ?? "MarksApiUsers";
        var jwtExpiryMinutes = _configuration.GetValue<int?>("Jwt:ExpiryMinutes") ?? 60;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(jwtExpiryHours),
            signingCredentials: credentials);

        return new AuthResult
        {
            Success = true,
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            Username = user.Username,
            Role = user.Role
        };
    }
}
