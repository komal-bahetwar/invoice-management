namespace InvoiceManagement.Modules.Invoicing.Application.DTOs;

public sealed record ApiResponse<T>(
    bool Success,
    T? Data,
    IReadOnlyList<string> Errors);
