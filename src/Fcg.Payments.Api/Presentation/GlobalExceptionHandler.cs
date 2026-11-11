using Fcg.Payments.Api.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Fcg.Payments.Api.Presentation;

/// <summary>
/// Último recurso para exceções não tratadas (IExceptionHandler, .NET 8+).
/// Nunca vaza stack trace: 5xx devolvem mensagem genérica + traceId para correlação com o log.
/// </summary>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail) = exception switch
        {
            DomainException => (StatusCodes.Status422UnprocessableEntity, "Regra de negócio violada", exception.Message),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Requisição inválida", exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "Erro interno", "Ocorreu um erro inesperado. Informe o traceId ao suporte.")
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Erro não tratado em {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning("Requisição rejeitada em {Method} {Path}: {Message}", httpContext.Request.Method, httpContext.Request.Path, exception.Message);
        }

        httpContext.Response.StatusCode = statusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail
            }
        });
    }
}
