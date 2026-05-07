document.addEventListener('DOMContentLoaded', () => {
    // 1. Configuración Validada (URL Dinámica Blindada)
    const API_BASE_URL = window.location.origin + '/api'; 
    
    const userStr = localStorage.getItem('user');
    const user = userStr ? JSON.parse(userStr) : null;
    
    // Rescate de ID Senior para que no use el genérico si el barbero ya entró
    const proveedorId = user ? (user.proveedorId || user.id) : 'F34FE619-8F7D-4EEE-8473-22979451EBC0'; 
    const token = localStorage.getItem('token') || localStorage.getItem('turnify_token');

    // Elementos UI
    const txtTotalCitas = document.getElementById('total-citas');
    const txtTotalIngresos = document.getElementById('total-ingresos');
    const txtNuevosClientes = document.getElementById('nuevos-clientes');
    const tablaCuerpo = document.getElementById('lista-reportes');
    const txtAdminName = document.getElementById('admin-name'); 
    
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
     * 📡 Función Maestra de Carga (Blindada con Parámetros UTC)
     */
    async function cargarReportes() {
        try {
            const periodo = document.getElementById('filtro-periodo')?.value || 'mes';
            const mesSeleccionado = document.getElementById('filtro-mes')?.value || '';
            const anioSeleccionado = document.getElementById('filtro-anio')?.value || '';

            // 🚩 REFUERZO ANTIBUGS: Aseguramos que los parámetros viajen limpios al backend
            let url = `${API_BASE_URL}/Dashboard/resumen/${proveedorId}?periodo=${periodo}`;
            
            if (mesSeleccionado && anioSeleccionado) {
                url = `${API_BASE_URL}/Dashboard/resumen/${proveedorId}?periodo=mes&mes=${mesSeleccionado}&anio=${anioSeleccionado}`;
            }
            
            console.log("🚀 Consultando Analítica en:", url);
            
            const response = await fetch(url, {
                method: 'GET',
                headers: {
                    'Authorization': `Bearer ${token}`,  
                    'Content-Type': 'application/json'
                }
            });

            if (!response.ok) throw new Error(`Error API: ${response.status}`);

            const data = await response.json();
            console.log("📊 Analítica Recibida:", data);

            // A. Actualizar Stats Cards
            txtTotalCitas.innerText = data.totalCitas || 0;
            const ingresos = data.gananciaReal || 0;
            txtTotalIngresos.innerText = formatter.format(ingresos);
            txtNuevosClientes.innerText = data.nuevosClientesTotales || 0;

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
                tablaCuerpo.innerHTML = `<tr><td colspan="6" style="text-align:center; color: #ff5e5e;">Error al cargar datos</td></tr>`;
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
                    legend: { position: 'bottom', labels: { color: '#ccc' } }
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
                    label: 'Clientes Nuevos',
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
                    y: { beginAtZero: true, grid: { color: '#2a3b4c' }, ticks: { color: '#ccc' } },
                    x: { grid: { display: false }, ticks: { color: '#ccc' } }
                }
            }
        });
    }

    /**
     * 📝 Renderizado de Tabla (CON SOPORTE DE REVERSIÓN PARA ERRORES)
     */
    function renderizarTabla(lista) {
        if(!tablaCuerpo) return;
        tablaCuerpo.innerHTML = '';
        
        if (lista.length === 0) {
            tablaCuerpo.innerHTML = '<tr><td colspan="6" style="text-align:center; padding: 20px;">No hay registros para este periodo.</td></tr>';
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
            
            // 🛡️ Lógica de visualización de estados
            let badgeClass = 'status-pendiente';
            let textoEstado = estadoRaw;

            if (estadoRaw.includes('completada')) {
                badgeClass = 'status-activo';
                textoEstado = 'Finalizada';
            } else if (estadoRaw.includes('cancelada')) {
                badgeClass = 'status-bloqueado';
                textoEstado = 'Anulada / No asistió';
            }

            // 🚩 LÓGICA DE BOTONES DINÁMICOS (CON BOTÓN DE CORRECCIÓN)
            let btnAccion = "";
            if (estadoRaw === "pendiente") {
                btnAccion = `
                    <button class="btn-checkin-report" onclick="lanzarCheckIn('${item.id}', '${item.codigoVerificacion}')">
                        <i class="fas fa-user-check"></i> CHECK-IN
                    </button>
                    <button onclick="finalizarCita('${item.id}', 'cancelada')" style="background: transparent; color: #ff5e5e; border: 1px solid #ff5e5e; padding: 5px 8px; border-radius: 4px; cursor: pointer; font-size: 0.7rem; margin-top: 5px; width: 100%;">
                        <i class="fas fa-times"></i> ANULAR CITA
                    </button>
                `;
            } else {
                // 🛡️ BOTÓN DE DESHACER: En caso de error del administrador
                btnAccion = `
                    <div style="display: flex; flex-direction: column; gap: 5px; align-items: center;">
                        <span style="font-size: 0.7rem; color: #888;"><i class="fas fa-lock"></i> Procesada</span>
                        <button onclick="finalizarCita('${item.id}', 'pendiente')" style="background: rgba(72, 193, 181, 0.1); color: #48c1b5; border: 1px solid #48c1b5; padding: 4px 8px; border-radius: 4px; cursor: pointer; font-size: 0.65rem; width: 100%;">
                            <i class="fas fa-undo"></i> CORREGIR / DESHACER
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
     * 🛡️ MOTOR DE VALIDACIÓN (CHECK-IN DE 6 DÍGITOS)
     */
    async function lanzarCheckIn(citaId, tokenSugerido) {
        const userInput = prompt(`⚠️ VALIDACIÓN DE PRESENCIA\nIngrese el código de 6 dígitos del cliente para cerrar la cita:\n(Código: ${tokenSugerido})`);
        
        if (!userInput) return;

        try {
            const response = await fetch(`${API_BASE_URL}/Citas/validar-checkin`, {
                method: 'POST',
                headers: { 
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${token}`
                },
                body: JSON.stringify({ citaId: citaId, token: userInput })
            });

            if (response.ok) {
                alert("✅ Check-in exitoso. La cita se ha cobrado y cerrado.");
                cargarReportes(); 
            } else {
                const error = await response.json();
                alert("❌ Código incorrecto. No se puede cerrar la cita.");
            }
        } catch (e) { alert("🔌 Error de red al validar."); }
    }

    /**
     * ⚡ FUNCIÓN PARA ACTUALIZAR ESTADO (PATCH) CON REVERSIÓN
     */
    async function finalizarCita(id, nuevoEstado) {
        let confirmMsg = `¿Seguro que deseas marcar como ${nuevoEstado.toUpperCase()}?`;
        if (nuevoEstado === 'pendiente') {
            confirmMsg = "⚠️ ¿Deseas DESHACER el estado de esta cita y volverla a poner como PENDIENTE?\n(Esto reactivará el botón de Check-in)";
        }

        if (!confirm(confirmMsg)) return;

        try {
            const response = await fetch(`${API_BASE_URL}/Citas/${id}/estado`, {
                method: 'PATCH', 
                headers: { 
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${token}`
                },
                body: JSON.stringify({ nuevoEstado: nuevoEstado }) 
            });

            if (response.ok) {
                alert(`✅ Estado actualizado.`);
                cargarReportes(); 
            } else {
                alert("❌ No se pudo actualizar el estado.");
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
        XLSX.utils.book_append_sheet(workbook, worksheet, "Analitica_Turnify");
        XLSX.writeFile(workbook, "Reporte_Auditoria_Turnify.xlsx");
    });

    document.getElementById('btn-pdf')?.addEventListener('click', () => {
        const elemento = document.getElementById('contenido-reporte');
        elemento.classList.add('pdf-export-mode');
        html2pdf().set({
            margin: 10,
            filename: 'Reporte_Mensual_Turnify.pdf',
            html2canvas: { scale: 2, backgroundColor: '#0a101e' },
            jsPDF: { unit: 'mm', format: 'a4', orientation: 'portrait' }
        }).from(elemento).save().then(() => elemento.classList.remove('pdf-export-mode'));
    });

    document.getElementById('filtro-periodo')?.addEventListener('change', cargarReportes);
    document.getElementById('filtro-mes')?.addEventListener('change', cargarReportes);
    document.getElementById('filtro-anio')?.addEventListener('change', cargarReportes);

    cargarReportes();
});

function logout() {
    localStorage.clear();
    window.location.href = 'login.html';
}