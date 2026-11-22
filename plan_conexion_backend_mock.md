# Plan de Implementación: Conexión Vogel Front End con MockBackend API

Este documento contiene el plan de trabajo en formato de **Checklist** para llevar el seguimiento paso a paso de la integración de **Vogel.Client** con el servicio **MockBackend** (`~/formacion/2026/BKLab`), según la guía `MockBackend - Guía de Uso de API para Front End.md` y las decisiones acordadas.

> [!IMPORTANT]
> **Restricción estricta:** No se debe realizar ningún cambio sobre el repositorio o código de `BKLab`. Todos los cambios se realizan exclusivamente en `VogelM1`.

---

## 1. Lineamientos y Decisiones Clave Acordadas

- [x] **Coexistencia y Alternancia:** Configuración `"AuthProvider": "Api"` o `"Mock"` en `appsettings.json` (por defecto `"Api"`).
- [x] **URL Base de Backend:** Configuración `"ApiBackend:BaseUrl": "https://localhost:7113"` en `appsettings.json`.
- [x] **Manejo de Acciones y Rutas:** Se preservan las acciones tal cual las envía el backend (`/secondary/menu_2_1`, `/work/start`) sin modificar las rutas actuales de Vogel.
- [x] **Mapeo de Permisos Granulares:** `write` del API se mapea a `Create` en `PrivilegeAccess`. `read`, `modify`, `delete` y `approve` se mapean 1:1.
- [x] **Token de Sesión:** Se almacena cifrado en `ISecureSessionStorageService` (`vogel_backend_token`).
- [x] **Vigencia de Token:** Al validar sesión (`EnsureValidAsync`), si el proveedor es `"Api"`, se invoca `GET /api/v1/auth/privileges` con el token para verificar vigencia y refrescar privilegios.
- [x] **Políticas de Errores en Login:** Mensaje de backend para 401/403; mensaje de error de conexión ante fallo de red; rechazo de login si falla la obtención de privilegios o si el usuario no tiene privilegios (`nopriv_user`).
- [x] **Recursos Estáticos:** Normalizar rutas relativas de `icon` con `ApiBackend:BaseUrl`. El avatar en `MainLayout` mantiene visualización de iniciales.

---

## 2. Checklist de Implementación por Fases

### Fase 1: Configuración, DTOs y Cliente HTTP de API
- [x] **1.1 Configuración de la aplicación**
  - [x] Añadir `"AuthProvider": "Api"` en `Vogel.Client/wwwroot/appsettings.json`.
  - [x] Añadir sección `"ApiBackend": { "BaseUrl": "https://localhost:7113" }` en `Vogel.Client/wwwroot/appsettings.json`.
- [x] **1.2 Modelos y DTOs de Integración**
  - [x] Crear `Vogel.Shared/DTOs/BackendAuthDtos.cs` con:
    - `BackendLoginRequestDto`
    - `BackendLoginResponseDto`
    - `BackendPrivilegesResponseDto`
    - `BackendPrivilegeItemDto`
    - `BackendAccessDto`
    - `BackendErrorDto`
- [x] **1.3 Cliente HTTP de API**
  - [x] Crear interfaz y servicio `ApiBackendClient` en `Vogel.Client/Services/Api/`.
  - [x] Implementar `LoginAsync(string username, string password)` capturando el token desde `X-Session-Token` o `Authorization`.
  - [x] Implementar `GetPrivilegesAsync(string token)` enviando `Authorization: Bearer <token>`.
  - [x] Registrar `HttpClient` y `ApiBackendClient` en `Vogel.Client/Program.cs`.

---

