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

/**
 * 🎨 HELPER DE CLASES DE ESTADO: Mapea estados a sus clases CSS correspondientes
 */
function getEstadoClass(estado) {
    if (!estado) return 'status-pendiente';
    const est = String(estado).toLowerCase();
    if (est.includes('completad') || est.includes('confirmad')) return 'status-activo';
    if (est.includes('cancelad') || est.includes('anulad')) return 'status-bloqueado';
    return 'status-pendiente'; 
}

/**
 * 🛠️ FUNCIÓN DE DETECCIÓN AVANZADA: Determina si el usuario actual es un Proveedor Independiente
 */
function evaluarEsIndependiente(userObj, token) {
    if (!userObj && !token) return false;

    const tipo = String(userObj?.tipo || userObj?.tipoProveedor || userObj?.tipoUsuario || userObj?.tipoModelo || "").toLowerCase();
    const rol = String(userObj?.rol || userObj?.rolNombre || localStorage.getItem('usuario_rol') || "").toLowerCase();
    const esFlag = userObj?.esIndependiente === true || userObj?.esProveedorIndependiente === true;

    // 🚀 BLINDAJE CLAVE: Si el rol es "Proveedor" o contiene "independiente", "autonomo", etc.
    if (esFlag || rol === "proveedor" || rol.includes("independiente") || rol.includes("autonomo") || tipo.includes("independiente")) {
        return true;
    }

    // Inspección profunda del Token JWT
    if (token) {
        try {
            const base64Url = token.split('.')[1];
            if (base64Url) {
                const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
                const jsonPayload = decodeURIComponent(atob(base64).split('').map(c => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2)).join(''));
                const tokenData = JSON.parse(jsonPayload);

                const claimRol = String(tokenData.role || tokenData["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] || "").toLowerCase();
                const claimTipo = String(tokenData.tipo || tokenData.tipoProveedor || "").toLowerCase();

                if (claimRol === "proveedor" || claimRol.includes("independiente") || claimTipo.includes("independiente")) {
                    return true;
                }
            }
        } catch (e) {
            console.warn("⚠️ No se pudo decodificar las claims del Token:", e);
        }
    }

    return false;
}

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

    // 2. Recuperar el nombre real y tipo de usuario (Blindaje contra el texto "PRUEBA")
    const userStr = localStorage.getItem('user');
    let nombreFinal = "Darwin"; // Fallback por defecto
    let userRoleStr = "Administración / Staff";
    let esIndependiente = false;

    if (userStr) {
        try {
            const userObj = JSON.parse(userStr);
            nombreFinal = userObj.nombre || userObj.Nombre || nombreFinal;
            
            // 🚀 Evaluación robusta integrada con soporte para rol "Proveedor"
            esIndependiente = evaluarEsIndependiente(userObj, token);
            userRoleStr = esIndependiente ? "Proveedor Independiente" : "Administración / Staff";
            
            // 🚀 HU-01 & HU-06: Configurar visibilidad de la columna de asignación
            const thStaffSilla = document.getElementById('thStaffSilla');
            if (thStaffSilla) {
                thStaffSilla.style.display = esIndependiente ? 'none' : 'table-cell';
            }

            // 🛡️ HU-06 CA4: El filtro de puestos solo lo ve el Staff/Administración
            const containerFiltro = document.getElementById('containerFiltroPuesto');
            if (containerFiltro) {
                containerFiltro.style.display = esIndependiente ? 'none' : 'flex';
            }
        } catch (e) { 
            console.error("❌ Error parseando objeto de usuario / Error al cargar nombre", e); 
        }
    }

    // 3. Inyectar el saludo con el estilo de color (🙋‍♂️ SALUDO CON NOMBRE REAL) Y ROL
    const welcomeText = document.getElementById('welcomeText');
    if (welcomeText) {
        welcomeText.innerHTML = `¡Qué más, <span style="color: #48c1b5;">${nombreFinal}</span>!`;
    }

    const userRoleEl = document.getElementById('userRole');
    if (userRoleEl) {
        userRoleEl.innerText = userRoleStr;
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

    // Formato robusto YYYY-MM-DD sin alterations regionales del motor de JS
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
        if (thStaffSilla) {
            thStaffSilla.style.display = esIndependiente ? 'none' : 'table-cell';
        }

        // 🛡️ HU-06 CA4: Ocultar selector de puesto si es proveedor independiente
        const containerFiltro = document.getElementById('containerFiltroPuesto');
        if (containerFiltro) {
            containerFiltro.style.display = esIndependiente ? 'none' : 'flex';
        }

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
        renderizarTablaDashboard(citasRespuesta, token, API_BASE, esIndependiente);
        actualizarContadoresDashboard(data, esIndependiente); 

    } catch (error) { 
        if (error.name === 'AbortError') {
            console.log("📥 Petición duplicada de dashboard cancelada exitosamente.");
            return;
        }
        console.error("🔥 Error al filtrar agenda:", error);
        if (tablaBody) {
            tablaBody.innerHTML = `<tr><td colspan="7" style="text-align: center; color: #ff5e5e;">Error al conectar con el servicio.</td></tr>`;
        }
    }
}

