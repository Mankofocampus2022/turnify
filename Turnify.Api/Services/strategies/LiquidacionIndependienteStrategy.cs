using Turnify.Api.Models.DTOs;

namespace Turnify.Api.Services.Strategies
{
    public class LiquidacionIndependienteStrategy : ILiquidacionStrategy
    {
        public DetalleMovimientoDto CalcularMovimiento(
            Guid citaId,
            DateTime fecha,
            string clienteNombre,
            string servicioNombre,
            decimal montoTotal,
            decimal porcentajeComision,
            string estado,
            string? especialistaNombre = null)
        {
            // Regla HU-21: Retención del 100% de la tarifa cobrada, sin deducción de comisión.
            return new DetalleMovimientoDto
            {
                CitaId = citaId,
                Fecha = fecha,
                Cliente = clienteNombre,
                Servicio = servicioNombre,
                Especialista = especialistaNombre ?? "Independiente",
                MontoTotal = montoTotal,
                PorcentajeComision = 0m, // 0% de comisión aplicada
                MontoComisionEspecialista = 0m,
                IngresoNeto = montoTotal, // 100% del ingreso retenido por el profesional
                Estado = estado,
                TipoModelo = "Independiente"
            };
        }
    }
}