using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AutoMarket.Data;
using AutoMarket.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Controllers;

[Authorize]
public class AdminEstatisticasController : Controller
{
    private readonly MarketplaceDbContext _marketDb;
    private readonly CurrentUserService _currentUser;

    public AdminEstatisticasController(MarketplaceDbContext marketDb, CurrentUserService currentUser)
    {
        _marketDb = marketDb;
        _currentUser = currentUser;
    }


    [HttpGet]
    public async Task<IActionResult> Index(string periodo = "30d", DateTime? dataInicio = null, DateTime? dataFim = null)
    {
        // Apenas admins
        if (!await _currentUser.IsAdminAsync(User))
            return Forbid();

        periodo = (periodo ?? "30d").Trim().ToLowerInvariant();

        var hoje = DateTime.UtcNow.Date;  // limite "hoje"
        DateTime? start = null;           // data mínima (inclusive)
        DateTime? end = null;             // data máxima (inclusive) — vamos converter para exclusivo depois

        // ✅ Presets + intervalo personalizado
        switch (periodo)
        {
            case "1d":
                start = DateTime.UtcNow.AddDays(-1);
                end = hoje;
                break;

            case "7d":
                start = DateTime.UtcNow.AddDays(-7);
                end = hoje;
                break;

            case "30d":
                start = DateTime.UtcNow.AddDays(-30);
                end = hoje;
                break;

            case "3m":
                start = DateTime.UtcNow.AddMonths(-3);
                end = hoje;
                break;

            case "6m":
                start = DateTime.UtcNow.AddMonths(-6);
                end = hoje;
                break;

            case "12m":
                start = DateTime.UtcNow.AddMonths(-12);
                end = hoje;
                break;

            case "all":
                start = null;
                end = null;
                break;

            case "custom":
            {
                // ✅ Validação 1: Ambas as datas têm de estar preenchidas
                if (!dataInicio.HasValue || !dataFim.HasValue)
                    return ReturnCustomError("Para usar um intervalo personalizado, seleciona Data início e Data fim.");

                var di = dataInicio.Value.Date;
                var df = dataFim.Value.Date;

                // ✅ Validação 2: Data início < Data fim (não podem ser iguais)
                if (di >= df)
                    return ReturnCustomError("A Data início tem de ser anterior à Data fim (não podem ser iguais).", di, df);

                // ✅ Validação 3: Nenhuma data pode ultrapassar hoje
                if (di > hoje || df > hoje)
                    return ReturnCustomError("As datas do intervalo personalizado não podem ultrapassar o dia de hoje.", di, df);

                // ✅ Válido: prosseguir
                start = di;
                end = df;

                ViewBag.CustomStart = di.ToString("yyyy-MM-dd");
                ViewBag.CustomEnd = df.ToString("yyyy-MM-dd");
                break;
            }

            default:
                start = DateTime.UtcNow.AddDays(-30);
                end = hoje;
                periodo = "30d";
                break;
        }

        // ✅ Converter end (inclusive) para endExclusive (mais fácil de filtrar)
        DateTime? endExclusive = end.HasValue ? end.Value.Date.AddDays(1) : null;

        // ✅ Para a view
        ViewBag.Periodo = periodo;
        ViewBag.Start = start;
        ViewBag.End = end;
        ViewBag.Today = hoje.ToString("yyyy-MM-dd");

        // =========================
        // CONTAGENS “BASE”
        // =========================
        var totalUtilizadores = await _marketDb.Utilizador.AsNoTracking().CountAsync();
        var totalAdmins = await _marketDb.Administrador.AsNoTracking().CountAsync();
        var totalCompradores = await _marketDb.Comprador.AsNoTracking().CountAsync();
        var totalVendedores = await _marketDb.Vendedor.AsNoTracking().CountAsync();

        // Anúncios por estado
        var anunciosEstadoRaw = await _marketDb.Anuncio.AsNoTracking()
            .GroupBy(a => a.estado)
            .Select(g => new { Estado = g.Key, Total = g.Count() })
            .ToListAsync();

        var anunciosPorEstado = anunciosEstadoRaw
            .ToDictionary(x => x.Estado ?? "(sem estado)", x => x.Total, StringComparer.OrdinalIgnoreCase);

        // Extras interessantes: quantos em destaque + preço médio ativos
        var anunciosDestaqueAtivos = await _marketDb.Anuncio.AsNoTracking()
            .CountAsync(a => a.estado == "ativo" && a.destaque); // existe na tabela Anuncio:contentReference[oaicite:4]{index=4}

        decimal? precoMedioAtivos = null;
        var qPrecoAtivos = _marketDb.Anuncio.AsNoTracking()
            .Where(a => a.estado == "ativo" && a.preco.HasValue);

        if (await qPrecoAtivos.AnyAsync())
            precoMedioAtivos = await qPrecoAtivos.AverageAsync(a => a.preco!.Value);

        // =========================
        // VENDAS / ENCOMENDAS
        // =========================
        var encomendasEstadoRaw = await _marketDb.Encomenda.AsNoTracking()
            .GroupBy(e => e.estado)
            .Select(g => new { Estado = g.Key, Total = g.Count() })
            .ToListAsync();

        var encomendasPorEstado = encomendasEstadoRaw
            .ToDictionary(x => x.Estado ?? "(sem estado)", x => x.Total, StringComparer.OrdinalIgnoreCase);

        // ✅ Filtro por período (start/endExclusive)
        var encomendasPeriodo = _marketDb.Encomenda.AsNoTracking();

        if (start.HasValue)
            encomendasPeriodo = encomendasPeriodo.Where(e => e.data_encomenda >= start.Value);

        if (endExclusive.HasValue)
            encomendasPeriodo = encomendasPeriodo.Where(e => e.data_encomenda < endExclusive.Value);

        var vendasPagasPeriodo = encomendasPeriodo.Where(e => e.estado == "pago");

        // Receita aproximada (somatório do preço do anúncio associado)
        // (No teu modelo Encomenda liga a Anuncio):contentReference[oaicite:6]{index=6}
        decimal receitaPeriodo = await vendasPagasPeriodo
            .SumAsync(e => e.id_anuncioNavigation.preco ?? 0m);

        int totalVendasPeriodo = await vendasPagasPeriodo.CountAsync();

        // Série "vendas por período"
        // - 1d/7d/30d => por dia
        // - 3m/6m/12m => por mês
        // - all => por dia (ou por mês, à tua escolha)
        // - custom => depende do intervalo (se > 60 dias, usa mês)
        var vendasSerie = new List<KeyValuePair<string, int>>();

        bool porMes = periodo == "3m" || periodo == "6m" || periodo == "12m";

        // ✅ Modo custom: se intervalo > 60 dias, agrupa por mês
        if (periodo == "custom" && start.HasValue && end.HasValue)
        {
            var diff = (end.Value - start.Value).TotalDays;
            porMes = diff > 60;
        }

        if (porMes)
        {
            // ✅ Agrupar por mês
            var raw = await vendasPagasPeriodo
                .GroupBy(e => new { e.data_encomenda.Year, e.data_encomenda.Month })
                .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Count() })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToListAsync();

