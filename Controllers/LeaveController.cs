using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using HRLeaveApp.Data;
using HRLeaveApp.Models;

namespace HRLeaveApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Nécessite un token JWT valide pour toutes les actions
    public class LeaveController : ControllerBase
    {
        private readonly AppDbContext _db;

        public LeaveController(AppDbContext db)
        {
            _db = db;
        }

        private int CurrentEmployeeId =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public class CreateLeaveRequestDto
        {
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public string LeaveType { get; set; } = "Annual";
            public string? Reason { get; set; }
        }

        // ---- EMPLOYÉ : créer une demande de congé ----
        [HttpPost]
        public async Task<IActionResult> CreateLeaveRequest([FromBody] CreateLeaveRequestDto request)
        {
            if (request.EndDate < request.StartDate)
                return BadRequest(new { message = "La date de fin doit être après la date de début." });

            var leave = new LeaveRequest
            {
                EmployeeId = CurrentEmployeeId,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                LeaveType = request.LeaveType,
                Reason = request.Reason,
                Status = "Pending"
            };

            _db.LeaveRequests.Add(leave);
            await _db.SaveChangesAsync();

            return Ok(leave);
        }

        // ---- EMPLOYÉ : voir ses propres demandes ----
        [HttpGet("mine")]
        public async Task<IActionResult> GetMyLeaveRequests()
        {
            var requests = await _db.LeaveRequests
                .Where(l => l.EmployeeId == CurrentEmployeeId)
                .OrderByDescending(l => l.RequestedAt)
                .ToListAsync();

            return Ok(requests);
        }

        // ---- ADMIN : voir toutes les demandes ----
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllLeaveRequests([FromQuery] string? status)
        {
            var query = _db.LeaveRequests
                .Include(l => l.Employee)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(l => l.Status == status);

            var requests = await query
                .OrderByDescending(l => l.RequestedAt)
                .Select(l => new
                {
                    l.LeaveRequestId,
                    l.StartDate,
                    l.EndDate,
                    l.LeaveType,
                    l.Reason,
                    l.Status,
                    l.RequestedAt,
                    EmployeeName = l.Employee!.FirstName + " " + l.Employee.LastName,
                    l.Employee.CIN
                })
                .ToListAsync();

            return Ok(requests);
        }

        public class ReviewRequest
        {
            public bool Approve { get; set; }
            public string? Comment { get; set; }
        }

        // ---- ADMIN : approuver / refuser une demande ----
        [HttpPut("{id}/review")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ReviewLeaveRequest(int id, [FromBody] ReviewRequest request)
        {
            var leave = await _db.LeaveRequests.FindAsync(id);
            if (leave == null) return NotFound();

            leave.Status = request.Approve ? "Approved" : "Rejected";
            leave.ReviewedBy = CurrentEmployeeId;
            leave.ReviewedAt = DateTime.UtcNow;
            leave.AdminComment = request.Comment;

            await _db.SaveChangesAsync();
            return Ok(leave);
        }
    }
}
