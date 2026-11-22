# Plan de Implementación y Seguimiento: Integración Vogel - MicroID

**Documento de Referencia:** [Requerimiento_Integracion_MicroID.md](./Requerimiento_Integracion_MicroID.md)  
**Servicio Target:** `http://localhost:8021` (`X-System-Id: "MICROID"`)  
**Usuario de Prueba Inicial:** `carlos.gomez` / `UserPass2026*`  

---

## Tablero de Seguimiento General

| Fase | Descripción | Estado | Avance |
|---|---|---|---|
| Fase 1 | Configuración y Contratos Compartidos (`Vogel.Shared` & `appsettings`) | Completado | 4 / 4 |
| Fase 2 | Cliente HTTP MicroID y Gestión Automática de `X-System-Key` (`Vogel.Server`) | Completado | 5 / 5 |
| Fase 3 | Integración BFF en `AuthController` y `IMicroIdAuthService` (`Vogel.Server`) | Pendiente | 0 / 5 |
| Fase 4 | Integración en `Vogel.Client` (Cambio de Clave Real y Validación de Privilegios) | Pendiente | 0 / 4 |
| Fase 5 | Hardening de Seguridad del Servicio Vogel y Auditoría InfoSec | Pendiente | 0 / 5 |
| Fase 6 | Pruebas Funcionales End-to-End (`carlos.gomez`) y Verificación | Pendiente | 0 / 5 |

---

## Fase 1: Configuración y Contratos Compartidos

- [x] **T1.1 — Configurar opciones de MicroID y `AuthProvider` en `Vogel.Server/appsettings.json`**
  - Actualizar `AuthProvider` a `"MicroID"` (manteniendo soporte para `"Mock"` y `"Api"`).
  - Agregar sección `MicroId`: `BaseUrl` (`"http://localhost:8021"`), `SystemId` (`"MICROID"`), `SystemNumericId` (`1`), `TimeoutSeconds` (`30`), `CredRenovation` (`"MONTHLY"`).
  - Verificar que `SecurityHeaders:Content-Security-Policy` permita los recursos locales necesarios sin romper Blazor WASM.
- [x] **T1.2 — Crear `MicroIdOptions` en `Vogel.Server/Configuration/MicroIdOptions.cs`**
  - Implementar validación con `DataAnnotations` + `IValidatableObject` (exigir `https://` si `BaseUrl` no es loopback, validar `CredRenovation` en `MONTHLY | ANNUAL | TEMPORARY`).
- [x] **T1.3 — Crear/extender DTOs en `Vogel.Shared/DTOs/`**
  - Crear `ChangePasswordRequestDto` (`CurrentPassword`, `NewPassword`, `VerificationCode?` con `[Required]` y `[MaxLength]`) y `ChangePasswordResponseDto` (`Success`, `Message`).
  - Agregar `[MaxLength]` en `LoginRequestDto` si aplica y asegurar compatibilidad de `BackendAccessDto` / `PrivilegeAccess` con los 7 verbos.
- [x] **T1.4 — Crear DTOs internos de protocolo MicroID en `Vogel.Server/Models/MicroId/`**
  - `MicroIdEnvelope.cs` (`MicroIdRequestMessage<T>`, `MicroIdResponseMessage<T>`, `MicroIdResponseCodes`).
  - `MicroIdCredsModels.cs`, `MicroIdAuthModels.cs` (`PrivilegeNodeDto` con los 7 verbos), `MicroIdUserModels.cs` (`UserDetailResponse`, `MicroIdChangePasswordRequest/Response`).

---

## Fase 2: Cliente HTTP MicroID y Gestión Automática de `X-System-Key`

- [x] **T2.1 — Implementar `IMicroIdSystemKeyProvider` / `MicroIdSystemKeyProvider` (Singleton)**
  - Caché *thread-safe* en memoria (`SemaphoreSlim`) para `PlainApiKey` y `DateExpiration`, con método `InvalidateKey()`.
