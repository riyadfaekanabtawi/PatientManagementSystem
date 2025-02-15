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
    [TypeFilter(typeof(AdminAuthFilter))]
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
                    var front_file = FrontImage.FirstOrDefault();

                    if (front_file != null)
                    {
                        patient.FrontImageUrl = await UploadFileToS3(front_file);

                    } else {

                        throw new ArgumentNullException(nameof(front_file), "File cannot be null");

                    }

                    var left_image = LeftImage.FirstOrDefault();
                    if (left_image != null)
                    {
                        patient.LeftImageUrl = await UploadFileToS3(left_image);

                    } else {

                        throw new ArgumentNullException(nameof(left_image), "File cannot be null");

                    }

                    var right_image = LeftImage.FirstOrDefault();
                    if (right_image != null)
                    {
                        patient.RightImageUrl = await UploadFileToS3(right_image);

                    } else {

                        throw new ArgumentNullException(nameof(right_image), "File cannot be null");

                    }

                    var back_image = BackImage.FirstOrDefault();
                    if (back_image != null)
                    {
                        patient.BackImageUrl = await UploadFileToS3(back_image);

                    } else {

                        throw new ArgumentNullException(nameof(back_image), "File cannot be null");
                    }
                    
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
                    {
                        var frontImageFile = FrontImage.FirstOrDefault();
                        if (frontImageFile != null)
                        {
                            existingPatient.FrontImageUrl = await UploadFileToS3(frontImageFile);
                        }
                    }

                    if (LeftImage.Any())
                    {
                        var leftImageFile = LeftImage.FirstOrDefault();
                        if (leftImageFile != null)
                        {
                            existingPatient.LeftImageUrl = await UploadFileToS3(leftImageFile);
                        }
                    }

                    if (RightImage.Any())
                    {
                        var rightImageFile = RightImage.FirstOrDefault();
                        if (rightImageFile != null)
                        {
                            existingPatient.RightImageUrl = await UploadFileToS3(rightImageFile);
                        }
                    }

                    if (BackImage.Any())
                    {
                        var backImageFile = BackImage.FirstOrDefault();
                        if (backImageFile != null)
                        {
                            existingPatient.BackImageUrl = await UploadFileToS3(backImageFile);
                        }
                    }



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
        [Route("Patients/RemeshModel/{id}")]
        public async Task<IActionResult> RemeshModel(int id)
        {
            var patient = await _context.Patients.FindAsync(id);
            if (patient == null || string.IsNullOrEmpty(patient.ThreeDObjectId))
                return Json(new { success = false, message = "No 3D object found for remeshing." });

            try
            {
                var requestBody = new
                {
                    input_task_id = patient.ThreeDObjectId,
                    target_formats = new[] { "glb", "fbx" },
                    topology = "quad",
                    target_polycount = 50000,
                    resize_height = 1.0,
                    origin_at = "bottom"
                };

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {MeshyApiKey}");

                var response = await httpClient.PostAsync(
                    "https://api.meshy.ai/openapi/v1/remesh",
                    new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
                );

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Meshy API error: {errorContent}");
                    return Json(new { success = false, message = "Failed to create remesh task." });
                }

                var result = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
                var remeshTaskId = result.GetProperty("result").GetString();

                // Update the patient's remeshed task ID
                patient.RemeshedTaskId = remeshTaskId;
                await _context.SaveChangesAsync();

                return Json(new { success = true, remeshTaskId });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception in RemeshModel: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while creating the remesh task." });
            }
        }


        [HttpGet]
        [Route("Patients/CheckRemeshStatus/{remeshTaskId}/{id}")]
        public async Task<IActionResult> CheckRemeshStatus(string remeshTaskId, int id)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {MeshyApiKey}");

                var response = await httpClient.GetAsync($"https://api.meshy.ai/openapi/v1/remesh/{remeshTaskId}");
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Meshy Remesh Status API error: {errorContent}");
                    return Json(new { success = false, message = "Failed to fetch remesh status." });
                }

                var result = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
                var status = result.GetProperty("status").GetString();

                if (status != "SUCCEEDED")
                {
                    return Json(new { success = false, pending = true });
                }

                // Extract GLB URL from Meshy
                var glbUrl = result.GetProperty("model_urls").GetProperty("glb").GetString();
                if (string.IsNullOrEmpty(glbUrl))
                {
                    _logger.LogError($"Meshy task {remeshTaskId} did not return a GLB URL.");
                    return Json(new { success = false, message = "No GLB URL found for remeshed task." });
                }

                // Generate a unique key for S3
                var s3Key = $"models/patient_{id}_{Guid.NewGuid()}.glb";

                // Upload the GLB model to S3
                var uploadSuccess = await UploadToS3(glbUrl, s3Key);
                if (!uploadSuccess)
                {
                    _logger.LogError("Failed to upload the 3D model to S3.");
                    return Json(new { success = false, message = "Failed to upload remeshed model to S3." });
                }

                // Construct S3 URL
                var s3ModelUrl = $"https://{S3BucketName}.s3.amazonaws.com/{s3Key}";

                // Update the patient record with the new model URL
                var patient = await _context.Patients.FindAsync(id);
                if (patient != null)
                {
                    patient.Model3DUrl = s3ModelUrl; // Save the new S3 URL
                    patient.RemeshedTaskId = remeshTaskId; // Update the task ID for tracking
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation($"3D model successfully uploaded to S3 and updated for patient {id}.");

                return Json(new { success = true, modelUrl = s3ModelUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Exception in CheckRemeshStatus: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while checking remesh status." });
            }
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

        public async Task<IActionResult> AdjustFace(int id)
        {
            var patient = await _context.Patients.FindAsync(id);
            if (patient == null)
            {
                return NotFound("Patient not found.");
            }
            return View(patient);
        }

        [Route("Patients/AdjustmentDetail/{id}")]
        public async Task<IActionResult> AdjustmentDetail(int id)
        {
            var adjustment = await _context.FaceAdjustmentHistories.Include(h => h.Patient).FirstOrDefaultAsync(h => h.Id == id);
            if (adjustment == null) return NotFound();

            return View(adjustment);
        }


        [Route("Patients/SaveFaceAdjustment/{id}")]
        [HttpPost]
        public async Task<IActionResult> SaveFaceAdjustment(int id, [FromBody] FaceAdjustmentRequest request)
        {
            var patient = await _context.Patients.FindAsync(id);
            if (patient == null) return NotFound();

            var fileName = $"adjustments/{id}_{DateTime.UtcNow.Ticks}.glb";
            var awsAccessKey = _configuration["AWS:AccessKey"];
            var awsSecretKey = _configuration["AWS:SecretKey"];
            var awsRegion = _configuration["AWS:Region"];
            var s3Bucket = _configuration["AWS:BucketName"];

            var s3Client = new AmazonS3Client(awsAccessKey, awsSecretKey, RegionEndpoint.GetBySystemName(awsRegion));

            using (var stream = new MemoryStream(Convert.FromBase64String(request.Model3D.Split(',')[1])))
            {
                var uploadRequest = new TransferUtilityUploadRequest
                {
                    InputStream = stream,
                    BucketName = s3Bucket,
                    Key = fileName,
                    ContentType = "model/gltf-binary",
                    CannedACL = S3CannedACL.PublicRead
                };

                var transferUtility = new TransferUtility(s3Client);
                await transferUtility.UploadAsync(uploadRequest);
            }

            var model3DUrl = $"https://{s3Bucket}.s3.amazonaws.com/{fileName}";

            var history = new FaceAdjustmentHistory
            {
                PatientId = id,
                Model3DUrl = model3DUrl,
                Notes = request.Notes,
                AdjustmentDate = DateTime.UtcNow
            };

            _context.FaceAdjustmentHistories.Add(history);
            await _context.SaveChangesAsync();

            return Json(new { success = true, model3DUrl });
        }

        public class FaceAdjustmentRequest
        {
            public string Snapshot { get; set; } = string.Empty;
            public string Notes { get; set; } = string.Empty;
        }

        public async Task<IActionResult> History(int id)
        {
            var patient = await _context.Patients
                .Include(p => p.AdjustmentHistory)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (patient == null) return NotFound();

            return View(patient);
        }

        [HttpPost]
        [Route("Patients/Generate3DModel/{id}")]
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
                    ai_model = "meshy-4",
                    topology = "quad",
                    target_polycount = 300000,
                    should_remesh = true,
                    enable_pbr = true,
                    should_texture = true,
                    symmetry_mode = "auto",
                    texture_prompt = "High-quality realistic texture with accurate facial details"
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

                patient.MeshyTaskId = taskId;
                await _context.SaveChangesAsync();

                return Json(new { success = true, taskId });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception in Generate3DModel: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while creating the 3D task." });
            }
        }


        
        [HttpGet]
        [Route("Patients/CheckModelStatus/{taskId}/{id}")]
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

                // Fetch the 3D model URL and object ID
                var modelUrl = result.GetProperty("model_urls").GetProperty("glb").GetString();
                var objectId = result.GetProperty("id").GetString();

                // Upload the 3D model to S3
                var s3Bucket = _configuration["AWS:BucketName"];
                var s3Key = $"3dmodels/patient_{id}.glb";

                if (string.IsNullOrEmpty(modelUrl) || string.IsNullOrEmpty(s3Key))
                {
                    return Json(new { success = false, message = "Model URL or S3 key is missing." });
                }

                if (!await UploadToS3(modelUrl, s3Key))
                {
                    return Json(new { success = false, message = "Failed to upload the 3D model to S3." });
                }

                var s3ModelUrl = $"https://{s3Bucket}.s3.amazonaws.com/{s3Key}";

                // Update the patient record in the database
                var patient = await _context.Patients.FindAsync(id);
                if (patient != null)
                {
                    patient.Model3DUrl = s3ModelUrl;
                    patient.ThreeDObjectId = objectId; // Save the object ID
                    await _context.SaveChangesAsync();
                }

                return Json(new { success = true, modelUrl = s3ModelUrl });
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
            if (file == null || file.Length == 0) return "null";

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
                // Fetch the file bytes from the URL
                using var httpClient = new HttpClient();
                var fileBytes = await httpClient.GetByteArrayAsync(fileUrl);

                // Get AWS configuration from settings
                var awsAccessKey = _configuration["AWS:AccessKey"];
                var awsSecretKey = _configuration["AWS:SecretKey"];
                var awsRegion = _configuration["AWS:Region"];
                var s3Bucket = _configuration["AWS:BucketName"];

                // Create S3 client
                var s3Client = new AmazonS3Client(awsAccessKey, awsSecretKey, RegionEndpoint.GetBySystemName(awsRegion));

                // Create upload request
                using var memoryStream = new MemoryStream(fileBytes);
                var uploadRequest = new TransferUtilityUploadRequest
                {
                    InputStream = memoryStream,
                    BucketName = s3Bucket,
                    Key = key,
                    ContentType = "model/gltf-binary", // MIME type for GLB files
                    CannedACL = S3CannedACL.PublicRead
                };

                // Upload the file
                var transferUtility = new TransferUtility(s3Client);
                await transferUtility.UploadAsync(uploadRequest);

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
