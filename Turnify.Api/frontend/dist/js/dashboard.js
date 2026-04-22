/* =========================================
   TURNIFY - LÓGICA DEL DASHBOARD 
   ========================================= */

document.addEventListener('DOMContentLoaded', () => {
    // 1. Puente de Seguridad
    const token = localStorage.getItem('turnify_token') || localStorage.getItem('token');
    
    if (!token) {
        console.warn("⚠️ Sin sesión activa. Redirigiendo...");
        window.location.href = 'login.html';
        return;
    }

    // 2. Recuperar el nombre real (Blindaje contra el texto "PRUEBA")
    const userStr = localStorage.getItem('user');
    let nombreFinal = "Darwin"; // Fallback por defecto

    if (userStr) {
        try {
            const userObj = JSON.parse(userStr);
            // Evitamos que salga 'PRUEBA' si ya tenemos el nombre real (Priorizamos nombre o Nombre)
            nombreFinal = userObj.nombre || userObj.Nombre || nombreFinal;
        } catch (e) { 
            console.error("❌ Error parseando objeto de usuario / Error al cargar nombre"); 
        }
    }

    // 3. Inyectar el saludo con el estilo de color (🙋‍♂️ SALUDO CON NOMBRE REAL)
    const welcomeText = document.getElementById('welcomeText');
    if (welcomeText) {
        // Usamos el span para que el CSS pinte tu nombre en turquesa (#48c1b5)
        welcomeText.innerHTML = `¡Qué más, <span style="color: #48c1b5;">${nombreFinal}</span>!`;
    }

    // 4. Carga Inicial Automática: Por defecto cargamos 'Hoy'
    const btnHoy = document.querySelector(".btn-filter");
    if (btnHoy) {
        cambiarPeriodo('hoy', btnHoy);
    }
    
    // Carga de estadísticas globales (Clientes nuevos, etc.)
    cargarResumenDashboard(token);
});

/**
 * 🔄 FUNCIÓN MAESTRA: Cambia el periodo de la agenda y actualiza la UI
 */
async function cambiarPeriodo(periodo, boton) {
    if (!boton) return;

    // A. Estética: Marcar el botón como activo (Efecto azul/vidrio)
    document.querySelectorAll('.btn-filter').forEach(b => b.classList.remove('active'));
    boton.classList.add('active');

    // B. Actualizar títulos según el periodo seleccionado
    const titulos = {
        'hoy': 'Agenda de Hoy',
        'mañana': 'Agenda de Mañana',
        'semana': 'Agenda de la Semana',
        'mes': 'Agenda del Mes'
    };
    const sectionTitle = document.getElementById('sectionTitle');
    if (sectionTitle) sectionTitle.innerText = titulos[periodo];

    // C. Cálculo de Fechas para el Backend (Blindaje de Rango)
    let inicio = new Date();
    let fin = new Date();

    if (periodo === 'mañana') {
        inicio.setDate(inicio.getDate() + 1);
        fin.setDate(fin.getDate() + 1);
    } else if (periodo === 'semana') {
        fin.setDate(fin.getDate() + 7);
    } else if (periodo === 'mes') {
        fin.setMonth(fin.getMonth() + 1);
    }

    // Formateo YYYY-MM-DD para la API (Evita problemas de zona horaria)
    const startStr = inicio.toISOString().split('T')[0];
    const endStr = fin.toISOString().split('T')[0];
    const token = localStorage.getItem('turnify_token') || localStorage.getItem('token');

    // D. Mostrar estado de carga (Ajustado a las 5 columnas de tu tabla)
    const tablaBody = document.getElementById('turnosTable');
    if (tablaBody) {
        tablaBody.innerHTML = '<tr><td colspan="5" style="text-align: center;"><i class="fas fa-spinner fa-spin"></i> Cargando agenda...</td></tr>';
    }

    try {
        // 🚩 LLAMADA AL ENDPOINT DE RANGOS
        const response = await fetch(`http://localhost:5000/api/Citas/rango?inicio=${startStr}&fin=${endStr}`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });

        if (response.ok) {
            const citas = await response.json();
            renderizarTablaDashboard(citas);
            actualizarContadoresDashboard(citas);
        } else if (response.status === 401) {
            logout(); // Sesión expirada
        }
    } catch (error) { 
        console.error("🔥 Error al filtrar agenda:", error);
        if (tablaBody) tablaBody.innerHTML = '<tr><td colspan="5" style="text-align: center; color: #ff5e5e;">Error de conexión con la API.</td></tr>';
    }
}

