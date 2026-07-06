using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using IncentivePortal.Data;
using IncentivePortal.Models;
using Microsoft.IdentityModel.Tokens;

namespace IncentivePortal.Helpers;

public interface IPasswordHasher
{
    (string Hash, string Salt) HashPassword(string password);
    bool Verify(string password, string hash, string salt);
}

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    public (string Hash, string Salt) HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(32);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 150_000, HashAlgorithmName.SHA256, 32);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public bool Verify(string password, string hash, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var expected = Convert.FromBase64String(hash);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, 150_000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}

public interface IJwtTokenService
{
    string Generate(User user, IEnumerable<string> roles);
}

public sealed class JwtTokenService(IConfiguration configuration) : IJwtTokenService
{
    public string Generate(User user, IEnumerable<string> roles)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new("branchId", user.BranchId?.ToString() ?? string.Empty)
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public interface ICurrentUser
{
    string UserName { get; }
    int? UserId { get; }
    int? BranchId { get; }
    bool IsInRole(string role);
    bool CanAccessBranch(int branchId);
}

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string UserName => accessor.HttpContext?.User.Identity?.Name ?? "system";
    public int? UserId => int.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    public int? BranchId => int.TryParse(accessor.HttpContext?.User.FindFirstValue("branchId"), out var id) ? id : null;
    public bool IsInRole(string role) => accessor.HttpContext?.User.IsInRole(role) ?? false;
    public bool CanAccessBranch(int branchId) => IsInRole(AppRoles.SuperAdmin) || IsInRole(AppRoles.HOFinance) || IsInRole(AppRoles.Auditor) || BranchId == branchId;
}
