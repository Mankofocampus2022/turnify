document.addEventListener('DOMContentLoaded', () => {
    const token = localStorage.getItem('turnify_token');
    const nombre = localStorage.getItem('usuario_nombre');
    const rol = localStorage.getItem('usuario_rol');

    // 1. Validación de sesión
    if (!token) {
        window.location.href = 'login.html';
        return;
    }

    // 2. Personalizar bienvenida
    const welcomeText = document.getElementById('welcomeText');
    const userRoleBadge = document.getElementById('userRole');

    if (welcomeText) welcomeText.innerText = `Bienvenido, ${nombre || 'Usuario'}`;

    if (userRoleBadge) {
        const SUPER_ADMIN_GUID = "6DE2A606-416E-4588-B4EB-CC20856CD80A";
        if (rol === SUPER_ADMIN_GUID || rol === "SuperAdministrador") {
            userRoleBadge.innerText = "🚀 SuperAdmin";
            userRoleBadge.style.background = "#e94560"; 
        } else {
            userRoleBadge.innerText = "🛡️ Administrador";
            userRoleBadge.style.background = "#0f3460"; 
        }
    }

    // 3. Botón de salida
    const btnLogout = document.getElementById('btnLogout');
    if (btnLogout) btnLogout.addEventListener('click', logout);

    // 4. Cargar la data real de SQL
    cargarEstadisticas(); 
});

function logout() {
    if (confirm("¿Estás seguro de que deseas cerrar sesión?")) {
        localStorage.clear(); 
        window.location.href = 'login.html';
    }
}

async function cargarEstadisticas() {
    const token = localStorage.getItem('turnify_token');
    const userId = localStorage.getItem('usuario_id');

    if (!userId) {
        console.error("❌ No se encontró el ID del usuario en el storage.");
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
                document.getElementById('totalCitas').innerText = stats.totalCitas;
            
            if(document.getElementById('nuevosClientes')) 
                document.getElementById('nuevosClientes').innerText = stats.nuevosClientes;

            // Actualizar tendencia (opcional)
            if(document.getElementById('citasTrend'))
                document.getElementById('citasTrend').innerText = "Actualizado ahora";

            // Formatear moneda colombiana
            if(document.getElementById('ingresosMes')) {
                const formatoMoneda = new Intl.NumberFormat('es-CO', {
                    style: 'currency', currency: 'COP', minimumFractionDigits: 0
                }).format(stats.gananciaEstimada);
                document.getElementById('ingresosMes').innerText = formatoMoneda;
            }
                
            // Llenar la tabla de Próximos Turnos
            llenarTablaTurnos(stats.proximasCitas);

            console.log("📊 Datos reales sincronizados para el ID:", userId);
        } else {
            console.error("❌ Error de API:", response.status);
        }
    } catch (error) {
        console.error("🚀 Error de conexión con la API:", error);
    }
}

function llenarTablaTurnos(citas) {
    const tablaBody = document.getElementById('turnosTable'); // Usamos el ID del HTML
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
            <td><span class="status-${cita.estado.toLowerCase()}">${cita.estado}</span></td>
        </tr>
    `).join('');
}