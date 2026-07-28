/* ============================================================
   TURNIFY - MOTOR DE AUTENTICACIÓN E IDENTIDAD
   ============================================================ */

// 🛡️ CAMBIO SENIOR: Dejamos la ruta fija "api/Usuarios" para evitar problemas.
// 🧠 BLINDAJE PARA DOCKER/PRODUCCIÓN: Detecta el host en caliente. Si entras desde localhost usa el puerto 5000, 
// si entras desde otra IP de la red local o dominio, reconfigura el endpoint automáticamente para que el navegador no falle.
const API_HOST = (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1')
    ? 'http://localhost:5000'
    : `${window.location.protocol}//${window.location.hostname}:5000`;

const API_URL = `${API_HOST}/api/Usuarios/login`;

async function login() {
    const btn = document.getElementById('btnEntrar');
    const emailInput = document.getElementById('email');
    const passwordInput = document.getElementById('password');

    const email = emailInput.value.trim();
    const password = passwordInput.value.trim();

    if (!email || !password) {
        alert("⚠️ Por favor, ingresa correo y contraseña.");
        return;
    }

    // Estado de carga (UX)
    btn.disabled = true;
    btn.innerText = "Cargando...";

    try {
        const response = await fetch(API_URL, {
            method: 'POST',
            headers: { 
                'Content-Type': 'application/json',
                'Accept': 'application/json' 
            },
            body: JSON.stringify({ 
                Email: email, 
                Password: password 
            })
        });

        if (response.ok) {
            const data = await response.json();
            console.log("✅ Login exitoso", data);

            // --- 🚩 FIX CRÍTICO: Extraemos el token del objeto de respuesta ---
            const token = data.token || data.Token; 

            if (!token) {
                throw new Error("El servidor no devolvió un token válido.");
            }

            // 1. Guardar en LocalStorage para toda la sesión (Doble guardado para compatibilidad)
            localStorage.setItem('turnify_token', token); 
            localStorage.setItem('token', token);
            
            // Guardamos el objeto usuario completo (importante para el dashboard)
            localStorage.setItem('user', JSON.stringify(data.user));

            // 🚀 PERSISTENCIA DE IDS E IDENTIDADES SEGÚN ROL INVERTIDO
            // Guardamos el proveedorId (Negocio del Administrador/Dueño) y el empleadoId (si es colaborador/barbero/Staff) de forma explícita
            if (data.user.proveedorId) {
                localStorage.setItem('turnify_proveedor_id', data.user.proveedorId);
            } else {
                localStorage.removeItem('turnify_proveedor_id');
            }

            if (data.user.empleadoId) {
                localStorage.setItem('turnify_empleado_id', data.user.empleadoId);
            } else {
                localStorage.removeItem('turnify_empleado_id');
            }

            // Extraemos y normalizamos el Rol (Manejamos nulls con "")
            const userRole = (data.user.rol || data.user.Rol || data.user.rolNombre || "").toUpperCase();
            localStorage.setItem('usuario_rol', userRole);
            localStorage.setItem('user_role', userRole); // ➕ AÑADIDO SIN BORRAR NADA: Compatibilidad para lectura de roles en vistas

            // 2. Definición de IDs de respaldo (Tus GUIDs de SQL Server)
            const ADMIN_ID = "8854C07C-6E5E-4876-A29A-C7AD5DCFBAB7"; 

            // 3. 🛡️ REDIRECCIÓN INTELIGENTE (Blindada)
            // Se sincroniza con tu lógica real: 
            // - El Administrador (Dueño del Local) y el Staff (Colaborador/Barbero) van al "admin-dashboard.html" para gestionar turnos y agendas.
            // - El Cliente (no administrativo) va al flujo de agendamiento "agendar-cita.html".
            const esAdmin = userRole.includes("ADMIN") || 
                            userRole.includes("SUPERADMIN") ||
                            userRole.includes("SUPER_ADMIN") ||
                            userRole.includes("PROVEEDOR") || 
                            userRole.includes("BARBERO") ||
                            userRole.includes("STAFF") ||
                            data.user.rolId?.toUpperCase() === ADMIN_ID;

            console.log("Verificando acceso para rol:", userRole);

            if (esAdmin) {
                console.log("🚀 Acceso concedido al Dashboard Administrativo");
                window.location.href = 'admin-dashboard.html';
            } else {
                console.log("👤 Acceso a panel de Clientes (Agendamiento)");
                // 🚩 CAMBIO ESTRATÉGICO: Mandamos al cliente directo a agendar
                // Si 'clientes.html' te rebota, es porque ese archivo tiene un script de auth viejo.
                window.location.href = 'agendar-cita.html'; 
            }

        } else {
            let errorMsg = "Credenciales incorrectas o error en el servidor";
            const contentType = response.headers.get("content-type");
            if (contentType && contentType.includes("application/json")) {
                const errorData = await response.json();
                errorMsg = errorData.message || errorMsg;
            }

            alert("❌ Acceso denegado: " + errorMsg);
            btn.disabled = false;
            btn.innerText = "Entrar";
        }

    } catch (error) {
        console.error("Error:", error);
        alert("🚀 Error de conexión: " + error.message);
        btn.disabled = false;
        btn.innerText = "Entrar";
    }
}

document.addEventListener('DOMContentLoaded', () => {
    const loginBtn = document.getElementById('btnEntrar');
    if (loginBtn) {
        loginBtn.addEventListener('click', login);
    }
});