namespace KeyBridge.Core;

/// <summary>Orders complete profile operations after loading and before shutdown.</summary>
public sealed class ProfileOperationQueue(Func<Task> initialize)
{
    private readonly object sync = new();
    private readonly SemaphoreSlim gate = new(1, 1);
    private Task? initialization;
    private Task? shutdown;
    private bool closing;

    public Task InitializeAsync()
    {
        lock (sync) return initialization ??= initialize();
    }

    public async Task<bool> RunAsync(Func<Task> operation)
    {
        await InitializeAsync();
        await gate.WaitAsync();
        try
        {
            lock (sync) { if (closing) return false; }
            await operation();
            return true;
        }
        finally { gate.Release(); }
    }

    public Task CloseAsync(Func<Task> close)
    {
        lock (sync)
        {
            closing = true;
            return shutdown ??= CloseCoreAsync(close);
        }
    }

    private async Task CloseCoreAsync(Func<Task> close)
    {
        // Even a failed initializer must permit cleanup. The caller decides whether
        // its loaded state is valid to save; defaults must not replace failed loads.
        try { await InitializeAsync(); }
        catch { }
        await gate.WaitAsync();
        try { await close(); }
        finally { gate.Release(); }
    }
}
