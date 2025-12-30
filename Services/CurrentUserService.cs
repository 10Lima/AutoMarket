using System.Security.Claims;
using AutoMarket.Data;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Services;

public class CurrentUserService
{
    private readonly MarketplaceDbContext _marketDb;

    public CurrentUserService(MarketplaceDbContext marketDb)
    {
        _marketDb = marketDb;
    }

    public string? GetIdentityUserId(ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.NameIdentifier);

    public async Task<int?> GetMarketUserIdAsync(ClaimsPrincipal user)
    {
        var identityId = GetIdentityUserId(user);
        if (string.IsNullOrWhiteSpace(identityId)) return null;

        return await _marketDb.Utilizador
            .Where(u => u.identity_user_id == identityId)
            .Select(u => (int?)u.id_utilizador)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> IsAdminAsync(ClaimsPrincipal user)
    {
        var idUtilizador = await GetMarketUserIdAsync(user);
        if (idUtilizador is null) return false;

        return await _marketDb.Administrador.AnyAsync(a => a.id_utilizador == idUtilizador.Value);
    }
}
