using Turnify.Api.Models.DTOs;

namespace Turnify.Api.Services.Strategies
{
    public class LiquidacionDependienteStrategy : ILiquidacionStrategy
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
            // Regla HU-20: Se deduce la comisión del especialista.
            // El negocio percibe (Total - Comisión) y al especialista se le liquida su % o monto correspondiente.
            decimal comisionEspecialista = montoTotal * (porcentajeComision / 100m);
            decimal ingresoNetoNegocio = montoTotal - comisionEspecialista;

            return new DetalleMovimientoDto
            {
                CitaId = citaId,
                Fecha = fecha,
                Cliente = clienteNombre,
                Servicio = servicioNombre,
                Especialista = especialistaNombre ?? "No asignado",
                MontoTotal = montoTotal,
                PorcentajeComision = porcentajeComision,
                MontoComisionEspecialista = comisionEspecialista,
                IngresoNeto = ingresoNetoNegocio,
                Estado = estado,
                TipoModelo = "Dependiente"
            };
        }
    }
}