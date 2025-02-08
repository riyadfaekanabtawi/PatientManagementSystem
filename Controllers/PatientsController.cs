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
        public async Task<IActionResult> Create(Patient patient, List<IFormFile> FrontImage)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    patient.FrontImageUrl = await UploadFileToS3(FrontImage.FirstOrDefault());
                    _context.Add(patient);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error creating patient: {ex.Message}");
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
        public async Task<IActionResult> Edit(int id, Patient patient, List<IFormFile> FrontImage)
        {
            if (id != patient.Id) return NotFound();
            if (ModelState.IsValid)
            {
                try
                {
                    var existingPatient = await _context.Patients.FindAsync(id);
                    if (existingPatient == null) return NotFound();

                    if (FrontImage.Any()) 
                        existingPatient.FrontImageUrl = await UploadFileToS3(FrontImage.FirstOrDefault());

                    existingPatient.Name = patient.Name;
                    existingPatient.DateOfBirth = patient.DateOfBirth;
                    existingPatient.Email = patient.Email;
                    existingPatient.Contact = patient.Contact;

                    _context.Update(existingPatient);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Patients.Any(e => e.Id == id)) return NotFound();
                    throw;
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

        [HttpPost("Generate3DModel/{id}")]
        public async Task<IActionResult> Generate3DModel(int id)
        {
            var patient = await _context.Patients.FindAsync(id);
            if (patient == null || string.IsNullOrEmpty(patient.FrontImageUrl))
                return Json(new { success = false, message = "Invalid patient or missing front image." });

            try
            {
                // Define the Curl command
                string curlCommand = $@"
                    curl https://api.meshy.ai/openapi/v1/image-to-3d \
                    -X POST \
                    -H 'Authorization: Bearer {MeshyApiKey}' \
                    -H 'Content-Type: application/json' \
                    -d '{{
                        ""image_url"": ""{patient.FrontImageUrl}"",
                        ""enable_pbr"": true,
                        ""should_remesh"": true,
                        ""should_texture"": true
                    }}'";

                // Execute the Curl command
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-c \"{curlCommand}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string result = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    _logger.LogError($"Curl error: {error}");
                    return Json(new { success = false, message = "Failed to create 3D task" });
                }

                // Parse the result
                var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
                var taskId = jsonResult.GetProperty("result").GetString();

                return Json(new { success = true, taskId });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception while generating 3D model: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while creating the 3D task" });
            }
        }


        [HttpGet("CheckModelStatus/{taskId}/{id}")]
        public async Task<IActionResult> CheckModelStatus(string taskId, int id)
        {
            try
            {
                // Define the Curl command to check the task status
                string curlCommand = $@"
                    curl https://api.meshy.ai/openapi/v1/image-to-3d/{taskId} \
                    -H 'Authorization: Bearer {MeshyApiKey}'";

                // Execute the Curl command
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-c \"{curlCommand}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string result = await process.StandardOutput.ReadToEndAsync();
                string error = await process.StandardError.ReadToEndAsync();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    _logger.LogError($"Curl error: {error}");
                    return Json(new { success = false, message = "Failed to fetch 3D model status" });
                }

                // Parse the result
                var jsonResult = JsonSerializer.Deserialize<JsonElement>(result);
                var status = jsonResult.GetProperty("status").GetString();

                if (status != "SUCCEEDED")
                {
                    return Json(new { success = false, pending = true });
                }

                // Fetch the model URL and upload it to S3
                var modelUrl = jsonResult.GetProperty("model_urls").GetProperty("glb").GetString();
                var s3Key = $"models/patient_{id}.glb";
                var uploadSuccess = await UploadToS3(modelUrl, s3Key);

                if (!uploadSuccess)
                {
                    return Json(new { success = false, message = "Failed to upload model to S3" });
                }

                var patient = await _context.Patients.FindAsync(id);
                if (patient != null)
                {
                    patient.Model3DUrl = $"https://{S3BucketName}.s3.amazonaws.com/{s3Key}";
                    _context.Update(patient);
                    await _context.SaveChangesAsync();
                }

                return Json(new { success = true, modelUrl = patient?.Model3DUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception while checking 3D model status: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while checking the 3D model status" });
            }
        }


        private async Task<string> UploadFileToS3(IFormFile file)
        {
            if (file == null || file.Length == 0) return null;

            var fileName = $"patients/{Guid.NewGuid()}_{file.FileName}";

            using (var stream = file.OpenReadStream())
            {
                var uploadRequest = new TransferUtilityUploadRequest
                {
                    InputStream = stream,
                    BucketName = S3BucketName,
                    Key = fileName,
                    ContentType = file.ContentType,
                    CannedACL = S3CannedACL.PublicRead
                };

                var transferUtility = new TransferUtility(_s3Client);
                await transferUtility.UploadAsync(uploadRequest);
            }

            return $"https://{S3BucketName}.s3.amazonaws.com/{fileName}";
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
