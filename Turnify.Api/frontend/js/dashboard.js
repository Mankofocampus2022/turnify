document.addEventListener('DOMContentLoaded', () => {
    // 1. Validar Sesión (Seguridad básica de Frontend)
    const token = localStorage.getItem('turnify_token');
    const nombre = localStorage.getItem('usuario_nombre');
    const rol = localStorage.getItem('usuario_rol');

    // Si no hay token, lo mandamos de patitas a la calle (al login)
    if (!token) {
        console.warn("⚠️ Intento de acceso sin token. Redirigiendo...");
        window.location.href = 'login.html';
        return;
    }

    // 2. Personalizar la Interfaz con datos reales
    const welcomeText = document.getElementById('welcomeText');
    const userRoleBadge = document.getElementById('userRole');

    if (welcomeText) {
        welcomeText.innerText = `Bienvenido, ${nombre || 'Administrador'}`;
    }

    if (userRoleBadge) {
        // Mapeo de GUIDs que definiste en el login
        const SUPER_ADMIN_GUID = "6DE2A606-416E-4588-B4EB-CC20856CD80A";
        
        if (rol === SUPER_ADMIN_GUID) {
            userRoleBadge.innerText = "🚀 SuperAdmin";
            userRoleBadge.style.background = "#e94560"; // Color acento
        } else {
            userRoleBadge.innerText = "🛡️ Administrador";
            userRoleBadge.style.background = "#0f3460"; // Color primario
        }
    }

    // 3. Cargar datos iniciales (Aquí llamaremos a la API de C# pronto)
    console.log("✅ Dashboard cargado para:", nombre);
    // cargarEstadisticas(); 
});

/**
 * Limpia el almacenamiento y sale del sistema
 */
function logout() {
    if (confirm("¿Estás seguro de que deseas cerrar sesión?")) {
        localStorage.clear(); // Borra token, nombre y rol
        window.location.href = 'login.html';
    }
}

/**
 * Función base para cuando conectemos el Backend de C#
 */
async function cargarEstadisticas() {
    try {
        // Aquí irá tu fetch a http://localhost:5000/api/Dashboard/stats
        console.log("Próximamente: Conexión con SQL Server...");
    } catch (error) {
        console.error("Error cargando estadísticas:", error);
    }
}