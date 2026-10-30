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
    public class AnnouncementController : ControllerBase
    {
        private readonly AppDbContext _db;

        public AnnouncementController(AppDbContext db)
        {
            _db = db;
        }

        // ---- Tout employé : voir les annonces actives (non expirées) ----
        [HttpGet]
        public async Task<IActionResult> GetActive()
        {
            var now = DateTime.UtcNow;
            var announcements = await _db.Announcements
                .Where(a => a.ExpiresAt == null || a.ExpiresAt > now)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new { a.AnnouncementId, a.Title, a.Message, a.CreatedAt, a.ExpiresAt })
                .ToListAsync();

            return Ok(announcements);
        }

        public class CreateAnnouncementDto
        {
            public string Title { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public DateTime? ExpiresAt { get; set; }
        }

        // ---- ADMIN : publier une annonce ----
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateAnnouncementDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Message))
                return BadRequest(new { message = "Titre et message sont obligatoires." });

            var employeeId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var announcement = new Announcement
            {
                Title = dto.Title,
                Message = dto.Message,
                CreatedBy = employeeId,
                ExpiresAt = dto.ExpiresAt
            };

            _db.Announcements.Add(announcement);
            await _db.SaveChangesAsync();

            return Ok(announcement);
        }

        // ---- ADMIN : supprimer une annonce ----
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var announcement = await _db.Announcements.FindAsync(id);
            if (announcement == null) return NotFound();

            _db.Announcements.Remove(announcement);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Annonce supprimée." });
        }
    }
}
