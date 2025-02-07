using System;
using System.IO;                        // Added for file/stream operations
using System.Linq;
using System.Net.Http;                  // Added for HttpClient
using System.Net.Http.Headers;          // Added for MediaTypeHeaderValue
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatientManagementSystem.Data;
using PatientManagementSystem.Models;
using Amazon;
using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PatientManagementSystem.Services;
using System.Text.Json;   

namespace PatientManagementSystem.Controllers
{
    public class PatientsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly I3DModelService _3DModelService;
        private readonly IAmazonS3 _s3Client;
        private readonly ILogger<PatientsController> _logger;

        private readonly HttpClient _httpClient;

        public PatientsController(AppDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, I3DModelService modelService, IAmazonS3 s3Client, ILogger<PatientsController> logger)
        {
            _context = context;
            _configuration = configuration;
            _3DModelService = modelService;
            _s3Client = s3Client;
            _httpClient = httpClientFactory.CreateClient();
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

            var awsAccessKey = _configuration["AWS:AccessKey"];
            var awsSecretKey = _configuration["AWS:SecretKey"];
            var awsRegion = _configuration["AWS:Region"];
            var s3Bucket = _configuration["AWS:BucketName"];

            // Use RegionEndpoint from Amazon SDK
            var s3Client = new AmazonS3Client(awsAccessKey, awsSecretKey, RegionEndpoint.GetBySystemName(awsRegion));

            var fileName = $"patients/{Guid.NewGuid()}_{file.FileName}";

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

                var transferUtility = new TransferUtility(s3Client);
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

         // POST: /Patients/Generate3DModel/{id}
        [HttpPost]
        [Route("Patients/Generate3DModel/{id}")]
        public async Task<IActionResult> Generate3DModel(int id)
        {
            // Retrieve the patient record from the database.
            var patient = await _context.Patients.FindAsync(id);
            if (patient == null)
            {
                return NotFound(new { success = false, message = "Patient not found." });
            }

            // Ensure that a front image URL exists.
            if (string.IsNullOrEmpty(patient.FrontImageUrl))
            {
                return BadRequest(new { success = false, message = "No front image available for this patient." });
            }

            try
            {
                // Download the image from the provided URL.
                var imageResponse = await _httpClient.GetAsync(patient.FrontImageUrl);
                if (!imageResponse.IsSuccessStatusCode)
                {
                    return BadRequest(new { success = false, message = "Failed to download the front image." });
                }

                // Obtain the image stream.
                var imageStream = await imageResponse.Content.ReadAsStreamAsync();

                using var multipartContent = new MultipartFormDataContent();
                var streamContent = new StreamContent(imageStream);
                // Set an appropriate content type (adjust if your image is not JPEG).
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                // Add the file content with the key "front" (which your Flask code expects)
                multipartContent.Add(streamContent, "front", "front_image.jpg");

                _logger.LogInformation("Sending request to ML service...");
                // Send the POST request to your Flask ML service.
                var mlResponse = await _httpClient.PostAsync("http://127.0.0.1:5001/flask-api/generate", multipartContent);
                var responseData = await mlResponse.Content.ReadAsStringAsync();
                _logger.LogInformation($"ML Service Response: {responseData}");

                if (!mlResponse.IsSuccessStatusCode)
                {
                    _logger.LogError($"ML service call failed. Status: {mlResponse.StatusCode}, Response: {responseData}");
                    return BadRequest(new { success = false, message = responseData });
                }

                // Deserialize the response; your Flask service returns JSON with a "mesh_file_url" key.
                var jsonResponse = JsonSerializer.Deserialize<Dictionary<string, string>>(responseData);
                if (jsonResponse != null && jsonResponse.ContainsKey("mesh_file_url"))
                {
                    patient.Model3DUrl = jsonResponse["mesh_file_url"];
                    await _context.SaveChangesAsync();
                    return Json(new { success = true });
                }
                else
                {
                    _logger.LogError("ML service response did not contain 'mesh_file_url'.");
                    return BadRequest(new { success = false, message = "Invalid response from ML service." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception calling ML service: {ex.Message}");
                return BadRequest(new { success = false, message = ex.Message });
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
