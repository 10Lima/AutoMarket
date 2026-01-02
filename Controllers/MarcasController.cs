using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMarket.Data;
using AutoMarket.Models.Market;
using AutoMarket.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Controllers;

public class MarcasController : Controller
{
    private readonly MarketplaceDbContext _marketDb;
    private readonly CurrentUserService _currentUser;

    public MarcasController(MarketplaceDbContext marketDb, CurrentUserService currentUser)
    {
        _marketDb = marketDb;
        _currentUser = currentUser;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Index(string sort = "az")
    {
        var marcas = await BuildListAsync(sort);
        return View(marcas);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> _Lista(string sort = "az")
    {
        var marcas = await BuildListAsync(sort);
        return PartialView("_MarcasLista", marcas);
    }

    // POST (AJAX): /Marcas/ToggleFavorito
    [HttpPost]
    [Authorize] // ✅ Só autenticados podem favoritar
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFavorito(int idMarca)
    {
        var idUtil = await _currentUser.GetMarketUserIdAsync(User);
        if (idUtil is null) return Unauthorized();

        // Só compradores podem favoritar
        var isComprador = await _marketDb.Comprador.AnyAsync(c => c.id_utilizador == idUtil.Value);
        if (!isComprador) return Forbid();

        var existe = await _marketDb.Comprador_Marca
            .AnyAsync(x => x.id_utilizador == idUtil.Value && x.id_marca == idMarca);

        if (existe)
        {
            var row = await _marketDb.Comprador_Marca
                .FirstAsync(x => x.id_utilizador == idUtil.Value && x.id_marca == idMarca);

            _marketDb.Comprador_Marca.Remove(row);
        }
        else
        {
            _marketDb.Comprador_Marca.Add(new Comprador_Marca
            {
                id_utilizador = idUtil.Value,
                id_marca = idMarca
            });
        }

        await _marketDb.SaveChangesAsync();
        
        // ✅ Devolver também o idMarca
        return Json(new { idMarca, favorito = !existe });
    }

    private async Task<List<Marca>> BuildListAsync(string sort)
    {
        sort = (sort ?? "az").Trim().ToLowerInvariant();
        ViewBag.Sort = sort;

        // ✅ Verificar se está autenticado
        var isAuth = User?.Identity?.IsAuthenticated == true;
        ViewBag.IsAuth = isAuth;

        int? idUtil = null;
        if (isAuth)
            idUtil = await _currentUser.GetMarketUserIdAsync(User);

        // ✅ Verificar se é comprador
        var canFav = false;
        if (idUtil != null)
            canFav = await _marketDb.Comprador.AnyAsync(c => c.id_utilizador == idUtil.Value);

        ViewBag.CanFav = canFav;

        // ✅ Obter favoritos (só se for comprador)
        var favSet = new HashSet<int>();
        if (canFav)
        {
            favSet = (await _marketDb.Comprador_Marca.AsNoTracking()
                .Where(x => x.id_utilizador == idUtil!.Value)
                .Select(x => x.id_marca)
                .ToListAsync()).ToHashSet();
        }
        ViewBag.FavSet = favSet;

        // ✅ Contagem de anúncios por marca
        var counts = await _marketDb.Anuncio.AsNoTracking()
            .Where(a => a.estado == "ativo")
            .GroupBy(a => a.id_veiculoNavigation.id_sub_modeloNavigation.id_modeloNavigation.id_marca)
            .Select(g => new { id_marca = g.Key, total = g.Count() })
            .ToListAsync();

        ViewBag.CountByMarca = counts.ToDictionary(x => x.id_marca, x => x.total);

        // ✅ Lista de marcas
        var marcas = await _marketDb.Marca.AsNoTracking().ToListAsync();

        if (sort == "top")
        {
            var dict = (Dictionary<int, int>)ViewBag.CountByMarca;
            marcas = marcas
                .OrderByDescending(m => dict.TryGetValue(m.id_marca, out var t) ? t : 0)
                .ThenBy(m => m.nome)
                .ToList();
        }
        else
        {
            marcas = marcas.OrderBy(m => m.nome).ToList();
        }

        return marcas;
    }
}
