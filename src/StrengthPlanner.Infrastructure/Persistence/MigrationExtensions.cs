using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace StrengthPlanner.Infrastructure.Persistence;

public static class MigrationExtensions
{
    /// <summary>
    /// Primeni sve neprimenjene EF Core migracije na startu aplikacije.
    /// Ponavlja pokušaj dok PostgreSQL ne postane dostupan (npr. kada se u
    /// Docker Compose okruženju baza tek podiže), pa tek onda propušta grešku.
    /// </summary>
    public static async Task ApplyMigrationsAsync(
        this IServiceProvider services,
        int maxAttempts = 10,
        TimeSpan? delay = null)
    {
        var retryDelay = delay ?? TimeSpan.FromSeconds(3);

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("StrengthPlanner.Migrations");

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync();
                logger.LogInformation("EF migracije primenjene (pokušaj {Attempt}).", attempt);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(
                    ex,
                    "Baza još nije dostupna (pokušaj {Attempt}/{MaxAttempts}). Ponavljam za {Delay}s…",
                    attempt,
                    maxAttempts,
                    retryDelay.TotalSeconds);

                await Task.Delay(retryDelay);
            }
        }
    }
}
