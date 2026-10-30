using Microsoft.EntityFrameworkCore;
using HRLeaveApp.Models;

namespace HRLeaveApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Employee> Employees => Set<Employee>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
        public DbSet<WorkSchedule> WorkSchedules => Set<WorkSchedule>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
        public DbSet<Holiday> Holidays => Set<Holiday>();
        public DbSet<Announcement> Announcements => Set<Announcement>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Employee>().ToTable("Employees");
            modelBuilder.Entity<Role>().ToTable("Roles");
            modelBuilder.Entity<LeaveRequest>().ToTable("LeaveRequests");
            modelBuilder.Entity<WorkSchedule>().ToTable("WorkSchedule");
            modelBuilder.Entity<AuditLog>().ToTable("AuditLogs");
            modelBuilder.Entity<LeaveType>().ToTable("LeaveTypes");
            modelBuilder.Entity<Holiday>().ToTable("Holidays");
            modelBuilder.Entity<Announcement>().ToTable("Announcements");
            modelBuilder.Entity<Announcement>()
                .HasOne(a => a.Creator)
                .WithMany()
                .HasForeignKey(a => a.CreatedBy);

            modelBuilder.Entity<WorkSchedule>().HasKey(w => w.ScheduleId);

            modelBuilder.Entity<Employee>()
                .HasIndex(e => e.CIN)
                .IsUnique();

            modelBuilder.Entity<Employee>()
                .HasOne(e => e.Role)
                .WithMany()
                .HasForeignKey(e => e.RoleId);

            modelBuilder.Entity<LeaveRequest>()
                .HasOne(l => l.Employee)
                .WithMany(e => e.LeaveRequests)
                .HasForeignKey(l => l.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
