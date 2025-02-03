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
using Microsoft.Extensions.Logging;
using PatientManagementSystem.Services;  // ✅ Import the service
using System.Text.Json;  // ✅ Required for JSON Parsing

namespace PatientManagementSystem.Controllers
{
    public class PatientsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly I3DModelService _3DModelService;
        private readonly IAmazonS3 _s3Client;
        private readonly ILogger<PatientsController> _logger;

        public PatientsController(AppDbContext context, IConfiguration configuration, I3DModelService modelService, IAmazonS3 s3Client, ILogger<PatientsController> logger)
        {
            _context = context;
            _configuration = configuration;
            _3DModelService = modelService;
            _s3Client = s3Client;
            _logger = logger;
        }

        public async Task<IActionResult> Index(int? session_id)
        {
            if (session_id.HasValue)
            {
                HttpContext.Session.SetString("AdminLoggedIn", session_id.Value.ToString());
            }
            var patients = await _context.Patients.ToListAsync();
            return View(patients);
        }

       // GET: Patients/Details/5
        public async Task<IActionResult> Details(int? id, int? session_id)
        {
            if (session_id.HasValue)
            {
                HttpContext.Session.SetString("AdminLoggedIn", session_id.Value.ToString());
            }

            if (!id.HasValue) return NotFound();

            var patient = await _context.Patients
                .FirstOrDefaultAsync(m => m.Id == id);
            if (patient == null) return NotFound();

            return View(patient);
        }

