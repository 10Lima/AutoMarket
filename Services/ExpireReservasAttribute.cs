using Microsoft.AspNetCore.Mvc;

namespace AutoMarket.Services
{
    // Usa DI para resolver ExpireReservasFilter automaticamente
    public class ExpireReservasAttribute : TypeFilterAttribute
    {
        public ExpireReservasAttribute() : base(typeof(ExpireReservasFilter))
        {
        }
    }
}
