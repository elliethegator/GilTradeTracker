using Dalamud.Game.ClientState.Conditions;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using static ECommons.GameHelpers.TradeDetectionManager;

namespace GilTradeTracker.Services;

public unsafe sealed class GilTracker : IDisposable
{
    public bool Paused { get; set; }

    public event Action<string, long>? TradeDetected;

    private Dictionary<uint, uint>? _snapshot;
    private string? _partnerName;

    public GilTracker()
    {
        Svc.Condition.ConditionChange += OnConditionChange;
    }

    public void Dispose()
    {
        Svc.Condition.ConditionChange -= OnConditionChange;
    }

    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        if (flag != ConditionFlag.TradeOpen) return;

        if (value)
        {
            if (Paused) return;
            var partner = GetTradePartner();
            _partnerName = partner != null ? partner.GetNameWithWorld() : null;
            _snapshot = GetInventorySnapshot(ValidInventories);
        }
        else
        {
            try
            {
                if (Paused) return;
                if (_snapshot == null || string.IsNullOrEmpty(_partnerName)) return;
                var after = GetInventorySnapshot(ValidInventories);
                var gilDiff = (long)after.GetValueOrDefault(1u, 0u) - _snapshot.GetValueOrDefault(1u, 0u);
                if (gilDiff == 0) return;

                TradeDetected?.Invoke(_partnerName!, gilDiff);
            }
            catch (Exception ex)
            {
                Svc.Log.Error(ex, "[GilTradeTracker] GilTracker trade-close handling failed");
            }
            finally
            {
                _snapshot = null;
                _partnerName = null;
            }
        }
    }
}
