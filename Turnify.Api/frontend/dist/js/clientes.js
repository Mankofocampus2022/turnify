document.addEventListener('DOMContentLoaded', () => {
    const rol = localStorage.getItem('usuario_rol');
    const rolesPermitidos = ['Administrador', 'SuperAdmin', 'Cliente'];

    if (!rolesPermitidos.includes(rol)) {
        window.location.href = 'login.html';
        return;
    }

    cargarClientes();
});

async function cargarClientes() {
    try {
        const response = await fetch('http://localhost:5261/api/Clientes'); 
        
        if (response.ok) {
            const clientes = await response.json();
            const tabla = document.getElementById('lista-clientes');
            if (!tabla) return;

            tabla.innerHTML = ''; 

            clientes.forEach(cliente => {
                // Diseño Senior: Agrupamos datos para que se vea ordenado
                const fila = `
                    <tr>
                        <td>
                            <div style="font-weight: 600; color: #1e293b;">${cliente.nombre}</div>
                            <div style="font-size: 12px; color: #64748b;">ID: ${cliente.id.substring(0,8)}...</div>
                        </td>
                        <td>
                            <div style="font-size: 14px;">${cliente.email}</div>
                            <div style="font-size: 12px; color: #2563eb;">${cliente.telefono || 'Sin teléfono'}</div>
                        </td>
                        <td>
                            <span class="status ${cliente.activo ? 'active' : 'inactive'}">
                                ${cliente.activo ? '● Activo' : '○ Inactivo'}
                            </span>
                        </td>
                        <td style="text-align: right;">
                            <button class="btn-action">Editar</button>
                        </td>
                    </tr>
                `;
                tabla.innerHTML += fila;
            });
        }
    } catch (error) {
        console.error("Error crítico:", error);
    }
}