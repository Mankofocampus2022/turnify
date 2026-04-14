document.addEventListener('DOMContentLoaded', () => {
    // 1. Puente de Seguridad (Sincronizado con configuracion.js)
    const token = localStorage.getItem('token') || localStorage.getItem('turnify_token');
    const nombre = localStorage.getItem('adminName') || localStorage.getItem('usuario_nombre');
    const proveedorId = localStorage.getItem('proveedorId') || localStorage.getItem('proveedor_id');

    if (!token) {
        console.warn("⚠️ Sin sesión activa. Redirigiendo...");
        window.location.href = 'login.html';
        return;
    }

    // 2. Personalizar Interfaz
    const welcomeText = document.getElementById('welcomeText');
    if (welcomeText) {
        welcomeText.innerText = `¡Qué más, ${nombre || 'Barbero'}!`;
    }

    // 3. Disparar carga de datos reales
    cargarResumenDashboard(token);
    cargarCitasHoy(token);
});

/**
 * Carga los contadores (Citas hoy, Ganancias, Clientes nuevos)
 */
async function cargarResumenDashboard(token) {
    try {
        // 🚩 LLAMADA AL DASHBOARD CONTROLLER
        const response = await fetch('http://localhost:5000/api/Dashboard/resumen', {
            headers: { 'Authorization': `Bearer ${token}` }
        });

        if (response.ok) {
            const data = await response.json();
            
            // Suponiendo que tienes estos IDs en tu HTML:
            if(document.getElementById('countCitas')) 
                document.getElementById('countCitas').innerText = data.totalCitasHoy || 0;
            
            if(document.getElementById('countGanancias')) 
                document.getElementById('countGanancias').innerText = `$${data.gananciasHoy || 0}`;
        }
    } catch (error) {
        console.error("🔥 Error cargando resumen:", error);
    }
}

/**
 * Carga la lista de citas de hoy (Usando el CitasController que arreglamos)
 */
async function cargarCitasHoy(token) {
    const contenedor = document.getElementById('listaCitasHoy');
    if (!contenedor) return;

    try {
        // 🚩 USAMOS EL NUEVO ENDPOINT DINÁMICO
        const response = await fetch('http://localhost:5000/api/Citas/mi-agenda', {
            headers: { 'Authorization': `Bearer ${token}` }
        });

        if (response.ok) {
            const citas = await response.json();
            
            if (citas.length === 0) {
                contenedor.innerHTML = '<p class="text-muted">No tienes citas para hoy... ¡A descansar! ☕</p>';
                return;
            }

            // Pintamos las citas en la tabla o lista
            contenedor.innerHTML = citas.map(cita => `
                <div class="appointment-card" style="display: flex; justify-content: space-between; align-items: center; background: #1b3d5f; padding: 15px; border-radius: 10px; margin-bottom: 10px; border-left: 5px solid #48c1b5;">
                    <div>
                        <h4 style="margin: 0; color: #48c1b5;">${cita.clienteNombre || 'Cliente'}</h4>
                        <small style="color: #ccc;">${cita.servicioNombre} - <strong>${cita.hora}</strong></small>
                    </div>
                    <div>
                        <span class="badge" style="background: ${getEstadoColor(cita.estado)}; padding: 5px 10px; border-radius: 20px; font-size: 0.8rem;">
                            ${cita.estado}
                        </span>
                    </div>
                </div>
            `).join('');
        }
    } catch (error) {
        console.error("🔥 Error cargando citas:", error);
        contenedor.innerHTML = '<p class="text-danger">Error al conectar con la agenda.</p>';
    }
}

function getEstadoColor(estado) {
    const colores = {
        'Pendiente': '#f39c12',
        'Completado': '#27ae60',
        'Cancelado': '#e74c3c',
        'En Proceso': '#3498db'
    };
    return colores[estado] || '#7f8c8d';
}

function logout() {
    if (confirm("¿Se va a abrir, mi perro? Guarde todo antes de salir.")) {
        localStorage.clear();
        window.location.href = 'login.html';
    }
}