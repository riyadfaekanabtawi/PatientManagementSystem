using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatientManagementSystem.Data;
using PatientManagementSystem.Models;

namespace PatientManagementSystem.Controllers
{
    public class AppointmentsController : Controller
    {
        private readonly AppDbContext _context;

        public AppointmentsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Appointments
        public async Task<IActionResult> Index(DateTime? date, int? patientId)
        {
            var appointments = _context.Appointments
                .Include(a => a.Patient)
                .AsQueryable();

            if (date.HasValue)
            {
                appointments = appointments.Where(a => a.AppointmentDateTime.Date == date.Value.Date);
            }

            if (patientId.HasValue)
            {
                appointments = appointments.Where(a => a.PatientId == patientId);
            }

            // Pass patients list to the view for filtering
            ViewData["Patients"] = await _context.Patients
                .Select(p => new { p.Id, p.Name })
                .ToListAsync();

            return View(await appointments.ToListAsync());
        }

        // GET: Appointments/CreateAppointment
        public IActionResult CreateAppointment()
        {
            // Fetch patients
            var patients = _context.Patients.ToList();

            // Fetch existing appointments
            var appointments = _context.Appointments
                .Include(a => a.Patient)
                .Select(a => new
                {
                    a.Id,
                    a.AppointmentDateTime,
                    PatientName = a.Patient.Name
                })
                .ToList();

            // Pass appointments as JSON to the view
            ViewData["Appointments"] = System.Text.Json.JsonSerializer.Serialize(appointments);

            return View(patients);
        }


        // POST: Appointments/CreateAppointment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAppointment([Bind("PatientId,AppointmentDateTime,Notes")] Appointment appointment)
        {
            // Check if the appointment is in the past
            if (appointment.AppointmentDateTime < DateTime.Now)
            {
                ModelState.AddModelError("", "You cannot book an appointment in the past.");
            }

            // Check for conflicting appointments
            var conflictingAppointment = _context.Appointments
                .Any(a => a.AppointmentDateTime == appointment.AppointmentDateTime);

            if (conflictingAppointment)
            {
                ModelState.AddModelError("", "The selected time slot is already booked.");
            }

            if (!ModelState.IsValid)
            {
                // Re-fetch patients and appointments if validation fails
                var patients = _context.Patients.ToList();
                var appointments = _context.Appointments
                    .Include(a => a.Patient)
                    .Select(a => new
                    {
                        a.Id,
                        a.AppointmentDateTime,
                        PatientName = a.Patient.Name
                    })
                    .ToList();

                ViewData["Appointments"] = appointments;
                return View(patients);
            }

            // Save the appointment
            _context.Add(appointment);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Appointments/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (appointment == null)
            {
                return NotFound();
            }

            return View(appointment);
        }

        // GET: Appointments/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null)
            {
                return NotFound();
            }

            ViewData["Patients"] = _context.Patients
                .Select(p => new { p.Id, p.Name })
                .ToList();
            return View(appointment);
        }

        // POST: Appointments/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,PatientId,AppointmentDateTime,Notes")] Appointment appointment)
        {
            if (id != appointment.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(appointment);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AppointmentExists(appointment.Id))
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

            ViewData["Patients"] = _context.Patients
                .Select(p => new { p.Id, p.Name })
                .ToList();
            return View(appointment);
        }

        // GET: Appointments/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (appointment == null)
            {
                return NotFound();
            }

            return View(appointment);
        }

        // POST: Appointments/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment != null)
            {
                _context.Appointments.Remove(appointment);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private bool AppointmentExists(int id)
        {
            return _context.Appointments.Any(e => e.Id == id);
        }
    }
}