using AutoMarket.Data;
using AutoMarket.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Controllers;

[Authorize]
public class UtilizadoresController : Controller
{
    private readonly MarketplaceDbContext _marketDb;
    private readonly ApplicationDbContext _identityDb;
    private readonly CurrentUserService _currentUser;

    public UtilizadoresController(MarketplaceDbContext marketDb, ApplicationDbContext identityDb, CurrentUserService currentUser)
    {
        _marketDb = marketDb;
        _identityDb = identityDb;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Retorna partial view com info básica do utilizador (nome, username, email, contacto).
    /// Acessível apenas por admins ou quando o utilizador consulta outro envolvido numa transação (visita/reserva/encomenda).
    /// </summary>
    // GET: /Utilizadores/InfoCard?id=123
    [HttpGet]
    public async Task<IActionResult> InfoCard(int id)
    {
        var idAtual = await _currentUser.GetMarketUserIdAsync(User);
        if (idAtual is null) return Unauthorized();

        var isAdmin = await _currentUser.IsAdminAsync(User);

        // ✅ Se não for admin, validar se tem relação com o utilizador
        if (!isAdmin)
        {
            var hasRelacao = await TemRelacaoComUtilizadorAsync(idAtual.Value, id);
            if (!hasRelacao)
                return Forbid();
        }

        // Carregar dados do utilizador
        var utilizador = await _marketDb.Utilizador
            .AsNoTracking()
            .Include(u => u.Utilizador_Contacto)
            .FirstOrDefaultAsync(u => u.id_utilizador == id);

        if (utilizador is null)
            return NotFound();

        // ✅ Identity (username + email)
        string? username = null;
        string? email = null;

        if (!string.IsNullOrWhiteSpace(utilizador.identity_user_id))
        {
            var ident = await _identityDb.Users
                .AsNoTracking()
                .Where(u => u.Id == utilizador.identity_user_id)
                .Select(u => new { u.UserName, u.Email })
                .FirstOrDefaultAsync();

            if (ident != null)
            {
                username = ident.UserName;
                email = ident.Email;
            }
        }

        ViewData["Username"] = username;
        ViewData["Email"] = email;

        return PartialView("_UserInfoCard", utilizador);
    }

    private async Task<bool> TemRelacaoComUtilizadorAsync(int idAtual, int idOutro)
    {
        if (idAtual == idOutro) return true;

        // ------------------------------------------------------------
        // 1) Visitas: comprador (id_utilizador) <-> vendedor (dono anúncio)
        // ------------------------------------------------------------
        var relVisita = await _marketDb.Visita
            .AsNoTracking()
            .AnyAsync(v =>
                (v.id_utilizador == idAtual && v.id_anuncioNavigation.id_vendedor == idOutro) ||
                (v.id_utilizador == idOutro && v.id_anuncioNavigation.id_vendedor == idAtual)
            );

        if (relVisita) return true;

        // ------------------------------------------------------------
        // 2) Reservas: reservante (id_utilizador) <-> vendedor (dono anúncio)
        // ------------------------------------------------------------
        var relReserva = await _marketDb.Reserva
            .AsNoTracking()
            .AnyAsync(r =>
                (r.id_utilizador == idAtual && r.id_anuncioNavigation.id_vendedor == idOutro) ||
                (r.id_utilizador == idOutro && r.id_anuncioNavigation.id_vendedor == idAtual)
            );

        if (relReserva) return true;

        // ------------------------------------------------------------
        // 3) Encomendas: comprador (id_utilizador) <-> vendedor (dono anúncio)
        // ------------------------------------------------------------
        var relEncomenda = await _marketDb.Encomenda
            .AsNoTracking()
            .AnyAsync(e =>
                (e.id_utilizador == idAtual && e.id_anuncioNavigation.id_vendedor == idOutro) ||
                (e.id_utilizador == idOutro && e.id_anuncioNavigation.id_vendedor == idAtual)
            );

        return relEncomenda;
    }

}
