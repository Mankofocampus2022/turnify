using System;
using System.Threading.Tasks;

namespace Turnify.Api.Interfaces
{
    public interface IDashboardService
    {
        /// <summary>
        /// Obtiene el resumen diario/periódico de ingresos y métricas para un proveedor.
        /// Sincronizado con filtros por fecha exacta, rango, mes y año.
        /// </summary>
        Task<object> GetResumenDiarioAsync(
            Guid proveedorId, 
            DateTime? fecha, 
            string periodo = "diario", 
            int? mes = null, 
            int? anio = null
        );

        /// <summary>
        /// Resumen consolidado mensual para reportes financieros y analítica avanzada.
        /// </summary>
        Task<object> GetResumenMensualAsync(Guid proveedorId);

        /// <summary>
        /// 🚀 HU-001 / MULTI-SILLA: Contrato para la liquidación financiera del personal en comisión (Staff).
        /// </summary>
        Task<object> GetLiquidacionStaffAsync(
            Guid empleadoId, 
            DateTime fechaBase, 
            string periodo = "diario", 
            int? mes = null, 
            int? anio = null
        );

        /// <summary>
        /// 💈 HU-06 & HU-07: Resumen del Panel de Control exclusivo para el Profesional Independiente.
        /// Garantiza ingresos 100% brutos y cálculo de distinción de clientes nuevos vs. recurrentes.
        /// </summary>
        Task<object> GetDashboardIndependienteAsync(
            Guid proveedorId, 
            DateTime fechaBase, 
            string periodo = "diario", 
            int? mes = null, 
            int? anio = null
        );
    }
}