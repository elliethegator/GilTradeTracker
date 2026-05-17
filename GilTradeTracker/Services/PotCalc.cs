namespace GilTradeTracker.Services;

public static class PotCalc
{
    public static (long ticketSales, long venueCut, long fullPot) Compute()
    {
        var ticketSales = C.Entries.Where(e => e.Amount > 0).Sum(e => e.Amount);
        var venueCut = (long)Math.Round(ticketSales * C.VenuePercent / 100.0);
        var fullPot = C.BasePrizePot + (ticketSales - venueCut);
        return (ticketSales, venueCut, fullPot);
    }

    public static string RenderShout(string? template)
    {
        var (ticketSales, venueCut, fullPot) = Compute();
        var tickets = C.PricePerTicket > 0 ? ticketSales / C.PricePerTicket : 0;

        return (template ?? string.Empty)
            .Replace("{pot}",         $"{fullPot:N0}")
            .Replace("{ticketPrice}", $"{C.PricePerTicket:N0}")
            .Replace("{basePot}",     $"{C.BasePrizePot:N0}")
            .Replace("{ticketSales}", $"{ticketSales:N0}")
            .Replace("{venueCut}",    $"{venueCut:N0}")
            .Replace("{venuePct}",    $"{C.VenuePercent}")
            .Replace("{tickets}",     $"{tickets:N0}");
    }
}
