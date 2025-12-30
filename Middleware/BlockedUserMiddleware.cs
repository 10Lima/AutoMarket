using AutoMarket.Data;
using AutoMarket.Services;
using Microsoft.EntityFrameworkCore;

namespace AutoMarket.Middleware;

public class BlockedUserMiddleware
{
    private readonly RequestDelegate _next;

    public BlockedUserMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context, MarketplaceDbContext marketDb, CurrentUserService currentUser)
    {
        var path = context.Request.Path.Value ?? "";

        // ✅ Permitir certas rotas mesmo para utilizadores bloqueados
        if (IsWhitelistedPath(path))
        {
            await _next(context);
            return;
        }

        // Só utilizadores autenticados
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var idUtilizador = await currentUser.GetMarketUserIdAsync(context.User);

            if (idUtilizador.HasValue)
            {
                var estado = await marketDb.Utilizador.AsNoTracking()
                    .Where(u => u.id_utilizador == idUtilizador.Value)
                    .Select(u => u.estado)
                    .FirstOrDefaultAsync();

                // Se bloqueado, redireciona (evita loop)
                if (string.Equals(estado, "bloqueado", StringComparison.OrdinalIgnoreCase))
                {
                    if (!path.StartsWith("/Acesso/Bloqueado", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Response.Redirect("/Acesso/Bloqueado");
                        return;
                    }
                }
            }
        }

        await _next(context);
    }

    private static bool IsWhitelistedPath(string path)
    {
        // Página de bloqueio (já tinhas)
        if (path.StartsWith("/Acesso/Bloqueado", StringComparison.OrdinalIgnoreCase))
            return true;

        // ✅ Logout/Login (Identity UI)
        if (path.StartsWith("/Identity/Account/Logout", StringComparison.OrdinalIgnoreCase))
            return true;

        if (path.StartsWith("/Identity/Account/Login", StringComparison.OrdinalIgnoreCase))
            return true;

        // (Opcional) se quiseres permitir recuperar password
        if (path.StartsWith("/Identity/Account/ForgotPassword", StringComparison.OrdinalIgnoreCase))
            return true;

        if (path.StartsWith("/Identity/Account/ResetPassword", StringComparison.OrdinalIgnoreCase))
            return true;

        // ✅ Static files (senão a página bloqueada pode vir “sem CSS/JS”)
        if (path.StartsWith("/css", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/js", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/lib", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/images", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/favicon", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
