document.addEventListener('DOMContentLoaded', () => {
    // 1. Configuración Validada
    const API_BASE_URL = 'http://localhost:5000/api'; 
    const proveedorId = 'F34FE619-8F7D-4EEE-8473-22979451EBC0'; 
    const token = localStorage.getItem('token');

    // Elementos del DOM
    const txtTotalCitas = document.getElementById('total-citas');
    const txtTotalIngresos = document.getElementById('total-ingresos');
    const txtNuevosClientes = document.getElementById('nuevos-clientes');
    const tablaCuerpo = document.getElementById('lista-reportes');
    const txtAdminName = document.getElementById('admin-name'); // Elemento del nombre

    const formatter = new Intl.NumberFormat('es-CO', {
        style: 'currency', currency: 'COP', minimumFractionDigits: 0
    });

    // Mostrar el nombre del Admin que guardamos en el login
    if (txtAdminName) {
        txtAdminName.innerText = localStorage.getItem('adminName') || 'Administrador';
    }

    async function cargarReportes() {
        try {
            console.log("🚀 Llamando a:", `${API_BASE_URL}/Dashboard/resumen/${proveedorId}`);
            
            const response = await fetch(`${API_BASE_URL}/Dashboard/resumen/${proveedorId}`, {
                method: 'GET',
                headers: {
                    'Authorization': `Bearer ${token}`,  
                    'Content-Type': 'application/json'
                }
            });

            if (!response.ok) throw new Error(`Error API: ${response.status}`);

            const data = await response.json();
            console.log("📦 Datos recibidos:", data);

            // 2. Mapeo exacto validado con el Swagger y Consola
            txtTotalCitas.innerText = data.totalCitas || 0;

            // Usamos gananciaReal para los ingresos totales
            const ingresos = data.gananciaReal || data.gananciaEstimada || 0;
            txtTotalIngresos.innerText = formatter.format(ingresos);

            txtNuevosClientes.innerText = data.nuevosClientes || 0;

            // 3. Renderizar Tabla
            const detalles = data.detalles || data.Detalles || [];
            renderizarTabla(detalles);

        } catch (error) {
            console.error('❌ Error:', error);
            if(tablaCuerpo) {
                tablaCuerpo.innerHTML = `<tr><td colspan="4" style="text-align:center; color: #ff5e5e;">Error de conexión o sesión expirada</td></tr>`;
            }
        }
    }

    function renderizarTabla(lista) {
        if(!tablaCuerpo) return;
        tablaCuerpo.innerHTML = '';
        
        if (lista.length === 0) {
            tablaCuerpo.innerHTML = '<tr><td colspan="4" style="text-align:center;">Sin datos hoy</td></tr>';
            return;
        }

        lista.forEach(item => {
            const fechaVal = item.fecha || item.Fecha || '';
            const horaVal = item.hora || item.Hora || '';
            const servicio = item.servicioNombre || item.ServicioNombre || 'Servicio';
            const cliente = item.clienteNombre || item.ClienteNombre || 'Cliente';
            const precio = item.precio || item.Precio || 0;
            const estado = (item.estado || item.Estado || 'pendiente').toLowerCase();

            const tr = document.createElement('tr');
            const statusClass = (estado === 'completada' || estado === 'finalizado') ? 'status-success' : 'status-pending';

            tr.innerHTML = `
                <td>${fechaVal.split('T')[0]} ${horaVal}</td>
                <td>
                    <div style="font-weight:bold">${servicio}</div>
                    <div style="font-size:11px; color:#7ed9c3">${cliente}</div>
                </td>
                <td style="font-weight:bold">${formatter.format(precio)}</td>
                <td><span class="status-pill ${statusClass}">${estado}</span></td>
            `;
            tablaCuerpo.appendChild(tr);
        });
    }

    // 4. Lógica para cerrar sesión
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