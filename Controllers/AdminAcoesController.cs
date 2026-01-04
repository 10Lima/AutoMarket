using AutoMarket.Data;
using AutoMarket.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Controllers;

[Authorize]
public class AdminAcoesController : Controller
{
    private readonly MarketplaceDbContext _marketDb;
    private readonly ApplicationDbContext _identityDb;
    private readonly CurrentUserService _currentUser;

    public AdminAcoesController(
        MarketplaceDbContext marketDb,
        ApplicationDbContext identityDb,
        CurrentUserService currentUser)
    {
        _marketDb = marketDb;
        _identityDb = identityDb;
        _currentUser = currentUser;
    }

    // ✅ Tipos válidos de ação
    private static readonly HashSet<string> TiposValidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "aprovar_vendedor",
        "rejeitar_vendedor",
        "bloquear",
        "desbloquear",
        "remover_admin",
        "recuperar_admin"
    };

    // ✅ Ordenações válidas
    private static readonly HashSet<string> OrdensValidas = new(StringComparer.OrdinalIgnoreCase)
    {
        "recentes",
        "antigas"
    };

    // GET: /AdminAcoes
    public async Task<IActionResult> Index(
        string? tipo,
        string? admin,
        string? utilizador,
        string? ordem,
        DateTime? dataInicio,
        DateTime? dataFim)
    {
        if (!await _currentUser.IsAdminAsync(User))
            return Forbid();

        tipo = NormalizeTipo(tipo);
        ordem = NormalizeOrdem(ordem);

        admin = (admin ?? "").Trim();
        utilizador = (utilizador ?? "").Trim();

        ViewData["TipoAtual"] = tipo;
        ViewData["AdminAtual"] = admin;
        ViewData["UtilizadorAtual"] = utilizador;

        ViewData["OrdemAtual"] = ordem;
        ViewData["DataInicioAtual"] = dataInicio;
        ViewData["DataFimAtual"] = dataFim;

        var (lista, usernames) = await GetAcoesAsync(tipo, admin, utilizador, ordem, dataInicio, dataFim);
        ViewData["Usernames"] = usernames;

        return View(lista);
    }

    // GET: /AdminAcoes/Lista (AJAX)
    [HttpGet]
    public async Task<IActionResult> Lista(
        string? tipo,
        string? admin,
        string? utilizador,
        string? ordem,
        DateTime? dataInicio,
        DateTime? dataFim)
    {
        if (!await _currentUser.IsAdminAsync(User))
            return Forbid();

        tipo = NormalizeTipo(tipo);
        ordem = NormalizeOrdem(ordem);

        admin = (admin ?? "").Trim();
        utilizador = (utilizador ?? "").Trim();

        var (lista, usernames) = await GetAcoesAsync(tipo, admin, utilizador, ordem, dataInicio, dataFim);
        ViewData["Usernames"] = usernames;

        return PartialView("_AcoesTable", lista);
    }

    // ✅ Helper: normalizar tipo de ação
    private static string NormalizeTipo(string? v)
    {
        v = (v ?? "todas").Trim().ToLowerInvariant();

        if (v == "todas" || !TiposValidos.Contains(v))
            return "todas";

        return v;
    }

    // ✅ Helper: normalizar ordem
    private static string NormalizeOrdem(string? v)
    {
        v = (v ?? "recentes").Trim().ToLowerInvariant();

        if (!OrdensValidas.Contains(v))
            return "recentes";

        return v;
    }

    // =======================
    // Query central (1 sítio)
    // =======================
    private async Task<(List<AutoMarket.Models.Market.Ace_Admin_Acao> lista, Dictionary<int, string> usernames)>
        GetAcoesAsync(
            string tipo,
            string admin,
            string utilizador,
            string ordem,
            DateTime? dataInicio,
            DateTime? dataFim)
    {
        IQueryable<AutoMarket.Models.Market.Ace_Admin_Acao> q =
            _marketDb.Ace_Admin_Acao.AsNoTracking();

        // ✅ Filtro por tipo
        if (tipo != "todas")
            q = q.Where(a => a.tipo_acao == tipo);

        // ✅ Filtro por datas (data_acao)
        if (dataInicio.HasValue)
        {
            var inicio = dataInicio.Value.Date; // 00:00
            q = q.Where(a => a.data_acao >= inicio);
        }

        if (dataFim.HasValue)
        {
            // Inclusivo até ao fim do dia
            var fimExclusivo = dataFim.Value.Date.AddDays(1); // dia seguinte 00:00
            q = q.Where(a => a.data_acao < fimExclusivo);
        }

        // Admin filter (id/username/email)
        if (!string.IsNullOrWhiteSpace(admin))
        {
            var adminId = await ResolveMarketUserIdAsync(admin);
            if (adminId.HasValue) q = q.Where(a => a.id_admin == adminId.Value);
            else q = q.Where(a => false);
        }

        // Utilizador filter (id/username/email)
        if (!string.IsNullOrWhiteSpace(utilizador))
        {
            var userId = await ResolveMarketUserIdAsync(utilizador);
            if (userId.HasValue) q = q.Where(a => a.id_utilizador == userId.Value);
            else q = q.Where(a => false);
        }

        // ✅ Ordenação
        q = ordem == "antigas"
            ? q.OrderBy(a => a.data_acao)
            : q.OrderByDescending(a => a.data_acao);

        var lista = await q.ToListAsync();

        // Map id_admin + id_utilizador => username
        var idsMarket = lista
            .SelectMany(a => new[] { a.id_admin, a.id_utilizador })
            .Distinct()
            .ToList();

        var marketMap = await _marketDb.Utilizador.AsNoTracking()
            .Where(u => idsMarket.Contains(u.id_utilizador))
            .Select(u => new { u.id_utilizador, u.identity_user_id })
            .ToListAsync();

        var identityIds = marketMap
            .Select(x => x.identity_user_id)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        var identityMap = await _identityDb.Users.AsNoTracking()
            .Where(u => identityIds.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName })
            .ToDictionaryAsync(x => x.Id, x => x.UserName ?? "");

        var usernames = marketMap.ToDictionary(
            x => x.id_utilizador,
            x => identityMap.TryGetValue(x.identity_user_id, out var uname) ? uname : ""
        );

        return (lista, usernames);
    }

    /// <summary>
    /// Input aceita: market id_utilizador (número) OU identity username OU email.
    /// </summary>
    private async Task<int?> ResolveMarketUserIdAsync(string input)
    {
        input = (input ?? "").Trim();
        if (string.IsNullOrWhiteSpace(input))
            return null;

        // 1) número => market id
        if (int.TryParse(input, out var id))
        {
            var exists = await _marketDb.Utilizador.AsNoTracking()
                .AnyAsync(u => u.id_utilizador == id);
            return exists ? id : null;
        }

        // 2) identity por username/email (usa Normalized*)
        var normalized = input.ToUpperInvariant();

        var identity = await _identityDb.Users.AsNoTracking()
            .Where(u => u.NormalizedUserName == normalized || u.NormalizedEmail == normalized)
            .Select(u => new { u.Id })
            .FirstOrDefaultAsync();

        if (identity == null)
            return null;

        // 3) map identity id -> market id_utilizador
        return await _marketDb.Utilizador.AsNoTracking()
            .Where(mu => mu.identity_user_id == identity.Id)
            .Select(mu => (int?)mu.id_utilizador)
            .FirstOrDefaultAsync();
    }
}
