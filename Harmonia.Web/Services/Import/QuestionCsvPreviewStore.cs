using System.Security.Cryptography;
using Harmonia.Web.ViewModels;
using Microsoft.Extensions.Caching.Memory;

namespace Harmonia.Web.Services.Import;

internal sealed class QuestionCsvTicket(string owner, QuestionCsvPreview preview)
{
    public string Owner { get; } = owner;
    public QuestionCsvPreview Preview { get; } = preview;
    private int _used;
    public bool IsUsed => Volatile.Read(ref _used) != 0;
    public bool TryUse(DateTimeOffset now) => now < Preview.ExpiresAt && Interlocked.CompareExchange(ref _used, 1, 0) == 0;
}

public sealed class QuestionCsvPreviewStore : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 16 });
    internal bool Add(QuestionCsvTicket ticket)
    {
        _cache.Set(ticket.Preview.Token, ticket, new MemoryCacheEntryOptions { AbsoluteExpiration = ticket.Preview.ExpiresAt, Size = 1 });
        return _cache.TryGetValue(ticket.Preview.Token, out _);
    }
    internal QuestionCsvTicket? Get(string token, string owner) => token.Length == 64
        && _cache.TryGetValue<QuestionCsvTicket>(token, out var ticket) && ticket!.Owner == owner
        && !ticket.IsUsed && DateTimeOffset.UtcNow < ticket.Preview.ExpiresAt ? ticket : null;
    internal static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    internal QuestionCsvTicket? Take(string token, string owner)
    {
        var ticket = Get(token, owner);
        if (ticket is null || !ticket.TryUse(DateTimeOffset.UtcNow)) return null;
        _cache.Remove(token);
        return ticket;
    }
    public void Dispose() => _cache.Dispose();
}
