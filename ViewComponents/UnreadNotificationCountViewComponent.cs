using HelpDeskPro.Data;
using HelpDeskPro.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskPro.ViewComponents;

/// <summary>Componente reutilizável que mostra o contador de notificações não lidas.</summary>
public class UnreadNotificationCountViewComponent : ViewComponent
{
    // A contagem depende da conta ativa e é consultada diretamente na base de dados.
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public UnreadNotificationCountViewComponent(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    /// <summary>Obtém o utilizador atual e devolve a contagem para a vista partilhada.</summary>
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var user = await _userManager.GetUserAsync(HttpContext.User);
        if (user == null) return Content(string.Empty);

        var count = await _context.Notificacoes.CountAsync(notification => notification.UtilizadorId == user.Id && !notification.Lida);
        return View(count);
    }
}