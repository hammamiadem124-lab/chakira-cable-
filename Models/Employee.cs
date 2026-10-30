namespace HRLeaveApp.Models
{
    public class Employee
    {
        public int EmployeeId { get; set; }
        public string CIN { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Email { get; set; }

        // Ne jamais exposer ces champs dans les réponses API
        public string PasswordHash { get; set; } = string.Empty;
        public string PasswordSalt { get; set; } = "notused"; // Non utilisé avec BCrypt (le sel est intégré au hash), gardé pour compatibilité avec la colonne DB

        public int RoleId { get; set; }
        public Role? Role { get; set; }

        public string? Department { get; set; }
        public DateTime? HireDate { get; set; }
        public int AnnualLeaveBalance { get; set; } = 18; // Solde de jours de congé restants
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int FailedLoginAttempts { get; set; } = 0;
        public DateTime? LockoutUntil { get; set; }

        public ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
        public ICollection<WorkSchedule> WorkSchedules { get; set; } = new List<WorkSchedule>();
    }

    public class Role
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty; // "Employee" ou "Admin"
    }
}
