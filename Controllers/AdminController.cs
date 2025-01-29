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

                // Log session state
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

        // GET: Admin
        public async Task<IActionResult> Index()
        {
            if (!IsAuthenticated()) return RedirectToAction("Login");

            var admins = await Task.FromResult(_context.Admins.ToList());
            return View(admins);
        }

        // GET: Admin/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login");

            if (id == null) return NotFound();

            var admin = await _context.Admins.FindAsync(id);
            if (admin == null) return NotFound();

            return View(admin);
        }

        // GET: Admin/Create
        public IActionResult Create()
        {
            if (!IsAuthenticated()) return RedirectToAction("Login");

            return View();
        }

        // POST: Admin/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Email,Password")] Admin admin)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login");

            if (ModelState.IsValid)
            {
                admin.Password = BCrypt.Net.BCrypt.HashPassword(admin.Password);
                _context.Add(admin);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(admin);
        }

        // GET: Admin/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login");

            if (id == null) return NotFound();

            var admin = await _context.Admins.FindAsync(id);
            if (admin == null) return NotFound();

            return View(admin);
        }

        // POST: Admin/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Email,Password")] Admin admin)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login");

            if (id != admin.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existingAdmin = await _context.Admins.FindAsync(id);
                    if (existingAdmin == null) return NotFound();

                    existingAdmin.Name = admin.Name;
                    existingAdmin.Email = admin.Email;

                    if (!string.IsNullOrWhiteSpace(admin.Password))
                    {
                        existingAdmin.Password = BCrypt.Net.BCrypt.HashPassword(admin.Password);
                    }

                    _context.Update(existingAdmin);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AdminExists(admin.Id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(admin);
        }

        // GET: Admin/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login");

            if (id == null) return NotFound();

            var admin = await _context.Admins.FindAsync(id);
            if (admin == null) return NotFound();

            return View(admin);
        }

        // POST: Admin/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!IsAuthenticated()) return RedirectToAction("Login");

            var admin = await _context.Admins.FindAsync(id);
            if (admin != null)
            {
                _context.Admins.Remove(admin);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool AdminExists(int id)
        {
            return _context.Admins.Any(e => e.Id == id);
        }

        private bool IsAuthenticated()
        {
            return HttpContext.Session.GetString("AdminLoggedIn") != null;
        }
    }
}
