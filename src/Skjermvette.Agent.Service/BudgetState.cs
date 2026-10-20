using System.ComponentModel;
using Skjermvette.Core;
using Skjermvette.Core.Models;

namespace Skjermvette.Agent.Service;

/// <summary>What this device may use right now. Enforcement reads it; the sync loop and server pushes keep it current.</summary>
public sealed class BudgetState(ILogger<BudgetState> logger)
{
    private readonly Lock _lock = new();

    public ConnectionEnum? Connection { get; private set; }

    /// <summary>Null until the first sync, and once unpaired.</summary>
    public BudgetSnapshot? Snapshot { get; private set; }

    /// <summary>Maps what is in use to the family's apps. Null until the catalog is first read.</summary>
    public AppResolver? Apps { get; private set; }

    /// <summary>The family's apps, which <see cref="Apps"/> resolves against. Empty until first read.</summary>
    public IReadOnlyList<LogicalApp> Catalog { get; private set; } = [];

    /// <summary>The server's host name, so the dashboard's website is never blocked. Null until the device's identity is loaded.</summary>
    public string? Dashboard { get; set; }

    public void SetCatalog(IReadOnlyList<LogicalApp> catalog)
    {
        Catalog = catalog;
        Apps = new AppResolver(catalog);
    }

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
