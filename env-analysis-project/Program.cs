using env_analysis_project.Data;
using env_analysis_project.Extensions;
using env_analysis_project.Models;
using env_analysis_project.Options;
using env_analysis_project.Security;
using env_analysis_project.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;


var builder = WebApplication.CreateBuilder(args);
// ======================================
// Build ML.net
// ======================================
builder.Services.Configure<ThresholdOptions>(builder.Configuration.GetSection("PollutionThresholds"));
builder.Services.Configure<ThresholdOptions>(builder.Configuration.GetSection("ThresholdOptions"));
builder.Services.AddScoped<IPredictionService, PredictionService>();

// ======================================
// Đăng ký DbContext
// ======================================
builder.Services.AddDbContext<env_analysis_projectContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("env_analysis_projectContext")
        ?? throw new InvalidOperationException("Connection string 'env_analysis_projectContext' not found."))
);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserActivityLogger, UserActivityLogger>();
builder.Services.AddScoped<IMeasurementImportService, MeasurementImportService>();
builder.Services.AddScoped<IMeasurementResultsService, MeasurementResultsService>();
builder.Services.AddScoped<IDashboardLookupService, DashboardLookupService>();
builder.Services.AddScoped<ISourceManagementService, SourceManagementService>();
builder.Services.AddScoped<IPollutionWorkflowService, PollutionWorkflowService>();
builder.Services.AddScoped<ISystemLogService, SystemLogService>();
builder.Services.AddScoped<IEmissionSourcesService, EmissionSourcesService>();
builder.Services.AddScoped<ISourceTypesService, SourceTypesService>();
builder.Services.AddScoped<IParametersService, ParametersService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();

// ======================================
// Cấu hình Identity
// ======================================
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
})
.AddEntityFrameworkStores<env_analysis_projectContext>()
.AddDefaultTokenProviders();

// ======================================
// Cấu hình JWT
// ======================================
builder.Services.AddJwtAuthenticationConfiguration(builder.Configuration);

// ======================================
// Thêm MVC Controller + View
// ======================================
builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/Login");
    options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/ForgotPassword");
    options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/ForgotPasswordConfirmation");
    options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/ResetPassword");
    options.Conventions.AllowAnonymousToAreaPage("Identity", "/Account/ResetPasswordConfirmation");
});

var app = builder.Build();

// ======================================
// Cấu hình Middleware Pipeline
// ======================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
// Chạy Service để Huấn luyện mô hình ngay khi ứng dụng khởi động
app.Services.GetRequiredService<IPredictionService>();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseMiddleware<AccessTokenForwardingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

// ======================================
// Cấu hình Route
// ======================================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.MapRazorPages();

// ======================================
// Chạy ứng dụng
// ======================================
await IdentityDataSeeder.SeedAsync(app);
app.Run();
