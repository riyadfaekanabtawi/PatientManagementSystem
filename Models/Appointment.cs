using System;
using System.ComponentModel.DataAnnotations;

namespace PatientManagementSystem.Models
{
    public class Appointment
    {
        public int Id { get; set; }

        [Required]
        public int PatientId { get; set; }

        [Required]
        public DateTime AppointmentDateTime { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        // Optional images for the appointment
        public string? FrontImageUrl { get; set; }
        public string? LeftImageUrl { get; set; }
        public string? RightImageUrl { get; set; }
        public string? BackImageUrl { get; set; }

        // Navigation property
        public Patient? Patient { get; set; }
    }
}
