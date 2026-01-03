using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AutoMarket.Data;
using AutoMarket.Models.Market;
using AutoMarket.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Controllers;

[Authorize]
public class AdminSobreNosController : Controller
{
    private readonly MarketplaceDbContext _marketDb;
    private readonly CurrentUserService _currentUser;

    public AdminSobreNosController(MarketplaceDbContext marketDb, CurrentUserService currentUser)
    {
        _marketDb = marketDb;
        _currentUser = currentUser;
    }

    private async Task<bool> IsAdminAsync()
        => await _currentUser.IsAdminAsync(User);

    private static bool IsAjaxRequest(HttpRequest request)
        => request.Headers["X-Requested-With"] == "XMLHttpRequest";

    private async Task<Informacoes_Market?> GetInfoAsync()
        => await _marketDb.Informacoes_Market.AsNoTracking()
            .OrderBy(i => i.id_market)
            .FirstOrDefaultAsync();

    private async Task<IActionResult> ForbidIfNotAdmin()
    {
        if (!await IsAdminAsync()) return Forbid();
        return Ok();
    }

    // GET: /AdminSobreNos
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!await IsAdminAsync()) return Forbid();

        var info = await GetInfoAsync();
        if (info == null) return View("Empty");

        ViewBag.Contactos = await _marketDb.Market_Contacto.AsNoTracking()
            .Where(c => c.id_market == info.id_market)
            .OrderBy(c => c.tipo).ThenBy(c => c.valor)
            .ToListAsync();

        ViewBag.Horarios = await _marketDb.Market_Horario_Suporte.AsNoTracking()
            .Where(h => h.id_market == info.id_market)
            .OrderBy(h => h.weekday)
            .ToListAsync();

        ViewBag.Excecoes = await _marketDb.Market_Horario_Suporte_Excecao.AsNoTracking()
            .Where(e => e.id_market == info.id_market)
            .OrderBy(e => e.data)
            .ToListAsync();

        return View(info);
    }

    // =========================================================
    // INFO (market.Informacoes_Market)
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> _Info()
    {
        if (!await IsAdminAsync()) return Forbid();
        var info = await GetInfoAsync();
        if (info == null) return PartialView("_InfoForm", new Informacoes_Market());
        return PartialView("_InfoForm", info);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateInfo(int id_market, string URL, string nome, string termos_e_politicas)
    {
        if (!await IsAdminAsync()) return Forbid();

        URL = (URL ?? "").Trim();
        nome = (nome ?? "").Trim();
        termos_e_politicas = (termos_e_politicas ?? "").Trim();

        if (string.IsNullOrWhiteSpace(URL) || URL.Length > 255)
            return BadRequest("URL inválida.");

        if (string.IsNullOrWhiteSpace(nome) || nome.Length > 120)
            return BadRequest("Nome inválido.");

        if (string.IsNullOrWhiteSpace(termos_e_politicas))
            return BadRequest("Termos e políticas são obrigatórios.");

        var info = await _marketDb.Informacoes_Market.FirstOrDefaultAsync(i => i.id_market == id_market);
        if (info == null) return NotFound();

        info.URL = URL;
        info.nome = nome;
        info.termos_e_politicas = termos_e_politicas;

        await _marketDb.SaveChangesAsync();

        var updated = await _marketDb.Informacoes_Market.AsNoTracking()
            .FirstAsync(i => i.id_market == id_market);

        return PartialView("_InfoForm", updated);
    }

    // =========================================================
    // CONTACTOS (market.Market_Contacto)
    // =========================================================

    private async Task<IActionResult> ContactosPartial(int id_market)
    {
        var lista = await _marketDb.Market_Contacto.AsNoTracking()
            .Where(c => c.id_market == id_market)
            .OrderBy(c => c.tipo).ThenBy(c => c.valor)
            .ToListAsync();

        ViewBag.IdMarket = id_market;
        return PartialView("_ContactosTable", lista);
    }

    [HttpGet]
    public async Task<IActionResult> _Contactos(int id_market)
    {
        if (!await IsAdminAsync()) return Forbid();
        return await ContactosPartial(id_market);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveContacto(int id_market, int id_contacto, string tipo, string valor)
    {
        if (!await IsAdminAsync()) return Forbid();

        tipo = (tipo ?? "").Trim().ToLowerInvariant();
        valor = (valor ?? "").Trim();

        if (string.IsNullOrWhiteSpace(tipo) || tipo.Length > 30)
            return BadRequest("Tipo inválido.");

        if (string.IsNullOrWhiteSpace(valor) || valor.Length > 120)
            return BadRequest("Valor inválido.");

        // evita duplicados (UQ_Market_Contacto)
        var dup = await _marketDb.Market_Contacto.AnyAsync(c =>
            c.id_market == id_market &&
            c.tipo == tipo &&
            c.valor == valor &&
            c.id_contacto != id_contacto);

        if (dup) return BadRequest("Já existe um contacto igual.");

        if (id_contacto == 0)
        {
            _marketDb.Market_Contacto.Add(new Market_Contacto
            {
                id_market = id_market,
                tipo = tipo,
                valor = valor
            });
        }
        else
        {
            var row = await _marketDb.Market_Contacto.FirstOrDefaultAsync(c => c.id_contacto == id_contacto && c.id_market == id_market);
            if (row == null) return NotFound();

            row.tipo = tipo;
            row.valor = valor;
        }

        await _marketDb.SaveChangesAsync();
        return await ContactosPartial(id_market);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteContacto(int id_market, int id_contacto)
    {
        if (!await IsAdminAsync()) return Forbid();

        var row = await _marketDb.Market_Contacto.FirstOrDefaultAsync(c => c.id_contacto == id_contacto && c.id_market == id_market);
        if (row == null) return NotFound();

        _marketDb.Market_Contacto.Remove(row);
        await _marketDb.SaveChangesAsync();

        return await ContactosPartial(id_market);
    }

    // =========================================================
    // HORÁRIO (market.Market_Horario_Suporte)
    // =========================================================

    private static bool TryParseTime(string? s, out TimeOnly t)
        => TimeOnly.TryParseExact((s ?? "").Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out t);

    private async Task<IActionResult> HorarioPartial(int id_market)
    {
        var lista = await _marketDb.Market_Horario_Suporte.AsNoTracking()
            .Where(h => h.id_market == id_market)
            .OrderBy(h => h.weekday)
            .ToListAsync();

        ViewBag.IdMarket = id_market;
        return PartialView("_HorarioTable", lista);
    }

    [HttpGet]
    public async Task<IActionResult> _Horario(int id_market)
    {
        if (!await IsAdminAsync()) return Forbid();
        return await HorarioPartial(id_market);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveHorario(int id_market)
    {
        if (!await IsAdminAsync()) return Forbid();

        await using var tx = await _marketDb.Database.BeginTransactionAsync();
        try
        {
            for (byte d = 1; d <= 7; d++)
            {
                var fechado = Request.Form[$"fechado_{d}"] == "on";
                var abreStr = (string?)Request.Form[$"abre_{d}"];
                var fechaStr = (string?)Request.Form[$"fecha_{d}"];
                var tz = ((string?)Request.Form[$"timezone_{d}"] ?? "").Trim();
                var obs = ((string?)Request.Form[$"observacoes_{d}"] ?? "").Trim();

                if (string.IsNullOrWhiteSpace(tz)) tz = "Europe/Lisbon";
                if (tz.Length > 64) return BadRequest("Timezone demasiado longa.");
                if (obs.Length > 200) return BadRequest("Observações demasiado longas.");

                TimeOnly? abre = null;
                TimeOnly? fecha = null;

                if (!fechado)
                {
                    if (!TryParseTime(abreStr, out var a)) return BadRequest($"Hora de abertura inválida (dia {d}).");
                    if (!TryParseTime(fechaStr, out var f)) return BadRequest($"Hora de fecho inválida (dia {d}).");
                    if (a >= f) return BadRequest($"Abertura tem de ser antes do fecho (dia {d}).");

                    abre = a; fecha = f;
                }

                var row = await _marketDb.Market_Horario_Suporte
                    .FirstOrDefaultAsync(h => h.id_market == id_market && h.weekday == d);

                if (row == null)
                {
                    row = new Market_Horario_Suporte
                    {
                        id_market = id_market,
                        weekday = d
                    };
                    _marketDb.Market_Horario_Suporte.Add(row);
                }

                row.fechado = fechado;
                row.abre = abre;
                row.fecha = fecha;
                row.timezone = tz;
                row.observacoes = string.IsNullOrWhiteSpace(obs) ? null : obs;
            }

            await _marketDb.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        return await HorarioPartial(id_market);
    }

    // =========================================================
    // EXCEÇÕES (market.Market_Horario_Suporte_Excecao)
    // =========================================================

    private async Task<IActionResult> ExcecoesPartial(int id_market)
    {
        var lista = await _marketDb.Market_Horario_Suporte_Excecao.AsNoTracking()
            .Where(e => e.id_market == id_market)
            .OrderBy(e => e.data)
            .ToListAsync();

        ViewBag.IdMarket = id_market;
        return PartialView("_ExcecoesTable", lista);
    }

    [HttpGet]
    public async Task<IActionResult> _Excecoes(int id_market)
    {
        if (!await IsAdminAsync()) return Forbid();
        return await ExcecoesPartial(id_market);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveExcecao(
        int id_market,
        int id_excecao_suporte,
        string data,
        bool fechado,
        string? abre,
        string? fecha,
        string? motivo)
    {
        if (!await IsAdminAsync()) return Forbid();

        if (!DateOnly.TryParseExact((data ?? "").Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return BadRequest("Data inválida.");

        motivo = (motivo ?? "").Trim();
        if (motivo.Length > 200) return BadRequest("Motivo demasiado longo.");

        TimeOnly? a = null, f = null;
        if (!fechado)
        {
            if (!TryParseTime(abre, out var at)) return BadRequest("Hora de abertura inválida.");
            if (!TryParseTime(fecha, out var ft)) return BadRequest("Hora de fecho inválida.");
            if (at >= ft) return BadRequest("Abertura tem de ser antes do fecho.");
            a = at; f = ft;
        }

        // UQ (id_market, data)
        var dup = await _marketDb.Market_Horario_Suporte_Excecao.AnyAsync(e =>
            e.id_market == id_market && e.data == d && e.id_excecao_suporte != id_excecao_suporte);

        if (dup) return BadRequest("Já existe uma exceção nessa data.");

        if (id_excecao_suporte == 0)
        {
            _marketDb.Market_Horario_Suporte_Excecao.Add(new Market_Horario_Suporte_Excecao
            {
                id_market = id_market,
                data = d,
                fechado = fechado,
                abre = a,
                fecha = f,
                motivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo
            });
        }
        else
        {
            var row = await _marketDb.Market_Horario_Suporte_Excecao
                .FirstOrDefaultAsync(e => e.id_excecao_suporte == id_excecao_suporte && e.id_market == id_market);

            if (row == null) return NotFound();

            row.data = d;
            row.fechado = fechado;
            row.abre = a;
            row.fecha = f;
            row.motivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo;
        }

        await _marketDb.SaveChangesAsync();
        return await ExcecoesPartial(id_market);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteExcecao(int id_market, int id_excecao_suporte)
    {
        if (!await IsAdminAsync()) return Forbid();

        var row = await _marketDb.Market_Horario_Suporte_Excecao
            .FirstOrDefaultAsync(e => e.id_excecao_suporte == id_excecao_suporte && e.id_market == id_market);

        if (row == null) return NotFound();

        _marketDb.Market_Horario_Suporte_Excecao.Remove(row);
        await _marketDb.SaveChangesAsync();

        return await ExcecoesPartial(id_market);
    }
}
