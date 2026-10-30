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
    public class HolidayController : ControllerBase
    {
        private readonly AppDbContext _db;
        public HolidayController(AppDbContext db) { _db = db; }

        // ---- Tout employé connecté : liste des jours fériés (pour affichage calendrier) ----
        [HttpGet]
        public async Task<IActionResult> GetHolidays([FromQuery] int? year)
        {
            var query = _db.Holidays.AsQueryable();
            if (year.HasValue)
                query = query.Where(h => h.HolidayDate.Year == year.Value);

            var holidays = await query.OrderBy(h => h.HolidayDate).ToListAsync();
            return Ok(holidays);
        }

        public class HolidayDto
        {
            public DateTime HolidayDate { get; set; }
            public string Name { get; set; } = string.Empty;
        }

        // ---- ADMIN : ajouter un jour férié ----
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddHoliday([FromBody] HolidayDto dto)
        {
            if (await _db.Holidays.AnyAsync(h => h.HolidayDate.Date == dto.HolidayDate.Date))
                return BadRequest(new { message = "Un jour férié existe déjà à cette date." });

            var holiday = new Holiday { HolidayDate = dto.HolidayDate.Date, Name = dto.Name };
            _db.Holidays.Add(holiday);
            await _db.SaveChangesAsync();
            return Ok(holiday);
        }

        // ---- ADMIN : supprimer un jour férié ----
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteHoliday(int id)
        {
            var holiday = await _db.Holidays.FindAsync(id);
            if (holiday == null) return NotFound();
            _db.Holidays.Remove(holiday);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Jour férié supprimé." });
        }
    }
}
