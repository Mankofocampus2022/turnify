document.addEventListener('DOMContentLoaded', () => {
    // 1. Sincronización de nombres (Usamos nombres consistentes)
    const token = localStorage.getItem('token') || localStorage.getItem('turnify_token');
    const nombre = localStorage.getItem('adminName');
    const rol = localStorage.getItem('usuario_rol');
    const proveedorId = localStorage.getItem('proveedorId'); // 🚩 NECESARIO PARA CITAS

    console.log("🛡️ Verificando sesión...");
    console.log("Token detectado:", token ? "SI" : "NO");
    console.log("Proveedor ID detectado:", proveedorId ? "SI" : "NO");

    // 2. Validación de sesión
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
        const SUPER_ADMIN_NAME = "SUPERADMIN"; // 🚩 Ajustado al nombre real en DB
        const ADMIN_NAME = "ADMINISTRADOR";
        
        const currentRol = (rol || "").toUpperCase();

        if (currentRol.includes("SUPERADMIN")) {
            userRoleBadge.innerText = "🚀 SuperAdmin";
            userRoleBadge.style.background = "#e94560"; 
        } else {
            userRoleBadge.innerText = "🛡️ Administrador";
            userRoleBadge.style.background = "#0f3460"; 
        }
    }

    // 4. Eventos y Carga de datos
    const btnLogout = document.getElementById('btnLogout');
    if (btnLogout) btnLogout.addEventListener('click', logout);

    // 🚩 CAMBIO: Solo cargamos si tenemos el ID del Negocio (Proveedor)
    if (proveedorId) {
        cargarEstadisticas(); 
    } else {
        console.warn("⚠️ No se cargaron stats porque falta el proveedorId.");
    }
});

function logout() {
    if (confirm("¿Estás seguro de que deseas cerrar sesión?")) {
        localStorage.clear(); 
        window.location.href = 'login.html';
    }
}

async function cargarEstadisticas() {
    // 🚩 Usamos consistencia de tokens
    const token = localStorage.getItem('token') || localStorage.getItem('turnify_token');
    const proveedorId = localStorage.getItem('proveedorId');

    if (!proveedorId || !token) {
        console.error("❌ Faltan credenciales del negocio (ProveedorID).");
        return;
    }

    try {
        // 🚩 CORRECCIÓN CRÍTICA: La URL debe pedir el resumen del PROVEEDOR, no del Usuario
        const response = await fetch(`http://localhost:5000/api/Dashboard/resumen/${proveedorId}`, {
            method: 'GET',
            headers: {
                'Authorization': `Bearer ${token}`,
                'Content-Type': 'application/json'
            }
        });

        if (response.ok) {
            const stats = await response.json();
            
            if(document.getElementById('totalCitas')) 
                document.getElementById('totalCitas').innerText = stats.totalCitas || 0;
            
            if(document.getElementById('nuevosClientes')) 
                document.getElementById('nuevosClientes').innerText = stats.nuevosClientes || 0;

            if(document.getElementById('citasTrend'))
                document.getElementById('citasTrend').innerText = "Sincronizado";

            if(document.getElementById('ingresosMes')) {
                const formatoMoneda = new Intl.NumberFormat('es-CO', {
                    style: 'currency', currency: 'COP', minimumFractionDigits: 0
                }).format(stats.gananciaEstimada || 0);
                document.getElementById('ingresosMes').innerText = formatoMoneda;
            }
                
            llenarTablaTurnos(stats.proximasCitas);
            console.log("📊 Dashboard actualizado para el negocio:", proveedorId);
        } else {
            console.error("❌ Error de API:", response.status);
            if (response.status === 401) logout();
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
            <td>${cita.cliente || 'Desconocido'}</td>
            <td>${cita.servicio || 'General'}</td>
            <td>${cita.hora || '--:--'}</td>
            <td><span class="status-${(cita.estado || 'pendiente').toLowerCase()}">${cita.estado || 'Pendiente'}</span></td>
        </tr>
    `).join('');
}