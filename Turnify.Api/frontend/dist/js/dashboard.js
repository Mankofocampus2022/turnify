/* ============================================================
   TURNIFY - LÓGICA DEL DASHBOARD (PRO)
   ============================================================ */

// 🧠 BLINDAJE PARA DOCKER/PRODUCCIÓN: Detecta la procedencia de red en tiempo de ejecución. 
// Si corre en localhost o por IP local (celular/tablet), rutea al puerto 5000 de .NET. En la nube mapea al origen limpio.
const API_HOST = (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1')
    ? 'http://localhost:5000'
    : (/^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$/.test(window.location.hostname)
        ? `${window.location.protocol}//${window.location.hostname}:5000`
        : window.location.origin);

const API_BASE_GLOBAL = `${API_HOST}/api`;

// 🧠 CONTROL DE CONCURRENCIA QA SENIOR: Evita que consultas viejas o el auto-refresh sobreescriban los datos actuales
let dashboardAbortController = null;

document.addEventListener('DOMContentLoaded', () => {
    // 1. Puente de Seguridad
    const token = localStorage.getItem('turnify_token') || localStorage.getItem('token');
    
    if (!token) {
        console.warn("⚠️ Sin sesión activa. Redirigiendo...");
        window.location.href = 'login.html';
        return;
    }

    // 🚩 [BLINDAJE] URL Base Dinámica para evitar fallos de puerto/dominio (Sincronizada globalmente)
    const API_BASE = API_BASE_GLOBAL;

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
        cambiarPeriodo('hoy', btnHoy, API_BASE);
    } else {
        // Fallback preventivo si no se encuentra la clase del botón en caliente
        cambiarPeriodo('hoy', { classList: { add: () => {}, remove: () => {} } }, API_BASE);
    }
    
    // Carga de estadísticas globales (Clientes nuevos, etc.)
    cargarResumenDashboard(token, API_BASE);

    // 🛡️ [NUEVO] - Auto-refresh cada 5 minutos para mantener la agenda fresca
    setInterval(() => {
        const activeBtn = document.querySelector('.btn-filter.active');
        if (activeBtn) {
            const texto = activeBtn.innerText.toLowerCase();
            const periodo = texto.includes('hoy') ? 'hoy' : 
                            texto.includes('mañana') ? 'mañana' : 
                            texto.includes('semana') ? 'semana' : 'mes';
            cambiarPeriodo(periodo, activeBtn, API_BASE);
        }
    }, 300000);
});

/**
 * 🔄 FUNCIÓN MAESTRA: Cambia el periodo de la agenda y actualiza la UI
 */
