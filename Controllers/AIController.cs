using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using HRLeaveApp.Data;

namespace HRLeaveApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Tout employé connecté peut poser des questions sur SES propres données
    public class AIController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;

        public AIController(AppDbContext db, IConfiguration config, IHttpClientFactory httpClientFactory)
        {
            _db = db;
            _config = config;
            _httpClientFactory = httpClientFactory;
        }

        private int CurrentEmployeeId =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public class AskRequest
        {
            public string Question { get; set; } = string.Empty;
        }

        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] AskRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Question))
                return BadRequest(new { message = "La question ne peut pas être vide." });

            var apiKey = _config["Gemini:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey) || apiKey.StartsWith("REMPLACER"))
                return StatusCode(500, new { message = "La clé API Gemini n'est pas configurée côté serveur (appsettings.json)." });

            // ---- 1. Rassembler UNIQUEMENT les données de l'employé connecté ----
            var employee = await _db.Employees
                .Include(e => e.Role)
                .FirstOrDefaultAsync(e => e.EmployeeId == CurrentEmployeeId);

            if (employee == null) return NotFound();

            var myLeaveRequests = await _db.LeaveRequests
                .Where(l => l.EmployeeId == CurrentEmployeeId)
                .OrderByDescending(l => l.RequestedAt)
                .Take(10)
                .Select(l => new
                {
                    l.StartDate,
                    l.EndDate,
                    l.LeaveType,
                    l.Status,
                    l.Reason,
                    l.AdminComment
                })
                .ToListAsync();

            var today = DateTime.UtcNow.Date;
            var teamAbsencesToday = await _db.LeaveRequests
                .Include(l => l.Employee)
                .Where(l => l.Status == "Approved"
                            && l.StartDate <= today && l.EndDate >= today
                            && l.Employee!.Department == employee.Department
                            && l.EmployeeId != CurrentEmployeeId)
                .Select(l => new { l.Employee!.FirstName, l.Employee.LastName })
                .ToListAsync();

            // ---- 2. Construire le contexte texte à donner à l'IA ----
            var contextBuilder = new StringBuilder();
            contextBuilder.AppendLine($"Employé connecté : {employee.FirstName} {employee.LastName}");
            contextBuilder.AppendLine($"Département : {employee.Department ?? "non renseigné"}");
            contextBuilder.AppendLine($"Solde de congés restant : {employee.AnnualLeaveBalance} jours");
            contextBuilder.AppendLine($"Date d'aujourd'hui : {today:yyyy-MM-dd}");
            contextBuilder.AppendLine();
            contextBuilder.AppendLine("Historique des demandes de congé de cet employé (les 10 dernières) :");
            if (myLeaveRequests.Count == 0)
            {
                contextBuilder.AppendLine("- Aucune demande de congé enregistrée.");
            }
            else
            {
                foreach (var l in myLeaveRequests)
                {
                    contextBuilder.AppendLine(
                        $"- Du {l.StartDate:yyyy-MM-dd} au {l.EndDate:yyyy-MM-dd}, type: {l.LeaveType}, statut: {l.Status}, motif: {l.Reason ?? "non précisé"}" +
                        (string.IsNullOrEmpty(l.AdminComment) ? "" : $", commentaire admin: {l.AdminComment}"));
                }
            }
            contextBuilder.AppendLine();
            contextBuilder.AppendLine($"Collègues du même département absents aujourd'hui : {(teamAbsencesToday.Count == 0 ? "aucun" : string.Join(", ", teamAbsencesToday.Select(t => $"{t.FirstName} {t.LastName}")))}");

            var systemPrompt =
                "Tu es l'assistant RH interne de l'entreprise. Tu réponds UNIQUEMENT à partir des informations fournies ci-dessous sur cet employé précis. " +
                "Ne donne jamais d'information sur d'autres employés que celles explicitement listées. " +
                "Si l'information demandée n'est pas dans les données fournies, dis-le clairement au lieu d'inventer une réponse. " +
                "Réponds toujours en français, de façon concise et professionnelle (quelques phrases maximum).\n\n" +
                "Données disponibles :\n" + contextBuilder.ToString();

            // ---- 3. Appeler l'API Google Gemini ----
            var httpClient = _httpClientFactory.CreateClient();
            var model = _config["Gemini:Model"] ?? "gemini-2.5-flash";
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

            var payload = new
            {
                system_instruction = new
                {
                    parts = new[] { new { text = systemPrompt } }
                },
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = request.Question } }
                    }
                }
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            httpRequest.Headers.Add("x-goog-api-key", apiKey);

            try
            {
                var response = await httpClient.SendAsync(httpRequest);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode(502, new { message = "Erreur de l'API Gemini.", details = responseBody });
                }

                using var doc = JsonDocument.Parse(responseBody);
                var answer = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return Ok(new { answer });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Impossible de contacter le service IA.", details = ex.Message });
            }
        }
    }
}
