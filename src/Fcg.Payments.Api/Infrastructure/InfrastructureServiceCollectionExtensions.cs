using System.Text.Json.Serialization;
using Fcg.Payments.Api.Application.Abstractions;
using Fcg.Payments.Api.Domain.Common;
using Fcg.Payments.Api.Infrastructure.Gateway;
using Fcg.Payments.Api.Infrastructure.Messaging;
using Fcg.Payments.Api.Infrastructure.Observability;
using Fcg.Payments.Api.Infrastructure.Persistence;
using Fcg.Payments.Api.Infrastructure.Security;
using Fcg.Payments.Api.Presentation;
using Fcg.Payments.Api.Presentation.Consumers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fcg.Payments.Api.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddPersistence(services, configuration);
        AddMessaging(services, configuration);
        services.AddObservability(configuration);
        services.AddJwtValidation(configuration);
        return services;
    }

    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("ConnectionStrings:Database não configurada (deve vir de um Secret).");

        services.AddDbContext<PaymentsDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 3)));

        services.AddHealthChecks()
            .AddDbContextCheck<PaymentsDbContext>("database", tags: [HealthEndpoints.ReadinessTag]);

        services.AddOptions<PaymentSimulationOptions>()
            .Bind(configuration.GetSection(PaymentSimulationOptions.SectionName))
            .Validate(o => o.MaxApprovedAmount >= 0, "PaymentSimulation:MaxApprovedAmount não pode ser negativo.")
            .ValidateOnStart();
        services.AddScoped<IPaymentGateway, SimulatedPaymentGateway>();
    }

    private static void AddMessaging(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RabbitMqOptions>().Bind(configuration.GetSection(RabbitMqOptions.SectionName));
        var messaging = configuration.GetSection(MessagingOptions.SectionName).Get<MessagingOptions>() ?? new MessagingOptions();

        services.AddMassTransit(bus =>
        {
            bus.SetKebabCaseEndpointNameFormatter();

            bus.AddEntityFrameworkOutbox<PaymentsDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();
                outbox.QueryDelay = TimeSpan.FromSeconds(1);
            });

            bus.AddConsumer<OrderPlacedConsumer>().Endpoint(endpoint =>
            {
                endpoint.Name = messaging.Queues.OrderPlaced;
                endpoint.PrefetchCount = messaging.PrefetchCount;
                endpoint.ConcurrentMessageLimit = messaging.ConcurrentMessageLimit;
            });

            // Aplicado a TODO receive endpoint. A ordem importa: retry por FORA, outbox por DENTRO
            // → cada nova tentativa abre uma transação nova (inbox + mudanças + mensagens de saída).
            bus.AddConfigureEndpointsCallback((context, _, endpoint) =>
            {
                endpoint.UseMessageRetry(retry =>
                {
                    // Retry com backoff exponencial (Aula 05): até 5 novas tentativas, de 200 ms a 10 s.
                    retry.Exponential(5, TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(500));
                    retry.Ignore<DomainException>(); // violação de regra não melhora tentando de novo
                });
                endpoint.UseEntityFrameworkOutbox<PaymentsDbContext>(context);
            });

            bus.UsingRabbitMq((context, cfg) =>
            {
                var rabbit = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
                cfg.Host(rabbit.Host, rabbit.Port, rabbit.VirtualHost, host =>
                {
                    host.Username(rabbit.Username);
                    host.Password(rabbit.Password);
                });

                cfg.ConfigureJsonSerializerOptions(json =>
                {
                    json.Converters.Add(new JsonStringEnumConverter());
                    return json;
                });

                // Circuit breaker do consumo (Aula 05): se 15% ou mais das mensagens falharem (após 10 no período),
                // o endpoint PARA de consumir por 30 s em vez de esgotar mensagens contra uma dependência fora do ar.
                cfg.UseKillSwitch(options => options
                    .SetActivationThreshold(10)
                    .SetTripThreshold(15)
                    .SetRestartTimeout(TimeSpan.FromSeconds(30)));

                cfg.ConfigureEndpoints(context);
            });
        });
    }
}
