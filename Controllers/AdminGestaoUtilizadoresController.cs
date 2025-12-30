using AutoMarket.Data;
using AutoMarket.Models.Market;
using AutoMarket.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Controllers;

[Authorize]
public class AdminGestaoUtilizadoresController : Controller
{
    private readonly MarketplaceDbContext _marketDb;
    private readonly ApplicationDbContext _identityDb;
    private readonly CurrentUserService _currentUser;

    public AdminGestaoUtilizadoresController(
        MarketplaceDbContext marketDb,
        ApplicationDbContext identityDb,
        CurrentUserService currentUser)
    {
        _marketDb = marketDb;
        _identityDb = identityDb;
        _currentUser = currentUser;
    }

    // GET: /AdminGestaoUtilizadores?filtro=...&adminFiltro=...
    [HttpGet]
    public async Task<IActionResult> Index(string? filtro = null, string? adminFiltro = "todos")
    {
        if (!await _currentUser.IsAdminAsync(User))
            return Forbid();

        filtro = (filtro ?? "").Trim();
        adminFiltro = NormalizeAdminFiltro(adminFiltro);

        ViewData["FiltroAtual"] = filtro;
        ViewData["AdminFiltroAtual"] = adminFiltro;

        var (lista, usernames, emails, adminsAtuais, estados, podeRecuperarAdmin) = await GetListaAsync(filtro, adminFiltro);

        ViewData["Usernames"] = usernames;
        ViewData["Emails"] = emails;
        ViewData["AdminsAtuais"] = adminsAtuais;
        ViewData["Estados"] = estados;
        ViewData["PodeRecuperarAdmin"] = podeRecuperarAdmin;

        return View(lista);
    }

    // GET: /AdminGestaoUtilizadores/Lista (AJAX)
    [HttpGet]
    public async Task<IActionResult> Lista(string? filtro = null, string? adminFiltro = "todos")
    {
        if (!await _currentUser.IsAdminAsync(User))
            return Forbid();

        filtro = (filtro ?? "").Trim();
        adminFiltro = NormalizeAdminFiltro(adminFiltro);

        var (lista, usernames, emails, adminsAtuais, estados, podeRecuperarAdmin) = await GetListaAsync(filtro, adminFiltro);

        ViewData["Usernames"] = usernames;
        ViewData["Emails"] = emails;
        ViewData["AdminsAtuais"] = adminsAtuais;
        ViewData["Estados"] = estados;
        ViewData["PodeRecuperarAdmin"] = podeRecuperarAdmin;

        return PartialView("_UtilizadoresTable", lista);
    }

