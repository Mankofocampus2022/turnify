document.addEventListener('DOMContentLoaded', () => {
    // 1. Configuración Validada
    const API_BASE_URL = 'http://localhost:5000/api'; 
    
    const userStr = localStorage.getItem('user');
    const user = userStr ? JSON.parse(userStr) : null;
    
    // Rescate de ID para que no use el genérico si el barbero ya entró
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
     * 📡 Función Maestra de Carga
     */
    async function cargarReportes() {
        try {
            const periodo = document.getElementById('filtro-periodo')?.value || 'mes';
            const mesSeleccionado = document.getElementById('filtro-mes')?.value || '';
            const anioSeleccionado = document.getElementById('filtro-anio')?.value || '';

            // 🚩 REFUERZO ANTIBUGS: Aseguramos que los parámetros viajen limpios al backend
            let url = `${API_BASE_URL}/Dashboard/resumen/${proveedorId}?periodo=${periodo}`;
            
            // Si hay mes y año, forzamos la precisión para que el Service no use fallbacks
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
            txtTotalCitas.innerText = data.totalServicios || data.totalCitas || 0;
            const ingresos = data.gananciaReal || data.ingresosReales || 0;
            txtTotalIngresos.innerText = formatter.format(ingresos);
            txtNuevosClientes.innerText = data.nuevosClientesTotales || data.nuevosClientes || 0;

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

        if (chartServicios) {
            chartServicios.destroy();
        }
        
        const existingChart = Chart.getChart(canvas); 
        if (existingChart) {
            existingChart.destroy();
        }

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

        if (chartClientes) {
            chartClientes.destroy();
        }
        
        const existingChart = Chart.getChart(canvas); 
        if (existingChart) {
            existingChart.destroy();
        }

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
     * 📝 Renderizado de Tabla (CON REVERSIÓN Y CANCELACIÓN ROJO LAVA)
     */
    function renderizarTabla(lista) {
        if(!tablaCuerpo) return;
        tablaCuerpo.innerHTML = '';
        
        if (lista.length === 0) {
            tablaCuerpo.innerHTML = '<tr><td colspan="6" style="text-align:center; padding: 20px;">No hay registros para este periodo.</td></tr>';
            return;
        }

        lista.forEach(item => {
            const fechaVal = item.fecha || '---';
            const horaVal = item.hora || '---';
            const cliente = item.cliente || item.clienteNombre || 'Anónimo';
            const servicio = item.servicio || item.servicioNombre || 'N/A';
            const monto = item.monto || item.precioPactado || 0;
            const estado = (item.estado || 'pendiente').toLowerCase();

            const tr = document.createElement('tr');
            let badgeClass = 'status-pending';
            if (estado.includes('completada')) badgeClass = 'status-success';
            if (estado.includes('cancelada')) badgeClass = 'status-danger';

            // 🚩 LÓGICA DE BOTONES DINÁMICOS
            let btnAccion = "";
            if (estado === "pendiente") {
                btnAccion = `
                    <button onclick="finalizarCita('${item.id || item.Id}', 'completada')" style="background: #48c1b5; color: #162431; border: none; padding: 5px 8px; border-radius: 4px; cursor: pointer; font-weight: bold; font-size: 0.7rem; margin-right: 5px;"><i class="fas fa-check"></i> COBRAR</button>
                    <button onclick="finalizarCita('${item.id || item.Id}', 'cancelada')" style="background: #ff4b2b; color: white; border: none; padding: 5px 8px; border-radius: 4px; cursor: pointer; font-weight: bold; font-size: 0.7rem;"><i class="fas fa-times"></i> CANCELAR</button>
                `;
            } else {
                // 🚩 BOTÓN DE REVERSIÓN: Permite corregir errores (vuelve a pendiente)
                btnAccion = `<button onclick="finalizarCita('${item.id || item.Id}', 'pendiente')" style="background: transparent; color: #ccc; border: 1px solid #555; padding: 4px 8px; border-radius: 4px; cursor: pointer; font-size: 0.65rem;"><i class="fas fa-undo"></i> REVERSAR</button>`;
            }

            tr.innerHTML = `
                <td>
                    <div style="font-weight: bold; color: #48c1b5;">${fechaVal}</div>
                    <div style="font-size: 0.85em; color: #ccc;">${horaVal}</div>
                </td>
                <td><strong>${cliente}</strong></td>
                <td>${servicio}</td>
                <td style="color: #48c1b5; font-weight: bold;">${formatter.format(monto)}</td>
                <td><span class="status-pill ${badgeClass}">${estado}</span></td>
                <td style="text-align: center;">${btnAccion}</td>
            `;
            tablaCuerpo.appendChild(tr);
        });
    }

    /**
     * ⚡ FUNCIÓN PARA ACTUALIZAR ESTADO (PATCH)
     */
    async function finalizarCita(id, nuevoEstado = "completada") {
        if (!id || id === 'undefined') return;
        
        const confirmMsg = nuevoEstado === 'pendiente' ? "¿Deseas REVERSAR esta cita a estado pendiente?" : `¿Deseas marcar esta cita como ${nuevoEstado.toUpperCase()}?`;
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
                alert(`✅ Estado actualizado a ${nuevoEstado}.`);
                cargarReportes(); 
            } else {
                const errorText = await response.text();
                console.error("Error:", errorText);
                alert("❌ No se pudo actualizar el estado.");
            }
        } catch (e) { console.error("Error al actualizar estado:", e); }
    }

    window.finalizarCita = finalizarCita;

    // --- 📤 LÓGICA DE EXPORTACIÓN ---
    document.getElementById('btn-excel')?.addEventListener('click', () => {
        if (!window.datosActuales) return;
        const worksheet = XLSX.utils.json_to_sheet(window.datosActuales);
        const workbook = XLSX.utils.book_new();
        XLSX.utils.book_append_sheet(workbook, worksheet, "Reporte_Turnify");
        XLSX.writeFile(workbook, "Reporte_Actividad_Turnify.xlsx");
    });

    document.getElementById('btn-pdf')?.addEventListener('click', () => {
        const elemento = document.getElementById('contenido-reporte');
        elemento.classList.add('pdf-export-mode');
        const opt = {
            margin: [10, 10],
            filename: `Reporte_Turnify_${new Date().toLocaleDateString()}.pdf`,
            image: { type: 'jpeg', quality: 0.98 },
            html2canvas: { scale: 2, useCORS: true, backgroundColor: '#0b141d', letterRendering: true },
            jsPDF: { unit: 'mm', format: 'a4', orientation: 'portrait' }
        };
        html2pdf().set(opt).from(elemento).save().then(() => {
            elemento.classList.remove('pdf-export-mode');
        });
    });

    // Filtros y Logout
    document.getElementById('filtro-periodo')?.addEventListener('change', cargarReportes);
    document.getElementById('filtro-mes')?.addEventListener('change', cargarReportes);
    document.getElementById('filtro-anio')?.addEventListener('change', cargarReportes);

    const btnLogout = document.getElementById('btn-logout');
    if (btnLogout) {
        btnLogout.addEventListener('click', (e) => {
            e.preventDefault();
            localStorage.clear(); 
            window.location.href = 'login.html'; 
        });
    }

    cargarReportes();
});