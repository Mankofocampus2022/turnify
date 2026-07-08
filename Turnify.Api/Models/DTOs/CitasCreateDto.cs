using System;
using System.ComponentModel.DataAnnotations;

namespace Turnify.Api.Models.DTOs
{
    public class CitaCreateDto
    {
        // --- TUS CAMPOS ORIGINALES (INTACTOS Y BLINDADOS) ---
        [Required(ErrorMessage = "El campo ClienteId es estrictamente obligatorio.")]
        public Guid ClienteId { get; set; }
        
        [Required(ErrorMessage = "El campo ProveedorId es estrictamente obligatorio.")]
        public Guid ProveedorId { get; set; }
        
        [Required(ErrorMessage = "El campo ServicioId es estrictamente obligatorio.")]
        public Guid ServicioId { get; set; }
        
        [Required(ErrorMessage = "La fecha de la reserva es requerida.")]
        public DateTime Fecha { get; set; }
        
        [Required(ErrorMessage = "La hora de la reserva es requerida.")]
        public TimeSpan Hour { get; set; } // Nota: Mantenemos el mapeo físico de tu propiedad
        public TimeSpan Hora { get => Hour; set => Hour = value; } // Alias seguro para compatibilidad de reflejo
        
        [Required(ErrorMessage = "La modalidad de la cita es obligatoria.")]
        [StringLength(20, ErrorMessage = "La modalidad no puede superar los 20 caracteres.")]
        [RegularExpression(@"^(local|domicilio)$", ErrorMessage = "La modalidad solo acepta los valores 'local' o 'domicilio'.")]
        public string Modalidad { get; set; } = "local"; // "local" o "domicilio"
        
        // 🛡️ BLINDAJE TC-002: Límite estricto a cadenas de dirección para mitigar desbordamientos
        [StringLength(255, ErrorMessage = "La dirección física excede el límite corporativo permitido de 255 caracteres.")]
        [RegularExpression(@"^[a-zA-Z0-9áéíóúÁÉÍÓÚñÑ\s\.,#\-]*$", ErrorMessage = "La dirección contiene caracteres especiales no permitidos para seguridad física.")]
        public string? Direccion { get; set; }
        
        // 🛡️ BLINDAJE TC-002: Control de ráfagas de texto en comentarios de usuario
        [StringLength(500, ErrorMessage = "El campo observaciones no puede superar el umbral de 500 caracteres.")]
        public string? Observaciones { get; set; }
        
        [Range(0, 9999999.99, ErrorMessage = "El precio pactado debe ser un valor positivo válido.")]
        public decimal PrecioPactado { get; set; }
        
        [Range(1, 1440, ErrorMessage = "La duración pactada debe estar entre 1 minuto y 1440 minutos (24 horas).")]
        public int DuracionPactadaMin { get; set; }

        // --- 🚩 ADICIONES PARA EL BOSS (QR & DOMICILIOS) ---
        
        // Para tracking: "QR", "Web", "Manual"
        [Required(ErrorMessage = "El método de registro es requerido para métricas del dashboard.")]
        [StringLength(30, ErrorMessage = "El método de registro no puede superar los 30 caracteres.")]
        [RegularExpression(@"^(QR|Web|Manual)$", ErrorMessage = "El método de registro únicamente admite 'QR', 'Web' o 'Manual'.")]
        public string MetodoRegistro { get; set; } = "Web";

        // Coordenadas para que el profesional use Google Maps/Waze
        // 🛡️ BLINDAJE: Validación de rangos geográficos reales de la tierra (-90 a 90 grados)
        [Range(-90.0, 90.0, ErrorMessage = "La latitud ingresada debe estar dentro del rango geográfico real (-90 a 90).")]
        public decimal? Latitud { get; set; }
        
        // 🛡️ BLINDAJE: Validación de rangos geográficos reales de la tierra (-180 a 180 grados)
        [Range(-180.0, 180.0, ErrorMessage = "La longitud ingresada debe estar dentro del rango geográfico real (-180 a 180).")]
        public decimal? Longitud { get; set; }

        // Si el barbero cobra un extra por ir hasta la casa
        [Range(0, 500000.00, ErrorMessage = "El costo de domicilio no puede ser negativo ni exceder las cuotas del mercado.")]
        public decimal CostoDomicilio { get; set; } = 0;

        // --- 🛡️ BLINDAJE EXTRA PARA MULTI-NEGOCIO (No daña funcionalidad) ---

        // 🚩 [NUEVO] Para validar si el barbero está agendando desde su propia cuenta o si es un cliente externo.
        public Guid? UsuarioCreadorId { get; set; }

        // 🚩 [NUEVO] Versión del DTO para asegurar compatibilidad con el JSON del Front
        [StringLength(15, ErrorMessage = "La versión de la aplicación no corresponde a un formato válido.")]
        public string? VersionApp { get; set; } = "1.0.2";

        // --- 🚀 [NUEVO GUEST CHECKOUT] - BLINDAJE PARA CLIENTES ANÓNIMOS (INVITADOS QR) ---
        
        // 🛡️ BLINDAJE TC-002: Evita el ingreso de payloads maliciosos de texto en nombres de invitados
        [StringLength(100, ErrorMessage = "El nombre del cliente anónimo no puede superar los 100 caracteres.")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]*$", ErrorMessage = "El nombre del invitado solo admite caracteres alfabéticos y espacios.")]
        public string? AnonimoNombre { get; set; }
        
        // 🛡️ BLINDAJE TC-002: Validación estructural estricta de correo electrónico RFC antes de persistencia
        [StringLength(150, ErrorMessage = "El correo electrónico del cliente anónimo supera el estándar de 150 caracteres.")]
        [EmailAddress(ErrorMessage = "La dirección de correo electrónico anónimo no posee un formato estructural válido.")]
        public string? AnonimoEmail { get; set; }
        
        // 🛡️ BLINDAJE TC-002: Control sobre strings de teléfonos para evitar saturación de RAM
        [StringLength(20, ErrorMessage = "La cadena del número de WhatsApp/Teléfono excede el límite de 20 caracteres.")]
        [RegularExpression(@"^\+?[0-9]*$", ErrorMessage = "El número telefónico de WhatsApp de invitados solo admite caracteres numéricos y el prefijo '+'.")]
        public string? AnonimoWhatsApp { get; set; }

        // --- 🚀 HU 001 - MULTI-SILLA: NUEVOS CAMPOS DE VINCULACIÓN OPERATIVA ---
        // Se establecen como opcionales con 'Guid?' para garantizar retrocompatibilidad total 
        // con agendamientos rápidos de clientes e historiales antiguos que no poseían asignaciones.
        public Guid? EmpleadoId { get; set; }
        public Guid? EstacionId { get; set; }
    }
}