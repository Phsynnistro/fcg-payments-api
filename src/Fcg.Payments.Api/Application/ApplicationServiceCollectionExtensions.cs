using Fcg.Payments.Api.Application.Payments;

namespace Fcg.Payments.Api.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ProcessOrderPaymentHandler>();
        services.AddScoped<PaymentQueries>();
        return services;
    }
}
