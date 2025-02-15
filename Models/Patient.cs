using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PatientManagementSystem.Models
{
    public class Patient
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Date)]
        public DateTime DateOfBirth { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Phone]
        public string Contact { get; set; } = string.Empty;

        public string? FrontImageUrl { get; set; }
        public string? LeftImageUrl { get; set; }
        public string? RightImageUrl { get; set; }
        public string? BackImageUrl { get; set; }

        public string? Model3DUrl { get; set; }
        public string? MeshyTaskId { get; set; }

        public string? RemeshedTaskId { get; set; }

         public string? ThreeDObjectId { get; set; }

        public int? CheekAdjustment { get; set; }
        public int? ChinAdjustment { get; set; }
        public int? NoseAdjustment { get; set; }

        public ICollection<FaceAdjustmentHistory>? AdjustmentHistory { get; set; }
    }

    public class FaceAdjustmentHistory
    {
        public int Id { get; set; }
        public int PatientId { get; set; }

        public string? AdjustedImageUrl { get; set; } // Keep this if you still want a snapshot

        public string? Model3DUrl { get; set; } // New: Stores GLB model file in S3

        [Required]
        public DateTime AdjustmentDate { get; set; } = DateTime.UtcNow;

        public string? Notes { get; set; }

        public Patient Patient { get; set; } = null!;
    }

}
