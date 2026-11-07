using System.Security.Claims;
using System.Text;
using DragonFlix.Api.Models;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DragonFlix.Api.Services;

public class TokenService(IConfiguration cfg)
{
    public static SymmetricSecurityKey Key(IConfiguration cfg) =>
        new(Encoding.UTF8.GetBytes(cfg["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured")));

    public string Create(User u)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("sub", u.Id.ToString()),
                new Claim("email", u.Email),
                new Claim("name", u.FullName),
                new Claim("role", u.Role),
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(Key(cfg), SecurityAlgorithms.HmacSha256),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}

public static class ClaimsExtensions
{
    public static int GetUserId(this ClaimsPrincipal p) => int.Parse(p.FindFirstValue("sub")!);
}
