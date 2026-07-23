using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Finbuckle.MultiTenant.Extensions;
using InvoiceManagement.Api.Extensions;
using InvoiceManagement.Api.Middleware;
using InvoiceManagement.Modules.Invoicing.Api;
using InvoiceManagement.Modules.Invoicing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ---- Serilog ----
builder.Host.UseSerilog((context, config) =>
{
    config.ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "InvoiceManagement.Api")
        .WriteTo.Console()
        .WriteTo.Seq(context.Configuration.GetConnectionString("Seq") ?? "http://localhost:5341");
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// ---- Multi-Tenancy (Finbuckle) ----
builder.Services.AddMultiTenant<TenantInfo>()
    .WithHeaderStrategy("X-Tenant-Id")
    .WithInMemoryStore(options =>
    {
        // Seed development tenants.
        // Schema naming: Finbuckle auto-generates schemas as `tenant_{Identifier}`,
        // e.g. `tenant_dev-tenant` for the dev tenant. To customize schema names,
        // create a custom ITenantInfo subclass with an Items/Schema property.
        options.Tenants.Add(new TenantInfo
        {
            Id = "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
            Identifier = "dev-tenant",
            Name = "Development Tenant"
        });
    });

// ---- Authentication & Authorization (JWT Bearer) ----
builder.Services.AddInvoiceManagementAuthentication(builder.Configuration);

// ---- EF Core (Schema-per-tenant via Finbuckle) ----
builder.Services.AddDbContext<InvoicingDbContext>((sp, options) =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.MigrationsAssembly(typeof(InvoicingDbContext).Assembly.FullName);
    });
});

// ---- Module Registration ----
builder.Services.AddInvoicingModule();

// ---- Controllers ----
builder.Services.AddControllers();

// ---- OpenAPI ----
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();

// ---- Exception Handling ----
// Order matters: ValidationExceptionHandler must be registered first so it
// intercepts FluentValidation exceptions before the generic GlobalExceptionHandler.
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// ---- Health Checks ----
builder.Services.AddHealthChecks();

// ---- Rate Limiting ----
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy<string>("GlobalLimit", _ =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: "global",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});

var app = builder.Build();

// ---- Middleware Pipeline ----
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    await app.UseDatabaseMigrationAsync();
}

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseMultiTenant();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();
app.MapHealthChecks("/health");

await app.RunAsync();

public partial class Program;