async function cambiarPeriodo(periodo, boton, API_BASE) {
    // 🛡️ REGLA DE ORO: Si API_BASE no viene del HTML, la calculamos aquí
    if (!API_BASE || typeof API_BASE !== 'string') {
        API_BASE = API_BASE_GLOBAL;
    }
    
    if (!boton) return;

    // 🧠 CONTROL DE RÁFAGAS: Cancelamos cualquier petición HTTP previa que siga colgada en la red
    if (dashboardAbortController) {
        dashboardAbortController.abort();
    }
    dashboardAbortController = new AbortController();
    const signal = dashboardAbortController.signal;

    // A. Estética: Marcar el botón como activo si existe físicamente
    if (boton.classList && typeof boton.classList.remove === 'function') {
        document.querySelectorAll('.btn-filter').forEach(b => b.classList.remove('active'));
        boton.classList.add('active');
    }

    // B. Actualizar títulos según el periodo seleccionado
    const titulos = {
        'hoy': 'Agenda de Hoy',
        'diario': 'Agenda de Hoy',
        'mañana': 'Agenda de Mañana',
        'semana': 'Agenda de la Semana',
        'mes': 'Agenda del Mes',
        'mensual': 'Agenda del Mes'
    };
    const sectionTitle = document.getElementById('sectionTitle');
    if (sectionTitle) sectionTitle.innerText = titulos[periodo] || 'Agenda de Turnos';

    // C. Calculation de Fechas para el Backend (Sincronizado con la Zona Horaria de Colombia)
    // Calculamos el desfase de Bogotá (UTC-5) para evitar que dependa de la hora de la máquina de desarrollo
    let d = new Date();
    let utc = d.getTime() + (d.getTimezoneOffset() * 60000);
    let inicio = new Date(utc + (3600000 * -5));
    let fin = new Date(utc + (3600000 * -5));

    // Forzamos limpieza absoluta de horas, minutos y segundos para enviar solo el bloque de la fecha
    inicio.setHours(0, 0, 0, 0);
    fin.setHours(0, 0, 0, 0);

    let periodoParamBackend = periodo;

    if (periodo === 'hoy') {
        periodoParamBackend = 'diario';
    } else if (periodo === 'mañana') {
        inicio.setDate(inicio.getDate() + 1);
        fin.setDate(fin.getDate() + 1);
        periodoParamBackend = 'diario'; // Mapeo semántico para el controlador .NET
    } else if (periodo === 'semana') {
        const day = inicio.getDay();
        const diff = inicio.getDate() - day + (day === 0 ? -6 : 1); 
        inicio.setDate(diff);
        fin.setDate(inicio.getDate() + 7);
    } else if (periodo === 'mes' || periodo === 'mensual') {
        inicio.setDate(1); 
        fin.setMonth(fin.getMonth() + 1);
        fin.setDate(1);
        periodoParamBackend = 'mensual';
    }

    // Formato robusto YYYY-MM-DD sin alterations regionales del motor de JS
    const year = inicio.getFullYear();
    const month = String(inicio.getMonth() + 1).padStart(2, '0');
    const dayStr = String(inicio.getDate()).padStart(2, '0');
    const startStr = `${year}-${month}-${dayStr}`;

    const token = localStorage.getItem('turnify_token') || localStorage.getItem('token');

    const tablaBody = document.getElementById('turnosTable');
    if (tablaBody) {
        tablaBody.innerHTML = '<tr><td colspan="6" style="text-align: center;"><i class="fas fa-spinner fa-spin"></i> Cargando agenda...</td></tr>';
    }

    try {
        const userStr = localStorage.getItem('user');
        if (!userStr) throw new Error("Sesión de usuario perdida.");
        
        const user = JSON.parse(userStr);
        const tipoUsuario = (user?.tipo || user?.tipoProveedor || user?.tipoUsuario || "").toLowerCase();
        const esIndependiente = tipoUsuario.includes("independiente") || user?.esIndependiente === true;
        const provId = user?.proveedorId || user?.id;

        let requestUrl = "";
        
        // 💈 HU-06 & HU-07: Si el usuario es Independiente, invoca el endpoint dedicado
        if (esIndependiente) {
            requestUrl = `${API_BASE}/Dashboard/independiente?periodo=${periodoParamBackend}&fecha=${startStr}`;
        } else {
            if (!provId) return;
            requestUrl = `${API_BASE}/Dashboard/resumen/${provId}?periodo=${periodoParamBackend}&fecha=${startStr}`;
        }

        const response = await fetch(requestUrl, {
            signal: signal,
            headers: { 
                'Authorization': `Bearer ${token}`,
                'Content-Type': 'application/json',
                'Accept': 'application/json',
                'X-TimeZone': Intl.DateTimeFormat().resolvedOptions().timeZone || 'America/Bogota'
            }
        });

        const contentType = response.headers.get("content-type");
        if (!response.ok || !contentType || !contentType.includes("application/json")) {
            throw new Error("Respuesta inválida del servidor");
        }

        const data = await response.json();
        
        // Mapeo unificado para ambas estructuras de respuesta
        const citasRespuesta = data.citas || data.proximasCitas || [];
        renderizarTablaDashboard(citasRespuesta, token, API_BASE);
        actualizarContadoresDashboard(data); 

    } catch (error) { 
        if (error.name === 'AbortError') {
            console.log("📥 Petición duplicada de dashboard cancelada exitosamente.");
            return;
        }
        console.error("🔥 Error al filtrar agenda:", error);
        if (tablaBody) {
            tablaBody.innerHTML = `<tr><td colspan="6" style="text-align: center; color: #ff5e5e;">Error al conectar con el servicio.</td></tr>`;
        }
    }
}

/**
 * 📝 Renderiza las filas (🛡️ BLINDAJE DE ESTADOS EN ACCIONES Y BANDERAS DE CLIENTE NUEVO HU-07)
 */
function renderizarTablaDashboard(citas, token, API_BASE) {
    const tabla = document.getElementById('turnosTable');
    if (!tabla) return;

    if (!citas || citas.length === 0) {
        tabla.innerHTML = '<tr><td colspan="6" style="text-align: center; padding: 20px; color: #ccc;">No hay citas agendadas para este periodo.</td></tr>';
        return;
    }

    tabla.innerHTML = citas.map(c => {
        const estado = (c.estado || c.Estado || "pendiente").toLowerCase();
        const badgeClass = getEstadoClass(estado);
        
        // 🚩 LÓGICA DE ACCIONES SEGÚN EL ESTADO REAL
        let celdaAccion = "";
        if (estado === 'completada' || estado === 'confirmada') {
            celdaAccion = `<span style="color: #48c1b5; font-size: 0.8rem;"><i class="fas fa-check-circle"></i> Validado</span>`;
        } else if (estado === 'cancelada') {
            celdaAccion = `<span style="color: #ff5e5e; font-size: 0.8rem;"><i class="fas fa-exclamation-triangle"></i> Anulada</span>`;
        } else {
            celdaAccion = `<span style="color: #888; font-size: 0.8rem;"><i class="fas fa-clock"></i> Pendiente</span>`;
        }
        
        // 💈 HU-07 CA1: Inyección visual del distintivo "Cliente Nuevo"
        const esNuevo = c.esNuevoCliente === true || c.EsNuevoCliente === true;
        const badgeCliente = esNuevo 
            ? `<span style="background-color: rgba(72, 193, 181, 0.15); color: #48c1b5; border: 1px solid #48c1b5; font-size: 0.68rem; padding: 2px 6px; border-radius: 4px; margin-left: 6px; font-weight: 600;"><i class="fas fa-user-plus"></i> Nuevo</span>`
            : '';

        const nombreCliente = c.cliente || c.Cliente || 'Sin nombre';

        return `
            <tr>
                <td style="color: #48c1b5; font-weight: bold;"><i class="far fa-clock"></i> ${c.hora || c.Hora}</td> 
                <td>${c.fecha ? String(c.fecha).split('T')[0] : 'Hoy'}</td>
                <td><strong>${nombreCliente}</strong>${badgeCliente}</td>
                <td>${c.servicio || c.Servicio || 'Servicio'}</td>
                <td><span class="status-pill ${badgeClass}">${estado}</span></td>
                <td>${celdaAccion}</td>
            </tr>
        `;
    }).join('');
}

