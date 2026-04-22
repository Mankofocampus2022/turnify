/* ============================================================
   TURNIFY - BLINDAJE GLOBAL DE NAVEGACIÓN
   ============================================================ */

function validarSesionYMenu() {
    const token = localStorage.getItem('turnify_token') || localStorage.getItem('token');
    const rol = (localStorage.getItem('usuario_rol') || "").toUpperCase();

    // 1. Si no hay token, para afuera de una
    if (!token || token === "null") {
        window.location.href = 'login.html';
        return;
    }

    // 2. Gestionar visibilidad de Reportes y Usuarios
    const navReportes = document.getElementById('nav-reportes');
    const navUsuarios = document.getElementById('nav-usuarios');

    // SuperAdmin y Proveedor/Admin ven Reportes
    const tieneAccesoReportes = rol.includes("ADMIN") || rol.includes("PROVEEDOR");
    
    if (navReportes) {
        navReportes.style.display = tieneAccesoReportes ? 'flex' : 'none';
    }

    // Solo SuperAdmin ve Usuarios (opcional, tú decides)
    if (navUsuarios) {
        const esSuper = rol.includes("SUPERADMIN");
        // navUsuarios.style.display = esSuper ? 'flex' : 'none'; 
    }

    console.log("🛡️ Navegación blindada para:", rol);
}

// Cerrar sesión global
window.logout = function() {
    localStorage.clear();
    window.location.href = 'login.html';
};

// Se ejecuta apenas carga la página
document.addEventListener('DOMContentLoaded', validarSesionYMenu);