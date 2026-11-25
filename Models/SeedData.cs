using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using HelpDeskPro.Data;
using HelpDeskPro.Models;

namespace HelpDeskPro.Models;

/// <summary>
/// Prepara papéis e contas iniciais de forma repetível: a rotina pode correr
/// em cada arranque sem criar duplicados.
/// </summary>
public static class SeedData
{
    /// <summary>Cria papéis, contas de demonstração e as respetivas fichas técnicas.</summary>
    public static async Task Initialize(IServiceProvider services, UserManager<ApplicationUser> userManager, ApplicationDbContext dbContext)
    {
        // Garante que todos os papéis referenciados por [Authorize(Roles = ...)] existem.
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var roleName in new[] { "Administrador", "Tecnico", "Cliente" })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (!roleResult.Succeeded)
                    throw new InvalidOperationException(string.Join("; ", roleResult.Errors.Select(error => error.Description)));
            }
        }

        // Conta administrativa inicial para instalação/demonstração do sistema.
        var adminEmail = "admin@helpdesk.pro";
        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin == null)
        {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                NomeCompleto = "Administrador Sistema",
                Empresa = "HelpDesk Pro",
                Departamento = "TI",
                ContactoTelefonico = "+351 911 222 333",
                TipoConta = "Administrador",
                DataRegisto = DateTime.Now,
                Ativo = true,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(admin, "Admin123!");
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }
        if (!await userManager.IsInRoleAsync(admin, "Administrador"))
            await userManager.AddToRoleAsync(admin, "Administrador");

        // Se a conta Trancoso já existe, sincroniza o perfil e dá-lhe acesso administrativo;
        // não cria uma palavra-passe nem uma conta nova caso não exista.
        var trancoso = await userManager.FindByEmailAsync("trancoso@gmail.com");
        if (trancoso != null)
        {
            if (string.IsNullOrWhiteSpace(trancoso.NomeCompleto))
                trancoso.NomeCompleto = "Trancoso";
            trancoso.TipoConta = "Administrador";
            trancoso.Ativo = true;
            trancoso.LockoutEnd = null;
            var updateResult = await userManager.UpdateAsync(trancoso);
            if (!updateResult.Succeeded)
                throw new InvalidOperationException(string.Join("; ", updateResult.Errors.Select(error => error.Description)));

            var currentRoles = await userManager.GetRolesAsync(trancoso);
            var rolesToRemove = currentRoles.Where(role => role != "Administrador").ToArray();
            if (rolesToRemove.Length > 0)
            {
                var removeResult = await userManager.RemoveFromRolesAsync(trancoso, rolesToRemove);
                if (!removeResult.Succeeded)
                    throw new InvalidOperationException(string.Join("; ", removeResult.Errors.Select(error => error.Description)));
            }

            if (!await userManager.IsInRoleAsync(trancoso, "Administrador"))
            {
                var roleResult = await userManager.AddToRoleAsync(trancoso, "Administrador");
                if (!roleResult.Succeeded)
                    throw new InvalidOperationException(string.Join("; ", roleResult.Errors.Select(error => error.Description)));
            }

            var technician = await dbContext.Technicians.FirstOrDefaultAsync(t => t.UtilizadorId == trancoso.Id);
            if (technician != null)
            {
                dbContext.Technicians.Remove(technician);
                await dbContext.SaveChangesAsync();
            }
        }

        // Utilizadores técnicos de demonstração e fichas profissionais associadas.
        var techs = new[]
        {
            new { Email = "silva@helpdesk.pro", Nome = "João Silva", Especialidade = "Redes", Tel = "+351 912 333 444" },
            new { Email = "santos@helpdesk.pro", Nome = "Maria Santos", Especialidade = "Hardware", Tel = "+351 913 444 555" },
            new { Email = "costa@helpdesk.pro", Nome = "Pedro Costa", Especialidade = "Software", Tel = "+351 914 555 666" }
        };

        foreach (var tech in techs)
        {
            var user = await userManager.FindByEmailAsync(tech.Email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = tech.Email,
                    Email = tech.Email,
                    NomeCompleto = tech.Nome,
                    Empresa = "HelpDesk Pro",
                    Departamento = "Suporte Técnico",
                    ContactoTelefonico = tech.Tel,
                    TipoConta = "Tecnico",
                    DataRegisto = DateTime.Now,
                    Ativo = true,
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(user, "Tech123!");
                if (!result.Succeeded)
                    throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
            }
            if (!await userManager.IsInRoleAsync(user, "Tecnico"))
                await userManager.AddToRoleAsync(user, "Tecnico");

            if (!await dbContext.Technicians.AnyAsync(t => t.UtilizadorId == user.Id))
            {
                dbContext.Technicians.Add(new Technician
                {
                    UtilizadorId = user.Id,
                    Especialidade = tech.Especialidade,
                    EstadoDisponibilidade = "Disponível",
                    TicketsAtivos = 0,
                    TicketsResolvidos = 0,
                    TempoMedioResolucao = 0
                });
            }
        }

        // Clientes de demonstração para testar a criação e associação de tickets/equipamentos.
        var clients = new[]
        {
            new { Email = "ana@empresa.pt", Nome = "Ana Rodrigues", Empresa = "TechSolutions Lda", Departamento = "Administração", Tel = "+351 921 111 222" },
            new { Email = "carlos@empresa.pt", Nome = "Carlos Pereira", Empresa = "Indústria Norte SA", Departamento = "Produção", Tel = "+351 922 222 333" },
            new { Email = "beatriz@empresa.pt", Nome = "Beatriz Lima", Empresa = "Comércio Sul", Departamento = "Vendas", Tel = "+351 923 333 444" }
        };

        foreach (var client in clients)
        {
            var user = await userManager.FindByEmailAsync(client.Email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = client.Email,
                    Email = client.Email,
                    NomeCompleto = client.Nome,
                    Empresa = client.Empresa,
                    Departamento = client.Departamento,
                    ContactoTelefonico = client.Tel,
                    TipoConta = "Cliente",
                    DataRegisto = DateTime.Now,
                    Ativo = true,
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(user, "Client123!");
                if (!result.Succeeded)
                    throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
            }
            if (!await userManager.IsInRoleAsync(user, "Cliente"))
                await userManager.AddToRoleAsync(user, "Cliente");
        }

        // Persiste as fichas técnicas que foram adicionadas durante a inicialização.
        await dbContext.SaveChangesAsync();
    }
}