using InvoiceManagement.Modules.Invoicing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InvoiceManagement.Api.Extensions;

/// <summary>
/// Extension methods for database initialization during startup.
/// </summary>
public static class DatabaseExtensions
{
    /// <summary>
    /// Applies pending EF Core migrations (or ensures the schema is created) for the invoicing module.
    /// Only call in Development — this is a convenience for local dev; production uses the standalone Migrator.
    /// </summary>
    public static async Task UseDatabaseMigrationAsync(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(DatabaseExtensions));

        try
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<InvoicingDbContext>();

            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();

            if (pendingMigrations.Any())
            {
                logger.LogInformation("Applying {Count} pending migration(s)...", pendingMigrations.Count());
                await dbContext.Database.MigrateAsync();
                logger.LogInformation("Migrations applied successfully.");
            }
            else
            {
                logger.LogInformation("No pending migrations. Ensuring database is created...");
                await dbContext.Database.EnsureCreatedAsync();
                logger.LogInformation("Database ready.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database migration/initialization skipped — SQL Server may not be available yet.");
        }
    }
}
