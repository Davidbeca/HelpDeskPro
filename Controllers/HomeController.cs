using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HelpDeskPro.Data;
using HelpDeskPro.Models;

namespace HelpDeskPro.Controllers;

/// <summary>Prepara o dashboard e as páginas gerais apresentadas ao utilizador autenticado.</summary>
public class HomeController : Controller
{
    // O contexto fornece os dados dos tickets; o UserManager identifica o utilizador e o seu papel.
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public HomeController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    /// <summary>Calcula indicadores, gráficos e atividade recente para o painel principal.</summary>
    [Authorize]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        var isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Administrador");
        var isTecnico = user != null && user.TipoConta == "Tecnico";
        // A mesma consulta base define o âmbito de dados permitido para os indicadores seguintes.
        var ticketsQuery = _context.Tickets.AsQueryable();
        if (!isAdmin && user != null)
            ticketsQuery = isTecnico
                ? ticketsQuery.Where(t => t.TecnicoId == user.Id || t.Estado == "Novo")
                : ticketsQuery.Where(t => t.ClienteId == user.Id);

        var totalTickets = await ticketsQuery.CountAsync();
        var ticketsNovos = await ticketsQuery.CountAsync(t => t.Estado == "Novo");
        var ticketsAbertos = await ticketsQuery.CountAsync(t => t.Estado == "Aberto");
        var ticketsEmAndamento = await ticketsQuery.CountAsync(t => t.Estado == "EmAnalise" || t.Estado == "EmResolucao");
        var ticketsPendentes = await ticketsQuery.CountAsync(t => t.Estado == "AguardarResposta");
        var ticketsResolvidos = await ticketsQuery.CountAsync(t => t.Estado == "Resolvido");
        var ticketsFechados = await ticketsQuery.CountAsync(t => t.Estado == "Fechado");
        var tecnicosDisponiveis = await _context.Technicians.CountAsync(t => t.EstadoDisponibilidade == "Disponível");
        // SQLite não traduz DateTime.Subtract/TotalHours para SQL: carrega apenas as datas
        // necessárias e calcula a média em memória depois da consulta.
        var duracoesResolucao = await ticketsQuery
            .Where(t => (t.Estado == "Resolvido" || t.Estado == "Fechado") && t.DataAtualizacao.HasValue)
            .Select(t => new { t.DataCriacao, DataAtualizacao = t.DataAtualizacao!.Value })
            .ToListAsync();
        var tempoMedioResolucao = duracoesResolucao.Count == 0
            ? 0
            : duracoesResolucao.Average(t => (t.DataAtualizacao - t.DataCriacao).TotalHours);

        // Cria uma série de seis meses completos, agrupando por ano e mês para não misturar anos.
        var inicioPeriodo = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-5);
        var ticketsPorMesDb = await ticketsQuery.Where(t => t.DataCriacao >= inicioPeriodo)
            .GroupBy(t => new { t.DataCriacao.Year, t.DataCriacao.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync();
        var ticketsPorMes = Enumerable.Range(0, 6).Select(offset =>
        {
            var month = inicioPeriodo.AddMonths(offset);
            return new { Mes = month.ToString("MMM yyyy"), Count = ticketsPorMesDb.FirstOrDefault(x => x.Year == month.Year && x.Month == month.Month)?.Count ?? 0 };
        }).ToList();

        var ticketsPorPrioridade = await ticketsQuery
            .GroupBy(t => t.Prioridade)
            .Select(g => new { Prioridade = g.Key, Count = g.Count() })
            .ToListAsync();

        var ultimosTickets = await ticketsQuery
            .Include(t => t.Cliente)
            .Include(t => t.TecnicoResponsavel)
            .OrderByDescending(t => t.DataCriacao)
            .Take(5)
            .ToListAsync();
        var ticketIds = ticketsQuery.Select(t => t.Id);
        // A atividade também respeita o âmbito dos tickets visíveis ao utilizador.
        var atividadeRecente = await _context.TicketHistories
            .Include(h => h.Ticket)
            .Include(h => h.Utilizador)
            .Where(h => ticketIds.Contains(h.TicketId))
            .OrderByDescending(h => h.DataAlteracao)
            .Take(8)
            .ToListAsync();

        // ViewData transporta os resultados para a vista Home/Index.cshtml.
        ViewData["TotalTickets"] = totalTickets;
        ViewData["TicketsNovos"] = ticketsNovos;
        ViewData["TicketsAbertos"] = ticketsAbertos;
        ViewData["TicketsEmAndamento"] = ticketsEmAndamento;
        ViewData["TicketsPendentes"] = ticketsPendentes;
        ViewData["TicketsResolvidos"] = ticketsResolvidos;
        ViewData["TicketsFechados"] = ticketsFechados;
        ViewData["TecnicosDisponiveis"] = tecnicosDisponiveis;
        ViewData["TempoMedioResolucao"] = tempoMedioResolucao;
        ViewData["TicketsPorMes"] = ticketsPorMes;
        ViewData["TicketsPorPrioridade"] = ticketsPorPrioridade;
        ViewData["UltimosTickets"] = ultimosTickets;
        ViewData["AtividadeRecente"] = atividadeRecente;
        ViewData["IsAdmin"] = isAdmin;
        ViewData["IsTecnico"] = isTecnico;
        ViewData["Utilizador"] = user;

        return View();
    }

    /// <summary>Apresenta a página de privacidade da aplicação.</summary>
    [Authorize]
    public IActionResult Privacy()
    {
        return View();
    }

    /// <summary>Apresenta a página de erro sem permitir que a resposta seja guardada em cache.</summary>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View();
    }
}
