using System.Data;
using Harmonia.Web.Data;
using Harmonia.Web.Models.Entities;
using Harmonia.Web.Models.Identity;
using Harmonia.Web.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace Harmonia.Web.Services.Security;
public sealed class TeacherManagementService(IDbContextFactory<HarmoniaDbContext> factory, HarmoniaIdentityDbContext identity,
    UserManager<HarmoniaUser> users, CurrentActor actor)
{
    private void Manager() { if (!actor.IsManager) throw new UnauthorizedAccessException(); }
    public async Task<IReadOnlyList<TeacherListRow>> ListAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var today = AcademicManagementService.Today;
        var rows = await db.Teachers.AsNoTracking().Where(t => actor.IsManager || t.TeacherId == actor.TeacherId)
            .OrderBy(t => t.TeacherCode).Select(t => new TeacherListRow(t.TeacherId, t.TeacherCode, t.FullName, t.Status)).ToListAsync(ct);
        if (rows.Count == 0) return rows;
        var ids = rows.Select(t => t.Id).ToArray();
        // Load relationships/account summaries in batches, never one query per Teacher.
        var assignments = await db.TeacherClassAssignments.AsNoTracking()
            .Where(a => ids.Contains(a.TeacherId) && a.StartDate <= today && (a.EndDate == null || a.EndDate > today))
            .OrderBy(a => a.Class.ClassName).ThenBy(a => a.Class.AcademicYear)
            .Select(a => new { a.TeacherId, Row = new TeacherAssignmentRow(a.ClassId, a.Class.ClassName, a.StartDate, a.EndDate, true)
                { AcademicYear = a.Class.AcademicYear, CanOpenClass = true } }).ToListAsync(ct);
        var accounts = await identity.TeacherUserLinks.AsNoTracking().Where(l => ids.Contains(l.TeacherId))
            .Select(l => new { l.TeacherId, l.User.IsActive }).ToDictionaryAsync(l => l.TeacherId, ct);
        var byTeacher = assignments.GroupBy(a => a.TeacherId).ToDictionary(g => g.Key, g => g.Select(a => a.Row).DistinctBy(a => a.ClassId).ToArray());
        return rows.Select(t => t with {
            Assignments = byTeacher.TryGetValue(t.Id, out var items) ? items : [],
            HasAccount = accounts.ContainsKey(t.Id), AccountActive = accounts.TryGetValue(t.Id, out var account) && account.IsActive
        }).ToArray();
    }
    public async Task<TeacherDetailsPage?> DetailsAsync(long id, CancellationToken ct)
    {
        if (!actor.IsManager && id != actor.TeacherId) return null;
        await using var db = await factory.CreateDbContextAsync(ct);
        var t = await db.Teachers.AsNoTracking().SingleOrDefaultAsync(t => t.TeacherId == id, ct);
        if (t is null) return null;
        var today = AcademicManagementService.Today;
        var rows = await db.TeacherClassAssignments.AsNoTracking().Where(a => a.TeacherId == id)
            .OrderByDescending(a => a.StartDate).ThenBy(a => a.Class.ClassName)
            .Select(a => new TeacherAssignmentRow(a.ClassId, a.Class.ClassName, a.StartDate, a.EndDate,
                a.StartDate <= today && (a.EndDate == null || a.EndDate > today)) { AcademicYear = a.Class.AcademicYear }).ToListAsync(ct);
        var readable = rows.Where(a => a.Current).Select(a => a.ClassId).ToHashSet();
        rows = rows.Select(a => a with { CanOpenClass = actor.IsManager || readable.Contains(a.ClassId) }).ToList();
        var account = await identity.TeacherUserLinks.AsNoTracking().Where(l => l.TeacherId == id)
            .Select(l => new { l.UserId, l.User.IsActive, l.User.UserName, IsManager = identity.UserRoles.Any(ur => ur.UserId == l.UserId && identity.Roles.Any(r => r.Id == ur.RoleId && r.Name == HarmoniaRoles.Manager)) }).SingleOrDefaultAsync(ct);
        var classes = actor.IsManager ? await db.Classes.AsNoTracking().Where(c => c.Status == "Active").OrderBy(c => c.ClassCode)
            .Select(c => new AuthoringClass(c.ClassId, c.ClassName, c.Grade, c.AcademicYear)).ToListAsync(ct) : [];
        return new(id, t.TeacherCode, t.FullName, t.Email, t.PhoneNumber, t.Status, account is not null,
            account?.IsActive == true, account?.UserName, rows, classes) {
            Assignment = new() { StartDate = today }, AccountIsManager = account?.IsManager == true,
            IsOwnAccount = account?.UserId == actor.Id,
            CanLinkOwnManager = actor.IsManager && t.Status == "Active" && account is null
                && !await identity.TeacherUserLinks.AsNoTracking().AnyAsync(l => l.UserId == actor.Id, ct)
        };
    }
    public async Task<AcademicClassHeader?> GetAssignmentClassAsync(long classId, CancellationToken ct)
    {
        Manager();
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Classes.AsNoTracking().Where(c => c.ClassId == classId)
            .Select(c => new AcademicClassHeader(c.ClassId, c.ClassCode, c.ClassName, c.Grade, c.AcademicYear, c.Status)).SingleOrDefaultAsync(ct);
    }
    public async Task<long?> CreateAsync(TeacherInput input,CancellationToken ct)
    {
        Manager(); if(AuthoringService.Validate(input)!=null)return null;
        await using var db=await factory.CreateDbContextAsync(ct);
        var row=new Teacher{TeacherCode=input.TeacherCode.Trim(),FullName=input.FullName.Trim(),Email=input.Email.Trim(),PhoneNumber=input.PhoneNumber.Trim(),Status="Active",CreatedAt=DateTime.UtcNow,CreatedByUserId=actor.RequiredId};
        db.Teachers.Add(row);try{await db.SaveChangesAsync(ct);return row.TeacherId;}catch(DbUpdateException ex)when(ex.InnerException is PostgresException{SqlState:PostgresErrorCodes.UniqueViolation}){return null;}
    }
    public async Task<bool> CreateAccountAsync(long id,TeacherAccountInput input,CancellationToken ct)
    {
        Manager();if(AuthoringService.Validate(input)!=null)return false;
        await using var db=await factory.CreateDbContextAsync(ct);
        if(!await db.Teachers.AnyAsync(t=>t.TeacherId==id&&t.Status=="Active",ct))return false;
        // UserManager and links share this context/transaction; role or FK failures roll back user too.
        await using var tx=await identity.Database.BeginTransactionAsync(ct);
        if(await identity.TeacherUserLinks.AnyAsync(l=>l.TeacherId==id,ct))return false;
        var user=new HarmoniaUser{UserName=input.UserName.Trim(),MustChangePassword=true,IsActive=true,LockoutEnabled=true};
        if(!(await users.CreateAsync(user,input.TemporaryPassword)).Succeeded)return false;
        if(!(await users.AddToRoleAsync(user,HarmoniaRoles.Teacher)).Succeeded)return false;
        identity.TeacherUserLinks.Add(new(){UserId=user.Id,TeacherId=id});
        await identity.SaveChangesAsync(ct);await tx.CommitAsync(ct);return true;
    }
    // Only attach the authenticated Manager to an unclaimed active Teacher; never grant Manager.
    public async Task<ManagerTeacherLinkResult> LinkOwnManagerAsync(long teacherId, CancellationToken ct)
    {
        Manager();
        if (teacherId <= 0) return new(false, "Hồ sơ giáo viên không hợp lệ.");
        var userId = actor.RequiredId;
        await using var tx = await identity.Database.BeginTransactionAsync(ct);
        var wroteIdentity = false;
        async Task<ManagerTeacherLinkResult> Fail(string message)
        {
            await tx.RollbackAsync(CancellationToken.None);
            if (wroteIdentity) identity.ChangeTracker.Clear();
            return new(false, message);
        }
        try
        {
            // Serialize changes for this account. Teacher SHARE lock keeps its Active status stable.
            await identity.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM auth.\"AspNetUsers\" WHERE \"Id\"={userId} FOR UPDATE", ct);
            await identity.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM auth.\"AspNetUserRoles\" WHERE \"UserId\"={userId} FOR UPDATE", ct);
            await identity.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM public.\"Teacher\" WHERE \"Teacher_ID\"={teacherId} FOR SHARE", ct);
            var user = await users.FindByIdAsync(userId);
            if (user is null) return await Fail("Tài khoản quản lý không còn hợp lệ.");
            await identity.Entry(user).ReloadAsync(ct);
            var state = await AccountEligibility.ReadAsync(user, users, identity, factory, ct);
            if (!state.Eligible || !state.Roles.Contains(HarmoniaRoles.Manager) || user.MustChangePassword)
                return await Fail("Tài khoản quản lý không đủ điều kiện liên kết.");
            if (state.LinkedTeacherId.HasValue || await identity.TeacherUserLinks.AnyAsync(l => l.TeacherId == teacherId, ct))
                return await Fail("Tài khoản hoặc hồ sơ giáo viên đã có liên kết. Không tạo tài khoản hay liên kết thứ hai.");
            await using var db = await factory.CreateDbContextAsync(ct);
            if (!await db.Teachers.AnyAsync(t => t.TeacherId == teacherId && t.Status == "Active", ct))
                return await Fail("Hồ sơ giáo viên không tồn tại hoặc đã ngừng hoạt động.");
            wroteIdentity = true;
            if (!state.Roles.Contains(HarmoniaRoles.Teacher) && !(await users.AddToRoleAsync(user, HarmoniaRoles.Teacher)).Succeeded)
                return await Fail("Chưa gán được vai trò giáo viên; không lưu liên kết.");
            identity.TeacherUserLinks.Add(new() { UserId = userId, TeacherId = teacherId });
            await identity.SaveChangesAsync(ct);
            if (!(await users.UpdateSecurityStampAsync(user)).Succeeded)
                return await Fail("Chưa thu hồi được phiên cũ; không lưu thay đổi.");
            await tx.CommitAsync(ct);
            return new(true, "Đã liên kết hồ sơ giáo viên với tài khoản quản lý của bạn. Hãy đăng nhập lại.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return await Fail("Tài khoản hoặc hồ sơ vừa được liên kết ở phiên khác. Không có thay đổi bổ sung.");
        }
    }
    public async Task<bool> ResetAsync(long id,ResetTeacherPasswordInput input,CancellationToken ct)
    {
        Manager();if(AuthoringService.Validate(input)!=null)return false;
        await using var tx=await identity.Database.BeginTransactionAsync(ct);
        var user=await LinkedUser(id,ct);if(user is null)return false;
        var token=await users.GeneratePasswordResetTokenAsync(user);
        if(!(await users.ResetPasswordAsync(user,token,input.TemporaryPassword)).Succeeded)return false;
        if(!(await users.ResetAccessFailedCountAsync(user)).Succeeded || !(await users.SetLockoutEndDateAsync(user,null)).Succeeded)return false;
        user.MustChangePassword=true;if(!(await users.UpdateAsync(user)).Succeeded)return false;
        await tx.CommitAsync(ct);return true;
    }
    public async Task<bool> SetActiveAsync(long id,bool active,CancellationToken ct)
    {
        Manager();await using var tx=await identity.Database.BeginTransactionAsync(ct);
        var user=await LinkedUser(id,ct);if(user is null)return false;user.IsActive=active;
        if(!(await users.UpdateAsync(user)).Succeeded || !(await users.UpdateSecurityStampAsync(user)).Succeeded)return false;
        await tx.CommitAsync(ct);return true;
    }
    private async Task<HarmoniaUser?> LinkedUser(long id,CancellationToken ct)
    {
        var user=await identity.TeacherUserLinks.Where(l=>l.TeacherId==id).Select(l=>l.User).SingleOrDefaultAsync(ct);
        return user is not null && (await users.GetRolesAsync(user)).SequenceEqual(new[]{HarmoniaRoles.Teacher})?user:null;
    }
    public async Task<bool> AssignAsync(long id, TeacherAssignmentInput input, CancellationToken ct) =>
        (await AssignWithResultAsync(id, input, ct)).Succeeded;

    public async Task<TeacherAssignmentResult> AssignWithResultAsync(long id, TeacherAssignmentInput input, CancellationToken ct)
    {
        Manager();
        if (AssignmentDates.Validate(input) is { } invalid) return invalid;
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Teacher\" WHERE \"Teacher_ID\"={id} FOR UPDATE", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Class\" WHERE \"Class_ID\"={input.ClassId} FOR SHARE", ct);
        if (!await db.Teachers.AnyAsync(t => t.TeacherId == id && t.Status == "Active", ct))
            return new(false, "Giáo viên không tồn tại hoặc đã ngừng hoạt động.");
        if (!await db.Classes.AnyAsync(c => c.ClassId == input.ClassId && c.Status == "Active", ct))
            return new(false, "Lớp không tồn tại hoặc đã ngừng hoạt động. Chọn lại lớp.", "ClassId");
        var start = input.StartDate!.Value; var end = input.EndDate;
        if (await db.TeacherClassAssignments.AnyAsync(a => a.TeacherId == id && a.ClassId == input.ClassId
            && (!end.HasValue || a.StartDate < end) && (a.EndDate == null || a.EndDate > start), ct))
            return new(false, "Khoảng phân công chồng lấn với phân công đã có của giáo viên trong lớp này. Kiểm tra ngày bắt đầu và kết thúc.", "StartDate");
        db.TeacherClassAssignments.Add(new() { TeacherId = id, ClassId = input.ClassId, StartDate = start, EndDate = end,
            CreatedAt = DateTime.UtcNow, CreatedByUserId = actor.RequiredId });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(true, "Đã lưu phân công giáo viên–lớp.");
    }
}
