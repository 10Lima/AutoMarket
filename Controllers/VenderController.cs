using AutoMarket.Data;
using AutoMarket.Models.Market;
using AutoMarket.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AutoMarket.Controllers;

public class VenderController : Controller
{
    private readonly MarketplaceDbContext _marketDb;
    private readonly CurrentUserService _currentUser;

    public VenderController(MarketplaceDbContext marketDb, CurrentUserService currentUser)
    {
        _marketDb = marketDb;
        _currentUser = currentUser;
    }

    // ✅ Bloquear admins no VenderController
    private async Task<IActionResult?> BloquearSeAdminAsync()
    {
        // Só faz sentido avaliar se estiver autenticado
        if (User?.Identity?.IsAuthenticated == true)
        {
            var isAdmin = await _currentUser.IsAdminAsync(User);
            if (isAdmin)
            {
                // Admin não deve usar o fluxo de vendedor
                return RedirectToAction("Index", "Home");
            }
        }

        return null;
    }

    // GET: /Vender/Comecar
    public async Task<IActionResult> Comecar()
    {
        // ✅ Se for admin autenticado, bloqueia
        var adminBlock = await BloquearSeAdminAsync();
        if (adminBlock != null) return adminBlock;

        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return RedirectToPage("/Account/Register", new
            {
                area = "Identity",
                returnUrl = Url.Action("Comecar", "Vender")
            });
        }

