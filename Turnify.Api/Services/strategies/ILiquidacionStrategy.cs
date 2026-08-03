using Turnify.Api.Models.DTOs;

namespace Turnify.Api.Services.Strategies
{
    public interface ILiquidacionStrategy
    {
        /// <summary>
        /// Aplica las reglas financieras de liquidación según el tipo de modelo de negocio (Dependiente / Independiente).
        /// </summary>
        DetalleMovimientoDto CalcularMovimiento(
            Guid citaId,
            DateTime fecha,
            string clienteNombre,
            string servicioNombre,
            decimal montoTotal,
            decimal porcentajeComision,
            string estado,
            string? especialistaNombre = null);
    }
}