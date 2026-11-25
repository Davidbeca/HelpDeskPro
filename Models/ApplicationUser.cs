using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace HelpDeskPro.Models;

/// <summary>
/// Conta da aplicação baseada no Identity, acrescida dos dados de negócio usados
/// por clientes, técnicos e administradores.
/// </summary>
public class ApplicationUser : IdentityUser
{
    // Os atributos de validação são aplicados também nos formulários que usam este modelo.
    [Required]
    [StringLength(100)]
    public string NomeCompleto { get; set; } = "";

    [StringLength(100)]
    public string? Empresa { get; set; }

    [StringLength(100)]
    public string? Departamento { get; set; }

    [StringLength(20)]
    public string? ContactoTelefonico { get; set; }

    [Required]
    [StringLength(20)]
    public string TipoConta { get; set; } = "Cliente";

    public DateTime DataRegisto { get; set; } = DateTime.Now;

    public bool Ativo { get; set; } = true;

    // Relações de navegação permitem chegar aos pedidos, comentários e notificações da conta.
    public ICollection<Ticket> TicketsCriados { get; set; } = new List<Ticket>();
    public ICollection<Ticket> TicketsAtribuidos { get; set; } = new List<Ticket>();
    public ICollection<TicketComment> Comentarios { get; set; } = new List<TicketComment>();
    public ICollection<Notificacao> Notificacoes { get; set; } = new List<Notificacao>();
}