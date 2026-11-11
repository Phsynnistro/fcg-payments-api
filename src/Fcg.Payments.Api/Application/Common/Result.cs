namespace Fcg.Payments.Api.Application.Common;

/// <summary>
/// Categoria do erro de negócio. A camada HTTP traduz cada categoria para um status code;
/// a camada de aplicação NÃO conhece HTTP.
/// </summary>
public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden
}

public sealed record Error(string Code, string Message, ErrorType Type)
{
    public Dictionary<string, string[]>? ValidationErrors { get; init; }

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static Error Validation(Dictionary<string, string[]> errors) =>
        new("validation_failed", "Um ou mais campos são inválidos.", ErrorType.Validation) { ValidationErrors = errors };
}

/// <summary>
/// Result pattern: falhas ESPERADAS (validação, conflito, não encontrado) são valores de retorno,
/// não exceções. Exceções ficam para o inesperado (banco fora, bug).
/// </summary>
public class Result
{
    protected Result(Error? error) => Error = error;

    public Error? Error { get; }
    public bool IsSuccess => Error is null;

    public static Result Success() => new(null);
    public static Result Failure(Error error) => new(error);

    public static implicit operator Result(Error error) => Failure(error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value) : base(null) => _value = value;
    private Result(Error error) : base(error) { }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Não é possível acessar o valor de um resultado com falha.");

    public static Result<T> Success(T value) => new(value);
    public static new Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
}

/// <summary>Acumula erros por campo e gera um único <see cref="Error"/> de validação.</summary>
public sealed class ValidationBuilder
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.OrdinalIgnoreCase);

    public ValidationBuilder AddIf(bool condition, string field, string message)
    {
        if (condition)
        {
            Add(field, message);
        }

        return this;
    }

    public void Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var messages))
        {
            messages = [];
            _errors[field] = messages;
        }

        messages.Add(message);
    }

    public Error? Build() => _errors.Count == 0
        ? null
        : Error.Validation(_errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
}

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);
}

public static class Paging
{
    public const int MaxPageSize = 50;

    public static (int Page, int PageSize) Normalize(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, MaxPageSize));
}
