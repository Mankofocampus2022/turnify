/* =========================================
   TURNIFY - LÓGICA DEL DASHBOARD (PRO)
   ========================================= */

document.addEventListener('DOMContentLoaded', () => {
    // 1. Puente de Seguridad
    const token = localStorage.getItem('turnify_token') || localStorage.getItem('token');
    
    if (!token) {
        console.warn("⚠️ Sin sesión activa. Redirigiendo...");
        window.location.href = 'login.html';
        return;
    }

    // 🚩 [BLINDAJE] URL Base Dinámica para evitar fallos de puerto/dominio
    const API_BASE = window.location.origin + '/api';

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
        API_BASE = window.location.origin + '/api';
    }
    
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

    // C. Cálculo de Fechas para el Backend
    let inicio = new Date();
    let fin = new Date();

    if (periodo === 'mañana') {
        inicio.setDate(inicio.getDate() + 1);
        fin.setDate(fin.getDate() + 1);
    } else if (periodo === 'semana') {
        const day = inicio.getDay();
        const diff = inicio.getDate() - day + (day === 0 ? -6 : 1); 
        inicio.setDate(diff);
        fin.setDate(inicio.getDate() + 7);
    } else if (periodo === 'mes') {
        inicio.setDate(1); 
        fin.setMonth(fin.getMonth() + 1);
        fin.setDate(1);
    }

    if (periodo === 'hoy') {
        inicio = new Date(); 
    }

    const startStr = inicio.toLocaleDateString('en-CA'); 
    const token = localStorage.getItem('turnify_token') || localStorage.getItem('token');

    const tablaBody = document.getElementById('turnosTable');
    if (tablaBody) {
        tablaBody.innerHTML = '<tr><td colspan="6" style="text-align: center;"><i class="fas fa-spinner fa-spin"></i> Cargando agenda...</td></tr>';
    }

    try {
        const userStr = localStorage.getItem('user');
        if (!userStr) throw new Error("Sesión de usuario perdida.");
        
        const user = JSON.parse(userStr);
        const provId = user?.proveedorId || user?.id;

        if (!provId) return;

        const response = await fetch(`${API_BASE}/Dashboard/resumen/${provId}?periodo=${periodo}&fecha=${startStr}`, {
            headers: { 
                'Authorization': `Bearer ${token}`,
                'Content-Type': 'application/json',
                'Accept': 'application/json'
            }
        });

        const contentType = response.headers.get("content-type");
        if (!response.ok || !contentType || !contentType.includes("application/json")) {
            throw new Error("Respuesta inválida del servidor");
        }

        const data = await response.json();
        renderizarTablaDashboard(data.proximasCitas || [], token, API_BASE);
        actualizarContadoresDashboard(data); 

    } catch (error) { 
        console.error("🔥 Error al filtrar agenda:", error);
        if (tablaBody) {
            tablaBody.innerHTML = `<tr><td colspan="6" style="text-align: center; color: #ff5e5e;">Error al conectar con el servicio.</td></tr>`;
        }
    }
}

/**
 * 📝 Renderiza las filas (🛡️ BLINDAJE DE ESTADOS EN ACCIONES)
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
        
        return `
            <tr>
                <td style="color: #48c1b5; font-weight: bold;"><i class="far fa-clock"></i> ${c.hora || c.Hora}</td> 
                <td>${c.fecha ? c.fecha.split('T')[0] : 'Hoy'}</td>
                <td><strong>${c.cliente || c.Cliente || 'Sin nombre'}</strong></td>
                <td>${c.servicio || c.Servicio || 'Servicio'}</td>
                <td><span class="status-pill ${badgeClass}">${estado}</span></td>
                <td>${celdaAccion}</td>
            </tr>
        `;
    }).join('');
}

/**
 * 🔢 ACTUALIZACIÓN DE CONTADORES
 */
function actualizarContadoresDashboard(data) {
    const totalCitasEl = document.getElementById('totalCitas') || document.getElementById('total-citas');
    const ingresosEl = document.getElementById('ingresosMes') || document.getElementById('total-ingresos');
    const clientesEl = document.getElementById('nuevosClientes') || document.getElementById('nuevos-clientes');

    const listaCitas = data.proximasCitas || data.citas || [];

    if (totalCitasEl) totalCitasEl.innerText = listaCitas.length;

    if (clientesEl) {
        const clientesUnicos = new Set(listaCitas.map(c => c.clienteId || c.ClienteId || c.cliente || c.Cliente));
        clientesEl.innerText = listaCitas.length > 0 ? clientesUnicos.size : 0;
    }

    if (ingresosEl) {
        let montoCalculado = 0;
        if (listaCitas.length > 0) {
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
    if (!API_BASE) API_BASE = window.location.origin + '/api';
    try {
        const userStr = localStorage.getItem('user');
        if (!userStr) return;
        const user = JSON.parse(userStr);
        const provId = user?.proveedorId || user?.id;
        const response = await fetch(`${API_BASE}/Dashboard/resumen/${provId}`, {
            headers: { 'Authorization': `Bearer ${token}` }
        });
        if (response.ok) {
            const data = await response.json();
            actualizarContadoresDashboard(data);
        }
    } catch (e) { console.error(e); }
}

function getEstadoClass(estado) {
    const est = estado.toLowerCase();
    if (est.includes('completado') || est.includes('confirmada')) return 'status-activo';
    if (est.includes('cancelada')) return 'status-bloqueado';
    return 'status-pendiente'; 
}

function logout() {
    if (confirm("¿Seguro que quieres salir?")) {
        localStorage.clear();
        window.location.href = 'login.html';
    }
}

window.cambiarPeriodo = cambiarPeriodo;