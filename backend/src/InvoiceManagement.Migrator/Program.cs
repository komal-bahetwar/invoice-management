using InvoiceManagement.Modules.Invoicing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace InvoiceManagement.Migrator;

public static class Program
{
    public static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();

        try
        {
            var host = Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureServices((context, services) =>
                {
                    var connectionString = context.Configuration.GetConnectionString("DefaultConnection")
                        ?? "Server=localhost,1433;Database=InvoiceManagement;User Id=sa;Password=YourStrong!Pass;TrustServerCertificate=True;";

                    services.AddDbContext<InvoicingDbContext>(options =>
                    {
                        options.UseSqlServer(connectionString);
                    });
                })
                .Build();

            using var scope = host.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<InvoicingDbContext>();

            Log.Information("Applying migrations...");
            await dbContext.Database.MigrateAsync();

            Log.Information("Migrations applied successfully.");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Migration failed.");
            throw;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }
}
