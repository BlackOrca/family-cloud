using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using FamilyCloud.Contracts.Auth;
using FamilyCloud.Core.Data;

namespace FamilyCloud.Server.Api;

internal static class AuthEndpoints
{
    // The only anonymous /api endpoint — everything else requires the JWT it hands out.
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/auth/login", async (
            LoginRequest request,
            UserManager<AppUser> userManager,
            IConfiguration configuration) =>
        {
            var user = await userManager.FindByNameAsync(request.UserName);
            if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
            {
                return Results.Unauthorized();
            }

            var signingKey = configuration["Jwt:SigningKey"]
                ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");
            // No refresh-token flow yet — a long-lived token is an accepted simplification for
            // now (same spirit as the no-HTTPS decision), revisit if that becomes a problem.
            var expiresUtc = DateTimeOffset.UtcNow.AddDays(30);

            Claim[] claims =
            [
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(JwtRegisteredClaimNames.UniqueName, user.UserName ?? ""),
            ];

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: configuration["Jwt:Issuer"] ?? "FamilyCloud",
                audience: configuration["Jwt:Audience"] ?? "FamilyCloud",
                claims: claims,
                expires: expiresUtc.UtcDateTime,
                signingCredentials: credentials);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            return Results.Ok(new LoginResponse(tokenString, expiresUtc, user.DisplayName));
        }).AllowAnonymous();

        return endpoints;
    }
}
