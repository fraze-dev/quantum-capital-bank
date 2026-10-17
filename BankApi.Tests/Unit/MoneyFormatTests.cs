using System.Globalization;
using BankApi.Services;

namespace BankApi.Tests.Unit;

public class MoneyFormatTests
{
    [Theory]
    [InlineData(100, "$100.00")]
    [InlineData(0, "$0.00")]
    [InlineData(1234.5, "$1,234.50")]
    [InlineData(-25, "-$25.00")]
    [InlineData(0.01, "$0.01")]
    public void Format_WritesUsDollars(decimal amount, string expected)
    {
        Assert.Equal(expected, Money.Format(amount));
    }

    // The bug this guards against: "{x:C}" printed "¤100.00" on a CI runner
    // whose culture was the invariant culture. The "European" culture is built
    // by hand so the test also runs where no named cultures are installed.
    [Theory]
    [InlineData(false)] // invariant culture
    [InlineData(true)]  // euro sign, comma decimals, dot thousands
    public void Format_IgnoresTheServersCulture(bool european)
    {
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        if (european)
        {
            culture.NumberFormat.CurrencySymbol = "€";
            culture.NumberFormat.NumberDecimalSeparator = ",";
            culture.NumberFormat.NumberGroupSeparator = ".";
            culture.NumberFormat.CurrencyDecimalSeparator = ",";
            culture.NumberFormat.CurrencyGroupSeparator = ".";
        }

        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = culture;
            Assert.Equal("$1,234.56", Money.Format(1234.56m));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
