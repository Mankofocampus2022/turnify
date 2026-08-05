using System;

namespace Turnify.Api.Models.DTOs
{
    public class ProveedorCreateDto
    {
        public string nombre_comercial { get; set; } = string.Empty;
        public string direccion { get; set; } = string.Empty;
        public string tipo { get; set; } = string.Empty;

        // 🧠 BLINDAJE MASTER SENIOR: Captura la categoría enviada desde el JSON ("Barbero" o "Manicurista")
        // Se define con un valor por defecto seguro en caso de que falte en el request.
        public string? categoria { get; set; } = "Barbero";

        public Guid usuarioId { get; set; }
        
        // 🚩 [NUEVO] LLAVES DE ENTRADA MULTI-TENANT: Mapeadas para resolver el error CS1061 del controlador
        // Capturan el teléfono corporativo y el email desde el formulario de registro inicial
        public string? telefono { get; set; }
        public string? email { get; set; }

        public bool trabaja_domicilio { get; set; } 
        public bool activo { get; set; } = true;

        // 🎯 FLAG DE INDEPENDIENTE / ESTABLECIMIENTO
        // Permite identificar si el proveedor opera de forma individual o maneja un staff/equipo.
        public bool es_independiente { get; set; } = false;

        // 🛡️ ALIAS DE COMPATIBILIDAD C#: Mapea a PascalCase para evitar errores CS1061 
        // en controladores, servicios o AutoMapper que busquen 'EsIndependiente'.
        public bool EsIndependiente 
        { 
            get => es_independiente; 
            set => es_independiente = value; 
        }
    }
}