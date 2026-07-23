using System.Net.Http.Json;
using InvoiceManagement.Modules.Invoicing.Application.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace InvoiceManagement.Api.IntegrationTests;

public class InvoicesApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public InvoicesApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Tenant-Id", "dev-tenant");
    }

    [Fact]
    public async Task CreateInvoice_ValidRequest_ShouldReturnCreated()
    {
        var request = new
        {
            customerName = "Integration Test Corp",
            customerEmail = "test@integration.com",
            customerAddress = "456 Test Ave",
            issueDate = "2026-07-01T00:00:00Z",
            dueDate = "2026-08-01T00:00:00Z",
            taxRate = 10.0,
            currency = "USD",
            notes = "Integration test invoice",
            lineItems = new[]
            {
                new { description = "Test line item", quantity = 5, unitPrice = 100.00 }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/invoices", request);

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Created);
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<InvoiceDto>>();
        apiResponse!.Success.ShouldBeTrue();
        apiResponse.Data!.CustomerName.ShouldBe("Integration Test Corp");
    }

    [Fact]
    public async Task CreateInvoice_InvalidRequest_ShouldReturnBadRequest()
    {
        var request = new
        {
            customerName = "",
            customerEmail = "invalid",
            issueDate = "2026-07-01T00:00:00Z",
            dueDate = "2026-07-01T00:00:00Z",
            taxRate = 10.0,
            currency = "USD",
            lineItems = Array.Empty<object>()
        };

        var response = await _client.PostAsJsonAsync("/api/invoices", request);

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetInvoice_NotFound_ShouldReturn404()
    {
        var response = await _client.GetAsync($"/api/invoices/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListInvoices_ShouldReturnPagedResult()
    {
        var response = await _client.GetAsync("/api/invoices?page=1&pageSize=10");

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<InvoiceDto>>>();
        apiResponse!.Success.ShouldBeTrue();
        apiResponse.Data!.Page.ShouldBe(1);
    }

    [Fact]
    public async Task GetDashboard_ShouldReturnDashboard()
    {
        var response = await _client.GetAsync("/api/invoices/dashboard");

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<InvoiceDashboardDto>>();
        apiResponse!.Success.ShouldBeTrue();
    }
}
