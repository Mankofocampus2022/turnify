using System;
using System.Threading.Tasks;

namespace Turnify.Api.Interfaces
{
    public interface IDashboardService
    {
        // 🚩 AJUSTE SENIOR: Sincronizamos con la implementación para incluir mes y anio.
        // Esto soluciona el error CS0535 al permitir filtros específicos de mes y año.
        Task<object> GetResumenDiarioAsync(Guid proveedorId, DateTime? fecha, string periodo = "hoy", int? mes = null, int? anio = null);

        // Resumen mensual para reportes específicos
        Task<object> GetResumenMensualAsync(Guid proveedorId);
    }
}