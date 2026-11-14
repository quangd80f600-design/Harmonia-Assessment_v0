using Harmonia.Web.ViewModels;
namespace Harmonia.Web.Services;

// Presentation-only filters; no write policy, snapshot or persistence changes.
public static class TheoryBrowsing
{
    public static TheoryPage Apply(TheoryPage page, TheoryBrowseFilter input)
    {
        var f = new TheoryBrowseFilter { Grade=input.Grade, UnitId=input.UnitId, AreaId=input.AreaId,
            SubskillId=input.SubskillId, Search=input.Search?.Trim() };
        var cleared=false;
        IEnumerable<TheorySkill> skills=page.Skills;
        if(f.Grade.HasValue) skills=skills.Where(s=>s.Grade==f.Grade);
        if(f.UnitId.HasValue && !skills.Any(s=>s.UnitId==f.UnitId)) { f.UnitId=null; f.AreaId=null; f.SubskillId=null; cleared=true; }
        if(f.UnitId.HasValue) skills=skills.Where(s=>s.UnitId==f.UnitId);
        if(f.AreaId.HasValue && !skills.Any(s=>s.AreaId==f.AreaId)) { f.AreaId=null; f.SubskillId=null; cleared=true; }
        if(f.AreaId.HasValue) skills=skills.Where(s=>s.AreaId==f.AreaId);
        if(f.SubskillId.HasValue && !skills.Any(s=>s.Id==f.SubskillId)) { f.SubskillId=null; cleared=true; }
        if(f.SubskillId.HasValue) skills=skills.Where(s=>s.Id==f.SubskillId);
        var ids=skills.Select(s=>s.Id).ToHashSet();
        // Do not hide old/inactive mappings in an unfiltered inventory.
        var filtered=f.Grade.HasValue || f.UnitId.HasValue || f.AreaId.HasValue || f.SubskillId.HasValue;
        var rows=page.Rows.Where(t=>!filtered || ids.Contains(t.SubskillId));
        if(!string.IsNullOrEmpty(f.Search)) rows=rows.Where(t=>t.Title.Contains(f.Search,StringComparison.OrdinalIgnoreCase)
            || t.Content.Contains(f.Search,StringComparison.OrdinalIgnoreCase)
            || page.Skills.Any(s=>s.Id==t.SubskillId && (s.Code.Contains(f.Search,StringComparison.OrdinalIgnoreCase) || s.Name.Contains(f.Search,StringComparison.OrdinalIgnoreCase))));
        return page with { Rows=rows.ToArray(), Filter=f, FilterNotice=cleared?"Đã bỏ lựa chọn không còn phù hợp. Hãy chọn lại nội dung cần tìm.":null };
    }
}