/**
 * 📝 Renderiza las filas en la tabla (🚩 ORDEN CORRECTO: Hora, Fecha, Cliente, Servicio, Estado)
 */
function renderizarTablaDashboard(citas) {
    const tabla = document.getElementById('turnosTable');
    if (!tabla || !citas) return;

    if (citas.length === 0) {
        tabla.innerHTML = '<tr><td colspan="5" style="text-align: center; padding: 20px; color: #ccc;">No hay citas agendadas para este periodo.</td></tr>';
        return;
    }

    tabla.innerHTML = citas.map(c => {
        const estado = (c.estado || "pendiente").toLowerCase();
        const badgeClass = getEstadoClass(estado);
        
        // Formateo amigable de la fecha (Ej: 22 abr)
        // Usamos T00:00:00 para blindar el desfase horario de JS
        const fechaObj = new Date(c.fecha + 'T00:00:00');
        const fechaFormateada = fechaObj.toLocaleDateString('es-CO', { day: '2-digit', month: 'short' });

        return `
            <tr>
                <td style="color: #48c1b5; font-weight: bold;"><i class="far fa-clock"></i> ${c.hora}</td> 
                <td>${fechaFormateada}</td>
                <td><strong>${c.clienteNombre || 'Sin nombre'}</strong></td>
                <td>${c.servicioNombre || 'Servicio'}</td>
                <td>
                    <span class="status-pill ${badgeClass}">
                        ${estado}
                    </span>
                </td>
            </tr>
        `;
    }).join('');
}

/**
 * 🔢 Actualiza los Cards de estadísticas superiores (Sincronizado con el periodo)
 */
function actualizarContadoresDashboard(citas) {
    const totalCitasEl = document.getElementById('totalCitas');
    const ingresosEl = document.getElementById('ingresosMes');

    if (totalCitasEl) totalCitasEl.innerText = citas.length;

    if (ingresosEl) {
        const total = citas.reduce((acc, c) => {
            // No sumamos ingresos de citas canceladas para mayor precisión
            return c.estado.toLowerCase() !== 'cancelada' ? acc + (c.precio || 0) : acc;
        }, 0);
        ingresosEl.innerText = `$${total.toLocaleString()}`;
    }
}

/**
 * 📊 Carga resumen de clientes (Dashboard Controller)
 */
async function cargarResumenDashboard(token) {
    try {
        const response = await fetch('http://localhost:5000/api/Dashboard/resumen', {
            headers: { 'Authorization': `Bearer ${token}` }
        });

        if (response.ok) {
            const data = await response.json();
            const clientesEl = document.getElementById('nuevosClientes');
            if(clientesEl) clientesEl.innerText = data.nuevosClientes || 0;
        }
    } catch (error) { 
        console.error("🔥 Error cargando resumen global:", error); 
    }
}

/**
 * 🎨 Asignación de clases CSS para los estados (Sincronizado con tu CSS Heineken)
 */
function getEstadoClass(estado) {
    if (estado.includes('completado') || estado.includes('confirmada')) return 'status-activo';
    if (estado.includes('cancelada') || estado.includes('suspendido')) return 'status-bloqueado';
    return 'status-pendiente'; 
}

/**
 * 🚪 Cerrar Sesión
 */
function logout() {
    if (confirm("¿Se va a abrir, mi perro? Guarde todo antes de salir.")) {
        localStorage.clear();
        window.location.href = 'login.html';
    }
}