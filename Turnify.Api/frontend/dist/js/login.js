const API_URL = 'http://localhost:5000/api/Usuarios/login';

async function login() {
    const btn = document.getElementById('btnEntrar');
    const emailInput = document.getElementById('email');
    const passwordInput = document.getElementById('password');

    const email = emailInput.value.trim();
    const password = passwordInput.value.trim();

    // 1. Validaciones iniciales
    if (!email || !password) {
        alert("⚠️ Por favor, ingresa correo y contraseña.");
        return;
    }

    btn.disabled = true;
    btn.innerText = "Verificando...";

    try {
        const response = await fetch(API_URL, {
            method: 'POST',
            headers: { 
                'Content-Type': 'application/json',
                'Accept': 'application/json' 
            },
            body: JSON.stringify({ Email: email, Password: password })
        });

        const data = await response.json();

       if (response.ok) {
            console.log("✅ Acceso concedido");
            
            // 2. Guardamos la info en el localStorage
            localStorage.setItem('turnify_token', data.token);
            localStorage.setItem('usuario_nombre', data.user.nombre);
            localStorage.setItem('usuario_rol', data.user.rol); 
            localStorage.setItem('usuario_id', data.user.id);   

            // --- 🚩 EL CAMBIO GANADOR ---
            // Si la API nos manda el proveedorId, lo guardamos. 
            // Si no (porque es un Admin sin negocio), guardamos el ID de usuario como respaldo.
            const pId = data.user.proveedorId || data.user.id;
            localStorage.setItem('proveedor_id', pId); 

            console.log("ID Proveedor guardado:", pId);
            
            // 3. Blindaje de Rol
            const userRole = (data.user.rol || "").toUpperCase().trim();
            console.log("Rol detectado:", userRole);

            if (userRole.includes("ADMIN")) {
                console.log("🚀 Redirigiendo a Dashboard");
                window.location.href = 'admin-dashboard.html'; 
            } else {
                console.log("👤 Redirigiendo a Clientes");
                window.location.href = 'clientes.html';
            }

        } else {
            // 4. Manejo de errores específicos
            if (response.status === 403) {
                alert("🚫 CUENTA BLOQUEADA: " + (data.message || "Contacta al soporte."));
            } else if (response.status === 402) { 
                alert("💳 SUSCRIPCIÓN VENCIDA: Por favor renueva tu plan.");
            } else {
                alert("❌ Error: " + (data.message || "Credenciales incorrectas"));
            }
            
            btn.disabled = false;
            btn.innerText = "Entrar al Panel";
        }
    } catch (error) {
        console.error("Error de conexión:", error);
        alert("🚀 Error de conexión. Revisa que la API esté activa en el puerto 5000.");
        btn.disabled = false;
        btn.innerText = "Entrar al Panel";
    }
}

// 5. Inicialización y Eventos
document.addEventListener('DOMContentLoaded', () => {
    const loginBtn = document.getElementById('btnEntrar');
    if (loginBtn) {
        loginBtn.addEventListener('click', login);
    }

    document.addEventListener('keypress', (e) => {
        if (e.key === 'Enter') {
            const btn = document.getElementById('btnEntrar');
            if (btn && !btn.disabled) {
                login();
            }
        }
    });
});