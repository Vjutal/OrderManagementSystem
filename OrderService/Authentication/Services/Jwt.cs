using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace OrderService.Authentication.Services;

public class JwtOptions
{
    public required string Key { get; init; }
}

public class Jwt(IOptions<JwtOptions> options)
{
    private static string UserId = "some-user-id"; // Should be replaced with real user-id.
    public string GenerateToken()
    {
        var key = SecurityKey(options.Value.Key);
        var signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);
        
        var token = new JwtSecurityToken
        (
            claims: [new Claim(ClaimTypes.NameIdentifier, UserId)],
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature),
            expires: DateTime.UtcNow.AddYears(1)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static SymmetricSecurityKey SecurityKey(string key) => new(Encoding.ASCII.GetBytes(key));
}