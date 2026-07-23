using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace InvoiceManagement.Api.Controllers;

/// <summary>
/// Development-only endpoint for generating JWT tokens.
/// In production, this would be replaced by Duende IdentityServer or an external identity provider.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public AuthController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Generate a development JWT token. Only available in Development environment.
    /// </summary>
    [HttpPost("token")]
    public IActionResult GenerateToken([FromBody] TokenRequest request)
    {
        if (!HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
            return NotFound();

        var jwtSection = _configuration.GetSection("Jwt");
        var secretKey = jwtSection["SecretKey"]!;
        var issuer = jwtSection["Issuer"]!;
        var audience = jwtSection["Audience"]!;
        var expirationMinutes = int.Parse(jwtSection["TokenExpirationMinutes"] ?? "60");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, request.Username ?? "dev-user"),
            new(ClaimTypes.Name, request.Username ?? "Dev User"),
            new("tenant_id", request.TenantId ?? "dev-tenant")
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        return Ok(new TokenResponse(tokenString, "Bearer", tokenDescriptor.Expires!.Value));
    }
}

/// <summary>
/// Request model for token generation.
/// </summary>
public sealed record TokenRequest(string? Username = "dev-user", string? TenantId = "dev-tenant");

/// <summary>
/// Response model containing the generated JWT.
/// </summary>
public sealed record TokenResponse(string AccessToken, string TokenType, DateTime ExpiresAt);
