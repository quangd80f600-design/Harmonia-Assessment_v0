using System.Security.Cryptography;
using Harmonia.Web.ViewModels;
using Microsoft.Extensions.Caching.Memory;

namespace Harmonia.Web.Services.Academic;

internal sealed class RosterTicket(string owner, RosterPreviewViewModel preview, ParsedRoster csv)
{
    public string Owner { get; } = owner;
    public RosterPreviewViewModel Preview { get; } = preview;
    public ParsedRoster Csv { get; } = csv;
    private int _used;
    public bool IsUsed => Volatile.Read(ref _used) != 0;
    public bool TryUse() => DateTimeOffset.UtcNow < Preview.ExpiresAt && Interlocked.CompareExchange(ref _used, 1, 0) == 0;
}
public sealed class RosterPreviewStore : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 32 });
    internal static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    internal bool Add(RosterTicket ticket)
    {
        _cache.Set(ticket.Preview.Token, ticket, new MemoryCacheEntryOptions { AbsoluteExpiration = ticket.Preview.ExpiresAt, Size = 1 });
        return _cache.TryGetValue(ticket.Preview.Token, out _);
    }
    internal RosterTicket? Get(long classId, string token, string owner) => token.Length == 64
        && _cache.TryGetValue<RosterTicket>(token, out var ticket) && ticket!.Owner == owner
        && ticket.Preview.Class.ClassId == classId && !ticket.IsUsed && DateTimeOffset.UtcNow < ticket.Preview.ExpiresAt ? ticket : null;
    public void Dispose() => _cache.Dispose();
}
