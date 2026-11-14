using System.ComponentModel.DataAnnotations;

namespace Harmonia.Web.ViewModels;

public sealed class WorkspaceFilter
{
    [Range(6,12)] public short Grade { get; set; } = 9;
    [Range(1,long.MaxValue)] public long? UnitId { get; set; }
    [Range(1,long.MaxValue)] public long? AreaId { get; set; }
    [Range(1,long.MaxValue)] public long? SubskillId { get; set; }
    [StringLength(100)] public string? Search { get; set; }
    [Range(1,10000)] public int Page { get; set; } = 1;
}
public sealed class WorkspaceTreeFilter
{
    [Range(6,12)] public short Grade { get; set; } = 9;
    [RegularExpression("^(unit|area|skill)$")] public string Level { get; set; } = "unit";
    [Range(1,long.MaxValue)] public long? ParentId { get; set; }
    [Range(1,10000)] public int Page { get; set; } = 1;
}
// IDs are strings on the JSON boundary to preserve PostgreSQL BIGINT precision in JavaScript.
public sealed record WorkspaceNode(string Id, string Label, string Code, string Level, int Count,
    string? UnitId = null, string? AreaId = null, string? Path = null);
public sealed record WorkspaceNodes(IReadOnlyList<WorkspaceNode> Items, bool HasMore);
public sealed record WorkspaceQuestion(string Id, string Code, string Text, string SubskillName, short Grade,
    short Difficulty, string A, string B, string C, string D, string Key);
public sealed record WorkspaceBank(IReadOnlyList<WorkspaceQuestion> Items, int Total, int Page, int PageSize, string Scope);
