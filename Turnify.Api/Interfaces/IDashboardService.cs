using System;
using System.Threading.Tasks;

namespace Turnify.Api.Interfaces
{
    public interface IDashboardService
    {
        // 🚩 Sincroniza con la implementación para incluir mes y año.
        // Esto soluciona el error CS0535 al permitir filtros específicos de mes y año.
        Task<object> GetResumenDiarioAsync(Guid proveedorId, DateTime? fecha, string periodo = "hoy", int? mes = null, int? anio = null);

        // Resumen mensual para reportes específicos
        Task<object> GetResumenMensualAsync(Guid proveedorId);

        // 🚀 HU 001: Contrato para la liquidación financiera global del Staff
        Task<object> GetLiquidacionStaffAsync(Guid empleadoId, DateTime fechaBase, string periodo, int? mes = null, int? anio = null);

        // 💈 HU-06 y HU-07: Resumen del Panel de Control exclusivo para el Profesional Independiente
        // Permite ingresos 100% brutos y cálculo de distinción de clientes nuevos vs habituales.
        Task<object> GetDashboardIndependienteAsync(Guid proveedorId, DateTime fechaBase, string periodo = "diario", int? mes = null, int? anio = null);
    }
}