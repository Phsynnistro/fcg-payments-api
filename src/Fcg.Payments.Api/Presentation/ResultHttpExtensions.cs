using Fcg.Payments.Api.Application.Common;

namespace Fcg.Payments.Api.Presentation;

/// <summary>Única tradução Error → HTTP (RFC 9457 Problem Details). Fica na borda, não na aplicação.</summary>
public static class ResultHttpExtensions
{
    public static IResult ToProblem(this Error error) => error.Type switch
    {
        ErrorType.Validation => TypedResults.ValidationProblem(
            error.ValidationErrors ?? new Dictionary<string, string[]>(),
            detail: error.Message,
            title: "Dados inválidos"),
        ErrorType.NotFound => Problem(StatusCodes.Status404NotFound, error),
        ErrorType.Conflict => Problem(StatusCodes.Status409Conflict, error),
        ErrorType.Unauthorized => Problem(StatusCodes.Status401Unauthorized, error),
        ErrorType.Forbidden => Problem(StatusCodes.Status403Forbidden, error),
        _ => Problem(StatusCodes.Status400BadRequest, error)
    };

    private static IResult Problem(int statusCode, Error error) =>
        TypedResults.Problem(
            detail: error.Message,
            statusCode: statusCode,
            title: error.Code);
}
