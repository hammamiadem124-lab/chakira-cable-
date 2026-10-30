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
    [Authorize]
    public class ScheduleController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ScheduleController(AppDbContext db)
        {
            _db = db;
        }

        private int CurrentEmployeeId =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // ---- EMPLOYÉ : voir son propre calendrier pour un mois donné ----
        // Combine le planning (WorkSchedule) ET les congés approuvés pour affichage
        [HttpGet("mine")]
        public async Task<IActionResult> GetMySchedule([FromQuery] int year, [FromQuery] int month)
        {
            var start = new DateTime(year, month, 1);
            var end = start.AddMonths(1).AddDays(-1);

            var schedule = await _db.WorkSchedules
                .Where(w => w.EmployeeId == CurrentEmployeeId && w.WorkDate >= start && w.WorkDate <= end)
                .ToListAsync();

            var approvedLeaves = await _db.LeaveRequests
                .Where(l => l.EmployeeId == CurrentEmployeeId
                            && l.Status == "Approved"
                            && l.StartDate <= end && l.EndDate >= start)
                .ToListAsync();

            return Ok(new { schedule, leaves = approvedLeaves });
        }

        public class ScheduleEntryDto
        {
            public int EmployeeId { get; set; }
            public DateTime WorkDate { get; set; }
            public string? ShiftStart { get; set; } // format "HH:mm"
            public string? ShiftEnd { get; set; }
            public string Status { get; set; } = "Working";
        }

        // ---- ADMIN : ajouter/mettre à jour une entrée de planning pour un employé ----
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpsertScheduleEntry([FromBody] ScheduleEntryDto dto)
        {
            var existing = await _db.WorkSchedules.FirstOrDefaultAsync(
                w => w.EmployeeId == dto.EmployeeId && w.WorkDate.Date == dto.WorkDate.Date);

            TimeSpan? shiftStart = string.IsNullOrEmpty(dto.ShiftStart) ? null : TimeSpan.Parse(dto.ShiftStart);
            TimeSpan? shiftEnd = string.IsNullOrEmpty(dto.ShiftEnd) ? null : TimeSpan.Parse(dto.ShiftEnd);

            if (existing != null)
            {
                existing.ShiftStart = shiftStart;
                existing.ShiftEnd = shiftEnd;
                existing.Status = dto.Status;
            }
            else
            {
                _db.WorkSchedules.Add(new WorkSchedule
                {
                    EmployeeId = dto.EmployeeId,
                    WorkDate = dto.WorkDate.Date,
                    ShiftStart = shiftStart,
                    ShiftEnd = shiftEnd,
                    Status = dto.Status
                });
            }

            await _db.SaveChangesAsync();
            return Ok(new { message = "Planning mis à jour." });
        }

        // ---- ADMIN : voir le calendrier de n'importe quel employé ----
        [HttpGet("employee/{employeeId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetEmployeeSchedule(int employeeId, [FromQuery] int year, [FromQuery] int month)
        {
            var start = new DateTime(year, month, 1);
            var end = start.AddMonths(1).AddDays(-1);

            var schedule = await _db.WorkSchedules
                .Where(w => w.EmployeeId == employeeId && w.WorkDate >= start && w.WorkDate <= end)
                .ToListAsync();

            var approvedLeaves = await _db.LeaveRequests
                .Where(l => l.EmployeeId == employeeId
                            && l.Status == "Approved"
                            && l.StartDate <= end && l.EndDate >= start)
                .ToListAsync();

            return Ok(new { schedule, leaves = approvedLeaves });
        }
    }
}
