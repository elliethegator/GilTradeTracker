using System.Diagnostics;
using System.IO;
using System.Text;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Interface.ManagedFontAtlas;
using ECommons.Configuration;
using ECommons.GameHelpers;

namespace GilTradeTracker.UI;

public sealed class MainWindow : Window, IDisposable
{
    private readonly ConfigWindow configWindow;
    private readonly IFontHandle headlineFont;

    private const string AddEntryPopupId = "##add_entry_popup";
    private string popupPlayerName = string.Empty;
    private int popupGilAmount = 0;
    private string popupError = string.Empty;

    private const string PayoutConfirmPopupId = "##payout_confirm";
    private string payoutTargetName = string.Empty;
    private long payoutAmount = 0;

    public MainWindow(ConfigWindow configWindow) : base("Gil Trade Tracker")
    {
        this.configWindow = configWindow;

        Size = new Vector2(540f, 560f);
        SizeCondition = ImGuiCond.FirstUseEver;

        TitleBarButtons.Add(new TitleBarButton
        {
            Icon = FontAwesomeIcon.Cog,
            Click = _ => configWindow.IsOpen = !configWindow.IsOpen,
            ShowTooltip = () => ImGui.SetTooltip("Settings"),
        });

        headlineFont = Svc.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e =>
            e.OnPreBuild(tk => tk.AddDalamudDefaultFont(28f)));
    }

    public void Dispose()
    {
        headlineFont.Dispose();
    }

    public override void Draw()
    {
        DrawPotCard();
        DrawActionRow();
        DrawPayoutSummary();
        ImGui.Spacing();
        DrawEntriesHeader();
        DrawEntriesTable();
        DrawAddEntryPopup();
        DrawPayoutConfirmPopup();
    }

    private static void DrawPayoutSummary()
    {
        if (PayoutAutomation.TargetTotal <= 0) return;

        ImGui.Spacing();
        using (ImRaii.PushColor(ImGuiCol.Text, Theme.Subtle))
            ImGui.Text("Auto-traded payout:");
        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Text, Theme.Received))
            ImGui.Text($"{PayoutAutomation.AmountTraded:N0}");
        ImGui.SameLine();
        ImGui.Text("/");
        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Text, Theme.Amber))
            ImGui.Text($"{PayoutAutomation.TargetTotal:N0}");
        ImGui.SameLine();
        using (ImRaii.PushColor(ImGuiCol.Text, Theme.Subtle))
            ImGui.Text($"gil to {PayoutAutomation.TargetName}");
    }

    private static void Status(string msg) => Svc.Chat.Print($"[GilTradeTracker] {msg}");

    private static void StatusError(string msg) => Svc.Chat.PrintError($"[GilTradeTracker] {msg}");

    private void DrawPotCard()
    {
        var (ticketSales, venueCut, fullPot) = PotCalc.Compute();

        DrawCard("##pot_card", "PRIZE POT", () =>
        {
            ImGui.Spacing();
            using (headlineFont.Push())
            using (ImRaii.PushColor(ImGuiCol.Text, Theme.Gold))
                CenterText($"{fullPot:N0} gil");
            ImGui.Spacing();

            var ticketLine = C.PricePerTicket > 0
                ? $"Ticket Sales: {ticketSales:N0}   ({ticketSales / C.PricePerTicket:N0} tickets)"
                : $"Ticket Sales: {ticketSales:N0}";

            using (ImRaii.PushColor(ImGuiCol.Text, Theme.Received))
                ImGui.Text(ticketLine);
            using (ImRaii.PushColor(ImGuiCol.Text, Theme.Amber))
                ImGui.Text($"Venue Cut ({C.VenuePercent}%): {venueCut:N0}");
            using (ImRaii.PushColor(ImGuiCol.Text, Theme.Cyan))
                ImGui.Text($"Base Prize: {C.BasePrizePot:N0}");
        });
    }

    private static void DrawCard(string id, string title, Action drawBody)
    {
        var gs = ImGuiHelpers.GlobalScale;
        var p0 = ImGui.GetCursorScreenPos();
        var availW = ImGui.GetContentRegionAvail().X;
        var dl = ImGui.GetWindowDrawList();
        var headerH = 24f * gs;

        dl.ChannelsSplit(2);
        dl.ChannelsSetCurrent(1);

        ImGui.PushID(id);
        ImGui.BeginGroup();
        ImGui.Dummy(new Vector2(0f, headerH));
        ImGui.Indent(14f * gs);
        ImGui.Dummy(new Vector2(0f, 4f * gs));
        drawBody();
        ImGui.Dummy(new Vector2(0f, 8f * gs));
        ImGui.Unindent(14f * gs);
        ImGui.EndGroup();
        ImGui.PopID();

        var p1 = new Vector2(p0.X + availW, ImGui.GetCursorScreenPos().Y);

        dl.ChannelsSetCurrent(0);
        dl.AddRectFilled(p0, p1, ImGui.ColorConvertFloat4ToU32(Theme.PotCardBg), 6f * gs);
        var bandP1 = new Vector2(p1.X, p0.Y + headerH);
        dl.AddRectFilled(p0, bandP1, ImGui.ColorConvertFloat4ToU32(Theme.PotHeaderBg), 6f * gs,
            ImDrawFlags.RoundCornersTop);

        var titleSize = ImGui.CalcTextSize(title);
        var titlePos = new Vector2(
            p0.X + (availW - titleSize.X) / 2f,
            p0.Y + (headerH - titleSize.Y) / 2f);
        dl.AddText(titlePos, ImGui.ColorConvertFloat4ToU32(Theme.PotHeaderText), title);

        dl.ChannelsMerge();
        ImGui.Dummy(new Vector2(0f, 10f * gs));
    }

    private void DrawActionRow()
    {
        var btnSize = new Vector2(140f, 32f);

        if (ImGuiEx.IconButtonWithText(
                FontAwesomeIcon.Bullhorn, "Advertise",
                defaultColor: Theme.PrimaryBtn,
                activeColor:  Theme.PrimaryBtnActive,
                hoveredColor: Theme.PrimaryBtnHover,
                size: btnSize))
            Advertise();
        ImGuiEx.Tooltip($"Send the configured shout via {C.AdvertiseChannel}.\nChange channel or message in Settings.");

        ImGui.SameLine();

        if (PayoutAutomation.IsBusy)
        {
            if (ImGuiEx.IconButtonWithText(
                    FontAwesomeIcon.Stop, "Stop",
                    defaultColor: Theme.DangerBtn,
                    activeColor:  Theme.DangerBtnActive,
                    hoveredColor: Theme.DangerBtnHover,
                    size: btnSize))
            {
                PayoutAutomation.Abort();
                Status("Payout aborted.");
            }
            ImGuiEx.Tooltip("Stop the queued payout steps.\nAny open trade window is left alone - close it manually if needed.");
        }
        else
        {
            var targetName = GetCurrentTargetPlayerWithWorld();
            var (_, _, fullPot) = PotCalc.Compute();
            var canPayout = !string.IsNullOrEmpty(targetName) && fullPot > 0;

            if (ImGuiEx.IconButtonWithText(
                    FontAwesomeIcon.HandHoldingUsd, "Auto Trade Pot",
                    enabled: canPayout,
                    defaultColor: Theme.DangerBtn,
                    activeColor:  Theme.DangerBtnActive,
                    hoveredColor: Theme.DangerBtnHover,
                    size: btnSize))
            {
                payoutTargetName = targetName;
                payoutAmount = fullPot;
                ImGui.OpenPopup(PayoutConfirmPopupId);
            }
            ImGuiEx.Tooltip(BuildPayoutTooltip(targetName, fullPot));
        }

        if (ImGuiEx.IconButtonWithText(
                FontAwesomeIcon.Plus, "Add Entry",
                defaultColor: Theme.AccentBtn,
                activeColor:  Theme.AccentBtnActive,
                hoveredColor: Theme.AccentBtnHover,
                size: btnSize))
            OpenAddEntryPopup();
        ImGuiEx.Tooltip("Manually record a gil trade.");

        ImGui.SameLine();

        if (ImGuiEx.IconButtonWithText(FontAwesomeIcon.Download, "Export CSV", size: btnSize))
            ExportCsv();
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            OpenConfigFolder();
        ImGuiEx.Tooltip("Left-click: write the entries to a CSV in the plugin config folder.\nRight-click: open that folder in Explorer.");
    }

    private static string BuildPayoutTooltip(string targetName, long fullPot)
    {
        if (string.IsNullOrEmpty(targetName)) return "Target a player first.";
        if (fullPot <= 0) return "Pot is 0 - nothing to pay.";
        return $"Pay {fullPot:N0} gil to {targetName}.\nHold Ctrl on the confirm screen to send.";
    }

    private void DrawEntriesHeader()
    {
        using (ImRaii.PushColor(ImGuiCol.Text, Theme.Subtle))
            ImGui.Text("Recent Entries");

        ImGui.SameLine();
        var totalGil = C.Entries.Sum(x => x.Amount);
        var netColor = totalGil > 0 ? Theme.Received : totalGil < 0 ? Theme.Paid : Theme.Muted;
        using (ImRaii.PushColor(ImGuiCol.Text, netColor))
            ImGui.Text($"   Net: {totalGil:+#,0;-#,0;0} gil");

        var deleteW = 120f;
        ImGui.SameLine();
        var avail = ImGui.GetContentRegionAvail().X;
        if (avail > deleteW)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + avail - deleteW);

        if (ImGuiEx.IconButtonWithText(
                FontAwesomeIcon.Trash, "Delete All",
                enabled: ImGuiEx.Ctrl && ImGuiEx.Shift,
                defaultColor: Theme.DangerBtn,
                activeColor:  Theme.DangerBtnActive,
                hoveredColor: Theme.DangerBtnHover,
                size: new Vector2(deleteW, 30f)))
        {
            C.Entries.Clear();
            EzConfig.Save();
            Status("All entries deleted.");
        }
        ImGuiEx.Tooltip("Hold Ctrl + Shift to delete all entries.");

        ImGui.Separator();
    }

    private void DrawEntriesTable()
    {
        if (C.Entries.Count == 0)
        {
            ImGui.Spacing();
            using (ImRaii.PushColor(ImGuiCol.Text, Theme.Muted))
                ImGui.TextWrapped("No entries yet. Open a trade in-game or use the Add Entry button to record one manually.");
            return;
        }

        using var table = ImRaii.Table("gil_entries_table", 4,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.ScrollY);
        if (!table.Success) return;

        ImGui.TableSetupColumn("Player", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Gil",    ImGuiTableColumnFlags.WidthFixed, 110f);
        ImGui.TableSetupColumn("Date",   ImGuiTableColumnFlags.WidthFixed, 120f);
        ImGui.TableSetupColumn("",       ImGuiTableColumnFlags.WidthFixed, 80f);
        ImGui.TableHeadersRow();

        for (var i = C.Entries.Count - 1; i >= 0; i--)
        {
            var entry = C.Entries[i];

            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.Text(entry.PlayerName);

            ImGui.TableNextColumn();
            var amountColor = entry.Amount > 0 ? Theme.Received : entry.Amount < 0 ? Theme.Paid : Theme.Muted;
            using (ImRaii.PushColor(ImGuiCol.Text, amountColor))
                ImGui.Text($"{entry.Amount:+#,0;-#,0;0}");

            ImGui.TableNextColumn();
            using (ImRaii.PushColor(ImGuiCol.Text, Theme.Muted))
                ImGui.Text(entry.Date.ToString("MM-dd HH:mm"));

            ImGui.TableNextColumn();
            using (ImRaii.PushColor(ImGuiCol.Button,        Theme.DangerBtn))
            using (ImRaii.PushColor(ImGuiCol.ButtonHovered, Theme.DangerBtnHover))
            using (ImRaii.PushColor(ImGuiCol.ButtonActive,  Theme.DangerBtnActive))
            {
                if (ImGuiEx.Button($"Delete##{i}", enabled: ImGuiEx.Ctrl))
                {
                    C.Entries.RemoveAt(i);
                    EzConfig.Save();
                    Status("Entry deleted.");
                }
            }
            ImGuiEx.Tooltip("Hold Ctrl to delete this entry.");
        }
    }

    private void OpenAddEntryPopup()
    {
        popupPlayerName = GetCurrentTargetName();
        popupGilAmount = 0;
        popupError = string.Empty;
        ImGui.OpenPopup(AddEntryPopupId);
    }

    private void DrawAddEntryPopup()
    {
        using var popup = ImRaii.Popup(AddEntryPopupId);
        if (!popup.Success) return;

        using (ImRaii.PushColor(ImGuiCol.Text, Theme.Gold))
            ImGui.Text("Add Manual Entry");
        ImGui.Separator();

        ImGui.SetNextItemWidth(220f);
        ImGui.InputText("Player", ref popupPlayerName, 100);

        ImGui.SetNextItemWidth(220f);
        ImGuiEx.InputFancyNumeric("Gil", ref popupGilAmount, 1000);

        ImGui.Spacing();

        var popupBtnSize = new Vector2(100f, 30f);

        if (ImGuiEx.IconButtonWithText(
                FontAwesomeIcon.Check, "Add",
                defaultColor: Theme.AccentBtn,
                activeColor:  Theme.AccentBtnActive,
                hoveredColor: Theme.AccentBtnHover,
                size: popupBtnSize)
            && TrySubmitAddEntry())
            ImGui.CloseCurrentPopup();
        ImGui.SameLine();
        if (ImGuiEx.IconButtonWithText(FontAwesomeIcon.Times, "Cancel", size: popupBtnSize))
            ImGui.CloseCurrentPopup();

        if (!string.IsNullOrWhiteSpace(popupError))
        {
            ImGui.Spacing();
            using (ImRaii.PushColor(ImGuiCol.Text, Theme.Paid))
                ImGui.TextWrapped(popupError);
        }
    }

    private void DrawPayoutConfirmPopup()
    {
        var center = ImGui.GetMainViewport().GetCenter();
        ImGui.SetNextWindowPos(center, ImGuiCond.Always, new Vector2(0.5f, 0.5f));

        using var popup = ImRaii.PopupModal(PayoutConfirmPopupId,
            ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar);
        if (!popup.Success) return;

        using (headlineFont.Push())
        using (ImRaii.PushColor(ImGuiCol.Text, Theme.Gold))
            ImGui.Text("Confirm Payout");

        ImGui.Separator();
        ImGui.Spacing();

        using (ImRaii.PushColor(ImGuiCol.Text, Theme.Subtle))
            ImGui.Text("Pay to:");
        using (ImRaii.PushColor(ImGuiCol.Text, Theme.Cyan))
            ImGui.Text(payoutTargetName);

        ImGui.Spacing();

        using (ImRaii.PushColor(ImGuiCol.Text, Theme.Subtle))
            ImGui.Text("Amount:");
        using (headlineFont.Push())
        using (ImRaii.PushColor(ImGuiCol.Text, Theme.Gold))
            ImGui.Text($"{payoutAmount:N0} gil");

        ImGui.Spacing();
        using (ImRaii.PushColor(ImGuiCol.Text, Theme.Subtle))
            ImGui.TextWrapped("This will target the player, open /trade, fill in the gil amount, lock, and confirm. Large amounts are split into 1,000,000-gil chunks.");
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        var confirmSize = new Vector2(180f, 32f);
        var cancelSize  = new Vector2(110f, 32f);

        if (ImGuiEx.IconButtonWithText(
                FontAwesomeIcon.HandHoldingUsd, "Confirm Payout",
                enabled: ImGuiEx.Ctrl,
                defaultColor: Theme.DangerBtn,
                activeColor:  Theme.DangerBtnActive,
                hoveredColor: Theme.DangerBtnHover,
                size: confirmSize))
        {
            PayoutAutomation.Pay(payoutTargetName, payoutAmount);
            Status($"Trading {payoutAmount:N0} gil to {payoutTargetName}...");
            ImGui.CloseCurrentPopup();
        }
        ImGuiEx.Tooltip("Hold Ctrl to confirm - this starts a real trade.");

        ImGui.SameLine();
        if (ImGuiEx.IconButtonWithText(FontAwesomeIcon.Times, "Cancel", size: cancelSize))
            ImGui.CloseCurrentPopup();
    }

    private bool TrySubmitAddEntry()
    {
        if (string.IsNullOrWhiteSpace(popupPlayerName))
        {
            popupError = "Please enter a player name or target a player.";
            return false;
        }
        if (popupGilAmount <= 0)
        {
            popupError = "Gil amount must be higher than 0.";
            return false;
        }

        C.Entries.Add(new GilEntry
        {
            PlayerName = popupPlayerName.Trim(),
            Amount = popupGilAmount,
            Date = DateTime.Now,
        });
        EzConfig.Save();
        Status($"Added {popupGilAmount:N0} gil for {popupPlayerName.Trim()}.");
        return true;
    }

    private void Advertise()
    {
        var msg = PotCalc.RenderShout(C.ShoutMessage);
        if (string.IsNullOrWhiteSpace(msg))
        {
            StatusError("Advertise message is empty - configure it in Settings.");
            return;
        }

        if (C.AdvertiseChannel == AdvertiseChannel.Clipboard)
        {
            ImGui.SetClipboardText(msg);
            Status("Copied to clipboard.");
            return;
        }

        var cmd = C.AdvertiseChannel switch
        {
            AdvertiseChannel.Shout => "/shout",
            AdvertiseChannel.Yell  => "/yell",
            AdvertiseChannel.Say   => "/say",
            AdvertiseChannel.Party => "/p",
            _                      => "/shout",
        };

        var lines = msg.Split('\n')
            .Select(l => l.Replace("\r", string.Empty).Trim())
            .Where(l => l.Length > 0)
            .SelectMany(ChunkLine)
            .ToList();

        if (lines.Count == 0)
        {
            StatusError("Advertise message has no content - configure it in Settings.");
            return;
        }

        foreach (var line in lines)
            ChatQueue.Enqueue($"{cmd} {line}");

        Status(lines.Count == 1
            ? $"Advertised in {cmd}."
            : $"Queued {lines.Count} lines to {cmd}.");
    }

    private void OpenConfigFolder()
    {
        var path = Svc.PluginInterface.ConfigDirectory.FullName;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, "[GilTradeTracker] Failed to open config folder");
            StatusError($"Failed to open folder: {ex.Message}");
        }
    }

    private void ExportCsv()
    {
        var configDirectory = Svc.PluginInterface.ConfigDirectory.FullName;
        var exportPath = Path.Combine(configDirectory, "GilTradeTracker_Export.csv");

        var csv = new StringBuilder();
        csv.AppendLine("Player Name,Gil Amount,Date");
        foreach (var entry in C.Entries)
            csv.AppendLine($"{EscapeCsv(entry.PlayerName)},{entry.Amount},{entry.Date:yyyy-MM-dd HH:mm:ss}");

        File.WriteAllText(exportPath, csv.ToString(), Encoding.UTF8);
        Status($"Exported to: {exportPath}");
    }

    private static IEnumerable<string> ChunkLine(string line)
    {
        const int max = 400;
        for (var i = 0; i < line.Length; i += max)
            yield return line.Substring(i, Math.Min(max, line.Length - i));
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private static string GetCurrentTargetName()
    {
        var target = Svc.Targets.Target;
        return target?.Name.ToString() ?? string.Empty;
    }

    private static string GetCurrentTargetPlayerWithWorld()
    {
        if (Svc.Targets.Target is IPlayerCharacter pc)
            return pc.GetNameWithWorld();
        return string.Empty;
    }

    private static void CenterText(string text)
    {
        var w = ImGui.CalcTextSize(text).X;
        var avail = ImGui.GetContentRegionAvail().X;
        if (avail > w)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + (avail - w) * 0.5f);
        ImGui.Text(text);
    }
}
