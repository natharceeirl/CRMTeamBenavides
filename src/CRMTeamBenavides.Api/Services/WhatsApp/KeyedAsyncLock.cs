using System.Collections.Concurrent;

namespace CRMTeamBenavides.Api.Services.WhatsApp;

/// <summary>
/// Lock asíncrono por clave con conteo de referencias que elimina automáticamente las entradas sin uso
/// (sin crecimiento ilimitado de memoria). Complementa —y nunca reemplaza— las restricciones únicas
/// y bloqueos <c>FOR UPDATE SKIP LOCKED</c> de PostgreSQL en despliegues multiinstancia.
/// </summary>
internal sealed class KeyedAsyncLock<TKey> where TKey : notnull
{
    private sealed class Entry
    {
        public readonly SemaphoreSlim Semaphore = new(1, 1);
        public int RefCount;
    }

    private readonly Dictionary<TKey, Entry> _entries;
    private readonly object _gate = new();

    public KeyedAsyncLock(IEqualityComparer<TKey>? comparer = null)
    {
        _entries = new Dictionary<TKey, Entry>(comparer);
    }

    public async Task<IDisposable> AcquireAsync(TKey key, CancellationToken ct = default)
    {
        Entry entry;
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out entry!))
            {
                entry = new Entry();
                _entries[key] = entry;
            }

            entry.RefCount++;
        }

        try
        {
            await entry.Semaphore.WaitAsync(ct);
            return new Releaser(this, key, entry);
        }
        catch
        {
            ReleaseReferenceOnly(key, entry);
            throw;
        }
    }

    private void Release(TKey key, Entry entry)
    {
        bool disposeSemaphore = false;
        lock (_gate)
        {
            entry.Semaphore.Release();
            entry.RefCount--;
            if (entry.RefCount == 0 && _entries.TryGetValue(key, out var current) && ReferenceEquals(current, entry))
            {
                _entries.Remove(key);
                disposeSemaphore = true;
            }
        }

        if (disposeSemaphore)
        {
            entry.Semaphore.Dispose();
        }
    }

    private void ReleaseReferenceOnly(TKey key, Entry entry)
    {
        bool disposeSemaphore = false;
        lock (_gate)
        {
            entry.RefCount--;
            if (entry.RefCount == 0 && _entries.TryGetValue(key, out var current) && ReferenceEquals(current, entry))
            {
                _entries.Remove(key);
                disposeSemaphore = true;
            }
        }

        if (disposeSemaphore)
        {
            entry.Semaphore.Dispose();
        }
    }

    private sealed class Releaser(KeyedAsyncLock<TKey> owner, TKey key, Entry entry) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.Release(key, entry);
            }
        }
    }
}
