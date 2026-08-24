using Microsoft.EntityFrameworkCore;
using AttendanceTrackingSystem.Models;

namespace AttendanceTrackingSystem.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<SchoolClass> SchoolClasses { get; set; }
        public DbSet<Enrollment> Enrollments { get; set; }
        public DbSet<AttendanceSession> AttendanceSessions { get; set; }
        public DbSet<PublicHoliday> PublicHolidays { get; set; }
        public DbSet<AttendanceRecord> AttendanceRecords { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<SchoolClass>()
                .HasOne(c => c.Teacher)
                .WithMany()
                .HasForeignKey(c => c.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
