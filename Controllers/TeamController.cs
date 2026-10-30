using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HRLeaveApp.Data;

namespace HRLeaveApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Tout employé connecté peut voir qui est absent, pas seulement l'admin
    public class TeamController : ControllerBase
    {
        private readonly AppDbContext _db;

        public TeamController(AppDbContext db)
        {
            _db = db;
        }

        // ---- Qui est absent aujourd'hui (toute l'entreprise) ----
        [HttpGet("absences-today")]
        public async Task<IActionResult> GetAbsencesToday()
        {
            var today = DateTime.UtcNow.Date;

            var absences = await _db.LeaveRequests
                .Include(l => l.Employee)
                .Where(l => l.Status == "Approved"
                            && l.StartDate <= today && l.EndDate >= today
                            && l.Employee!.IsActive)
                .Select(l => new
                {
                    l.Employee!.EmployeeId,
                    l.Employee.FirstName,
                    l.Employee.LastName,
                    l.Employee.Department,
                    l.LeaveType,
                    l.EndDate
                })
                .ToListAsync();

            return Ok(absences);
        }

        // ---- Toutes les absences approuvées d'un mois donné (vue calendrier d'équipe) ----
        [HttpGet("absences")]
        public async Task<IActionResult> GetAbsencesForMonth([FromQuery] int year, [FromQuery] int month)
        {
            var start = new DateTime(year, month, 1);
            var end = start.AddMonths(1).AddDays(-1);

            var absences = await _db.LeaveRequests
                .Include(l => l.Employee)
                .Where(l => l.Status == "Approved"
                            && l.StartDate <= end && l.EndDate >= start
                            && l.Employee!.IsActive)
                .OrderBy(l => l.StartDate)
                .Select(l => new
                {
                    l.Employee!.EmployeeId,
                    l.Employee.FirstName,
                    l.Employee.LastName,
                    l.Employee.Department,
                    l.LeaveType,
                    l.StartDate,
                    l.EndDate
                })
                .ToListAsync();

            return Ok(absences);
        }
    }
}
