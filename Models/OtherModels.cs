using System.ComponentModel.DataAnnotations;

namespace HelpDeskPro.Models;

/// <summary>Ficha profissional que complementa uma conta do tipo Técnico.</summary>
public class Technician
{
    public int Id { get; set; }

    public string? UtilizadorId { get; set; }
    public ApplicationUser? Utilizador { get; set; }

    [Required]
    [StringLength(100)]
    public string Especialidade { get; set; } = "";

    [Required]
    [StringLength(20)]
    public string EstadoDisponibilidade { get; set; } = "Disponível";

    // Indicadores de produtividade apresentados na área da equipa técnica.
    public int TicketsAtivos { get; set; } = 0;

    public int TicketsResolvidos { get; set; } = 0;

    public double TempoMedioResolucao { get; set; } = 0;
}

/// <summary>Equipamento informático pertencente a um cliente e relacionado com tickets.</summary>
public class Equipment
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Nome { get; set; } = "";

    [Required]
    [StringLength(50)]
    public string Categoria { get; set; } = "";

    [Required]
    [StringLength(50)]
    public string Marca { get; set; } = "";

    [Required]
    [StringLength(50)]
    public string Modelo { get; set; } = "";

    [Required]
    [StringLength(50)]
    public string NumeroSerie { get; set; } = "";

    [Required]
    [StringLength(50)]
    public string SistemaOperativo { get; set; } = "";

    public string? ClienteId { get; set; }
    public ApplicationUser? Cliente { get; set; }

    [Required]
    [StringLength(20)]
    public string Estado { get; set; } = "Ativo";

    public DateTime DataRegisto { get; set; } = DateTime.Now;

    // Relação de navegação usada para mostrar tickets associados à máquina.
    public ICollection<Ticket> TicketsRelacionados { get; set; } = new List<Ticket>();
}

/// <summary>Artigo de suporte com conteúdo, categoria, autor e estado de publicação.</summary>
public class KnowledgeBaseArticle
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Titulo { get; set; } = "";

    [Required]
    public string Conteudo { get; set; } = "";

    [Required]
    [StringLength(50)]
    public string Categoria { get; set; } = "Geral";

    public string? AutorId { get; set; }
    public ApplicationUser? Autor { get; set; }

    public int Visualizacoes { get; set; } = 0;

    public bool Publicado { get; set; } = true;

    public DateTime DataCriacao { get; set; } = DateTime.Now;

    public DateTime? DataAtualizacao { get; set; }
}

/// <summary>Notificação privada destinada a uma conta da aplicação.</summary>
public class Notificacao
{
    public int Id { get; set; }

    public string? UtilizadorId { get; set; }
    public ApplicationUser? Utilizador { get; set; }

    [Required]
    [StringLength(200)]
    public string Titulo { get; set; } = "";

    [Required]
    public string Mensagem { get; set; } = "";

    [StringLength(50)]
    public string Tipo { get; set; } = "Info";

    public bool Lida { get; set; } = false;

    public string? Url { get; set; }

    public DateTime DataCriacao { get; set; } = DateTime.Now;
}