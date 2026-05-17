using ECommons.Configuration;

namespace GilTradeTracker.UI;

public sealed class ConfigWindow : Window, IDisposable
{
    private const float InputWidth = 250f;
    private const float MessageWidth = 380f;

    public ConfigWindow() : base("Gil Trade Tracker Settings")
    {
        Size = new Vector2(500f, 460f);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }

    public override void Draw()
    {
        DrawPrizePotSection();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        DrawAdvertiseSection();
    }

    private static void DrawPrizePotSection()
    {
        ImGui.TextDisabled("Prize Pot Settings");

        var basePot = C.BasePrizePot;
        ImGui.SetNextItemWidth(InputWidth);
        if (ImGuiEx.InputFancyNumeric("Base Prize Pot (gil)", ref basePot, 1000))
        {
            C.BasePrizePot = basePot;
            EzConfig.Save();
        }

        var pricePerTicket = C.PricePerTicket;
        ImGui.SetNextItemWidth(InputWidth);
        if (ImGuiEx.InputFancyNumeric("Price per Ticket (gil)", ref pricePerTicket, 100))
        {
            C.PricePerTicket = pricePerTicket;
            EzConfig.Save();
        }

        var venuePct = C.VenuePercent;
        ImGui.SetNextItemWidth(InputWidth);
        if (ImGuiEx.InputFancyNumeric("Venue % (of ticket sales)", ref venuePct, 1))
        {
            C.VenuePercent = Math.Clamp(venuePct, 0, 100);
            EzConfig.Save();
        }
    }

    private static void DrawAdvertiseSection()
    {
        ImGui.TextDisabled("Advertise");

        var ch = C.AdvertiseChannel;
        ImGui.SetNextItemWidth(InputWidth);
        if (ImGuiEx.EnumCombo("Channel", ref ch))
        {
            C.AdvertiseChannel = ch;
            EzConfig.Save();
        }

        var msg = C.ShoutMessage ?? string.Empty;
        ImGui.Text("Message");
        if (ImGuiEx.InputTextWrapMultilineExpanding("##shoutmsg", ref msg, 500, 2, 6, (int)MessageWidth))
        {
            C.ShoutMessage = msg;
            EzConfig.Save();
        }

        using (ImRaii.PushColor(ImGuiCol.Text, Theme.Muted))
            ImGui.TextWrapped("Placeholders: {pot}, {ticketPrice}, {basePot}, {ticketSales}, {venueCut}, {venuePct}, {tickets}");

        ImGui.Spacing();
        using (ImRaii.PushColor(ImGuiCol.Text, Theme.Subtle))
            ImGui.Text("Preview:");
        using (ImRaii.PushColor(ImGuiCol.Text, Theme.Gold))
            ImGui.TextWrapped(PotCalc.RenderShout(C.ShoutMessage));
    }
}
