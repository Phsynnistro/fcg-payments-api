using System.Text.Json.Serialization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Fcg.Payments.Api.Presentation;

public static class ApiDefaultsExtensions
{
    public static IServiceCollection AddApiDefaults(this IServiceCollection services)
    {
        // RFC 9457: todo erro sai no mesmo formato, com traceId para correlacionar com o log.
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Instance = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
            context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();

        // Enums trafegam como texto ("Pending"), não como número mágico.
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());
        services.AddHealthChecks();

        return services;
    }

    /// <summary>OpenAPI + Scalar. Ligado por configuração (ApiDocs:Enabled), não por ambiente.</summary>
    public static WebApplication MapApiDocs(this WebApplication app, string title)
    {
        if (!app.Configuration.GetValue("ApiDocs:Enabled", false))
        {
            return app;
        }

        app.MapOpenApi();
        app.MapScalarApiReference(options => options
            .WithTitle(title)
            .AddPreferredSecuritySchemes("Bearer"));

        return app;
    }

    /// <summary>Publica o esquema Bearer no documento OpenAPI (Microsoft.OpenApi v2, .NET 10).</summary>
    private sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
    {
        private const string SchemeName = "Bearer";

        public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "JWT emitido pelo UsersAPI em POST /api/auth/login."
            };

            document.Security =
            [
                new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(SchemeName, document)] = [] }
            ];

            return Task.CompletedTask;
        }
    }
}
