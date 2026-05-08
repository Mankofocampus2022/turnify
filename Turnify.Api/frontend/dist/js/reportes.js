document.addEventListener('DOMContentLoaded', () => {
    // 1. Configuración Validada (URL Dinámica Blindada)
    const API_BASE_URL = window.location.origin + '/api'; 
    
    const userStr = localStorage.getItem('user');
    const user = userStr ? JSON.parse(userStr) : null;
    
    // Rescate de ID Senior para que no use el genérico si el barbero ya entró
    const proveedorId = user ? (user.proveedorId || user.id) : 'F34FE619-8F7D-4EEE-8473-22979451EBC0'; 
    const token = localStorage.getItem('token') || localStorage.getItem('turnify_token');

    // Elementos UI Principales
    const txtTotalCitas = document.getElementById('total-citas');
    const txtTotalIngresos = document.getElementById('total-ingresos');
    const txtNuevosClientes = document.getElementById('nuevos-clientes');
    const tablaCuerpo = document.getElementById('lista-reportes');
    const txtAdminName = document.getElementById('admin-name'); 
    
    // 📈 [NUEVO] Elementos de Métricas BI (Porcentajes y Tasas)
    // Nota: Asegúrate de que estos IDs existan en tu HTML (o se crearán dinámicamente)
    const elTendenciaCitas = document.getElementById('trend-citas');
    const elCrecimientoIngresos = document.getElementById('trend-ingresos');
    const elClientesRiesgo = document.getElementById('clientes-riesgo') || document.getElementById('nuevos-clientes'); // Fallback
    
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
     * 📡 FUNCIÓN MAESTRA DE CARGA (🚩 CORREGIDA PARA FILTROS REALES)
     */
    async function cargarReportes() {
        try {
            const selectPeriodo = document.getElementById('filtro-periodo');
            const periodo = selectPeriodo?.value || 'mes';
            const mesSeleccionado = document.getElementById('filtro-mes')?.value;
            const anioSeleccionado = document.getElementById('filtro-anio')?.value;

            // 🛡️ BLINDAJE DE FILTROS: 
            // Solo usamos mes/año si el periodo es "especifico" o "mes"
            let url = `${API_BASE_URL}/Dashboard/resumen/${proveedorId}?periodo=${periodo}`;
            
            if (periodo === 'especifico' || periodo === 'mes') {
                if (mesSeleccionado && anioSeleccionado) {
                    url = `${API_BASE_URL}/Dashboard/resumen/${proveedorId}?periodo=mes&mes=${mesSeleccionado}&anio=${anioSeleccionado}`;
                }
            }
            
            console.log("🚀 Modo de Consulta:", periodo, "URL:", url);
            
            const response = await fetch(url, {
                method: 'GET',
                headers: {
                    'Authorization': `Bearer ${token}`,  
                    'Content-Type': 'application/json'
                }
            });

            if (!response.ok) throw new Error(`Error API: ${response.status}`);

            const data = await response.json();
            console.log("📊 Analítica Senior Recibida:", data);

            // A. Actualizar Stats Cards con Lógica BI
            txtTotalCitas.innerText = data.totalCitas || 0;
            
            // 📈 Inyectar tendencia de citas
            if (elTendenciaCitas) {
                const valor = data.tendenciaCitas || 0;
                const color = valor >= 0 ? '#48c1b5' : '#ff5e5e';
                const icono = valor >= 0 ? '↑' : '↓';
                elTendenciaCitas.innerHTML = `<small style="color: ${color}; font-weight: bold;">${icono} ${Math.abs(valor)}% vs ant.</small>`;
            }

            const ingresos = data.gananciaReal || 0;
            txtTotalIngresos.innerText = formatter.format(ingresos);

            // 💰 Inyectar crecimiento de ingresos
            if (elCrecimientoIngresos) {
                const valorInc = data.crecimientoIngresos || 0;
                const colorInc = valorInc >= 0 ? '#48c1b5' : '#ff5e5e';
                elCrecimientoIngresos.innerHTML = `<small style="color: ${colorInc}; font-weight: bold;">${valorInc >= 0 ? '+' : ''}${valorInc}% hoy</small>`;
            }

            // 👥 Clientes Nuevos vs En Riesgo
            txtNuevosClientes.innerText = data.nuevosClientesTotales || 0;
            if (data.clientesEnRiesgo > 0) {
                console.warn(`⚠️ Tienes ${data.clientesEnRiesgo} clientes que no han vuelto hace 1 mes.`);
            }

            // B. Pintar Tabla
            const detalles = data.proximasCitas || data.detalles || [];
            renderizarTabla(detalles);

            // C. 🎨 Generar Gráficas
            inicializarGraficaServicios(data.chartServiciosPopulares || []);
            inicializarGraficaCrecimiento(data.chartCrecimientoClientes || []);

            window.datosActuales = detalles;

        } catch (error) {
            console.error('❌ Error:', error);
            if(tablaCuerpo) {
                tablaCuerpo.innerHTML = `<tr><td colspan="6" style="text-align:center; color: #ff5e5e;">Error al conectar con el servidor</td></tr>`;
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
     * 📝 Renderizado de Tabla (CON SOPORTE DE REVERSIÓN Y ESTADOS REALES)
     */
    function renderizarTabla(lista) {
        if(!tablaCuerpo) return;
        tablaCuerpo.innerHTML = '';
        
        if (lista.length === 0) {
            tablaCuerpo.innerHTML = '<tr><td colspan="6" style="text-align:center; padding: 20px; color: #888;">No se encontraron registros para este filtro.</td></tr>';
            return;
        }

        lista.forEach(item => {
            const fechaVal = item.fecha ? item.fecha.split('T')[0] : '---';
            const horaVal = item.hora || '---';
            const cliente = item.cliente || 'Anónimo';
            const servicio = item.servicio || 'N/A';
            const monto = item.precioPactado || 0;
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
            if (estadoRaw === "pendiente") {
                btnAccion = `
                    <button class="btn-checkin-report" onclick="lanzarCheckIn('${item.id}', '${item.codigoVerificacion}')">
                        <i class="fas fa-user-check"></i> CHECK-IN
                    </button>
                    <button onclick="finalizarCita('${item.id}', 'cancelada')" style="background: transparent; color: #ff5e5e; border: 1px solid #ff5e5e; padding: 5px 8px; border-radius: 4px; cursor: pointer; font-size: 0.7rem; margin-top: 5px; width: 100%;">
                        <i class="fas fa-times"></i> ANULAR
                    </button>
                `;
            } else {
                btnAccion = `
                    <div style="display: flex; flex-direction: column; gap: 5px; align-items: center;">
                        <span style="font-size: 0.7rem; color: #888;"><i class="fas fa-lock"></i> Procesada</span>
                        <button onclick="finalizarCita('${item.id}', 'pendiente')" style="background: rgba(72, 193, 181, 0.1); color: #48c1b5; border: 1px solid #48c1b5; padding: 4px 8px; border-radius: 4px; cursor: pointer; font-size: 0.65rem; width: 100%;">
                            <i class="fas fa-undo"></i> CORREGIR
                        </button>
                    </div>
                `;
            }

            tr.innerHTML = `
                <td>
                    <div style="font-weight: bold; color: #48c1b5;">${fechaVal}</div>
                    <div style="font-size: 0.85em; color: #ccc;">${horaVal}</div>
                </td>
                <td><strong>${cliente}</strong></td>
                <td>${servicio}</td>
                <td style="color: #48c1b5; font-weight: bold;">${formatter.format(monto)}</td>
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

    // --- 📤 EXPORTACIÓN ---
    document.getElementById('btn-excel')?.addEventListener('click', () => {
        if (!window.datosActuales) return;
        const worksheet = XLSX.utils.json_to_sheet(window.datosActuales);
        const workbook = XLSX.utils.book_new();
        XLSX.utils.book_append_sheet(workbook, worksheet, "Turnify_Report");
        XLSX.writeFile(workbook, "Reporte_Turnify.xlsx");
    });

    document.getElementById('btn-pdf')?.addEventListener('click', () => {
        const elemento = document.getElementById('contenido-reporte');
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