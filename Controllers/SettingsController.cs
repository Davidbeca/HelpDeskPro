using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HelpDeskPro.Controllers;

/// <summary>Serve a página de preferências locais da aplicação.</summary>
[Authorize]
public class SettingsController : Controller
{
    /// <summary>Abre as definições de aparência guardadas no navegador.</summary>
    public IActionResult Index() => View();
}