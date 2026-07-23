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

            // Always use MigrateAsync() — EnsureCreatedAsync() can create tables
            // from a stale model snapshot that doesn't include recent column additions.
            logger.LogInformation("Applying database migrations...");
            await dbContext.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database migration/initialization skipped — SQL Server may not be available yet.");
        }
    }
}
