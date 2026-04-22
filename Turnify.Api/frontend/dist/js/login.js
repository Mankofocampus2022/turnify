// 🛡️ CAMBIO SENIOR: Dejamos la ruta fija "api/Usuarios" para evitar problemas.
const API_URL = 'http://localhost:5000/api/Usuarios/login';

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

            // 1. Guardar en LocalStorage para toda la sesión
            localStorage.setItem('turnify_token', token); 
            localStorage.setItem('token', token);
            
            // Guardamos el objeto usuario completo (importante para el dashboard)
            localStorage.setItem('user', JSON.stringify(data.user));

            // Extraemos y normalizamos el Rol
            const userRole = (data.user.rol || data.user.Rol || "").toUpperCase();
            localStorage.setItem('usuario_rol', userRole);

            // 2. Definición de IDs de respaldo (Tus GUIDs de SQL Server)
            const ADMIN_ID = "8854C07C-6E5E-4876-A29A-C7AD5DCFBAB7"; 

            // 3. 🛡️ REDIRECCIÓN INTELIGENTE (Blindada)
            // Agregamos SUPERADMINISTRADOR que es el que viene en tu log
            const esAdmin = userRole.includes("ADMIN") || 
                            userRole.includes("PROVEEDOR") || 
                            data.user.rolId === ADMIN_ID;

            console.log("Verificando acceso para rol:", userRole);

            if (esAdmin) {
                console.log("🚀 Acceso concedido al Dashboard");
                window.location.href = 'admin-dashboard.html';
            } else {
                console.log("👤 Acceso a panel de Clientes");
                window.location.href = 'clientes.html';
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