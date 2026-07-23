using InvoiceManagement.Common.Domain;
using InvoiceManagement.Modules.Invoicing.Application.Commands.CreateInvoice;
using InvoiceManagement.Modules.Invoicing.Application.Commands.UpdateInvoiceStatus;
using InvoiceManagement.Modules.Invoicing.Application.DTOs;
using InvoiceManagement.Modules.Invoicing.Application.Queries.GetDashboard;
using InvoiceManagement.Modules.Invoicing.Application.Queries.GetInvoice;
using InvoiceManagement.Modules.Invoicing.Application.Queries.ListInvoices;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceManagement.Modules.Invoicing.Api.Controllers;

[ApiController]
[Route("api/invoices")]
public sealed class InvoicesController : ControllerBase
{
    private readonly IMediator _mediator;

    public InvoicesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Create a new invoice.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<InvoiceDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateInvoiceCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);

        if (result.IsFailure)
            return BadRequest(new ApiResponse<InvoiceDto>(false, null, result.Errors));

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Value!.Id },
            new ApiResponse<InvoiceDto>(true, result.Value, []));
    }

    /// <summary>
    /// List invoices with pagination and optional filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<InvoiceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTimeOffset? fromDate = null,
        [FromQuery] DateTimeOffset? toDate = null,
        CancellationToken ct = default)
    {
        var query = new ListInvoicesQuery(
            Math.Max(1, page),
            Math.Clamp(pageSize, 1, 100),
            status,
            search,
            fromDate,
            toDate);

        var result = await _mediator.Send(query, ct);

        return Ok(new ApiResponse<PagedResponse<InvoiceDto>>(true, result, []));
    }

    /// <summary>
    /// View invoice details by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<InvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetInvoiceQuery(id), ct);

        if (result.IsFailure)
            return NotFound(new ApiResponse<InvoiceDto>(false, null, result.Errors));

        return Ok(new ApiResponse<InvoiceDto>(true, result.Value, []));
    }

    /// <summary>
    /// Update invoice status. Allowed transitions: Draft→Sent, Sent/Overdue→Paid, Draft/Sent→Cancelled.
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(ApiResponse<InvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateInvoiceStatusCommand command,
        CancellationToken ct)
    {
        if (id != command.InvoiceId)
            return BadRequest(new ApiResponse<InvoiceDto>(false, null, ["Invoice ID mismatch."]));

        var result = await _mediator.Send(command, ct);

        if (result.IsFailure)
        {
            var error = result.Errors.FirstOrDefault() ?? "";
            if (error.Contains("not found"))
                return NotFound(new ApiResponse<InvoiceDto>(false, null, result.Errors));
            if (error.Contains("Cannot"))
                return Conflict(new ApiResponse<InvoiceDto>(false, null, result.Errors));
            return BadRequest(new ApiResponse<InvoiceDto>(false, null, result.Errors));
        }

        return Ok(new ApiResponse<InvoiceDto>(true, result.Value, []));
    }

    /// <summary>
    /// Get invoice dashboard summary for the current tenant.
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(ApiResponse<InvoiceDashboardDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetDashboardQuery(), ct);

        return Ok(new ApiResponse<InvoiceDashboardDto>(true, result, []));
    }
}
