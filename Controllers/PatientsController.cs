using System;
using System.Collections.Generic;
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
            return View(await _context.Patients.ToListAsync());
        }

        // GET: Patients/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var patient = await _context.Patients.FirstOrDefaultAsync(m => m.Id == id);
            if (patient == null)
            {
                return NotFound();
            }

            return View(patient);
        }

        // GET: Patients/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Patients/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,DateOfBirth,Email,Contact")] Patient patient, IFormFile? FrontImage, IFormFile? LeftImage, IFormFile? RightImage, IFormFile? BackImage)
        {
            if (ModelState.IsValid)
            {
                if (FrontImage != null)
                    patient.FrontImageUrl = await UploadToS3Async(FrontImage, $"patients/{Guid.NewGuid()}_front.{FrontImage.ContentType.Split('/')[1]}");
                if (LeftImage != null)
                    patient.LeftImageUrl = await UploadToS3Async(LeftImage, $"patients/{Guid.NewGuid()}_left.{LeftImage.ContentType.Split('/')[1]}");
                if (RightImage != null)
                    patient.RightImageUrl = await UploadToS3Async(RightImage, $"patients/{Guid.NewGuid()}_right.{RightImage.ContentType.Split('/')[1]}");
                if (BackImage != null)
                    patient.BackImageUrl = await UploadToS3Async(BackImage, $"patients/{Guid.NewGuid()}_back.{BackImage.ContentType.Split('/')[1]}");

                _context.Add(patient);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(patient);
        }

        // GET: Patients/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var patient = await _context.Patients.FindAsync(id);
            if (patient == null)
            {
                return NotFound();
            }
            return View(patient);
        }

        // POST: Patients/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,DateOfBirth,Email,Contact")] Patient patient, IFormFile? FrontImage, IFormFile? LeftImage, IFormFile? RightImage, IFormFile? BackImage)
        {
            if (id != patient.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    if (FrontImage != null)
                        patient.FrontImageUrl = await UploadToS3Async(FrontImage, $"patients/{Guid.NewGuid()}_front.{FrontImage.ContentType.Split('/')[1]}");
                    if (LeftImage != null)
                        patient.LeftImageUrl = await UploadToS3Async(LeftImage, $"patients/{Guid.NewGuid()}_left.{LeftImage.ContentType.Split('/')[1]}");
                    if (RightImage != null)
                        patient.RightImageUrl = await UploadToS3Async(RightImage, $"patients/{Guid.NewGuid()}_right.{RightImage.ContentType.Split('/')[1]}");
                    if (BackImage != null)
                        patient.BackImageUrl = await UploadToS3Async(BackImage, $"patients/{Guid.NewGuid()}_back.{BackImage.ContentType.Split('/')[1]}");

                    _context.Update(patient);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PatientExists(patient.Id))
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
            return View(patient);
        }

        // GET: Patients/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var patient = await _context.Patients.FirstOrDefaultAsync(m => m.Id == id);
            if (patient == null)
            {
                return NotFound();
            }

            return View(patient);
        }

        // POST: Patients/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var patient = await _context.Patients.FindAsync(id);
            if (patient != null)
            {
                _context.Patients.Remove(patient);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PatientExists(int id)
        {
            return _context.Patients.Any(e => e.Id == id);
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
                ContentType = file.ContentType
            };

            await transferUtility.UploadAsync(uploadRequest);

            return $"https://{_configuration["AWS:BucketName"]}.s3.{_configuration["AWS:Region"]}.amazonaws.com/{fileName}";
        }
    }
}
