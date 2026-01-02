using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using AutoMarket.Data;
using AutoMarket.Services;
using AutoMarket.Models.Market;
using System.Text.Json;

namespace AutoMarket.Controllers;

[Authorize]
public class VisitasController : Controller
{
    private readonly MarketplaceDbContext _marketDb;
    private readonly CurrentUserService _currentUser;

    public VisitasController(MarketplaceDbContext marketDb, CurrentUserService currentUser)
    {
        _marketDb = marketDb;
        _currentUser = currentUser;
    }

    private static bool IsAjaxRequest(HttpRequest request)
        => request.Headers["X-Requested-With"] == "XMLHttpRequest";

    // ============================================================
    // ✅ OPÇÃO B: helper centralizado para agendada -> realizada
    // ============================================================
    private async Task AtualizarRealizadasAsync(int? idVendedor = null, int? idComprador = null)
    {
        // Se for vendedor: atualizar apenas visitas dos anúncios desse vendedor
        if (idVendedor.HasValue)
        {
            await _marketDb.Database.ExecuteSqlRawAsync(@"
UPDATE market.Visita
SET estado = N'realizada'
WHERE estado = N'agendada'
  AND data_visita <= SYSUTCDATETIME()
  AND EXISTS (
      SELECT 1
      FROM market.Anuncio a
      WHERE a.id_anuncio = market.Visita.id_anuncio
        AND a.id_vendedor = {0}
  );", idVendedor.Value);

            return;
        }

        // Se for comprador: atualizar apenas visitas desse comprador
        if (idComprador.HasValue)
        {
            await _marketDb.Database.ExecuteSqlRawAsync(@"
UPDATE market.Visita
SET estado = N'realizada'
WHERE estado = N'agendada'
  AND data_visita <= SYSUTCDATETIME()
  AND id_utilizador = {0};", idComprador.Value);

            return;
        }

        // fallback (não deve ser usado no teu fluxo, mas fica seguro)
        await _marketDb.Database.ExecuteSqlRawAsync(@"
UPDATE market.Visita
SET estado = N'realizada'
WHERE estado = N'agendada'
  AND data_visita <= SYSUTCDATETIME();");
    }

    // ============================================================
    // HELPERS PARA NOTIFICAÇÕES
    // ============================================================
    private static string NormalizarCanalEmailSms(string? canalPreferido)
    {
        var c = (canalPreferido ?? "").Trim().ToLowerInvariant();
        return (c == "sms") ? "sms" : "email";
    }

    private async Task CriarNotificacaoParaUtilizadorAsync(
        int idDestinatario,
        string tipo,
        string titulo,
        string texto,
        object payload)
    {
        var prefs = await _marketDb.Utilizador
            .AsNoTracking()
            .Where(u => u.id_utilizador == idDestinatario)
            .Select(u => new { u.notificar_novidades, u.canal_notificacao_preferido })
            .FirstOrDefaultAsync();

        if (prefs is null) return;

        var notif = new Notificacao
        {
            tipo = tipo,
            titulo = titulo,
            texto = texto,
            payload_json = JsonSerializer.Serialize(payload)
        };

        var canal = NormalizarCanalEmailSms(prefs.canal_notificacao_preferido);
        
        // ✅ só fica pendente quando é para EMAIL e o user aceitou receber novidades
        var estadoEnvio = (prefs.notificar_novidades && canal == "email")
            ? "pendente"
            : "enviada";

        var recebida = new Notificacao_Recebida
        {
            id_utilizador = idDestinatario,
            id_notificacaoNavigation = notif,
            data_envio = DateTime.UtcNow,
            canal = canal,
            estado = estadoEnvio
        };

        _marketDb.Notificacao_Recebida.Add(recebida);
    }

    private async Task CriarNotificacaoParaAmbosAsync(
        int idComprador,
        int idVendedor,
        string tipo,
        string tituloComprador,
        string textoComprador,
        string tituloVendedor,
        string textoVendedor,
        object payload)
    {
        await CriarNotificacaoParaUtilizadorAsync(idComprador, tipo, tituloComprador, textoComprador, payload);
        if (idVendedor != idComprador)
            await CriarNotificacaoParaUtilizadorAsync(idVendedor, tipo, tituloVendedor, textoVendedor, payload);
    }

    // ============================================================
    // COMPRADOR: abrir form (partial) para agendar visita
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> FormAgendar(int idAnuncio)
    {
        var idUtil = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtil == null) return Unauthorized();

        var isComprador = await _marketDb.Comprador.AnyAsync(c => c.id_utilizador == idUtil.Value);
        if (!isComprador) return Forbid();

        // ✅ manter estado realizado "em dia" mesmo no detalhe
        await AtualizarRealizadasAsync(idComprador: idUtil.Value);

        var anuncio = await _marketDb.Anuncio.AsNoTracking().FirstOrDefaultAsync(a => a.id_anuncio == idAnuncio);
        if (anuncio == null) return NotFound();

        if (anuncio.id_vendedor == idUtil.Value) return Forbid();

        if (string.Equals(anuncio.estado, "vendido", StringComparison.OrdinalIgnoreCase))
            return Forbid();

        ViewBag.IdAnuncio = idAnuncio;
        ViewBag.MinDate = DateTime.UtcNow.ToLocalTime().Date;
        ViewBag.MaxDate = DateTime.UtcNow.ToLocalTime().Date.AddDays(31);

        return PartialView("_AgendarVisita");
    }

    // ============================================================
    // COMPRADOR: submeter agendamento (pendente)
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Agendar(int idAnuncio, string Data, string Hora)
    {
        var idUtil = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtil == null) return Unauthorized();

        var isComprador = await _marketDb.Comprador.AnyAsync(c => c.id_utilizador == idUtil.Value);
        if (!isComprador) return Forbid();

        // ✅ manter estado realizado "em dia"
        await AtualizarRealizadasAsync(idComprador: idUtil.Value);

        var anuncio = await _marketDb.Anuncio.FirstOrDefaultAsync(a => a.id_anuncio == idAnuncio);
        if (anuncio == null) return NotFound();

        if (anuncio.id_vendedor == idUtil.Value) return Forbid();

        if (string.Equals(anuncio.estado, "vendido", StringComparison.OrdinalIgnoreCase))
            return Forbid();

        // para re-render do form
        ViewBag.IdAnuncio = idAnuncio;
        ViewBag.MinDate = DateTime.UtcNow.ToLocalTime().Date;
        ViewBag.MaxDate = DateTime.UtcNow.ToLocalTime().Date.AddDays(31);

        if (string.IsNullOrWhiteSpace(Data))
            ModelState.AddModelError("", "Escolhe a data da visita.");

        if (string.IsNullOrWhiteSpace(Hora))
            ModelState.AddModelError("", "Escolhe a hora da visita.");

        DateTime local = DateTime.MinValue;

        if (!DateTime.TryParse($"{Data} {Hora}", out local))
            ModelState.AddModelError("", "Data/hora inválida.");

        if (!ModelState.IsValid)
            return PartialView("_AgendarVisita");

        // normalizar (segundos=0) e manter Local
        local = new DateTime(local.Year, local.Month, local.Day, local.Hour, local.Minute, 0, DateTimeKind.Local);

        if (local.Minute % 5 != 0)
            ModelState.AddModelError("", "A visita tem de ser marcada de 5 em 5 minutos.");

        var nowLocal = DateTime.UtcNow.ToLocalTime();
        if (local < nowLocal)
            ModelState.AddModelError("", "A data da visita não pode estar no passado.");

        if (local > nowLocal.AddDays(31))
            ModelState.AddModelError("", "A visita só pode ser marcada até 31 dias para a frente.");

        if (!ModelState.IsValid)
            return PartialView("_AgendarVisita");

        var utc = local.ToUniversalTime();

        // bloquear apenas se existir visita AGENDADA exatamente igual para o mesmo anúncio
        var conflito = await _marketDb.Visita
            .AsNoTracking()
            .AnyAsync(v => v.id_anuncio == idAnuncio
                        && v.estado == "agendada"
                        && v.data_visita == utc);

        if (conflito)
        {
            ModelState.AddModelError("", "Já existe uma visita agendada exatamente para essa data e hora.");
            return PartialView("_AgendarVisita");
        }

        // utilizador só pode ter 1 visita em pendente OU agendada
        var jaTem = await _marketDb.Visita
            .AsNoTracking()
            .AnyAsync(v => v.id_utilizador == idUtil.Value
                       && (v.estado == "pendente" || v.estado == "agendada"));

        if (jaTem)
        {
            ModelState.AddModelError("", "Só podes ter uma visita pendente ou agendada de cada vez.");
            return PartialView("_AgendarVisita");
        }

        var visita = new Visita
        {
            id_anuncio = idAnuncio,
            id_utilizador = idUtil.Value,
            data_visita = utc,
            estado = "pendente"
        };

        _marketDb.Visita.Add(visita);

        try
        {
            await _marketDb.SaveChangesAsync();

            // ✅ NOTIFICAÇÃO 1: Vendedor recebe "Pedido de visita"
            var payload = new
            {
                v = 1,
                id_visita = visita.id_visita,
                id_anuncio = visita.id_anuncio,
                data_visita = visita.data_visita
            };

            await CriarNotificacaoParaUtilizadorAsync(
                anuncio.id_vendedor,
                "visita_pendente",
                "Novo pedido de visita",
                "Um comprador pediu uma visita ao teu anúncio.",
                payload
            );

            await _marketDb.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627))
        {
            ModelState.AddModelError("", "Só podes ter uma visita pendente ou agendada de cada vez.");
            return PartialView("_AgendarVisita");
        }

        ViewBag.IdAnuncio = idAnuncio;
        ViewBag.DataAgendada = local.ToString("dd/MM/yyyy HH:mm");
        return PartialView("_AgendarVisitaSucesso");
    }

