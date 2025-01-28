using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PatientManagementSystem.Models;
using PatientManagementSystem.Data;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace PatientManagementSystem.Controllers
{
    public class HomeController : BaseController
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _context; // Add DbContext dependency

        public HomeController(ILogger<HomeController> logger, AppDbContext context)
        {
            _logger = logger;
            _context = context; // Inject DbContext
        }

        public IActionResult Index()
        {
            var patients = _context.Patients.ToList(); // Fetch all patients
            return View(patients); // Pass patients to the view
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpPost]
        public IActionResult AdminLogin(string Email, string Password)
        {
            // Authenticate the admin
            var admin = _context.Admins.FirstOrDefault(a => a.Email == Email);
            if (admin != null && BCrypt.Net.BCrypt.Verify(Password, admin.Password))
            {
                // Save admin login status in the session
                HttpContext.Session.SetString("AdminLoggedIn", admin.Email);
                return RedirectToAction("Index");
            }

            ModelState.AddModelError("", "Invalid email or password.");
            return View("Index", new List<Patient>());
        }

        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Remove("AdminLoggedIn");
            return RedirectToAction("Index");
        }
    }
}
