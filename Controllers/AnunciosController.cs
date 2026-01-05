using AutoMarket.Data;
using AutoMarket.Models.Market;
using AutoMarket.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Text.Json;
using System.Text.Json.Serialization;
using MailKit.Net.Smtp;
using MimeKit;


namespace AutoMarket.Controllers;

[Authorize]
public class AnunciosController : Controller
{
    private readonly MarketplaceDbContext _marketDb;
    private readonly CurrentUserService _currentUser;
    private readonly IWebHostEnvironment _env;

    public AnunciosController(
        MarketplaceDbContext marketDb,
        CurrentUserService currentUser,
        IWebHostEnvironment env)
    {
        _marketDb = marketDb;
        _currentUser = currentUser;
        _env = env;
    }

    // ========================================
    // GET: /Anuncios/Criar
    // ========================================
    [HttpGet]
    public async Task<IActionResult> Criar()
    {
        // Só vendedores podem criar anúncio
        var idUtilizador = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtilizador is null)
            return Unauthorized();

        var isVendedor = await _marketDb.Vendedor.AnyAsync(v => v.id_utilizador == idUtilizador.Value);
        if (!isVendedor)
            return Forbid();

        ViewBag.Marcas = await _marketDb.Marca
            .AsNoTracking()
            .OrderBy(m => m.nome)
            .ToListAsync();

