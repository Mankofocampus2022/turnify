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
        // 🚩 CORRECCIÓN CRÍTICA: Eliminamos el -2 para que busque HOY realmente
        inicio = new Date(); 
    }

    // 🛡️ Ajuste para que ISO no nos cambie la fecha por la diferencia horaria de Colombia (UTC-5)
    const startStr = inicio.toLocaleDateString('en-CA'); // Formato YYYY-MM-DD local
    const endStr = fin.toLocaleDateString('en-CA');
    const token = localStorage.getItem('turnify_token') || localStorage.getItem('token');

    const tablaBody = document.getElementById('turnosTable');
    if (tablaBody) {
        tablaBody.innerHTML = '<tr><td colspan="5" style="text-align: center;"><i class="fas fa-spinner fa-spin"></i> Cargando agenda...</td></tr>';
    }

    try {
        const user = JSON.parse(localStorage.getItem('user'));
        const provId = user?.proveedorId || user?.id;

        const response = await fetch(`http://localhost:5000/api/Dashboard/resumen/${provId}?periodo=${periodo}&fecha=${startStr}`, {
            headers: { 
                'Authorization': `Bearer ${token}`,
                'Content-Type': 'application/json'
            }
        });

        if (response.ok) {
            const data = await response.json();
            console.log("📅 [DEBUG] Data Recibida:", data);
            
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
        const estado = (c.estado || c.Estado || "pendiente").toLowerCase();
        const badgeClass = getEstadoClass(estado);
        
        return `
            <tr>
                <td style="color: #48c1b5; font-weight: bold;"><i class="far fa-clock"></i> ${c.hora || c.Hora}</td> 
                <td>${c.fecha ? c.fecha.split('T')[0] : 'Hoy'}</td>
                <td><strong>${c.cliente || c.Cliente || 'Sin nombre'}</strong></td>
                <td>${c.servicio || c.Servicio || 'Servicio'}</td>
                <td><span class="status-pill ${badgeClass}">${estado}</span></td>
            </tr>
        `;
    }).join('');
}

/**
 * 🔢 ACTUALIZACIÓN SENIOR: Ahora suma ingresos si el Backend no lo hace
 */
function actualizarContadoresDashboard(data) {
    const totalCitasEl = document.getElementById('totalCitas') || document.getElementById('total-citas');
    const ingresosEl = document.getElementById('ingresosMes') || document.getElementById('total-ingresos') || document.getElementById('ingresosProyectados') || document.getElementById('txtIngresosTotales');
    const clientesEl = document.getElementById('nuevosClientes') || document.getElementById('nuevos-clientes');

    const listaCitas = data.proximasCitas || data.citas || [];

    // 1. Citas Totales
    if (totalCitasEl) {
        // Usamos la longitud de la lista filtrada para que no cuente citas de otros
        totalCitasEl.innerText = listaCitas.length;
    }

    // 2. Clientes Nuevos (🚩 CORRECCIÓN PARA DARWIN)
    if (clientesEl) {
        // En lugar de usar data.nuevosClientes (que trae el global de 3),
        // contamos los clientes únicos en la lista de citas de este barbero.
        const clientesUnicos = new Set(listaCitas.map(c => c.clienteId || c.ClienteId || c.cliente || c.Cliente));
        clientesEl.innerText = listaCitas.length > 0 ? clientesUnicos.size : 0;
    }

    // 3. Ingresos Totales (Suma Manual Blindada)
    if (ingresosEl) {
        let montoCalculado = 0;

        if (listaCitas.length > 0) {
            console.log("📡 Sumando ingresos de", listaCitas.length, "citas...");
            montoCalculado = listaCitas.reduce((acc, c) => {
                const valor = c.precioPactado || c.PrecioPactado || c.precio || c.Precio || c.valor || c.monto || 0;
                const est = (c.estado || c.Estado || "").toLowerCase().trim();
                
                // SUMAMOS TODO: Completadas + Pendientes (Para la Proyección de $105.000)
                if (est.includes("completad") || est.includes("confirmad") || est.includes("pendiente") || est.includes("pago")) {
                    return acc + parseFloat(valor);
                }
                return acc;
            }, 0);
        }

        // Respaldo por si la lista falla
        if (montoCalculado === 0 && listaCitas.length === 0) {
            montoCalculado = data.gananciaReal || data.ingresosReales || 0;
        }

        console.log("💵 [Lupe Debug] Resultado Proyectado:", montoCalculado);

        // Formateo elegante: COP $ 105.000
        ingresosEl.innerText = new Intl.NumberFormat('es-CO', {
            style: 'currency',
            currency: 'COP',
            minimumFractionDigits: 0
        }).format(montoCalculado);
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
            actualizarContadoresDashboard(data);
        }
    } catch (error) { 
        console.error("🔥 Error cargando resumen global:", error); 
    }
}

function getEstadoClass(estado) {
    const est = estado.toLowerCase();
    if (est.includes('completado') || est.includes('confirmada')) return 'status-activo';
    if (est.includes('cancelada') || est.includes('suspendido')) return 'status-bloqueado';
    return 'status-pendiente'; 
}

function logout() {
    if (confirm("¿te vas a ir, mijito? Guarde todo antes de salir o si no , perdimos el tiempo.")) {
        localStorage.clear();
        window.location.href = 'login.html';
    }
}