    // POST: bloquear (motivo obrigatório)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Bloquear(int idUtilizador, string motivo, string? filtroAtual = null)
    {
        if (!await _currentUser.IsAdminAsync(User))
            return Forbid();

        motivo = (motivo ?? "").Trim();
        if (string.IsNullOrWhiteSpace(motivo))
            return BadRequest("Motivo é obrigatório.");

        if (motivo.Length > 400)
            return BadRequest("O motivo não pode ter mais de 400 caracteres.");

        var adminId = await _currentUser.GetMarketUserIdAsync(User);
        if (adminId is null)
            return Forbid();

        await using var tx = await _marketDb.Database.BeginTransactionAsync();
        try
        {
            var u = await _marketDb.Utilizador.FirstOrDefaultAsync(x => x.id_utilizador == idUtilizador);
            if (u == null) return NotFound();

            u.estado = "bloqueado";

            _marketDb.Ace_Admin_Acao.Add(new Ace_Admin_Acao
            {
                id_admin = adminId.Value,
                id_utilizador = u.id_utilizador,
                tipo_acao = "bloquear", // ? ALTERADO
                motivo = motivo
            });

            await _marketDb.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return await Lista(filtroAtual);
    }

    // POST: desbloquear (motivo opcional)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Desbloquear(int idUtilizador, string? motivo, string? filtroAtual = null)
    {
        if (!await _currentUser.IsAdminAsync(User))
            return Forbid();

        motivo = (motivo ?? "").Trim();
        if (motivo.Length > 400)
            return BadRequest("O motivo não pode ter mais de 400 caracteres.");

        var adminId = await _currentUser.GetMarketUserIdAsync(User);
        if (adminId is null)
            return Forbid();

        await using var tx = await _marketDb.Database.BeginTransactionAsync();
        try
        {
            var u = await _marketDb.Utilizador.FirstOrDefaultAsync(x => x.id_utilizador == idUtilizador);
            if (u == null) return NotFound();

            u.estado = "ativo";

            _marketDb.Ace_Admin_Acao.Add(new Ace_Admin_Acao
            {
                id_admin = adminId.Value,
                id_utilizador = u.id_utilizador,
                tipo_acao = "desbloquear", // ? ALTERADO
                motivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo
            });

            await _marketDb.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return await Lista(filtroAtual);
    }

    // POST: remover_admin (motivo obrigatório + bloquear conta)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoverAdmin(int idUtilizador, string motivo, string? filtroAtual = null)
    {
        if (!await _currentUser.IsAdminAsync(User))
            return Forbid();

        motivo = (motivo ?? "").Trim();
        if (string.IsNullOrWhiteSpace(motivo))
            return BadRequest("Motivo é obrigatório para remover um administrator.");

        if (motivo.Length > 400)
            return BadRequest("O motivo não pode ter mais de 400 caracteres.");

        var adminId = await _currentUser.GetMarketUserIdAsync(User);
        if (adminId is null)
            return Forbid();

        // Impedir remover a si próprio
        if (adminId.Value == idUtilizador)
            return BadRequest("Não é possível remover os próprios privilégios de administrador.");

        await using var tx = await _marketDb.Database.BeginTransactionAsync();
        try
        {
            // Verificar se é admin
            var alvoAdmin = await _marketDb.Administrador.FirstOrDefaultAsync(a => a.id_utilizador == idUtilizador);
            if (alvoAdmin == null)
                return BadRequest("Este utilizador não é administrador.");

            // Bloquear conta
            var utilizador = await _marketDb.Utilizador.FirstOrDefaultAsync(u => u.id_utilizador == idUtilizador);
            if (utilizador == null)
                return NotFound("Utilizador não encontrado.");

            utilizador.estado = "bloqueado";

            // Registar auditoria
            _marketDb.Ace_Admin_Acao.Add(new Ace_Admin_Acao
            {
                id_admin = adminId.Value,
                id_utilizador = idUtilizador,
                tipo_acao = "remover_admin",
                motivo = motivo
            });

            // Remover privilégios admin
            _marketDb.Administrador.Remove(alvoAdmin);

            await _marketDb.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return await Lista(filtroAtual);
    }

    // POST: recuperar_admin (motivo opcional)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecuperarAdmin(int idUtilizador, string? motivo, string? filtroAtual = null)
    {
        if (!await _currentUser.IsAdminAsync(User))
            return Forbid();

        var adminId = await _currentUser.GetMarketUserIdAsync(User);
        if (adminId is null)
            return Forbid();

        motivo = (motivo ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(motivo) && motivo.Length > 400)
            return BadRequest("O motivo não pode ter mais de 400 caracteres.");

        await using var tx = await _marketDb.Database.BeginTransactionAsync();
        try
        {
            var utilizador = await _marketDb.Utilizador.FirstOrDefaultAsync(u => u.id_utilizador == idUtilizador);
            if (utilizador == null)
                return NotFound("Utilizador não encontrado.");

            // Não pode ser vendedor
            if (await _marketDb.Vendedor.AnyAsync(v => v.id_utilizador == idUtilizador))
                return BadRequest("Não é possível recuperar admin: o utilizador é vendedor.");

            // Remover comprador se existir
            var comprador = await _marketDb.Comprador.FirstOrDefaultAsync(c => c.id_utilizador == idUtilizador);
            if (comprador != null)
                _marketDb.Comprador.Remove(comprador);

            // Inserir admin se não existir
            if (!await _marketDb.Administrador.AnyAsync(a => a.id_utilizador == idUtilizador))
            {
                _marketDb.Administrador.Add(new Administrador
                {
                    id_utilizador = idUtilizador
                });
            }

            // Desbloquear conta
            utilizador.estado = "ativo";

            // Registar auditoria
            _marketDb.Ace_Admin_Acao.Add(new Ace_Admin_Acao
            {
                id_admin = adminId.Value,
                id_utilizador = idUtilizador,
                tipo_acao = "recuperar_admin",
                motivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo
            });

            await _marketDb.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return await Lista(filtroAtual);
    }

    // =========================
    // Helpers privados
    // =========================
    private static string NormalizeAdminFiltro(string? v)
    {
        v = (v ?? "todos").Trim().ToLowerInvariant();
        return v switch
        {
            "admins" => "admins",
            "nao_admins" => "nao_admins",
            _ => "todos"
        };
    }

    private async Task<(List<Utilizador> lista,
                    Dictionary<int, string> usernames,
                    Dictionary<int, string> emails,
                    HashSet<int> adminsAtuais,
                    Dictionary<int, string> estados,
                    HashSet<int> podeRecuperarAdmin)>
        GetListaAsync(string filtro, string adminFiltro)
    {
        IQueryable<Utilizador> q = _marketDb.Utilizador.AsNoTracking();

        // Filtro por ID/username/email
        if (!string.IsNullOrWhiteSpace(filtro))
        {
            if (int.TryParse(filtro, out var n))
            {
                q = q.Where(u => u.id_utilizador == n);
            }
            else
            {
                var norm = filtro.Trim().ToUpperInvariant();
                var identity = await _identityDb.Users.AsNoTracking()
                    .Where(u => u.NormalizedUserName == norm || u.NormalizedEmail == norm)
                    .Select(u => new { u.Id })
                    .FirstOrDefaultAsync();

                if (identity != null)
                    q = q.Where(u => u.identity_user_id == identity.Id);
                else
                    q = q.Where(u => false);
            }
        }

        // ✅ Filtro por tipo (admin/não admin)
        if (adminFiltro != "todos")
        {
            var adminIds = _marketDb.Administrador.AsNoTracking().Select(a => a.id_utilizador);

            if (adminFiltro == "admins")
                q = q.Where(u => adminIds.Contains(u.id_utilizador));
            else // nao_admins
                q = q.Where(u => !adminIds.Contains(u.id_utilizador));
        }

        var lista = await q.OrderBy(u => u.id_utilizador).ToListAsync();

        var ids = lista.Select(u => u.id_utilizador).Distinct().ToList();

        var adminsAtuais = (await _marketDb.Administrador.AsNoTracking()
            .Where(a => ids.Contains(a.id_utilizador))
            .Select(a => a.id_utilizador)
            .ToListAsync())
            .ToHashSet();

        var estados = lista.ToDictionary(
            u => u.id_utilizador,
            u => (u.estado ?? "ativo").ToLowerInvariant()
        );

        var podeRecuperarAdmin = (await _marketDb.Ace_Admin_Acao.AsNoTracking()
            .Where(a => ids.Contains(a.id_utilizador) && a.tipo_acao == "remover_admin")
            .Select(a => a.id_utilizador)
            .Distinct()
            .ToListAsync())
            .ToHashSet();

        var map = lista
            .Where(u => !string.IsNullOrWhiteSpace(u.identity_user_id))
            .Select(u => new { u.id_utilizador, u.identity_user_id })
            .ToList();

        var identityIds = map.Select(x => x.identity_user_id).Distinct().ToList();

        var identityMap = await _identityDb.Users.AsNoTracking()
            .Where(u => identityIds.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName, u.Email })
            .ToListAsync();

        var userNameByIdentity = identityMap.ToDictionary(x => x.Id, x => x.UserName ?? "");
        var emailByIdentity = identityMap.ToDictionary(x => x.Id, x => x.Email ?? "");

        var usernames = map.ToDictionary(
            x => x.id_utilizador,
            x => userNameByIdentity.TryGetValue(x.identity_user_id, out var uname) ? uname : ""
        );

        var emails = map.ToDictionary(
            x => x.id_utilizador,
            x => emailByIdentity.TryGetValue(x.identity_user_id, out var em) ? em : ""
        );

        return (lista, usernames, emails, adminsAtuais, estados, podeRecuperarAdmin);
    }
}
