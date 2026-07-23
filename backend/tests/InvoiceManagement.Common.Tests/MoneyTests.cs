using InvoiceManagement.Common.Domain;
using Shouldly;
using Xunit;

namespace InvoiceManagement.Common.Tests;

public class MoneyTests
{
    [Fact]
    public void Create_ValidAmount_ShouldSucceed()
    {
        var money = new Money(100.50m, "USD");

        money.Amount.ShouldBe(100.50m);
        money.Currency.ShouldBe("USD");
    }

    [Fact]
    public void Create_NegativeAmount_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => new Money(-1, "USD"));
    }

    [Fact]
    public void Create_InvalidCurrency_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => new Money(100, ""));
        Should.Throw<ArgumentException>(() => new Money(100, "USDD"));
    }

    [Fact]
    public void Add_SameCurrency_ShouldSucceed()
    {
        var a = new Money(100, "USD");
        var b = new Money(50, "USD");

        var result = a + b;

        result.Amount.ShouldBe(150m);
        result.Currency.ShouldBe("USD");
    }

    [Fact]
    public void Add_DifferentCurrency_ShouldThrow()
    {
        var a = new Money(100, "USD");
        var b = new Money(50, "EUR");

        Should.Throw<InvalidOperationException>(() => a + b);
    }

    [Fact]
    public void Multiply_ShouldSucceed()
    {
        var money = new Money(100.50m, "USD");

        var result = money * 3;

        result.Amount.ShouldBe(301.50m);
    }

    [Fact]
    public void RoundsToTwoDecimals()
    {
        var money = new Money(100.555m, "USD");

        money.Amount.ShouldBe(100.56m);
    }
}
