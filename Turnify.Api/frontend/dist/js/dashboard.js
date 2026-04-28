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
            nombreFinal = userObj.nombre || userObj.Nombre || nombreFinal;
        } catch (e) { 
            console.error("❌ Error parseando objeto de usuario / Error al cargar nombre"); 
        }
    }

    // 3. Inyectar el saludo con el estilo de color (🙋‍♂️ SALUDO CON NOMBRE REAL)
    const welcomeText = document.getElementById('welcomeText');
    if (welcomeText) {
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

    // A. Estética: Marcar el botón como activo
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

    // C. Cálculo de Fechas para el Backend (🚩 MEJORADO SIN BORRAR)
    let inicio = new Date();
    let fin = new Date();

    if (periodo === 'mañana') {
        inicio.setDate(inicio.getDate() + 1);
        fin.setDate(fin.getDate() + 1);
    } else if (periodo === 'semana') {
        // 🛡️ Ajuste Senior: Vamos al lunes de esta semana para no perder citas pasadas
        const day = inicio.getDay();
        const diff = inicio.getDate() - day + (day === 0 ? -6 : 1); 
        inicio.setDate(diff);
        fin.setDate(inicio.getDate() + 7);
    } else if (periodo === 'mes') {
        // 🚩 EL ARREGLO PARA DARWIN: Retrocedemos al día 1 del mes actual (Abril 1)
        inicio.setDate(1); 
        // Y el fin es el día 1 del mes siguiente (Mayo 1)
        fin.setMonth(fin.getMonth() + 1);
        fin.setDate(1);
    }

    // 🛡️ Ajuste Senior: Captura citas de hace 2 días hasta hoy para ver lo "vencido" en la vista de Hoy
    if (periodo === 'hoy') {
        inicio.setDate(inicio.getDate() - 2); 
    }

    const startStr = inicio.toISOString().split('T')[0];
    const endStr = fin.toISOString().split('T')[0];
    const token = localStorage.getItem('turnify_token') || localStorage.getItem('token');

    const tablaBody = document.getElementById('turnosTable');
    if (tablaBody) {
        tablaBody.innerHTML = '<tr><td colspan="5" style="text-align: center;"><i class="fas fa-spinner fa-spin"></i> Cargando agenda...</td></tr>';
    }

    try {
        const user = JSON.parse(localStorage.getItem('user'));
        const provId = user?.proveedorId || user?.id;

        // 📡 Usamos el endpoint de resumen con las fechas ya calculadas
        // Pasamos el startStr para que el Service sepa desde dónde empezar a buscar
        const response = await fetch(`http://localhost:5000/api/Dashboard/resumen/${provId}?periodo=${periodo}&fecha=${startStr}`, {
            headers: { 
                'Authorization': `Bearer ${token}`,
                'Content-Type': 'application/json'
            }
        });

        if (response.ok) {
            const data = await response.json();
            console.log("📅 JSON Recibido de API:", data);
            
            renderizarTablaDashboard(data.proximasCitas || []);
            actualizarContadoresDashboard(data); 
        }
    } catch (error) { 
        console.error("🔥 Error al filtrar agenda:", error);
    }
}

/**
 * 📝 Renderiza las filas en la tabla
 */
function renderizarTablaDashboard(citas) {
    const tabla = document.getElementById('turnosTable');
    if (!tabla) return;

    if (!citas || citas.length === 0) {
        tabla.innerHTML = '<tr><td colspan="5" style="text-align: center; padding: 20px; color: #ccc;">No hay citas agendadas para este periodo.</td></tr>';
        return;
    }

    tabla.innerHTML = citas.map(c => {
        const estado = (c.estado || "pendiente").toLowerCase();
        const badgeClass = getEstadoClass(estado);
        
        return `
            <tr>
                <td style="color: #48c1b5; font-weight: bold;"><i class="far fa-clock"></i> ${c.hora}</td> 
                <td>${c.fecha || 'Hoy'}</td>
                <td><strong>${c.cliente || 'Sin nombre'}</strong></td>
                <td>${c.servicio || 'Servicio'}</td>
                <td><span class="status-pill ${badgeClass}">${estado}</span></td>
            </tr>
        `;
    }).join('');
}

/**
 * 🔢 Actualiza los Cards de estadísticas
 */
function actualizarContadoresDashboard(data) {
    const totalCitasEl = document.getElementById('totalCitas') || document.getElementById('total-citas');
    const ingresosEl = document.getElementById('ingresosMes') || document.getElementById('total-ingresos') || document.getElementById('ingresosProyectados');

    if (totalCitasEl) {
        totalCitasEl.innerText = data.totalCitas !== undefined ? data.totalCitas : (data.length || 0);
    }

    if (ingresosEl) {
        const monto = data.gananciaReal || data.ingresosReales || data.gananciaEstimada || 0;
        ingresosEl.innerText = `$${monto.toLocaleString()}`;
    }
}

/**
 * 📊 Carga resumen de clientes
 */
async function cargarResumenDashboard(token) {
    try {
        const user = JSON.parse(localStorage.getItem('user'));
        const provId = user?.proveedorId || user?.id;

        const response = await fetch(`http://localhost:5000/api/Dashboard/resumen/${provId}`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });

        if (response.ok) {
            const data = await response.json();
            const clientesEl = document.getElementById('nuevosClientes') || document.getElementById('nuevos-clientes');
            if(clientesEl) clientesEl.innerText = data.nuevosClientes || data.nuevosClientesMes || 0;
            
            actualizarContadoresDashboard(data);
        }
    } catch (error) { 
        console.error("🔥 Error cargando resumen global:", error); 
    }
}

/**
 * 🎨 Asignación de clases CSS
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