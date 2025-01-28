using Microsoft.EntityFrameworkCore;
using PatientManagementSystem.Models;

namespace PatientManagementSystem.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Patient> Patients { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<FaceAdjustmentHistory> FaceAdjustmentHistories { get; set; }
        public DbSet<Admin> Admins { get; set; }
        public override int SaveChanges()
        {
            foreach (var entry in ChangeTracker.Entries<Patient>())
            {
                if (entry.Entity.DateOfBirth.Kind == DateTimeKind.Unspecified)
                {
                    entry.Entity.DateOfBirth = DateTime.SpecifyKind(entry.Entity.DateOfBirth, DateTimeKind.Utc);
                }
            }
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries<Patient>())
            {
                if (entry.Entity.DateOfBirth.Kind == DateTimeKind.Unspecified)
                {
                    entry.Entity.DateOfBirth = DateTime.SpecifyKind(entry.Entity.DateOfBirth, DateTimeKind.Utc);
                }
            }
            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
