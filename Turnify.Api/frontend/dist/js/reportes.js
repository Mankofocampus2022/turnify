document.addEventListener('DOMContentLoaded', () => {
    // 1. Configuración Validada
    const API_BASE_URL = 'http://localhost:5000/api'; 
    
    const userStr = localStorage.getItem('user');
    const user = userStr ? JSON.parse(userStr) : null;
    
    const proveedorId = user ? user.proveedorId : 'F34FE619-8F7D-4EEE-8473-22979451EBC0'; 
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

    /**
     * 📡 Función Maestra de Carga
     */
    async function cargarReportes() {
        try {
            const periodo = document.getElementById('filtro-periodo')?.value || 'mes';
            const url = `${API_BASE_URL}/Dashboard/resumen/${proveedorId}?periodo=${periodo}`;
            
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

            // Guardar data global para exportación
            window.datosActuales = detalles;

        } catch (error) {
            console.error('❌ Error:', error);
            if(tablaCuerpo) {
                tablaCuerpo.innerHTML = `<tr><td colspan="5" style="text-align:center; color: #ff5e5e;">Error al cargar datos</td></tr>`;
            }
        }
    }

    /**
     * 🍩 Gráfica de Dona: Servicios más pedidos
     */
    function inicializarGraficaServicios(servicios) {
        const ctx = document.getElementById('chartServicios').getContext('2d');
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
        const ctx = document.getElementById('chartClientes').getContext('2d');
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
     * 📝 Renderizado de Tabla
     */
    function renderizarTabla(lista) {
        if(!tablaCuerpo) return;
        tablaCuerpo.innerHTML = '';
        
        if (lista.length === 0) {
            tablaCuerpo.innerHTML = '<tr><td colspan="5" style="text-align:center;">No hay registros para este periodo.</td></tr>';
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

            tr.innerHTML = `
                <td>
                    <div style="font-weight: bold; color: #48c1b5;">${fechaVal}</div>
                    <div style="font-size: 0.85em; color: #ccc;">${horaVal}</div>
                </td>
                <td><strong>${cliente}</strong></td>
                <td>${servicio}</td>
                <td style="color: #48c1b5; font-weight: bold;">${formatter.format(monto)}</td>
                <td><span class="status-pill ${badgeClass}">${estado}</span></td>
            `;
            tablaCuerpo.appendChild(tr);
        });
    }

    // --- 📤 LÓGICA DE EXPORTACIÓN EXCEL ---
    document.getElementById('btn-excel')?.addEventListener('click', () => {
        if (!window.datosActuales) return;
        const worksheet = XLSX.utils.json_to_sheet(window.datosActuales);
        const workbook = XLSX.utils.book_new();
        XLSX.utils.book_append_sheet(workbook, worksheet, "Reporte_Turnify");
        XLSX.writeFile(workbook, "Reporte_Actividad_Turnify.xlsx");
    });

    // --- 📤 LÓGICA DE EXPORTACIÓN PDF (RECARGADA) ---
    document.getElementById('btn-pdf')?.addEventListener('click', () => {
        const elemento = document.getElementById('contenido-reporte');
        
        // 1. Le ponemos el "traje de gala" para el PDF
        elemento.classList.add('pdf-export-mode');

        const opt = {
            margin: [10, 10],
            filename: `Reporte_Turnify_${new Date().toLocaleDateString()}.pdf`,
            image: { type: 'jpeg', quality: 0.98 },
            html2canvas: { 
                scale: 2, 
                useCORS: true, 
                backgroundColor: '#0b141d', 
                letterRendering: true
            },
            jsPDF: { unit: 'mm', format: 'a4', orientation: 'portrait' }
        };

        // 2. Generamos el PDF
        html2pdf().set(opt).from(elemento).save().then(() => {
            // 3. Le quitamos el traje de gala para que el Dashboard siga normal
            elemento.classList.remove('pdf-export-mode');
        });
    });

    // Filtros y Logout
    document.getElementById('filtro-periodo')?.addEventListener('change', cargarReportes);

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