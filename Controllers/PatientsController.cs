using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Amazon;
using Amazon.S3;
using Amazon.S3.Transfer;
using System.Diagnostics;
using PatientManagementSystem.Data;
using PatientManagementSystem.Models;
using PatientManagementSystem.Services;

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

        private const string MeshyApiKey = "msy_yhs6I9Ks7ck8uvnraxsYG8gMFRfC9GawEVaP";
        private const string S3BucketName = "patients-tree";

        public PatientsController(AppDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration, I3DModelService modelService, IAmazonS3 s3Client, ILogger<PatientsController> logger)
        {
            _context = context;
            _configuration = configuration;
            _3DModelService = modelService;
            _s3Client = s3Client;
            _httpClient = httpClientFactory.CreateClient();
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var patients = await _context.Patients.ToListAsync();
            return View(patients);
        }

        public async Task<IActionResult> Details(int id)
        {
            var patient = await _context.Patients.FindAsync(id);
            if (patient == null) return NotFound();
            return View(patient);
        }

        public IActionResult Create() => View();

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

        public async Task<IActionResult> Edit(int id)
        {
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

        public async Task<IActionResult> Delete(int id)
        {
            var patient = await _context.Patients.FindAsync(id);
            if (patient == null) return NotFound();
            return View(patient);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var patient = await _context.Patients.FindAsync(id);
            if (patient == null) return NotFound();

            _context.Patients.Remove(patient);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public IActionResult AdjustFace(int id)
        {
            var patient = _context.Patients.FirstOrDefault(p => p.Id == id);
            if (patient == null)
            {
                return NotFound("Patient not found.");
            }

            return View(patient);
        }

        [HttpPost]
        public async Task<IActionResult> SaveFaceAdjustment(int id, [FromBody] FaceAdjustmentRequest request)
        {
            var patient = await _context.Patients.FindAsync(id);
            if (patient == null) return NotFound();

            var fileName = $"adjustments/{id}_{DateTime.UtcNow.Ticks}.png";

            using (var stream = new MemoryStream(Convert.FromBase64String(request.Snapshot.Split(',')[1])))
            {
                var uploadRequest = new TransferUtilityUploadRequest
                {
                    InputStream = stream,
                    BucketName = S3BucketName,
                    Key = fileName,
                    ContentType = "image/png",
                    CannedACL = S3CannedACL.PublicRead
                };

                var transferUtility = new TransferUtility(_s3Client);
                await transferUtility.UploadAsync(uploadRequest);
            }

            var snapshotUrl = $"https://{S3BucketName}.s3.amazonaws.com/{fileName}";

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
        [Route("Patients/Generate3DModel/{id}")]
        [HttpPost("Generate3DModel/{id}")]
        public async Task<IActionResult> Generate3DModel(int id)
        {
            var patient = await _context.Patients.FindAsync(id);
            if (patient == null || string.IsNullOrEmpty(patient.FrontImageUrl))
                return Json(new { success = false, message = "Invalid patient or missing front image." });

            try
            {
                var requestBody = new
                {
                    image_url = patient.FrontImageUrl,
                    enable_pbr = true,
                    should_remesh = true,
                    should_texture = true
                };

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {MeshyApiKey}");

                var response = await httpClient.PostAsync(
                    "https://api.meshy.ai/openapi/v1/image-to-3d",
                    new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
                );

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Meshy API error: {errorContent}");
                    return Json(new { success = false, message = "Failed to create 3D task." });
                }

                var result = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
                var taskId = result.GetProperty("result").GetString();

                return Json(new { success = true, taskId });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception in Generate3DModel: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while creating the 3D task." });
            }
        }

        [Route("Patients/CheckModelStatus/{taskId}/{id}")]
        [HttpGet]
        public async Task<IActionResult> CheckModelStatus(string taskId, int id)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {MeshyApiKey}");

                var response = await httpClient.GetAsync($"https://api.meshy.ai/openapi/v1/image-to-3d/{taskId}");
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Meshy API error: {errorContent}");
                    return Json(new { success = false, message = "Failed to fetch 3D model status." });
                }

                var result = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
                var status = result.GetProperty("status").GetString();

                if (status != "SUCCEEDED")
                {
                    return Json(new { success = false, pending = true });
                }

                // Fetch the model URL directly for rendering
                var modelUrl = result.GetProperty("model_urls").GetProperty("glb").GetString();

                return Json(new { success = true, modelUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception in CheckModelStatus: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while checking the 3D model status." });
            }
        }



        private bool PatientExists(int id)
        {
            return _context.Patients.Any(e => e.Id == id);
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

        private async Task<bool> UploadToS3(string fileUrl, string key)
        {
            try
            {
                using var httpClient = new HttpClient();
                var fileBytes = await httpClient.GetByteArrayAsync(fileUrl);
                using var transferUtility = new TransferUtility(_s3Client);
                using var memoryStream = new MemoryStream(fileBytes);

                await transferUtility.UploadAsync(memoryStream, S3BucketName, key);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ S3 Upload Failed: {ex.Message}");
                return false;
            }
        }

    }
}
