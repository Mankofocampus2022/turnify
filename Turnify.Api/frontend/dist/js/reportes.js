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
        // Mapeo seguro si accedes desde tu celular o tablet a la IP de la laptop en la misma red Wi-Fi
        API_BASE_URL = `${window.location.protocol}//${window.location.hostname}:5000/api`;
    }
    
    const userStr = localStorage.getItem('user');
    const user = userStr ? JSON.parse(userStr) : null;
    
    // Rescate de ID Senior para que no use el genérico si el barbero ya entró
    const proveedorId = user ? (user.proveedorId || user.id) : 'F34FE619-8F7D-4EEE-8473-22979451EBC0'; 
    const token = localStorage.getItem('token') || localStorage.getItem('turnify_token');

    const esIndependienteGlobal = evaluarEsIndependiente(user, token);

    // Elementos UI Principales
    const txtTotalCitas = document.getElementById('total-citas');
    const txtTotalIngresos = document.getElementById('total-ingresos');
    const txtNuevosClientes = document.getElementById('nuevos-clientes');
    const tablaCuerpo = document.getElementById('lista-reportes');
    const txtAdminName = document.getElementById('admin-name'); 
    
    // 🚀 HU-20 & HU-21: Elementos de Liquidación Financiera por Strategy
    const txtComisionesPagadas = document.getElementById('total-comisiones-pagadas');
    const txtIngresoNeto = document.getElementById('total-ingreso-neto');
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

    if (txtAdminName) {
        txtAdminName.innerText = localStorage.getItem('adminName') || (user ? user.nombre : 'Administrador');
    }

    // 🚩 Llenar años automáticamente hasta el 2100
    popularAnios();

    /**
     * 📅 Generador de años dinámico (2024 - 2100)
     */
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
     * 📡 FUNCIÓN MAESTRA DE CARGA (CORREGIDA Y EXTENDIDA CON PATRÓN STRATEGY HU-20 & HU-21)
     */
    async function cargarReportes() {
        try {
            const selectPeriodo = document.getElementById('filtro-periodo');
            const periodo = selectPeriodo?.value || 'mes';
            const mesSeleccionado = document.getElementById('filtro-mes')?.value;
            const anioSeleccionado = document.getElementById('filtro-anio')?.value;

            // 🛡️ BLINDAJE DE FILTROS:
            let urlResumen = `${API_BASE_URL}/Dashboard/resumen/${proveedorId}?periodo=${periodo}`;
            let urlMovimientos = `${API_BASE_URL}/Dashboard/movimientos?periodo=${periodo}`;
            
            if (periodo === 'especifico' || periodo === 'mes') {
                if (mesSeleccionado && anioSeleccionado) {
                    urlResumen = `${API_BASE_URL}/Dashboard/resumen/${proveedorId}?periodo=mes&mes=${mesSeleccionado}&anio=${anioSeleccionado}`;
                    urlMovimientos = `${API_BASE_URL}/Dashboard/movimientos?periodo=mes&mes=${mesSeleccionado}&anio=${anioSeleccionado}`;
                }
            }
            
            console.log("🚀 Modo de Consulta:", periodo, "URL Resumen:", urlResumen);
            
            // 1. Petición paralela para obtener métricas BI globales y detalle Strategy
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

            console.log("📊 Analítica Senior Recibida:", data);
            console.log("🚀 Movimientos Strategy Recibidos:", dataStrategy);

            // Determinar si es independiente a partir del usuario o la respuesta strategy
            const esIndependiente = esIndependienteGlobal || (dataStrategy && dataStrategy.tipoModelo === "Independiente");

            // 🛡️ OCULTAR LA TARJETA DE COMISIONES SI ES INDEPENDIENTE
            if (txtComisionesPagadas) {
                const cardComisiones = txtComisionesPagadas.closest('.stat-card');
                if (cardComisiones) {
                    cardComisiones.style.display = esIndependiente ? 'none' : 'flex';
                }
            }

            // A. Actualizar Stats Cards con Lógica BI
            if (txtTotalCitas) txtTotalCitas.innerText = data.totalCitas || 0;
            
            // 📈 Inyectar tendencia de citas
            if (elTendenciaCitas) {
                const valor = data.tendenciaCitas || 0;
                const color = valor >= 0 ? '#48c1b5' : '#ff5e5e';
                const icono = valor >= 0 ? '↑' : '↓';
                elTendenciaCitas.innerHTML = `<small style="color: ${color}; font-weight: bold;">${icono} ${Math.abs(valor)}% vs ant.</small>`;
            }

            // Actualizar montos financieros
            const ingresosBrutos = dataStrategy?.montoTotalAcumulado ?? (data.gananciaReal || 0);
            if (txtTotalIngresos) txtTotalIngresos.innerText = formatter.format(ingresosBrutos);

            if (dataStrategy) {
                if (txtComisionesPagadas && !esIndependiente) {
                    txtComisionesPagadas.innerText = formatter.format(dataStrategy.comisionesTotalesPagadas || 0);
                }
                if (txtIngresoNeto) txtIngresoNeto.innerText = formatter.format(dataStrategy.ingresoNetoTotal || 0);
                
                if (badgeModeloReportes) {
                    badgeModeloReportes.innerText = `Modelo: ${esIndependiente ? 'Independiente' : (dataStrategy.tipoModelo || 'Estándar')}`;
                    badgeModeloReportes.style.borderColor = esIndependiente ? "#48c1b5" : "#0284c7";
                    badgeModeloReportes.style.color = esIndependiente ? "#48c1b5" : "#38bdf8";
                }
            }

            // 💰 Inyectar crecimiento de ingresos
            if (elCrecimientoIngresos) {
                const valorInc = data.crecimientoIngresos || 0;
                const colorInc = valorInc >= 0 ? '#48c1b5' : '#ff5e5e';
                elCrecimientoIngresos.innerHTML = `<small style="color: ${colorInc}; font-weight: bold;">${valorInc >= 0 ? '+' : ''}${valorInc}% hoy</small>`;
            }

            // 👥 Clientes Nuevos vs En Riesgo
            if (txtNuevosClientes) txtNuevosClientes.innerText = data.nuevosClientesTotales || 0;

            // B. Pintar Tabla de Liquidación
            const movimientosList = dataStrategy?.movimientos || data.proximasCitas || data.detalles || [];
            renderizarTablaStrategy(movimientosList, esIndependiente);

            // C. 🎨 Generar Gráficas
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

    /**
     * 🍩 Gráfica de Dona: Servicios más pedidos
     */
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

    /**
     * 📈 Gráfica de Líneas: Crecimiento Clientes
     */
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
     * 📝 Renderizado de Tabla con Auditoría Financiera Strategy (HU-20 & HU-21)
     */
    function renderizarTablaStrategy(lista, esIndependiente = false) {
        if (!tablaCuerpo) return;

        // Ocultar cabeceras de Especialista y Deducción en la tabla HTML si es Independiente
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
            const especialista = item.especialistaNombre || item.empleadoAsignado || 'No Asignado';

            const montoBruto = item.montoTotal ?? item.precioPactado ?? 0;
            const deduccionComision = item.montoComisionEspecialista ?? 0;
            const ingresoNeto = item.ingresoNeto ?? (montoBruto - deduccionComision);

            const estadoRaw = (item.estado || 'pendiente').toLowerCase();
            const tr = document.createElement('tr');
            
            let badgeClass = 'status-pendiente';
            let textoEstado = estadoRaw;

            if (estadoRaw.includes('completada') || estadoRaw.includes('confirmada')) {
                badgeClass = 'status-activo';
                textoEstado = 'Finalizada';
            } else if (estadoRaw.includes('cancelada')) {
                badgeClass = 'status-bloqueado';
                textoEstado = 'Anulada / No asistió';
            }

            let btnAccion = "";
            const citaIdReal = item.citaId || item.id;
            const tokenVerif = item.codigoVerificacion || '';

            if (estadoRaw === "pendiente") {
                btnAccion = `
                    <button class="btn-checkin-report" onclick="lanzarCheckIn('${citaIdReal}', '${tokenVerif}')" style="background: #48c1b5; color: #000; border: none; padding: 4px 8px; border-radius: 4px; cursor: pointer; font-size: 0.7rem; font-weight: bold;">
                        <i class="fas fa-user-check"></i> CHECK-IN
                    </button>
                    <button onclick="finalizarCita('${citaIdReal}', 'cancelada')" style="background: transparent; color: #ff5e5e; border: 1px solid #ff5e5e; padding: 4px 8px; border-radius: 4px; cursor: pointer; font-size: 0.7rem; margin-top: 4px; width: 100%;">
                        <i class="fas fa-times"></i> ANULAR
                    </button>
                `;
            } else {
                btnAccion = `
                    <div style="display: flex; flex-direction: column; gap: 4px; align-items: center;">
                        <span style="font-size: 0.7rem; color: #888;"><i class="fas fa-lock"></i> Procesada</span>
                        <button onclick="finalizarCita('${citaIdReal}', 'pendiente')" style="background: rgba(72, 193, 181, 0.1); color: #48c1b5; border: 1px solid #48c1b5; padding: 3px 6px; border-radius: 4px; cursor: pointer; font-size: 0.65rem; width: 100%;">
                            <i class="fas fa-undo"></i> CORREGIR
                        </button>
                    </div>
                `;
            }

            const celdaEspecialista = esIndependiente ? '' : `<td style="color: #cbd5e1;"><i class="fas fa-user-tie"></i> ${especialista}</td>`;
            const celdaDeduccion = esIndependiente ? '' : `<td style="color: #ff5e5e;">-${formatter.format(deduccionComision)}</td>`;

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
     * 🛡️ MOTOR DE VALIDACIÓN (CHECK-IN)
     */
    async function lanzarCheckIn(citaId, tokenSugerido) {
        const userInput = prompt(`⚠️ VALIDACIÓN\nIngrese el código de 6 dígitos del cliente:\n(Sugerido: ${tokenSugerido})`);
        if (!userInput) return;

        try {
            const response = await fetch(`${API_BASE_URL}/Citas/validar-checkin`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${token}` },
                body: JSON.stringify({ citaId: citaId, token: userInput })
            });

            if (response.ok) {
                alert("✅ Check-in exitoso.");
                cargarReportes(); 
            } else {
                alert("❌ Código incorrecto.");
            }
        } catch (e) { alert("🔌 Error de conexión."); }
    }

    /**
     * ⚡ CAMBIO DE ESTADO
     */
    async function finalizarCita(id, nuevoEstado) {
        let msg = `¿Marcar como ${nuevoEstado.toUpperCase()}?`;
        if (nuevoEstado === 'pendiente') msg = "⚠️ ¿Deseas DESHACER esta cita?";
        if (!confirm(msg)) return;

        try {
            const response = await fetch(`${API_BASE_URL}/Citas/${id}/estado`, {
                method: 'PATCH', 
                headers: { 'Content-Type': 'application/json', 'Authorization': `Bearer ${token}` },
                body: JSON.stringify({ nuevoEstado: nuevoEstado }) 
            });

            if (response.ok) {
                cargarReportes(); 
            }
        } catch (e) { console.error(e); }
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