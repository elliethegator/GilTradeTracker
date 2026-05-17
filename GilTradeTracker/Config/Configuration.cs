using Dalamud.Configuration;
using ECommons.Configuration;

namespace GilTradeTracker.Config;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public List<GilEntry> Entries { get; set; } = new();

    public int BasePrizePot { get; set; } = 0;

    public int PricePerTicket { get; set; } = 0;

    public int VenuePercent { get; set; } = 0;

    public string ShoutMessage { get; set; } = "Current Prize Pot: {pot} gil! Tickets are {ticketPrice} gil each.";

    public AdvertiseChannel AdvertiseChannel { get; set; } = AdvertiseChannel.Shout;
}

public sealed class GilEntry
{
    public string PlayerName { get; set; } = string.Empty;

    public long Amount { get; set; }

    public DateTime Date { get; set; } = DateTime.Now;
}
