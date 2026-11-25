using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HelpDeskPro.Data;
using HelpDeskPro.Models;

namespace HelpDeskPro.Controllers;

/// <summary>Apresenta e atualiza as notificações privadas da conta autenticada.</summary>
[Authorize]
public class NotificationsController : Controller
{
    // As consultas são sempre limitadas ao ID do utilizador atual para manter notificações privadas.
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    /// <summary>Lista notificações recentes e conta quantas ainda não foram lidas.</summary>
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var notificacoes = await _context.Notificacoes
            .Where(n => n.UtilizadorId == user.Id)
            .OrderByDescending(n => n.DataCriacao)
            .ToListAsync();

        ViewData["NaoLidas"] = notificacoes.Count(n => !n.Lida);
        return View(notificacoes);
    }

    /// <summary>Marca como lida uma notificação que pertence ao utilizador atual.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var notificacao = await _context.Notificacoes.FirstOrDefaultAsync(n => n.Id == id && n.UtilizadorId == user.Id);
        if (notificacao == null) return NotFound();

        notificacao.Lida = true;
        _context.Notificacoes.Update(notificacao);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    /// <summary>Marca todas as notificações não lidas do utilizador como lidas.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var notificacoes = await _context.Notificacoes.Where(n => n.UtilizadorId == user.Id && !n.Lida).ToListAsync();
        foreach (var n in notificacoes) n.Lida = true;
        _context.Notificacoes.UpdateRange(notificacoes);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }
}