using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Memory;
using ECommons.Automation;
using ECommons.Automation.NeoTaskManager;
using ECommons.Automation.UIInput;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using Callback = ECommons.Automation.Callback;

namespace GilTradeTracker.Services;

public static unsafe class PayoutAutomation
{
    private const int MaxGilPerTrade = 1_000_000;
    private static readonly TaskManager TaskManager = new();

    public static bool IsBusy => TaskManager.IsBusy;

    public static void Abort() => TaskManager.Abort();

    public static void Pay(string targetNameWithWorld, long gilAmount)
    {
        if (gilAmount <= 0) return;
        long remaining = gilAmount;
        while (remaining > 0)
        {
            int chunk = (int)Math.Min(MaxGilPerTrade, remaining);
            remaining -= chunk;

            TaskManager.Enqueue(() => UseTradeOn(targetNameWithWorld), $"UseTradeOn({targetNameWithWorld})");
            TaskManager.Enqueue(WaitUntilTradeOpen);
            TaskManager.Enqueue(OpenGilInput);
            TaskManager.Enqueue(() => SetNumericInput(chunk), $"SetNumericInput({chunk})");
            TaskManager.Enqueue(ConfirmTrade);
            TaskManager.Enqueue(WaitUntilTradeClosed);
            TaskManager.EnqueueDelay(250);
        }
    }

    private static bool UseTradeOn(string playerNameWithWorld)
    {
        var target = Svc.Objects.OfType<IPlayerCharacter>()
            .FirstOrDefault(p => p.IsTargetable && p.GetNameWithWorld() == playerNameWithWorld);
        if (target == null) return false;

        if (Svc.Targets.Target?.Address != target.Address)
        {
            Svc.Targets.Target = target;
            return false;
        }
        if (!EzThrottler.Throttle("PayoutTradeOpen", 2000)) return false;
        Chat.SendMessage("/trade");
        return true;
    }

    private static bool WaitUntilTradeOpen() => Svc.Condition[ConditionFlag.TradeOpen];
    private static bool WaitUntilTradeClosed() => !Svc.Condition[ConditionFlag.TradeOpen];

    private static bool OpenGilInput()
    {
        if (!TryGetAddonByName<AtkUnitBase>("Trade", out var addon)) return false;
        if (!IsAddonReady(addon)) return false;
        Callback.Fire(addon, true, 2, Callback.ZeroAtkValue);
        return true;
    }

    private static bool SetNumericInput(int amount)
    {
        if (!TryGetAddonByName<AtkUnitBase>("InputNumeric", out var addon)) return false;
        if (!IsAddonReady(addon)) return false;
        Callback.Fire(addon, true, amount);
        return true;
    }

    private static bool ConfirmTrade()
    {
        if (TryGetAddonByName<AtkUnitBase>("Trade", out var tradeAddon) && IsAddonReady(tradeAddon))
        {
            var tradeButton = (AtkComponentButton*)(tradeAddon->UldManager.NodeList[3]->GetComponent());
            if (EzThrottler.Check("PayoutLockTrade")
                && tradeButton->IsEnabled
                && EzThrottler.Throttle("PayoutConfirmDelay", 200)
                && EzThrottler.Throttle("PayoutLockTrade", 2000))
            {
                tradeButton->ClickAddonButton(tradeAddon);
            }
        }

        var yesno = GetTradeConfirmYesno();
        if (yesno != null
            && EzThrottler.Throttle("PayoutConfirmDelay", 200)
            && EzThrottler.Throttle("PayoutSelectYes", 2000))
        {
            new AddonMaster.SelectYesno(yesno).Yes();
        }

        return !TryGetAddonByName<AtkUnitBase>("Trade", out _);
    }

    private static AtkUnitBase* GetTradeConfirmYesno()
    {
        // Excel row 102223 is FFXIV's "Trade with this party?" body text - we
        // match against it to find the right SelectYesno when several may exist.
        var tradeText = Svc.Data.GetExcelSheet<Addon>().GetRowOrDefault(102223)?.Text.ExtractText();
        if (string.IsNullOrEmpty(tradeText)) return null;

        for (int i = 1; i < 100; i++)
        {
            try
            {
                var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("SelectYesno", i).Address;
                if (addon == null) return null;
                if (!IsAddonReady(addon)) continue;
                var textNode = addon->UldManager.NodeList[15]->GetAsAtkTextNode();
                var text = MemoryHelper.ReadSeString(&textNode->NodeText).ExtractText();
                if (text == tradeText) return addon;
            }
            catch
            {
                return null;
            }
        }
        return null;
    }
}
