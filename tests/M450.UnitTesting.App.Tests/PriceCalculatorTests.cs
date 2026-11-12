using System.Globalization;
using M450.UnitTesting.App;

namespace M450.UnitTesting.App.Tests;

public class PriceCalculatorTests
{
    [Fact]
    public void CalculateTotal_WithPriceAndQuantityWithoutDiscount_ReturnsSubtotal()
    {
        // Arrange
        var calculator = new PriceCalculator();
        var unitPrice = 25.50m;
        var quantity = 3;
        var expectedTotal = 76.50m;

        // Act
        var actualTotal = calculator.CalculateTotal(unitPrice, quantity);

        // Assert
        Assert.Equal(expectedTotal, actualTotal);
    }

    [Theory]
    [InlineData("25.50", 3, "76.50")]
    [InlineData("10.00", 1, "10.00")]
    [InlineData("0.01", 1000000, "10000.00")]
    public void CalculateTotal_WithExplicitZeroDiscount_ReturnsSubtotal(
        string unitPriceText,
        int quantity,
        string expectedTotalText)
    {
        // Arrange
        var calculator = new PriceCalculator();
        var unitPrice = ParseDecimal(unitPriceText);
        var discountPercentage = 0m;
        var expectedTotal = ParseDecimal(expectedTotalText);

        // Act
        var actualTotal = calculator.CalculateTotal(unitPrice, quantity, discountPercentage);

        // Assert
        Assert.Equal(expectedTotal, actualTotal);
    }

    [Fact]
    public void CalculateTotal_WithPriceQuantityAndDiscount_ReturnsDiscountedTotal()
    {
        // Arrange
        var calculator = new PriceCalculator();
        var unitPrice = 25.50m;
        var quantity = 3;
        var discountPercentage = 10m;
        var expectedTotal = 68.85m;

        // Act
        var actualTotal = calculator.CalculateTotal(unitPrice, quantity, discountPercentage);

        // Assert
        Assert.Equal(expectedTotal, actualTotal);
    }

    [Theory]
    [InlineData("100", 2, "25", "150")]
    [InlineData("19.99", 4, "15", "67.966")]
    [InlineData("10", 1, "33.33", "6.667")]
    [InlineData("0.10", 3, "50", "0.15")]
    [InlineData("80", 1, "0.5", "79.6")]
    [InlineData("1.005", 2, "10", "1.809")]
    [InlineData("0.01", 1000000, "12.5", "8750")]
    public void CalculateTotal_WithDifferentDiscounts_ReturnsDiscountedTotal(
        string unitPriceText,
        int quantity,
        string discountPercentageText,
        string expectedTotalText)
    {
        // Arrange
        var calculator = new PriceCalculator();
        var unitPrice = ParseDecimal(unitPriceText);
        var discountPercentage = ParseDecimal(discountPercentageText);
        var expectedTotal = ParseDecimal(expectedTotalText);

        // Act
        var actualTotal = calculator.CalculateTotal(unitPrice, quantity, discountPercentage);

        // Assert
        Assert.Equal(expectedTotal, actualTotal);
    }

    [Theory]
    [InlineData("25.50", "0")]
    [InlineData("25.50", "10")]
    [InlineData("99.99", "100")]
    [InlineData("0", "0")]
    [InlineData("0.01", "50")]
    public void CalculateTotal_WithQuantityOfZero_ReturnsZero(
        string unitPriceText,
        string discountPercentageText)
    {
        // Arrange
        var calculator = new PriceCalculator();
        var unitPrice = ParseDecimal(unitPriceText);
        var quantity = 0;
        var discountPercentage = ParseDecimal(discountPercentageText);
        var expectedTotal = 0m;

        // Act
        var actualTotal = calculator.CalculateTotal(unitPrice, quantity, discountPercentage);

        // Assert
        Assert.Equal(expectedTotal, actualTotal);
    }

    [Theory]
    [InlineData("25.50", 3)]
    [InlineData("0.01", 1)]
    [InlineData("1000", 100)]
    [InlineData("19.99", 4)]
    public void CalculateTotal_WithDiscountOf100Percent_ReturnsZero(
        string unitPriceText,
        int quantity)
    {
        // Arrange
        var calculator = new PriceCalculator();
        var unitPrice = ParseDecimal(unitPriceText);
        var discountPercentage = 100m;
        var expectedTotal = 0m;

        // Act
        var actualTotal = calculator.CalculateTotal(unitPrice, quantity, discountPercentage);

        // Assert
        Assert.Equal(expectedTotal, actualTotal);
    }

    [Fact]
    public void CalculateTotal_WithZeroUnitPrice_ReturnsZero()
    {
        // Arrange
        var calculator = new PriceCalculator();
        var unitPrice = 0m;
        var quantity = 4;
        var discountPercentage = 10m;
        var expectedTotal = 0m;

        // Act
        var actualTotal = calculator.CalculateTotal(unitPrice, quantity, discountPercentage);

        // Assert
        Assert.Equal(expectedTotal, actualTotal);
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("-1")]
    [InlineData("-999999.99")]
    public void CalculateTotal_WithNegativeUnitPrice_ThrowsArgumentOutOfRangeException(
        string unitPriceText)
    {
        // Arrange
        var calculator = new PriceCalculator();
        var unitPrice = ParseDecimal(unitPriceText);
        var quantity = 1;
        var discountPercentage = 0m;

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            calculator.CalculateTotal(unitPrice, quantity, discountPercentage));

        // Assert
        Assert.Equal("unitPrice", exception.ParamName);
    }

    [Fact]
    public void CalculateTotal_WithNegativeUnitPriceAndZeroQuantity_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var calculator = new PriceCalculator();
        var unitPrice = -1m;
        var quantity = 0;
        var discountPercentage = 0m;

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            calculator.CalculateTotal(unitPrice, quantity, discountPercentage));

        // Assert
        Assert.Equal("unitPrice", exception.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(int.MinValue)]
    public void CalculateTotal_WithNegativeQuantity_ThrowsArgumentOutOfRangeException(int quantity)
    {
        // Arrange
        var calculator = new PriceCalculator();
        var unitPrice = 10m;
        var discountPercentage = 0m;

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            calculator.CalculateTotal(unitPrice, quantity, discountPercentage));

        // Assert
        Assert.Equal("quantity", exception.ParamName);
    }

    [Fact]
    public void CalculateTotal_WithNegativeQuantityAndFullDiscount_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var calculator = new PriceCalculator();
        var unitPrice = 10m;
        var quantity = -1;
        var discountPercentage = 100m;

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            calculator.CalculateTotal(unitPrice, quantity, discountPercentage));

        // Assert
        Assert.Equal("quantity", exception.ParamName);
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("-1")]
    [InlineData("100.01")]
    [InlineData("100.1")]
    [InlineData("250")]
    public void CalculateTotal_WithDiscountBelowZeroOrAbove100_ThrowsArgumentOutOfRangeException(
        string discountPercentageText)
    {
        // Arrange
        var calculator = new PriceCalculator();
        var unitPrice = 25.50m;
        var quantity = 3;
        var discountPercentage = ParseDecimal(discountPercentageText);

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            calculator.CalculateTotal(unitPrice, quantity, discountPercentage));

        // Assert
        Assert.Equal("discountPercentage", exception.ParamName);
        Assert.Contains("between 0 and 100", exception.Message);
    }

    private static decimal ParseDecimal(string value) =>
        decimal.Parse(value, CultureInfo.InvariantCulture);
}
