using System.Security.Cryptography;
using Harmonia.Web.ViewModels;
using Microsoft.Extensions.Caching.Memory;

namespace Harmonia.Web.Services;

public sealed class WorksheetGroupStore : IDisposable
{
    private sealed class Entry(WorksheetGroupDraft draft)
    {
        public WorksheetGroupDraft Draft { get; }=draft with{StudentIds=Array.AsReadOnly(draft.StudentIds.ToArray()),SubskillIds=Array.AsReadOnly(draft.SubskillIds.ToArray())};
        public string Token { get; }=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        public int Used;
    }
    private readonly MemoryCache cache=new(new MemoryCacheOptions{SizeLimit=256});
    public void Save(string owner,WorksheetGroupDraft draft)=>cache.Set((owner,draft.PackageId),new Entry(draft),new MemoryCacheEntryOptions{Size=1,AbsoluteExpirationRelativeToNow=TimeSpan.FromMinutes(20)});
    public (WorksheetGroupDraft Draft,string Token)? Get(string owner,long packageId)
        =>cache.TryGetValue<Entry>((owner,packageId),out var e)&&Volatile.Read(ref e!.Used)==0?(e.Draft,e.Token):null;
    public WorksheetGroupDraft? Take(string owner,long packageId,string? token)
    {
        if(token?.Length!=64||!cache.TryGetValue<Entry>((owner,packageId),out var e)||e!.Token!=token||Interlocked.CompareExchange(ref e.Used,1,0)!=0)return null;
        cache.Remove((owner,packageId));return e.Draft;
    }
    public void Dispose()=>cache.Dispose();
}
