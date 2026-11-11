namespace Fcg.Payments.Api.Domain.Common;

/// <summary>
/// Violação de invariante de domínio. Se esta exceção chegar ao usuário, significa que a
/// camada de aplicação deixou passar uma entrada inválida — por isso é mapeada para 422.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
