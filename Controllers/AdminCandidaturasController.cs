using AutoMarket.Data;
using AutoMarket.Services;
using AutoMarket.Models.Market;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AutoMarket.Controllers;

[Authorize]
public class AdminCandidaturasController : Controller
{
    private readonly MarketplaceDbContext _marketDb;
    private readonly ApplicationDbContext _identityDb;
    private readonly CurrentUserService _currentUser;

    private static readonly HashSet<string> EstadosValidos =
        new(StringComparer.OrdinalIgnoreCase) { "pendente", "aprovada", "rejeitada", "cancelada" };

    public AdminCandidaturasController(
        MarketplaceDbContext marketDb,
        ApplicationDbContext identityDb,
        CurrentUserService currentUser)
    {
        _marketDb = marketDb;
        _identityDb = identityDb;
        _currentUser = currentUser;
    }

    // GET: /AdminCandidaturas?estado=pendente&idCandidatura=5&utilizador=seed_user_05
    [HttpGet]
    public async Task<IActionResult> Index(string? estado = "pendente", int? idCandidatura = null, string? utilizador = null)
    {
        if (!await _currentUser.IsAdminAsync(User))
            return Forbid();

        estado = NormalizeEstado(estado);
        utilizador = (utilizador ?? "").Trim();

        ViewData["EstadoAtual"] = estado;
        ViewData["IdCandidaturaAtual"] = idCandidatura;
        ViewData["UtilizadorAtual"] = utilizador;

        var (lista, usernames) = await GetListaAsync(estado, idCandidatura, utilizador);
        ViewData["Usernames"] = usernames;

        return View(lista);
    }

    // GET: /AdminCandidaturas/Lista (AJAX)
    [HttpGet]
    public async Task<IActionResult> Lista(string? estado = "pendente", int? idCandidatura = null, string? utilizador = null)
    {
        if (!await _currentUser.IsAdminAsync(User))
            return Forbid();

        estado = NormalizeEstado(estado);
        utilizador = (utilizador ?? "").Trim();

        var (lista, usernames) = await GetListaAsync(estado, idCandidatura, utilizador);
        ViewData["Usernames"] = usernames;

        return PartialView("_CandidaturasTable", lista);
    }

