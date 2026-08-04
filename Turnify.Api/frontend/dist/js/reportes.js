/* ============================================================
   TURNIFY - MOTOR ANALÍTICO Y METRICAS DE BUSINESS INTELLIGENCE
   ============================================================ */

/**
 * 🛠️ FUNCIÓN DE DETECCIÓN AVANZADA: Determina si el usuario actual es un Proveedor Independiente
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
    if (rol.includes("staff") || rol.includes("admin") || rol.includes("administrador")) {
        return false;
    }

    const tipo = String(userObj?.tipo || userObj?.tipoProveedor || userObj?.tipoUsuario || userObj?.tipoModelo || "").toLowerCase();
    if (rol.includes("independiente") || rol.includes("autonomo") || tipo.includes("independiente")) {
        return true;
    }

    if (token) {
        try {
            const base64Url = token.split('.')[1];
            if (base64Url) {
                const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
                const jsonPayload = decodeURIComponent(atob(base64).split('').map(c => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2)).join(''));
                const tokenData = JSON.parse(jsonPayload);

                const claimEsInd = tokenData.EsIndependiente || tokenData.esIndependiente || tokenData["EsIndependiente"];
                if (claimEsInd === "false" || claimEsInd === false) return false;
                if (claimEsInd === "true" || claimEsInd === true) return true;

                const claimRol = String(tokenData.role || tokenData["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] || "").toLowerCase();
                if (claimRol.includes("staff") || claimRol.includes("admin")) return false;
                if (claimRol.includes("independiente")) return true;
            }
        } catch (e) {
            console.warn("⚠️ No se pudo decodificar las claims del Token en reportes:", e);
        }
    }

    return false;
}

document.addEventListener('DOMContentLoaded', () => {
    // 1. Configuración Validada (URL Dinámica Blindada - Matriz de Red Inteligente para Docker)
    let API_BASE_URL = window.location.origin + '/api'; 
    if (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1') {
        API_BASE_URL = 'http://localhost:5000/api';
    } else if (/^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$/.test(window.location.hostname)) {
        API_BASE_URL = `${window.location.protocol}//${window.location.hostname}:5000/api`;
    }
    
    const userStr = localStorage.getItem('user');
    const user = userStr ? JSON.parse(userStr) : null;
    
    const proveedorId = user ? (user.proveedorId || user.id) : 'F34FE619-8F7D-4EEE-8473-22979451EBC0'; 
    const token = localStorage.getItem('token') || localStorage.getItem('turnify_token');

    const esIndependienteGlobal = evaluarEsIndependiente(user, token);

    // Elementos UI Principales
    const txtTotalCitas = document.getElementById('total-citas');
    const txtTotalIngresos = document.getElementById('total-ingresos');
    const txtNuevosClientes = document.getElementById('nuevos-clientes');
    const tablaCuerpo = document.getElementById('lista-reportes');
    const txtAdminName = document.getElementById('admin-name'); 
    
    // 🚀 HU-20 & HU-21: Elementos de Liquidación Financiera
    const txtComisionesPagadas = document.getElementById('total-comisiones-pagadas') || document.getElementById('comisionesTotalesPagadas');
    const txtIngresoNeto = document.getElementById('total-ingreso-neto') || document.getElementById('ingresoNetoTotal');
    const badgeModeloReportes = document.getElementById('badgeModeloReportes');

    // 📈 Elementos de Métricas BI (Porcentajes y Tasas)
    const elTendenciaCitas = document.getElementById('trend-citas');
    const elCrecimientoIngresos = document.getElementById('trend-ingresos');
    
    // 📊 Referencias para Gráficas
    let chartServicios = null;
    let chartClientes = null;

    const formatter = new Intl.NumberFormat('es-CO', {
        style: 'currency', currency: 'COP', minimumFractionDigits: 0
    });

    const nombreUsuarioSesion = localStorage.getItem('adminName') || (user ? user.nombre : 'darwin');

    if (txtAdminName) {
        txtAdminName.innerText = nombreUsuarioSesion;
    }

    popularAnios();

    function popularAnios() {
        const selectAnio = document.getElementById('filtro-anio');
        if (!selectAnio) return;
        
        const anioActual = new Date().getFullYear();
        for (let i = 2024; i <= 2100; i++) {
            const opt = document.createElement('option');
            opt.value = i;
            opt.innerText = i;
            if (i === anioActual) opt.selected = true; 
            selectAnio.appendChild(opt);
        }
    }

    /**
     * 📡 FUNCIÓN MAESTRA DE CARGA DE REPORTES CON LIQUIDACIÓN ESTRECHA (SOLO CITAS COMPLETADAS)
     */
    async function cargarReportes() {
        try {
            const selectPeriodo = document.getElementById('filtro-periodo');
            const periodo = selectPeriodo?.value || 'mes';
            const mesSeleccionado = document.getElementById('filtro-mes')?.value;
            const anioSeleccionado = document.getElementById('filtro-anio')?.value;

            let urlResumen = `${API_BASE_URL}/Dashboard/resumen/${proveedorId}?periodo=${periodo}`;
            let urlMovimientos = `${API_BASE_URL}/Dashboard/movimientos?periodo=${periodo}`;
            
            if (periodo === 'especifico' || periodo === 'mes') {
                if (mesSeleccionado && anioSeleccionado) {
                    urlResumen = `${API_BASE_URL}/Dashboard/resumen/${proveedorId}?periodo=mes&mes=${mesSeleccionado}&anio=${anioSeleccionado}`;
                    urlMovimientos = `${API_BASE_URL}/Dashboard/movimientos?periodo=mes&mes=${mesSeleccionado}&anio=${anioSeleccionado}`;
                }
            }
            
            const [respResumen, respMovimientos] = await Promise.all([
                fetch(urlResumen, {
                    method: 'GET',
                    headers: {
                        'Authorization': `Bearer ${token}`,  
                        'Content-Type': 'application/json',
                        'X-TimeZone': Intl.DateTimeFormat().resolvedOptions().timeZone || 'America/Bogota'
                    }
                }),
                fetch(urlMovimientos, {
                    method: 'GET',
                    headers: {
                        'Authorization': `Bearer ${token}`,  
                        'Content-Type': 'application/json',
                        'X-TimeZone': Intl.DateTimeFormat().resolvedOptions().timeZone || 'America/Bogota'
                    }
                })
            ]);

            if (!respResumen.ok) throw new Error(`Error API Resumen: ${respResumen.status}`);

            const data = await respResumen.json();
            let dataStrategy = null;

            if (respMovimientos.ok) {
                dataStrategy = await respMovimientos.json();
            }

            const esIndependiente = esIndependienteGlobal || (dataStrategy && dataStrategy.tipoModelo === "Independiente");

            if (txtComisionesPagadas) {
                const cardComisiones = txtComisionesPagadas.closest('.stat-card') || txtComisionesPagadas.closest('.card');
                if (cardComisiones) {
                    cardComisiones.style.display = esIndependiente ? 'none' : 'flex';
                }
            }

            // A. Actualización de KPIs
            if (txtTotalCitas) txtTotalCitas.innerText = data.totalCitas || 0;
            
            if (elTendenciaCitas) {
                const valor = data.tendenciaCitas || 0;
                const color = valor >= 0 ? '#48c1b5' : '#ff5e5e';
                const icono = valor >= 0 ? '↑' : '↓';
                elTendenciaCitas.innerHTML = `<small style="color: ${color}; font-weight: bold;">${icono} ${Math.abs(valor)}% vs ant.</small>`;
            }

            // 🚀 CRUCE Y RESOLUCIÓN DE DATOS CON FILTRADO POR ESTADO COMPLETADO
            const citasResumen = data.citas || data.proximasCitas || data.detalles || [];
            let movimientosList = (dataStrategy && dataStrategy.movimientos && dataStrategy.movimientos.length > 0) 
                ? dataStrategy.movimientos 
                : (dataStrategy && Array.isArray(dataStrategy) && dataStrategy.length > 0)
                ? dataStrategy
                : citasResumen;
            
            let totalBrutoAcumulado = 0;
            let totalComisionesAcumuladas = 0;

            movimientosList.forEach(m => {
                const citaIdTarget = m.citaId || m.id;
                const matchCita = citasResumen.find(c => (c.id || c.citaId) === citaIdTarget);

                // 1. RESOLUCIÓN DEL ESPECIALISTA QUE ATIENDE
                let esp = m.especialistaNombre || m.empleadoAsignado || m.EmpleadoAsignado || m.especialista || m.Especialista;
                
                if ((!esp || esp === 'No Asignado' || esp === 'Sin Asignar' || esp === 'Sin asignar' || esp === 'Especialista Asignado' || esp === 'Sin Proveedor') && matchCita) {
                    esp = matchCita.empleadoAsignado || matchCita.EmpleadoAsignado || matchCita.especialistaNombre || matchCita.especialista;
                }

                if (!esp || esp === 'No Asignado' || esp === 'Sin Asignar' || esp === 'Sin asignar' || esp === 'Especialista Asignado' || esp === 'Sin Proveedor') {
                    esp = nombreUsuarioSesion || 'darwin';
                }

                m.especialistaNombre = esp;
                m.empleadoAsignado = esp;

                // 2. DETECCIÓN ESTRECHA DE ESTADO
                const estadoRaw = (m.estado || matchCita?.estado || 'pendiente').toLowerCase().trim();
                const esCompletada = estadoRaw.includes('completad') || estadoRaw.includes('confirmad') || estadoRaw.includes('finalizad') || estadoRaw.includes('pagad');

                // 3. CÁLCULO DE MONTO BRUTO Y COMISIÓN
                const mBruto = parseFloat(m.montoTotal ?? m.precioPactado ?? m.montoBruto ?? matchCita?.precioPactado ?? matchCita?.precio ?? 0);
                
                let rawPct = (m.porcentajeComision && m.porcentajeComision > 0) ? m.porcentajeComision : matchCita?.porcentajeComision;
                let pctComision = parseFloat(rawPct);
                if (isNaN(pctComision) || pctComision <= 0) {
                    pctComision = 20; // Fallback al 20%
                }

                let mComision = parseFloat(m.montoComisionEspecialista || m.comision || matchCita?.montoComisionEspecialista);
                
                if (!esIndependiente && (isNaN(mComision) || mComision <= 0) && mBruto > 0) {
                    mComision = (mBruto * pctComision) / 100;
                }

                m.porcentajeComision = pctComision;
                m.montoTotal = mBruto;
                m.montoComisionEspecialista = esIndependiente ? 0 : mComision;
                m.ingresoNeto = mBruto - m.montoComisionEspecialista;

                if (!m.estado && matchCita?.estado) m.estado = matchCita.estado;
                if (!m.codigoVerificacion && matchCita?.codigoVerificacion) m.codigoVerificacion = matchCita.codigoVerificacion;

                // 🎯 ACUMULACIÓN ÚNICAMENTE PARA CITAS COMPLETADAS
                if (esCompletada) {
                    totalBrutoAcumulado += mBruto;
                    totalComisionesAcumuladas += m.montoComisionEspecialista;
                }
            });

            const totalNetoCalculado = totalBrutoAcumulado - totalComisionesAcumuladas;

            if (txtTotalIngresos) txtTotalIngresos.innerText = formatter.format(totalBrutoAcumulado);
            if (txtComisionesPagadas && !esIndependiente) txtComisionesPagadas.innerText = formatter.format(totalComisionesAcumuladas);
            if (txtIngresoNeto) txtIngresoNeto.innerText = formatter.format(totalNetoCalculado);

            if (badgeModeloReportes) {
                badgeModeloReportes.innerText = `Modelo: ${esIndependiente ? 'Independiente' : 'Estándar'}`;
                badgeModeloReportes.style.borderColor = esIndependiente ? "#48c1b5" : "#0284c7";
                badgeModeloReportes.style.color = esIndependiente ? "#48c1b5" : "#38bdf8";
            }

            if (elCrecimientoIngresos) {
                const valorInc = data.crecimientoIngresos || 0;
                const colorInc = valorInc >= 0 ? '#48c1b5' : '#ff5e5e';
                elCrecimientoIngresos.innerHTML = `<small style="color: ${colorInc}; font-weight: bold;">${valorInc >= 0 ? '+' : ''}${valorInc}% hoy</small>`;
            }

            if (txtNuevosClientes) txtNuevosClientes.innerText = data.nuevosClientesTotales || 0;

            // B. Renderizar Tabla de Movimientos y Liquidación
            renderizarTablaStrategy(movimientosList, esIndependiente, nombreUsuarioSesion);

            // C. Generar Gráficas
            inicializarGraficaServicios(data.chartServiciosPopulares || []);
            inicializarGraficaCrecimiento(data.chartCrecimientoClientes || []);

            window.datosActuales = movimientosList;

        } catch (error) {
            console.error('❌ Error:', error);
            if (tablaCuerpo) {
                const spanCol = esIndependienteGlobal ? 7 : 9;
                tablaCuerpo.innerHTML = `<tr><td colspan="${spanCol}" style="text-align:center; color: #ff5e5e;">Error al conectar con el servidor</td></tr>`;
            }
        }
    }

    function inicializarGraficaServicios(servicios) {
        const canvas = document.getElementById('chartServicios');
        if (!canvas) return;
        const ctx = canvas.getContext('2d');
        if (chartServicios) chartServicios.destroy();
        
        chartServicios = new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: servicios.map(s => s.nombre),
                datasets: [{
                    data: servicios.map(s => s.cantidad),
                    backgroundColor: ['#48c1b5', '#2c3e50', '#1d6f42', '#34495e', '#16a085'],
                    borderWidth: 0
                }]
            },
            options: {
                responsive: true,
                plugins: {
                    legend: { position: 'bottom', labels: { color: '#48c1b5', font: { weight: 'bold' } } }
                }
            }
        });
    }

    function inicializarGraficaCrecimiento(puntos) {
        const canvas = document.getElementById('chartClientes');
        if (!canvas) return;
        const ctx = canvas.getContext('2d');
        if (chartClientes) chartClientes.destroy();

        chartClientes = new Chart(ctx, {
            type: 'line',
            data: {
                labels: puntos.map(p => p.fecha),
                datasets: [{
                    label: 'Crecimiento de Clientes',
                    data: puntos.map(p => p.cantidad),
                    borderColor: '#48c1b5',
                    backgroundColor: 'rgba(72, 193, 181, 0.1)',
                    fill: true,
                    tension: 0.4
                }]
            },
            options: {
                responsive: true,
                scales: {
                    y: { beginAtZero: true, grid: { color: 'rgba(72, 193, 181, 0.1)' }, ticks: { color: '#ccc' } },
                    x: { grid: { display: false }, ticks: { color: '#ccc' } }
                }
            }
        });
    }

    /**
     * 📝 RENDERIZADO DE TABLA CON CICLO DE VIDA DE ESTADOS Y ESPECIALISTA DEFINIDO
     */
    function renderizarTablaStrategy(lista, esIndependiente = false, fallbackNombreEspecialista = 'darwin') {
        if (!tablaCuerpo) return;

        const thEspecialista = document.getElementById('thRepEspecialista');
        const thDeduccion = document.getElementById('thRepDeduccion');
        if (thEspecialista) thEspecialista.style.display = esIndependiente ? 'none' : 'table-cell';
        if (thDeduccion) thDeduccion.style.display = esIndependiente ? 'none' : 'table-cell';

        tablaCuerpo.innerHTML = '';
        const totalColumns = esIndependiente ? 7 : 9;

        if (!lista || lista.length === 0) {
            tablaCuerpo.innerHTML = `<tr><td colspan="${totalColumns}" style="text-align:center; padding: 20px; color: #888;">No se encontraron registros para este filtro.</td></tr>`;
            return;
        }

        lista.forEach(item => {
            const fechaVal = item.fecha ? String(item.fecha).split('T')[0] : '---';
            const cliente = item.clienteNombre || item.cliente || 'Anónimo';
            const servicio = item.servicioNombre || item.servicio || 'N/A';
            
            let especialista = item.especialistaNombre || item.empleadoAsignado || item.EmpleadoAsignado || item.especialista || item.Especialista;
            if (!especialista || especialista === 'No Asignado' || especialista === 'Sin Asignar' || especialista === 'Sin asignar' || especialista === 'Especialista Asignado' || especialista === 'Sin Proveedor') {
                especialista = fallbackNombreEspecialista;
            }

            const montoBruto = item.montoTotal ?? item.precioPactado ?? 0;
            const deduccionComision = item.montoComisionEspecialista ?? 0;
            const ingresoNeto = item.ingresoNeto ?? (montoBruto - deduccionComision);

            const estadoRaw = (item.estado || 'pendiente').toLowerCase().trim();
            const tr = document.createElement('tr');
            
            let badgeClass = 'status-pendiente';
            let textoEstado = 'Pendiente';

            if (estadoRaw.includes('proceso') || estadoRaw.includes('ejecucion')) {
                badgeClass = 'status-proceso';
                textoEstado = 'En Proceso';
            } else if (estadoRaw.includes('completad') || estadoRaw.includes('confirmad') || estadoRaw.includes('finalizad')) {
                badgeClass = 'status-activo';
                textoEstado = 'Finalizada / Pagada';
            } else if (estadoRaw.includes('cancelad') || estadoRaw.includes('anulad')) {
                badgeClass = 'status-bloqueado';
                textoEstado = 'Anulada / No asistió';
            }

            let btnAccion = "";
            const citaIdReal = item.citaId || item.id;
            const tokenVerif = item.codigoVerificacion || '';

            if (estadoRaw === "pendiente") {
                btnAccion = `
                    <div style="display: flex; flex-direction: column; gap: 4px;">
                        <button class="btn-checkin-report" onclick="lanzarCheckIn('${citaIdReal}', '${tokenVerif}')" style="background: #48c1b5; color: #000; border: none; padding: 4px 8px; border-radius: 4px; cursor: pointer; font-size: 0.7rem; font-weight: bold;">
                            <i class="fas fa-play"></i> INICIAR CORTE
                        </button>
                        <button onclick="finalizarCita('${citaIdReal}', 'cancelada')" style="background: transparent; color: #ff5e5e; border: 1px solid #ff5e5e; padding: 3px 6px; border-radius: 4px; cursor: pointer; font-size: 0.65rem;">
                            <i class="fas fa-times"></i> ANULAR
                        </button>
                    </div>
                `;
            } else if (estadoRaw.includes("proceso")) {
                btnAccion = `
                    <button onclick="finalizarCita('${citaIdReal}', 'completada')" style="background: #38bdf8; color: #000; border: none; padding: 5px 10px; border-radius: 4px; cursor: pointer; font-size: 0.7rem; font-weight: bold;">
                        <i class="fas fa-check-circle"></i> FINALIZAR Y COBRAR
                    </button>
                `;
            } else {
                btnAccion = `
                    <div style="display: flex; flex-direction: column; gap: 4px; align-items: center;">
                        <span style="font-size: 0.7rem; color: #48c1b5;"><i class="fas fa-lock"></i> Procesada</span>
                        <button onclick="finalizarCita('${citaIdReal}', 'pendiente')" style="background: rgba(72, 193, 181, 0.1); color: #48c1b5; border: 1px solid #48c1b5; padding: 3px 6px; border-radius: 4px; cursor: pointer; font-size: 0.65rem; width: 100%;">
                            <i class="fas fa-undo"></i> CORREGIR
                        </button>
                    </div>
                `;
            }

            const celdaEspecialista = esIndependiente ? '' : `<td style="color: #cbd5e1;"><i class="fas fa-user-tie"></i> <strong>${especialista}</strong></td>`;
            const celdaDeduccion = esIndependiente ? '' : `<td style="color: #ff5e5e; font-weight: bold;">-${formatter.format(deduccionComision)}</td>`;

            tr.innerHTML = `
                <td>
                    <div style="font-weight: bold; color: #48c1b5;">${fechaVal}</div>
                </td>
                <td><strong>${cliente}</strong></td>
                <td>${servicio}</td>
                ${celdaEspecialista}
                <td style="color: #38bdf8; font-weight: bold;">${formatter.format(montoBruto)}</td>
                ${celdaDeduccion}
                <td style="color: #48c1b5; font-weight: bold;">${formatter.format(ingresoNeto)}</td>
                <td><span class="status-pill ${badgeClass}">${textoEstado}</span></td>
                <td style="text-align: center;">${btnAccion}</td>
            `;
            tablaCuerpo.appendChild(tr);
        });
    }

    /**
     * 🛡️ CHECK-IN / INICIO DE ATENCIÓN DE CLIENTE
     */
    async function lanzarCheckIn(citaId, tokenSugerido) {
        const userInput = prompt(`⚠️ VALIDACIÓN DE TURNO\nIngrese el código de 6 dígitos del cliente para INICIAR el servicio:\n(Sugerido: ${tokenSugerido})`, tokenSugerido);
        
        if (userInput === null) return;

        try {
            const tokenSesion = localStorage.getItem('token') || localStorage.getItem('turnify_token');
            let exitoCheckIn = false;

            if (userInput.trim() !== "") {
                const response = await fetch(`${API_BASE_URL}/Citas/validar-checkin`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${tokenSesion}` },
                    body: JSON.stringify({ citaId: citaId, token: userInput.trim() })
                });

                if (response.ok) {
                    exitoCheckIn = true;
                } else {
                    const errData = await response.json().catch(() => ({}));
                    alert(`❌ Código incorrecto: ${errData.message || 'El token ingresado no es válido.'}`);
                    return;
                }
            } else {
                exitoCheckIn = true;
            }

            if (exitoCheckIn) {
                const okState = await finalizarCitaSilencioso(citaId, 'en_proceso');
                if (okState) {
                    alert("✅ Check-in exitoso. Cita iniciada (En Proceso).");
                    cargarReportes(); 
                }
            }
        } catch (e) { 
            console.error("Error en lanzarCheckIn:", e);
            alert("🔌 Error de conexión al validar el Check-In."); 
        }
    }

    /**
     * ⚡ CAMBIO DE ESTADO HTTP PATCH
     */
    async function finalizarCita(id, nuevoEstado) {
        let msg = `¿Marcar esta cita como ${nuevoEstado.toUpperCase()}?`;
        if (nuevoEstado === 'en_proceso') msg = "🚀 ¿Iniciar la atención de este cliente?";
        if (nuevoEstado === 'completada' || nuevoEstado === 'finalizada') msg = "💰 ¿Finalizar servicio y procesar el cobro?";
        if (nuevoEstado === 'pendiente') msg = "⚠️ ¿Deseas DESHACER esta cita a estado pendiente?";
        
        if (!confirm(msg)) return;
        const exito = await finalizarCitaSilencioso(id, nuevoEstado);
        if (exito) cargarReportes();
    }

    async function finalizarCitaSilencioso(id, nuevoEstado) {
        try {
            const tokenSesion = localStorage.getItem('token') || localStorage.getItem('turnify_token');
            const response = await fetch(`${API_BASE_URL}/Citas/${id}/estado`, {
                method: 'PATCH', 
                headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${tokenSesion}` },
                body: JSON.stringify({ nuevoEstado: nuevoEstado }) 
            });

            if (response.ok) {
                return true;
            } else {
                const errData = await response.json().catch(() => ({}));
                console.error("❌ Falla al cambiar estado:", errData.message);
                alert(`⚠️ Error al actualizar estado: ${errData.message || 'No se pudo actualizar el estado de la cita.'}`);
                return false;
            }
        } catch (e) { 
            console.error("Error cambiando estado:", e); 
            alert("🔌 Error de conexión al comunicarse con el servidor.");
            return false;
        }
    }

    window.lanzarCheckIn = lanzarCheckIn;
    window.finalizarCita = finalizarCita;

    // --- 📤 EXPORTACIÓN A EXCEL Y PDF ---
    document.getElementById('btn-excel')?.addEventListener('click', () => {
        if (!window.datosActuales) return;
        const worksheet = XLSX.utils.json_to_sheet(window.datosActuales);
        const workbook = XLSX.utils.book_new();
        XLSX.utils.book_append_sheet(workbook, worksheet, "Turnify_Report");
        XLSX.writeFile(workbook, "Reporte_Turnify.xlsx");
    });

    document.getElementById('btn-pdf')?.addEventListener('click', () => {
        const elemento = document.getElementById('contenido-reporte');
        if (!elemento) return;
        elemento.classList.add('pdf-export-mode');
        html2pdf().set({
            margin: 10,
            filename: 'Reporte_Turnify.pdf',
            html2canvas: { scale: 2, backgroundColor: '#0a101e' },
            jsPDF: { unit: 'mm', format: 'a4', orientation: 'portrait' }
        }).from(elemento).save().then(() => elemento.classList.remove('pdf-export-mode'));
    });

    // 🔄 Event Listeners de Filtros
    document.getElementById('filtro-periodo')?.addEventListener('change', cargarReportes);
    document.getElementById('filtro-mes')?.addEventListener('change', cargarReportes);
    document.getElementById('filtro-anio')?.addEventListener('change', cargarReportes);

    cargarReportes();
});

function logout() {
    localStorage.clear();
    window.location.href = 'login.html';
}