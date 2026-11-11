using System.Security.Claims;
using Fcg.Payments.Api.Application.Payments;
using Fcg.Payments.Api.Infrastructure.Security;

namespace Fcg.Payments.Api.Presentation.Endpoints;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/payments/orders/{orderId:guid}", GetByOrderAsync)
            .RequireAuthorization()
            .WithTags("Payments")
            .WithName("GetPaymentByOrder")
            .WithSummary("Consulta o pagamento de um pedido (dono do pedido ou Admin)")
            .Produces<PaymentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetByOrderAsync(Guid orderId, ClaimsPrincipal user, PaymentQueries queries, CancellationToken cancellationToken)
    {
        var result = await queries.GetByOrderIdAsync(orderId, user.GetUserId(), user.IsAdmin(), cancellationToken);
        return result.IsSuccess ? Results.Ok(result.Value) : result.Error!.ToProblem();
    }
}
