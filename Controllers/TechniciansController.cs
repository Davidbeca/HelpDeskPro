using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HelpDeskPro.Data;
using HelpDeskPro.Models;

namespace HelpDeskPro.Controllers;

/// <summary>Gere fichas técnicas, disponibilidade e tickets atribuídos à equipa de suporte.</summary>
[Authorize]
public class TechniciansController : Controller
{
    // As fichas técnicas ligam contas Identity aos dados específicos da equipa de suporte.
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public TechniciansController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    /// <summary>Lista técnicos para administradores ou apenas a ficha do próprio técnico.</summary>
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        var isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Administrador");
        var isTecnico = user != null && user.TipoConta == "Tecnico";

        if (!isAdmin && !isTecnico) return Forbid();

        // Inclui a conta associada para mostrar nome e email na lista.
        var query = _context.Technicians.Include(t => t.Utilizador).AsQueryable();

        if (isTecnico && user != null)
        {
            var tecnico = await _context.Technicians.FirstOrDefaultAsync(t => t.UtilizadorId == user.Id);
            if (tecnico != null)
                query = query.Where(t => t.Id == tecnico.Id);
        }

        var tecnicos = await query.ToListAsync();
        ViewData["IsAdmin"] = isAdmin;
        ViewData["IsTecnico"] = isTecnico;
        return View(tecnicos);
    }

    /// <summary>Mostra métricas e tickets atribuídos ao técnico selecionado.</summary>
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Unauthorized();
        var isAdmin = await _userManager.IsInRoleAsync(currentUser, "Administrador");
        var tecnico = await _context.Technicians.Include(t => t.Utilizador)
            .Include(t => t.Utilizador!.TicketsAtribuidos)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (tecnico == null) return NotFound();
        if (!isAdmin && tecnico.UtilizadorId != currentUser.Id) return Forbid();
        return View(tecnico);
    }

    /// <summary>Abre formulário com contas do tipo Técnico ainda sem ficha técnica.</summary>
    [Authorize(Roles = "Administrador")]
    public IActionResult Create()
    {
        var utilizadoresDisponiveis = _context.Users
            .Where(u => u.Ativo && !_context.Technicians.Any(t => t.UtilizadorId == u.Id))
            .ToList();
        ViewData["Utilizadores"] = utilizadoresDisponiveis;
        return View();
    }

    /// <summary>Cria a ficha técnica associada a uma conta existente.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    /// <summary>Carrega a ficha técnica e prepara as contas disponíveis para edição.</summary>
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Create([Bind("Id,UtilizadorId,Especialidade,EstadoDisponibilidade,TicketsAtivos,TicketsResolvidos,TempoMedioResolucao")] Technician tecnico)
    {
        if (ModelState.IsValid)
        {
            var utilizador = await _context.Users.FindAsync(tecnico.UtilizadorId);
            if (utilizador == null || !utilizador.Ativo)
            {
                ModelState.AddModelError(nameof(tecnico.UtilizadorId), "Selecione um utilizador ativo.");
            }
            else if (await _context.Technicians.AnyAsync(t => t.UtilizadorId == utilizador.Id))
            {
                ModelState.AddModelError(nameof(tecnico.UtilizadorId), "Este utilizador já está associado à equipa técnica.");
            }
            else
            {
                utilizador.TipoConta = "Tecnico";
                var updateResult = await _userManager.UpdateAsync(utilizador);
                if (!updateResult.Succeeded)
                {
                    foreach (var error in updateResult.Errors)
                        ModelState.AddModelError(string.Empty, error.Description);
                }
                else
                {
                    var papeisAtuais = await _userManager.GetRolesAsync(utilizador);
                    if (papeisAtuais.Contains("Cliente") || papeisAtuais.Contains("Administrador"))
                    {
                        var papeisRemover = papeisAtuais.Where(p => p == "Cliente" || p == "Administrador").ToArray();
                        await _userManager.RemoveFromRolesAsync(utilizador, papeisRemover);
                    }
                    if (!await _userManager.IsInRoleAsync(utilizador, "Tecnico"))
                    {
                        var roleResult = await _userManager.AddToRoleAsync(utilizador, "Tecnico");
                        if (!roleResult.Succeeded)
                        {
                            foreach (var error in roleResult.Errors)
                                ModelState.AddModelError(string.Empty, error.Description);
                        }
                    }

                    if (ModelState.IsValid)
                    {
                        _context.Add(tecnico);
                        await _context.SaveChangesAsync();
                        return RedirectToAction(nameof(Index));
                    }
                }
            }
        }
        var utilizadoresDisponiveis = _context.Users
            .Where(u => u.Ativo && !_context.Technicians.Any(t => t.UtilizadorId == u.Id))
            .ToList();
        ViewData["Utilizadores"] = utilizadoresDisponiveis;
        return View(tecnico);
    }

    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var tecnico = await _context.Technicians.FindAsync(id);
        if (tecnico == null) return NotFound();
        ViewData["Utilizadores"] = _context.Users.Where(u => u.TipoConta == "Tecnico" && !_context.Technicians.Any(t => t.UtilizadorId == u.Id)).ToList();
        return View(tecnico);
    }

    /// <summary>Grava alterações na ficha e trata conflitos concorrentes.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    /// <summary>Apresenta a confirmação de remoção da ficha técnica.</summary>
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Edit(int id, [Bind("Id,UtilizadorId,Especialidade,EstadoDisponibilidade,TicketsAtivos,TicketsResolvidos,TempoMedioResolucao")] Technician tecnico)
    {
        if (id != tecnico.Id) return NotFound();
        if (ModelState.IsValid)
        {
            try { _context.Update(tecnico); await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { if (!_context.Technicians.Any(e => e.Id == id)) return NotFound(); throw; }
            return RedirectToAction(nameof(Index));
        }
        ViewData["Utilizadores"] = _context.Users.Where(u => u.TipoConta == "Tecnico" && !_context.Technicians.Any(t => t.UtilizadorId == u.Id)).ToList();
        return View(tecnico);
    }

    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var tecnico = await _context.Technicians.Include(t => t.Utilizador).FirstOrDefaultAsync(m => m.Id == id);
        if (tecnico == null) return NotFound();
        return View(tecnico);
    }

    /// <summary>Remove a ficha da equipa sem apagar a conta de utilizador.</summary>
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var tecnico = await _context.Technicians.FindAsync(id);
        if (tecnico != null)
        {
            _context.Technicians.Remove(tecnico);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}