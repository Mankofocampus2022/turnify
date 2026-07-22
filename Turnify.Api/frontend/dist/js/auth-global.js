/* ============================================================
   TURNIFY - BLINDAJE GLOBAL DE NAVEGACIÓN (VERSIÓN CLIENTE)
   ============================================================ */

function validarSesionYMenu() {
    let token = localStorage.getItem('turnify_token') || localStorage.getItem('token');
    
    // 🧠 FIX DE SEGURIDAD QA SENIOR: Separamos el nombre del archivo de los parámetros "?" (Query Strings)
    // Esto evita que un usuario salte el bloqueo usando "admin-dashboard.html?id=1"
    const currentPath = window.location.pathname.split('/').pop().split('?')[0];

    // 🛡️ REFUERZO DE CONTENCIÓN: Limpiamos falsos positivos de cadenas vacías o literales de error
    if (token === "null" || token === "undefined" || !token) {
        token = null;
    }

    // 1. SI NO HAY TOKEN: no entra (al login)
    if (!token) {
        // 🚀 EXCEPCIÓN SENIOR PARA GUEST CHECKOUT QR:
        // Si el cliente ingresa de manera anónima a agendar-cita.html trayendo el parámetro ID del proveedor (?id=...),
        // detenemos el bloqueo global de autenticación para permitirle el agendamiento fluido de invitado.
        const urlParamsQR = new URLSearchParams(window.location.search);
        if (currentPath === 'agendar-cita.html' && urlParamsQR.has('id')) {
            return; // Concedemos luz verde inmediata y omitimos el redirect
        }

        if (currentPath !== 'login.html' && currentPath !== 'registro.html' && currentPath !== '') {
            localStorage.clear(); // Limpiamos residuos huérfanos antes de sacar al usuario
            window.location.href = 'login.html';
        }
        return;
    }

    // 🚀 INTEGRACIÓN MULTI-CLAVE DE IDENTIDAD: Recuperamos el rol desde la clave directa o el objeto user parseado
    let rolRaw = localStorage.getItem('usuario_rol') || localStorage.getItem('role') || "";
    const userStr = localStorage.getItem('user');

    if (!rolRaw && userStr) {
        try {
            const userObj = JSON.parse(userStr);
            rolRaw = userObj.rol || userObj.Rol || userObj.role || userObj.Role || "";
        } catch (e) {
            console.error("❌ Error de contingencia parseando rol desde el objeto de usuario");
        }
    }

    const rol = String(rolRaw).toUpperCase().trim();

    // GUIDs transaccionales de control de la base de datos SQL Server
    const SUPER_ADMIN_GUID = "6DE2A606-416E-4588-B4EB-CC20856CD80A";
    const ADMIN_GUID = "6A7FA68F-C28D-4F1B-B2D8-4FB0A6146A43";

    // 🧠 AJUSTE ADAPTATIVO: Soporta tanto el texto plano como los identificadores GUID reales de la BD
    const esCliente = rol.includes("CLIENTE") && rol !== SUPER_ADMIN_GUID && rol !== ADMIN_GUID;
    // 🚩 NUEVO: Detectamos si es un profesional (Barbero/Admin/Proveedor)
    const esProfesional = rol.includes("BARBERO") || rol.includes("PROVEEDOR") || rol.includes("ADMIN") || rol === SUPER_ADMIN_GUID || rol === ADMIN_GUID;

    // 2. 🛡️ BLOQUEO DE ACCESO DIRECTO (URL)
    // Lista negra para clientes: No pueden ver nada administrativo
    const paginasProhibidasParaCliente = [
        'admin-dashboard.html', 
        'reportes.html', 
        'usuarios.html', 
        'configuracion.html', 
        'servicios.html'
    ];

    if (esCliente && paginasProhibidasParaCliente.includes(currentPath)) {
        console.error("🚫 ¡Intento de acceso no autorizado! Redirigiendo...");
        window.location.href = 'agendar-cita.html';
        return;
    }

    // 3. 👁️ VISIBILIDAD DE MENÚS (SIDEBAR)
    // Capturamos los elementos por ID (Asegúrate de poner estos IDs en tus <a> del sidebar)
    const navResumen = document.getElementById('nav-resumen');
    const navReportes = document.getElementById('nav-reportes');
    const navUsuarios = document.getElementById('nav-usuarios');
    const navServicios = document.getElementById('nav-servicios');
    const navConfig = document.getElementById('nav-config');
    const navAgendar = document.getElementById('nav-agendar'); // Nuevo ID para "Nueva Cita"

    if (esCliente) {
        // 🔒 BLINDAJE PARA CLIENTE: Escondemos todo lo sensible
        if (navResumen) navResumen.style.display = 'none';
        if (navReportes) navReportes.style.display = 'none';
        if (navUsuarios) navUsuarios.style.display = 'none';
        if (navServicios) navServicios.style.display = 'none';
        if (navConfig) navConfig.style.display = 'none';
        
        // El cliente solo debe ver su opción de agendar
        if (navAgendar) navAgendar.style.display = 'flex';
    } else {
        // ✅ ACCESO PARA BARBEROS/ADMINS (Profesionales)
        if (navResumen) navResumen.style.display = 'flex';
        if (navReportes) navReportes.style.display = 'flex';
        if (navConfig) navConfig.style.display = 'flex';
        
        // 🚩 AJUSTE: El barbero ahora debe ver Servicios para gestionarlos
        if (navServicios) navServicios.style.display = 'flex';
        
        // 🚩 AJUSTE: El barbero/admin ve Usuarios para mirar sus clientes
        // Solo ocultamos si no tiene ninguno de los roles de gestión
        if (navUsuarios) {
            const puedeVerUsuarios = rol.includes("SUPERADMIN") || rol.includes("BARBERO") || rol.includes("ADMIN") || rol.includes("PROVEEDOR") || rol === SUPER_ADMIN_GUID || rol === ADMIN_GUID;
            navUsuarios.style.display = puedeVerUsuarios ? 'flex' : 'none';
        }

        // El barbero también puede agendar citas manualmente
        if (navAgendar) navAgendar.style.display = 'flex';
    }

    console.log("🛡️ Lupe Guard: Blindaje activo para", rol);
}

// Cerrar sesión
window.logout = function() {
    if (confirm("¿Seguro que deseas cerrar sesión? vuelve pronto")) {
        localStorage.clear();
        window.location.href = 'login.html';
    }
};

document.addEventListener('DOMContentLoaded', validarSesionYMenu);