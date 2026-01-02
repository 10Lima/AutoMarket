using AutoMarket.Data;
using AutoMarket.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Controllers;

[AllowAnonymous]
public class NotificacoesController : Controller
{
    private readonly MarketplaceDbContext _marketDb;
    private readonly CurrentUserService _currentUser;

    public NotificacoesController(MarketplaceDbContext marketDb, CurrentUserService currentUser)
    {
        _marketDb = marketDb;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Helper para detectar pedidos AJAX
    /// </summary>
    private static bool IsAjaxRequest(HttpRequest request)
        => string.Equals(request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // Página existe para todos.
        // Se não autenticado → mostra mensagem.
        if (!(User?.Identity?.IsAuthenticated ?? false))
        {
            ViewBag.Anonimo = true;
            return View(new List<AutoMarket.Models.Market.Notificacao_Recebida>());
        }

        var idUtil = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtil is null)
        {
            ViewBag.Anonimo = true;
            return View(new List<AutoMarket.Models.Market.Notificacao_Recebida>());
        }

        var list = await _marketDb.Notificacao_Recebida
            .AsNoTracking()
            .Where(nr => nr.id_utilizador == idUtil.Value)
            .Include(nr => nr.id_notificacaoNavigation)
            .OrderByDescending(nr => nr.data_envio)
            .ToListAsync();

        return View(list);
    }

    // ============================================================
    // POST: /Notificacoes/MarcarLida
    // Marca uma notificação como "lida" (só o dono pode fazer)
    // ============================================================
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarcarLida(long idRecebida)
    {
        var idUtil = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtil is null) return Unauthorized();

        // Só pode marcar como lida se a notificação for dele
        var nr = await _marketDb.Notificacao_Recebida
            .Where(x => x.id_recebida == idRecebida && x.id_utilizador == idUtil.Value)
            .Include(x => x.id_notificacaoNavigation)
            .FirstOrDefaultAsync();

        if (nr is null) return NotFound();

        // Atualizar estado apenas se ainda não está lida
        if (!string.Equals(nr.estado, "lida", StringComparison.OrdinalIgnoreCase))
        {
            nr.estado = "lida";
            await _marketDb.SaveChangesAsync();
        }

        // Se for AJAX, devolve só o item atualizado (partial)
        if (IsAjaxRequest(Request))
            return PartialView("_NotificacaoItem", nr);

        // Caso contrário, recarrega a lista completa
        return RedirectToAction(nameof(Index));
    }
}