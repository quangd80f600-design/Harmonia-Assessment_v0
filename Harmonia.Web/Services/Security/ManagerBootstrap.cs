using Harmonia.Web.Data;
using Harmonia.Web.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace Harmonia.Web.Services.Security;
public static class ManagerBootstrap
{
    public static async Task RunAsync(IServiceProvider services)
    {
        if(Console.IsInputRedirected)throw new InvalidOperationException("Bootstrap yêu cầu terminal tương tác, không nhận password qua arguments/logs.");
        Console.Write("Tên tài khoản quản lý: ");var name=Console.ReadLine()?.Trim();
        Console.Write("Mật khẩu (ít nhất 12 ký tự, không hiển thị): ");var password="";
        while(true){var key=Console.ReadKey(true);if(key.Key==ConsoleKey.Enter)break;if(key.Key==ConsoleKey.Backspace){if(password.Length>0)password=password[..^1];}else if(!char.IsControl(key.KeyChar))password+=key.KeyChar;}
        Console.WriteLine();if(string.IsNullOrWhiteSpace(name)||password.Length<12)throw new InvalidOperationException("Thông tin bootstrap không hợp lệ.");
        using var scope=services.CreateScope();var users=scope.ServiceProvider.GetRequiredService<UserManager<HarmoniaUser>>();
        var roles=scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();var db=scope.ServiceProvider.GetRequiredService<HarmoniaIdentityDbContext>();
        await using var tx=await db.Database.BeginTransactionAsync();
        if(await users.FindByNameAsync(name)is not null || await db.UserRoles.AnyAsync(ur=>db.Roles.Any(r=>r.Id==ur.RoleId&&r.Name==HarmoniaRoles.Manager)))throw new InvalidOperationException("Đã có tài khoản/Manager; không tự thay mật khẩu hoặc tạo Manager thứ hai.");
        foreach(var role in new[]{HarmoniaRoles.Teacher,HarmoniaRoles.Manager})if(!await roles.RoleExistsAsync(role)&&!(await roles.CreateAsync(new(role))).Succeeded)throw new InvalidOperationException("Không tạo được role.");
        var user=new HarmoniaUser{UserName=name,MustChangePassword=false,LockoutEnabled=true};
        if(!(await users.CreateAsync(user,password)).Succeeded||!(await users.AddToRoleAsync(user,HarmoniaRoles.Manager)).Succeeded)throw new InvalidOperationException("Bootstrap thất bại; không lưu một phần.");
        await tx.CommitAsync();Console.WriteLine("Đã tạo Manager. Không có credential được ghi ra output.");
    }
}