        return View();
    }

    // ========================================
    // AJAX: GetModelos (Marca → Modelos)
    // ========================================
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetModelos(int idMarca)
    {
        var list = await _marketDb.Modelo
            .AsNoTracking()
            .Where(m => m.id_marca == idMarca)
            .OrderBy(m => m.nome)
            .Select(m => new { m.id_modelo, m.nome })
            .ToListAsync();

        return Json(list);
    }

    // ========================================
    // AJAX: GetSubModelos (Modelo → Sub_Modelos)
    // ========================================
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetSubModelos(int idModelo)
    {
        var list = await _marketDb.Sub_Modelo
            .AsNoTracking()
            .Where(s => s.id_modelo == idModelo)
            .OrderBy(s => s.nome)
            .Select(s => new { s.id_sub_modelo, s.nome })
            .ToListAsync();

        return Json(list);
    }

    // ========================================
    // AJAX: GetSpecs (Sub_Modelo → Especificações)
    // ========================================
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetSpecs(int idSubModelo)
    {
        var s = await _marketDb.Sub_Modelo
            .AsNoTracking()
            .Where(x => x.id_sub_modelo == idSubModelo)
            .Select(x => new
            {
                x.cilindrada,
                x.potencia_hp,
                x.combustivel,
                x.caixa,
                x.categoria
            })
            .FirstOrDefaultAsync();

        if (s == null)
            return NotFound();

        return Json(s);
    }

    //=========================================================================================
    // HELPER PARA O TITULO DA VIEW ANUNCIOS/CARROS
    //=========================================================================================
    private async Task<string> BuildCarrosTitleAsync(int? idMarca, int? idModelo, int? idSubModelo)
    {
        // 1) Sub-modelo -> "Fiat 500 1.0" (SEM "– Carros")
        if (idSubModelo.HasValue && idSubModelo.Value > 0)
        {
            var sub = await _marketDb.Sub_Modelo
                .AsNoTracking()
                .Where(s => s.id_sub_modelo == idSubModelo.Value)
                .Select(s => new
                {
                    Sub = s.nome,
                    Modelo = s.id_modeloNavigation.nome,
                    Marca = s.id_modeloNavigation.id_marcaNavigation.nome
                })
                .FirstOrDefaultAsync();

            if (sub != null)
                return $"{sub.Marca} {sub.Modelo} {sub.Sub}".Trim();
        }

        // 2) Modelo -> "Audi A4 – Carros"
        if (idModelo.HasValue && idModelo.Value > 0)
        {
            var mod = await _marketDb.Modelo
                .AsNoTracking()
                .Where(m => m.id_modelo == idModelo.Value)
                .Select(m => new { Modelo = m.nome, Marca = m.id_marcaNavigation.nome })
                .FirstOrDefaultAsync();

            if (mod != null)
                return $"{mod.Marca} {mod.Modelo} – Carros";
        }

        // 3) Marca -> "BMW – Carros"
        if (idMarca.HasValue && idMarca.Value > 0)
        {
            var marca = await _marketDb.Marca
                .AsNoTracking()
                .Where(x => x.id_marca == idMarca.Value)
                .Select(x => x.nome)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(marca))
                return $"{marca} – Carros";
        }

        // 4) Sem filtros
        return "Carros";
    }


    // ========================================
    // POST: /Anuncios/Criar
    // Regras:
    // - RASCUNHO: só exige marca/modelo/submodelo/título
    // - PUBLICAR: exige tudo + pelo menos 1 imagem
    // ========================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Criar(
        int id_marca,
        int id_modelo,
        int id_sub_modelo,
        short? ano,
        int? quilometragem,
        string? cor,
        string? titulo,
        string? descricao,
        string? localizacao,
        decimal? preco,
        int prazo_reserva_dias,
        bool destaque,
        List<IFormFile>? imagens,
        string? imagensMeta,
        string submitAction)
    {
        var idUtilizador = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtilizador is null) return Unauthorized();

        var vendedor = await _marketDb.Vendedor
            .FirstOrDefaultAsync(v => v.id_utilizador == idUtilizador.Value);
        if (vendedor == null) return Forbid();

        // ✅ Normalizar submitAction
        submitAction = (submitAction ?? "publicar").Trim().ToLowerInvariant();
        bool isDraft = submitAction == "rascunho";
        bool isPublish = submitAction == "publicar";

        // ✅ Converter dias → horas
        int prazoHoras = 168;
        if (isPublish)
        {
            if (prazo_reserva_dias < 1 || prazo_reserva_dias > 7)
                ModelState.AddModelError("prazo_reserva_dias", "Escolhe entre 1 e 7 dias.");
            else
                prazoHoras = prazo_reserva_dias * 24;
        }

        // ✅ SEMPRE obrigatório
        if (id_marca <= 0)
            ModelState.AddModelError("id_marca", "Seleciona uma marca.");
        if (id_modelo <= 0)
            ModelState.AddModelError("id_modelo", "Seleciona um modelo.");
        if (id_sub_modelo <= 0)
            ModelState.AddModelError("id_sub_modelo", "Seleciona um sub-modelo.");
        if (string.IsNullOrWhiteSpace(titulo))
            ModelState.AddModelError("titulo", "Título obrigatório.");

        // ✅ Só obrigatório ao PUBLICAR
        if (isPublish)
        {
            if (string.IsNullOrWhiteSpace(localizacao))
                ModelState.AddModelError("localizacao", "Localização obrigatória.");

            if (!preco.HasValue || preco.Value <= 0)
                ModelState.AddModelError("preco", "Preço obrigatório.");

            if (string.IsNullOrWhiteSpace(cor))
                ModelState.AddModelError("cor", "Cor obrigatória.");

            if (!ano.HasValue || ano.Value < 1900 || ano.Value > 2100)
                ModelState.AddModelError("ano", "Ano obrigatório.");

            if (!quilometragem.HasValue || quilometragem.Value < 0)
                ModelState.AddModelError("quilometragem", "Quilometragem obrigatória.");

            if (imagens == null || imagens.Count == 0)
                ModelState.AddModelError("imagens", "Seleciona pelo menos 1 imagem.");
        }

        // Validar coerência Marca/Modelo/SubModelo
        var subInfo = await _marketDb.Sub_Modelo
            .Where(s => s.id_sub_modelo == id_sub_modelo)
            .Select(s => new { s.id_modelo, id_marca = s.id_modeloNavigation.id_marca })
            .FirstOrDefaultAsync();

        if (subInfo == null || subInfo.id_modelo != id_modelo || subInfo.id_marca != id_marca)
            ModelState.AddModelError(string.Empty, "Combinação Marca/Modelo/Sub-Modelo inválida.");

        if (!ModelState.IsValid)
        {
            ViewBag.Marcas = await _marketDb.Marca.AsNoTracking().OrderBy(m => m.nome).ToListAsync();
            return View();
        }

        await using var tx = await _marketDb.Database.BeginTransactionAsync();

        try
        {
            // 1) Criar Veículo
            var veiculo = new Veiculo
            {
                id_sub_modelo = id_sub_modelo,
                ano = ano,
                quilometragem = quilometragem,
                cor = cor?.Trim()
            };

            _marketDb.Veiculo.Add(veiculo);
            await _marketDb.SaveChangesAsync();

            // 2) Criar Anúncio
            var anuncio = new Anuncio
            {
                id_vendedor = vendedor.id_utilizador,
                id_veiculo = veiculo.id_veiculo,
                titulo = titulo!.Trim(),
                descricao = descricao,
                localizacao = localizacao?.Trim(),
                preco = preco,
                prazo_reserva = prazoHoras,
                destaque = destaque,
                estado = isDraft ? "rascunho" : "ativo",
                data_publicacao = isDraft ? (DateTime?)null : DateTime.UtcNow,
                data_atualizacao = DateTime.UtcNow
            };

            _marketDb.Anuncio.Add(anuncio);
            await _marketDb.SaveChangesAsync();

            // 3) ✅ Guardar imagens (só se existirem)
            if (imagens != null && imagens.Count > 0)
            {
                await GuardarImagensDoAnuncioAsync(anuncio.id_anuncio, imagens, imagensMeta);
            }

            // 4) Histórico (só ao publicar)
            if (isPublish)
            {
                _marketDb.Publica_Atualiza.Add(new Publica_Atualiza
                {
                    id_anuncio = anuncio.id_anuncio,
                    id_vendedor = vendedor.id_utilizador,
                    tipo_acao = "criar",
                    data_acao = DateTime.UtcNow,
                    data_publicacao = anuncio.data_publicacao
                });
                await _marketDb.SaveChangesAsync();

                // ✅ Notificação: anúncio ficou ativo (marca favoritada)
                await CriarNotificacaoMarcaFavoritaAsync(anuncio.id_anuncio);
            }

            // ✅ Commit da transação (imagens já guardadas)
            await tx.CommitAsync();
            return RedirectToAction(nameof(Detalhes), new { id = anuncio.id_anuncio });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            ModelState.AddModelError(string.Empty, $"Erro ao criar anúncio: {ex.Message}");
            ViewBag.Marcas = await _marketDb.Marca.AsNoTracking().OrderBy(m => m.nome).ToListAsync();
            return View();
        }
    }

    // ========================================
    // DTO para metadata
    // ========================================
    private sealed class ImgMetaDto
    {
        public int rotation { get; set; } // 0, 90, 180, 270
    }

    // ========================================
    // HELPER: Guardar imagens COM ROTAÇÃO REAL
    // ========================================
    private async Task GuardarImagensDoAnuncioAsync(
        int idAnuncio,
        List<IFormFile> imagens,
        string? imagensMeta)
    {
        // Pasta física: wwwroot/uploads/anuncios/{id}
        var root = Path.Combine(_env.WebRootPath, "uploads", "anuncios", idAnuncio.ToString());
        Directory.CreateDirectory(root);

        // ✅ Parse metadata (rotações na mesma ordem que input.files)
        List<ImgMetaDto> meta = new();
        if (!string.IsNullOrWhiteSpace(imagensMeta))
        {
            try
            {
                meta = JsonSerializer.Deserialize<List<ImgMetaDto>>(imagensMeta) ?? new();
            }
            catch
            {
                meta = new();
            }
        }

        for (int i = 0; i < imagens.Count; i++)
        {
            var file = imagens[i];
            if (file == null || file.Length == 0) continue;

            // Validação MIME
            if (!file.ContentType.StartsWith("image/")) continue;

            // Tamanho máx (5MB)
            if (file.Length > 5 * 1024 * 1024) continue;

            // ✅ Obter rotação (normalizar para 0, 90, 180, 270)
            int rotation = 0;
            if (i < meta.Count)
            {
                rotation = meta[i].rotation % 360;
                if (rotation < 0) rotation += 360;
                if (rotation % 90 != 0) rotation = 0;
            }

            // Nome seguro
            var safeBase = Guid.NewGuid().ToString("N");
            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";

            var safeName = safeBase + ext.ToLowerInvariant();
            var physicalPath = Path.Combine(root, safeName);

            // ✅ Guardar com rotação real (ImageSharp)
            await using (var inputStream = file.OpenReadStream())
            {
                try
                {
                    using var image = await Image.LoadAsync(inputStream);

                    if (rotation != 0)
                        image.Mutate(ctx => ctx.Rotate(rotation));

                    await image.SaveAsync(physicalPath);
                }
                catch
                {
                    inputStream.Position = 0;
                    await using var fs = new FileStream(physicalPath, FileMode.Create);
                    await inputStream.CopyToAsync(fs);
                }
            }

            var url = $"/uploads/anuncios/{idAnuncio}/{safeName}";

            // 1) Imagem_Anuncio
            var img = new Imagem_Anuncio
            {
                url = url,
                legenda = null
            };

            _marketDb.Imagem_Anuncio.Add(img);
            await _marketDb.SaveChangesAsync();

            // 2) Anuncio_Imagem (ordem + principal)
            var ordem = i + 1;
            var link = new Anuncio_Imagem
            {
                id_anuncio = idAnuncio,
                id_imagem_anuncio = img.id_imagem_anuncio,
                data_adicao = DateTime.UtcNow,
                ordem = ordem <= 1000 ? ordem : (int?)null,
                principal = (i == 0)
            };

            _marketDb.Anuncio_Imagem.Add(link);
        }

        await _marketDb.SaveChangesAsync();
    }


    // ========================================
    // GET: /Anuncios/Meus
    // Página "shell" (a lista vem por AJAX em _MeusLista)
    // ========================================
    [HttpGet]
    public async Task<IActionResult> Meus(string estado = "ativo", string? q = null, string sort = "recent")
    {
        var idUtilizador = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtilizador is null) return Unauthorized();

        var isVendedor = await _marketDb.Vendedor.AnyAsync(v => v.id_utilizador == idUtilizador.Value);
        if (!isVendedor) return Forbid();

        // contagens para tabs
        var counts = await _marketDb.Anuncio
            .AsNoTracking()
            .Where(a => a.id_vendedor == idUtilizador.Value)
            .GroupBy(a => a.estado)
            .Select(g => new { estado = g.Key, total = g.Count() })
            .ToListAsync();

        int Count(string st) => counts.FirstOrDefault(x => x.estado == st)?.total ?? 0;

        ViewBag.CountAtivo = Count("ativo");
        ViewBag.CountInativo = Count("inativo");
        ViewBag.CountRascunho = Count("rascunho");
        ViewBag.CountReservado = Count("reservado");
        ViewBag.CountVendido = Count("vendido");
        ViewBag.CountTodos = counts.Sum(x => x.total);

        // já tinhas isto:
        ViewBag.Estado = (estado ?? "ativo").Trim().ToLowerInvariant();
        ViewBag.Q = q ?? "";
        ViewBag.Sort = sort ?? "recent";

        return View();
    }


    // ========================================
    // GET (AJAX): /Anuncios/_MeusLista
    // ========================================
    [HttpGet]
    public async Task<IActionResult> _MeusLista(string estado = "ativo", string? q = null, string sort = "recent")
    {
        var idUtilizador = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtilizador is null) return Unauthorized();

        // (opcional mas recomendado) reafirma vendedor também aqui
        var isVendedor = await _marketDb.Vendedor.AnyAsync(v => v.id_utilizador == idUtilizador.Value);
        if (!isVendedor) return Forbid();

        estado = (estado ?? "ativo").Trim().ToLowerInvariant();
        q = (q ?? "").Trim();
        sort = (sort ?? "recent").Trim().ToLowerInvariant();

        // normaliza estado
        if (estado is not ("ativo" or "inativo" or "rascunho" or "reservado" or "vendido" or "todos"))
            estado = "ativo";

        var query = _marketDb.Anuncio
            .AsNoTracking()
            .Where(a => a.id_vendedor == idUtilizador.Value)
            .Include(a => a.Anuncio_Imagem)
                .ThenInclude(ai => ai.id_imagem_anuncioNavigation)
            .AsQueryable();

        // Só precisamos disto se for "reservado" / "vendido" / "todos"
        if (estado is "reservado" or "vendido" or "todos")
        {
            query = query
                .Include(a => a.Reserva)
                    .ThenInclude(r => r.id_utilizadorNavigation)
                        .ThenInclude(c => c.id_utilizadorNavigation)
                .Include(a => a.Encomenda)
                    .ThenInclude(e => e.id_utilizadorNavigation)
                        .ThenInclude(c => c.id_utilizadorNavigation);
        }


        if (estado != "todos")
            query = query.Where(a => a.estado == estado);

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(a => a.titulo != null && a.titulo.Contains(q));

        query = sort switch
        {
            "oldest" => query.OrderBy(a => a.data_publicacao ?? DateTime.MaxValue),
            "price_asc" => query.OrderBy(a => a.preco ?? decimal.MaxValue),
            "price_desc" => query.OrderByDescending(a => a.preco ?? 0m),
            _ => query.OrderByDescending(a => a.data_publicacao ?? DateTime.MinValue) // "recent"
        };

        var lista = await query.ToListAsync();

        // ✅ FORÇA o ficheiro do partial (evita apanhar outro _MeusLista.cshtml antigo)
        return PartialView("~/Views/Anuncios/_MeusLista.cshtml", lista);
    }




    // ========================================
    // GET: /Anuncios/Detalhes/5
    // Regras de acesso por estado:
    // - ativo/reservado → público
    // - rascunho/inativo → só vendedor + admin
    // - vendido → só vendedor + comprador + admin
    // ========================================
    [ExpireReservas]
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Detalhes(int id)
    {
        var anuncio = await _marketDb.Anuncio
            .AsNoTracking()
            .Include(a => a.Anuncio_Imagem)
                .ThenInclude(ai => ai.id_imagem_anuncioNavigation)
            .Include(a => a.id_veiculoNavigation)
                .ThenInclude(v => v.id_sub_modeloNavigation)
                    .ThenInclude(sm => sm.id_modeloNavigation)
                        .ThenInclude(m => m.id_marcaNavigation)
            .FirstOrDefaultAsync(a => a.id_anuncio == id);

        if (anuncio == null)
            return NotFound();

        var estado = (anuncio.estado ?? "").Trim().ToLowerInvariant();

        // ✅ Público: ativo e reservado
        if (estado is "ativo" or "reservado")
            return View(anuncio);

        // ✅ A partir daqui: privado (requer autenticação)
        if (!User.Identity?.IsAuthenticated ?? true)
            return Challenge(); // Força login

        var idUtilizador = await GetCurrentMarketUserIdAsync();
        if (idUtilizador is null)
            return Forbid();

        var isAdmin = await IsAdminAsync(idUtilizador.Value);
        var isOwner = anuncio.id_vendedor == idUtilizador.Value;

        // ✅ Rascunho/Inativo: só vendedor dono + admin
        if (estado is "rascunho" or "inativo")
        {
            if (isOwner || isAdmin)
                return View(anuncio);
            return Forbid();
        }

        // ✅ Vendido: só vendedor + comprador + admin
        if (estado == "vendido")
        {
            var isBuyer = await IsBuyerOfAnuncioAsync(idUtilizador.Value, anuncio.id_anuncio);
            if (isOwner || isAdmin || isBuyer)
                return View(anuncio);
            return Forbid();
        }

        // ✅ Qualquer outro estado não previsto: bloqueia por defeito
        return Forbid();
    }

    // ========================================
    // POST: Pausar (ativo → inativo)
    // ========================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pausar(int id)
    {
        var idUtilizador = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtilizador is null) return Unauthorized();

        var anuncio = await _marketDb.Anuncio
            .FirstOrDefaultAsync(a => a.id_anuncio == id && a.id_vendedor == idUtilizador.Value);
        if (anuncio == null) return NotFound();

        if (anuncio.estado == "ativo" || anuncio.estado == "reservado")
        {
            var agora = DateTime.UtcNow;

            // =========================================================
            // 0) ENCOMENDAS: permitir pausar só se TODAS forem "pendente"
            // =========================================================
            var encomendas = await _marketDb.Encomenda
                .Where(e => e.id_anuncio == id)
                .ToListAsync();

            if (encomendas.Any(e => !string.Equals(e.estado, "pendente", StringComparison.OrdinalIgnoreCase)))
            {
                TempData["Erro"] = "Não é possível pausar este anúncio porque existem encomendas que não estão no estado 'pendente'.";
                return RedirectToAction(nameof(Meus), new { estado = anuncio.estado });
            }

            var compradoresEncomendaPendente = encomendas
                .Where(e => string.Equals(e.estado, "pendente", StringComparison.OrdinalIgnoreCase))
                .Select(e => e.id_utilizador)
                .Distinct()
                .ToList();

            // ===== 1) Capturar afetados (reserva + visitas) ANTES de mexer em estados =====
            var reservaAtivaInfo = await _marketDb.Reserva
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.id_anuncio == id && r.estado == "ativa");

            var reservadores = (reservaAtivaInfo != null)
                ? new List<int> { reservaAtivaInfo.id_utilizador }
                : new List<int>();

            var visitantes = await _marketDb.Visita
                .AsNoTracking()
                .Where(v => v.id_anuncio == id && (v.estado == "pendente" || v.estado == "agendada"))
                .Select(v => v.id_utilizador)
                .Distinct()
                .ToListAsync();

            // ✅ Não removemos interseção: se tiver reserva+visita recebe 2 notif.

            // ===== 2) Cancelar reserva ativa =====
            if (reservaAtivaInfo != null)
            {
                var reservaTracked = await _marketDb.Reserva
                    .FirstOrDefaultAsync(r => r.id_reserva == reservaAtivaInfo.id_reserva);

                if (reservaTracked != null)
                    reservaTracked.estado = "cancelada";
            }

            // ===== 3) Cancelar visitas pendente/agendada =====
            var visitas = await _marketDb.Visita
                .Where(v => v.id_anuncio == id && (v.estado == "pendente" || v.estado == "agendada"))
                .ToListAsync();

            foreach (var v in visitas)
                v.estado = "cancelada";

            // ===== 4) Cancelar encomendas pendentes (set estado = "cancelado") =====
            foreach (var e in encomendas)
                e.estado = "cancelado"; // CHECK: (pendente|pago|cancelado)

            // ===== 5) Pausar anúncio =====
            anuncio.estado = "inativo";
            anuncio.data_atualizacao = agora;

            _marketDb.Publica_Atualiza.Add(new Publica_Atualiza
            {
                id_anuncio = anuncio.id_anuncio,
                id_vendedor = idUtilizador.Value,
                tipo_acao = "pausar",
                data_acao = agora
            });

            // ===== 6) Notificações (3 tipos de mensagem) =====
            var acao = "pausado";
            var tituloBase = string.IsNullOrWhiteSpace(anuncio.titulo) ? "Anúncio" : anuncio.titulo.Trim();

            // 6.1) Encomenda pendente cancelada
            if (compradoresEncomendaPendente.Count > 0)
            {
                var payloadEnc = new { v = 1, origem = "encomenda", acao = "pausar", id_anuncio = anuncio.id_anuncio };

                await CriarNotificacaoParaVariosAsync(
                    idsDestinatarios: compradoresEncomendaPendente,
                    idVendedor: anuncio.id_vendedor,
                    tipo: "encomenda_pendente_cancelada",
                    titulo: $"Anúncio {acao} — {tituloBase} (Encomenda pendente cancelada)",
                    texto: $"A sua encomenda pendente foi cancelada porque o anúncio foi {acao} pelo vendedor.",
                    payload: payloadEnc
                );
            }

            // 6.2) Reserva cancelada
            if (reservadores.Count > 0)
            {
                var payloadReserva = new { v = 1, origem = "pausar", id_anuncio = anuncio.id_anuncio, motivo = "reserva" };

                await CriarNotificacaoParaVariosAsync(
                    idsDestinatarios: reservadores,
                    idVendedor: anuncio.id_vendedor,
                    tipo: "anuncio_pausado",
                    titulo: $"Anúncio {acao} — {tituloBase} (Reserva cancelada)",
                    texto: $"A sua reserva foi cancelada porque o anúncio foi {acao} pelo vendedor.",
                    payload: payloadReserva
                );
            }

            // 6.3) Visita cancelada
            if (visitantes.Count > 0)
            {
                var payloadVisita = new { v = 1, origem = "pausar", id_anuncio = anuncio.id_anuncio, motivo = "visita" };

                await CriarNotificacaoParaVariosAsync(
                    idsDestinatarios: visitantes,
                    idVendedor: anuncio.id_vendedor,
                    tipo: "anuncio_pausado",
                    titulo: $"Anúncio {acao} — {tituloBase} (Visita cancelada)",
                    texto: $"A sua visita foi cancelada porque o anúncio foi {acao} pelo vendedor.",
                    payload: payloadVisita
                );
            }

            await _marketDb.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Meus), new { estado = "inativo" });
    }



    // ========================================
    // POST: Ativar (inativo/rascunho → ativo)
    // ========================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ativar(int id)
    {
        var idUtilizador = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtilizador is null) return Unauthorized();

        var anuncio = await _marketDb.Anuncio
            .FirstOrDefaultAsync(a => a.id_anuncio == id && a.id_vendedor == idUtilizador.Value);
        if (anuncio == null) return NotFound();

        if (anuncio.estado == "inativo" || anuncio.estado == "rascunho")
        {
            // ✅ Antes de ativar, este anúncio deve estar completo
            // (Se quiseres, podemos adicionar aqui validação de "completude" também.)
            anuncio.estado = "ativo";
            anuncio.data_atualizacao = DateTime.UtcNow;

            if (!anuncio.data_publicacao.HasValue)
                anuncio.data_publicacao = DateTime.UtcNow;

            _marketDb.Publica_Atualiza.Add(new Publica_Atualiza
            {
                id_anuncio = anuncio.id_anuncio,
                id_vendedor = idUtilizador.Value,
                tipo_acao = "editar", // não existe "ativar" no CHECK
                data_acao = DateTime.UtcNow,
                data_publicacao = anuncio.data_publicacao,
                data_atualizacao = anuncio.data_atualizacao
            });

            await _marketDb.SaveChangesAsync();

            await CriarNotificacaoMarcaFavoritaAsync(anuncio.id_anuncio);
        }

        return RedirectToAction(nameof(Meus), new { estado = "ativo" });
    }

    // ========================================
    // POST: Toggle Destaque
    // ========================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleDestaque(int id)
    {
        var idUtilizador = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtilizador is null) return Unauthorized();

        var anuncio = await _marketDb.Anuncio
            .FirstOrDefaultAsync(a => a.id_anuncio == id && a.id_vendedor == idUtilizador.Value);
        if (anuncio == null) return NotFound();

        anuncio.destaque = !anuncio.destaque;
        anuncio.data_atualizacao = DateTime.UtcNow;

        _marketDb.Publica_Atualiza.Add(new Publica_Atualiza
        {
            id_anuncio = anuncio.id_anuncio,
            id_vendedor = idUtilizador.Value,
            tipo_acao = "editar",
            data_acao = DateTime.UtcNow,
            data_atualizacao = anuncio.data_atualizacao
        });

        await _marketDb.SaveChangesAsync();

        return RedirectToAction(nameof(Meus), new { estado = anuncio.estado });
    }

    // ========================================
    // POST: Remover (DELETE físico do Anúncio + Veículo + Pasta de Imagens)
    // ========================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remover(int id)
    {
        var (a, block) = await GetMyAnuncioOrBlock(id, blockIfSold: true);
        if (block != null) return block;

        var idUtilizador = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtilizador is null) return Unauthorized();

        var anuncio = await _marketDb.Anuncio
            .Include(a => a.id_veiculoNavigation)
            .FirstOrDefaultAsync(a => a.id_anuncio == id && a.id_vendedor == idUtilizador.Value);

        if (anuncio == null) return NotFound();

        var idAnuncio = anuncio.id_anuncio;
        var veiculo = anuncio.id_veiculoNavigation;

        // =========================================================
        // 0) ENCOMENDAS: permitir remover só se TODAS forem "pendente"
        //    (e como é delete físico, temos de APAGAR as pendentes)
        // =========================================================
        var encomendas = await _marketDb.Encomenda
            .Where(e => e.id_anuncio == idAnuncio)
            .ToListAsync();

        if (encomendas.Any(e => !string.Equals(e.estado, "pendente", StringComparison.OrdinalIgnoreCase)))
        {
            TempData["Erro"] = "Não é possível remover este anúncio porque existem encomendas que não estão no estado 'pendente'.";
            return RedirectToAction(nameof(Detalhes), new { id });
        }

        var compradoresEncomendaPendente = encomendas
            .Where(e => string.Equals(e.estado, "pendente", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.id_utilizador)
            .Distinct()
            .ToList();

        // ===== 1) Capturar afetados (reserva + visitas) ANTES do delete/cascade =====
        var reservadores = await _marketDb.Reserva
            .AsNoTracking()
            .Where(r => r.id_anuncio == idAnuncio && r.estado == "ativa")
            .Select(r => r.id_utilizador)
            .Distinct()
            .ToListAsync();

        var visitantes = await _marketDb.Visita
            .AsNoTracking()
            .Where(v => v.id_anuncio == idAnuncio && (v.estado == "pendente" || v.estado == "agendada"))
            .Select(v => v.id_utilizador)
            .Distinct()
            .ToListAsync();

        // ✅ Não removemos interseção: se tiver reserva+visita recebe 2 notif.

        await using var tx = await _marketDb.Database.BeginTransactionAsync();

        try
        {
            _marketDb.Publica_Atualiza.Add(new Publica_Atualiza
            {
                id_anuncio = idAnuncio,
                id_vendedor = idUtilizador.Value,
                tipo_acao = "remover",
                data_acao = DateTime.UtcNow
            });
            await _marketDb.SaveChangesAsync();

            // ===== 2) Notificações ANTES do delete =====
            var acao = "removido";
            var tituloBase = string.IsNullOrWhiteSpace(anuncio.titulo) ? "Anúncio" : anuncio.titulo.Trim();

            // 2.1) Encomenda pendente cancelada
            if (compradoresEncomendaPendente.Count > 0)
            {
                var payloadEnc = new { v = 1, origem = "encomenda", acao = "remover", id_anuncio = idAnuncio };

                await CriarNotificacaoParaVariosAsync(
                    idsDestinatarios: compradoresEncomendaPendente,
                    idVendedor: anuncio.id_vendedor,
                    tipo: "encomenda_pendente_cancelada",
                    titulo: $"Anúncio {acao} — {tituloBase} (Encomenda pendente cancelada)",
                    texto: $"A sua encomenda pendente foi cancelada porque o anúncio foi {acao} pelo vendedor.",
                    payload: payloadEnc
                );
            }

            // 2.2) Reserva cancelada
            if (reservadores.Count > 0)
            {
                var payloadReserva = new { v = 1, origem = "remover", id_anuncio = idAnuncio, motivo = "reserva" };

                await CriarNotificacaoParaVariosAsync(
                    idsDestinatarios: reservadores,
                    idVendedor: anuncio.id_vendedor,
                    tipo: "anuncio_removido",
                    titulo: $"Anúncio {acao} — {tituloBase} (Reserva cancelada)",
                    texto: $"A sua reserva foi cancelada porque o anúncio foi {acao} pelo vendedor.",
                    payload: payloadReserva
                );
            }

            // 2.3) Visita cancelada
            if (visitantes.Count > 0)
            {
                var payloadVisita = new { v = 1, origem = "remover", id_anuncio = idAnuncio, motivo = "visita" };

                await CriarNotificacaoParaVariosAsync(
                    idsDestinatarios: visitantes,
                    idVendedor: anuncio.id_vendedor,
                    tipo: "anuncio_removido",
                    titulo: $"Anúncio {acao} — {tituloBase} (Visita cancelada)",
                    texto: $"A sua visita foi cancelada porque o anúncio foi {acao} pelo vendedor.",
                    payload: payloadVisita
                );
            }

            // ===== 3) APAGAR encomendas pendentes (senão FK NO_ACTION impede delete do anúncio) =====
            if (encomendas.Count > 0)
            {
                _marketDb.Encomenda.RemoveRange(encomendas);
                await _marketDb.SaveChangesAsync();
            }

            // ===== 4) delete físico do anúncio (CASCADE trata Reserva/Visita/Imagens/etc.) =====
            _marketDb.Anuncio.Remove(anuncio);
            await _marketDb.SaveChangesAsync();

            if (veiculo != null)
            {
                _marketDb.Veiculo.Remove(veiculo);
                await _marketDb.SaveChangesAsync();
            }

            await tx.CommitAsync();

            // apagar pasta de imagens (fora da transação)
            var pastaImagens = Path.Combine(_env.WebRootPath, "uploads", "anuncios", idAnuncio.ToString());
            if (Directory.Exists(pastaImagens))
            {
                try { Directory.Delete(pastaImagens, recursive: true); }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AVISO] Erro ao apagar pasta {pastaImagens}: {ex.Message}");
                }
            }

            TempData["Msg"] = "Anúncio removido.";
            return RedirectToAction(nameof(Meus), new { estado = "ativo" });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            TempData["Erro"] = $"Erro ao remover anúncio: {ex.Message}";
            return RedirectToAction(nameof(Meus), new { estado = "ativo" });
        }
    }




    // ========================================
    // GET: /Anuncios/Editar/5
    // ========================================
    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var (a, block) = await GetMyAnuncioOrBlock(id, blockIfSold: true);
        if (block != null) return block;

        var idUtilizador = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtilizador is null) return Unauthorized();

        var isVendedor = await _marketDb.Vendedor.AnyAsync(v => v.id_utilizador == idUtilizador.Value);
        if (!isVendedor) return Forbid();

        var anuncio = await _marketDb.Anuncio
            .Include(a => a.id_veiculoNavigation)
                .ThenInclude(v => v.id_sub_modeloNavigation)
                    .ThenInclude(sm => sm.id_modeloNavigation)
                        .ThenInclude(m => m.id_marcaNavigation)
            .Include(a => a.Anuncio_Imagem)
                .ThenInclude(ai => ai.id_imagem_anuncioNavigation)
            .FirstOrDefaultAsync(a => a.id_anuncio == id && a.id_vendedor == idUtilizador.Value);

        if (anuncio == null) return NotFound();

        ViewBag.IsEdit = true;
        ViewBag.EditId = anuncio.id_anuncio;

        ViewBag.Marcas = await _marketDb.Marca
            .AsNoTracking()
            .OrderBy(m => m.nome)
            .ToListAsync();

        var idSub = anuncio.id_veiculoNavigation.id_sub_modelo;
        var idModelo = anuncio.id_veiculoNavigation.id_sub_modeloNavigation.id_modelo;
        var idMarca = anuncio.id_veiculoNavigation.id_sub_modeloNavigation.id_modeloNavigation.id_marca;

        ViewBag.Modelos = await _marketDb.Modelo
            .AsNoTracking()
            .Where(x => x.id_marca == idMarca)
            .OrderBy(x => x.nome)
            .Select(x => new { x.id_modelo, x.nome })
            .ToListAsync();

        ViewBag.SubModelos = await _marketDb.Sub_Modelo
            .AsNoTracking()
            .Where(x => x.id_modelo == idModelo)
            .OrderBy(x => x.nome)
            .Select(x => new { x.id_sub_modelo, x.nome })
            .ToListAsync();

        var imagens = (anuncio.Anuncio_Imagem ?? new List<Anuncio_Imagem>())
            .OrderByDescending(x => x.principal)
            .ThenBy(x => x.ordem ?? 9999)
            .Select(x => new
            {
                id = x.id_imagem_anuncio,
                url = x.id_imagem_anuncioNavigation.url,
                rotation = 0
            })
            .ToList();

        ViewBag.EditData = new
        {
            id_anuncio = anuncio.id_anuncio,
            id_marca = idMarca,
            id_modelo = idModelo,
            id_sub_modelo = idSub,
            ano = anuncio.id_veiculoNavigation.ano,
            quilometragem = anuncio.id_veiculoNavigation.quilometragem,
            cor = anuncio.id_veiculoNavigation.cor,
            titulo = anuncio.titulo,
            descricao = anuncio.descricao,
            localizacao = anuncio.localizacao,
            preco = anuncio.preco,
            prazo_reserva_horas = anuncio.prazo_reserva,
            destaque = anuncio.destaque,
            estado = anuncio.estado,
            imagens = imagens
        };

        return View("Criar");
    }

    // ========================================
    // POST: /Anuncios/Editar/5
    // Regras:
    // - RASCUNHO: só exige marca/modelo/submodelo/título
    // - PUBLICAR: exige tudo + pelo menos 1 imagem final
    // ========================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(
        int id,
        int id_marca,
        int id_modelo,
        int id_sub_modelo,
        short? ano,               // ✅ nullable
        int? quilometragem,       // ✅ nullable
        string? cor,
        string? titulo,
        string? descricao,
        string? localizacao,
        decimal? preco,           // ✅ nullable
        int prazo_reserva_dias,
        bool destaque,
        List<IFormFile>? imagens,
        string submitAction,
        string? imagensOrder)
    {
        var (a, block) = await GetMyAnuncioOrBlock(id, blockIfSold: true);
        if (block != null) return block;

        var idUtilizador = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtilizador is null) return Unauthorized();

        var vendedor = await _marketDb.Vendedor
            .FirstOrDefaultAsync(v => v.id_utilizador == idUtilizador.Value);
        if (vendedor == null) return Forbid();

        // ✅ Normalizar submitAction
        submitAction = (submitAction ?? "publicar").Trim().ToLowerInvariant();
        bool isDraft = submitAction == "rascunho";
        bool isPublish = submitAction == "publicar";

        // ✅ Converter dias → horas
        int prazoHoras = 168;
        if (isPublish)
        {
            if (prazo_reserva_dias < 1 || prazo_reserva_dias > 7)
                ModelState.AddModelError("prazo_reserva_dias", "Escolhe entre 1 e 7 dias.");
            else
                prazoHoras = prazo_reserva_dias * 24;
        }

        // ✅ SEMPRE obrigatório
        if (id_marca <= 0) ModelState.AddModelError("id_marca", "Seleciona uma marca.");
        if (id_modelo <= 0) ModelState.AddModelError("id_modelo", "Seleciona um modelo.");
        if (id_sub_modelo <= 0) ModelState.AddModelError("id_sub_modelo", "Seleciona um sub-modelo.");
        if (string.IsNullOrWhiteSpace(titulo)) ModelState.AddModelError("titulo", "Título obrigatório.");

        // ✅ Só obrigatório ao PUBLICAR
        if (isPublish)
        {
            if (string.IsNullOrWhiteSpace(localizacao))
                ModelState.AddModelError("localizacao", "Localização obrigatória.");

            if (preco <= 0)
                ModelState.AddModelError("preco", "Preço obrigatório.");

            if (string.IsNullOrWhiteSpace(cor))
                ModelState.AddModelError("cor", "Cor obrigatória.");

            if (ano < 1900 || ano > 2100)
                ModelState.AddModelError("ano", "Ano inválido.");

            if (quilometragem < 0)
                ModelState.AddModelError("quilometragem", "Quilometragem inválida.");

            if (string.IsNullOrWhiteSpace(imagensOrder))
                ModelState.AddModelError("imagens", "Seleciona pelo menos 1 imagem.");
        }

        // Coerência marca/modelo/sub
        var subInfo = await _marketDb.Sub_Modelo
            .Where(s => s.id_sub_modelo == id_sub_modelo)
            .Select(s => new { s.id_modelo, id_marca = s.id_modeloNavigation.id_marca })
            .FirstOrDefaultAsync();

        if (subInfo == null || subInfo.id_modelo != id_modelo || subInfo.id_marca != id_marca)
            ModelState.AddModelError(string.Empty, "Combinação Marca/Modelo/Sub-Modelo inválida.");

        if (!ModelState.IsValid)
        {
            ViewBag.IsEdit = true;
            ViewBag.EditId = id;
            ViewBag.Marcas = await _marketDb.Marca.AsNoTracking().OrderBy(m => m.nome).ToListAsync();
            return View("Criar");
        }

        await using var tx = await _marketDb.Database.BeginTransactionAsync();

        try
        {
            var anuncioDb = await _marketDb.Anuncio
                .Include(a => a.id_veiculoNavigation)
                .Include(a => a.Anuncio_Imagem)
                .FirstOrDefaultAsync(a => a.id_anuncio == id && a.id_vendedor == vendedor.id_utilizador);

            if (anuncioDb == null) return NotFound();

            // ✅ Guardar estado anterior
            var estadoAntes = anuncioDb.estado;

            // ✅ Atualizar veículo (aceita nulls)
            anuncioDb.id_veiculoNavigation.id_sub_modelo = id_sub_modelo;
            anuncioDb.id_veiculoNavigation.ano = ano;
            anuncioDb.id_veiculoNavigation.quilometragem = quilometragem;
            anuncioDb.id_veiculoNavigation.cor = cor?.Trim();

            // ✅ Atualizar anúncio (aceita nulls)
            anuncioDb.titulo = titulo!.Trim();
            anuncioDb.descricao = descricao;
            anuncioDb.localizacao = localizacao?.Trim();
            anuncioDb.preco = preco;
            anuncioDb.prazo_reserva = prazoHoras;
            anuncioDb.destaque = destaque;
            anuncioDb.data_atualizacao = DateTime.UtcNow;

            if (isPublish)
            {
                anuncioDb.estado = "ativo";
                if (!anuncioDb.data_publicacao.HasValue)
                    anuncioDb.data_publicacao = DateTime.UtcNow;
            }
            else
            {
                anuncioDb.estado = "rascunho";
            }

            // ✅ Histórico
            _marketDb.Publica_Atualiza.Add(new Publica_Atualiza
            {
                id_anuncio = anuncioDb.id_anuncio,
                id_vendedor = vendedor.id_utilizador,
                tipo_acao = "editar",
                data_acao = DateTime.UtcNow,
                data_atualizacao = anuncioDb.data_atualizacao,
                data_publicacao = anuncioDb.data_publicacao
            });

            await _marketDb.SaveChangesAsync();

            // ===== ✅ IMAGENS =====
            if (!string.IsNullOrWhiteSpace(imagensOrder))
            {
                var tokens = imagensOrder
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();

                var newFiles = (imagens ?? new List<IFormFile>()).ToList();

                var keepDbIds = tokens
                    .Where(t => t.StartsWith("db:", StringComparison.OrdinalIgnoreCase))
                    .Select(t => int.Parse(t.Substring(3)))
                    .ToHashSet();

                var linksAtuais = anuncioDb.Anuncio_Imagem?.ToList() ?? new List<Anuncio_Imagem>();
                foreach (var l in linksAtuais)
                {
                    if (!keepDbIds.Contains(l.id_imagem_anuncio))
                        _marketDb.Anuncio_Imagem.Remove(l);
                }
                await _marketDb.SaveChangesAsync();

                var links = await _marketDb.Anuncio_Imagem
                    .Where(x => x.id_anuncio == anuncioDb.id_anuncio)
                    .ToListAsync();

                int Ordem = 1;
                bool principalSet = false;

                foreach (var t in tokens)
                {
                    if (t.StartsWith("db:", StringComparison.OrdinalIgnoreCase))
                    {
                        var idImg = int.Parse(t.Substring(3));
                        var link = links.FirstOrDefault(x => x.id_imagem_anuncio == idImg);
                        if (link == null) continue;

                        link.ordem = Ordem;
                        link.principal = !principalSet;
                        principalSet = true;
                        Ordem++;
                    }
                    else if (t.StartsWith("new:", StringComparison.OrdinalIgnoreCase))
                    {
                        var idx = int.Parse(t.Substring(4));
                        if (idx < 0 || idx >= newFiles.Count) continue;

                        var createdId = await GuardarUmaImagemDoAnuncioAsync(anuncioDb.id_anuncio, newFiles[idx]);

                        _marketDb.Anuncio_Imagem.Add(new Anuncio_Imagem
                        {
                            id_anuncio = anuncioDb.id_anuncio,
                            id_imagem_anuncio = createdId,
                            data_adicao = DateTime.UtcNow,
                            ordem = Ordem,
                            principal = !principalSet
                        });

                        principalSet = true;
                        Ordem++;

                        await _marketDb.SaveChangesAsync();
                    }
                }

                await _marketDb.SaveChangesAsync();
            }

            // ✅ Notificação só se realmente "tornou-se ativo"
            if (isPublish && !string.Equals(estadoAntes, "ativo", StringComparison.OrdinalIgnoreCase))
            {
                await CriarNotificacaoMarcaFavoritaAsync(anuncioDb.id_anuncio);
            }

            await tx.CommitAsync();
            return RedirectToAction(nameof(Detalhes), new { id = anuncioDb.id_anuncio });
        }
        catch
        {
            await tx.RollbackAsync();
            ModelState.AddModelError(string.Empty, "Não foi possível guardar as alterações.");

            ViewBag.IsEdit = true;
            ViewBag.EditId = id;
            ViewBag.Marcas = await _marketDb.Marca.AsNoTracking().OrderBy(m => m.nome).ToListAsync();
            return View("Criar");
        }
    }

    // ========================================
    // HELPER: Guardar 1 imagem nova
    // ========================================
    private async Task<int> GuardarUmaImagemDoAnuncioAsync(int idAnuncio, IFormFile file)
    {
        var root = Path.Combine(_env.ContentRootPath, "wwwroot", "uploads", "anuncios", idAnuncio.ToString());
        Directory.CreateDirectory(root);

        if (file == null || file.Length == 0)
            throw new InvalidOperationException("Ficheiro inválido.");
        if (!file.ContentType.StartsWith("image/"))
            throw new InvalidOperationException("Tipo inválido.");
        if (file.Length > 5 * 1024 * 1024)
            throw new InvalidOperationException("Imagem demasiado grande.");

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";

        var safeName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var physicalPath = Path.Combine(root, safeName);

        await using (var fs = new FileStream(physicalPath, FileMode.Create))
            await file.CopyToAsync(fs);

        var url = $"/uploads/anuncios/{idAnuncio}/{safeName}";

        var img = new Imagem_Anuncio
        {
            url = url,
            legenda = null
        };

        _marketDb.Imagem_Anuncio.Add(img);
        await _marketDb.SaveChangesAsync();

        return img.id_imagem_anuncio;
    }

    // ========================================
    // HELPERS PRIVADOS
    // ========================================
    private async Task<int?> GetCurrentMarketUserIdAsync()
        => await _currentUser.GetMarketUserIdAsync(User);

    private async Task<bool> IsAdminAsync(int idUtilizador)
        => await _marketDb.Administrador
            .AsNoTracking()
            .AnyAsync(a => a.id_utilizador == idUtilizador);

    private async Task<bool> IsBuyerOfAnuncioAsync(int idUtilizador, int idAnuncio)
    {
        return await _marketDb.Encomenda.AnyAsync(e =>
            e.id_anuncio == idAnuncio &&
            e.id_utilizador == idUtilizador &&
            e.estado == "pago"
        );
    }

    // ========================================
    // PÚBLICO: /Anuncios/Carros
    // Lista anúncios "ativo" e "reservado"
    // ========================================
    [ExpireReservas]
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Carros(
        string estado = "todos",
        string? q = null,
        string sort = "recent",
        int? idMarca = null,
        int? idModelo = null,
        int? idSubModelo = null,
        string? categoria = null,
        string? combustivel = null,
        short? anoDe = null,
        short? anoAte = null,
        int? kmDe = null,
        int? kmAte = null,
        decimal? precoDe = null,
        decimal? precoAte = null)
    {

        ViewBag.Marcas = await _marketDb.Marca
            .AsNoTracking()
            .OrderBy(m => m.nome)
            .ToListAsync();

        estado = (estado ?? "todos").Trim().ToLowerInvariant();
        ViewBag.Estado = estado;
        ViewBag.Q = (q ?? "").Trim();
        ViewBag.Sort = (sort ?? "recent").Trim().ToLowerInvariant();

        ViewBag.IdMarca = idMarca;
        ViewBag.IdModelo = idModelo;
        ViewBag.IdSubModelo = idSubModelo;
        ViewBag.Categoria = categoria;
        ViewBag.Combustivel = combustivel;
        ViewBag.AnoDe = anoDe;
        ViewBag.AnoAte = anoAte;
        ViewBag.KmDe = kmDe;
        ViewBag.KmAte = kmAte;
        ViewBag.PrecoDe = precoDe;
        ViewBag.PrecoAte = precoAte;

        var baseQ = _marketDb.Anuncio
            .AsNoTracking()
            .Where(a => a.estado == "ativo" || a.estado == "reservado");

        var counts = await baseQ
            .GroupBy(a => a.estado)
            .Select(g => new { estado = g.Key, total = g.Count() })
            .ToListAsync();

        int Count(string st) => counts.FirstOrDefault(x => x.estado == st)?.total ?? 0;

        ViewBag.CountAtivo = Count("ativo");
        ViewBag.CountReservado = Count("reservado");
        ViewBag.CountTodos = ViewBag.CountAtivo + ViewBag.CountReservado;

        return View();
    }

    // ========================================
    // GET (AJAX): /Anuncios/_CarrosLista
    // ========================================
    [ExpireReservas]
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> _CarrosLista(
        string estado = "todos",
        string? q = null,
        string sort = "recent",
        int? idMarca = null,
        int? idModelo = null,
        int? idSubModelo = null,
        string? categoria = null,
        string? combustivel = null,
        string? caixa = null,
        short? anoDe = null,
        short? anoAte = null,
        int? kmDe = null,
        int? kmAte = null,
        decimal? precoDe = null,
        decimal? precoAte = null)
    {

        estado = (estado ?? "todos").Trim().ToLowerInvariant();
        sort = (sort ?? "recent").Trim().ToLowerInvariant();
        q = (q ?? "").Trim();

        var baseQuery = _marketDb.Anuncio
            .AsNoTracking()
            .Where(a => a.estado == "ativo" || a.estado == "reservado")
            .Include(a => a.Anuncio_Imagem)
                .ThenInclude(ai => ai.id_imagem_anuncioNavigation)
            .Include(a => a.id_veiculoNavigation)
                .ThenInclude(v => v.id_sub_modeloNavigation)
                    .ThenInclude(sm => sm.id_modeloNavigation)
                        .ThenInclude(m => m.id_marcaNavigation)
            .AsQueryable()
            .Include(a => a.id_vendedorNavigation)
            .ThenInclude(v => v.id_candidaturaNavigation)
            .AsQueryable();

        var filteredForCounts = ApplyCarrosFilters(
            baseQuery,
            q, idMarca, idModelo, idSubModelo,
            categoria, combustivel, caixa,
            anoDe, anoAte, kmDe, kmAte, precoDe, precoAte);

        var counts = await filteredForCounts
            .GroupBy(a => a.estado)
            .Select(g => new { estado = g.Key, total = g.Count() })
            .ToListAsync();

        int Count(string st) => counts.FirstOrDefault(x => x.estado == st)?.total ?? 0;

        ViewBag.CountAtivo = Count("ativo");
        ViewBag.CountReservado = Count("reservado");
        ViewBag.CountTodos = ViewBag.CountAtivo + ViewBag.CountReservado;
        ViewBag.PageTitle = await BuildCarrosTitleAsync(idMarca, idModelo, idSubModelo);

        var query = filteredForCounts;

        if (estado == "ativo") query = query.Where(a => a.estado == "ativo");
        else if (estado == "reservado") query = query.Where(a => a.estado == "reservado");

        query = sort switch
        {
            "destaque" => query.OrderByDescending(a => a.destaque)
                               .ThenByDescending(a => a.data_atualizacao ?? a.data_publicacao),
            "price_asc" => query.OrderBy(a => a.preco ?? decimal.MaxValue),
            "price_desc" => query.OrderByDescending(a => a.preco ?? 0),
            "km_asc" => query.OrderBy(a => a.id_veiculoNavigation.quilometragem ?? int.MaxValue),
            "km_desc" => query.OrderByDescending(a => a.id_veiculoNavigation.quilometragem ?? 0),
            "hp_asc" => query.OrderBy(a => a.id_veiculoNavigation.id_sub_modeloNavigation.potencia_hp),
            "hp_desc" => query.OrderByDescending(a => a.id_veiculoNavigation.id_sub_modeloNavigation.potencia_hp),
            _ => query.OrderByDescending(a => a.data_atualizacao ?? a.data_publicacao)
        };

        var lista = await query.ToListAsync();

        ViewBag.Estado = estado;
        ViewBag.Sort = sort;

        return PartialView("_CarrosResultados", lista);
    }

    private IQueryable<Anuncio> ApplyCarrosFilters(
        IQueryable<Anuncio> query,
        string? q,
        int? idMarca,
        int? idModelo,
        int? idSubModelo,
        string? categoria,
        string? combustivel,
        string? caixa,
        short? anoDe,
        short? anoAte,
        int? kmDe,
        int? kmAte,
        decimal? precoDe,
        decimal? precoAte)
    {
        q = (q ?? "").Trim();

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(a => a.titulo != null && a.titulo.Contains(q));

        if (idMarca.HasValue && idMarca > 0)
            query = query.Where(a =>
                a.id_veiculoNavigation.id_sub_modeloNavigation.id_modeloNavigation.id_marca == idMarca.Value);

        if (idModelo.HasValue && idModelo > 0)
            query = query.Where(a =>
                a.id_veiculoNavigation.id_sub_modeloNavigation.id_modelo == idModelo.Value);

        if (idSubModelo.HasValue && idSubModelo > 0)
            query = query.Where(a =>
                a.id_veiculoNavigation.id_sub_modelo == idSubModelo.Value);

        if (!string.IsNullOrWhiteSpace(categoria))
            query = query.Where(a => a.id_veiculoNavigation.id_sub_modeloNavigation.categoria == categoria);

        if (!string.IsNullOrWhiteSpace(combustivel))
            query = query.Where(a => a.id_veiculoNavigation.id_sub_modeloNavigation.combustivel == combustivel);

        if (!string.IsNullOrWhiteSpace(caixa))
            query = query.Where(a => a.id_veiculoNavigation.id_sub_modeloNavigation.caixa == caixa);

        if (anoDe.HasValue) query = query.Where(a => a.id_veiculoNavigation.ano >= anoDe.Value);
        if (anoAte.HasValue) query = query.Where(a => a.id_veiculoNavigation.ano <= anoAte.Value);

        if (kmDe.HasValue) query = query.Where(a => a.id_veiculoNavigation.quilometragem >= kmDe.Value);
        if (kmAte.HasValue) query = query.Where(a => a.id_veiculoNavigation.quilometragem <= kmAte.Value);

        if (precoDe.HasValue) query = query.Where(a => a.preco >= precoDe.Value);
        if (precoAte.HasValue) query = query.Where(a => a.preco <= precoAte.Value);

        return query;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetCategorias()
    {
        var list = await _marketDb.Sub_Modelo
            .AsNoTracking()
            .Select(x => x.categoria)
            .Where(x => x != null && x != "")
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        return Json(list);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetCombustiveis()
    {
        var list = await _marketDb.Sub_Modelo
            .AsNoTracking()
            .Select(x => x.combustivel)
            .Where(x => x != null && x != "")
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        return Json(list);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetCaixas()
    {
        var list = await _marketDb.Sub_Modelo
            .AsNoTracking()
            .Select(x => x.caixa)
            .Where(x => x != null && x != "")
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

        return Json(list);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetCarrosFacetCounts(
    string? q = null,
    int? idMarca = null,
    int? idModelo = null,
    int? idSubModelo = null,
    string? categoria = null,
    string? combustivel = null,
    string? caixa = null,
    short? anoDe = null,
    short? anoAte = null,
    int? kmDe = null,
    int? kmAte = null,
    decimal? precoDe = null,
    decimal? precoAte = null)
    {
        q = (q ?? "").Trim();

        var baseQuery = _marketDb.Anuncio
            .AsNoTracking()
            .Where(a => a.estado == "ativo" || a.estado == "reservado")
            .AsQueryable();

        // ✅ MARCAS: ignora marca/modelo/submodelo (porque são dependentes)
        var qMarca = ApplyCarrosFilters(
            baseQuery,
            q,
            null, null, null,
            categoria, combustivel, caixa,
            anoDe, anoAte, kmDe, kmAte, precoDe, precoAte);

        var marcas = await qMarca
            .GroupBy(a => new
            {
                id = a.id_veiculoNavigation.id_sub_modeloNavigation.id_modeloNavigation.id_marca,
                nome = a.id_veiculoNavigation.id_sub_modeloNavigation.id_modeloNavigation.id_marcaNavigation.nome
            })
            .Select(g => new { id = g.Key.id, text = g.Key.nome, count = g.Count() })
            .OrderBy(x => x.text)
            .ToListAsync();

        // ✅ MODELOS: só faz sentido se existir Marca selecionada
        object modelos = Array.Empty<object>();
        if (idMarca.HasValue && idMarca > 0)
        {
            var qModelo = ApplyCarrosFilters(
                baseQuery,
                q,
                idMarca, null, null,
                categoria, combustivel, caixa,
                anoDe, anoAte, kmDe, kmAte, precoDe, precoAte);

            modelos = await qModelo
                .GroupBy(a => new
                {
                    id = a.id_veiculoNavigation.id_sub_modeloNavigation.id_modelo,
                    nome = a.id_veiculoNavigation.id_sub_modeloNavigation.id_modeloNavigation.nome
                })
                .Select(g => new { id = g.Key.id, text = g.Key.nome, count = g.Count() })
                .OrderBy(x => x.text)
                .ToListAsync();
        }

        // ✅ SUB-MODELOS: só faz sentido se existir Modelo selecionado
        object subModelos = Array.Empty<object>();
        if (idModelo.HasValue && idModelo > 0)
        {
            var qSub = ApplyCarrosFilters(
                baseQuery,
                q,
                idMarca, idModelo, null,
                categoria, combustivel, caixa,
                anoDe, anoAte, kmDe, kmAte, precoDe, precoAte);

            subModelos = await qSub
                .GroupBy(a => new
                {
                    id = a.id_veiculoNavigation.id_sub_modelo,
                    nome = a.id_veiculoNavigation.id_sub_modeloNavigation.nome
                })
                .Select(g => new { id = g.Key.id, text = g.Key.nome, count = g.Count() })
                .OrderBy(x => x.text)
                .ToListAsync();
        }

        // ✅ CATEGORIA: ignora categoria (mas mantém marca/modelo/sub se existirem)
        var qCat = ApplyCarrosFilters(
            baseQuery,
            q,
            idMarca, idModelo, idSubModelo,
            null, combustivel, caixa,
            anoDe, anoAte, kmDe, kmAte, precoDe, precoAte);

        var categorias = await qCat
            .GroupBy(a => a.id_veiculoNavigation.id_sub_modeloNavigation.categoria)
            .Select(g => new { value = g.Key, count = g.Count() })
            .Where(x => x.value != null && x.value != "")
            .OrderBy(x => x.value)
            .ToListAsync();

        // ✅ COMBUSTÍVEL
        var qComb = ApplyCarrosFilters(
            baseQuery,
            q,
            idMarca, idModelo, idSubModelo,
            categoria, null, caixa,
            anoDe, anoAte, kmDe, kmAte, precoDe, precoAte);

        var combustiveis = await qComb
            .GroupBy(a => a.id_veiculoNavigation.id_sub_modeloNavigation.combustivel)
            .Select(g => new { value = g.Key, count = g.Count() })
            .Where(x => x.value != null && x.value != "")
            .OrderBy(x => x.value)
            .ToListAsync();

        // ✅ CAIXA
        var qCaixa = ApplyCarrosFilters(
            baseQuery,
            q,
            idMarca, idModelo, idSubModelo,
            categoria, combustivel, null,
            anoDe, anoAte, kmDe, kmAte, precoDe, precoAte);

        var caixas = await qCaixa
            .GroupBy(a => a.id_veiculoNavigation.id_sub_modeloNavigation.caixa)
            .Select(g => new { value = g.Key, count = g.Count() })
            .Where(x => x.value != null && x.value != "")
            .OrderBy(x => x.value)
            .ToListAsync();

        return Json(new { marcas, modelos, subModelos, categorias, combustiveis, caixas });
    }
   

    // ========================================
    // POST: /Anuncios/GuardarFiltros
    // ========================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GuardarFiltros(
        string estado = "todos",
        string? q = null,
        string sort = "recent",
        int? idMarca = null,
        int? idModelo = null,
        int? idSubModelo = null,
        string? categoria = null,
        string? combustivel = null,
        string? caixa = null,
        short? anoDe = null,
        short? anoAte = null,
        int? kmDe = null,
        int? kmAte = null,
        decimal? precoDe = null,
        decimal? precoAte = null)
    {
        var idUtilizador = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtilizador is null)
            return Unauthorized();

        var isComprador = await _marketDb.Comprador
            .AnyAsync(c => c.id_utilizador == idUtilizador.Value);

        if (!isComprador)
        {
            // admin autenticado, mas não é comprador
            return Forbid();
        }

        var payload = new
        {
            v = 1,
            estado = (estado ?? "todos").Trim().ToLowerInvariant(),
            q = (q ?? "").Trim(),
            sort = (sort ?? "recent").Trim().ToLowerInvariant(),
            idMarca,
            idModelo,
            idSubModelo,
            categoria = string.IsNullOrWhiteSpace(categoria) ? null : categoria,
            combustivel = string.IsNullOrWhiteSpace(combustivel) ? null : combustivel,
            caixa = string.IsNullOrWhiteSpace(caixa) ? null : caixa,
            anoDe,
            anoAte,
            kmDe,
            kmAte,
            precoDe,
            precoAte
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        var jaExiste = await _marketDb.Comprador_Filtro
            .AsNoTracking()
            .Where(cf => cf.id_utilizador == idUtilizador.Value)
            .AnyAsync(cf => cf.id_filtroNavigation.parametros == json);

        if (jaExiste)
            return Json(new { ok = true, message = "Já tinhas estes filtros guardados." });

        var filtro = new Filtro { parametros = json };
        _marketDb.Filtro.Add(filtro);
        await _marketDb.SaveChangesAsync();

        _marketDb.Comprador_Filtro.Add(new Comprador_Filtro
        {
            id_utilizador = idUtilizador.Value,
            id_filtro = filtro.id_filtro,
            data_filtros = DateTime.UtcNow
        });

        await _marketDb.SaveChangesAsync();

        return Json(new { ok = true, message = "Filtros guardados com sucesso." });
    }

    // ========================================
    // GET: /Anuncios/MeusFiltros
    // ========================================
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> MeusFiltros()
    {
        var idUtil = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtil is null) return Unauthorized();

        var isComprador = await _marketDb.Comprador.AnyAsync(c => c.id_utilizador == idUtil.Value);
        if (!isComprador) return Forbid();

        var lista = await _marketDb.Comprador_Filtro
            .AsNoTracking()
            .Where(cf => cf.id_utilizador == idUtil.Value)
            .Include(cf => cf.id_filtroNavigation)
            .OrderByDescending(cf => cf.data_filtros)
            .ToListAsync();

        // ✅ Recolher IDs do JSON (1x) para resolver nomes (sem N+1)
        var marcaIds = new HashSet<int>();
        var modeloIds = new HashSet<int>();
        var subIds = new HashSet<int>();

        foreach (var cf in lista)
        {
            var json = cf.id_filtroNavigation?.parametros;
            if (string.IsNullOrWhiteSpace(json)) continue;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                int? GetInt(string k)
                    => root.TryGetProperty(k, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : null;

                var idMarca = GetInt("idMarca");
                var idModelo = GetInt("idModelo");
                var idSub = GetInt("idSubModelo");

                if (idMarca.HasValue && idMarca.Value > 0) marcaIds.Add(idMarca.Value);
                if (idModelo.HasValue && idModelo.Value > 0) modeloIds.Add(idModelo.Value);
                if (idSub.HasValue && idSub.Value > 0) subIds.Add(idSub.Value);
            }
            catch { /* ignora JSON inválido */ }
        }

        var marcaById = await _marketDb.Marca
            .AsNoTracking()
            .Where(m => marcaIds.Contains(m.id_marca))
            .ToDictionaryAsync(m => m.id_marca, m => m.nome);

        var modeloById = await _marketDb.Modelo
            .AsNoTracking()
            .Where(m => modeloIds.Contains(m.id_modelo))
            .ToDictionaryAsync(m => m.id_modelo, m => m.nome);

        var subById = await _marketDb.Sub_Modelo
            .AsNoTracking()
            .Where(s => subIds.Contains(s.id_sub_modelo))
            .ToDictionaryAsync(s => s.id_sub_modelo, s => s.nome);

        ViewBag.MarcaById = marcaById;
        ViewBag.ModeloById = modeloById;
        ViewBag.SubModeloById = subById;

        return View(lista);
    }


    // ========================================
    // POST: /Anuncios/EliminarFiltro
    // Opção B: Remove ligação + apaga Filtro se órfão
    // ========================================
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EliminarFiltro(int idFiltro)
    {
        var idUtil = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtil is null) return Unauthorized();

        var isComprador = await _marketDb.Comprador.AnyAsync(c => c.id_utilizador == idUtil.Value);
        if (!isComprador) return Forbid();

        var link = await _marketDb.Comprador_Filtro
            .FirstOrDefaultAsync(cf => cf.id_utilizador == idUtil.Value && cf.id_filtro == idFiltro);

        if (link == null)
        {
            TempData["Msg"] = "Filtro não encontrado.";
            return RedirectToAction(nameof(MeusFiltros));
        }

        _marketDb.Comprador_Filtro.Remove(link);
        await _marketDb.SaveChangesAsync();

        var stillUsed = await _marketDb.Comprador_Filtro.AnyAsync(cf => cf.id_filtro == idFiltro);
        if (!stillUsed)
        {
            var filtro = await _marketDb.Filtro.FindAsync(idFiltro);
            if (filtro != null)
            {
                _marketDb.Filtro.Remove(filtro);
                await _marketDb.SaveChangesAsync();
            }
        }

        TempData["MsgFiltros"] = "Filtro eliminado.";
        return RedirectToAction(nameof(MeusFiltros));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> AplicarFiltro(int idFiltro)
    {
        var idUtil = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtil is null) return Unauthorized();

        // Só compradores (e vendedores, porque são compradores)
        var isComprador = await _marketDb.Comprador
            .AnyAsync(c => c.id_utilizador == idUtil.Value);

        if (!isComprador) return Forbid();

        // O filtro tem de pertencer a este utilizador
        var cf = await _marketDb.Comprador_Filtro
            .AsNoTracking()
            .Include(x => x.id_filtroNavigation)
            .FirstOrDefaultAsync(x =>
                x.id_utilizador == idUtil.Value &&
                x.id_filtro == idFiltro);

        if (cf == null)
            return NotFound();

        var json = cf.id_filtroNavigation?.parametros;
        if (string.IsNullOrWhiteSpace(json))
            return RedirectToAction("Carros", "Anuncios");

        // Converter JSON → querystring
        var qs = new Dictionary<string, string?>();

        using (var doc = JsonDocument.Parse(json))
        {
            var root = doc.RootElement;

            void AddString(string key)
            {
                if (root.TryGetProperty(key, out var p) &&
                    p.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(p.GetString()))
                {
                    qs[key] = p.GetString();
                }
            }

            void AddNumber(string key)
            {
                if (root.TryGetProperty(key, out var p) &&
                    p.ValueKind == JsonValueKind.Number)
                {
                    qs[key] = p.ToString();
                }
            }

            // campos suportados
            AddString("estado");
            AddString("q");
            AddString("sort");
            AddString("categoria");
            AddString("combustivel");
            AddString("caixa");

            AddNumber("idMarca");
            AddNumber("idModelo");
            AddNumber("idSubModelo");

            AddNumber("anoDe");
            AddNumber("anoAte");
            AddNumber("kmDe");
            AddNumber("kmAte");
            AddNumber("precoDe");
            AddNumber("precoAte");
        }

        return RedirectToAction("Carros", "Anuncios", qs);
    }

    // ========================================
    // POST: /Anuncios/Reservar
    // Fase 1: Criar reserva básica
    // ========================================
    [ExpireReservas]
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reservar(int idAnuncio)
    {

        var idUtil = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtil is null) return Unauthorized();

        var isComprador = await _marketDb.Comprador
            .AnyAsync(c => c.id_utilizador == idUtil.Value);

        if (!isComprador) return Forbid();

        var anuncio = await _marketDb.Anuncio
            .FirstOrDefaultAsync(a => a.id_anuncio == idAnuncio);

        if (anuncio == null)
            return NotFound();

        if (anuncio.id_vendedor == idUtil.Value)
        {
            TempData["Msg"] = "Não podes reservar o teu próprio anúncio.";
            return RedirectToAction(nameof(Detalhes), new { id = idAnuncio });
        }

        if (anuncio.estado != "ativo")
        {
            TempData["Msg"] = "Este anúncio não pode ser reservado.";
            return RedirectToAction(nameof(Detalhes), new { id = idAnuncio });
        }

        // ✅ Verificar se já existe reserva ATIVA para este anúncio
        var jaReservado = await _marketDb.Reserva
            .AnyAsync(r => r.id_anuncio == idAnuncio && r.estado == "ativa");

        if (jaReservado)
        {
            TempData["Msg"] = "Este anúncio já se encontra reservado.";
            return RedirectToAction(nameof(Detalhes), new { id = idAnuncio });
        }

        var agora = DateTime.UtcNow;
        var expiracao = agora.AddHours(anuncio.prazo_reserva);

        // ✅ Criar reserva com estado = "ativa" (obrigatório)
        var reserva = new Reserva
        {
            id_anuncio = idAnuncio,
            id_utilizador = idUtil.Value,
            data_reserva = agora,
            data_expiracao = expiracao,
            estado = "ativa"  // ✅ Campo obrigatório (NOT NULL + CHECK constraint)
        };

        _marketDb.Reserva.Add(reserva);

        anuncio.estado = "reservado";
        anuncio.data_atualizacao = agora;

        // ✅ Notificação para o VENDEDOR: anúncio foi reservado
        await AdicionarNotif_AnuncioReservado_VendedorAsync(anuncio, idUtil.Value, expiracao);

        await _marketDb.SaveChangesAsync();

        TempData["Msg"] = "Reserva efetuada com sucesso.";
        return RedirectToAction(nameof(Detalhes), new { id = idAnuncio });
    }

    // ========================================
    // GET: /Anuncios/MinhasOperacoes
    // Área do comprador (reservas/visitas/encomendas)
    // ========================================
    [ExpireReservas]
    [Authorize]
    [HttpGet]
    // GET: /Anuncios/MinhasOperacoes?tipo=todos|reservas|visitas|encomendas
    public async Task<IActionResult> MinhasOperacoes(string tipo = "todos")
    {
        var idUtil = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtil == null) return Unauthorized();

        var tipoUi = (tipo ?? "todos").ToLowerInvariant();
        if (tipoUi is not ("todos" or "reservas" or "visitas" or "encomendas"))
            tipoUi = "todos";

        // === CONTAGENS p/ tabs ===
        // Reservas: contar TODAS (ativa/expirada/cancelada)
        var cReservas = await _marketDb.Reserva
            .AsNoTracking()
            .CountAsync(r => r.id_utilizador == idUtil.Value);

        // Visitas: só contar (a lista continua ajax no VisitasController)
        var cVisitas = await _marketDb.Visita
            .AsNoTracking()
            .CountAsync(v => v.id_utilizador == idUtil.Value);

        // Encomendas: contar TODAS (pendente/pago/cancelado)
        var cEncomendas = await _marketDb.Encomenda
            .AsNoTracking()
            .CountAsync(e => e.id_utilizador == idUtil.Value);

        ViewBag.Tipo = tipoUi;
        ViewBag.CountReservas = cReservas;
        ViewBag.CountVisitas = cVisitas;
        ViewBag.CountEncomendas = cEncomendas;
        ViewBag.CountTodos = cReservas + cVisitas + cEncomendas;

        return View(); // a view principal só tem tabs + container ajax
    }

    // ========================================
    // GET: /Anuncios/_MinhasOperacoesConteudo
    // Conteúdo trocado por tabs (AJAX)
    // ========================================
    [ExpireReservas]
    [Authorize]
    [HttpGet]
    // GET: /Anuncios/_MinhasOperacoesConteudo?tipo=...
    public async Task<IActionResult> _MinhasOperacoesConteudo(string tipo = "todos")
    {
        var idUtil = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtil == null) return Unauthorized();

        var tipoUi = (tipo ?? "todos").ToLowerInvariant();
        if (tipoUi is not ("todos" or "reservas" or "visitas" or "encomendas"))
            tipoUi = "todos";

        // Carregamentos iniciais (para renderizar logo a lista por defeito).
        // Reservas: TODAS (ativa/expirada/cancelada)
        var reservas = new List<Reserva>();
        if (tipoUi is "todos" or "reservas")
        {
            reservas = await _marketDb.Reserva
                .Include(r => r.id_anuncioNavigation)
                    .ThenInclude(a => a.Anuncio_Imagem)
                        .ThenInclude(ai => ai.id_imagem_anuncioNavigation)
                .Where(r => r.id_utilizador == idUtil.Value)
                .OrderByDescending(r => r.data_reserva)
                .ToListAsync();
        }

        // Encomendas: por defeito “todos” (pendente/pago/cancelado)
        // (Se o teu _MinhasOperacoesConteudo ainda renderiza a secção de encomendas
        // com dados iniciais, podes continuar a carregar aqui — ou deixar só o container
        // e a lista vir do EncomendasController via AJAX.)
        if (tipoUi is "todos" or "encomendas")
        {
            var encomendas = await _marketDb.Encomenda
                .Include(e => e.id_anuncioNavigation)
                    .ThenInclude(a => a.Anuncio_Imagem)
                        .ThenInclude(ai => ai.id_imagem_anuncioNavigation)
                .Where(e => e.id_utilizador == idUtil.Value)
                .OrderByDescending(e => e.data_encomenda)
                .ToListAsync();

            ViewBag.Encomendas = encomendas;
        }

        ViewBag.Tipo = tipoUi;

        return PartialView("_MinhasOperacoesConteudo", reservas);
    }

    // ========================================
    // GET: /Anuncios/ListaMinhasReservas  (AJAX)
    // Partial: Views/Anuncios/_MinhasReservasLista.cshtml
    // ========================================
    [ExpireReservas]
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> ListaMinhasReservas(string estado = "todos", string ordem = "recent")
    {
        var idUtil = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtil == null) return Unauthorized();

        var q = _marketDb.Reserva
            .Include(r => r.id_anuncioNavigation)
                .ThenInclude(a => a.Anuncio_Imagem)
                    .ThenInclude(ai => ai.id_imagem_anuncioNavigation)
            .Where(r => r.id_utilizador == idUtil.Value);

        // ✅ Filtro por estado
        estado = (estado ?? "todos").Trim().ToLowerInvariant();
        if (estado != "todos")
        {
            q = q.Where(r => r.estado != null && r.estado.ToLower() == estado);
        }

        // ✅ Ordenação: recent/oldest (data_reserva) + expira_proxima/expira_longe (data_expiracao)
        ordem = (ordem ?? "recent").Trim().ToLowerInvariant();
        q = ordem switch
        {
            "oldest" => q.OrderBy(r => r.data_reserva),
            "recent" => q.OrderByDescending(r => r.data_reserva),

            // ✅ Expira nulo: empurra para o fim
            "expira_proxima" => q.OrderBy(r => r.data_expiracao == null)
                                 .ThenBy(r => r.data_expiracao),

            "expira_longe" => q.OrderBy(r => r.data_expiracao == null)
                               .ThenByDescending(r => r.data_expiracao),

            _ => q.OrderByDescending(r => r.data_reserva)
        };

        var lista = await q.ToListAsync();

        ViewBag.Estado = estado;
        ViewBag.Ordem = ordem;

        return PartialView("~/Views/Reservas/_MinhasReservasLista.cshtml", lista);
    }




    // ========================================
    // POST: /Anuncios/CancelarReserva
    // Cancela reserva + volta anúncio para ativo
    // ========================================
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelarReserva(long idReserva)
    {
        var idUtil = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtil is null) return Unauthorized();

        var isComprador = await _marketDb.Comprador.AnyAsync(c => c.id_utilizador == idUtil.Value);
        if (!isComprador) return Forbid();

        var reserva = await _marketDb.Reserva
            .Include(r => r.id_anuncioNavigation)
            .FirstOrDefaultAsync(r => r.id_reserva == idReserva);

        if (reserva == null)
        {
            TempData["Msg"] = "Reserva não encontrada.";
            return RedirectToAction(nameof(MinhasOperacoes));
        }

        if (reserva.id_utilizador != idUtil.Value)
        {
            TempData["Msg"] = "Não tens permissões para cancelar esta reserva.";
            return RedirectToAction(nameof(MinhasOperacoes));
        }

        if (!string.Equals(reserva.estado, "ativa", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Msg"] = "Esta reserva já não está ativa.";
            return RedirectToAction(nameof(MinhasOperacoes));
        }

        await using var tx = await _marketDb.Database.BeginTransactionAsync();

        var anuncio = reserva.id_anuncioNavigation;

        // ✅ Notificação para o VENDEDOR: reserva cancelada pelo comprador
        await AdicionarNotif_ReservaCancelada_VendedorAsync(reserva, anuncio);

        _marketDb.Reserva.Remove(reserva);

        if (string.Equals(anuncio.estado, "reservado", StringComparison.OrdinalIgnoreCase))
        {
            anuncio.estado = "ativo";
            anuncio.data_atualizacao = DateTime.UtcNow;
            _marketDb.Anuncio.Update(anuncio);
        }

        await _marketDb.SaveChangesAsync();

        // ✅ Notificação: anúncio voltou a ativo (reservado → ativo)
        await CriarNotificacaoMarcaFavoritaAsync(anuncio.id_anuncio);

        await tx.CommitAsync();

        TempData["Msg"] = "Reserva cancelada.";
        return RedirectToAction(nameof(MinhasOperacoes));
    }


    // ============================================================
    // NOTIFICAÇÕES — Marca favoritada → anúncio torna-se ativo
    // Regras:
    // - Sempre cria Notificacao_Recebida (inbox interno / view)
    // - Se Utilizador.notificar_novidades = true → estado='pendente' (para envio externo)
    // - Se false → estado='enviada' (apenas inbox/view)
    // - canal só 'email' ou 'sms'
    // ============================================================

    private static string NormalizarCanalEmailSms(string? canalPreferido)
    {
        var c = (canalPreferido ?? "").Trim().ToLowerInvariant();
        return (c == "sms") ? "sms" : "email"; // default email
    }

    private async Task CriarNotificacaoMarcaFavoritaAsync(int idAnuncio)
    {
        // Buscar marca do anúncio + vendedor (para não se notificar a si próprio)
        var info = await _marketDb.Anuncio
            .AsNoTracking()
            .Where(a => a.id_anuncio == idAnuncio)
            .Select(a => new
            {
                a.id_anuncio,
                a.id_vendedor,
                anuncioTitulo = a.titulo,
                idMarca = a.id_veiculoNavigation.id_sub_modeloNavigation.id_modeloNavigation.id_marca,
                nomeMarca = a.id_veiculoNavigation.id_sub_modeloNavigation.id_modeloNavigation.id_marcaNavigation.nome
            })
            .FirstOrDefaultAsync();

        if (info == null) return;

        // Destinatários: quem favoritou a marca
        var destinatarios = await _marketDb.Comprador_Marca
            .AsNoTracking()
            .Where(cm => cm.id_marca == info.idMarca)
            .Select(cm => cm.id_utilizador)
            .Distinct()
            .Join(
                _marketDb.Utilizador.AsNoTracking(),
                id => id,
                u => u.id_utilizador,
                (id, u) => new { u.id_utilizador, u.notificar_novidades, u.canal_notificacao_preferido }
            )
            .Where(x => x.id_utilizador != info.id_vendedor) // não notificar o vendedor
            .ToListAsync();

        if (destinatarios.Count == 0) return;

        // Conteúdo da notificação (1 registo)
        var payload = new
        {
            v = 1,
            origem = "marca_favorita",
            id_anuncio = info.id_anuncio,
            id_marca = info.idMarca
        };

        var notif = new Notificacao
        {
            tipo = "marca_anuncio_ativo",
            titulo = $"Novo anúncio ativo — {info.nomeMarca}",
            texto = string.IsNullOrWhiteSpace(info.anuncioTitulo)
                ? "Foi publicado/ativado um anúncio de uma marca que favoritou."
                : $"Anúncio: {info.anuncioTitulo}",
            payload_json = JsonSerializer.Serialize(payload)
        };

        _marketDb.Notificacao.Add(notif);
        await _marketDb.SaveChangesAsync(); // gera id_notificacao

        // Inbox interno (sempre) + estado depende do notificar_novidades
        var rows = destinatarios.Select(x => new Notificacao_Recebida
        {
            id_utilizador = x.id_utilizador,
            id_notificacao = notif.id_notificacao,
            canal = NormalizarCanalEmailSms(x.canal_notificacao_preferido),
            estado = x.notificar_novidades ? "pendente" : "enviada",
            data_envio = DateTime.UtcNow
        }).ToList();

        _marketDb.Notificacao_Recebida.AddRange(rows);
        await _marketDb.SaveChangesAsync();
    }


    // ============================================================
    // NOTIFICAÇÕES — Reservas (Vendedor)
    // - Inbox sempre (Notificacao_Recebida)
    // - Email só se Utilizador.notificar_novidades = true (estado = 'pendente')
    // ============================================================
    private async Task AdicionarNotif_AnuncioReservado_VendedorAsync(Anuncio anuncio, int idComprador, DateTime dataExpiracaoUtc)
    {
        // Preferências do vendedor (na tabela market.Utilizador)
        var vendedorPrefs = await _marketDb.Utilizador
            .FirstOrDefaultAsync(u => u.id_utilizador == anuncio.id_vendedor);

        if (vendedorPrefs is null) return;

        var payload = new
        {
            v = 1,
            origem = "reserva",
            id_anuncio = anuncio.id_anuncio,
            id_comprador = idComprador,
            data_expiracao = dataExpiracaoUtc
        };

        var notif = new Notificacao
        {
            tipo = "anuncio_reservado_vendedor",
            titulo = "O teu anúncio foi reservado",
            texto = string.IsNullOrWhiteSpace(anuncio.titulo)
                ? "Um comprador reservou o teu anúncio."
                : $"Um comprador reservou o teu anúncio: {anuncio.titulo}",
            payload_json = JsonSerializer.Serialize(payload)
        };

        var recebida = new Notificacao_Recebida
        {
            id_utilizador = anuncio.id_vendedor,
            id_notificacaoNavigation = notif,         // EF insere Notificacao e depois Notificacao_Recebida
            data_envio = DateTime.UtcNow,
            canal = NormalizarCanalEmailSms(vendedorPrefs.canal_notificacao_preferido),
            estado = vendedorPrefs.notificar_novidades ? "pendente" : "enviada"
        };

        _marketDb.Notificacao_Recebida.Add(recebida);
    }

    private async Task AdicionarNotif_ReservaCancelada_VendedorAsync(Reserva reserva, Anuncio anuncio)
    {
        var vendedorPrefs = await _marketDb.Utilizador
            .FirstOrDefaultAsync(u => u.id_utilizador == anuncio.id_vendedor);

        if (vendedorPrefs is null) return;

        var payload = new
        {
            v = 1,
            origem = "reserva",
            id_reserva = reserva.id_reserva,
            id_anuncio = reserva.id_anuncio,
            id_comprador = reserva.id_utilizador
        };

        var notif = new Notificacao
        {
            tipo = "reserva_cancelada_vendedor",
            titulo = "Reserva cancelada",
            texto = string.IsNullOrWhiteSpace(anuncio.titulo)
                ? "O comprador cancelou a reserva do teu anúncio."
                : $"O comprador cancelou a reserva do teu anúncio: {anuncio.titulo}",
            payload_json = JsonSerializer.Serialize(payload)
        };

        var recebida = new Notificacao_Recebida
        {
            id_utilizador = anuncio.id_vendedor,
            id_notificacaoNavigation = notif,
            data_envio = DateTime.UtcNow,
            canal = NormalizarCanalEmailSms(vendedorPrefs.canal_notificacao_preferido),
            estado = vendedorPrefs.notificar_novidades ? "pendente" : "enviada"
        };

        _marketDb.Notificacao_Recebida.Add(recebida);
    }

    private async Task<(Anuncio? anuncio, IActionResult? block)> GetMyAnuncioOrBlock(long id, bool blockIfSold)
    {
        var idUtil = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtil == null) return (null, Unauthorized());

        var anuncio = await _marketDb.Anuncio
            .FirstOrDefaultAsync(a => a.id_anuncio == id && a.id_vendedor == idUtil.Value);

        if (anuncio == null) return (null, NotFound());

        if (blockIfSold && string.Equals(anuncio.estado, "vendido", StringComparison.OrdinalIgnoreCase))
            return (anuncio, Forbid()); // 403

        return (anuncio, null);
    }



    // ============================================================
    // NOTIFICAÇÕES — Anúncio pausado/removido (afetados por reserva/visita)
    // ============================================================

    private async Task<List<int>> ObterUtilizadoresAfetadosPorReservaOuVisitaAsync(int idAnuncio)
    {
        var reservadores = await _marketDb.Reserva
            .AsNoTracking()
            .Where(r => r.id_anuncio == idAnuncio && r.estado == "ativa")
            .Select(r => r.id_utilizador)
            .ToListAsync();

        var visitantes = await _marketDb.Visita
            .AsNoTracking()
            .Where(v => v.id_anuncio == idAnuncio && (v.estado == "pendente" || v.estado == "agendada"))
            .Select(v => v.id_utilizador)
            .ToListAsync();

        return reservadores
            .Concat(visitantes)
            .Distinct()
            .ToList();
    }

    private async Task CriarNotificacaoParaVariosAsync(
        IEnumerable<int> idsDestinatarios,
        int idVendedor,
        string tipo,
        string titulo,
        string texto,
        object payload)
    {
        var destinatarios = idsDestinatarios
            .Where(id => id != idVendedor) // nunca notificar o vendedor aqui
            .Distinct()
            .ToList();

        if (destinatarios.Count == 0) return;

        // 1) Criar 1 Notificacao
        var notif = new Notificacao
        {
            tipo = tipo,
            titulo = titulo,
            texto = texto,
            payload_json = JsonSerializer.Serialize(payload)
        };

        _marketDb.Notificacao.Add(notif);
        await _marketDb.SaveChangesAsync(); // gera id_notificacao

        // 2) Obter prefs dos destinatários
        var prefs = await _marketDb.Utilizador
            .AsNoTracking()
            .Where(u => destinatarios.Contains(u.id_utilizador))
            .Select(u => new
            {
                u.id_utilizador,
                u.notificar_novidades,
                u.canal_notificacao_preferido
            })
            .ToListAsync();

        var now = DateTime.UtcNow;

        var rows = prefs.Select(p =>
        {
            var canal = NormalizarCanalEmailSms(p.canal_notificacao_preferido);

            // só email é externo (pendente) e só se o user aceitou
            var estado = (p.notificar_novidades && canal == "email") ? "pendente" : "enviada";

            return new Notificacao_Recebida
            {
                id_utilizador = p.id_utilizador,
                id_notificacao = notif.id_notificacao,
                data_envio = now,
                canal = canal,
                estado = estado
            };
        }).ToList();

        _marketDb.Notificacao_Recebida.AddRange(rows);
        await _marketDb.SaveChangesAsync();
    }



}
