/* ============================================================
   TURNIFY - LÓGICA DEL DASHBOARD (PRO / HOTFIX LIQUIDACIÓN)
   ============================================================ */

// 🧠 BLINDAJE PARA DOCKER/PRODUCCIÓN: Detecta la procedencia de red en tiempo de ejecución. 
const API_HOST = (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1')
    ? 'http://localhost:5000'
    : (/^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$/.test(window.location.hostname)
        ? `${window.location.protocol}//${window.location.hostname}:5000`
        : window.location.origin);

const API_BASE_GLOBAL = `${API_HOST}/api`;

// 🧠 CONTROL DE CONCURRENCIA QA SENIOR: Evita que consultas viejas sobreescriban los datos
let dashboardAbortController = null;

/**
 * 🎨 HELPER DE CLASES DE ESTADO
 */
function getEstadoClass(estado) {
    if (!estado) return 'status-pendiente';
    const est = String(estado).toLowerCase();
    if (est.includes('completad') || est.includes('confirmad') || est.includes('finalizad')) return 'status-activo';
    if (est.includes('cancelad') || est.includes('anulad')) return 'status-bloqueado';
    return 'status-pendiente'; 
}

/**
 * 🛠️ HELPER MULTI-ID PARA ATACAR TODOS LOS NODOS DEL DOM
 */
function actualizarKPIsMultiples(idList, valorFormateado) {
    idList.forEach(id => {
        const el = document.getElementById(id) || document.querySelector(`.${id}`);
        if (el) el.innerText = valorFormateado;
    });
}

/**
 * 🛠️ FUNCIÓN DE DETECCIÓN AVANZADA DE PROFESIONAL INDEPENDIENTE
 */
function evaluarEsIndependiente(userObj, token) {
    const flagLocal = localStorage.getItem('es_independiente') || localStorage.getItem('turnify_es_independiente');
    if (flagLocal === 'false') return false;
    if (flagLocal === 'true') return true;

    if (userObj) {
        if (userObj.esIndependiente === false || userObj.EsIndependiente === false) return false;
        if (userObj.esIndependiente === true || userObj.EsIndependiente === true) return true;
    }

    const rol = String(userObj?.rol || userObj?.rolNombre || localStorage.getItem('usuario_rol') || "").toLowerCase();
    if (rol.includes("staff") || rol.includes("admin") || rol.includes("administrador")) return false;

    const tipo = String(userObj?.tipo || userObj?.tipoProveedor || userObj?.tipoUsuario || userObj?.tipoModelo || "").toLowerCase();
    if (rol.includes("independiente") || rol.includes("autonomo") || tipo.includes("independiente")) return true;

    if (token) {
        try {
            const base64Url = token.split('.')[1];
            if (base64Url) {
                const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
                const jsonPayload = decodeURIComponent(atob(base64).split('').map(c => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2)).join(''));
                const tokenData = JSON.parse(jsonPayload);

                const claimEsInd = tokenData.EsIndependiente || tokenData.esIndependiente;
                if (claimEsInd === "false" || claimEsInd === false) return false;
                if (claimEsInd === "true" || claimEsInd === true) return true;

                const claimRol = String(tokenData.role || tokenData["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] || "").toLowerCase();
                if (claimRol.includes("staff") || claimRol.includes("admin")) return false;
                if (claimRol.includes("independiente")) return true;
            }
        } catch (e) {
            console.warn("⚠️ No se pudo decodificar las claims del Token:", e);
        }
    }

    return false;
}

document.addEventListener('DOMContentLoaded', () => {
    const token = localStorage.getItem('turnify_token') || localStorage.getItem('token');
    
    if (!token) {
        console.warn("⚠️ Sin sesión activa. Redirigiendo...");
        window.location.href = 'login.html';
        return;
    }

    const API_BASE = API_BASE_GLOBAL;

    const userStr = localStorage.getItem('user');
    let nombreFinal = "Darwin";
    let userRoleStr = "Administración / Staff";
    let esIndependiente = false;

    if (userStr) {
        try {
            const userObj = JSON.parse(userStr);
            nombreFinal = userObj.nombre || userObj.Nombre || nombreFinal;
            
            esIndependiente = evaluarEsIndependiente(userObj, token);
            userRoleStr = esIndependiente ? "Proveedor Independiente" : "Administración / Staff";
            
            const thStaffSilla = document.getElementById('thStaffSilla');
            if (thStaffSilla) thStaffSilla.style.display = esIndependiente ? 'none' : 'table-cell';

            const containerFiltro = document.getElementById('containerFiltroPuesto');
            if (containerFiltro) containerFiltro.style.display = esIndependiente ? 'none' : 'flex';
        } catch (e) { 
            console.error("❌ Error parseando objeto de usuario", e); 
        }
    }

    const welcomeText = document.getElementById('welcomeText');
    if (welcomeText) welcomeText.innerHTML = `¡Qué más, <span style="color: #48c1b5;">${nombreFinal}</span>!`;

    const userRoleEl = document.getElementById('userRole');
    if (userRoleEl) userRoleEl.innerText = userRoleStr;

    const btnHoy = document.querySelector(".btn-filter");
    if (btnHoy) {
        cambiarPeriodo('hoy', btnHoy, API_BASE);
    } else {
        cambiarPeriodo('hoy', { classList: { add: () => {}, remove: () => {} } }, API_BASE);
    }
    
    cargarResumenDashboard(token, API_BASE);

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
    if (!API_BASE || typeof API_BASE !== 'string') API_BASE = API_BASE_GLOBAL;
    if (!boton) return;

    if (dashboardAbortController) dashboardAbortController.abort();
    dashboardAbortController = new AbortController();
    const signal = dashboardAbortController.signal;

    if (boton.classList && typeof boton.classList.remove === 'function') {
        document.querySelectorAll('.btn-filter').forEach(b => b.classList.remove('active'));
        boton.classList.add('active');
    }

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

    let d = new Date();
    let utc = d.getTime() + (d.getTimezoneOffset() * 60000);
    let inicio = new Date(utc + (3600000 * -5));
    let fin = new Date(utc + (3600000 * -5));

    inicio.setHours(0, 0, 0, 0);
    fin.setHours(0, 0, 0, 0);

    let periodoParamBackend = periodo;

    if (periodo === 'hoy') {
        periodoParamBackend = 'diario';
    } else if (periodo === 'mañana') {
        inicio.setDate(inicio.getDate() + 1);
        fin.setDate(fin.getDate() + 1);
        periodoParamBackend = 'diario'; 
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

    const year = inicio.getFullYear();
    const month = String(inicio.getMonth() + 1).padStart(2, '0');
    const dayStr = String(inicio.getDate()).padStart(2, '0');
    const startStr = `${year}-${month}-${dayStr}`;

    const token = localStorage.getItem('turnify_token') || localStorage.getItem('token');

    const tablaBody = document.getElementById('turnosTable');
    if (tablaBody) {
        tablaBody.innerHTML = '<tr><td colspan="7" style="text-align: center;"><i class="fas fa-spinner fa-spin"></i> Cargando agenda...</td></tr>';
    }

    try {
        const userStr = localStorage.getItem('user');
        if (!userStr) throw new Error("Sesión de usuario perdida.");
        
        const user = JSON.parse(userStr);
        const esIndependiente = evaluarEsIndependiente(user, token);
        const provId = user?.proveedorId || user?.id;

        const thStaffSilla = document.getElementById('thStaffSilla');
        if (thStaffSilla) thStaffSilla.style.display = esIndependiente ? 'none' : 'table-cell';

        const containerFiltro = document.getElementById('containerFiltroPuesto');
        if (containerFiltro) containerFiltro.style.display = esIndependiente ? 'none' : 'flex';

        let requestUrl = esIndependiente
            ? `${API_BASE}/Dashboard/independiente?periodo=${periodoParamBackend}&fecha=${startStr}`
            : `${API_BASE}/Dashboard/resumen/${provId}?periodo=${periodoParamBackend}&fecha=${startStr}`;

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
        const citasRespuesta = data.citas || data.proximasCitas || [];
        
        renderizarTablaDashboard(citasRespuesta, token, API_BASE, esIndependiente);
        actualizarContadoresDashboard(data, esIndependiente); 

        // 🚀 HOTFIX: Pasamos citasRespuesta a la capa de movimientos para cruzar comisiones si el endpoint falla
        cargarDetalleMovimientosStrategy(token, API_BASE, periodoParamBackend, startStr, citasRespuesta);

    } catch (error) { 
        if (error.name === 'AbortError') return;
        console.error("🔥 Error al filtrar agenda:", error);
        if (tablaBody) {
            tablaBody.innerHTML = `<tr><td colspan="7" style="text-align: center; color: #ff5e5e;">Error al conectar con el servicio.</td></tr>`;
        }
    }
}

/**
 * 🚀 CONSUMO DEL ENDPOINT DE MOVIMIENTOS CON SOPORTE DE RECONCILIACIÓN EN MEMORIA
 */
async function cargarDetalleMovimientosStrategy(token, API_BASE, periodo = "diario", fechaStr = "", citasAgenda = []) {
    try {
        const url = `${API_BASE}/Dashboard/movimientos?periodo=${periodo}&fecha=${fechaStr}`;
        const resp = await fetch(url, {
            headers: {
                'Authorization': `Bearer ${token}`,
                'Accept': 'application/json',
                'X-TimeZone': Intl.DateTimeFormat().resolvedOptions().timeZone || 'America/Bogota'
            }
        });

        if (resp.ok) {
            const data = await resp.json();
            actualizarUIStrategyMovimientos(data, citasAgenda);
        } else {
            console.warn("⚠️ Endpoint de movimientos no disponible. Sintetizando desde agenda...");
            actualizarUIStrategyMovimientos({ movimientos: [] }, citasAgenda);
        }
    } catch (err) {
        console.error("❌ Error al consumir api/dashboard/movimientos:", err);
        actualizarUIStrategyMovimientos({ movimientos: [] }, citasAgenda);
    }
}

/**
 * 🎨 RENDERIZADO DE LIQUIDACIÓN Y RECALCULO MULTI-ID DE KPIS (FILTRADO ESTRICTO DE CITAS COMPLETADAS)
 */
function actualizarUIStrategyMovimientos(data, citasAgenda = []) {
    const userStr = localStorage.getItem('user');
    const token = localStorage.getItem('turnify_token') || localStorage.getItem('token');
    const userObj = userStr ? JSON.parse(userStr) : null;
    
    const esIndependiente = evaluarEsIndependiente(userObj, token) || (data && data.tipoModelo === "Independiente");
    const fmtCOP = new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', minimumFractionDigits: 0 });

    let movimientos = (data && data.movimientos && data.movimientos.length > 0) ? data.movimientos : [];

    // 🚀 RECONCILIACIÓN DE MOVIMIENTOS (Si venían de la agenda)
    if (movimientos.length === 0 && citasAgenda.length > 0) {
        movimientos = citasAgenda.map(c => {
            const mTotal = parseFloat(c.precioPactado || c.PrecioPactado || c.precio || c.Precio || 0);
            const pct = parseFloat(c.porcentajeComision || c.PorcentajeComision || 20);
            const mComision = esIndependiente ? 0 : (mTotal * (pct / 100));
            
            return {
                citaId: c.id || c.citaId,
                fecha: c.fecha,
                estado: c.estado || c.Estado || "pendiente",
                clienteNombre: c.cliente || c.Cliente || "Cliente General",
                servicioNombre: c.servicio || c.Servicio || "Servicio General",
                especialistaNombre: c.empleadoAsignado || c.EmpleadoAsignado || "Especialista Asignado",
                montoTotal: mTotal,
                porcentajeComision: pct,
                montoComisionEspecialista: mComision,
                ingresoNeto: mTotal - mComision
            };
        });
    } else {
        // Enriquecer movimientos existentes si tienen "No Asignado", falta de estado o comisiones en $0
        movimientos.forEach(m => {
            const matchCita = citasAgenda.find(c => (c.id || c.citaId) === m.citaId);
            if (matchCita) {
                if (!m.estado && (matchCita.estado || matchCita.Estado)) {
                    m.estado = matchCita.estado || matchCita.Estado;
                }
                if (!m.especialistaNombre || m.especialistaNombre === "No Asignado") {
                    m.especialistaNombre = matchCita.empleadoAsignado || matchCita.EmpleadoAsignado || "Especialista Asignado";
                }
                const pct = parseFloat(matchCita.porcentajeComision || matchCita.PorcentajeComision || 20);
                if (!esIndependiente && (m.montoComisionEspecialista === 0 || !m.montoComisionEspecialista)) {
                    m.porcentajeComision = pct;
                    m.montoComisionEspecialista = (m.montoTotal * pct) / 100;
                    m.ingresoNeto = m.montoTotal - m.montoComisionEspecialista;
                }
            }
        });
    }

    // 🎯 RECALCULO ESTRICTO DE VALORES ACUMULADOS (SOLO CITAS COMPLETADAS)
    let acumuladoBruto = 0;
    let acumuladoComisiones = 0;
    let acumuladoProyectado = 0;

    movimientos.forEach(item => {
        const estadoRaw = String(item.estado || "pendiente").toLowerCase().trim();
        const esCompletada = estadoRaw.includes('completad') || estadoRaw.includes('confirmad') || estadoRaw.includes('finalizad') || estadoRaw.includes('pagad');
        const esValida = !estadoRaw.includes('cancelad') && !estadoRaw.includes('anulad');

        const mBruto = parseFloat(item.montoTotal) || 0;
        const mComision = esIndependiente ? 0 : (parseFloat(item.montoComisionEspecialista) || 0);

        if (esCompletada) {
            acumuladoBruto += mBruto;
            acumuladoComisiones += mComision;
        }

        if (esValida) {
            acumuladoProyectado += (mBruto - mComision);
        }
    });

    const acumuladoNeto = acumuladoBruto - acumuladoComisiones;

    // 🚀 ACTUALIZACIÓN DE TODAS LAS VARIANTES DE IDS EN KPIS DE RESUMEN
    actualizarKPIsMultiples(['montoTotalAcumulado', 'totalRecaudado', 'totalRecaudadoBruto', 'total-recaudado'], fmtCOP.format(acumuladoBruto));
    actualizarKPIsMultiples(['comisionesTotalesPagadas', 'comisionesEspecialistas', 'comisiones-especialistas', 'totalComisiones', 'total-comisiones'], fmtCOP.format(acumuladoComisiones));
    actualizarKPIsMultiples(['ingresoNetoTotal', 'ingresoNetoReal', 'total-ingresos', 'ingresoNeto'], fmtCOP.format(acumuladoNeto));
    actualizarKPIsMultiples(['ingresosProyectados', 'ingresosMes', 'ingresoProyectado'], fmtCOP.format(acumuladoProyectado));

    const badgeModeloEl = document.getElementById('badgeModeloNegocio');
    if (badgeModeloEl) {
        badgeModeloEl.innerText = `Modelo: ${esIndependiente ? 'Independiente' : 'Estándar'}`;
        badgeModeloEl.className = esIndependiente ? "badge-modelo-independiente" : "badge-modelo-dependiente";
    }

    // Ocultar cabeceras y tarjetas según el tipo de modelo
    const thEspecialista = document.getElementById('thMovEspecialista');
    const thDeduccion = document.getElementById('thMovDeduccion');
    if (thEspecialista) thEspecialista.style.display = esIndependiente ? 'none' : 'table-cell';
    if (thDeduccion) thDeduccion.style.display = esIndependiente ? 'none' : 'table-cell';

    const cardComisionesNode = document.getElementById('comisionesTotalesPagadas') || document.getElementById('comisionesEspecialistas');
    if (cardComisionesNode) {
        const cardContainer = cardComisionesNode.closest('.stat-card') || cardComisionesNode.closest('.card');
        if (cardContainer) cardContainer.style.display = esIndependiente ? 'none' : 'flex';
    }

    // Renderizado de la Tabla Inferior (Detalle de Movimientos Financieros)
    const tablaMov = document.getElementById('tablaMovimientosStrategy') || document.getElementById('bodyMovimientos') || document.getElementById('turnosMovimientosTable');
    if (!tablaMov) return;

    const colSpanTotal = esIndependiente ? 5 : 7;

    if (movimientos.length === 0) {
        tablaMov.innerHTML = `<tr><td colspan="${colSpanTotal}" style="text-align: center; color: #888; padding: 15px;">Sin movimientos financieros registrados.</td></tr>`;
        return;
    }

    tablaMov.innerHTML = movimientos.map(m => {
        const fechaFormatted = m.fecha ? String(m.fecha).split('T')[0] : '--';
        const celdaEspecialista = esIndependiente ? '' : `<td>${m.especialistaNombre || 'Especialista Asignado'}</td>`;
        const celdaDeduccion = esIndependiente ? '' : `<td style="color: #ff5e5e; font-weight: bold;">-${fmtCOP.format(m.montoComisionEspecialista || 0)}</td>`;

        return `
            <tr>
                <td>${fechaFormatted}</td>
                <td><strong>${m.clienteNombre || 'Cliente'}</strong></td>
                <td>${m.servicioNombre || 'Servicio'}</td>
                ${celdaEspecialista}
                <td style="font-weight: bold; color: #38bdf8;">${fmtCOP.format(m.montoTotal || 0)}</td>
                ${celdaDeduccion}
                <td style="font-weight: bold; color: #48c1b5;">${fmtCOP.format(m.ingresoNeto || 0)}</td>
            </tr>
        `;
    }).join('');
}

/**
 * 📝 Renderiza las filas de citas en la Agenda Principal
 */
function renderizarTablaDashboard(citas, token, API_BASE, esIndependiente = false) {
    const tabla = document.getElementById('turnosTable');
    if (!tabla) return;

    if (!citas || citas.length === 0) {
        tabla.innerHTML = '<tr><td colspan="7" style="text-align: center; padding: 20px; color: #ccc;">No hay citas agendadas para este periodo.</td></tr>';
        return;
    }

    const filtroPuestoEl = document.getElementById('filtroEstacion') || document.getElementById('selectEstacion');
    const puestoSeleccionado = (filtroPuestoEl && !esIndependiente) ? filtroPuestoEl.value : "todos";

    const citasFiltradas = citas.filter(c => {
        if (esIndependiente || !filtroPuestoEl || puestoSeleccionado === "todos") return true;
        const estacion = (c.estacion || c.Estacion || "").toLowerCase();
        return estacion.includes(puestoSeleccionado.toLowerCase());
    });

    if (citasFiltradas.length === 0) {
        tabla.innerHTML = '<tr><td colspan="7" style="text-align: center; padding: 20px; color: #ccc;">No hay citas registradas para el puesto seleccionado.</td></tr>';
        return;
    }

    tabla.innerHTML = citasFiltradas.map(c => {
        const estado = (c.estado || c.Estado || "pendiente").toLowerCase();
        const badgeClass = getEstadoClass(estado);
        
        let celdaAccion = "";
        if (estado === 'completada' || estado === 'confirmada' || estado === 'completado' || estado === 'finalizada') {
            celdaAccion = `<span style="color: #48c1b5; font-size: 0.8rem;"><i class="fas fa-check-circle"></i> Validado</span>`;
        } else if (estado === 'cancelada' || estado === 'anulada') {
            celdaAccion = `<span style="color: #ff5e5e; font-size: 0.8rem;"><i class="fas fa-exclamation-triangle"></i> Anulada</span>`;
        } else {
            celdaAccion = `<span style="color: #888; font-size: 0.8rem;"><i class="fas fa-clock"></i> Pendiente</span>`;
        }
        
        const esNuevo = c.esNuevoCliente === true || c.EsNuevoCliente === true;
        const badgeCliente = esNuevo 
            ? `<span class="badge-nuevo-cliente" style="margin-left: 5px;"><i class="fas fa-star"></i> Nuevo</span>`
            : '';

        const nombreCliente = c.cliente || c.Cliente || 'Sin nombre';
        const empleadoNombre = c.empleadoAsignado || c.EmpleadoAsignado || 'Especialista Asignado';
        const estacionNombre = c.estacionAsignada || c.estacion || c.Estacion || 'Local';

        const tipoContrato = (c.tipoContratoEmpleado || c.TipoContratoEmpleado || "").toLowerCase();
        const precioSilla = c.precioSilla || c.PrecioSilla;
        const estadoPagoSilla = c.estadoPagoSilla || c.EstadoPagoSilla || "Al día";

        let badgeEsquema = '';
        if (tipoContrato.includes("silla") || tipoContrato.includes("fijo") || tipoContrato.includes("arriendo")) {
            const detallePrecio = precioSilla ? `: ${new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', minimumFractionDigits: 0 }).format(precioSilla)}` : '';
            badgeEsquema = `<br><span class="badge-silla-fija"><i class="fas fa-chair"></i> Silla Fija${detallePrecio} (${estadoPagoSilla})</span>`;
        } else {
            const pct = c.porcentajeComision || c.PorcentajeComision || 20;
            badgeEsquema = `<br><span class="badge-comision"><i class="fas fa-percentage"></i> Comisión (${pct}%)</span>`;
        }

        const celdaStaffSilla = esIndependiente 
            ? '' 
            : `<td class="col-staff-silla" style="display: table-cell; font-size: 0.8rem; color: #cbd5e1;">
                <div><i class="fas fa-user-tie"></i> <strong>${empleadoNombre}</strong></div>
                <div><i class="fas fa-chair"></i> ${estacionNombre}</div>
                ${badgeEsquema}
               </td>`;

        const fechaTexto = c.fecha ? String(c.fecha).split('T')[0] : 'Hoy';

        return `
            <tr>
                <td style="color: #48c1b5; font-weight: bold;"><i class="far fa-clock"></i> ${c.hora || c.Hora || '--:--'}</td> 
                <td>${fechaTexto}</td>
                <td><strong>${nombreCliente}</strong>${badgeCliente}</td>
                <td>${c.servicio || c.Servicio || 'Servicio'}</td>
                ${celdaStaffSilla}
                <td><span class="status-pill ${badgeClass}">${estado}</span></td>
                <td>${celdaAccion}</td>
            </tr>
        `;
    }).join('');
}

/**
 * 🔢 ACTUALIZACIÓN DE CONTADORES GLOBALES
 */
function actualizarContadoresDashboard(data, esIndependiente = false) {
    const totalCitasEl = document.getElementById('totalCitas') || document.getElementById('total-citas');
    const clientesEl = document.getElementById('nuevosClientes') || document.getElementById('nuevos-clientes');
    const listaCitas = data.citas || data.proximasCitas || [];

    if (totalCitasEl) {
        totalCitasEl.innerText = data.totalCitas !== undefined ? data.totalCitas : listaCitas.length;
    }

    if (clientesEl) {
        if (data.totalNuevosClientes !== undefined) {
            clientesEl.innerText = data.totalNuevosClientes;
        } else if (data.nuevosClientesTotales !== undefined) {
            clientesEl.innerText = data.nuevosClientesTotales;
        } else {
            const conteoNuevos = listaCitas.filter(c => c.esNuevoCliente === true || c.EsNuevoCliente === true).length;
            clientesEl.innerText = conteoNuevos;
        }
    }
}

async function cargarResumenDashboard(token, API_BASE) {
    if (!API_BASE) API_BASE = API_BASE_GLOBAL;
    try {
        const userStr = localStorage.getItem('user');
        if (!userStr) return;
        const user = JSON.parse(userStr);
        const esIndependiente = evaluarEsIndependiente(user, token);
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
            actualizarContadoresDashboard(data, esIndependiente);
        }
    } catch (e) { 
        console.error("❌ Error cargando resumen global del dashboard:", e); 
    }
}

function logout() {
    if (confirm("¿Seguro que quieres salir? te vamos a extrañar mucho hasta que vuelvas.")) {
        localStorage.clear();
        window.location.href = 'login.html';
    }
}

// Exposición global para eventos HTML inline
window.cambiarPeriodo = cambiarPeriodo;
window.logout = logout;
window.evaluarEsIndependiente = evaluarEsIndependiente;
window.cargarDetalleMovimientosStrategy = cargarDetalleMovimientosStrategy;