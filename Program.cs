using Microsoft.EntityFrameworkCore;
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

// Add session services
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Session timeout
    options.Cookie.HttpOnly = true; // Secure the cookie
    options.Cookie.IsEssential = true; // Ensure the cookie is essential
});



// Add distributed memory cache (required for session handling)
builder.Services.AddDistributedMemoryCache();
builder.Logging.ClearProviders();
builder.Logging.AddConsole(); // Enables console logging
builder.Logging.AddDebug(); 

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
// Use session middleware
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