- [x] **T2.2 — Implementar consumo de `POST /creds/create`**
  - Enviar `X-Request-Id` (UUID v4), `X-Timestamp` (UTC ISO 8601 `"O"`) y `{ "data": { "id_system": 1, "renovation": "MONTHLY" } }`.
- [x] **T2.3 — Implementar `IMicroIdApiClient` / `MicroIdApiClient` con `IHttpClientFactory`**
  - Inyectar `X-Request-Id`, `X-Timestamp`, `X-System-Id`, `X-System-Key` y `Authorization: Bearer <token>` opcional.
- [x] **T2.4 — Implementar reintento único automático ante `ERR_INVALID_SYSTEM_KEY`**
  - Invalidar llave en caché, solicitar nueva vía `/creds/create` y reintentar la petición una vez.
- [x] **T2.5 — Registrar opciones y clientes HTTP en `Vogel.Server/Program.cs`**
  - Registrar `MicroIdOptions` (`ValidateDataAnnotations().ValidateOnStart()`), `IMicroIdSystemKeyProvider` e `IMicroIdApiClient`.

---

## Fase 3: Integración BFF en `AuthController` y `MicroIdAuthService`

- [ ] **T3.1 — Implementar `IMicroIdAuthService` / `MicroIdAuthService` en `Vogel.Server/Services/`**
  - `LoginAsync`: consumir `POST /login` y `POST /user/get`, mapear `PrivilegeNodeDto` -> `Vogel.Shared.Models.Privilege` (jerarquía completa + 7 verbos de `PrivilegeAccess`) y `User` (`TypeAuthentication`, `AllowPasswordChange`).
  - Manejar caso de usuario sin privilegios si aplica (`ERR_EMPTY_EFFECTIVE_LIST` o lista vacía según política de `AuthController`).
- [ ] **T3.2 — Integrar `AuthProvider = "MicroID"` en `AuthController.Login`**
  - Emitir `X-Session-Token` y `Authorization: Bearer <token>` en la respuesta y retornar `LoginResponseDto` con `User`.
- [ ] **T3.3 — Integrar `AuthProvider = "MicroID"` en `AuthController.Privileges` (`GET /api/auth/privileges`)**
  - Extraer `username` del JWT (claims `preferred_username` / `unique_name` / `sub`) o parámetro, invocar `POST /auth` en MicroID y retornar `BackendPrivilegesResponseDto` con privilegios vigentes.
- [ ] **T3.4 — Implementar `POST /api/auth/change-password` en `AuthController` y `MicroIdAuthService`**
  - Resolver `id_user` del usuario autenticado vía `POST /user/get` usando su `Bearer` token (anti-BOLA/IDOR) e invocar `POST /user/changepw`, propagando errores de política (`ERR_PASSWORD_POLICY` -> `400`).
- [ ] **T3.5 — Verificar convivencia con `AuthProvider = "Mock"` y `"Api"`**
  - Asegurar que `IMockDataService` (`Mock`) e `IBackendApiService` (`Api`, `BlockWorkController`) sigan funcionando sin regresiones.

---

## Fase 4: Integración en `Vogel.Client`

- [ ] **T4.1 — Extender `IAuthenticationService` / `AuthenticationService` con `ChangePasswordAsync`**
  - Recuperar token cifrado desde `ISecureSessionStorageService` (`BackendTokenKey`), enviar `POST api/auth/change-password` con `Authorization: Bearer <token>` y manejar respuesta/errores.
- [ ] **T4.2 — Conectar `Vogel.Client/Pages/Auth/ChangePasswordView.razor.cs` con el BFF real**
  - Reemplazar la simulación `await Task.Delay(500)` por la llamada real a `AuthenticationService.ChangePasswordAsync`, mostrando errores de política (`400`) o éxito.
