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

        public async Task<IActionResult> Index()
        {
            var patients = await _context.Patients.ToListAsync();
            return View(patients);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (!id.HasValue) return NotFound();

            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.Id == id);
            if (patient == null) return NotFound();

            return View(patient);
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
    }
}
