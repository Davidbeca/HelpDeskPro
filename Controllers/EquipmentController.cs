using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HelpDeskPro.Models;
using HelpDeskPro.Data;

namespace HelpDeskPro.Controllers;

/// <summary>
/// Gere o inventário de equipamentos: listagem, consulta, criação, edição e remoção.
/// Os equipamentos pertencem a clientes e podem estar relacionados com tickets.
/// </summary>
[Authorize]
public class EquipmentController : Controller
{
    // O contexto dá acesso às tabelas da aplicação; o UserManager identifica
    // a conta autenticada e permite consultar o respetivo papel.
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    /// <summary>
    /// Recebe do sistema de injeção de dependências os serviços necessários
    /// para aceder à base de dados e às contas do Identity.
    /// </summary>
    public EquipmentController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    /// <summary>
    /// Apresenta o inventário com pesquisa e filtros opcionais.
    /// Clientes só veem os equipamentos associados à sua própria conta.
    /// </summary>
    public async Task<IActionResult> Index(string pesquisa, string estado, string categoria)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        // A autorização geral exige início de sessão; aqui distinguimos os
        // papéis de aplicação para decidir que registos cada pessoa pode ver.
        var isAdmin = user != null && await _userManager.IsInRoleAsync(user, "Administrador");
        var isTecnico = isAdmin || (user != null && user.TipoConta == "Tecnico");
        var isCliente = !isAdmin && user != null && user.TipoConta == "Cliente";
        if (!isAdmin && !isTecnico && !isCliente) return Forbid();

        // Include carrega também os dados do cliente para a tabela. A consulta
        // continua a ser construída na base de dados até ToListAsync.
        var query = _context.Equipments.Include(e => e.Cliente).AsQueryable();

        if (isCliente && user != null)
            query = query.Where(e => e.ClienteId == user.Id);

        // Cada filtro é aplicado apenas quando recebeu um valor, permitindo
        // combinar pesquisa, estado e categoria na mesma consulta.
        if (!string.IsNullOrEmpty(pesquisa))
        {
            var termo = pesquisa.Trim().ToLower();
            query = query.Where(e => e.Nome.ToLower().Contains(termo) || e.NumeroSerie.ToLower().Contains(termo) || e.Marca.ToLower().Contains(termo));
        }
        if (!string.IsNullOrEmpty(estado)) query = query.Where(e => e.Estado == estado);
        if (!string.IsNullOrEmpty(categoria)) query = query.Where(e => e.Categoria == categoria);

        // Ordena por nome e executa a consulta de forma assíncrona.
        var equipamentos = await query.OrderBy(e => e.Nome).ToListAsync();

