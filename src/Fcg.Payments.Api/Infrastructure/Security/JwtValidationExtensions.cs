using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Fcg.Payments.Api.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;

    /// <summary>Discovery do emissor. Ex.: http://users-api/.well-known/openid-configuration (nome do Service no cluster).</summary>
    public string MetadataAddress { get; init; } = string.Empty;

    /// <summary>false apenas dentro do cluster/compose. Em produção: TLS (ou mTLS via service mesh).</summary>
    public bool RequireHttpsMetadata { get; init; }
}

/// <summary>
/// Este serviço só VALIDA tokens: não tem a chave privada. A chave pública é obtida via JWKS
/// no UsersAPI e cacheada pelo ConfigurationManager (rotação de chave sem redeploy dos consumidores).
/// </summary>
public static class JwtValidationExtensions
{
    public const string JwksHttpClientName = "users-api-jwks";

    public static IServiceCollection AddJwtValidation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer), "Jwt:Issuer é obrigatório.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Audience), "Jwt:Audience é obrigatório.")
            .Validate(o => Uri.TryCreate(o.MetadataAddress, UriKind.Absolute, out _), "Jwt:MetadataAddress deve ser uma URL absoluta.")
            .ValidateOnStart();

        // Única chamada SÍNCRONA entre microsserviços: buscar a chave pública (JWKS) no UsersAPI.
        // Pipeline padrão do Microsoft.Extensions.Http.Resilience (Polly v8, Aula 05): timeout por tentativa,
        // retry com backoff exponencial + jitter, circuit breaker e timeout total.
        services.AddHttpClient(JwksHttpClientName).AddStandardResilienceHandler();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, IHttpClientFactory>((bearer, jwtOptions, httpClientFactory) =>
            {
                var jwt = jwtOptions.Value;
                bearer.Backchannel = httpClientFactory.CreateClient(JwksHttpClientName);
                bearer.MetadataAddress = jwt.MetadataAddress;
                bearer.RequireHttpsMetadata = jwt.RequireHttpsMetadata;
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = FcgClaims.Name,
                    RoleClaimType = FcgClaims.Role
                };
                bearer.Events = new JwtBearerEvents
                {
                    // Sem isto, um 401 por chave/assinatura inválida é "mudo": o motivo real fica invisível.
                    OnAuthenticationFailed = context =>
                    {
                        context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                            .CreateLogger("JwtBearer")
                            .LogWarning("Token rejeitado: {Reason}", context.Exception.Message);
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Admin, policy => policy.RequireRole(Policies.Admin));

        return services;
    }
}