            vendasSerie = raw
                .Select(x => new KeyValuePair<string, int>($"{x.Month:D2}/{x.Year}", x.Total))
                .ToList();
        }
        else
        {
            // ✅ Agrupar por dia
            var raw = await vendasPagasPeriodo
                .GroupBy(e => e.data_encomenda.Date)
                .Select(g => new { Dia = g.Key, Total = g.Count() })
                .OrderBy(x => x.Dia)
                .ToListAsync();

            vendasSerie = raw
                .Select(x => new KeyValuePair<string, int>(x.Dia.ToString("dd/MM/yyyy"), x.Total))
                .ToList();
        }

        // =========================
        // TOP MARCAS / MODELOS (por vendas pagas)
        // =========================
        var topMarcasRaw = await vendasPagasPeriodo
            .Select(e =>
                e.id_anuncioNavigation.id_veiculoNavigation
                    .id_sub_modeloNavigation.id_modeloNavigation
                    .id_marcaNavigation.nome)
            .GroupBy(nome => nome)
            .Select(g => new { Nome = g.Key, Total = g.Count() })
            .OrderByDescending(x => x.Total)
            .Take(10)
            .ToListAsync();

        var topMarcas = topMarcasRaw
            .Select(x => new KeyValuePair<string, int>(x.Nome, x.Total))
            .ToList();

        var topModelosRaw = await vendasPagasPeriodo
            .Select(e => new
            {
                Marca = e.id_anuncioNavigation.id_veiculoNavigation
                    .id_sub_modeloNavigation.id_modeloNavigation
                    .id_marcaNavigation.nome,
                Modelo = e.id_anuncioNavigation.id_veiculoNavigation
                    .id_sub_modeloNavigation.id_modeloNavigation.nome
            })
            .GroupBy(x => (x.Marca + " " + x.Modelo))
            .Select(g => new { Nome = g.Key, Total = g.Count() })
            .OrderByDescending(x => x.Total)
            .Take(10)
            .ToListAsync();

        var topModelos = topModelosRaw
            .Select(x => new KeyValuePair<string, int>(x.Nome, x.Total))
            .ToList();

        // =========================
        // RESERVAS (ativa/cancelada/expirada na BD):contentReference[oaicite:7]{index=7}
        // =========================
        var reservasEstadoRaw = await _marketDb.Reserva.AsNoTracking()
            .GroupBy(r => r.estado)
            .Select(g => new { Estado = g.Key, Total = g.Count() })
            .ToListAsync();

        var reservasPorEstado = reservasEstadoRaw
            .ToDictionary(x => x.Estado ?? "(sem estado)", x => x.Total, StringComparer.OrdinalIgnoreCase);

        // =========================
        // TEMPO MÉDIO ATÉ VENDA (dias)
        // (primeira encomenda paga por anúncio - data_publicacao)
        // =========================
        double? tempoMedioVendaDias = null;

        var primeiraVendaPorAnuncio = _marketDb.Encomenda.AsNoTracking()
            .Where(e => e.estado == "pago")
            .GroupBy(e => e.id_anuncio)
            .Select(g => new { id_anuncio = g.Key, data_venda = g.Min(x => x.data_encomenda) });

        var qTempo = primeiraVendaPorAnuncio
            .Join(_marketDb.Anuncio.AsNoTracking(),
                v => v.id_anuncio,
                a => a.id_anuncio,
                (v, a) => new { a.data_publicacao, v.data_venda })
            .Where(x => x.data_publicacao.HasValue);

        if (await qTempo.AnyAsync())
        {
            tempoMedioVendaDias = await qTempo
                .AverageAsync(x => EF.Functions.DateDiffDay(x.data_publicacao!.Value, x.data_venda));
        }

        // =========================
        // TOP VENDEDORES (por nº vendas pagas no período)
        // =========================
        var topVendedoresRaw = await vendasPagasPeriodo
            .GroupBy(e => e.id_anuncioNavigation.id_vendedor)
            .Select(g => new { id_vendedor = g.Key, Total = g.Count() })
            .OrderByDescending(x => x.Total)
            .Take(10)
            .ToListAsync();

        var topVendedores = await vendasPagasPeriodo
    .GroupBy(e => e.id_anuncioNavigation.id_vendedor)
    .Select(g => new { id_vendedor = g.Key, Total = g.Count() })
    .OrderByDescending(x => x.Total)
    .Take(10)
    .Join(_marketDb.Utilizador.AsNoTracking(),
        x => x.id_vendedor,
        u => u.id_utilizador,
        (x, u) => new KeyValuePair<string, int>(u.nome, x.Total))
    .ToListAsync();

        // =========================
        // TOP MARCAS FAVORITADAS (subscritas) — Comprador_Marca
        // =========================
        var qFav = _marketDb.Comprador_Marca.AsNoTracking();

        // ✅ Filtro por período (start/endExclusive)
        if (start.HasValue)
            qFav = qFav.Where(cm => cm.data_subscricao >= start.Value);

        if (endExclusive.HasValue)
            qFav = qFav.Where(cm => cm.data_subscricao < endExclusive.Value);

        var topMarcasFavoritas = await qFav
            .GroupBy(cm => cm.id_marca)
            .Select(g => new { id_marca = g.Key, Total = g.Count() })
            .OrderByDescending(x => x.Total)
            .Take(10)
            .Join(_marketDb.Marca.AsNoTracking(),
                x => x.id_marca,
                m => m.id_marca,
                (x, m) => new KeyValuePair<string, int>(m.nome, x.Total))
            .ToListAsync();

        ViewBag.TopMarcasFavoritas = topMarcasFavoritas;

        // =========================
        // VISITAS POR ANÚNCIO (ATIVOS): média
        // =========================
        var visitasValidas = _marketDb.Visita.AsNoTracking()
            .Where(v => v.estado == "agendada" || v.estado == "realizada");

        var visitasPorAnuncioAtivos = await _marketDb.Anuncio.AsNoTracking()
            .Where(a => a.estado == "ativo")
            .GroupJoin(
                visitasValidas,
                a => a.id_anuncio,
                v => v.id_anuncio,
                (a, vs) => new { a.id_anuncio, Count = vs.Count() }
            )
            .ToListAsync();

        double? mediaVisitasAtivos = null;
        if (visitasPorAnuncioAtivos.Count > 0)
            mediaVisitasAtivos = visitasPorAnuncioAtivos.Average(x => (double)x.Count);

        ViewBag.MediaVisitasPorAnuncioAtivos = mediaVisitasAtivos;

        // =========================
        // TOP LOCALIZAÇÕES (ANÚNCIOS ATIVOS)
        // =========================
        var topLocalizacoesAnuncios = await _marketDb.Anuncio.AsNoTracking()
            .Where(a => a.estado == "ativo" && a.localizacao != null && a.localizacao.Trim() != "")
            .Select(a => a.localizacao!.Trim())
            .GroupBy(loc => loc)
            .Select(g => new { Localizacao = g.Key, Total = g.Count() })
            .OrderByDescending(x => x.Total)
            .Take(10)
            .Select(x => new KeyValuePair<string, int>(x.Localizacao, x.Total))
            .ToListAsync();

        ViewBag.TopLocalizacoesAnuncios = topLocalizacoesAnuncios;

        // =========================
        // TOP LOCALIZAÇÕES (VENDAS PAGAS NO PERÍODO)
        // (reusa vendasPagasPeriodo que já tens)
        // =========================
        var topLocalizacoesVendas = await vendasPagasPeriodo
            .Select(e => e.id_anuncioNavigation.localizacao)
            .Where(loc => loc != null && loc.Trim() != "")
            .Select(loc => loc!.Trim())
            .GroupBy(loc => loc)
            .Select(g => new { Localizacao = g.Key, Total = g.Count() })
            .OrderByDescending(x => x.Total)
            .Take(10)
            .Select(x => new KeyValuePair<string, int>(x.Localizacao, x.Total))
            .ToListAsync();

        ViewBag.TopLocalizacoesVendas = topLocalizacoesVendas;

        // =========================
        // PERFIL DO STOCK (VEÍCULOS) — baseado em anúncios ATIVOS
        // =========================
        var veiculosEmStock = _marketDb.Veiculo.AsNoTracking()
            .Where(v => _marketDb.Anuncio.AsNoTracking()
                .Any(a => a.estado == "ativo" && a.id_veiculo == v.id_veiculo));

        // -------------------------
        // Distribuição por ANO: top anos + média + min/max
        // -------------------------
        short? anoMin = null;
        short? anoMax = null;
        double? anoMedio = null;

        var anoAgg = await veiculosEmStock
            .Where(v => v.ano.HasValue)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Min = g.Min(x => x.ano),
                Max = g.Max(x => x.ano),
                Avg = g.Average(x => (double)x.ano!.Value)
            })
            .FirstOrDefaultAsync();

        if (anoAgg != null)
        {
            anoMin = anoAgg.Min;
            anoMax = anoAgg.Max;
            anoMedio = anoAgg.Avg;
        }

        var topAnosStock = await veiculosEmStock
            .Where(v => v.ano.HasValue)
            .GroupBy(v => v.ano!.Value)
            .Select(g => new { Ano = g.Key, Total = g.Count() })
            .OrderByDescending(x => x.Total)
            .ThenByDescending(x => x.Ano)
            .Take(10)
            .Select(x => new KeyValuePair<string, int>(x.Ano.ToString(), x.Total))
            .ToListAsync();

        ViewBag.StockAnoMin = anoMin;
        ViewBag.StockAnoMax = anoMax;
        ViewBag.StockAnoMedio = anoMedio;
        ViewBag.StockTopAnos = topAnosStock;

        // -------------------------
        // Distribuição por QUILOMETRAGEM (faixas)
        // -------------------------
        var kmRaw = await veiculosEmStock
            .Select(v => new
            {
                Bucket = v.quilometragem == null ? "Sem dados" :
                         v.quilometragem < 50000 ? "0–50k" :
                         v.quilometragem < 100000 ? "50–100k" :
                         v.quilometragem < 150000 ? "100–150k" :
                         v.quilometragem < 200000 ? "150–200k" :
                         "200k+"
            })
            .GroupBy(x => x.Bucket)
            .Select(g => new { Bucket = g.Key, Total = g.Count() })
            .ToListAsync();

        // ordenar em memória por uma ordem fixa
        var ordem = new Dictionary<string, int>
        {
            ["0–50k"] = 1,
            ["50–100k"] = 2,
            ["100–150k"] = 3,
            ["150–200k"] = 4,
            ["200k+"] = 5,
            ["Sem dados"] = 99
        };

        var kmDistribuicao = kmRaw
            .OrderBy(x => ordem.TryGetValue(x.Bucket, out var o) ? o : 50)
            .Select(x => new KeyValuePair<string, int>(x.Bucket, x.Total))
            .ToList();

        ViewBag.StockKmDistribuicao = kmDistribuicao;

        // -------------------------
        // Cores mais comuns (Top 10)
        // -------------------------
        var topCores = await veiculosEmStock
            .Select(v => v.cor)
            .Where(c => c != null && c.Trim() != "")
            .Select(c => c!.Trim())
            .GroupBy(c => c)
            .Select(g => new { Cor = g.Key, Total = g.Count() })
            .OrderByDescending(x => x.Total)
            .Take(10)
            .Select(x => new KeyValuePair<string, int>(x.Cor, x.Total))
            .ToListAsync();

        ViewBag.StockTopCores = topCores;

        // =========================
        // VIEWBAG (sem ViewModels)
        // =========================
        ViewBag.Periodo = periodo;
        ViewBag.TotalUtilizadores = totalUtilizadores;
        ViewBag.TotalAdmins = totalAdmins;
        ViewBag.TotalCompradores = totalCompradores;
        ViewBag.TotalVendedores = totalVendedores;

        ViewBag.AnunciosPorEstado = anunciosPorEstado;
        ViewBag.AnunciosDestaqueAtivos = anunciosDestaqueAtivos;
        ViewBag.PrecoMedioAtivos = precoMedioAtivos;

        ViewBag.EncomendasPorEstado = encomendasPorEstado;
        ViewBag.TotalVendasPeriodo = totalVendasPeriodo;
        ViewBag.ReceitaPeriodo = receitaPeriodo;
        ViewBag.VendasSerie = vendasSerie;

        ViewBag.TopMarcas = topMarcas;
        ViewBag.TopModelos = topModelos;
        ViewBag.ReservasPorEstado = reservasPorEstado;

        ViewBag.TempoMedioVendaDias = tempoMedioVendaDias;
        ViewBag.TopVendedores = topVendedores;
        ViewBag.TopMarcasFavoritas = topMarcasFavoritas;

        // =========================
        // NOVOS UTILIZADORES (por período) — usa Utilizador.data_criacao
        // =========================
        var novosUtilizadoresQ = _marketDb.Utilizador.AsNoTracking();

        if (start.HasValue)
            novosUtilizadoresQ = novosUtilizadoresQ.Where(u => u.data_criacao >= start.Value);

        if (endExclusive.HasValue)
            novosUtilizadoresQ = novosUtilizadoresQ.Where(u => u.data_criacao < endExclusive.Value);

        var novosUtilizadores = await novosUtilizadoresQ.CountAsync();
        var novosCompradores = await novosUtilizadoresQ.CountAsync(u => u.Comprador != null);
        var novosVendedores = await novosUtilizadoresQ.CountAsync(u => u.Vendedor != null);
        var novosAdmins = await novosUtilizadoresQ.CountAsync(u => u.Administrador != null);

        ViewBag.NovosUtilizadores = novosUtilizadores;
        ViewBag.NovosCompradores = novosCompradores;
        ViewBag.NovosVendedores = novosVendedores;
        ViewBag.NovosAdmins = novosAdmins;

        // =========================
        // BLOQUEADOS (total + por tipo)
        // =========================
        var bloqueadosQ = _marketDb.Utilizador.AsNoTracking()
            .Where(u => u.estado == "bloqueado");

        var totalBloqueados = await bloqueadosQ.CountAsync();
        var bloqueadosCompradores = await bloqueadosQ.CountAsync(u => u.Comprador != null);
        var bloqueadosVendedores = await bloqueadosQ.CountAsync(u => u.Vendedor != null);
        var bloqueadosAdmins = await bloqueadosQ.CountAsync(u => u.Administrador != null);

        ViewBag.TotalBloqueados = totalBloqueados;
        ViewBag.BloqueadosCompradores = bloqueadosCompradores;
        ViewBag.BloqueadosVendedores = bloqueadosVendedores;
        ViewBag.BloqueadosAdmins = bloqueadosAdmins;


        return View();
    }

    private IActionResult ReturnCustomError(string msg, DateTime? di = null, DateTime? df = null)
    {
        var hoje = DateTime.UtcNow.Date;

        ViewBag.ErroPeriodo = msg;

        ViewBag.Periodo = "custom";
        ViewBag.Today = hoje.ToString("yyyy-MM-dd");

        if (di.HasValue) ViewBag.CustomStart = di.Value.ToString("yyyy-MM-dd");
        if (df.HasValue) ViewBag.CustomEnd = df.Value.ToString("yyyy-MM-dd");

        ViewBag.Start = di;
        ViewBag.End = df;

        // ✅ NOVOS DEFAULTS
        ViewBag.NovosUtilizadores = 0;
        ViewBag.NovosCompradores = 0;
        ViewBag.NovosVendedores = 0;
        ViewBag.NovosAdmins = 0;

        ViewBag.BloqueadosCompradores = 0;
        ViewBag.BloqueadosVendedores = 0;
        ViewBag.BloqueadosAdmins = 0;

        // ... (resto do código — TotalBloqueados, TotalUtilizadores, etc.)
        ViewBag.TotalBloqueados = 0;
        ViewBag.TotalUtilizadores = 0;
        ViewBag.TotalAdmins = 0;
        ViewBag.TotalCompradores = 0;
        ViewBag.TotalVendedores = 0;

        ViewBag.AnunciosPorEstado = new Dictionary<string, int>();
        ViewBag.AnunciosDestaqueAtivos = 0;
        ViewBag.PrecoMedioAtivos = null;

        ViewBag.EncomendasPorEstado = new Dictionary<string, int>();
        ViewBag.TotalVendasPeriodo = 0;
        ViewBag.ReceitaPeriodo = 0m;
        ViewBag.VendasSerie = new List<KeyValuePair<string, int>>();

        ViewBag.TopMarcas = new List<KeyValuePair<string, int>>();
        ViewBag.TopModelos = new List<KeyValuePair<string, int>>();
        ViewBag.TopVendedores = new List<KeyValuePair<string, int>>();
        ViewBag.TopMarcasFavoritas = new List<KeyValuePair<string, int>>();

        ViewBag.ReservasPorEstado = new Dictionary<string, int>();
        ViewBag.TempoMedioVendaDias = null;

        ViewBag.MediaVisitasPorAnuncioAtivos = null;

        ViewBag.TopLocalizacoesAnuncios = new List<KeyValuePair<string, int>>();
        ViewBag.TopLocalizacoesVendas = new List<KeyValuePair<string, int>>();

        ViewBag.StockAnoMin = null;
        ViewBag.StockAnoMax = null;
        ViewBag.StockAnoMedio = null;
        ViewBag.StockTopAnos = new List<KeyValuePair<string, int>>();
        ViewBag.StockKmDistribuicao = new List<KeyValuePair<string, int>>();
        ViewBag.StockTopCores = new List<KeyValuePair<string, int>>();

        return View();
    }
}
