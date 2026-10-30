using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HRLeaveApp.Data;
using HRLeaveApp.Models;

namespace HRLeaveApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LeaveTypeController : ControllerBase
    {
        private readonly AppDbContext _db;
        public LeaveTypeController(AppDbContext db) { _db = db; }

        // ---- Tout employé connecté : liste des types actifs (pour le formulaire de demande) ----
        [HttpGet]
        public async Task<IActionResult> GetActiveTypes()
        {
            var types = await _db.LeaveTypes
                .Where(t => t.IsActive)
                .Select(t => new { t.LeaveTypeId, t.Code, t.DisplayName })
                .ToListAsync();
            return Ok(types);
        }

        public class LeaveTypeDto
        {
            public string Code { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
        }

        // ---- ADMIN : créer un type de congé ----
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateType([FromBody] LeaveTypeDto dto)
        {
            if (await _db.LeaveTypes.AnyAsync(t => t.Code == dto.Code))
                return BadRequest(new { message = "Ce code de type de congé existe déjà." });

            var type = new LeaveType { Code = dto.Code, DisplayName = dto.DisplayName, IsActive = true };
            _db.LeaveTypes.Add(type);
            await _db.SaveChangesAsync();
            return Ok(type);
        }

        // ---- ADMIN : désactiver un type de congé (garde l'historique) ----
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeactivateType(int id)
        {
            var type = await _db.LeaveTypes.FindAsync(id);
            if (type == null) return NotFound();
            type.IsActive = false;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Type de congé désactivé." });
        }
    }
}
