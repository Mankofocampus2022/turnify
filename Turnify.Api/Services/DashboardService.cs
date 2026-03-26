using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.Interfaces;
using Turnify.Api.Dtos;

namespace Turnify.Api.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly TurnifyDbContext _context;

        public DashboardService(TurnifyDbContext context)
        {
            _context = context;
        }

        public async Task<object> GetResumenDiarioAsync(Guid proveedorId, DateTime? fecha)
        {
            var fechaConsulta = fecha?.Date ?? DateTime.Today;

            // 1. Buscamos en 'citas' (usando las propiedades que el build anterior ya aceptó)
            var citasDia = await _context.citas 
                .Where(c => c.ProveedorId == proveedorId && c.Fecha.Date == fechaConsulta && c.Estado != "cancelada")
                .Include(c => c.Servicio)
                .Include(c => c.Cliente) 
                .ToListAsync();

            // 2. CORREGIDO: Usamos 'fecha_creacion' en minúscula como pide el error
            var nuevosClientesHoy = await _context.clientes
                .CountAsync(cl => cl.fecha_creacion.Date == fechaConsulta);

            // 3. Cálculos de ganancias
            var gananciaReal = citasDia.Where(c => c.Estado == "completada").Sum(c => c.PrecioPactado);
            var gananciaEstimada = citasDia.Sum(c => c.PrecioPactado);

            return new
            {
                Fecha = fechaConsulta.ToShortDateString(),
                TotalCitas = citasDia.Count,
                NuevosClientes = nuevosClientesHoy,
                GananciaEstimada = gananciaEstimada,
                GananciaReal = gananciaReal,
                Pendientes = citasDia.Count(c => c.Estado == "pendiente"),
                Completadas = citasDia.Count(c => c.Estado == "completada"),
                
                // 4. CORREGIDO: Usamos 'nombre' en minúscula para el cliente
                ProximasCitas = citasDia
                    .Where(c => c.Estado == "pendiente")
                    .OrderBy(c => c.Hora)
                    .Take(5)
                    .Select(c => new {
                        Hora = c.Hora.ToString(), 
                        Cliente = c.Cliente != null ? c.Cliente.nombre : "Cliente Anónimo",
                        Servicio = c.Servicio != null ? c.Servicio.Nombre : "N/A",
                        Estado = c.Estado
                    }).ToList()
            };
        }
    }
}