using AutoMarket.Data;
using AutoMarket.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Controllers;

[Authorize]
public class AcessoController : Controller
{
    private readonly MarketplaceDbContext _marketDb;
    private readonly CurrentUserService _currentUser;

    public AcessoController(MarketplaceDbContext marketDb, CurrentUserService currentUser)
    {
        _marketDb = marketDb;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Bloqueado()
    {
        var idUtilizador = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtilizador is null)
            return Forbid();

        var estado = await _marketDb.Utilizador.AsNoTracking()
            .Where(u => u.id_utilizador == idUtilizador.Value)
            .Select(u => u.estado)
            .FirstOrDefaultAsync();

        // Se não está bloqueado, redireciona
        if (!string.Equals(estado, "bloqueado", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction("Index", "Home");

        // Buscar motivo da última ação de bloqueio (bloquear OU remover_admin)
        var motivo = await _marketDb.Ace_Admin_Acao.AsNoTracking()
            .Where(a => a.id_utilizador == idUtilizador.Value && 
                        (a.tipo_acao == "bloquear" || a.tipo_acao == "remover_admin"))
            .OrderByDescending(a => a.id_acao) // Usar PK se não tiver data_acao
            .Select(a => a.motivo)
            .FirstOrDefaultAsync();

        ViewData["Motivo"] = string.IsNullOrWhiteSpace(motivo) ? "Motivo não indicado." : motivo;
        return View();
    }
}