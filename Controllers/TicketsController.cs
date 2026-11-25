using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HelpDeskPro.Data;
using HelpDeskPro.Models;

namespace HelpDeskPro.Controllers;

/// <summary>Coordena a criação, consulta, atualização e comunicação associada aos tickets.</summary>
[Authorize]
public class TicketsController : Controller
{
    // O ambiente da aplicação indica onde guardar anexos; os restantes serviços
    // tratam da base de dados e da identidade/permissões do utilizador.
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _environment;

    public TicketsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment environment)
    {
        _context = context;
        _userManager = userManager;
        _environment = environment;
    }

    /// <summary>Lista tickets aplicando o âmbito de acesso e os filtros recebidos da página.</summary>
    public async Task<IActionResult> Index(string pesquisa, string estado, string prioridade, string categoria, string tecnico)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();
        var isAdmin = await _userManager.IsInRoleAsync(user, "Administrador");
        var isTecnico = user.TipoConta == "Tecnico";
        // Carrega cliente e técnico para apresentar nomes em vez de apenas IDs.
        var query = _context.Tickets
            .Include(t => t.Cliente)
            .Include(t => t.TecnicoResponsavel)
            .AsQueryable();

        // Administradores veem todos; técnicos veem os seus e os novos;
        // clientes ficam limitados aos tickets que criaram.
        if (!isAdmin && !isTecnico && user != null)
            query = query.Where(t => t.ClienteId == user.Id);
        else if (isTecnico && user != null)
            query = query.Where(t => t.TecnicoId == user.Id || t.Estado == "Novo");

        if (!string.IsNullOrEmpty(pesquisa))
        {
            var termo = pesquisa.Trim().ToLower();
            query = query.Where(t => t.NumeroTicket.ToLower().Contains(termo) ||
                                   t.Titulo.ToLower().Contains(termo) ||
                                   t.Descricao.ToLower().Contains(termo));
        }

        if (!string.IsNullOrEmpty(estado)) query = query.Where(t => t.Estado == estado);
        if (!string.IsNullOrEmpty(prioridade)) query = query.Where(t => t.Prioridade == prioridade);
        if (!string.IsNullOrEmpty(categoria)) query = query.Where(t => t.Categoria == categoria);
        if (!string.IsNullOrEmpty(tecnico)) query = query.Where(t => t.TecnicoId == tecnico);

        // Guarda filtros e opções para que a vista possa reconstruir os controlos.
        ViewData["Pesquisa"] = pesquisa;
        ViewData["Estado"] = estado;
        ViewData["Prioridade"] = prioridade;
        ViewData["Categoria"] = categoria;
        ViewData["Tecnico"] = tecnico;
        ViewData["Tecnicos"] = await _context.Users.Where(u => u.TipoConta == "Tecnico").ToListAsync();
        ViewData["Estados"] = new[] { "Novo", "Aberto", "EmAnalise", "EmResolucao", "AguardarResposta", "Resolvido", "Fechado" };
        ViewData["Prioridades"] = new[] { "Baixa", "Media", "Alta", "Critica" };
        ViewData["Categorias"] = new[] { "Geral", "Hardware", "Software", "Rede", "Email", "Impressora", "Segurança", "Outro" };
        ViewData["IsAdmin"] = isAdmin;
        ViewData["IsTecnico"] = isTecnico;

        return View(await query.OrderByDescending(t => t.DataCriacao).ToListAsync());
    }

    /// <summary>Carrega a ficha completa do ticket e confirma que o utilizador pode consultá-la.</summary>
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        // Inclui comentários, anexos e histórico para renderizar o detalhe numa única passagem.
        var ticket = await _context.Tickets
            .Include(t => t.Cliente)
            .Include(t => t.TecnicoResponsavel)
            .Include(t => t.Comentarios).ThenInclude(c => c.Utilizador)
            .Include(t => t.Anexos)
            .Include(t => t.Historico).ThenInclude(h => h.Utilizador)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (ticket == null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();
        var isAdmin = await _userManager.IsInRoleAsync(user, "Administrador");
        var isTecnico = user.TipoConta == "Tecnico";
        if (!isAdmin && !isTecnico && ticket.ClienteId != user.Id) return Forbid();
        if (isTecnico && ticket.TecnicoId != user.Id && ticket.Estado != "Novo") return Forbid();

        ViewData["IsAdmin"] = isAdmin;
        ViewData["IsTecnico"] = isTecnico;
        ViewData["UtilizadorId"] = user.Id;

        return View(ticket);
    }

    /// <summary>Abre o formulário e prepara as opções permitidas para a conta atual.</summary>
    public async Task<IActionResult> Create()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();
        await PopulateTicketOptions(user);
        ViewData["CanAssign"] = user.TipoConta is "Administrador" or "Tecnico";
        return View();
    }

    /// <summary>Valida e grava um ticket, atribui-lhe número e regista notificações/histórico.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,NumeroTicket,Titulo,Descricao,ClienteId,TecnicoId,Categoria,Prioridade,Estado,DataCriacao,DataAtualizacao,PrazoResolucao")] Ticket ticket)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        // O servidor volta a impor as regras do papel; valores enviados pelo browser
        // não podem permitir a um cliente alterar dono, estado ou atribuição.
        var isAdmin = await _userManager.IsInRoleAsync(user, "Administrador");
        var canAssign = isAdmin || user.TipoConta == "Tecnico";
        if (!canAssign)
        {
            ticket.ClienteId = user.Id;
            ticket.TecnicoId = null;
            ticket.Estado = "Novo";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(ticket.ClienteId))
            {
                if (isAdmin)
                {
                    var clientePadrao = await _context.Users
                        .Where(u => u.TipoConta == "Cliente" && u.Ativo)
                        .OrderBy(u => u.NomeCompleto)
                        .FirstOrDefaultAsync();
                    ticket.ClienteId = clientePadrao?.Id ?? user.Id;
                }
                else
                {
                    ticket.ClienteId = user.Id;
                }
            }
            if (isAdmin && string.IsNullOrWhiteSpace(ticket.Estado))
                ticket.Estado = "Novo";
        }
        if (!new[] { "Baixa", "Media", "Alta", "Critica" }.Contains(ticket.Prioridade))
            ticket.Prioridade = "Media";
        if (!new[] { "Geral", "Hardware", "Software", "Rede", "Email", "Impressora", "Segurança", "Outro" }.Contains(ticket.Categoria))
            ticket.Categoria = "Geral";
        if (canAssign)
        {
            if (!new[] { "Novo", "Aberto", "EmAnalise", "EmResolucao", "AguardarResposta", "Resolvido", "Fechado" }.Contains(ticket.Estado))
                ticket.Estado = "Novo";
        }

        // Campos gerados pelo servidor não vêm dos controis HTML e não devem
        // continuar marcados como inválidos pelo model binding.
        ModelState.Remove(nameof(Ticket.NumeroTicket));
        ModelState.Remove(nameof(Ticket.DataCriacao));
        ModelState.Remove(nameof(Ticket.DataAtualizacao));

        // O número público e as datas são gerados pelo servidor, nunca pelo formulário.
        var ultimoId = await _context.Tickets.Select(t => (int?)t.Id).MaxAsync() ?? 0;
        ticket.NumeroTicket = $"HD-{DateTime.Now:yyyy}-{ultimoId + 1:D6}";
        ticket.DataCriacao = DateTime.Now;
        ticket.DataAtualizacao = DateTime.Now;

        if (ModelState.IsValid)
        {
            _context.Tickets.Add(ticket);
            await _context.SaveChangesAsync();

            // Toda a criação começa com um evento de auditoria e uma notificação ao cliente.
            var historico = new TicketHistory
            {
                TicketId = ticket.Id,
                UtilizadorId = user.Id,
                CampoAlterado = "Criação",
                ValorNovo = $"Ticket criado com título: {ticket.Titulo}",
                DataAlteracao = DateTime.Now
            };
            _context.TicketHistories.Add(historico);

            var notificacao = new Notificacao
            {
                UtilizadorId = ticket.ClienteId,
                Titulo = $"Novo ticket {ticket.NumeroTicket}",
                Mensagem = $"O seu ticket '{ticket.Titulo}' foi criado com sucesso.",
                Tipo = "Info",
                Lida = false,
                Url = $"/Tickets/Details/{ticket.Id}",
                DataCriacao = DateTime.Now
            };
            _context.Notificacoes.Add(notificacao);
            if (!string.IsNullOrEmpty(ticket.TecnicoId) && ticket.TecnicoId != user.Id)
            {
                _context.Notificacoes.Add(new Notificacao
                {
                    UtilizadorId = ticket.TecnicoId,
                    Titulo = $"Ticket atribuído: {ticket.NumeroTicket}",
                    Mensagem = $"Foi-lhe atribuído o ticket '{ticket.Titulo}'.",
                    Tipo = "Info",
                    Url = $"/Tickets/Details/{ticket.Id}"
                });
            }
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        await PopulateTicketOptions(user);
        return View(ticket);
    }

    /// <summary>Carrega os dados atuais para edição; apenas equipa autorizada pode alterar tickets.</summary>
    [Authorize(Roles = "Administrador,Tecnico")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        var ticket = await _context.Tickets.FindAsync(id);
        if (ticket == null) return NotFound();

        await PopulateTicketOptions(await _userManager.GetUserAsync(User));

        return View(ticket);
    }

    /// <summary>Atualiza campos do ticket e regista diferenças para auditoria.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador,Tecnico")]
    public async Task<IActionResult> Edit(int id, [Bind("Id,NumeroTicket,Titulo,Descricao,ClienteId,TecnicoId,Categoria,Prioridade,Estado,DataCriacao,DataAtualizacao,PrazoResolucao")] Ticket ticket)
    {
        if (id != ticket.Id) return NotFound();
        var user = await _userManager.GetUserAsync(User);
        var ticketExistente = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticketExistente == null) return NotFound();
        if (user == null) return Unauthorized();
        if (ModelState.IsValid)
        {
            // Regista apenas os campos cujo valor mudou, preservando o valor anterior e o novo.
            var changes = new List<TicketHistory>();
            void Record(string field, string? oldValue, string? newValue)
            {
                if (oldValue == newValue) return;
                changes.Add(new TicketHistory { TicketId = id, UtilizadorId = user.Id, CampoAlterado = field, ValorAntigo = oldValue, ValorNovo = newValue, DataAlteracao = DateTime.Now });
            }
            Record("Título", ticketExistente.Titulo, ticket.Titulo);
            Record("Descrição", ticketExistente.Descricao, ticket.Descricao);
            Record("Categoria", ticketExistente.Categoria, ticket.Categoria);
            Record("Prioridade", ticketExistente.Prioridade, ticket.Prioridade);
            Record("Estado", ticketExistente.Estado, ticket.Estado);
            Record("Técnico", ticketExistente.TecnicoId, ticket.TecnicoId);
            Record("Prazo", ticketExistente.PrazoResolucao?.ToString("O"), ticket.PrazoResolucao?.ToString("O"));
            var previousState = ticketExistente.Estado;
            var previousTechnician = ticketExistente.TecnicoId;
            ticketExistente.Titulo = ticket.Titulo;
            ticketExistente.Descricao = ticket.Descricao;
            ticketExistente.Categoria = ticket.Categoria;
            ticketExistente.Prioridade = ticket.Prioridade;
            ticketExistente.Estado = ticket.Estado;
            ticketExistente.TecnicoId = ticket.TecnicoId;
            ticketExistente.PrazoResolucao = ticket.PrazoResolucao;
            if (!new[] { "Baixa", "Media", "Alta", "Critica" }.Contains(ticket.Prioridade) ||
                !new[] { "Geral", "Hardware", "Software", "Rede", "Email", "Impressora", "Segurança", "Outro" }.Contains(ticket.Categoria) ||
                !new[] { "Novo", "Aberto", "EmAnalise", "EmResolucao", "AguardarResposta", "Resolvido", "Fechado" }.Contains(ticket.Estado))
                ModelState.AddModelError(string.Empty, "Categoria, prioridade ou estado inválido.");
            if (!ModelState.IsValid)
            {
                await PopulateTicketOptions(user);
                return View(ticket);
            }
            ticketExistente.DataAtualizacao = DateTime.Now;
            _context.TicketHistories.AddRange(changes);
            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { if (!TicketExists(ticket.Id)) return NotFound(); throw; }
            // Mudanças de estado ou atribuição notificam cliente e técnicos envolvidos.
            if (previousState != ticketExistente.Estado || previousTechnician != ticketExistente.TecnicoId)
            {
                var recipients = new[] { ticketExistente.ClienteId, previousTechnician, ticketExistente.TecnicoId }.Where(id => !string.IsNullOrEmpty(id) && id != user.Id).Distinct();
                foreach (var recipient in recipients)
                {
                    _context.Notificacoes.Add(new Notificacao { UtilizadorId = recipient, Titulo = $"Ticket atualizado: {ticketExistente.NumeroTicket}", Mensagem = $"Estado: {ticketExistente.Estado}. Técnico: {ticketExistente.TecnicoId ?? "não atribuído"}.", Tipo = "Info", Url = $"/Tickets/Details/{id}" });
                }
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
        await PopulateTicketOptions(user);
        return View(ticket);
    }

    /// <summary>Mostra confirmação de eliminação; a operação efetiva só ocorre por POST.</summary>
    [Authorize(Roles = "Administrador")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var ticket = await _context.Tickets.Include(t => t.Cliente).FirstOrDefaultAsync(m => m.Id == id);
        if (ticket == null) return NotFound();
        return View(ticket);
    }

    /// <summary>Remove um ticket e as relações que o modelo configura em cascata.</summary>
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var ticket = await _context.Tickets.FindAsync(id);
        if (ticket != null)
        {
            _context.Tickets.Remove(ticket);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Adiciona uma atualização pública ou uma nota interna ao ticket.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int ticketId, string conteudo, bool interno = false)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();
        var ticket = await _context.Tickets.FindAsync(ticketId);
        if (ticket == null) return NotFound();
        // Notas internas só podem ser escritas pela equipa; os restantes participantes
        // ficam limitados ao ticket a que têm acesso.
        var isAdmin = await _userManager.IsInRoleAsync(user, "Administrador");
        var isTecnico = user.TipoConta == "Tecnico";
        if (!isAdmin && !isTecnico && ticket.ClienteId != user.Id) return Forbid();
        if (isTecnico && ticket.TecnicoId != user.Id && ticket.Estado != "Novo") return Forbid();
        if (interno && !isAdmin && !isTecnico) return Forbid();
        if (string.IsNullOrWhiteSpace(conteudo))
        {
            TempData["Error"] = "O comentário não pode estar vazio.";
            return RedirectToAction(nameof(Details), new { id = ticketId });
        }

        var comentario = new TicketComment
        {
            TicketId = ticketId,
            UtilizadorId = user.Id,
            Conteudo = conteudo,
            Interno = interno,
            DataCriacao = DateTime.Now
        };
        _context.TicketComments.Add(comentario);
        ticket.DataAtualizacao = DateTime.Now;
        _context.Tickets.Update(ticket);

        // O comentário é também registado no histórico para manter uma linha temporal única.
        var historico = new TicketHistory
        {
            TicketId = ticketId,
            UtilizadorId = user.Id,
            CampoAlterado = "Comentário",
            ValorNovo = $"Adicionado comentário: {conteudo.Substring(0, Math.Min(50, conteudo.Length))}...",
            DataAlteracao = DateTime.Now
        };
        _context.TicketHistories.Add(historico);
        await _context.SaveChangesAsync();

        var destinatarios = new List<string>();
        if (ticket.ClienteId != null && ticket.ClienteId != user.Id) destinatarios.Add(ticket.ClienteId);
        if (ticket.TecnicoId != null && ticket.TecnicoId != user.Id) destinatarios.Add(ticket.TecnicoId);

        if (!interno) foreach (var destId in destinatarios)
        {
            _context.Notificacoes.Add(new Notificacao
            {
                UtilizadorId = destId,
                Titulo = $"Novo comentário no ticket {ticket.NumeroTicket}",
                Mensagem = $"{user.NomeCompleto} adicionou um comentário ao ticket '{ticket.Titulo}'.",
                Tipo = "Info",
                Lida = false,
                Url = $"/Tickets/Details/{ticketId}",
                DataCriacao = DateTime.Now
            });
        }
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = ticketId });
    }

    /// <summary>Guarda um anexo no disco e os seus metadados na base de dados.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadAttachment(int ticketId, IFormFile ficheiro)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();
        var ticket = await _context.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId);
        if (ticket == null) return NotFound();
        var isAdmin = await _userManager.IsInRoleAsync(user, "Administrador");
        var isTechnician = user.TipoConta == "Tecnico";
        if (!isAdmin && !isTechnician && ticket.ClienteId != user.Id) return Forbid();
        if (isTechnician && ticket.TecnicoId != user.Id && ticket.Estado != "Novo") return Forbid();
        if (ficheiro == null || ficheiro.Length == 0 || ficheiro.Length > 10 * 1024 * 1024)
        {
            TempData["Error"] = "Selecione um ficheiro até 10 MB.";
            return RedirectToAction(nameof(Details), new { id = ticketId });
        }

        // Valida extensão e tamanho, e guarda com nome aleatório para evitar confiar
        // no nome original como caminho no servidor.
        var extension = Path.GetExtension(ficheiro.FileName).ToLowerInvariant();
        var allowedExtensions = new[] { ".png", ".jpg", ".jpeg", ".gif", ".pdf", ".txt", ".log", ".csv", ".docx", ".xlsx", ".zip" };
        if (!allowedExtensions.Contains(extension))
        {
            TempData["Error"] = "Este tipo de ficheiro não é permitido.";
            return RedirectToAction(nameof(Details), new { id = ticketId });
        }

        var storageFolder = Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), "uploads", "tickets", ticketId.ToString());
        Directory.CreateDirectory(storageFolder);
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(storageFolder, storedName);
        await using (var stream = System.IO.File.Create(filePath))
            await ficheiro.CopyToAsync(stream);

        // A base de dados guarda o nome original para apresentação e o nome gerado para armazenamento.
        _context.TicketAttachments.Add(new TicketAttachment
        {
            TicketId = ticketId,
            NomeFicheiro = Path.GetFileName(ficheiro.FileName),
            CaminhoFicheiro = storedName,
            Tamanho = ficheiro.Length,
            TipoConteudo = ficheiro.ContentType,
            DataUpload = DateTime.Now
        });
        ticket.DataAtualizacao = DateTime.Now;
        _context.TicketHistories.Add(new TicketHistory { TicketId = ticketId, UtilizadorId = user.Id, CampoAlterado = "Anexo", ValorNovo = Path.GetFileName(ficheiro.FileName), DataAlteracao = DateTime.Now });
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = ticketId });
    }

    /// <summary>Entrega um anexo apenas a quem pode consultar o ticket correspondente.</summary>
    public async Task<IActionResult> DownloadAttachment(int id)
    {
        var attachment = await _context.TicketAttachments.Include(a => a.Ticket).FirstOrDefaultAsync(a => a.Id == id);
        var user = await _userManager.GetUserAsync(User);
        if (attachment?.Ticket == null || user == null) return NotFound();
        var isAdmin = await _userManager.IsInRoleAsync(user, "Administrador");
        var isTechnician = user.TipoConta == "Tecnico";
        if (!isAdmin && !isTechnician && attachment.Ticket.ClienteId != user.Id) return Forbid();
        if (isTechnician && attachment.Ticket.TecnicoId != user.Id && attachment.Ticket.Estado != "Novo") return Forbid();
        var storageFolder = Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), "uploads", "tickets", attachment.TicketId.ToString());
        // Normaliza e valida o caminho para que o nome guardado não permita sair da pasta de anexos.
        var filePath = Path.GetFullPath(Path.Combine(storageFolder, attachment.CaminhoFicheiro));
        if (!filePath.StartsWith(Path.GetFullPath(storageFolder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(filePath)) return NotFound();
        return PhysicalFile(filePath, attachment.TipoConteudo ?? "application/octet-stream", attachment.NomeFicheiro);
    }

    /// <summary>Centraliza listas e indicadores de papel usados pelos formulários de ticket.</summary>
    private async Task PopulateTicketOptions(ApplicationUser? user)
    {
        ViewData["Clientes"] = await _context.Users.Where(u => u.TipoConta == "Cliente").OrderBy(u => u.NomeCompleto).ToListAsync();
        ViewData["Tecnicos"] = await _context.Users.Where(u => u.TipoConta == "Tecnico" && u.Ativo).OrderBy(u => u.NomeCompleto).ToListAsync();
        ViewData["Estados"] = new[] { "Novo", "Aberto", "EmAnalise", "EmResolucao", "AguardarResposta", "Resolvido", "Fechado" };
        ViewData["Prioridades"] = new[] { "Baixa", "Media", "Alta", "Critica" };
        ViewData["Categorias"] = new[] { "Geral", "Hardware", "Software", "Rede", "Email", "Impressora", "Segurança", "Outro" };
        ViewData["IsAdmin"] = user != null && await _userManager.IsInRoleAsync(user, "Administrador");
        ViewData["CanAssign"] = user != null && (user.TipoConta == "Tecnico" || await _userManager.IsInRoleAsync(user, "Administrador"));
    }

    /// <summary>Confirma a existência do ticket ao tratar conflitos de concorrência.</summary>
    private bool TicketExists(int id) => _context.Tickets.Any(e => e.Id == id);
}