    // ============================================================
    // VENDEDOR: view base (filtros + container). Lista vem por AJAX.
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> GestaoVendedor()
    {
        var idUtil = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtil == null) return Unauthorized();

        var isVendedor = await _marketDb.Vendedor.AnyAsync(v => v.id_utilizador == idUtil.Value);
        if (!isVendedor) return Forbid();

        // ✅ manter estado realizado "em dia"
        await AtualizarRealizadasAsync(idVendedor: idUtil.Value);

        return View();
    }

    // ============================================================
    // VENDEDOR: partial lista (AJAX) com filtros/ordenação
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> ListaVisitas(string estado = "pendente", string ordem = "data_proxima")
    {
        var idUtil = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtil == null) return Unauthorized();

        var isVendedor = await _marketDb.Vendedor.AnyAsync(v => v.id_utilizador == idUtil.Value);
        if (!isVendedor) return Forbid();

        // ✅ manter estado realizado "em dia"
        await AtualizarRealizadasAsync(idVendedor: idUtil.Value);

        IQueryable<Visita> q = _marketDb.Visita
            .AsNoTracking()
            .Include(v => v.id_anuncioNavigation)
                .ThenInclude(a => a.Anuncio_Imagem)
                    .ThenInclude(ai => ai.id_imagem_anuncioNavigation)
            .Where(v => v.id_anuncioNavigation.id_vendedor == idUtil.Value);

        if (!string.IsNullOrWhiteSpace(estado) && !estado.Equals("todos", StringComparison.OrdinalIgnoreCase))
            q = q.Where(v => v.estado == estado);

        q = ordem switch
        {
            "id_asc" => q.OrderBy(v => v.id_visita),
            "id_desc" => q.OrderByDescending(v => v.id_visita),
            "data_longe" => q.OrderByDescending(v => v.data_visita),
            _ => q.OrderBy(v => v.data_visita), // data_proxima
        };

        ViewBag.Estado = estado;
        ViewBag.Ordem = ordem;

        var lista = await q.ToListAsync();
        return PartialView("_ListaVisitas", lista);
    }

    // ============================================================
    // VENDEDOR: Aceitar (pendente -> agendada) + refresh partial
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Aceitar(long id, string estado, string ordem)
    {
        var idUtil = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtil == null) return Unauthorized();

        var isVendedor = await _marketDb.Vendedor.AnyAsync(v => v.id_utilizador == idUtil.Value);
        if (!isVendedor) return Forbid();

        // ✅ manter estado realizado "em dia"
        await AtualizarRealizadasAsync(idVendedor: idUtil.Value);

        var visita = await _marketDb.Visita
            .Include(v => v.id_anuncioNavigation)
            .FirstOrDefaultAsync(v => v.id_visita == id);

        if (visita == null) return NotFound();
        if (visita.id_anuncioNavigation.id_vendedor != idUtil.Value) return Forbid();
        if (visita.estado != "pendente") return BadRequest();

        var conflito = await _marketDb.Visita.AnyAsync(v =>
            v.id_anuncio == visita.id_anuncio &&
            v.estado == "agendada" &&
            v.data_visita == visita.data_visita);

        if (!conflito)
        {
            visita.estado = "agendada";
            try
            {
                await _marketDb.SaveChangesAsync();

                // ✅ NOTIFICAÇÃO 2: Comprador recebe "Visita agendada"
                var payload = new
                {
                    v = 1,
                    id_visita = visita.id_visita,
                    id_anuncio = visita.id_anuncio,
                    data_visita = visita.data_visita
                };

                await CriarNotificacaoParaUtilizadorAsync(
                    visita.id_utilizador,
                    "visita_agendada",
                    "Visita agendada",
                    "O vendedor aceitou o teu pedido e agendou a visita.",
                    payload
                );

                await _marketDb.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627))
            {
                // aqui podes meter TempData se quiseres feedback
            }
        }

        if (IsAjaxRequest(Request))
            return await ListaVisitas(estado, ordem);

        return RedirectToAction(nameof(GestaoVendedor));
    }

    // ============================================================
    // VENDEDOR: Rejeitar (pendente -> rejeitada) + refresh partial
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rejeitar(long id, string estado, string ordem)
    {
        var idUtil = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtil == null) return Unauthorized();

        var isVendedor = await _marketDb.Vendedor.AnyAsync(v => v.id_utilizador == idUtil.Value);
        if (!isVendedor) return Forbid();

        await AtualizarRealizadasAsync(idVendedor: idUtil.Value);

        var visita = await _marketDb.Visita
            .Include(v => v.id_anuncioNavigation)
            .FirstOrDefaultAsync(v => v.id_visita == id);

        if (visita == null) return NotFound();
        if (visita.id_anuncioNavigation.id_vendedor != idUtil.Value) return Forbid();
        if (visita.estado != "pendente") return BadRequest();

        visita.estado = "rejeitada";
        await _marketDb.SaveChangesAsync();

        // ✅ NOTIFICAÇÃO 2: Comprador recebe "Visita rejeitada"
        var payload = new
        {
            v = 1,
            id_visita = visita.id_visita,
            id_anuncio = visita.id_anuncio,
            data_visita = visita.data_visita
        };

        await CriarNotificacaoParaUtilizadorAsync(
            visita.id_utilizador,
            "visita_rejeitada",
            "Visita rejeitada",
            "O vendedor rejeitou o teu pedido de visita.",
            payload
        );

        await _marketDb.SaveChangesAsync();

        if (IsAjaxRequest(Request))
            return await ListaVisitas(estado, ordem);

        return RedirectToAction(nameof(GestaoVendedor));
    }

    // ============================================================
    // VENDEDOR: Cancelar (agendada -> cancelada) + refresh partial
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelarVendedor(long id, string estado, string ordem)
    {
        var idUtil = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtil == null) return Unauthorized();

        var isVendedor = await _marketDb.Vendedor.AnyAsync(v => v.id_utilizador == idUtil.Value);
        if (!isVendedor) return Forbid();

        await AtualizarRealizadasAsync(idVendedor: idUtil.Value);

        var visita = await _marketDb.Visita
            .Include(v => v.id_anuncioNavigation)
            .FirstOrDefaultAsync(v => v.id_visita == id);

        if (visita == null) return NotFound();
        if (visita.id_anuncioNavigation.id_vendedor != idUtil.Value) return Forbid();

        if (visita.estado == "realizada") return BadRequest();
        if (visita.estado != "agendada") return BadRequest();

        visita.estado = "cancelada";
        await _marketDb.SaveChangesAsync();

        // ✅ NOTIFICAÇÃO 3: Ambos recebem "Visita cancelada"
        var payload = new
        {
            v = 1,
            id_visita = visita.id_visita,
            id_anuncio = visita.id_anuncio,
            data_visita = visita.data_visita
        };

        await CriarNotificacaoParaAmbosAsync(
            idComprador: visita.id_utilizador,
            idVendedor: visita.id_anuncioNavigation.id_vendedor,
            tipo: "visita_cancelada",
            tituloComprador: "Visita cancelada",
            textoComprador: "A visita foi cancelada pelo vendedor.",
            tituloVendedor: "Visita cancelada",
            textoVendedor: "Cancelaste a visita.",
            payload: payload
        );

        await _marketDb.SaveChangesAsync();

        if (IsAjaxRequest(Request))
            return await ListaVisitas(estado, ordem);

        return RedirectToAction(nameof(GestaoVendedor));
    }

    // ============================================================
    // COMPRADOR: lista AJAX das minhas visitas (MinhasOperacoes)
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> ListaMinhasVisitas(string estado = "todos", string ordem = "data_proxima")
    {
        var idUtil = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtil == null) return Unauthorized();

        var isComprador = await _marketDb.Comprador.AnyAsync(c => c.id_utilizador == idUtil.Value);
        if (!isComprador) return Forbid();

        await AtualizarRealizadasAsync(idComprador: idUtil.Value);

        IQueryable<Visita> q = _marketDb.Visita
            .AsNoTracking()
            .Include(v => v.id_anuncioNavigation)
                .ThenInclude(a => a.Anuncio_Imagem)
                    .ThenInclude(ai => ai.id_imagem_anuncioNavigation)
            .Where(v => v.id_utilizador == idUtil.Value);

        if (!string.IsNullOrWhiteSpace(estado) && !estado.Equals("todos", StringComparison.OrdinalIgnoreCase))
            q = q.Where(v => v.estado == estado);

        q = ordem switch
        {
            "id_asc" => q.OrderBy(v => v.id_visita),
            "id_desc" => q.OrderByDescending(v => v.id_visita),
            "data_longe" => q.OrderByDescending(v => v.data_visita),
            _ => q.OrderBy(v => v.data_visita), // data_proxima
        };

        ViewBag.Estado = estado;
        ViewBag.Ordem = ordem;

        var lista = await q.ToListAsync();
        return PartialView("_ListaMinhasVisitas", lista);
    }

    // ============================================================
    // COMPRADOR: cancelar visita (qualquer estado exceto realizada)
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelarComprador(long id, string estado, string ordem)
    {
        var idUtil = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtil == null) return Unauthorized();

        var isComprador = await _marketDb.Comprador.AnyAsync(c => c.id_utilizador == idUtil.Value);
        if (!isComprador) return Forbid();

        await AtualizarRealizadasAsync(idComprador: idUtil.Value);

        var visita = await _marketDb.Visita
            .Include(v => v.id_anuncioNavigation)
            .FirstOrDefaultAsync(v => v.id_visita == id);

        if (visita == null) return NotFound();

        if (visita.id_utilizador != idUtil.Value) return Forbid();
        if (visita.estado == "realizada") return BadRequest();

        if (visita.estado != "cancelada")
        {
            visita.estado = "cancelada";
            await _marketDb.SaveChangesAsync();

            // ✅ NOTIFICAÇÃO 3: Ambos recebem "Visita cancelada"
            var payload = new
            {
                v = 1,
                id_visita = visita.id_visita,
                id_anuncio = visita.id_anuncio,
                data_visita = visita.data_visita
            };

            await CriarNotificacaoParaAmbosAsync(
                idComprador: visita.id_utilizador,
                idVendedor: visita.id_anuncioNavigation.id_vendedor,
                tipo: "visita_cancelada",
                tituloComprador: "Visita cancelada",
                textoComprador: "Cancelaste a visita.",
                tituloVendedor: "Visita cancelada",
                textoVendedor: "A visita foi cancelada pelo comprador.",
                payload: payload
            );

            await _marketDb.SaveChangesAsync();
        }

        if (IsAjaxRequest(Request))
            return await ListaMinhasVisitas(estado, ordem);

        return RedirectToAction("MinhasOperacoes", "Anuncios");
    }
}
