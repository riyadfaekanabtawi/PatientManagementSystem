using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using PatientManagementSystem.Data;
using PatientManagementSystem.Models;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BCrypt.Net;

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

        // GET: Admin/Index
        public async Task<IActionResult> Index()
        {
            if (!IsAuthenticated())
            {
                return RedirectToAction("Login");
            }

            var admins = await _context.Admins.ToListAsync();
            return View(admins);
        }


        // GET: Admin/Create
        public IActionResult Create()
        {
            if (!IsAuthenticated())
            {
                return RedirectToAction("Login");
            }

            return View();
        }

        // POST: Admin/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Admin admin)
        {
            if (!IsAuthenticated())
            {
                return RedirectToAction("Login");
            }

            if (ModelState.IsValid)
            {
                // Hash the password before saving
                admin.Password = BCrypt.Net.BCrypt.HashPassword(admin.Password);

                _context.Add(admin);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"New admin created with email: {admin.Email}");
                return RedirectToAction(nameof(Index));
            }
            return View(admin);
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
                var sessionValue = HttpContext.Session.GetString("AdminLoggedIn");
                 _logger.LogInformation($"[DEBUG] Session Retrieved: {sessionValue}");
                return RedirectToAction("Index", "Home", new { session_id = admin.Id });

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


        // GET: Admin/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (!IsAuthenticated())
            {
                return RedirectToAction("Login");
            }

            if (id == null)
            {
                return NotFound();
            }

            var admin = await _context.Admins.FindAsync(id);
            if (admin == null)
            {
                return NotFound();
            }

            return View(admin);
        }

        // POST: Admin/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Admin admin)
        {
            if (!IsAuthenticated())
            {
                return RedirectToAction("Login");
            }

            if (id != admin.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Update the password only if it's provided (optional)
                    if (!string.IsNullOrEmpty(admin.Password))
                    {
                        admin.Password = BCrypt.Net.BCrypt.HashPassword(admin.Password);
                    }
                    else
                    {
                        // Retain the old password if none is provided
                        _context.Entry(admin).Property(a => a.Password).IsModified = false;
                    }

                    _context.Update(admin);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Admin ID {admin.Id} updated successfully.");
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AdminExists(admin.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(admin);
        }

        // GET: Admin/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (!IsAuthenticated())
            {
                return RedirectToAction("Login");
            }

            if (id == null)
            {
                return NotFound();
            }

            var admin = await _context.Admins
                .FirstOrDefaultAsync(a => a.Id == id);

            if (admin == null)
            {
                return NotFound();
            }

            return View(admin);
        }

        // POST: Admin/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!IsAuthenticated())
            {
                return RedirectToAction("Login");
            }

            var admin = await _context.Admins.FindAsync(id);
            if (admin != null)
            {
                _context.Admins.Remove(admin);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Admin ID {id} deleted successfully.");
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (!IsAuthenticated())
            {
                return RedirectToAction("Login");
            }

            if (id == null)
            {
                return NotFound();
            }

            var admin = await _context.Admins
                .FirstOrDefaultAsync(a => a.Id == id);

            if (admin == null)
            {
                return NotFound();
            }

            return View(admin);
        }

        // Helper: Check if Admin is Authenticated
        private bool IsAuthenticated()
        {
             if (session_id.HasValue)
            {
                HttpContext.Session.SetString("AdminLoggedIn", session_id.Value.ToString());
                _logger.LogInformation($"[DEBUG] Session set from query param: {session_id.Value}");
            }
            return HttpContext.Session.GetString("AdminLoggedIn") != null;
        }

        // Helper: Check if Admin Exists
        private bool AdminExists(int id)
        {
            return _context.Admins.Any(e => e.Id == id);
        }
    }
}
