using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HelpDeskPro.Data;
using HelpDeskPro.Models;

namespace HelpDeskPro.Controllers;

/// <summary>Publica, pesquisa e administra artigos de solução técnica.</summary>
[Authorize]
public class KnowledgeBaseController : Controller
{
    // UserManager é usado para identificar o autor de artigos criados ou editados.
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public KnowledgeBaseController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    /// <summary>Lista artigos publicados e permite pesquisa textual e por categoria.</summary>
    public async Task<IActionResult> Index(string pesquisa, string categoria)
    {
        // Técnicos e administradores também veem rascunhos; clientes veem apenas publicados.
        var canManage = User.IsInRole("Administrador") || User.IsInRole("Tecnico");
        var query = _context.KnowledgeBaseArticles.Include(k => k.Autor).Where(k => k.Publicado || canManage).AsQueryable();

        if (!string.IsNullOrEmpty(pesquisa))
        {
            var termo = pesquisa.Trim().ToLower();
            query = query.Where(k => k.Titulo.ToLower().Contains(termo) || k.Conteudo.ToLower().Contains(termo));
        }
        if (!string.IsNullOrEmpty(categoria)) query = query.Where(k => k.Categoria == categoria);

        var articles = await query.OrderByDescending(k => k.Visualizacoes).ThenByDescending(k => k.DataCriacao).ToListAsync();
        ViewData["Pesquisa"] = pesquisa;
        ViewData["Categoria"] = categoria;
        ViewData["Categorias"] = new[] { "Geral", "Hardware", "Software", "Rede", "Email", "Impressora", "Segurança", "Outro" };
        ViewData["CanManage"] = canManage;
        return View(articles);
    }

    /// <summary>Apresenta um artigo e incrementa o contador de visualizações.</summary>
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();
        var article = await _context.KnowledgeBaseArticles.Include(k => k.Autor).FirstOrDefaultAsync(m => m.Id == id);
        if (article == null) return NotFound();
        if (!article.Publicado && !User.IsInRole("Administrador") && !User.IsInRole("Tecnico")) return NotFound();

        article.Visualizacoes++;
        _context.KnowledgeBaseArticles.Update(article);
        await _context.SaveChangesAsync();

        return View(article);
    }

    /// <summary>Abre o formulário de criação para a equipa de suporte.</summary>
    [Authorize(Roles = "Administrador,Tecnico")]
    public IActionResult Create()
    {
        ViewData["Categorias"] = new[] { "Geral", "Hardware", "Software", "Rede", "Email", "Impressora", "Segurança", "Outro" };
        return View();
    }

    /// <summary>Define o utilizador autenticado como autor e grava o artigo validado.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    /// <summary>Carrega um artigo existente para edição.</summary>
    [Authorize(Roles = "Administrador,Tecnico")]
    public async Task<IActionResult> Create([Bind("Id,Titulo,Conteudo,Categoria,AutorId,Publicado,DataCriacao,DataAtualizacao")] KnowledgeBaseArticle article)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user != null)
        {
            article.AutorId = user.Id;
            article.DataCriacao = DateTime.Now;
        }

        if (ModelState.IsValid)
        {
            _context.Add(article);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        ViewData["Categorias"] = new[] { "Geral", "Hardware", "Software", "Rede", "Email", "Impressora", "Segurança", "Outro" };
        return View(article);
    }

    [Authorize(Roles = "Administrador,Tecnico")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var article = await _context.KnowledgeBaseArticles.FindAsync(id);
        if (article == null) return NotFound();
        ViewData["Categorias"] = new[] { "Geral", "Hardware", "Software", "Rede", "Email", "Impressora", "Segurança", "Outro" };
        return View(article);
    }

    /// <summary>Atualiza o artigo e regista a data da alteração.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador,Tecnico")]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Titulo,Conteudo,Categoria,AutorId,Publicado,DataCriacao,DataAtualizacao")] KnowledgeBaseArticle article)
    {
        if (id != article.Id) return NotFound();
        var user = await _userManager.GetUserAsync(User);
        if (user != null) article.DataAtualizacao = DateTime.Now;

        if (ModelState.IsValid)
        {
            try { _context.Update(article); await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { if (!_context.KnowledgeBaseArticles.Any(e => e.Id == id)) return NotFound(); throw; }
            return RedirectToAction(nameof(Index));
        }
        ViewData["Categorias"] = new[] { "Geral", "Hardware", "Software", "Rede", "Email", "Impressora", "Segurança", "Outro" };
        return View(article);
    }

    /// <summary>Mostra confirmação de eliminação, reservada a administradores.</summary>
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var article = await _context.KnowledgeBaseArticles.FirstOrDefaultAsync(m => m.Id == id);
        if (article == null) return NotFound();
        return View(article);
    }

    /// <summary>Elimina o artigo após confirmação por POST.</summary>
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var article = await _context.KnowledgeBaseArticles.FindAsync(id);
        if (article != null)
        {
            _context.KnowledgeBaseArticles.Remove(article);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}