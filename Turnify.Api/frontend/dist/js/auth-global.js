/* ============================================================
   TURNIFY - BLINDAJE GLOBAL DE NAVEGACIÓN (VERSIÓN CLIENTE)
   ============================================================ */

function validarSesionYMenu() {
    const token = localStorage.getItem('turnify_token') || localStorage.getItem('token');
    const rol = (localStorage.getItem('usuario_rol') || "").toUpperCase();
    const currentPath = window.location.pathname.split('/').pop();

    // 1. SI NO HAY TOKEN: Patitas a la calle (al login)
    if (!token || token === "null") {
        if (currentPath !== 'login.html' && currentPath !== 'registro.html') {
            window.location.href = 'login.html';
        }
        return;
    }

    const esCliente = rol.includes("CLIENTE");
    // 🚩 NUEVO: Detectamos si es un profesional (Barbero/Admin/Proveedor)
    const esProfesional = rol.includes("BARBERO") || rol.includes("PROVEEDOR") || rol.includes("ADMIN");

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
            const puedeVerUsuarios = rol.includes("SUPERADMIN") || rol.includes("BARBERO") || rol.includes("ADMIN") || rol.includes("PROVEEDOR");
            navUsuarios.style.display = puedeVerUsuarios ? 'flex' : 'none';
        }

        // El barbero también puede agendar citas manualmente
        if (navAgendar) navAgendar.style.display = 'flex';
    }

    console.log("🛡️ Lupe Guard: Blindaje activo para", rol);
}

// Cerrar sesión
window.logout = function() {
    if (confirm("¿Seguro que quiere abrirse, mi perro?")) {
        localStorage.clear();
        window.location.href = 'login.html';
    }
};

document.addEventListener('DOMContentLoaded', validarSesionYMenu);