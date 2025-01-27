using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatientManagementSystem.Data;
using PatientManagementSystem.Models;
using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace PatientManagementSystem.Controllers
{
    public class PatientsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public PatientsController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // GET: Patients
        public async Task<IActionResult> Index()
        {
            var patients = await _context.Patients.ToListAsync();
            return View(patients);
        }

        // GET: Patients/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (!id.HasValue)
                return NotFound();

            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.Id == id);
            if (patient == null)
                return NotFound();

            return View(patient);
        }

        // GET: Patients/Create
        public IActionResult Create()
        {
            return View(new Patient
            {
                DateOfBirth = DateTime.Today // Default date to avoid validation issues
            });
        }

        // POST: Patients/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Id,Name,DateOfBirth,Email,Contact")] Patient patient,
            IFormFile? FrontImage,
            IFormFile? LeftImage,
            IFormFile? RightImage,
            IFormFile? BackImage)
        {
            if (!ModelState.IsValid)
                return View(patient);

            try
            {
                patient.FrontImageUrl = FrontImage != null ? await UploadToS3Async(FrontImage, $"patients/{Guid.NewGuid()}_front.{FrontImage.ContentType.Split('/')[1]}") : null;
                patient.LeftImageUrl = LeftImage != null ? await UploadToS3Async(LeftImage, $"patients/{Guid.NewGuid()}_left.{LeftImage.ContentType.Split('/')[1]}") : null;
                patient.RightImageUrl = RightImage != null ? await UploadToS3Async(RightImage, $"patients/{Guid.NewGuid()}_right.{RightImage.ContentType.Split('/')[1]}") : null;
                patient.BackImageUrl = BackImage != null ? await UploadToS3Async(BackImage, $"patients/{Guid.NewGuid()}_back.{BackImage.ContentType.Split('/')[1]}") : null;

                _context.Add(patient);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"An error occurred: {ex.Message}");
                return View(patient);
            }
        }

        // GET: Patients/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (!id.HasValue)
                return NotFound();

            var patient = await _context.Patients.FindAsync(id);
            if (patient == null)
                return NotFound();

            return View(patient);
        }

        // POST: Patients/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("Id,Name,DateOfBirth,Email,Contact")] Patient patient,
            IFormFile? FrontImage,
            IFormFile? LeftImage,
            IFormFile? RightImage,
            IFormFile? BackImage)
        {
            if (id != patient.Id)
                return NotFound();

            if (!ModelState.IsValid)
                return View(patient);

            try
            {
                patient.FrontImageUrl = FrontImage != null ? await UploadToS3Async(FrontImage, $"patients/{Guid.NewGuid()}_front.{FrontImage.ContentType.Split('/')[1]}") : patient.FrontImageUrl;
                patient.LeftImageUrl = LeftImage != null ? await UploadToS3Async(LeftImage, $"patients/{Guid.NewGuid()}_left.{LeftImage.ContentType.Split('/')[1]}") : patient.LeftImageUrl;
                patient.RightImageUrl = RightImage != null ? await UploadToS3Async(RightImage, $"patients/{Guid.NewGuid()}_right.{RightImage.ContentType.Split('/')[1]}") : patient.RightImageUrl;
                patient.BackImageUrl = BackImage != null ? await UploadToS3Async(BackImage, $"patients/{Guid.NewGuid()}_back.{BackImage.ContentType.Split('/')[1]}") : patient.BackImageUrl;

                _context.Update(patient);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PatientExists(patient.Id))
                    return NotFound();

                throw;
            }
        }

        // GET: Patients/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (!id.HasValue)
                return NotFound();

            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.Id == id);
            if (patient == null)
                return NotFound();

            return View(patient);
        }

        // POST: Patients/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var patient = await _context.Patients.FindAsync(id);
            if (patient != null)
                _context.Patients.Remove(patient);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PatientExists(int id)
        {
            return _context.Patients.Any(p => p.Id == id);
        }

        private async Task<string> UploadToS3Async(IFormFile file, string fileName)
        {
            var s3Client = new AmazonS3Client(
                _configuration["AWS:AccessKey"],
                _configuration["AWS:SecretKey"],
                Amazon.RegionEndpoint.GetBySystemName(_configuration["AWS:Region"])
            );

            using var transferUtility = new TransferUtility(s3Client);
            using var stream = file.OpenReadStream();

            var uploadRequest = new TransferUtilityUploadRequest
            {
                InputStream = stream,
                BucketName = _configuration["AWS:BucketName"],
                Key = fileName,
                ContentType = file.ContentType,
                CannedACL = S3CannedACL.PublicRead // Publicly accessible
            };

            await transferUtility.UploadAsync(uploadRequest);

            return $"https://{_configuration["AWS:BucketName"]}.s3.{_configuration["AWS:Region"]}.amazonaws.com/{fileName}";
        }
    }
}
