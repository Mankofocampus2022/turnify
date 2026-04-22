/* ============================================================
   TURNIFY - MOTOR DE REGISTRO INTELIGENTE (Versión Senior)
   ============================================================ */

// 1. CONFIGURACIÓN DE ROLES (GUIDs de tu base de datos SQL Server)
const ROLES = {
    CLIENTE: "56992f75-6420-4d55-a5f9-9223248c50d7",
    BARBERO: "8854c07c-6e5e-4876-a29a-c7ad5dcfbab7" // Rol de Proveedor/Admin
};

let currentRole = 'CLIENTE';

/**
 * Función para alternar entre Cliente y Barbero en la UI
 */
function cambiarRol(rol) {
    currentRole = rol;
    
    const groupNegocio = document.getElementById('groupNegocio');
    const inputNegocio = document.getElementById('regNegocio');
    const btnText = document.getElementById('btnText');
    const btnCliente = document.getElementById('btnSoyCliente');
    const btnBarbero = document.getElementById('btnSoyBarbero');

    if (rol === 'BARBERO') {
        groupNegocio.style.display = 'block';
        inputNegocio.required = true;
        btnText.innerText = "Registrarme como Barbero";
        btnBarbero.classList.add('active');
        btnCliente.classList.remove('active');
    } else {
        groupNegocio.style.display = 'none';
        inputNegocio.required = false;
        btnText.innerText = "Registrarme como Cliente";
        btnCliente.classList.add('active');
        btnBarbero.classList.remove('active');
    }
}

/**
 * Manejador principal del Registro
 */
document.getElementById('formRegistroCliente').addEventListener('submit', async (e) => {
    e.preventDefault();

    const btnSubmit = document.getElementById('btnSubmit');
    const password = document.getElementById('regPassword').value;
    const confirm = document.getElementById('regConfirmPassword').value;

    // 🛡️ VALIDACIÓN SENIOR: Match de Password
    if (password !== confirm) {
        alert("⚠️ Las contraseñas no coinciden. Por favor, verifica.");
        return;
    }

    // 🛡️ BLOQUEO UX: Evitar doble post
    btnSubmit.disabled = true;
    btnSubmit.innerText = "Procesando...";

    // 📦 MAPEO AL DTO DE C# (Respetando PropertyNames)
    const registroData = {
        nombre: document.getElementById('regNombre').value.trim(),
        email: document.getElementById('regEmail').value.trim(),
        password: password,
        rol_id: currentRole === 'CLIENTE' ? ROLES.CLIENTE : ROLES.BARBERO,
        telefono: document.getElementById('regTelefono').value.trim(),
        nombreComercial: currentRole === 'BARBERO' ? document.getElementById('regNegocio').value.trim() : null
    };

    try {
        const response = await fetch('http://localhost:5000/api/Usuarios/registrar', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Accept': 'application/json'
            },
            body: JSON.stringify(registroData)
        });

        const result = await response.json();

        if (response.ok) {
            alert("🚀 ¡Bienvenido a Turnify! Tu cuenta ha sido creada exitosamente.");
            window.location.href = 'login.html'; // Redirección al login
        } else {
            // Manejo de errores controlados (Usuario ya existe, etc.)
            alert("❌ Error: " + (result.message || "No se pudo completar el registro."));
            btnSubmit.disabled = false;
            btnSubmit.innerText = currentRole === 'CLIENTE' ? "Registrarme como Cliente" : "Registrarme como Barbero";
        }

    } catch (error) {
        console.error("🚨 Error de conexión:", error);
        alert("🔌 Error de red: No se pudo conectar con el servidor. Verifica que la API esté corriendo.");
        btnSubmit.disabled = false;
        btnSubmit.innerText = "Reintentar Registro";
    }
});