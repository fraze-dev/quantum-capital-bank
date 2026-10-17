using System.Globalization;

namespace BankApi.Services;

// Formats amounts as US dollars no matter how the server is configured.
// "{amount:C}" follows the process's culture, which differs between machines:
// Lambda and dev PCs print "$100.00", but a Linux CI runner prints "¤100.00".
public static class Money
{
    public static string Format(decimal amount) =>
        (amount < 0 ? "-$" : "$") + Math.Abs(amount).ToString("N2", CultureInfo.InvariantCulture);
}
