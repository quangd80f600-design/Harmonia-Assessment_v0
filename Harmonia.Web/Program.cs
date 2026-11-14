using Harmonia.Web.Models.Identity;
using Harmonia.Web.Services.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Harmonia.Web.Data;
using Harmonia.Web.Services;
using Harmonia.Web.Services.Import;
using Harmonia.Web.Services.Academic;
using Microsoft.EntityFrameworkCore;
var bootstrapManager = args.Contains("--bootstrap-manager");
var builder = WebApplication.CreateBuilder(args.Where(a => a != "--bootstrap-manager").ToArray());
// Đăng ký kết nối PostgreSQL cho Harmonia
builder.Services.AddDbContextFactory<HarmoniaDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("HarmoniaDb")
    )
);
// Add services to the container.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentActor>();
builder.Services.AddScoped<BusinessAccess>();
builder.Services.AddScoped<BusinessAuthorizationFilter>();
builder.Services.AddScoped<TeacherManagementService>();
builder.Services.AddDbContext<HarmoniaIdentityDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("HarmoniaDb"), n => n.MigrationsHistoryTable("__EFMigrationsHistory", "auth")));
builder.Services.AddIdentity<HarmoniaUser, IdentityRole>(o => {
    o.Password.RequiredLength = 12; o.Lockout.MaxFailedAccessAttempts = 5; o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    o.Stores.SchemaVersion = IdentitySchemaVersions.Version2;
}).AddEntityFrameworkStores<HarmoniaIdentityDbContext>().AddDefaultTokenProviders();
builder.Services.AddScoped<IUserClaimsPrincipalFactory<HarmoniaUser>, HarmoniaClaimsFactory>();
builder.Services.AddScoped<HarmoniaCookieEvents>();
builder.Services.ConfigureApplicationCookie(o => {
    o.Cookie.Name = ".Harmonia.Auth"; o.Cookie.HttpOnly = true; o.Cookie.SecurePolicy = CookieSecurePolicy.Always; o.Cookie.SameSite = SameSiteMode.Lax;
    o.LoginPath = "/Account/Login"; o.AccessDeniedPath = "/Account/AccessDenied";
    o.ExpireTimeSpan = TimeSpan.FromMinutes(30); o.SlidingExpiration = false; o.EventsType = typeof(HarmoniaCookieEvents);
});
builder.Services.AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddControllersWithViews(o => o.Filters.AddService<BusinessAuthorizationFilter>());
builder.Services.AddScoped<AssessmentAnalysisService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<AssessmentFollowupPlanService>();
builder.Services.AddScoped<AssessmentParticipationService>();
builder.Services.AddScoped<IndividualDiagnosisService>();
builder.Services.AddScoped<StudentProgressService>();
builder.Services.Configure<LearningObservationOptions>(builder.Configuration.GetSection("LearningObservations"));
builder.Services.AddScoped<LearningObservationService>();
builder.Services.Configure<ParentCommentOptions>(builder.Configuration.GetSection("ParentComments"));
builder.Services.AddScoped<ParentCommentService>();
builder.Services.Configure<Harmonia.Web.Services.Gemini.GeminiOptions>(builder.Configuration.GetSection("Gemini"));
builder.Services.PostConfigure<Harmonia.Web.Services.Gemini.GeminiOptions>(g => g.SeparateSections=builder.Configuration.GetValue<bool>("ParentComments:SeparateSections"));
builder.Services.AddHttpClient("GeminiComments").RemoveAllLoggers();
builder.Services.AddTransient(sp => new Harmonia.Web.Services.Gemini.GeminiCommentClient(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("GeminiComments"),
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Harmonia.Web.Services.Gemini.GeminiOptions>>().Value));
builder.Services.Configure<StudentReportOptions>(builder.Configuration.GetSection("StudentReport"));
builder.Services.AddSingleton<StudentReportAccess>();
builder.Services.AddScoped<StudentReportService>();
builder.Services.Configure<AuthoringOptions>(builder.Configuration.GetSection("Authoring"));
builder.Services.AddSingleton<AuthoringAccess>();
builder.Services.AddScoped<AuthoringService>();
builder.Services.AddSingleton<QuestionCsvParser>();
builder.Services.AddSingleton<QuestionCsvPreviewStore>();
builder.Services.AddScoped<QuestionCsvImportService>();
builder.Services.AddScoped<TestWorkspaceService>();
builder.Services.Configure<CsvImportOptions>(builder.Configuration.GetSection("CsvImport"));
builder.Services.AddSingleton<CsvResultParser>();
builder.Services.AddSingleton<ImportPreviewStore>();
builder.Services.AddScoped<AssessmentImportService>();
builder.Services.Configure<AcademicManagementOptions>(builder.Configuration.GetSection("AcademicManagement"));
builder.Services.AddScoped<AcademicManagementService>();
builder.Services.AddSingleton<StudentRosterParser>();
builder.Services.AddSingleton<RosterPreviewStore>();
builder.Services.AddScoped<StudentRosterImportService>();
builder.Services.Configure<InterventionManagementOptions>(builder.Configuration.GetSection("InterventionManagement"));
builder.Services.AddScoped<InterventionManagementService>();
builder.Services.AddScoped<InterventionFollowupService>();
builder.Services.Configure<WorksheetOptions>(builder.Configuration.GetSection("Worksheets"));
builder.Services.AddScoped<WorksheetService>();
builder.Services.AddSingleton<WorksheetSubmissionStore>();
builder.Services.AddSingleton<WorksheetGroupStore>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".Harmonia.Import.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromMinutes(20);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets().AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


if (bootstrapManager) { await ManagerBootstrap.RunAsync(app.Services); return; }
app.Run();
