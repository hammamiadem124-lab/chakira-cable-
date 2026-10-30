namespace HRLeaveApp.Models
{
    public class LeaveRequest
    {
        public int LeaveRequestId { get; set; }
        public int EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string LeaveType { get; set; } = "Annual"; // Annual, Sick, Unpaid, Other
        public string? Reason { get; set; }

        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        public int? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? AdminComment { get; set; }
    }

    public class WorkSchedule
    {
        public int ScheduleId { get; set; }
        public int EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        public DateTime WorkDate { get; set; }
        public TimeSpan? ShiftStart { get; set; }
        public TimeSpan? ShiftEnd { get; set; }
        public string Status { get; set; } = "Working"; // Working, Leave, Holiday, Weekend
    }
}
