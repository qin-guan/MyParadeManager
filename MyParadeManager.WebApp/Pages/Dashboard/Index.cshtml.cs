using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MyParadeManager.WebApp.Pages.Dashboard;

[Authorize]
public class IndexModel : PageModel
{
    public string Email { get; set; }

    public void OnGet()
    {
    }
}