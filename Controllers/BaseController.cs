using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PatientManagementSystem.Controllers
{
    public class BaseController : Controller
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            // // Check if the admin is logged in
            // if (HttpContext.Session.GetString("AdminLoggedIn") == null)
            // {
            //     // Redirect to login if not logged in
            //     context.Result = RedirectToAction("Login", "Admin");
            // }

            base.OnActionExecuting(context);
        }
    }
}
