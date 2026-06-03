/* ============================================================
   TURNIFY - MOTOR DE SEGURIDAD INTERNA PARA ADMINISTRADORES
   ============================================================ */

// 1. Token secreto requerido en la URL (?secret=...)
const SECRET_INVITATION_KEY = "TurnifyAdminSecure2026Key"; 

// 2. 🧠 FIX: Mapeo correcto de los dos niveles de administración sin duplicar variables
const ROLES_ADMIN = {
    ADMIN: "6DE2A606-416E-4588-B4EB-CC20856CD80A",
    SUPER_ADMIN: "6A7FA68F-C28D-4F1B-B2D8-4FB0A6146A43"
};

// Validamos el acceso antes de pintar nada
const urlParams = new URLSearchParams(window.location.search);
const tokenIngresado = urlParams.get('secret');

if (tokenIngresado === SECRET_INVITATION_KEY) {
    // Si la firma es correcta, destruimos el bloqueo y mostramos el panel
    document.getElementById('lockScreen').style.display = 'none';
    document.getElementById('adminCard').style.display = 'block';
} else {
    console.error("🔒 Intento de acceso no autorizado al registro administrativo.");
}

/**
 * Envío seguro del formulario de Administración
 */
document.getElementById('formRegistroAdmin').addEventListener('submit', async (e) => {
    e.preventDefault();

    const btnSubmit = document.getElementById('btnSubmitAdmin');
    const password = document.getElementById('adminPassword').value;
    const confirm = document.getElementById('adminConfirmPassword').value;

    if (password !== confirm) {
        alert("⚠️ Las contraseñas no coinciden. Por favor, verifica.");
        return;
    }

    btnSubmit.disabled = true;
    btnSubmit.innerText = "Garantizando privilegios...";

    // 🧠 FIX: Capturamos el GUID del rol seleccionado en la interfaz
    const tipoAdminSeleccionado = document.getElementById('adminTipoRol').value;
    const rolGuidFinal = ROLES_ADMIN[tipoAdminSeleccionado] || ROLES_ADMIN.ADMIN;

    const adminPayload = {
        nombre: document.getElementById('adminNombre').value.trim(),
        email: document.getElementById('adminEmail').value.trim(),
        password: password,
        rol_id: rolGuidFinal, // 🧠 Inyección del GUID dinámico según la selección (Admin o Super Admin)
        telefono: document.getElementById('adminTelefono').value.trim(),
        nombreComercial: "Consola de Administración Central",
        tipoNegocio: "Sistemas"
    };

    try {
        // Despachamos al endpoint de usuarios de tu API en .NET
        const response = await fetch('http://localhost:5000/api/Usuarios/registrar', {
            method: 'POST',
            headers: { 
                'Content-Type': 'application/json',
                'X-Admin-Creation-Key': tokenIngresado // Enviamos el token en las cabeceras por seguridad extra
            },
            body: JSON.stringify(adminPayload)
        });

        const result = await response.json();

        if (response.ok) {
            alert("🚀 ¡Cuenta de Administrador Turnify configurada con éxito!");
            window.location.href = 'login.html';
        } else {
            alert("❌ Fallo en registro: " + (result.message || "Error interno de validación."));
            btnSubmit.disabled = false;
            btnSubmit.innerText = "Crear Cuenta Administrativa";
        }
    } catch (error) {
        console.error("🚨 Error:", error);
        alert("🔌 Error crítico de red al conectar con el servidor.");
        btnSubmit.disabled = false;
        btnSubmit.innerText = "Reintentar Configuración";
    }
});