document.addEventListener('DOMContentLoaded', () => {
    // 1. Sincronización de nombres con login.js
    const token = localStorage.getItem('token'); // Quitamos el "turnify_" para que coincida
    const nombre = localStorage.getItem('adminName'); // En login guardamos 'adminName'
    const rol = localStorage.getItem('usuario_rol');

    console.log("🛡️ Verificando sesión...");
    console.log("Token detectado:", token ? "SI" : "NO");
    console.log("Rol detectado:", rol);

    // 2. Validación de sesión (El Portero)
    if (!token) {
        console.error("🚫 No hay token. Redirigiendo al login...");
        window.location.href = 'login.html';
        return;
    }

    // 3. Personalizar bienvenida
    const welcomeText = document.getElementById('welcomeText');
    const userRoleBadge = document.getElementById('userRole');

    if (welcomeText) {
        welcomeText.innerText = `¡Hola, ${nombre || 'Darwin'}!`;
    }

    if (userRoleBadge) {
        const SUPER_ADMIN_GUID = "6DE2A606-416E-4588-B4EB-CC20856CD80A";
        const ADMIN_GUID = "6A7FA68F-C28D-4F1B-B2D8-4FB0A6146A43";
        
        // Normalizamos el rol a mayúsculas para comparar seguro
        const currentRol = (rol || "").toUpperCase();

        if (currentRol === SUPER_ADMIN_GUID || currentRol === "SUPERADMINISTRADOR") {
            userRoleBadge.innerText = "🚀 SuperAdmin";
            userRoleBadge.style.background = "#e94560"; 
        } else if (currentRol === ADMIN_GUID || currentRol === "ADMINISTRADOR") {
            userRoleBadge.innerText = "🛡️ Administrador";
            userRoleBadge.style.background = "#0f3460"; 
        }
    }

    // 4. Eventos y Carga de datos
    const btnLogout = document.getElementById('btnLogout');
    if (btnLogout) btnLogout.addEventListener('click', logout);

    // Solo cargamos estadísticas si hay un ID de usuario
    if (localStorage.getItem('usuario_id')) {
        cargarEstadisticas(); 
    }
});

function logout() {
    if (confirm("¿Estás seguro de que deseas cerrar sesión?")) {
        localStorage.clear(); 
        window.location.href = 'login.html';
    }
}

async function cargarEstadisticas() {
    const token = localStorage.getItem('token');
    const userId = localStorage.getItem('usuario_id');

    if (!userId || !token) {
        console.error("❌ Faltan credenciales para la API.");
        return;
    }

    try {
        const response = await fetch(`http://localhost:5000/api/Dashboard/resumen/${userId}`, {
            method: 'GET',
            headers: {
                'Authorization': `Bearer ${token}`,
                'Content-Type': 'application/json'
            }
        });

        if (response.ok) {
            const stats = await response.json();
            
            // Inyectar datos en los cuadros superiores
            if(document.getElementById('totalCitas')) 
                document.getElementById('totalCitas').innerText = stats.totalCitas || 0;
            
            if(document.getElementById('nuevosClientes')) 
                document.getElementById('nuevosClientes').innerText = stats.nuevosClientes || 0;

            if(document.getElementById('citasTrend'))
                document.getElementById('citasTrend').innerText = "Sincronizado";

            // Formatear moneda COP
            if(document.getElementById('ingresosMes')) {
                const formatoMoneda = new Intl.NumberFormat('es-CO', {
                    style: 'currency', currency: 'COP', minimumFractionDigits: 0
                }).format(stats.gananciaEstimada || 0);
                document.getElementById('ingresosMes').innerText = formatoMoneda;
            }
                
            llenarTablaTurnos(stats.proximasCitas);
            console.log("📊 Dashboard actualizado para:", userId);
        } else {
            console.error("❌ Error de API:", response.status);
            if (response.status === 401) logout(); // Si el token expiró, fuera.
        }
    } catch (error) {
        console.error("🚀 Error de conexión:", error);
    }
}

function llenarTablaTurnos(citas) {
    const tablaBody = document.getElementById('turnosTable');
    if (!tablaBody) return;

    if (!citas || citas.length === 0) {
        tablaBody.innerHTML = '<tr><td colspan="4" style="text-align:center;">No hay turnos para hoy</td></tr>';
        return;
    }

    tablaBody.innerHTML = citas.map(cita => `
        <tr>
            <td>${cita.cliente}</td>
            <td>${cita.servicio}</td>
            <td>${cita.hora}</td>
            <td><span class="status-${(cita.estado || 'pendiente').toLowerCase()}">${cita.estado}</span></td>
        </tr>
    `).join('');
}