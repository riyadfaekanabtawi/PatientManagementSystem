using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PatientManagementSystem.Models;
using PatientManagementSystem.Data;
using Microsoft.Extensions.Logging;

namespace PatientManagementSystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _context;

        public HomeController(ILogger<HomeController> logger, AppDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index(int? session_id)
        {
            if (session_id.HasValue)
            {
                HttpContext.Session.SetString("AdminLoggedIn", session_id.Value.ToString());
                _logger.LogInformation($"[DEBUG] Session set from query param: {session_id.Value}");
            }

            var sessionValue = HttpContext.Session.GetString("AdminLoggedIn");
            var cookies = HttpContext.Request.Headers["Cookie"];

            _logger.LogInformation($"[DEBUG] Cookies Received: {cookies}");
            _logger.LogInformation($"[DEBUG] Session Retrieved: {sessionValue}");

            if (string.IsNullOrEmpty(sessionValue))
            {
                return RedirectToAction("Login", "Admin");
            }

            var patients = _context.Patients.ToList();
            return View(patients);
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
    }
}
