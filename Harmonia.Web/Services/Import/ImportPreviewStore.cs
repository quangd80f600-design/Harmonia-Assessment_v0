using System.Security.Cryptography;
using Harmonia.Web.ViewModels;
using Microsoft.Extensions.Caching.Memory;

namespace Harmonia.Web.Services.Import;

public sealed class CsvImportOptions
{
    public bool Enabled { get; set; }
    public string DemoActorId { get; set; } = "harmonia-demo-importer";
}

internal sealed class ImportTicket(string owner, long assessmentId, string fingerprint,
    string fileName, ParsedCsv csv, ImportPreviewViewModel preview)
{
    public string Owner { get; } = owner;
    public long AssessmentId { get; } = assessmentId;
    public string Fingerprint { get; } = fingerprint;
    public string FileName { get; } = fileName;
    public ParsedCsv Csv { get; } = csv;
    public ImportPreviewViewModel Preview { get; } = preview;
    public SemaphoreSlim Gate { get; } = new(1, 1);
    private int _used;
    public bool IsUsed => Volatile.Read(ref _used) != 0;
    public bool TryUse(DateTimeOffset now) => now < Preview.ExpiresAt
        && Interlocked.CompareExchange(ref _used, 1, 0) == 0;
}

public sealed class ImportPreviewStore : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 32 });
    internal static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    internal bool Add(string token, ImportTicket ticket)
    {
        _cache.Set(token, ticket, new MemoryCacheEntryOptions { AbsoluteExpiration = ticket.Preview.ExpiresAt, Size = 1 });
        return _cache.TryGetValue(token, out _);
    }
    internal ImportTicket? Get(string token, string owner, long assessmentId) =>
        token.Length == 64 && _cache.TryGetValue<ImportTicket>(token, out var ticket)
        && ticket!.Owner == owner && ticket.AssessmentId == assessmentId && !ticket.IsUsed
        && DateTimeOffset.UtcNow < ticket.Preview.ExpiresAt ? ticket : null;
    public void Dispose() => _cache.Dispose();
}
