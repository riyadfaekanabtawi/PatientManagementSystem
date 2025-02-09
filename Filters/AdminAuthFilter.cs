using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

public class AdminAuthFilter : ActionFilterAttribute
{
    private readonly ILogger<AdminAuthFilter> _logger;

    public AdminAuthFilter(ILogger<AdminAuthFilter> logger)
    {
        _logger = logger;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var sessionValue = context.HttpContext.Session.GetString("AdminLoggedIn");
        var cookies = context.HttpContext.Request.Headers["Cookie"];

        _logger.LogInformation($"[DEBUG] Cookies Received: {cookies}");
        _logger.LogInformation($"[DEBUG] Session Retrieved: {sessionValue}");

        if (string.IsNullOrEmpty(sessionValue))
        {
            context.Result = new RedirectToActionResult("Login", "Admin", null);
        }

        base.OnActionExecuting(context);
    }
}
