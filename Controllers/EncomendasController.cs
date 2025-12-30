using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AutoMarket.Data;
using AutoMarket.Models.Market;
using AutoMarket.Services;

namespace AutoMarket.Controllers;

[Authorize]
public class EncomendasController : Controller
{
    private readonly MarketplaceDbContext _marketDb;
    private readonly CurrentUserService _currentUser;

    public EncomendasController(MarketplaceDbContext marketDb, CurrentUserService currentUser)
    {
        _marketDb = marketDb;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Carrinho()
    {
        var idUtil = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtil == null) return Unauthorized();

        var isComprador = await _marketDb.Comprador.AnyAsync(c => c.id_utilizador == idUtil.Value);
        if (!isComprador) return Forbid();

        var pendentes = await _marketDb.Encomenda
            .AsNoTracking()
            .Where(e => e.id_utilizador == idUtil.Value && e.estado == "pendente")
            .Include(e => e.id_anuncioNavigation)
                .ThenInclude(a => a.id_veiculoNavigation)
            .OrderByDescending(e => e.data_encomenda)
            .ToListAsync();

        return View(pendentes);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Adicionar(int idAnuncio)
    {
        var idUtil = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtil == null) return Unauthorized();

        var isComprador = await _marketDb.Comprador.AnyAsync(c => c.id_utilizador == idUtil.Value);
        if (!isComprador) return Forbid();

        var anuncio = await _marketDb.Anuncio.FirstOrDefaultAsync(a => a.id_anuncio == idAnuncio);
        if (anuncio == null) return NotFound();

        if (anuncio.id_vendedor == idUtil.Value) return Forbid();

        var estadoAnuncio = (anuncio.estado ?? "").Trim().ToLowerInvariant();

        if (estadoAnuncio == "vendido") return Forbid();

        if (estadoAnuncio == "reservado")
        {
            var reservaAtiva = await _marketDb.Reserva
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.id_anuncio == idAnuncio && r.estado == "ativa");

            if (reservaAtiva == null || reservaAtiva.id_utilizador != idUtil.Value)
                return Forbid();
        }

        var existente = await _marketDb.Encomenda.FirstOrDefaultAsync(e =>
            e.id_utilizador == idUtil.Value &&
            e.id_anuncio == idAnuncio &&
            e.estado == "pendente");

        if (existente != null)
            return RedirectToAction(nameof(Checkout), new { id = existente.id_encomenda });

        var encomenda = new Encomenda
        {
            id_utilizador = idUtil.Value,
            id_anuncio = idAnuncio,
            data_encomenda = DateTime.UtcNow,
            metodo_pagamento = "cartao",
            estado = "pendente",
            moeda = "EUR",
            ref_pagamento = null
        };

        _marketDb.Encomenda.Add(encomenda);
        await _marketDb.SaveChangesAsync();

        return RedirectToAction(nameof(Checkout), new { id = encomenda.id_encomenda });
    }

    [HttpGet]
    public async Task<IActionResult> Checkout(long id)
    {
        var idUtil = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtil == null) return Unauthorized();

        var isComprador = await _marketDb.Comprador.AnyAsync(c => c.id_utilizador == idUtil.Value);
        if (!isComprador) return Forbid();

        var encomenda = await _marketDb.Encomenda
            .AsNoTracking()
            .Include(e => e.id_anuncioNavigation)
            .FirstOrDefaultAsync(e =>
                e.id_encomenda == id &&
                e.id_utilizador == idUtil.Value &&
                e.estado == "pendente");

        if (encomenda == null) return NotFound();

        return View(encomenda);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(long id, string NumeroCartao, int MesExpiracao, int AnoExpiracao, string Cvv)
    {
        var idUtil = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtil == null) return Unauthorized();

        var isComprador = await _marketDb.Comprador.AnyAsync(c => c.id_utilizador == idUtil.Value);
        if (!isComprador) return Forbid();

        var encomenda = await _marketDb.Encomenda
            .Include(e => e.id_anuncioNavigation)
            .FirstOrDefaultAsync(e =>
                e.id_encomenda == id &&
                e.id_utilizador == idUtil.Value &&
                e.estado == "pendente");

        if (encomenda == null) return NotFound();

        try
        {
            var fimMes = new DateTime(AnoExpiracao, MesExpiracao, 1).AddMonths(1).AddSeconds(-1);
            if (fimMes < DateTime.UtcNow)
                ModelState.AddModelError("", "Cartão expirado.");
        }
        catch
        {
            ModelState.AddModelError("", "Data de expiração inválida.");
        }

        if (!ModelState.IsValid)
            return View(encomenda);

        var anuncio = encomenda.id_anuncioNavigation;
        var estadoAnuncio = (anuncio.estado ?? "").Trim().ToLowerInvariant();

        if (estadoAnuncio == "vendido") return Forbid();

        if (estadoAnuncio == "reservado")
        {
            var reservaAtiva = await _marketDb.Reserva
                .FirstOrDefaultAsync(r => r.id_anuncio == anuncio.id_anuncio && r.estado == "ativa");

            if (reservaAtiva == null || reservaAtiva.id_utilizador != idUtil.Value)
                return Forbid();
        }

        await using var tx = await _marketDb.Database.BeginTransactionAsync();
        try
        {
            encomenda.metodo_pagamento = "cartao";
            encomenda.estado = "pago";
            encomenda.ref_pagamento = GerarRefSimulada(NumeroCartao, MesExpiracao, AnoExpiracao);

            anuncio.estado = "vendido";
            anuncio.data_atualizacao = DateTime.UtcNow;

            var reserva = await _marketDb.Reserva
                .FirstOrDefaultAsync(r => r.id_anuncio == anuncio.id_anuncio && r.estado == "ativa");
            if (reserva != null)
                reserva.estado = "cancelada";

            var outras = await _marketDb.Encomenda
                .Where(e => e.id_anuncio == anuncio.id_anuncio && e.estado == "pendente" && e.id_encomenda != encomenda.id_encomenda)
                .ToListAsync();

            foreach (var e in outras)
                e.estado = "cancelado";

            await _marketDb.SaveChangesAsync();
            await tx.CommitAsync();

            TempData["SucessoCompra"] = "Compra bem sucedida.";
            return RedirectToAction(nameof(Carrinho));
        }
        catch
        {
            await tx.RollbackAsync();
            ModelState.AddModelError("", "Falha ao concluir o pagamento (simulação).");
            return View(encomenda);
        }
    }

    private static string GerarRefSimulada(string numero, int mes, int ano)
    {
        var digits = new string((numero ?? "").Where(char.IsDigit).ToArray());
        var last4 = digits.Length >= 4 ? digits[^4..] : digits;
        var token = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return $"SIM-CARD-{last4}-{mes:00}/{ano}-{token}";
    }
}