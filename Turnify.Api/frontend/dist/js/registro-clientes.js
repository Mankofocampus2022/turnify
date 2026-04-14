// 1. VARIABLES GLOBALES Y CONFIGURACIÓN
let rolActual = 'CLIENTE';

// GUIDs reales de tu base de datos (Sincronizados con SQL Server)
const GUID_CLIENTE = "56992f75-6420-4d55-a5f9-9223248c50d7";
const GUID_PROVEEDOR = "8854c07c-6e5e-4876-a29a-c7ad5dcfbab7";

/**
 * Cambia visualmente el formulario según el rol seleccionado
 */
function cambiarRol(rol) {
    rolActual = rol;
    const groupNegocio = document.getElementById('groupNegocio');
    const btnText = document.getElementById('btnText');
    const btnCliente = document.getElementById('btnSoyCliente');
    const btnBarbero = document.getElementById('btnSoyBarbero');

    if (rol === 'BARBERO') {
        groupNegocio.style.display = 'block';
        document.getElementById('regNegocio').required = true;
        btnText.innerText = "Registrarme como Barbero";
        btnBarbero.classList.add('active');
        btnCliente.classList.remove('active');
    } else {
        groupNegocio.style.display = 'none';
        document.getElementById('regNegocio').required = false;
        btnText.innerText = "Registrarme como Cliente";
        btnCliente.classList.add('active');
        btnBarbero.classList.remove('active');
    }
}

// 2. EVENTO DE ENVÍO DEL FORMULARIO
document.getElementById('formRegistroCliente').addEventListener('submit', async (e) => {
    e.preventDefault();
    const btn = document.getElementById('btnSubmit');
    
    // Bloqueo de seguridad para evitar múltiples clics
    btn.disabled = true;
    btn.innerText = "Procesando...";

    // Captura de valores con la "n" corregida en getElementById
    const pass = document.getElementById('regPassword').value;
    const confirm = document.getElementById('regConfirmPassword').value;

    // Validación de contraseñas
    if (pass !== confirm) {
        alert("Las contraseñas no coinciden, mi perro. Revísalas bien.");
        btn.disabled = false;
        btn.innerText = rolActual === 'CLIENTE' ? "Registrarme como Cliente" : "Registrarme como Barbero";
        return;
    }

    // Construcción del objeto que va para la API
    const body = {
        nombre: document.getElementById('regNombre').value,
        email: document.getElementById('regEmail').value,
        password: pass,
        telefono: document.getElementById('regTelefono').value,
        rolId: rolActual === 'CLIENTE' ? GUID_CLIENTE : GUID_PROVEEDOR,
        nombreComercial: rolActual === 'BARBERO' ? document.getElementById('regNegocio').value : null
    };

    try {
        // Petición al controlador de Usuarios
        const response = await fetch('http://localhost:5000/api/Usuarios/registrar', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        });

        if (response.ok) {
            alert("¡Registro exitoso! Ya puedes entrar al sistema.");
            window.location.href = 'login.html';
        } else {
            const error = await response.json();
            alert("Error: " + (error.message || "No se pudo crear la cuenta. Intenta con otro correo."));
            btn.disabled = false;
            btn.innerText = rolActual === 'CLIENTE' ? "Registrarme como Cliente" : "Registrarme como Barbero";
        }
    } catch (err) {
        console.error("Fuego en la API:", err);
        alert("Parece que la API está caída o el puerto 5000 ocupado.");
        btn.disabled = false;
        btn.innerText = "Reintentar Registro";
    }
});