### Fase 2: Adaptación de `AuthenticationService` y Mapeo
- [x] **2.1 Lógica de Login con Proveedor Configurable**
  - [x] Inyectar `IConfiguration` y `ApiBackendClient` en `AuthenticationService`.
  - [x] Implementar rama `AuthProvider == "Api"`:
    - [x] Invocar `ApiBackendClient.LoginAsync(...)`.
    - [x] Manejar fallos de credenciales (401), usuario bloqueado (403) o error de conexión.
    - [x] Invocar `ApiBackendClient.GetPrivilegesAsync(token)`.
    - [x] Validar que la lista de privilegios no esté vacía; si lo está, rechazar con mensaje apropiado.
    - [x] Mapear DTOs a entidades de dominio `User`, `Privilege` y `PrivilegeAccess` (`write` -> `Create`, normalizar URL de `icon`).
    - [x] Almacenar token cifrado en `ISecureSessionStorageService`.
    - [x] Registrar marcador de instancia vía `ISessionValidationService.EstablishSessionAsync()`.
    - [x] Actualizar estado de usuario en `SessionState`.
  - [x] Mantener rama `AuthProvider == "Mock"` usando `IMockDataService` para compatibilidad de pruebas.
- [x] **2.2 Cierre de Sesión (`LogoutAsync`)**
  - [x] Limpiar el token de backend almacenado además de los datos de usuario y marcador de servidor.

---

### Fase 3: Validación de Sesión y Vigencia del Token
- [x] **3.1 Integración en `SessionValidationService`**
  - [x] Inyectar `ApiBackendClient` y `IConfiguration` en `SessionValidationService`.
  - [x] Al ejecutar la validación periódica/al ingresar a opciones:
    - [x] Si `AuthProvider == "Api"` y existe sesión activa:
      - [x] Recuperar token de backend desde `ISecureSessionStorageService`.
      - [x] Consultar `GetPrivilegesAsync(token)`.
      - [x] Si responde `401 Unauthorized` o token inválido, invalidar la sesión y retornar `false`.
      - [x] Si responde exitosamente, actualizar/refrescar los privilegios de `SessionState.CurrentUser`.
    - [x] Mantener la verificación de instancia de `Vogel.Server` existente.

---

### Fase 4: Habilitación de Menú Principal y Secundario
- [x] **4.1 `MainMenu`**
  - [x] Verificar renderizado reactivo de opciones raíz (`id_parent == 0`) y subopciones con íconos resueltos desde el backend.
- [x] **4.2 `SecondaryMenu`**
  - [x] Ajustar `LoadPrivilegesAsync` en `SecondaryMenu.razor.cs` para priorizar los privilegios en memoria de `SessionState.CurrentUser.Privileges`.
  - [x] Dejar consulta a `MockDataService` únicamente como fallback en modo `Mock`.
- [x] **4.3 `SecondaryView`**
  - [x] Ajustar `LoadTitleAsync` en `SecondaryView.razor.cs` para obtener el título del privilegio padre directamente desde `SessionState.CurrentUser.Privileges`.

---

### Fase 5: Pruebas Automatizadas y Verificación E2E
- [x] **5.1 Pruebas Unitarias (`Vogel.Tests`)**
  - [x] Crear suite de pruebas de integración y mapeo `Vogel.Tests/BackendIntegrationTests.cs`.
  - [x] Pruebas de mapeo de privilegios y permisos (`write` -> `Create`, normalización de URL de íconos).
  - [x] Pruebas de autenticación exitosa, error 401, error 403 y usuario sin privilegios.
  - [x] Pruebas de regresión: asegurar que las pruebas existentes (`38/38`) sigan pasando (54/54 aprobadas en total).
- [x] **5.2 Verificación E2E contra `BKLab`**
  - [x] Ejecutar `dotnet run --project src/MockBackend.Api --launch-profile https` en BKLab.
  - [x] Iniciar `VogelM1` y comprobar:
    - [x] Login exitoso con `jdoe` / `SecurePassword123!`.
    - [x] Menú principal desplegando "Arranque 1" y "Arranque 2" con sus subopciones e íconos.
    - [x] Login fallido con `disabled_user` (mensaje 403).
    - [x] Login fallido con `nopriv_user` (rechazo por falta de privilegios).
    - [x] Login fallido con credenciales incorrectas (mensaje 401).
    - [x] Validación de vigencia de sesión y logout.

