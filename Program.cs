using AutoMarket.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AutoMarket.Services.Email;

var builder = WebApplication.CreateBuilder(args);

builder.Environment.WebRootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");

// Connection string (única)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// 1) Identity DbContext (dbo.*)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// 2) Marketplace DbContext (market.*) — scaffold
builder.Services.AddDbContext<MarketplaceDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

//Para verificar se as reservas expiraram
builder.Services.AddScoped<AutoMarket.Services.ExpireReservasFilter>();

// Identity
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();

// Serviço para saber o id do current user
builder.Services.AddScoped<AutoMarket.Services.CurrentUserService>();

// ✅ Configuração do Gmail SMTP
builder.Services.Configure<GmailSmtpOptions>(
    builder.Configuration.GetSection("GmailSmtp")
);

// ✅ Serviço de envio de email
builder.Services.AddSingleton<IEmailSender, GmailEmailSender>();

builder.Services.AddHostedService<NotificacoesEmailDispatcherHostedService>();


var app = builder.Build();

//// ✅ SEED DO CATÁLOGO (Marcas/Modelos/Sub_Modelos)
//using (var scope = app.Services.CreateScope())
//{
//    var db = scope.ServiceProvider.GetRequiredService<MarketplaceDbContext>();
//    var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

//    await CatalogoSeed.SeedAsync(db, env);
//}

// Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseMiddleware<AutoMarket.Middleware.BlockedUserMiddleware>();
app.UseAuthorization();

app.MapRazorPages();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