    // POST: /AdminCandidaturas/Aprovar
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aprovar(int id, string? estado = "pendente")
    {
        if (!await _currentUser.IsAdminAsync(User))
            return Forbid();

        estado = NormalizeEstado(estado);

        var adminId = await _currentUser.GetMarketUserIdAsync(User);
        if (adminId is null)
            return Forbid();

        await using var tx = await _marketDb.Database.BeginTransactionAsync();
        try
        {
            var candidatura = await _marketDb.Candidatura_Vendedor
                .FirstOrDefaultAsync(c => c.id_candidatura == id);

            if (candidatura == null)
                return NotFound();

            if (!string.Equals(candidatura.estado, "pendente", StringComparison.OrdinalIgnoreCase))
                return BadRequest("A candidatura já não está pendente.");

            // Criar vendedor (se ainda não existir)
            var jaVendedor = await _marketDb.Vendedor.AnyAsync(v => v.id_utilizador == candidatura.id_utilizador);
            if (!jaVendedor)
            {
                _marketDb.Vendedor.Add(new AutoMarket.Models.Market.Vendedor
                {
                    id_utilizador = candidatura.id_utilizador,
                    id_candidatura = candidatura.id_candidatura
                });
            }

            candidatura.estado = "aprovada";

            _marketDb.Ace_Admin_Acao.Add(new AutoMarket.Models.Market.Ace_Admin_Acao
            {
                id_admin = adminId.Value,
                id_utilizador = candidatura.id_utilizador,
                id_candidatura = candidatura.id_candidatura,
                tipo_acao = "aprovar_vendedor",
                motivo = null
            });

            // ✅ Notificação ao utilizador
            await CriarNotif_CandidaturaAprovada_UtilizadorAsync(candidatura);

            await _marketDb.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        // devolve a tabela atualizada (AJAX)
        var (lista, usernames) = await GetListaAsync(estado, null, null);
        ViewData["Usernames"] = usernames;
        return PartialView("_CandidaturasTable", lista);
    }

    // POST: /AdminCandidaturas/Rejeitar
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rejeitar(int id, string motivo, string? estado = "pendente")
    {
        if (!await _currentUser.IsAdminAsync(User))
            return Forbid();

        estado = NormalizeEstado(estado);

        var adminId = await _currentUser.GetMarketUserIdAsync(User);
        if (adminId is null)
            return Forbid();

        // ✅ Motivo obrigatório
        motivo = (motivo ?? "").Trim();
        if (string.IsNullOrWhiteSpace(motivo))
            return BadRequest("Motivo é obrigatório para rejeitar uma candidatura.");

        if (motivo.Length > 400)
            return BadRequest("O motivo não pode ter mais de 400 caracteres.");

        await using var tx = await _marketDb.Database.BeginTransactionAsync();
        try
        {
            var candidatura = await _marketDb.Candidatura_Vendedor
                .FirstOrDefaultAsync(c => c.id_candidatura == id);

            if (candidatura == null)
                return NotFound();

            if (!string.Equals(candidatura.estado, "pendente", StringComparison.OrdinalIgnoreCase))
                return BadRequest("A candidatura já não está pendente.");

            candidatura.estado = "rejeitada";

            _marketDb.Ace_Admin_Acao.Add(new AutoMarket.Models.Market.Ace_Admin_Acao
            {
                id_admin = adminId.Value,
                id_utilizador = candidatura.id_utilizador,
                id_candidatura = candidatura.id_candidatura,
                tipo_acao = "rejeitar_vendedor",
                motivo = motivo
            });

            // ✅ Notificação ao utilizador (com motivo)
            await CriarNotif_CandidaturaRejeitada_UtilizadorAsync(candidatura, motivo);

            await _marketDb.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        var (lista2, usernames2) = await GetListaAsync(estado, null, null);
        ViewData["Usernames"] = usernames2;
        return PartialView("_CandidaturasTable", lista2);
    }

    [HttpGet]
    public async Task<IActionResult> Detalhes(int id)
    {
        if (!await _currentUser.IsAdminAsync(User))
            return Forbid();

        var candidatura = await _marketDb.Candidatura_Vendedor.AsNoTracking()
            .FirstOrDefaultAsync(c => c.id_candidatura == id);

        if (candidatura == null)
            return NotFound();

        var utilizador = await _marketDb.Utilizador.AsNoTracking()
            .FirstOrDefaultAsync(u => u.id_utilizador == candidatura.id_utilizador);

        // contactos + morada (podem não existir em seed/testes)
        var contactos = await _marketDb.Utilizador_Contacto.AsNoTracking()
            .Where(x => x.id_utilizador == candidatura.id_utilizador)
            .OrderBy(x => x.tipo)
            .ToListAsync();

        var morada = await _marketDb.Morada.AsNoTracking()
            .FirstOrDefaultAsync(m => m.id_utilizador == candidatura.id_utilizador);

        // identity (username/email)
        string username = "";
        string email = "";

        if (utilizador != null && !string.IsNullOrWhiteSpace(utilizador.identity_user_id))
        {
            var iu = await _identityDb.Users.AsNoTracking()
                .Where(u => u.Id == utilizador.identity_user_id)
                .Select(u => new { u.UserName, u.Email })
                .FirstOrDefaultAsync();

            if (iu != null)
            {
                username = iu.UserName ?? "";
                email = iu.Email ?? "";
            }
        }

        ViewData["Username"] = username;
        ViewData["Email"] = email;
        ViewData["Nome"] = utilizador?.nome ?? "";
        ViewData["Morada"] = morada;
        ViewData["Contactos"] = contactos;

        return PartialView("_CandidaturaDetalhesModalBody", candidatura);
    }

    // =====================
    // Helpers privados
    // =====================

    private string NormalizeEstado(string? estado)
    {
        var e = (estado ?? "pendente").Trim().ToLowerInvariant();

        if (e == "todas")
            return "todas";

        if (!EstadosValidos.Contains(e))
            return "pendente";

        return e;
    }

    /// <summary>
    /// Query central com 3 filtros: estado, ID candidatura, utilizador (id/username/email)
    /// </summary>
    private async Task<(List<AutoMarket.Models.Market.Candidatura_Vendedor> lista, Dictionary<int, string> usernames)>
        GetListaAsync(string estado, int? idCandidatura, string utilizadorInput)
    {
        IQueryable<AutoMarket.Models.Market.Candidatura_Vendedor> q =
            _marketDb.Candidatura_Vendedor.AsNoTracking();

        // 1) Estado
        if (!string.Equals(estado, "todas", StringComparison.OrdinalIgnoreCase))
            q = q.Where(c => c.estado == estado);

        // 2) ID candidatura (se preenchido)
        if (idCandidatura.HasValue)
            q = q.Where(c => c.id_candidatura == idCandidatura.Value);

        // 3) Utilizador (id_utilizador OU username/email)
        if (!string.IsNullOrWhiteSpace(utilizadorInput))
        {
            int? idUtilizador = null;

            // se for número -> id_utilizador
            if (int.TryParse(utilizadorInput, out var n))
            {
                idUtilizador = await _marketDb.Utilizador.AsNoTracking()
                    .Where(u => u.id_utilizador == n)
                    .Select(u => (int?)u.id_utilizador)
                    .FirstOrDefaultAsync();
            }
            else
            {
                idUtilizador = await ResolveMarketUserIdAsync(utilizadorInput);
            }

            if (idUtilizador.HasValue)
                q = q.Where(c => c.id_utilizador == idUtilizador.Value);
            else
                q = q.Where(c => false); // sem resultados
        }

        q = q.OrderByDescending(c => c.data_submissao);

        var lista = await q.ToListAsync();

        // map usernames (id_utilizador -> username)
        var idsUtilizador = lista.Select(c => c.id_utilizador).Distinct().ToList();

        var marketMap = await _marketDb.Utilizador.AsNoTracking()
            .Where(u => idsUtilizador.Contains(u.id_utilizador))
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
    /// Aceita: market id_utilizador (número) OU identity username OU email.
    /// </summary>
    private async Task<int?> ResolveMarketUserIdAsync(string input)
    {
        input = (input ?? "").Trim();
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var normalized = input.ToUpperInvariant();

        var identity = await _identityDb.Users.AsNoTracking()
            .Where(u => u.NormalizedUserName == normalized || u.NormalizedEmail == normalized)
            .Select(u => new { u.Id })
            .FirstOrDefaultAsync();

        if (identity == null)
            return null;

        return await _marketDb.Utilizador.AsNoTracking()
            .Where(mu => mu.identity_user_id == identity.Id)
            .Select(mu => (int?)mu.id_utilizador)
            .FirstOrDefaultAsync();
    }

    // ============================================================
    // NOTIFICAÇÕES — Candidaturas (AdminCandidaturasController)
    // ============================================================

    private static string Notif_NormalizarCanalEmailSms(string? canalPreferido)
    {
        var c = (canalPreferido ?? "").Trim().ToLowerInvariant();
        return (c == "sms") ? "sms" : "email";
    }

    // só temos dispatcher de email -> sms nunca fica pendente
    private static string Notif_EstadoEnvio(bool notificarNovidades, string canal)
    {
        return (notificarNovidades && canal == "email") ? "pendente" : "enviada";
    }

    private async Task CriarNotif_CandidaturaAprovada_UtilizadorAsync(Candidatura_Vendedor candidatura)
    {
        var actionUrl = Url.Action("Criar", "Anuncios") ?? "/Anuncios/Criar";

        await CriarNotif_ParaUmUtilizadorAsync(
            idUtilizador: candidatura.id_utilizador,
            tipo: "candidatura_aprovada",
            titulo: "Candidatura aprovada",
            texto: "A tua candidatura foi aprovada. Já podes começar a vender.",
            payload: new
            {
                v = 1,
                id_candidatura = candidatura.id_candidatura,
                action_url = actionUrl,
                action_label = "Criar anúncio"
            }
        );
    }

    private async Task CriarNotif_CandidaturaRejeitada_UtilizadorAsync(Candidatura_Vendedor candidatura, string motivo)
    {
        var actionUrl = Url.Action("Candidatar", "Vender") ?? "/Vender/Candidatar";

        await CriarNotif_ParaUmUtilizadorAsync(
            idUtilizador: candidatura.id_utilizador,
            tipo: "candidatura_rejeitada",
            titulo: "Candidatura rejeitada",
            texto: $"A tua candidatura foi rejeitada. Motivo: {motivo}",
            payload: new
            {
                v = 1,
                id_candidatura = candidatura.id_candidatura,
                motivo,
                action_url = actionUrl,
                action_label = "Submeter nova candidatura"
            }
        );
    }

    private async Task CriarNotif_ParaUmUtilizadorAsync(int idUtilizador, string tipo, string titulo, string texto, object payload)
    {
        var u = await _marketDb.Utilizador
            .AsNoTracking()
            .Where(x => x.id_utilizador == idUtilizador)
            .Select(x => new { x.id_utilizador, x.notificar_novidades, x.canal_notificacao_preferido })
            .FirstOrDefaultAsync();

        if (u == null) return;

        var notif = new Notificacao
        {
            tipo = tipo,
            titulo = titulo,
            texto = texto,
            payload_json = JsonSerializer.Serialize(payload)
        };

        var canal = Notif_NormalizarCanalEmailSms(u.canal_notificacao_preferido);

        _marketDb.Notificacao_Recebida.Add(new Notificacao_Recebida
        {
            id_utilizador = u.id_utilizador,
            id_notificacaoNavigation = notif,
            data_envio = DateTime.UtcNow,
            canal = canal,
            estado = Notif_EstadoEnvio(u.notificar_novidades, canal)
        });
    }
}