- [ ] **T4.3 — Verificar compatibilidad de privilegios y sesión en `Vogel.Client`**
  - Confirmar que `SessionState`, `MainMenu`, `SecondaryMenu`, `OptionMenu` y `SuboptionMenu` rendericen correctamente la jerarquía `Privilege` (`IdParent`, `Id`, `Codename`, `Group`, `Title`, `Action`, `Icon`, `Access`) proveniente de MicroID.
- [ ] **T4.4 — Verificar limpieza de sesión y manejo de `401 Unauthorized` en cliente**
  - Confirmar que `LogoutAsync` limpie `SecureSessionStorageService`, `SessionState` y llame a `POST api/auth/logout`.

---

## Fase 5: Hardening de Seguridad del Servicio Vogel y Auditoría InfoSec

- [ ] **T5.1 — Añadir `Cache-Control: no-store, no-cache` y `Pragma: no-cache` en `/api/auth/*`**
  - Reforzar `SecurityHeadersMiddleware` (o middleware en `Program.cs`) y revisar `SecurityHeaders` en `appsettings.json`.
- [ ] **T5.2 — Implementar Rate Limiting (`AuthStrict`) en `/api/auth/login` y `/api/auth/change-password`**
  - Configurar `AddRateLimiter` por IP en `Program.cs` devolviendo `429 Too Many Requests` en JSON estándar.
- [ ] **T5.3 — Auditar protección anti-BOLA/IDOR, logs (`Serilog`) y `GlobalExceptionFilter`**
  - Verificar que no se registren contraseñas, `X-System-Key`, `plain_api_key` ni JWT en `Serilog` (consola/archivo), sanitizar `\r`/`\n` en `username`, y validar que `GlobalExceptionFilter` no filtre detalles internos en producción.
- [ ] **T5.4 — Auditar validación de DTOs y superficie de controladores en `Vogel.Server`**
  - Verificar `[Required]` / `[MaxLength]` en DTOs de entrada y revisar controladores (`AuthController`, `BlockWorkController`, `SessionController`, `LogsController`, `ReportsController`).
- [ ] **T5.5 — Auditoría integral de `vogel-infosec-guardian` (OWASP / NIST SP 800-63B / RFC 7519)**
  - Emitir dictamen final con 0 hallazgos Críticos/Altos abiertos.

---

## Fase 6: Pruebas Funcionales End-to-End y Verificación

- [ ] **T6.1 — Compilación completa de la solución (`dotnet build Vogel.slnx`)**
  - Verificar 0 errores en `Vogel.Shared`, `Vogel.Client` y `Vogel.Server`.
- [ ] **T6.2 — Prueba de obtención automática de `X-System-Key` contra `http://localhost:8021/creds/create`**
  - Confirmar obtención de `plain_api_key` con `id_system = 1` y `renovation = "MONTHLY"`.
- [ ] **T6.3 — Prueba de autenticación inicial con `carlos.gomez` / `UserPass2026*`**
  - Verificar `POST /api/auth/login` -> `200 OK`, `X-Session-Token` emitido, `User` poblado y login fallido -> `401`.
- [ ] **T6.4 — Prueba de `GET /api/auth/privileges` (`/auth`) y `POST /api/auth/change-password` (`/user/changepw`)**
  - Confirmar validación con token válido, rechazo `401` con token inválido y validación de política (`400`) en cambio de clave.
- [ ] **T6.5 — Prueba de alternancia `AuthProvider = "Mock"`, Rate Limiting (`429`) y cabeceras HTTP**
  - Verificar operación en modo `"Mock"`, cabeceras de seguridad y límite de tasa en login.

---

## Registro de Seguimiento / Notas de Ejecución

| Fecha | Tarea | Responsable | Resultado / Observaciones |
|---|---|---|---|
| 2026-11-22 | Reevaluación sobre fuentes `aaf0722` | Agente | Requerimiento y plan actualizados a la arquitectura real de Vogel (`Blazor Web App` + `Vogel.Server` BFF). |
