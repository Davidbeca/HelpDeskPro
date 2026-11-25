using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HelpDeskPro.Models;
using HelpDeskPro.Data;

namespace HelpDeskPro.Controllers;

/// <summary>Calcula indicadores agregados para relatórios de suporte.</summary>
[Authorize(Roles = "Administrador,Tecnico")]
public class ReportsController : Controller
{
    // Os relatórios consultam tickets e a identidade para aplicar limites por papel.
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ReportsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    /// <summary>Calcula totais, distribuição e tempos médios para o período escolhido.</summary>
    public async Task<IActionResult> Index(string periodo = "mes")
    {
        var user = await _userManager.GetUserAsync(User);
        var isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Administrador");

        // Converte a opção da interface numa data inicial para limitar todas as consultas.
        DateTime dataInicio = periodo switch
        {
            "semana" => DateTime.Now.AddDays(-7),
            "mes" => DateTime.Now.AddMonths(-1),
            "trimestre" => DateTime.Now.AddMonths(-3),
            "ano" => DateTime.Now.AddYears(-1),
            _ => DateTime.Now.AddMonths(-1)
        };

        // Esta consulta base é reutilizada para manter consistência entre os indicadores.
        var query = _context.Tickets.Where(t => t.DataCriacao >= dataInicio).AsQueryable();

        if (!isAdmin && user != null && user.TipoConta == "Tecnico")
            query = query.Where(t => t.TecnicoId == user.Id);

        var ticketsPorPeriodo = await query
            .GroupBy(t => t.DataCriacao.Date)
            .Select(g => new { Data = g.Key, Count = g.Count() })
            .OrderBy(g => g.Data)
            .ToListAsync();

        var ticketsPorTecnico = await query
            .Where(t => t.TecnicoId != null)
            .Include(t => t.TecnicoResponsavel)
            .GroupBy(t => t.TecnicoResponsavel!.NomeCompleto)
            .Select(g => new { Tecnico = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ToListAsync();

        var ticketsPorPrioridade = await query
            .GroupBy(t => t.Prioridade)
            .Select(g => new { Prioridade = g.Key, Count = g.Count() })
            .ToListAsync();

        var ticketsResolvidos = await query
            .CountAsync(t => t.Estado == "Resolvido" || t.Estado == "Fechado");

        // Carrega datas para calcular horas na aplicação, evitando funções não traduzidas pelo SQLite.
        var duracoesResolucao = await query
            .Where(t => t.Estado == "Resolvido" && t.DataAtualizacao != null)
            .Select(t => new { t.DataCriacao, DataAtualizacao = t.DataAtualizacao!.Value })
            .ToListAsync();
        var tempoMedioResolucao = duracoesResolucao.Count == 0
            ? 0
            : duracoesResolucao.Average(t => (t.DataAtualizacao - t.DataCriacao).TotalHours);

        var totalTickets = await query.CountAsync();
        var ticketsPendentes = await query.CountAsync(t => t.Estado == "AguardarResposta");

        // Entrega cada indicador à vista de relatórios.
        ViewData["Periodo"] = periodo;
        ViewData["TotalTickets"] = totalTickets;
        ViewData["TicketsResolvidos"] = ticketsResolvidos;
        ViewData["TicketsPendentes"] = ticketsPendentes;
        ViewData["TempoMedioResolucao"] = tempoMedioResolucao;
        ViewData["TicketsPorPeriodo"] = ticketsPorPeriodo;
        ViewData["TicketsPorTecnico"] = ticketsPorTecnico;
        ViewData["TicketsPorPrioridade"] = ticketsPorPrioridade;

        return View();
    }
}