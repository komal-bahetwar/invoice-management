using FluentValidation;
using InvoiceManagement.Modules.Invoicing.Application.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceManagement.Modules.Invoicing.Api;

/// <summary>
/// Extension methods for registering the Invoicing module's services.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInvoicingModule(this IServiceCollection services)
    {
        // MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(Application.Commands.CreateInvoice.CreateInvoiceCommand).Assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        // FluentValidation
        services.AddValidatorsFromAssembly(
            typeof(Application.Commands.CreateInvoice.CreateInvoiceCommandValidator).Assembly);

        // Scrutor — scan and register repositories
        services.Scan(scan => scan
            .FromAssemblies(typeof(Infrastructure.Repositories.InvoiceRepository).Assembly)
            .AddClasses(classes => classes.Where(t => t.Name.EndsWith("Repository")))
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        // Register DbContext as IUnitOfWork
        services.AddScoped<Domain.Interfaces.IUnitOfWork>(
            sp => sp.GetRequiredService<Infrastructure.Data.InvoicingDbContext>());

        return services;
    }
}
