namespace Skjermvette.Agent.Service;

/// <summary>
/// Which program the pipe takes samples from. Once installed (SYSTEM), the Service starts the Helper itself, so only that
/// process is taken; any other program of the user's could send made-up samples. In a console the Helper is started by
/// hand, so any client is taken.
/// </summary>
public sealed class HelperProcess(bool supervised)
{
    // 0 when no Helper is running: no client has that process id (it's the System Idle Process).
    private volatile int _id;

    /// <summary>The Service started the Helper as process <paramref name="id"/>.</summary>
    public void Started(int id) => _id = id;

    /// <summary>The Helper the Service started is no longer running.</summary>
    public void Stopped() => _id = 0;

    /// <summary>Whether to take samples from the pipe client running as process <paramref name="clientId"/>.</summary>
    public bool Accepts(int clientId) => !supervised || (clientId != 0 && clientId == _id);
}
