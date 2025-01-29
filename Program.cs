using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using PatientManagementSystem.Data;

var builder = WebApplication.CreateBuilder(args);

// Increase the request size limit
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 52428800; // 50 MB
});

// Add services to the container
builder.Services.AddControllersWithViews();

// Add PostgreSQL DbContext
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Redis for session storage
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("RedisConnection");
    options.InstanceName = "Session_"; // Optional: Prefix for keys in Redis
});

// Configure session services to use Redis
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Session timeout
    options.Cookie.HttpOnly = true; // Secure the cookie
    options.Cookie.IsEssential = true; // Ensure the cookie is essential
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // Use `SameAsRequest` for secure handling
});

// Register IHttpContextAccessor
builder.Services.AddHttpContextAccessor();

// Configure Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddFilter("Microsoft.AspNetCore.Session", LogLevel.Debug); // Enable session logs
builder.Logging.AddFilter("Microsoft.AspNetCore.Http", LogLevel.Debug); // Enable HTTP logs

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Ensure session middleware is used before routing and authorization
app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