        // Os valores enviados em ViewData alimentam as opções dos filtros na vista.
        ViewData["Pesquisa"] = pesquisa;
        ViewData["Estado"] = estado;
        ViewData["Categoria"] = categoria;
        ViewData["Estados"] = new[] { "Ativo", "Inativo", "EmReparacao", "Descontinuado" };
        ViewData["Categorias"] = new[] { "Portátil", "Desktop", "Servidor", "Impressora", "Rede", "Telemóvel", "Tablet", "Outro" };
        ViewData["IsAdmin"] = isAdmin;
        ViewData["IsTecnico"] = isTecnico;
        ViewData["IsCliente"] = isCliente;
        return View(equipamentos);
    }

    /// <summary>
    /// Mostra um equipamento, o cliente associado e os tickets relacionados.
    /// </summary>
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        // Carrega as relações necessárias para apresentar a ficha completa
        // sem consultas adicionais na vista.
        var equipamento = await _context.Equipments.Include(e => e.Cliente).Include(e => e.TicketsRelacionados).FirstOrDefaultAsync(m => m.Id == id);
        if (equipamento == null) return NotFound();
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();
        var isAdmin = await _userManager.IsInRoleAsync(user, "Administrador");
        // Um cliente só pode consultar os seus próprios equipamentos. Técnicos
        // podem consultar o inventário para prestar suporte; administradores têm acesso total.
        if (!isAdmin && user.TipoConta == "Cliente" && equipamento.ClienteId != user.Id) return Forbid();
        if (!isAdmin && user.TipoConta != "Cliente" && user.TipoConta != "Tecnico") return Forbid();
        return View(equipamento);
    }

    /// <summary>
    /// Abre o formulário de criação e prepara as listas de clientes, estados e categorias.
    /// </summary>
    [Authorize(Roles = "Administrador,Tecnico")]
    public IActionResult Create()
    {
        // A vista utiliza estas listas para gerar os menus de seleção.
        ViewData["Clientes"] = _context.Users.Where(u => u.TipoConta == "Cliente").ToList();
        ViewData["Estados"] = new[] { "Ativo", "Inativo", "EmReparacao", "Descontinuado" };
        ViewData["Categorias"] = new[] { "Portátil", "Desktop", "Servidor", "Impressora", "Rede", "Telemóvel", "Tablet", "Outro" };
        return View();
    }

    /// <summary>
    /// Recebe e valida o formulário de criação enviado por POST.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador,Tecnico")]
    public async Task<IActionResult> Create([Bind("Id,Nome,Categoria,Marca,Modelo,NumeroSerie,SistemaOperativo,ClienteId,Estado,DataRegisto")] Equipment equipamento)
    {
        if (ModelState.IsValid)
        {
            // A data é definida no servidor para não depender de um valor enviado pelo browser.
            equipamento.DataRegisto = DateTime.Now;
            // Adiciona o novo registo e grava as alterações permanentemente no SQLite.
            _context.Add(equipamento);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        // Se a validação falhar, volta a preencher as listas da vista para que
        // o utilizador possa corrigir os campos sem perder o formulário.
        ViewData["Clientes"] = _context.Users.Where(u => u.TipoConta == "Cliente").ToList();
        ViewData["Estados"] = new[] { "Ativo", "Inativo", "EmReparacao", "Descontinuado" };
        ViewData["Categorias"] = new[] { "Portátil", "Desktop", "Servidor", "Impressora", "Rede", "Telemóvel", "Tablet", "Outro" };
        return View(equipamento);
    }

    /// <summary>
    /// Abre o formulário de edição do equipamento indicado.
    /// </summary>
    [Authorize(Roles = "Administrador,Tecnico")]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();
        // FindAsync procura pela chave primária; se não existir, responde 404.
        var equipamento = await _context.Equipments.FindAsync(id);
        if (equipamento == null) return NotFound();
        ViewData["Clientes"] = _context.Users.Where(u => u.TipoConta == "Cliente").ToList();
        ViewData["Estados"] = new[] { "Ativo", "Inativo", "EmReparacao", "Descontinuado" };
        ViewData["Categorias"] = new[] { "Portátil", "Desktop", "Servidor", "Impressora", "Rede", "Telemóvel", "Tablet", "Outro" };
        return View(equipamento);
    }

    /// <summary>
    /// Atualiza os campos editáveis do equipamento e trata conflitos concorrentes.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador,Tecnico")]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Nome,Categoria,Marca,Modelo,NumeroSerie,SistemaOperativo,ClienteId,Estado,DataRegisto")] Equipment equipamento)
    {
        // Confirma que o ID do URL coincide com o ID enviado pelo formulário.
        if (id != equipamento.Id) return NotFound();
        // Carrega a entidade existente e altera apenas os campos permitidos.
        // Assim DataRegisto e outras propriedades não são substituídas pelo cliente.
        var existingEquipment = await _context.Equipments.FindAsync(id);
        if (existingEquipment == null) return NotFound();
        if (ModelState.IsValid)
        {
            existingEquipment.Nome = equipamento.Nome;
            existingEquipment.Categoria = equipamento.Categoria;
            existingEquipment.Marca = equipamento.Marca;
            existingEquipment.Modelo = equipamento.Modelo;
            existingEquipment.NumeroSerie = equipamento.NumeroSerie;
            existingEquipment.SistemaOperativo = equipamento.SistemaOperativo;
            existingEquipment.ClienteId = equipamento.ClienteId;
            existingEquipment.Estado = equipamento.Estado;
            try { await _context.SaveChangesAsync(); }
            // O registo pode ter sido alterado ou removido por outra operação
            // desde que o formulário foi aberto; nesse caso devolvemos 404 se já não existir.
            catch (DbUpdateConcurrencyException) { if (!_context.Equipments.Any(e => e.Id == id)) return NotFound(); throw; }
            return RedirectToAction(nameof(Index));
        }
        // Em caso de validação falhada, repõe os menus de seleção antes de voltar à vista.
        ViewData["Clientes"] = _context.Users.Where(u => u.TipoConta == "Cliente").ToList();
        ViewData["Estados"] = new[] { "Ativo", "Inativo", "EmReparacao", "Descontinuado" };
        ViewData["Categorias"] = new[] { "Portátil", "Desktop", "Servidor", "Impressora", "Rede", "Telemóvel", "Tablet", "Outro" };
        return View(equipamento);
    }

    /// <summary>
    /// Apresenta a confirmação de remoção do equipamento.
    /// </summary>
    [Authorize(Roles = "Administrador,Tecnico")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var equipamento = await _context.Equipments.Include(e => e.Cliente).FirstOrDefaultAsync(m => m.Id == id);
        if (equipamento == null) return NotFound();
        return View(equipamento);
    }

    /// <summary>
    /// Confirma a remoção. Equipamentos com tickets associados são preservados
    /// para não quebrar o histórico de suporte.
    /// </summary>
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrador,Tecnico")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var equipamento = await _context.Equipments.FindAsync(id);
        if (equipamento != null)
        {
            // EquipmentId é uma chave de relação gerida pelo EF Core; consulta-se
            // diretamente para impedir a eliminação quando existem tickets ligados.
            var hasTickets = await _context.Tickets.AnyAsync(ticket => EF.Property<int?>(ticket, "EquipmentId") == id);
            if (hasTickets)
            {
                // Mantém o equipamento e orienta a equipa a marcá-lo como inativo.
                TempData["Error"] = "Não é possível eliminar este equipamento porque tem tickets associados. Altere o estado para Inativo.";
                return RedirectToAction(nameof(Delete), new { id });
            }
            // Sem tickets dependentes, remove o equipamento e persiste a alteração.
            _context.Equipments.Remove(equipamento);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}