using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using PatientManagementSystem.Data;
using PatientManagementSystem.Services;  // ✅ Corrected namespace
using Amazon.S3;
using Amazon.Extensions.NETCore.Setup;
using Microsoft.AspNetCore.Mvc.Filters;

var builder = WebApplication.CreateBuilder(args);

// Increase request size limit
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 52428800; // 50 MB
});

// Add services
builder.Services.AddControllersWithViews(options =>
{
    var serviceProvider = builder.Services.BuildServiceProvider();
    var logger = serviceProvider.GetRequiredService<ILogger<AdminAuthFilter>>();
    options.Filters.Add(new AdminAuthFilter(logger)); // Register globally
});

// ✅ Register ThreeDModelService correctly
builder.Services.AddHttpClient<I3DModelService, ThreeDModelService>();

// Configure PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register AWS S3 Client
builder.Services.AddAWSService<IAmazonS3>(builder.Configuration.GetAWSOptions());

// ✅ Register IHttpContextAccessor
builder.Services.AddHttpContextAccessor();

// Redis Session Storage
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
    options.InstanceName = "Session_";
});

// Configure session middleware
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Configure middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
