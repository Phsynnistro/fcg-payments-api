using Microsoft.EntityFrameworkCore;

namespace Fcg.Payments.Api.Infrastructure.Persistence;

/// <summary>Executado após o schema existir. Deve ser idempotente (roda a cada startup, em N réplicas).</summary>
public interface IDataSeeder
{
    Task SeedAsync(CancellationToken cancellationToken);
}

public static class DatabaseInitializer
{
    private const int MaxAttempts = 10;

    /// <summary>
    /// Cria/atualiza o schema com retry (o Postgres pode subir depois da API — em Kubernetes não existe
    /// "depends_on"). Se o projeto tiver migrations, usa Migrate (EF Core 9+ usa lock, seguro com várias
    /// réplicas). Sem migrations, cai para EnsureCreated — aceitável só em ambiente de estudo.
    /// Em produção: migrations bundle executado por um Job/initContainer, fora do processo da API.
    /// </summary>
    public static async Task InitializeDatabaseAsync<TContext>(this IServiceProvider services, CancellationToken cancellationToken = default)
        where TContext : DbContext
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var scope = services.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<TContext>();

                if (db.Database.GetMigrations().Any())
                {
                    logger.LogInformation("Aplicando migrations de {Context}...", typeof(TContext).Name);
                    await db.Database.MigrateAsync(cancellationToken);
                }
                else
                {
                    logger.LogWarning("{Context} sem migrations: usando EnsureCreated (somente para estudo).", typeof(TContext).Name);
                    await db.Database.EnsureCreatedAsync(cancellationToken);
                }

                foreach (var seeder in scope.ServiceProvider.GetServices<IDataSeeder>())
                {
                    await seeder.SeedAsync(cancellationToken);
                }

                logger.LogInformation("Banco de {Context} pronto.", typeof(TContext).Name);
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts && !cancellationToken.IsCancellationRequested)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(attempt * 2, 10));
                logger.LogWarning(ex, "Falha ao inicializar o banco (tentativa {Attempt}/{Max}). Nova tentativa em {Delay}s.",
                    attempt, MaxAttempts, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}
