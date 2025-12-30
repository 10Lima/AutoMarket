using AutoMarket.Data;
using AutoMarket.Models.Market;
using AutoMarket.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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

    // GET: /Vender/Comecar
    public async Task<IActionResult> Comecar()
    {
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

        // Campos que NÃO vêm do form (são definidos no servidor ou são navegações)
        ModelState.Remove(nameof(Candidatura_Vendedor.id_candidatura));
        ModelState.Remove(nameof(Candidatura_Vendedor.id_utilizador));
        ModelState.Remove(nameof(Candidatura_Vendedor.estado));
        ModelState.Remove(nameof(Candidatura_Vendedor.data_submissao));

        // Navegações (DB-first costuma marcar como required por serem non-nullable)
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
}