        var idUtilizador = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtilizador is null)
        {
            return RedirectToPage("/Account/Register", new { area = "Identity" });
        }

        // Já é vendedor?
        var isVendedor = await _marketDb.Vendedor.AnyAsync(v => v.id_utilizador == idUtilizador.Value);
        if (isVendedor)
        {
            return RedirectToAction("Criar", "Anuncios");
        }

        // Tem alguma candidatura (qualquer estado)?
        var temCandidatura = await _marketDb.Candidatura_Vendedor
            .AnyAsync(c => c.id_utilizador == idUtilizador.Value);

        if (temCandidatura)
            return RedirectToAction(nameof(Estado));

        return RedirectToAction(nameof(Candidatar));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Candidatar(
        [Bind("tipo,NIF_NIPC,telemovel_comercial,website_url")] Candidatura_Vendedor input)
    {
        // ✅ Se for admin autenticado, bloqueia
        var adminBlock = await BloquearSeAdminAsync();
        if (adminBlock != null) return adminBlock;

        var idUtilizador = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtilizador is null)
            return RedirectToPage("/Account/Register", new { area = "Identity" });

        // Normalizar tipo
        input.tipo = (input.tipo ?? "").Trim().ToLowerInvariant();
        if (input.tipo != "particular" && input.tipo != "empresa")
            ModelState.AddModelError("tipo", "Tipo inválido. Use particular ou empresa.");

        // Normalizar strings
        input.NIF_NIPC = (input.NIF_NIPC ?? "").Trim();
        input.telemovel_comercial = string.IsNullOrWhiteSpace(input.telemovel_comercial) ? null : input.telemovel_comercial.Trim();
        input.website_url = string.IsNullOrWhiteSpace(input.website_url) ? null : input.website_url.Trim();

        // Se já existe pendente, bloqueia (UX)
        var pendente = await _marketDb.Candidatura_Vendedor
            .AnyAsync(c => c.id_utilizador == idUtilizador.Value && c.estado == "pendente");

        if (pendente)
            return RedirectToAction(nameof(Estado));

        // Campos que NÃO vêm do form
        ModelState.Remove(nameof(Candidatura_Vendedor.id_candidatura));
        ModelState.Remove(nameof(Candidatura_Vendedor.id_utilizador));
        ModelState.Remove(nameof(Candidatura_Vendedor.estado));
        ModelState.Remove(nameof(Candidatura_Vendedor.data_submissao));

        // Navegações (DB-first)
        ModelState.Remove(nameof(Candidatura_Vendedor.id_utilizadorNavigation));
        ModelState.Remove(nameof(Candidatura_Vendedor.Vendedor));
        ModelState.Remove(nameof(Candidatura_Vendedor.Ace_Admin_Acao));

        if (!ModelState.IsValid)
            return View(input);

        // Criar entidade final (evita overposting)
        var candidatura = new Candidatura_Vendedor
        {
            id_utilizador = idUtilizador.Value,
            tipo = input.tipo,
            NIF_NIPC = input.NIF_NIPC,
            telemovel_comercial = input.telemovel_comercial,
            website_url = input.website_url,
            estado = "pendente"
        };

        _marketDb.Candidatura_Vendedor.Add(candidatura);

        try
        {
            await _marketDb.SaveChangesAsync();

            // ✅ Notificações
            await CriarNotif_CandidaturaSubmetida_UtilizadorAsync(candidatura);
            await CriarNotif_AdminCandidaturaPendenteAsync(candidatura);

            // grava Notificacao + Notificacao_Recebida
            await _marketDb.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(string.Empty, "Não foi possível submeter a candidatura. Confirma os dados e tenta novamente.");
            return View(input);
        }

        return RedirectToAction(nameof(Estado));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancelar()
    {
        // ✅ Se for admin autenticado, bloqueia
        var adminBlock = await BloquearSeAdminAsync();
        if (adminBlock != null) return adminBlock;

        var idUtilizador = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtilizador is null)
            return Unauthorized();

        var candidatura = await _marketDb.Candidatura_Vendedor
            .Where(c => c.id_utilizador == idUtilizador.Value)
            .OrderByDescending(c => c.data_submissao)
            .FirstOrDefaultAsync();

        if (candidatura == null)
            return BadRequest("Não existe candidatura.");

        if (!string.Equals(candidatura.estado, "pendente", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Só é possível cancelar candidaturas pendentes.");

        candidatura.estado = "cancelada";
        await _marketDb.SaveChangesAsync();

        // Se for AJAX, devolve partial com o novo estado
        var isAjax = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
        if (isAjax)
        {
            return PartialView("_EstadoCandidatura", candidatura);
        }

        return RedirectToAction(nameof(Estado));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Estado()
    {
        // ✅ Se for admin autenticado, bloqueia
        var adminBlock = await BloquearSeAdminAsync();
        if (adminBlock != null) return adminBlock;

        var idUtilizador = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtilizador is null)
            return RedirectToPage("/Account/Register", new { area = "Identity" });

        var candidatura = await _marketDb.Candidatura_Vendedor.AsNoTracking()
            .Where(c => c.id_utilizador == idUtilizador.Value)
            .OrderByDescending(c => c.data_submissao)
            .FirstOrDefaultAsync();

        if (candidatura == null)
            return RedirectToAction(nameof(Candidatar));

        string? motivo = null;

        if (string.Equals(candidatura.estado, "rejeitada", StringComparison.OrdinalIgnoreCase))
        {
            motivo = await _marketDb.Ace_Admin_Acao.AsNoTracking()
                .Where(a =>
                    a.id_utilizador == idUtilizador.Value &&
                    a.id_candidatura == candidatura.id_candidatura &&
                    a.tipo_acao == "rejeitar_vendedor")
                .OrderByDescending(a => a.data_acao)
                .Select(a => a.motivo)
                .FirstOrDefaultAsync();
        }

        ViewData["MotivoRejeicao"] = motivo;
        return View(candidatura);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Candidatar(int? nova = null)
    {
        // ✅ Se for admin autenticado, bloqueia
        var adminBlock = await BloquearSeAdminAsync();
        if (adminBlock != null) return adminBlock;

        var idUtilizador = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtilizador is null)
            return RedirectToPage("/Account/Register", new { area = "Identity" });

        // Se já é vendedor, vai para criar anúncio
        if (await _marketDb.Vendedor.AnyAsync(v => v.id_utilizador == idUtilizador.Value))
            return RedirectToAction("Criar", "Anuncios");

        // Se já existe alguma candidatura e o utilizador não pediu explicito
        if (await _marketDb.Candidatura_Vendedor.AnyAsync(c => c.id_utilizador == idUtilizador.Value))
        {
            if (nova != 1)
                return RedirectToAction(nameof(Estado));
        }

        return View();
    }

    // ============================================================
    // NOTIFICAÇÕES — Candidaturas (VenderController)
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

    private async Task CriarNotif_CandidaturaSubmetida_UtilizadorAsync(Candidatura_Vendedor candidatura)
    {
        var u = await _marketDb.Utilizador
            .AsNoTracking()
            .Where(x => x.id_utilizador == candidatura.id_utilizador)
            .Select(x => new { x.id_utilizador, x.notificar_novidades, x.canal_notificacao_preferido })
            .FirstOrDefaultAsync();

        if (u == null) return;

        var payload = new { v = 1, id_candidatura = candidatura.id_candidatura, tipo = candidatura.tipo };

        var notif = new Notificacao
        {
            tipo = "candidatura_submetida",
            titulo = "Candidatura submetida",
            texto = "A tua candidatura para vendedor foi submetida e está em análise.",
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

    private async Task CriarNotif_AdminCandidaturaPendenteAsync(Candidatura_Vendedor candidatura)
    {
        // admins = linhas em market.Administrador
        var adminIds = await _marketDb.Administrador
            .AsNoTracking()
            .Select(a => a.id_utilizador)
            .ToListAsync();

        if (adminIds.Count == 0) return;

        var admins = await _marketDb.Utilizador
            .AsNoTracking()
            .Where(u => adminIds.Contains(u.id_utilizador))
            .Select(u => new { u.id_utilizador, u.notificar_novidades, u.canal_notificacao_preferido })
            .ToListAsync();

        if (admins.Count == 0) return;

        var payload = new { v = 1, id_candidatura = candidatura.id_candidatura, id_utilizador = candidatura.id_utilizador };

        var notif = new Notificacao
        {
            tipo = "admin_candidatura_pendente",
            titulo = "Nova candidatura pendente",
            texto = "Existe uma nova candidatura pendente para aprovação/rejeição.",
            payload_json = JsonSerializer.Serialize(payload)
        };

        foreach (var a in admins)
        {
            var canal = Notif_NormalizarCanalEmailSms(a.canal_notificacao_preferido);

            _marketDb.Notificacao_Recebida.Add(new Notificacao_Recebida
            {
                id_utilizador = a.id_utilizador,
                id_notificacaoNavigation = notif,
                data_envio = DateTime.UtcNow,
                canal = canal,
                estado = Notif_EstadoEnvio(a.notificar_novidades, canal)
            });
        }
    }
}
