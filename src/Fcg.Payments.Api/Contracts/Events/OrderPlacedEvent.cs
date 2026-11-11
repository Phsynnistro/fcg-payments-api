using MassTransit;

// ATENÇÃO: contrato público compartilhado por CÓPIA entre CatalogAPI (publica) e PaymentsAPI (consome).
// O namespace + nome do tipo formam a URN da mensagem ("urn:message:Fcg.Contracts.Events:OrderPlacedEvent").
// Evolua apenas de forma ADITIVA. O ContractTests de cada repositório protege essa identidade.
namespace Fcg.Contracts.Events;

/// <summary>
/// Publicado pelo CatalogAPI quando um usuário inicia a compra de um jogo.
/// Requisito do desafio: UserId, GameId e Price. Campos extras:
///  - OrderId: correlação ponta a ponta e chave de idempotência;
///  - UserEmail/GameTitle: event-carried state transfer, para o NotificationsAPI não depender de chamadas síncronas.
/// </summary>
[EntityName("fcg.order-placed")]
public sealed record OrderPlacedEvent(
    Guid OrderId,
    Guid UserId,
    Guid GameId,
    decimal Price,
    string UserEmail,
    string GameTitle,
    DateTime PlacedAt);
