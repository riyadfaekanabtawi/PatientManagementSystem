using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

public class AdminAuthFilter : IActionFilter
{
    private readonly ILogger<AdminAuthFilter> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AdminAuthFilter(ILogger<AdminAuthFilter> logger, IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var adminSession = httpContext?.Session.GetString("AdminLoggedIn");

        if (string.IsNullOrEmpty(adminSession))
        {
            _logger.LogWarning("Unauthorized access attempt detected. Redirecting to login.");

            // Prevent infinite loop by allowing Login page
            var path = httpContext?.Request.Path.Value?.ToLower();
            if (path != null && (path.Contains("/admin/login") || path.Contains("/admin/logout")))
            {
                return;
            }

            context.Result = new RedirectToActionResult("Login", "Admin", null);
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        // Do nothing after action executes
    }
}
