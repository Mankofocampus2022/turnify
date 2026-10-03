using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;

namespace Turnify.Api.Controllers
{
    /// <summary>
    /// API RESTful para la integración de Business Intelligence (Power BI, Looker Studio, etc.) (HU-REP02).
    /// Protegida mediante autenticación por X-Api-Key desacoplada de JWT.
    /// </summary>
    [AllowAnonymous]
    [Route("api/v1/bi")]
    [ApiController]
    public class BiController : ControllerBase
    {
        private readonly TurnifyDbContext _context;
        private const string EXPECTED_API_KEY = "Turnify-BI-Key-2026-SecureSecret";

        public BiController(TurnifyDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Endpoint seguro para la extracción de métricas de suscriptores, ciclo de vida, MRR y LTV para Power BI.
        /// GET /api/v1/bi/subscribers-lifecycle
        /// Requiere cabecera: X-Api-Key: Turnify-BI-Key-2026-SecureSecret
        /// </summary>
        [HttpGet("subscribers-lifecycle")]
        public async Task<IActionResult> GetSubscribersLifecycle(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string estado = "todos",
            [FromQuery] string categoria = "todos")
        {
            // 🛡️ 1. Validación de Autenticación por API Key en cabecera
            if (!Request.Headers.TryGetValue("X-Api-Key", out var extractedApiKey) ||
                string.IsNullOrWhiteSpace(extractedApiKey) ||
                !string.Equals(extractedApiKey.ToString().Trim(), EXPECTED_API_KEY, StringComparison.Ordinal))
            {
                return Unauthorized(new 
                { 
                    status = 401,
                    message = "Acceso denegado. Se requiere una cabecera 'X-Api-Key' válida para consumir la API de BI." 
                });
            }

            // Normalización defensiva de paginación
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            DateTime hoyUtc = DateTime.UtcNow.Date;

            // 🚀 2. Consulta de Proveedores y Suscripciones en modo Solo Lectura (AsNoTracking)
            List<object> proveedoresRaw;
            try
            {
                var provsInclude = await _context.proveedores
                    .AsNoTracking()
                    .Include(p => p.Suscripciones)
                    .ToListAsync();
                proveedoresRaw = provsInclude.Cast<object>().ToList();
            }
            catch
            {
                var provsSinInclude = await _context.proveedores
                    .AsNoTracking()
                    .ToListAsync();
                proveedoresRaw = provsSinInclude.Cast<object>().ToList();
            }

            // 📊 3. Transformación y cálculo del Ciclo de Vida, MRR y LTV
            var todosLosSuscriptores = proveedoresRaw.Select(p =>
            {
                var tP = p.GetType();
                var provId = (Guid)(tP.GetProperty("Id")?.GetValue(p) ?? Guid.Empty);

                bool esIndependiente = false;
                var propEsInd = tP.GetProperty("EsIndependiente") ?? tP.GetProperty("es_independiente");
                if (propEsInd != null && bool.TryParse(propEsInd.GetValue(p)?.ToString(), out var bInd))
                {
                    esIndependiente = bInd;
                }

                // Colección de suscripciones del usuario
                IEnumerable<object>? suscripcionesCol = null;
                var propSusc = tP.GetProperty("Suscripciones") ?? tP.GetProperty("suscripciones");
                if (propSusc != null)
                {
                    suscripcionesCol = propSusc.GetValue(p) as IEnumerable<object>;
                }

                List<object> listaSuscripciones = suscripcionesCol?.ToList() ?? new List<object>();

                // Suscripción más reciente
                object? ultSuscripcion = listaSuscripciones
                    .OrderByDescending(s => GetFecha(s, "FechaFin", "fecha_fin", "FechaVencimiento", hoyUtc.AddDays(30)))
                    .FirstOrDefault();

                // Fecha de Alta (primera suscripción o fecha de creación del proveedor)
                DateTime fechaAlta = listaSuscripciones.Any()
                    ? listaSuscripciones.Min(s => GetFecha(s, "FechaInicio", "fecha_inicio", "FechaCreacion", hoyUtc))
                    : GetFecha(p, "FechaCreacion", "fecha_creacion", "CreatedAt", hoyUtc.AddDays(-30));

                DateTime fechaVencimiento = GetFecha(ultSuscripcion, "FechaFin", "fecha_fin", "FechaVencimiento", hoyUtc.AddDays(30));
                int diasRestantes = (fechaVencimiento.Date - hoyUtc.Date).Days;
                bool estaActiva = GetBool(ultSuscripcion, "Estado", "estado", "Activa", true);

                // Cálculo de Estado
                string estadoCalculado = "Activa";
                if (ultSuscripcion != null && !estaActiva)
                {
                    estadoCalculado = "Cancelada";
                }
                else if (diasRestantes < 0)
                {
                    estadoCalculado = "Vencida";
                }
                else if (diasRestantes <= 7)
                {
                    estadoCalculado = "Próximo a Vencer";
                }

                // Categoría de Ciclo de Vida
                string categoriaCicloVida;
                if (estadoCalculado == "Cancelada" || estadoCalculado == "Vencida")
                {
                    categoriaCicloVida = "Churn";
                }
                else if (diasRestantes <= 7)
                {
                    categoriaCicloVida = "En Riesgo";
                }
                else if ((hoyUtc - fechaAlta).TotalDays <= 30)
                {
                    categoriaCicloVida = "Nuevo";
                }
                else
                {
                    categoriaCicloVida = "Retenido";
                }

                // Cálculo de MRR y LTV
                decimal mrr = GetDecimal(ultSuscripcion, "MontoPagado", "monto_pagado", "Precio", 0m);
                decimal ltv = listaSuscripciones.Sum(s => GetDecimal(s, "MontoPagado", "monto_pagado", "Precio", 0m));
                if (ltv == 0m) ltv = mrr;

                string plan = GetString(ultSuscripcion, "PlanAdquirido", "plan_adquirido", "Plan", esIndependiente ? "Plan Independiente Pro" : "Plan Estándar Pro");
                string clienteNombre = GetString(p, "NombreComercial", "nombre_comercial", "Nombre", "Suscriptor General");
                string email = GetString(p, "Email", "email", "Correo", "Sin correo registrado");
                string telefono = GetString(p, "Telefono", "telefono", "Celular", "No registrado");

                return new
                {
                    SubscriberId = provId,
                    ClienteNombre = clienteNombre,
                    Email = email,
                    Telefono = telefono,
                    ModeloNegocio = esIndependiente ? "Independiente" : "Estandar",
                    PlanActual = plan,
                    EstadoSuscripcion = estadoCalculado,
                    CategoriaCicloVida = categoriaCicloVida,
                    FechaAltaUtc = fechaAlta.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    FechaVencimientoUtc = fechaVencimiento.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    DiasRestantes = diasRestantes,
                    Mrr = mrr,
                    Ltv = ltv
                };
            }).ToList();

            // 🎯 4. Aplicación de Filtros
            var queryFiltrada = todosLosSuscriptores.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(estado) && !estado.Equals("todos", StringComparison.OrdinalIgnoreCase))
            {
                string normEst = estado.Trim().ToLower().Replace("_", " ");
                queryFiltrada = queryFiltrada.Where(s => s.EstadoSuscripcion.ToLower().Replace(" ", "") == normEst.Replace(" ", "") ||
                                                         s.EstadoSuscripcion.ToLower().Contains(normEst));
            }

            if (!string.IsNullOrWhiteSpace(categoria) && !categoria.Equals("todos", StringComparison.OrdinalIgnoreCase))
            {
                string normCat = categoria.Trim().ToLower().Replace("_", " ");
                queryFiltrada = queryFiltrada.Where(s => s.CategoriaCicloVida.ToLower() == normCat);
            }

            var listaResultadosable = queryFiltrada.ToList();
            int totalRecords = listaResultadosable.Count;
            int totalPages = (int)Math.Ceiling((double)totalRecords / pageSize);

            // Paginación
            var itemsPaginados = listaResultadosable
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // 📈 5. Métricas Globales para Power BI
            decimal totalMRR = todosLosSuscriptores.Sum(s => s.Mrr);
            decimal promedioLTV = todosLosSuscriptores.Any() ? todosLosSuscriptores.Average(s => s.Ltv) : 0m;
            int suscriptoresActivosCount = todosLosSuscriptores.Count(s => s.EstadoSuscripcion == "Activa" || s.EstadoSuscripcion == "Próximo a Vencer");
            int churnCount = todosLosSuscriptores.Count(s => s.CategoriaCicloVida == "Churn");
            double churnRatePercent = todosLosSuscriptores.Any() ? Math.Round((double)churnCount / todosLosSuscriptores.Count * 100, 2) : 0;

            // 📦 6. Respuesta JSON Estandarizada ISO 8601
            return Ok(new
            {
                meta = new
                {
                    page = page,
                    pageSize = pageSize,
                    totalRecords = totalRecords,
                    totalPages = totalPages,
                    timestampUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                },
                kpis = new
                {
                    totalMRR = Math.Round(totalMRR, 2),
                    averageLTV = Math.Round(promedioLTV, 2),
                    activeSubscribersCount = suscriptoresActivosCount,
                    churnRatePercent = churnRatePercent
                },
                data = itemsPaginados
            });
        }

