using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.DTOs;

namespace Turnify.Api.Controllers
{
    [ApiController]
    [Route("api/v1/superadmin")]
    [Authorize(Roles = "SuperAdministrador")]
    public class SuperAdminController : ControllerBase
    {
        private readonly TurnifyDbContext _context;

        public SuperAdminController(TurnifyDbContext context)
        {
            _context = context;
        }

        [HttpGet("metrics/subscriptions")]
        public async Task<ActionResult<SuperAdminMetricsResponseDto>> GetSubscriptionMetrics()
        {
            var now = DateTimeOffset.UtcNow;
            var alertThreshold = now.AddDays(7);

            // 1. Obtener métricas de suscripciones próximas a vencer (próximos 7 días)
            var expiringQuery = await _context.suscripciones
                .Include(s => s.Proveedor)
                .Include(s => s.Plan)
                .Where(s => s.Activo && s.FechaFin >= now && s.FechaFin <= alertThreshold)
                .Select(s => new ExpiringSubscriptionDto
                {
                    SuscripcionId = s.Id,
                    ProveedorNombre = s.Proveedor.NombreComercial ?? "Sin Marca",
                    PlanNombre = s.Plan.Nombre,
                    FechaFin = s.FechaFin,
                    DiasRestantes = (s.FechaFin - now).Days
                })
                .ToListAsync();

            // 2. Cálculo de ingresos totales (Revenue acumulado en planes pagados activos)
            var totalRevenue = await _context.suscripciones
                .Where(s => s.Activo)
                .SumAsync(s => (decimal?)s.Plan.PrecioMensual) ?? 0m;

            // 3. Distribución de usuarios por rol
            var rolesDistribution = await _context.roles
                .Select(r => new RoleDistributionDto
                {
                    RolNombre = r.nombre,
                    TotalUsuarios = _context.usuarios.Count(u => u.rol_id == r.id)
                })
                .ToListAsync();

            var response = new SuperAdminMetricsResponseDto
            {
                TotalRevenue = totalRevenue,
                TotalActiveSubscriptions = await _context.suscripciones.CountAsync(s => s.Activo),
                ExpiringSoonCount = expiringQuery.Count,
                ExpiringSubscriptions = expiringQuery,
                RolesDistribution = rolesDistribution
            };

            return Ok(response);
        }
    }
}