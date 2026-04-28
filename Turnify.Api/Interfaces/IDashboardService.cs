using System;
using System.Threading.Tasks;

namespace Turnify.Api.Interfaces
{
    public interface IDashboardService
    {
        // 🚩 AJUSTE SENIOR: Agregamos 'string periodo = "hoy"' para que coincida 
        // exactamente con la implementación en el DashboardService.
        // Esto permite que el Controller pase el filtro (hoy, semana, mes) sin errores.
        Task<object> GetResumenDiarioAsync(Guid proveedorId, DateTime? fecha, string periodo = "hoy");

        // Resumen mensual para reportes específicos
        Task<object> GetResumenMensualAsync(Guid proveedorId);
    }
}