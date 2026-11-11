using System.Security.Claims;

namespace Fcg.Payments.Api.Infrastructure.Security;

/// <summary>
/// Nomes de claims usados no token. MapInboundClaims = false mantém os nomes curtos do JWT
/// ("sub", "role") em vez dos URIs longos do WS-Federation.
/// </summary>
public static class FcgClaims
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";
}

public static class Policies
{
    public const string Admin = "Admin";
}

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst(FcgClaims.Subject)?.Value, out var id)
            ? id
            : throw new InvalidOperationException("Token sem claim 'sub' válida.");

    public static string GetEmail(this ClaimsPrincipal user) =>
        user.FindFirst(FcgClaims.Email)?.Value
        ?? throw new InvalidOperationException("Token sem claim 'email'.");

    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(Policies.Admin);
}
