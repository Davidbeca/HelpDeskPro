using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using HelpDeskPro.Models;

namespace HelpDeskPro.Data;

/// <summary>
/// Contexto EF Core da aplicação. Herda o esquema de Identity e acrescenta
/// as entidades próprias do HelpDesk Pro e as relações entre elas.
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    // Cada DbSet representa uma tabela/coleção consultável pelo EF Core.
    public DbSet<Ticket> Tickets { get; set; }
    public DbSet<TicketComment> TicketComments { get; set; }
    public DbSet<TicketAttachment> TicketAttachments { get; set; }
    public DbSet<TicketHistory> TicketHistories { get; set; }
    public DbSet<Technician> Technicians { get; set; }
    public DbSet<Equipment> Equipments { get; set; }
    public DbSet<KnowledgeBaseArticle> KnowledgeBaseArticles { get; set; }
    public DbSet<Notificacao> Notificacoes { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Número público do ticket tem de ser único. Cliente e técnico não são
        // eliminados em cascata para preservar os registos de suporte associados.
        builder.Entity<Ticket>(entity =>
        {
            entity.HasIndex(e => e.NumeroTicket).IsUnique();
            entity.HasOne(e => e.Cliente)
                .WithMany(u => u.TicketsCriados)
                .HasForeignKey(e => e.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.TecnicoResponsavel)
                .WithMany(u => u.TicketsAtribuidos)
                .HasForeignKey(e => e.TecnicoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Comentários e histórico pertencem ao ticket e são removidos com ele;
        // utilizadores referenciados são protegidos contra eliminação em cascata.
        builder.Entity<TicketComment>(entity =>
        {
            entity.HasOne(e => e.Ticket)
                .WithMany(t => t.Comentarios)
                .HasForeignKey(e => e.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Utilizador)
                .WithMany(u => u.Comentarios)
                .HasForeignKey(e => e.UtilizadorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Metadados do anexo pertencem ao ticket.
        builder.Entity<TicketAttachment>(entity =>
        {
            entity.HasOne(e => e.Ticket)
                .WithMany(t => t.Anexos)
                .HasForeignKey(e => e.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TicketHistory>(entity =>
        {
            entity.HasOne(e => e.Ticket)
                .WithMany(t => t.Historico)
                .HasForeignKey(e => e.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Utilizador)
                .WithMany()
                .HasForeignKey(e => e.UtilizadorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Equipamentos e artigos mantêm os respetivos clientes/autores existentes.
        builder.Entity<Equipment>(entity =>
        {
            entity.HasOne(e => e.Cliente)
                .WithMany()
                .HasForeignKey(e => e.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<KnowledgeBaseArticle>(entity =>
        {
            entity.HasOne(e => e.Autor)
                .WithMany()
                .HasForeignKey(e => e.AutorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Notificacao>(entity =>
        {
            entity.HasOne(e => e.Utilizador)
                .WithMany(u => u.Notificacoes)
                .HasForeignKey(e => e.UtilizadorId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

