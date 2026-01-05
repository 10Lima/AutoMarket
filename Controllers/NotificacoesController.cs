using AutoMarket.Data;
using AutoMarket.Models.Market;
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

    private static string Norm(string? s) => (s ?? "").Trim().ToLowerInvariant();

    // ============================================================
    // GET: /Notificacoes
    // "Shell" (tabs + filtros). A lista vem por AJAX via Lista().
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // Página existe para todos.
        // Se não autenticado → mostra mensagem.
        if (!(User?.Identity?.IsAuthenticated ?? false))
        {
            ViewBag.Anonimo = true;
            ViewBag.CountNaoLidas = 0;
            ViewBag.CountLidas = 0;
            ViewBag.CountTodas = 0;
            ViewBag.DefaultEstado = "nao_lidas";
            ViewBag.DefaultOrdem = "recent";
            return View(new List<Notificacao_Recebida>());
        }

        var idUtil = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtil is null)
        {
            ViewBag.Anonimo = true;
            ViewBag.CountNaoLidas = 0;
            ViewBag.CountLidas = 0;
            ViewBag.CountTodas = 0;
            ViewBag.DefaultEstado = "nao_lidas";
            ViewBag.DefaultOrdem = "recent";
            return View(new List<Notificacao_Recebida>());
        }

        // Contagens para tabs
        var baseQ = _marketDb.Notificacao_Recebida
            .AsNoTracking()
            .Where(nr => nr.id_utilizador == idUtil.Value);

        var countLidas = await baseQ.CountAsync(nr => nr.estado == "lida");
        var countNaoLidas = await baseQ.CountAsync(nr => nr.estado != "lida");
        var countTodas = countLidas + countNaoLidas;

        ViewBag.Anonimo = false;
        ViewBag.CountNaoLidas = countNaoLidas;
        ViewBag.CountLidas = countLidas;
        ViewBag.CountTodas = countTodas;

        // Defaults (tab default = não lidas)
        ViewBag.DefaultEstado = "nao_lidas";
        ViewBag.DefaultOrdem = "recent";

        // A lista é carregada por AJAX na action Lista()
        return View(new List<Notificacao_Recebida>());
    }

    // ============================================================
    // GET: /Notificacoes/Lista?estado=nao_lidas|lidas|todas&ordem=recent|old
    // Partial para atualizar a lista via AJAX.
    // ============================================================
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Lista(string estado = "nao_lidas", string ordem = "recent")
    {
        var idUtil = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtil is null) return Unauthorized();

        var st = Norm(estado);
        var ord = Norm(ordem);

        // Query base
        var baseQ = _marketDb.Notificacao_Recebida
            .AsNoTracking()
            .Include(nr => nr.id_notificacaoNavigation)
            .Where(nr => nr.id_utilizador == idUtil.Value);

        // Contagens (para atualizar tabs no partial via meta)
        var countLidas = await baseQ.CountAsync(nr => nr.estado == "lida");
        var countNaoLidas = await baseQ.CountAsync(nr => nr.estado != "lida");
        var countTodas = countLidas + countNaoLidas;

        ViewBag.CountNaoLidas = countNaoLidas;
        ViewBag.CountLidas = countLidas;
        ViewBag.CountTodas = countTodas;

        // Guardar filtro atual (para o _NotificacaoItem postar e manter contexto)
        ViewBag.Estado = st;
        ViewBag.Ordem = ord;

        // Filtro por tab
        IQueryable<Notificacao_Recebida> q = baseQ;

        if (st == "lidas")
            q = q.Where(nr => nr.estado == "lida");
        else if (st == "nao_lidas" || st == "naolidas" || st == "unread")
            q = q.Where(nr => nr.estado != "lida");
        // st == "todas" -> sem filtro extra

        // Ordenação
        q = (ord == "old" || ord == "antigas")
            ? q.OrderBy(nr => nr.data_envio)
            : q.OrderByDescending(nr => nr.data_envio);

        var list = await q.ToListAsync();

        return PartialView("_ListaNotificacoes", list);
    }

    // ============================================================
    // POST: /Notificacoes/MarcarLida
    // Marca uma notificação como "lida" (só o dono pode fazer)
    // Se AJAX: devolve a lista completa (para atualizar contagens/tabs)
    // ============================================================
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarcarLida(long idRecebida, string estado = "nao_lidas", string ordem = "recent")
    {
        var idUtil = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtil is null) return Unauthorized();

        // Só pode marcar como lida se a notificação for dele
        var nr = await _marketDb.Notificacao_Recebida
            .Where(x => x.id_recebida == idRecebida && x.id_utilizador == idUtil.Value)
            .FirstOrDefaultAsync();

        if (nr is null) return NotFound();

        // Atualizar estado apenas se ainda não está lida
        if (!string.Equals(nr.estado, "lida", StringComparison.OrdinalIgnoreCase))
        {
            nr.estado = "lida";
            await _marketDb.SaveChangesAsync();
        }

        // Se for AJAX, refresca a lista (para atualizar tabs/contagens)
        if (IsAjaxRequest(Request))
            return await Lista(estado, ordem);

        return RedirectToAction(nameof(Index));
    }
}