        // GET: Patients/Create
        public IActionResult Create(int? session_id)
        {
            if (session_id.HasValue)
            {
                HttpContext.Session.SetString("AdminLoggedIn", session_id.Value.ToString());
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Patient patient, List<IFormFile> FrontImage, List<IFormFile> LeftImage, List<IFormFile> RightImage, List<IFormFile> BackImage)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Upload images to S3 and store URLs
                    patient.FrontImageUrl = await UploadFileToS3(FrontImage.FirstOrDefault());
                    patient.LeftImageUrl = await UploadFileToS3(LeftImage.FirstOrDefault());
                    patient.RightImageUrl = await UploadFileToS3(RightImage.FirstOrDefault());
                    patient.BackImageUrl = await UploadFileToS3(BackImage.FirstOrDefault());

                    // Save patient to database
                    _context.Add(patient);
                    await _context.SaveChangesAsync();
                    TempData["Message"] = "Paciente cargado correctamente";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error creating patient: {ex.Message}");
                    ModelState.AddModelError("", "An error occurred while saving the patient.");
                }
            }

            return View(patient);
        }

        private async Task<string> UploadFileToS3(IFormFile file)
        {
            if (file == null || file.Length == 0) return null;

            var fileName = $"patients/{Guid.NewGuid()}_{file.FileName}";
            var s3Bucket = _configuration["AWS:BucketName"];

            using (var stream = file.OpenReadStream())
            {
                var uploadRequest = new TransferUtilityUploadRequest
                {
                    InputStream = stream,
                    BucketName = s3Bucket,
                    Key = fileName,
                    ContentType = file.ContentType,
                    CannedACL = S3CannedACL.PublicRead
                };

                var transferUtility = new TransferUtility(_s3Client);
                await transferUtility.UploadAsync(uploadRequest);
            }

            return $"https://{s3Bucket}.s3.amazonaws.com/{fileName}";
        }


        // GET: Patients/Edit/5
        public async Task<IActionResult> Edit(int? id, int? session_id)
        {
            if (session_id.HasValue)
            {
                HttpContext.Session.SetString("AdminLoggedIn", session_id.Value.ToString());
            }

            if (!id.HasValue) return NotFound();

            var patient = await _context.Patients.FindAsync(id);
            if (patient == null) return NotFound();

            return View(patient);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Patient patient, 
            List<IFormFile> FrontImage, List<IFormFile> LeftImage, 
            List<IFormFile> RightImage, List<IFormFile> BackImage)
        {
            if (id != patient.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingPatient = await _context.Patients.FindAsync(id);
                    if (existingPatient == null)
                    {
                        return NotFound();
                    }

                    // Only update the image if a new one is uploaded
                    if (FrontImage.Any()) 
                        existingPatient.FrontImageUrl = await UploadFileToS3(FrontImage.FirstOrDefault());

                    if (LeftImage.Any()) 
                        existingPatient.LeftImageUrl = await UploadFileToS3(LeftImage.FirstOrDefault());

                    if (RightImage.Any()) 
                        existingPatient.RightImageUrl = await UploadFileToS3(RightImage.FirstOrDefault());

                    if (BackImage.Any()) 
                        existingPatient.BackImageUrl = await UploadFileToS3(BackImage.FirstOrDefault());

                    // Update other patient details
                    existingPatient.Name = patient.Name;
                    existingPatient.DateOfBirth = patient.DateOfBirth;
                    existingPatient.Email = patient.Email;
                    existingPatient.Contact = patient.Contact;

                    _context.Update(existingPatient);
                    await _context.SaveChangesAsync();
                    TempData["Message"] = "Paciente actualizado correctamente";
                    return RedirectToAction(nameof(Index));
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
            }
            return View(patient);
        }


        // GET: Patients/Delete/5
        public async Task<IActionResult> Delete(int? id, int? session_id)
        {
            if (session_id.HasValue)
            {
                HttpContext.Session.SetString("AdminLoggedIn", session_id.Value.ToString());
            }

            if (!id.HasValue) return NotFound();

            var patient = await _context.Patients
                .FirstOrDefaultAsync(m => m.Id == id);
            if (patient == null) return NotFound();

            return View(patient);
        }

        // POST: Patients/Delete/5
        [HttpPost]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            _logger.LogInformation($"DeleteConfirmed called with ID: {id}"); // ✅ Debugging

            var patient = await _context.Patients.FindAsync(id);
            if (patient == null)
            {
                _logger.LogWarning($"Patient with ID {id} not found.");
                return NotFound();
            }

            _context.Patients.Remove(patient);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Patient {id} deleted successfully.");
            TempData["Message"] = "Paciente eliminado correctamente";
            return RedirectToAction(nameof(Index));
        }


        private bool PatientExists(int id)
        {
            return _context.Patients.Any(e => e.Id == id);
        }

        [HttpPost]
        public async Task<IActionResult> Generate3DModel(int id)
        {
            var patient = await _context.Patients.FindAsync(id);
            if (patient == null) 
            {
                return Json(new { success = false, message = "Patient not found." });
            }

            _logger.LogInformation($"🚀 Generating 3D model for Patient ID: {id}");

            if (string.IsNullOrEmpty(patient.FrontImageUrl) ||
                string.IsNullOrEmpty(patient.LeftImageUrl) ||
                string.IsNullOrEmpty(patient.RightImageUrl) ||
                string.IsNullOrEmpty(patient.BackImageUrl))
            {
                _logger.LogError("❌ Missing patient images for 3D model generation.");
                return Json(new { success = false, message = "Missing patient images." });
            }

            try
            {
                string apiResponse = await _3DModelService.GenerateModelAsync(
                    patient.FrontImageUrl, 
                    patient.LeftImageUrl, 
                    patient.RightImageUrl, 
                    patient.BackImageUrl
                );

                if (string.IsNullOrEmpty(apiResponse))
                {
                    _logger.LogError("❌ 3D Model generation service returned an empty response.");
                    return Json(new { success = false, message = "3D Model generation service failed." });
                }

                _logger.LogInformation($"📩 API Response: {apiResponse}");

                string modelFileUrl = null;

                try
                {
                    // ✅ Try parsing as JSON first
                    var modelResponse = JsonSerializer.Deserialize<Dictionary<string, string>>(apiResponse);
                    if (modelResponse != null && modelResponse.ContainsKey("modelFileUrl"))
                    {
                        modelFileUrl = modelResponse["modelFileUrl"];
                    }
                }
                catch (JsonException)
                {
                    // ✅ If JSON parsing fails, assume it's a raw URL string
                    _logger.LogWarning("⚠️ API response was not JSON. Assuming direct URL.");
                    modelFileUrl = apiResponse.Trim();
                }

                if (string.IsNullOrEmpty(modelFileUrl))
                {
                    _logger.LogError("❌ No valid model URL found in API response.");
                    return Json(new { success = false, message = "Invalid API response." });
                }

                // ✅ Save Model URL in Database
                patient.Model3DUrl = modelFileUrl;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"✅ 3D model successfully generated: {modelFileUrl}");
                return Json(new { success = true, modelFileUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Exception in 3D Model Generation: {ex.Message}");
                return Json(new { success = false, message = "Error generating 3D model." });
            }
        }

        public IActionResult AdjustFace(int id, int? session_id)
        {
            if (session_id.HasValue)
            {
                HttpContext.Session.SetString("AdminLoggedIn", session_id.Value.ToString());
            }
            var patient = _context.Patients.FirstOrDefault(p => p.Id == id);
            if (patient == null)
            {
                return NotFound("Patient not found.");
            }

            return View(patient);
        }

        [HttpPost]
        public async Task<IActionResult> SaveAdjustments(int id, [FromBody] FaceAdjustmentModel adjustments)
        {
            var patient = await _context.Patients.FindAsync(id);
            if (patient == null) return NotFound();

            patient.CheekAdjustment = adjustments.Cheeks;
            patient.ChinAdjustment = adjustments.Chin;
            patient.NoseAdjustment = adjustments.Nose;

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> SaveFaceAdjustment(int id, [FromBody] FaceAdjustmentRequest request)
        {
            var patient = await _context.Patients.FindAsync(id);
            if (patient == null) return NotFound();

            // Upload Snapshot to S3
            var fileName = $"adjustments/{id}_{DateTime.UtcNow.Ticks}.png";
            var s3Bucket = _configuration["AWS:BucketName"];

            using (var stream = new MemoryStream(Convert.FromBase64String(request.Snapshot.Split(',')[1])))
            {
                var uploadRequest = new TransferUtilityUploadRequest
                {
                    InputStream = stream,
                    BucketName = s3Bucket,
                    Key = fileName,
                    ContentType = "image/png",
                    CannedACL = S3CannedACL.PublicRead
                };

                var transferUtility = new TransferUtility(_s3Client);
                await transferUtility.UploadAsync(uploadRequest);
            }

            var snapshotUrl = $"https://{s3Bucket}.s3.amazonaws.com/{fileName}";

            // Save FaceAdjustmentHistory
            var history = new FaceAdjustmentHistory
            {
                PatientId = id,
                AdjustedImageUrl = snapshotUrl,
                Notes = request.Notes,
                AdjustmentDate = DateTime.UtcNow
            };

            _context.FaceAdjustmentHistories.Add(history);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        public class FaceAdjustmentRequest
        {
            public string Snapshot { get; set; } = string.Empty;
            public string Notes { get; set; } = string.Empty;
        }

        public async Task<IActionResult> History(int id, int? session_id)
        {
            if (session_id.HasValue)
            {
                HttpContext.Session.SetString("AdminLoggedIn", session_id.Value.ToString());
            }
            var patient = await _context.Patients
                .Include(p => p.AdjustmentHistory)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (patient == null) return NotFound();

            return View(patient);
        }

    }
}
