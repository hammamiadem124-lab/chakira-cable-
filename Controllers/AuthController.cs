using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HRLeaveApp.Data;
using HRLeaveApp.Models;

namespace HRLeaveApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;

        public AuthController(AppDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        public class LoginRequest
        {
            public string CIN { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            const int maxAttempts = 5;
            const int lockoutMinutes = 15;

            var employee = await _db.Employees
                .Include(e => e.Role)
                .FirstOrDefaultAsync(e => e.CIN == request.CIN && e.IsActive);

            // Toujours renvoyer un message générique pour ne pas révéler
            // si c'est le CIN ou le mot de passe qui est incorrect (sécurité)
            if (employee == null)
            {
                return Unauthorized(new { message = "CIN ou mot de passe incorrect." });
            }

            // Vérifie si le compte est actuellement verrouillé
            if (employee.LockoutUntil.HasValue && employee.LockoutUntil.Value > DateTime.UtcNow)
            {
                var minutesLeft = Math.Ceiling((employee.LockoutUntil.Value - DateTime.UtcNow).TotalMinutes);
                return StatusCode(423, new { message = $"Compte temporairement verrouillé suite à plusieurs échecs. Réessaie dans {minutesLeft} minute(s)." });
            }

            if (!BCrypt.Net.BCrypt.Verify(request.Password, employee.PasswordHash))
            {
                employee.FailedLoginAttempts++;
                if (employee.FailedLoginAttempts >= maxAttempts)
                {
                    employee.LockoutUntil = DateTime.UtcNow.AddMinutes(lockoutMinutes);
                    employee.FailedLoginAttempts = 0;
                    _db.AuditLogs.Add(new AuditLog { EmployeeId = employee.EmployeeId, Action = "AccountLocked", Details = $"Verrouillé après {maxAttempts} échecs de connexion." });
                }
                await _db.SaveChangesAsync();
                return Unauthorized(new { message = "CIN ou mot de passe incorrect." });
            }

            // Connexion réussie : réinitialise le compteur d'échecs
            employee.FailedLoginAttempts = 0;
            employee.LockoutUntil = null;
            _db.AuditLogs.Add(new AuditLog { EmployeeId = employee.EmployeeId, Action = "Login", Details = "Connexion réussie." });
            await _db.SaveChangesAsync();

            var token = GenerateJwtToken(employee.EmployeeId, employee.CIN, employee.Role?.RoleName ?? "Employee");

            return Ok(new
            {
                token,
                employee = new
                {
                    employee.EmployeeId,
                    employee.FirstName,
                    employee.LastName,
                    Role = employee.Role?.RoleName
                }
            });
        }

        public class ChangePasswordRequest
        {
            public string CurrentPassword { get; set; } = string.Empty;
            public string NewPassword { get; set; } = string.Empty;
        }

        // ---- Tout employé connecté : changer son propre mot de passe ----
        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
                return BadRequest(new { message = "Le nouveau mot de passe doit contenir au moins 6 caractères." });

            var employeeId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var employee = await _db.Employees.FindAsync(employeeId);
            if (employee == null) return NotFound();

            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, employee.PasswordHash))
                return BadRequest(new { message = "Mot de passe actuel incorrect." });

            employee.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            _db.AuditLogs.Add(new AuditLog { EmployeeId = employee.EmployeeId, Action = "PasswordChanged" });
            await _db.SaveChangesAsync();

            return Ok(new { message = "Mot de passe mis à jour avec succès." });
        }

        // ---- Tout employé connecté : récupérer son propre profil complet ----
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetMe()
        {
            var employeeId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var employee = await _db.Employees
                .Include(e => e.Role)
                .Where(e => e.EmployeeId == employeeId)
                .Select(e => new
                {
                    e.EmployeeId,
                    e.CIN,
                    e.FirstName,
                    e.LastName,
                    e.Email,
                    e.Department,
                    e.AnnualLeaveBalance,
                    Role = e.Role!.RoleName
                })
                .FirstOrDefaultAsync();

            if (employee == null) return NotFound();
            return Ok(employee);
        }

        private string GenerateJwtToken(int employeeId, string cin, string role)
        {
            var jwtKey = _config["Jwt:Key"]!;
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, employeeId.ToString()),
                new Claim("CIN", cin),
                new Claim(ClaimTypes.Role, role)
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(double.Parse(_config["Jwt:ExpireMinutes"]!)),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
