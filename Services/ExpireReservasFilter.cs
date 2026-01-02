using AutoMarket.Data;
using AutoMarket.Models.Market;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AutoMarket.Services
{
    public class ExpireReservasFilter : IAsyncActionFilter
    {
        private readonly MarketplaceDbContext _db;

        public ExpireReservasFilter(MarketplaceDbContext db)
        {
            _db = db;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var agora = DateTime.UtcNow;

            var vencidas = await _db.Reserva
                .Include(r => r.id_anuncioNavigation)
                .Where(r => r.estado == "ativa" && r.data_expiracao <= agora)
                .ToListAsync();

            if (vencidas.Count > 0)
            {
                foreach (var r in vencidas)
                {
                    r.estado = "expirada";

                    var a = r.id_anuncioNavigation;
                    if (a != null && string.Equals(a.estado, "reservado", StringComparison.OrdinalIgnoreCase))
                    {
                        a.estado = "ativo";
                        a.data_atualizacao = agora;
                    }
                }

                await _db.SaveChangesAsync();
            }

            await next();
        }
    }
}
