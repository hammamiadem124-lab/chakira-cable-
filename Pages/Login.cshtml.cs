using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HRLeaveApp.Pages
{
    public class LoginModel : PageModel
    {
        public void OnGet()
        {
            // Rien à faire ici : toute la logique de connexion se fait côté client en JS,
            // qui appelle l'API /api/Auth/login.
        }
    }
}
