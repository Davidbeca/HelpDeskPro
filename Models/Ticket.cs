using System.ComponentModel.DataAnnotations;

namespace HelpDeskPro.Models;

/// <summary>Pedido de suporte técnico, ligado ao cliente, técnico e respetivo histórico.</summary>
public class Ticket
{
    // Identificador interno da base de dados; NumeroTicket é o código legível mostrado aos utilizadores.
    public int Id { get; set; }

    [Required]
    public string NumeroTicket { get; set; } = "";

    [Required]
    [StringLength(200)]
    public string Titulo { get; set; } = "";

    [Required]
    public string Descricao { get; set; } = "";

    // IDs e navegações estabelecem as duas relações independentes com contas Identity.
    public string? ClienteId { get; set; }
    public ApplicationUser? Cliente { get; set; }

    public string? TecnicoId { get; set; }
    public ApplicationUser? TecnicoResponsavel { get; set; }

    [Required]
    [StringLength(50)]
    public string Categoria { get; set; } = "Geral";

    [Required]
    [StringLength(20)]
    public string Prioridade { get; set; } = "Media";

    [Required]
    [StringLength(30)]
    public string Estado { get; set; } = "Novo";

    // Datas de criação/atualização apoiam o histórico, relatórios e prazos de resolução.
    public DateTime DataCriacao { get; set; } = DateTime.Now;

    public DateTime? DataAtualizacao { get; set; }

    public DateTime? PrazoResolucao { get; set; }

    // Coleções de navegação associam comunicação, anexos e auditoria ao ticket.
    public ICollection<TicketComment> Comentarios { get; set; } = new List<TicketComment>();
    public ICollection<TicketAttachment> Anexos { get; set; } = new List<TicketAttachment>();
    public ICollection<TicketHistory> Historico { get; set; } = new List<TicketHistory>();
}

/// <summary>Mensagem de um participante; Interno distingue notas privadas da equipa.</summary>
public class TicketComment
{
    public int Id { get; set; }

    public int TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    public string? UtilizadorId { get; set; }
    public ApplicationUser? Utilizador { get; set; }

    [Required]
    public string Conteudo { get; set; } = "";

    public bool Interno { get; set; } = false;

    public DateTime DataCriacao { get; set; } = DateTime.Now;
}

/// <summary>Metadados de um ficheiro enviado para apoiar a resolução do ticket.</summary>
public class TicketAttachment
{
    public int Id { get; set; }

    public int TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    [Required]
    public string NomeFicheiro { get; set; } = "";

    [Required]
    // CaminhoFicheiro contém o nome interno gerado pelo servidor, não um caminho fornecido pelo browser.
    public string CaminhoFicheiro { get; set; } = "";

    public long Tamanho { get; set; }

    public string? TipoConteudo { get; set; }

    public DateTime DataUpload { get; set; } = DateTime.Now;
}

/// <summary>Evento de auditoria que regista campo alterado, valores e autor da alteração.</summary>
public class TicketHistory
{
    public int Id { get; set; }

    public int TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    public string? UtilizadorId { get; set; }
    public ApplicationUser? Utilizador { get; set; }

    [Required]
    public string CampoAlterado { get; set; } = "";

    public string? ValorAntigo { get; set; }

    public string? ValorNovo { get; set; }

    public DateTime DataAlteracao { get; set; } = DateTime.Now;
}