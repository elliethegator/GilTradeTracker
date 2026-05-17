using ECommons.Automation;

namespace GilTradeTracker.Services;

public static class ChatQueue
{
    private const int IntervalMs = 1300;

    private static readonly Queue<string> Queue = new();
    private static DateTime nextSendUtc = DateTime.MinValue;

    public static int PendingCount => Queue.Count;

    public static void Enqueue(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        Queue.Enqueue(message);
    }

    public static void Pump()
    {
        if (Queue.Count == 0) return;
        if (DateTime.UtcNow < nextSendUtc) return;

        var msg = Queue.Dequeue();
        try
        {
            Chat.SendMessage(msg);
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, $"[GilTradeTracker] ChatQueue: failed to send '{msg}'");
        }
        nextSendUtc = DateTime.UtcNow.AddMilliseconds(IntervalMs);
    }

    public static void Clear() => Queue.Clear();
}
