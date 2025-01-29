using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using PatientManagementSystem.Data;
using PatientManagementSystem.Models;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace PatientManagementSystem.Controllers
{
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AdminController> _logger;

        public AdminController(AppDbContext context, ILogger<AdminController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Admin/Login
        public IActionResult Login()
        {
            if (HttpContext.Session.GetString("AdminLoggedIn") != null)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        // POST: Admin/Login
        [HttpPost]
        public IActionResult Login(string email, string password)
        {
            var admin = _context.Admins.FirstOrDefault(a => a.Email == email);

            if (admin != null)
            {
                HttpContext.Session.SetString("AdminLoggedIn", admin.Id.ToString());

                _logger.LogInformation($"AdminLoggedIn session set for Admin ID: {admin.Id}");
                return RedirectToAction("Index", "Home");
            }

            _logger.LogWarning($"Failed login attempt for email: {email}");
            ViewBag.Error = "Correo o contraseña incorrectos.";

            return View();
        }

        // GET: Admin/Logout
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        // Other actions remain unchanged...

        private bool IsAuthenticated()
        {
            return HttpContext.Session.GetString("AdminLoggedIn") != null;
        }
    }
}
