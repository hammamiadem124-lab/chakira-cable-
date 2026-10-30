using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HRLeaveApp.Data;
using HRLeaveApp.Models;

namespace HRLeaveApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")] // Toutes les routes ici sont réservées aux admins
    public class EmployeeController : ControllerBase
    {
        private readonly AppDbContext _db;

        public EmployeeController(AppDbContext db)
        {
            _db = db;
        }

        public class CreateEmployeeDto
        {
            public string CIN { get; set; } = string.Empty;
            public string FirstName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
            public string? Email { get; set; }
            public string Password { get; set; } = string.Empty;
            public string? Department { get; set; }
            public bool IsAdmin { get; set; } = false;
        }

        // ---- ADMIN : lister tous les employés (avec statut de présence + solde de congés) ----
        [HttpGet]
        public async Task<IActionResult> GetAllEmployees()
        {
            var today = DateTime.UtcNow.Date;

            var employees = await _db.Employees
                .Include(e => e.Role)
                .Where(e => e.IsActive)
                .ToListAsync();

            var employeeIds = employees.Select(e => e.EmployeeId).ToList();

            // Congés approuvés couvrant aujourd'hui, pour déterminer le statut de présence
            var activeLeavesToday = await _db.LeaveRequests
                .Where(l => employeeIds.Contains(l.EmployeeId)
                            && l.Status == "Approved"
                            && l.StartDate <= today && l.EndDate >= today)
                .ToListAsync();

            var result = employees.Select(e =>
            {
                var leaveToday = activeLeavesToday.FirstOrDefault(l => l.EmployeeId == e.EmployeeId);
                string status = "Actif";
                if (leaveToday != null)
                    status = leaveToday.LeaveType == "Sick" ? "En arrêt" : "En congé";

                return new
                {
                    e.EmployeeId,
                    e.CIN,
                    e.FirstName,
                    e.LastName,
                    e.Email,
                    e.Department,
                    e.HireDate,
                    e.AnnualLeaveBalance,
                    Status = status,
                    Role = e.Role!.RoleName
                };
            });

            return Ok(result);
        }

        // ---- ADMIN : statistiques résumées pour les cartes KPI ----
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var today = DateTime.UtcNow.Date;

            var totalEmployees = await _db.Employees.CountAsync(e => e.IsActive);

            var onLeaveToday = await _db.LeaveRequests
                .Where(l => l.Status == "Approved" && l.StartDate <= today && l.EndDate >= today)
                .Select(l => l.EmployeeId)
                .Distinct()
                .CountAsync();

            var pendingRequests = await _db.LeaveRequests.CountAsync(l => l.Status == "Pending");

            return Ok(new
            {
                totalEmployees,
                onLeaveToday,
                pendingRequests
            });
        }

        // ---- ADMIN : exporter la liste des employés en CSV (ouvrable dans Excel) ----
        [HttpGet("export")]
        public async Task<IActionResult> ExportEmployees()
        {
            var employees = await _db.Employees
                .Include(e => e.Role)
                .Where(e => e.IsActive)
                .ToListAsync();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("CIN;Prénom;Nom;Email;Département;Rôle;Solde congés;Date embauche");

            foreach (var e in employees)
            {
                sb.AppendLine($"{e.CIN};{e.FirstName};{e.LastName};{e.Email};{e.Department};{e.Role?.RoleName};{e.AnnualLeaveBalance};{e.HireDate:yyyy-MM-dd}");
            }

            var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv", $"employes_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        // ---- ADMIN : récupérer un employé précis ----
        [HttpGet("{id}")]
        public async Task<IActionResult> GetEmployee(int id)
        {
            var employee = await _db.Employees
                .Include(e => e.Role)
                .Where(e => e.EmployeeId == id)
                .Select(e => new
                {
                    e.EmployeeId,
                    e.CIN,
                    e.FirstName,
                    e.LastName,
                    e.Email,
                    e.Department,
                    e.AnnualLeaveBalance,
                    IsAdmin = e.RoleId == 2
                })
                .FirstOrDefaultAsync();

            if (employee == null) return NotFound();
            return Ok(employee);
        }

        // ---- ADMIN : créer un nouvel employé ----
        [HttpPost]
        public async Task<IActionResult> CreateEmployee([FromBody] CreateEmployeeDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.CIN) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(new { message = "CIN et mot de passe sont obligatoires." });

            var exists = await _db.Employees.AnyAsync(e => e.CIN == dto.CIN);
            if (exists)
                return BadRequest(new { message = "Un employé avec ce CIN existe déjà." });

            var employee = new Employee
            {
                CIN = dto.CIN,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                RoleId = dto.IsAdmin ? 2 : 1,
                Department = dto.Department,
                HireDate = DateTime.UtcNow,
                IsActive = true
            };

            _db.Employees.Add(employee);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                employee.EmployeeId,
                employee.CIN,
                employee.FirstName,
                employee.LastName,
                employee.Department
            });
        }

        public class UpdateEmployeeDto
        {
            public string FirstName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
            public string? Email { get; set; }
            public string? Department { get; set; }
            public bool IsAdmin { get; set; } = false;
            public int? AnnualLeaveBalance { get; set; }
            public string? NewPassword { get; set; } // optionnel : laisser vide pour ne pas changer le mot de passe
        }

        // ---- ADMIN : modifier les coordonnées d'un employé ----
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEmployee(int id, [FromBody] UpdateEmployeeDto dto)
        {
            var employee = await _db.Employees.FindAsync(id);
            if (employee == null) return NotFound();

            employee.FirstName = dto.FirstName;
            employee.LastName = dto.LastName;
            employee.Email = dto.Email;
            employee.Department = dto.Department;
            employee.RoleId = dto.IsAdmin ? 2 : 1;
            if (dto.AnnualLeaveBalance.HasValue)
                employee.AnnualLeaveBalance = dto.AnnualLeaveBalance.Value;

            if (!string.IsNullOrWhiteSpace(dto.NewPassword))
            {
                employee.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            }

            await _db.SaveChangesAsync();
            return Ok(new { message = "Employé mis à jour." });
        }

        // ---- ADMIN : désactiver un employé (au lieu de le supprimer, pour garder l'historique) ----
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeactivateEmployee(int id)
        {
            var employee = await _db.Employees.FindAsync(id);
            if (employee == null) return NotFound();

            employee.IsActive = false;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Employé désactivé." });
        }
    }
}