/**
 * 🔢 ACTUALIZACIÓN DE CONTADORES (HU-06: SOPORTE PARA INGRESOS BRUTOS Y NUEVOS CLIENTES)
 */
function actualizarContadoresDashboard(data) {
    const totalCitasEl = document.getElementById('totalCitas') || document.getElementById('total-citas');
    const ingresosEl = document.getElementById('ingresosMes') || document.getElementById('total-ingresos');
    const clientesEl = document.getElementById('nuevosClientes') || document.getElementById('nuevos-clientes');

    const listaCitas = data.citas || data.proximasCitas || [];

    if (totalCitasEl) {
        totalCitasEl.innerText = data.totalCitas !== undefined ? data.totalCitas : listaCitas.length;
    }

    // HU-07 CA2: Prioriza la métrica directa de nuevos clientes entregada por el backend
    if (clientesEl) {
        if (data.totalNuevosClientes !== undefined) {
            clientesEl.innerText = data.totalNuevosClientes;
        } else if (data.nuevosClientesTotales !== undefined) {
            clientesEl.innerText = data.nuevosClientesTotales;
        } else {
            const clientesUnicos = new Set(listaCitas.map(c => c.clienteId || c.ClienteId || c.cliente || c.Cliente));
            clientesEl.innerText = listaCitas.length > 0 ? clientesUnicos.size : 0;
        }
    }

    // HU-06 CA1: Muestra los ingresos 100% brutos reales o proyectados
    if (ingresosEl) {
        let montoCalculado = 0;

        if (data.ingresosRealesBrutos !== undefined && data.ingresosRealesBrutos > 0) {
            montoCalculado = data.ingresosRealesBrutos;
        } else if (data.ingresosProyectadosBrutos !== undefined) {
            montoCalculado = data.ingresosProyectadosBrutos;
        } else if (data.gananciaReal !== undefined) {
            montoCalculado = data.gananciaReal;
        } else if (listaCitas.length > 0) {
            montoCalculado = listaCitas.reduce((acc, c) => {
                const valor = c.precioPactado || c.PrecioPactado || c.precio || c.Precio || 0;
                const est = (c.estado || c.Estado || "").toLowerCase().trim();
                if (est.includes("completad") || est.includes("confirmad") || est.includes("pendiente")) {
                    return acc + parseFloat(valor);
                }
                return acc;
            }, 0);
        }

        ingresosEl.innerText = new Intl.NumberFormat('es-CO', {
            style: 'currency',
            currency: 'COP',
            minimumFractionDigits: 0
        }).format(montoCalculado);
    }
}

async function cargarResumenDashboard(token, API_BASE) {
    if (!API_BASE) API_BASE = API_BASE_GLOBAL;
    try {
        const userStr = localStorage.getItem('user');
        if (!userStr) return;
        const user = JSON.parse(userStr);
        const tipoUsuario = (user?.tipo || user?.tipoProveedor || user?.tipoUsuario || "").toLowerCase();
        const esIndependiente = tipoUsuario.includes("independiente") || user?.esIndependiente === true;
        const provId = user?.proveedorId || user?.id;

        let requestUrl = esIndependiente 
            ? `${API_BASE}/Dashboard/independiente?periodo=diario`
            : `${API_BASE}/Dashboard/resumen/${provId}?periodo=diario`;

        const response = await fetch(requestUrl, {
            headers: { 
                'Authorization': `Bearer ${token}`,
                'X-TimeZone': Intl.DateTimeFormat().resolvedOptions().timeZone || 'America/Bogota'
            }
        });
        if (response.ok) {
            const data = await response.json();
            actualizarContadoresDashboard(data);
        }
    } catch (e) { 
        console.error("❌ Error cargando resumen global del dashboard:", e); 
    }
}

function getEstadoClass(estado) {
    const est = estado.toLowerCase();
    if (est.includes('completado') || est.includes('confirmada')) return 'status-activo';
    if (est.includes('cancelada')) return 'status-bloqueado';
    return 'status-pendiente'; 
}

function logout() {
    if (confirm("¿Seguro que quieres salir? te vamos a extrañar mucho hasta que vuelvas.")) {
        localStorage.clear();
        window.location.href = 'login.html';
    }
}

window.cambiarPeriodo = cambiarPeriodo;