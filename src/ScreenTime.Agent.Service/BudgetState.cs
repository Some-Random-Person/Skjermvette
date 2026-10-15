using System.ComponentModel;
using ScreenTime.Core.Models;

namespace ScreenTime.Agent.Service;

/// <summary>What this device may use right now. Enforcement reads it; the sync loop and server pushes keep it current.</summary>
public sealed class BudgetState(ILogger<BudgetState> logger)
{
    private readonly Lock _lock = new();

    public ConnectionEnum? Connection { get; private set; }

    /// <summary>Null until the first sync, and once unpaired.</summary>
    public BudgetSnapshot? Snapshot { get; private set; }

    public void Set(ConnectionEnum connection, BudgetSnapshot? snapshot)
    {
        lock (_lock)
        {
            if (connection != Connection)
                logger.LogInformation("Connection: {Connection}", connection);
            Connection = connection;
            Snapshot = snapshot;
            logger.LogDebug("Budget: {TotalRemaining} left in total", snapshot?.TotalRemaining);
        }
    }
}

public enum ConnectionEnum
{
    [Description("Online")]
    Online = 1,

    [Description("Offline, using the offline allowance")]
    Offline = 2,

    [Description("Unpaired: needs pairing again")]
    Unpaired = 3,
}