        // =========================================================================
        // 🛠️ MÉTODOS AUXILIARES DE REFLEXIÓN DEFENSIVA
        // =========================================================================

        private static DateTime GetFecha(object? obj, string p1, string p2, string p3, DateTime fallback)
        {
            if (obj == null) return fallback;
            var t = obj.GetType();
            var prop = t.GetProperty(p1) ?? t.GetProperty(p2) ?? t.GetProperty(p3);
            if (prop != null)
            {
                var val = prop.GetValue(obj);
                if (val is DateTime dt) return dt;
                if (val is DateTimeOffset dto) return dto.DateTime;
                if (val != null && DateTime.TryParse(val.ToString(), out var parsed)) return parsed;
            }
            return fallback;
        }

        private static string GetString(object? obj, string p1, string p2, string p3, string fallback)
        {
            if (obj == null) return fallback;
            var t = obj.GetType();
            var prop = t.GetProperty(p1) ?? t.GetProperty(p2) ?? t.GetProperty(p3);
            var val = prop?.GetValue(obj)?.ToString();
            return !string.IsNullOrWhiteSpace(val) ? val : fallback;
        }

        private static decimal GetDecimal(object? obj, string p1, string p2, string p3, decimal fallback)
        {
            if (obj == null) return fallback;
            var t = obj.GetType();
            var prop = t.GetProperty(p1) ?? t.GetProperty(p2) ?? t.GetProperty(p3);
            if (prop != null)
            {
                var val = prop.GetValue(obj);
                if (val != null && decimal.TryParse(val.ToString(), out var m)) return m;
            }
            return fallback;
        }

        private static bool GetBool(object? obj, string p1, string p2, string p3, bool fallback)
        {
            if (obj == null) return fallback;
            var t = obj.GetType();
            var prop = t.GetProperty(p1) ?? t.GetProperty(p2) ?? t.GetProperty(p3);
            if (prop != null)
            {
                var val = prop.GetValue(obj);
                if (val is bool b) return b;
                if (val != null && bool.TryParse(val.ToString(), out var parsedB)) return parsedB;
                if (val != null && int.TryParse(val.ToString(), out var parsedI)) return parsedI == 1;
            }
            return fallback;
        }
    }
}