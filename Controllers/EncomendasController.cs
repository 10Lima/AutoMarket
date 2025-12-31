using AutoMarket.Data;
using AutoMarket.Models.Market;
using AutoMarket.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
    public async Task<IActionResult> Checkout(
    long id,
    string TitularCartao,
    string NumeroCartao,
    string Validade,
    string Cvv)
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

        // -------------------------
        // 1) Validações do cartão
        // -------------------------

        // ✅ Normalizar titular: remove acentos + uppercase
        TitularCartao = (TitularCartao ?? "").Trim();
        TitularCartao = TitularCartao.Normalize(System.Text.NormalizationForm.FormD);
        TitularCartao = new string(TitularCartao.Where(c => 
            System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
        TitularCartao = TitularCartao.ToUpperInvariant();

        if (TitularCartao.Length < 3 || TitularCartao.Length > 80)
            ModelState.AddModelError("", "O titular do cartão deve ter entre 3 e 80 caracteres.");
        else
        {
            var titularOk = Regex.IsMatch(TitularCartao, @"^[A-Z]+(?:[ '\-][A-Z]+)*$");
            if (!titularOk)
                ModelState.AddModelError("", "O titular do cartão tem um formato inválido.");
        }

        // ✅ PAN: remove espaços/hífens, valida 16 dígitos
        var digits = new string((NumeroCartao ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length != 16)
            ModelState.AddModelError("", "O número do cartão deve ter exatamente 16 dígitos.");
        else if (!PassaLuhn(digits))
            ModelState.AddModelError("", "O número do cartão é inválido (Luhn).");

        // ✅ CVV: remove espaços, valida 3 dígitos
        var cvvDigits = new string((Cvv ?? "").Where(char.IsDigit).ToArray());
        if (cvvDigits.Length != 3)
            ModelState.AddModelError("", "O CVV deve ter exatamente 3 dígitos.");

        // ✅ Validade: formato MM/YY + validação de expiração (mantém como estava)
        Validade = (Validade ?? "").Trim();
        var matchValidade = Regex.Match(Validade, @"^(0[1-9]|1[0-2])\/(\d{2})$");

        int mesVal = 0;
        int anoVal = 0;

        if (!matchValidade.Success)
        {
            ModelState.AddModelError("", "A validade deve estar no formato MM/YY (ex.: 12/27).");
        }
        else
        {
            mesVal = int.Parse(matchValidade.Groups[1].Value);
            int yy = int.Parse(matchValidade.Groups[2].Value);
            anoVal = 2000 + yy;

            var agora = DateTime.UtcNow;
            int anoAtual = agora.Year;
            int mesAtual = agora.Month;

            if (anoVal < anoAtual || (anoVal == anoAtual && mesVal < mesAtual))
                ModelState.AddModelError("", "O cartão está expirado.");
        }

        if (!ModelState.IsValid)
        {
            return View(encomenda);
        }

        // -------------------------
        // 2) Regras do anúncio
        // -------------------------
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

        // -------------------------
        // 3) Finalizar (simulação)
        // -------------------------
        await using var tx = await _marketDb.Database.BeginTransactionAsync();
        try
        {
            encomenda.metodo_pagamento = "cartao";
            encomenda.estado = "pago";
            encomenda.ref_pagamento = GerarRefSimulada(digits, mesVal, anoVal);

            anuncio.estado = "vendido";
            anuncio.data_atualizacao = DateTime.UtcNow;

            // Cancela reserva ativa (se existir)
            var reserva = await _marketDb.Reserva
                .FirstOrDefaultAsync(r => r.id_anuncio == anuncio.id_anuncio && r.estado == "ativa");
            if (reserva != null)
                reserva.estado = "cancelada";

            // Cancela outras encomendas pendentes para o mesmo anúncio
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
            ModelState.AddModelError("", "Falha ao concluir a compra (simulação).");
            return View(encomenda);
        }
    }

    private static bool PassaLuhn(string digits)
    {
        int sum = 0;
        bool alternate = false;

        for (int i = digits.Length - 1; i >= 0; i--)
        {
            int n = digits[i] - '0';
            if (alternate)
            {
                n *= 2;
                if (n > 9) n -= 9;
            }
            sum += n;
            alternate = !alternate;
        }
        return (sum % 10) == 0;
    }

    private static string GerarRefSimulada(string digitsOnly, int mes, int ano)
    {
        var last4 = digitsOnly.Length >= 4 ? digitsOnly[^4..] : digitsOnly;
        var token = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return $"SIM-CARD-{last4}-{mes:00}/{ano}-{token}";
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancelar(long id, string? returnTo)
    {
        var idUtil = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtil == null) return Unauthorized();

        var isComprador = await _marketDb.Comprador.AnyAsync(c => c.id_utilizador == idUtil.Value);
        if (!isComprador) return Forbid();

        var encomenda = await _marketDb.Encomenda
            .FirstOrDefaultAsync(e =>
                e.id_encomenda == id &&
                e.id_utilizador == idUtil.Value &&
                e.estado == "pendente");

        if (encomenda == null) return NotFound();

        encomenda.estado = "cancelado";
        await _marketDb.SaveChangesAsync();

        TempData["InfoCarrinho"] = "Encomenda cancelada.";

        // 🔁 Decidir para onde voltar
        if (string.Equals(returnTo, "MinhasOperacoes", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction("MinhasOperacoes", "Anuncios");

        // Default: volta para Carrinho
        return RedirectToAction(nameof(Carrinho));
    }

}