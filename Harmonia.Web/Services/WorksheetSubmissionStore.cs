using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;

namespace Harmonia.Web.Services;

// Instance-local replay protection, intentionally bounded and disabled with the feature by default.
public sealed class WorksheetSubmissionStore : IDisposable
{
    private sealed class Ticket(string owner,string context)
    {
        public readonly string Owner=owner, Context=context;
        public int Used;
    }
    private readonly MemoryCache cache=new(new MemoryCacheOptions{SizeLimit=512});
    public string Issue(string owner,string context)
    {
        var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        cache.Set(token,new Ticket(owner,context),new MemoryCacheEntryOptions{AbsoluteExpirationRelativeToNow=TimeSpan.FromMinutes(20),Size=1});return token;
    }
    public bool Take(string? token,string owner,string context)
    {
        if(token?.Length!=64||!cache.TryGetValue<Ticket>(token,out var t)||t!.Owner!=owner||t.Context!=context||Interlocked.CompareExchange(ref t.Used,1,0)!=0)return false;
        cache.Remove(token);return true;
    }
    public void Dispose()=>cache.Dispose();
}
