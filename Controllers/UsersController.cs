using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HelpDeskPro.Data;
using HelpDeskPro.Models;

namespace HelpDeskPro.Controllers;

/// <summary>Administra contas de clientes, técnicos e administradores usando o Identity.</summary>
[Authorize]
public class UsersController : Controller
{
    // O contexto consulta relações com tickets; UserManager executa operações seguras sobre contas.
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public UsersController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    /// <summary>Lista e filtra utilizadores; a listagem completa é restrita a administradores.</summary>
    public async Task<IActionResult> Index(string pesquisa, string tipo)
    {
        var user = await _userManager.GetUserAsync(User);
        var isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Administrador");
        if (!isAdmin) return Forbid();

        var query = _context.Users.AsQueryable();
        if (!string.IsNullOrEmpty(pesquisa))
        {
            var termo = pesquisa.Trim().ToLower();
            query = query.Where(u => u.NomeCompleto.ToLower().Contains(termo) || (u.Email != null && u.Email.ToLower().Contains(termo)));
        }
        if (!string.IsNullOrEmpty(tipo)) query = query.Where(u => u.TipoConta == tipo);

        var utilizadores = await query.OrderBy(u => u.NomeCompleto).ToListAsync();
        ViewData["Pesquisa"] = pesquisa;
        ViewData["Tipo"] = tipo;
        ViewData["Tipos"] = new[] { "Administrador", "Tecnico", "Cliente" };
        return View(utilizadores);
    }

    /// <summary>Mostra perfil e tickets associados, respeitando o acesso do próprio utilizador.</summary>
    public async Task<IActionResult> Details(string id)
    {
        if (id == null) return NotFound();
        var utilizador = await _context.Users.Include(u => u.TicketsCriados).Include(u => u.TicketsAtribuidos).FirstOrDefaultAsync(m => m.Id == id);
        if (utilizador == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        var isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Administrador");
        if (!isAdmin && user?.Id != id) return Forbid();
        return View(utilizador);
    }

    /// <summary>Abre o formulário de criação de conta para um administrador.</summary>
    [Authorize(Roles = "Administrador")]
    public IActionResult Create()
    {
        ViewData["Tipos"] = new[] { "Administrador", "Tecnico", "Cliente" };
        return View();
    }

    /// <summary>Cria a conta, atribui o papel e cria ficha técnica quando necessário.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Create([Bind("Id,NomeCompleto,Email,Empresa,Departamento,ContactoTelefonico,TipoConta")] ApplicationUser utilizador, string password)
    {
        if (ModelState.IsValid)
        {
            utilizador.UserName = utilizador.Email;
            utilizador.DataRegisto = DateTime.Now;
            utilizador.EmailConfirmed = true;
            // O Identity valida e guarda a palavra-passe como hash; nunca se grava em texto simples.
            var result = await _userManager.CreateAsync(utilizador, password ?? "User123!");
            if (result.Succeeded)
            {
                // A conta só fica operacional depois de receber um papel válido.
                var roleResult = await _userManager.AddToRoleAsync(utilizador, utilizador.TipoConta);
                if (!roleResult.Succeeded)
                {
                    await _userManager.DeleteAsync(utilizador);
                    foreach (var error in roleResult.Errors) ModelState.AddModelError(string.Empty, error.Description);
                    ViewData["Tipos"] = new[] { "Administrador", "Tecnico", "Cliente" };
                    return View(utilizador);
                }
                if (utilizador.TipoConta == "Tecnico")
                {
                    _context.Technicians.Add(new Technician { UtilizadorId = utilizador.Id, Especialidade = "Geral", EstadoDisponibilidade = "Disponível" });
                    await _context.SaveChangesAsync();
                }
                return RedirectToAction(nameof(Index));
            }
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
        }
        ViewData["Tipos"] = new[] { "Administrador", "Tecnico", "Cliente" };
        return View(utilizador);
    }

    /// <summary>Carrega os dados de uma conta para edição.</summary>
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Edit(string id)
    {
        if (id == null) return NotFound();
        var utilizador = await _context.Users.FindAsync(id);
        if (utilizador == null) return NotFound();
        ViewData["Tipos"] = new[] { "Administrador", "Tecnico", "Cliente" };
        return View(utilizador);
    }

    /// <summary>Atualiza campos do perfil preservando os restantes dados internos do Identity.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Edit(string id, [Bind("Id,NomeCompleto,Email,Empresa,Departamento,ContactoTelefonico,TipoConta,Ativo")] ApplicationUser utilizador)
    {
        if (id != utilizador.Id) return NotFound();
        var existingUser = await _userManager.FindByIdAsync(id);
        if (existingUser == null) return NotFound();

        if (ModelState.IsValid)
        {
            existingUser.NomeCompleto = utilizador.NomeCompleto;
            existingUser.Email = utilizador.Email;
            existingUser.UserName = utilizador.Email;
            existingUser.Empresa = utilizador.Empresa;
            existingUser.Departamento = utilizador.Departamento;
            existingUser.ContactoTelefonico = utilizador.ContactoTelefonico;
            existingUser.TipoConta = utilizador.TipoConta;
            existingUser.Ativo = utilizador.Ativo;

            var updateResult = await _userManager.UpdateAsync(existingUser);
            if (updateResult.Succeeded)
            {
                // Mantém apenas um papel de aplicação entre Administrador, Tecnico e Cliente.
                var currentRoles = await _userManager.GetRolesAsync(existingUser);
                var applicationRoles = new[] { "Administrador", "Tecnico", "Cliente" };
                var rolesToRemove = currentRoles.Intersect(applicationRoles).ToArray();
                if (rolesToRemove.Length > 0)
                    await _userManager.RemoveFromRolesAsync(existingUser, rolesToRemove);

                var roleResult = await _userManager.AddToRoleAsync(existingUser, existingUser.TipoConta);
                if (roleResult.Succeeded)
                {
                    var technician = await _context.Technicians.FirstOrDefaultAsync(t => t.UtilizadorId == existingUser.Id);
                    if (existingUser.TipoConta == "Tecnico" && technician == null)
                        _context.Technicians.Add(new Technician { UtilizadorId = existingUser.Id, Especialidade = "Geral", EstadoDisponibilidade = "Disponível" });
                    else if (existingUser.TipoConta != "Tecnico" && technician != null)
                        _context.Technicians.Remove(technician);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                foreach (var error in roleResult.Errors) ModelState.AddModelError(string.Empty, error.Description);
            }
            else
            {
                foreach (var error in updateResult.Errors) ModelState.AddModelError(string.Empty, error.Description);
            }
        }
        ViewData["Tipos"] = new[] { "Administrador", "Tecnico", "Cliente" };
        return View(existingUser);
    }

    /// <summary>Apresenta os dados da conta antes da desativação.</summary>
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Delete(string id)
    {
        if (id == null) return NotFound();
        var utilizador = await _context.Users.FirstOrDefaultAsync(m => m.Id == id);
        if (utilizador == null) return NotFound();
        return View(utilizador);
    }

    /// <summary>Desativa a conta e bloqueia o login sem apagar tickets do histórico.</summary>
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
        var utilizador = await _context.Users.FindAsync(id);
        if (utilizador != null)
        {
            utilizador.Ativo = false;
            await _userManager.UpdateAsync(utilizador);
            await _userManager.SetLockoutEndDateAsync(utilizador, DateTimeOffset.MaxValue);
        }
        return RedirectToAction(nameof(Index));
    }
}