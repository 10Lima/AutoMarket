using AutoMarket.Data;
using AutoMarket.Models;          // (para ErrorViewModel)
using AutoMarket.Models.Market;   // (para Informacoes_Market)
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using AutoMarket.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace AutoMarket.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly MarketplaceDbContext _marketDb;
        private readonly CurrentUserService _currentUser;

        public HomeController(ILogger<HomeController> logger, MarketplaceDbContext marketDb, CurrentUserService currentUser)
        {
            _logger = logger;
            _marketDb = marketDb;
            _currentUser = currentUser;
        }

        [AllowAnonymous]
        public IActionResult Index()
        {
            return View(); // SEM redirect
        }

        [AllowAnonymous]
        public async Task<IActionResult> Landing()
        {
            // Admin: abre Home/Index
            if (User?.Identity?.IsAuthenticated == true && await _currentUser.IsAdminAsync(User))
                return RedirectToAction(nameof(Index));

            // Restantes: abre Anuncios/Carros
            return RedirectToAction("Carros", "Anuncios");
        }


        // NOVO: Sobre Nós (substitui o conteúdo do antigo Privacy)
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> SobreNos()
        {
            // 1 registo "principal" (o teu seed criou 1)
            var info = await _marketDb.Informacoes_Market
                .AsNoTracking()
                .OrderBy(i => i.id_market)
                .FirstOrDefaultAsync();

            if (info == null)
            {
                // opcional: cria Views/Home/SobreNos_Empty.cshtml
                return View("SobreNos_Empty");
            }

            var contactos = await _marketDb.Market_Contacto
                .AsNoTracking()
                .Where(c => c.id_market == info.id_market)
                .OrderBy(c => c.tipo).ThenBy(c => c.valor)
                .ToListAsync();

            var horarios = await _marketDb.Market_Horario_Suporte
                .AsNoTracking()
                .Where(h => h.id_market == info.id_market)
                .OrderBy(h => h.weekday)
                .ToListAsync();

            var excecoes = await _marketDb.Market_Horario_Suporte_Excecao
                .AsNoTracking()
                .Where(e => e.id_market == info.id_market)
                .OrderBy(e => e.data)
                .ToListAsync();

            ViewBag.Contactos = contactos;
            ViewBag.Horarios = horarios;
            ViewBag.Excecoes = excecoes;

            // Model = Informacoes_Market
            return View(info);
        }

        // Mantém Privacy mas redireciona para não partir links antigos
        [AllowAnonymous]
        public IActionResult Privacy()
        {
            return RedirectToAction(nameof(SobreNos));
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}
