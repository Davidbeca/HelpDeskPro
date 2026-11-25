using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using HelpDeskPro.Data;
using HelpDeskPro.Models;

// Prepara configuração e registo dos serviços usados pela aplicação ASP.NET Core.
var builder = WebApplication.CreateBuilder(args);

// A base SQLite guarda permanentemente contas, tickets e restantes módulos.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// Identity trata autenticação; os papéis são usados nas políticas [Authorize] dos controladores.
builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddControllersWithViews();

// Constrói a aplicação depois de todos os serviços estarem registados.
var app = builder.Build();

// Em cada arranque, atualiza o esquema através de migrações e garante os papéis/contas iniciais.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var dbContext = services.GetRequiredService<ApplicationDbContext>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    await dbContext.Database.MigrateAsync();
    await SeedData.Initialize(services, userManager, dbContext);
}

// Configura páginas de erro distintas para desenvolvimento e produção.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Ordem do pipeline: redirecionamento, encaminhamento, autenticação e autorização.
app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

// Regista rotas MVC e páginas Razor (incluindo as páginas de autenticação Identity).
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.Run();