/**
 * 📝 Renderiza las filas (🛡️ BLINDAJE DE ESTADOS EN ACCIONES Y BANDERAS DE CLIENTE NUEVO HU-07)
 */
function renderizarTablaDashboard(citas, token, API_BASE, esIndependiente = false) {
    const tabla = document.getElementById('turnosTable');
    if (!tabla) return;

    if (!citas || citas.length === 0) {
        tabla.innerHTML = '<tr><td colspan="7" style="text-align: center; padding: 20px; color: #ccc;">No hay citas agendadas para este periodo.</td></tr>';
        return;
    }

    // 🚀 HU-01 CA1: Filtro por Puesto sólo si es Staff
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
        
        // 🚩 LÓGICA DE ACCIONES SEGÚN EL ESTADO REAL
        let celdaAccion = "";
        if (estado === 'completada' || estado === 'confirmada' || estado === 'completado') {
            celdaAccion = `<span style="color: #48c1b5; font-size: 0.8rem;"><i class="fas fa-check-circle"></i> Validado</span>`;
        } else if (estado === 'cancelada' || estado === 'anulada') {
            celdaAccion = `<span style="color: #ff5e5e; font-size: 0.8rem;"><i class="fas fa-exclamation-triangle"></i> Anulada</span>`;
        } else {
            celdaAccion = `<span style="color: #888; font-size: 0.8rem;"><i class="fas fa-clock"></i> Pendiente</span>`;
        }
        
        // 💈 HU-07 CA1: Inyección visual del distintivo "Cliente Nuevo"
        const esNuevo = c.esNuevoCliente === true || c.EsNuevoCliente === true;
        const badgeCliente = esNuevo 
            ? `<span class="badge-nuevo-cliente" style="margin-left: 5px;"><i class="fas fa-star"></i> Nuevo</span>`
            : '';

        const nombreCliente = c.cliente || c.Cliente || 'Sin nombre';
        const empleadoNombre = c.empleadoAsignado || c.EmpleadoAsignado || 'Sin Asignar';
        const estacionNombre = c.estacionAsignada || c.estacion || c.Estacion || 'Sin Silla';

        // 🚀 HU-01 & HU-03: Etiqueta de esquema de contratación con Alerta de Cobro Silla
        const tipoContrato = (c.tipoContratoEmpleado || c.TipoContratoEmpleado || "").toLowerCase();
        const precioSilla = c.precioSilla || c.PrecioSilla;
        const estadoPagoSilla = c.estadoPagoSilla || c.EstadoPagoSilla || "Al día";

        let badgeEsquema = '';
        if (tipoContrato.includes("silla") || tipoContrato.includes("fijo") || tipoContrato.includes("arriendo")) {
            const detallePrecio = precioSilla ? `: ${new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', minimumFractionDigits: 0 }).format(precioSilla)}` : '';
            badgeEsquema = `<br><span class="badge-silla-fija"><i class="fas fa-chair"></i> Silla Fija${detallePrecio} (${estadoPagoSilla})</span>`;
        } else if (tipoContrato.includes("comision") || tipoContrato.includes("porcentaje")) {
            const pct = c.porcentajeComision || c.PorcentajeComision;
            const detallePct = pct ? ` (${pct}%)` : '';
            badgeEsquema = `<br><span class="badge-comision"><i class="fas fa-percentage"></i> Comisión${detallePct}</span>`;
        }

        // 🚀 HU-01: Celda condicional para asignación
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
 * 🔢 ACTUALIZACIÓN DE CONTADORES (HU-06: SOPORTE PARA INGRESOS BRUTOS Y NUEVOS CLIENTES)
 */
function actualizarContadoresDashboard(data, esIndependiente = false) {
    const totalCitasEl = document.getElementById('totalCitas') || document.getElementById('total-citas');
    const ingresosEl = document.getElementById('ingresosMes') || document.getElementById('total-ingresos');
    const clientesEl = document.getElementById('nuevosClientes') || document.getElementById('nuevos-clientes');
    const ingresoNotaEl = document.getElementById('ingresoNota');

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
            const conteoNuevos = listaCitas.filter(c => c.esNuevoCliente === true || c.EsNuevoCliente === true).length;
            clientesEl.innerText = conteoNuevos;
        }
    }

    // HU-06 CA1: Muestra los ingresos 100% brutos reales o proyectados
    if (ingresosEl) {
        let montoCalculado = 0;

        if (esIndependiente) {
            // HU-05 & HU-06: 100% de Ingreso Bruto sin deducción
            if (data.ingresosRealesBrutos !== undefined && data.ingresosRealesBrutos > 0) {
                montoCalculado = data.ingresosRealesBrutos;
            } else if (data.ingresosProyectadosBrutos !== undefined) {
                montoCalculado = data.ingresosProyectadosBrutos;
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
            if (ingresoNotaEl) ingresoNotaEl.innerText = "100% Ingreso Bruto (Sin deducciones)";
        } else {
            // HU-02 & HU-04: Descuenta comisiones del staff
            if (data.ingresoProyectadoNegocio !== undefined) {
                montoCalculado = data.ingresoProyectadoNegocio;
            } else if (data.gananciaReal !== undefined) {
                montoCalculado = data.gananciaReal;
            } else if (listaCitas.length > 0) {
                montoCalculado = listaCitas.reduce((acc, c) => {
                    const valor = parseFloat(c.precioPactado || c.PrecioPactado || c.precio || c.Precio || 0);
                    const comisionPct = parseFloat(c.porcentajeComision || c.PorcentajeComision || 0);
                    const tipoContrato = (c.tipoContratoEmpleado || c.TipoContratoEmpleado || "").toLowerCase();
                    const est = (c.estado || c.Estado || "").toLowerCase().trim();

                    if (est.includes("completad") || est.includes("confirmad") || est.includes("pendiente")) {
                        if (tipoContrato.includes("silla") || tipoContrato.includes("fijo")) {
                            return acc + valor; // En sillas fijas el negocio recibe el pago completo o no retiene comisión
                        } else {
                            const valorComision = valor * (comisionPct / 100);
                            return acc + (valor - valorComision); // Margen neto del negocio
                        }
                    }
                    return acc;
                }, 0);
            }
            if (ingresoNotaEl) ingresoNotaEl.innerText = "Neto tras comisiones de colaboradores";
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

// Expansión global para eventos inline HTML
window.cambiarPeriodo = cambiarPeriodo;
window.logout = logout;
window.evaluarEsIndependiente = evaluarEsIndependiente;