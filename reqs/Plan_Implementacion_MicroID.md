# Plan de Implementación y Seguimiento: Integración Vogel - MicroID

**Documento de Referencia:** [Requerimiento_Integracion_MicroID.md](./Requerimiento_Integracion_MicroID.md)  
**Servicio Target:** `http://localhost:8021` (`X-System-Id: "MICROID"`)  
**Usuario de Prueba Inicial:** `carlos.gomez` / `UserPass2026*`  

---

## Tablero de Seguimiento General

| Fase | Descripción | Estado | Avance |
|---|---|---|---|
| Fase 1 | Configuración y Contratos Compartidos (`Vogel.Shared` & `appsettings`) | Completada | 4 / 4 |
| Fase 2 | Cliente HTTP MicroID y Gestión Automática de `X-System-Key` (`Vogel.Server`) | Completada | 5 / 5 |
| Fase 3 | Servicio de Autenticación, Autorización y Cambio de Clave (`Vogel.Server`) | Completada | 5 / 5 |
| Fase 4 | Integración en `Vogel.Client` (Tokens, Validación y Cambio de Clave) | Completada | 4 / 4 |
| Fase 5 | Hardening de Seguridad del Servicio Vogel y Auditoría InfoSec | Completada | 5 / 5 |
| Fase 6 | Pruebas Funcionales End-to-End (`carlos.gomez`) y Verificación | Pendiente | 0 / 5 |

---

## Fase 1: Configuración y Contratos Compartidos

- [x] **T1.1 — Configurar opciones de MicroID y proveedor de autenticación en `Vogel.Server/appsettings.json`**
  - Agregar sección `MicroId`: `BaseUrl` (`http://localhost:8021`), `SystemId` (`"MICROID"`), `SystemNumericId` (`1`), `TimeoutSeconds` (`30`), `CredRenovation` (`"NONE"`).
  - Agregar `AuthSettings:Provider` (`"MicroID"` por defecto; `"Mock"` como alternativa).
- [x] **T1.2 — Crear clase de opciones tipadas `MicroIdOptions` en `Vogel.Server`**
  - Validar propiedades requeridas al arranque o resolución de servicios.
- [x] **T1.3 — Extender DTOs en `Vogel.Shared`**
  - Actualizar `LoginResponse` con propiedades opcionales `RefreshToken` (`string?`) y `UserId` (`int?`).
  - Crear `ChangePasswordRequestDto`, `ChangePasswordResponseDto` y `ValidateAuthResponseDto` en `Vogel.Shared/DTOs/`.
- [x] **T1.4 — Crear DTOs internos de protocolo MicroID en `Vogel.Server`**
  - `MicroIdRequestMessage<T>` (`data`), `MicroIdResponseMessage<T>` (`code`, `message`, `data`).
  - DTOs espejo de MicroID: `CreateCredRequest/Response`, `MicroIdLoginRequest/Response`, `PrivilegeNodeDto`, `MicroIdPrivilegeAccess`, `AuthValidateRequest/Response`, `GetUserRequest/UserDetailResponse`, `MicroIdChangePasswordRequest/Response`.

---

## Fase 2: Cliente HTTP MicroID y Gestión Automática de `X-System-Key`

- [x] **T2.1 — Implementar `IMicroIdSystemKeyProvider` / `MicroIdSystemKeyProvider` (Singleton)**
  - Mantener en memoria `PlainApiKey` y `DateExpiration`.
  - Sincronizar con `SemaphoreSlim` para evitar múltiples llamadas concurrentes a `/creds/create`.
  - Exponer `GetSystemKeyAsync(CancellationToken)` e `InvalidateKey()` (para renovación ante `ERR_INVALID_SYSTEM_KEY`).
- [x] **T2.2 — Implementar consumo de `POST /creds/create` en el proveedor de llave**
  - Enviar cabeceras `X-Request-Id` (UUID v4) y `X-Timestamp` (UTC ISO 8601).
  - Enviar `{ "data": { "id_system": 1, "renovation": "NONE" } }` (usando `SystemNumericId`).
  - Extraer `data.plain_api_key` y `data.date_expiration`.
- [x] **T2.3 — Implementar `IMicroIdApiClient` / `MicroIdApiClient` con `HttpClient` tipado**
  - Configurar `BaseAddress = http://localhost:8021` vía `AddHttpClient`.
  - Inyectar automáticamente `X-Request-Id`, `X-Timestamp`, `X-System-Id: "MICROID"`, `X-System-Key` y opcionalmente `Authorization: Bearer <token>`.
- [x] **T2.4 — Implementar política de reintento único ante `ERR_INVALID_SYSTEM_KEY`**
  - Si MicroID retorna código `ERR_INVALID_SYSTEM_KEY`, invalidar caché, solicitar nueva llave vía `/creds/create` y reintentar la petición una vez.
- [x] **T2.5 — Registrar servicios y `HttpClient` en `Vogel.Server/Program.cs`**
  - Registrar `MicroIdOptions`, `IMicroIdSystemKeyProvider` y `IMicroIdApiClient`.

---

## Fase 3: Servicio de Autenticación, Autorización y Cambio de Clave (`Vogel.Server`)

- [x] **T3.1 — Implementar `MicroIdAuthBackendService : IAuthBackendService`**
  - `AuthenticateAsync(LoginRequest)`: consumir `POST /login` en MicroID con `username` y `credential.secret`.
  - Mapear `PrivilegeNodeDto` -> `Vogel.Shared.Models.Privilege` (`Name = codename`, `Access = 7 verbos`).
  - Consultar `POST /user/get` con el JWT obtenido para poblar `UserId`, `AuthType` (`sysauth == "self" ? "INTERNA" : "EXTERNA"`) y `CanChangePassword` (con fallback seguro si no está disponible).
- [x] **T3.2 — Implementar selección dinámica de proveedor (`MicroID` vs `Mock`)**
  - En `Program.cs` (o factory/wrapper de `IAuthBackendService`), resolver `MicroIdAuthBackendService` cuando `AuthSettings:Provider == "MicroID"` y `AuthBackendService` (Mock) cuando sea `"Mock"`.
- [x] **T3.3 — Implementar validación en tiempo real en `IAuthBackendService` y `AuthController`**
  - Agregar método `ValidateAuthAsync(string username, string bearerToken)` consumiendo `POST /auth` en MicroID.
  - Exponer `POST /api/auth/validate` en `AuthController` recibiendo el token del cliente.
- [x] **T3.4 — Implementar cambio/recuperación de contraseña en `IAuthBackendService` y `AuthController`**
  - Agregar método `ChangePasswordAsync(ChangePasswordRequestDto, string? bearerToken)` consumiendo `POST /user/changepw` en MicroID.
  - Exponer `POST /api/auth/change-password` en `AuthController` propagando errores de negocio (p. ej. `ERR_PASSWORD_POLICY`).
- [x] **T3.5 — Mapear respuestas de error de MicroID a HTTP Status y mensajes amigables**
  - Manejar `ERR_AUTH_FAILED`, `ERR_INACTIVE_MASTER`, `ERR_EMPTY_EFFECTIVE_LIST`, `ERR_PASSWORD_POLICY`, `ERR_IDP_COMMUNICATION` y fallos de conectividad.

---

## Fase 4: Integración en `Vogel.Client`

- [x] **T4.1 — Actualizar `CustomAuthStateProvider` para almacenar y exponer `Token` y `RefreshToken`**
  - Asegurar que el JWT de MicroID quede disponible en los claims o almacenamiento de sesión para llamadas subsecuentes a `/api/auth/validate` y `/api/auth/change-password`.
- [x] **T4.2 — Extender `IAuthService` / `AuthService` en `Vogel.Client`**
  - Agregar `ValidateSessionAsync()` -> `POST api/auth/validate` (actualizando `PrivilegeService` con los privilegios vigentes o cerrando sesión si el token expiró/fue revocado).
  - Agregar `ChangePasswordAsync(ChangePasswordRequestDto)` -> `POST api/auth/change-password`.
- [x] **T4.3 — Verificar compatibilidad de privilegios en `PrivilegeService` y páginas de Vogel**
  - Confirmar cómo se comportan `NavMenu`, `MainMenu`, `SecondaryMenu` y `WorkView` frente a los `codename` devueltos por MicroID para `carlos.gomez`.
- [x] **T4.4 — Verificar flujo de cambio de clave en UI (si aplica en `Header` / componente de perfil)**
  - Conectar acción de cambio de contraseña con `AuthService.ChangePasswordAsync` respetando `CanChangePassword`.

---

## Fase 5: Hardening de Seguridad del Servicio Vogel y Auditoría InfoSec

- [x] **T5.1 — Restringir política CORS en `Vogel.Server/Program.cs`**
  - Reemplazar `AllowAnyOrigin()` (`AllowAll`) por orígenes explícitos configurados en `appsettings.json`.
- [x] **T5.2 — Agregar middleware de cabeceras de seguridad HTTP y `Cache-Control: no-store` en `/api/auth/*`**
  - Configurar `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer` y `no-store` en respuestas sensibles.
- [x] **T5.3 — Configurar Rate Limiting en endpoints de autenticación (`/api/auth/login`, `/api/auth/change-password`)**
  - Proteger contra fuerza bruta y credential stuffing devolviendo `429 Too Many Requests` al superar el umbral.
- [x] **T5.4 — Implementar manejador global de excepciones y revisar superficie de controladores en `Vogel.Server`**
  - Garantizar cero fuga de stack traces/detalles internos y auditar endpoints expuestos (p. ej. controladores plantilla/demo).
- [x] **T5.5 — Auditoría integral de `vogel-infosec-guardian` (OWASP / NIST SP 800-63B / RFC 7519)**
  - Verificar ausencia de secretos en logs/cliente, manejo seguro de JWT, validación de entradas y emitir dictamen de aprobación.

---

## Fase 6: Pruebas Funcionales End-to-End y Verificación

- [x] **T6.1 — Compilación completa de la solución (`dotnet build Vogel.slnx`)**
  - Verificar cero errores de compilación en `Vogel.Shared`, `Vogel.Server` y `Vogel.Client`.
- [x] **T6.2 — Prueba de obtención automática de `X-System-Key` contra `http://localhost:8021/creds/create`**
  - Confirmar que `Vogel.Server` obtiene `plain_api_key` sin errores de IP ni formato de cabeceras.
- [x] **T6.3 — Prueba de autenticación inicial con `carlos.gomez` / `UserPass2026*`**
  - Verificar `POST /api/auth/login` -> `POST http://localhost:8021/login`, respuesta `200 OK`, JWT recibido y privilegios mapeados.
  - Verificar login fallido con clave incorrecta -> `401 Unauthorized`.
- [x] **T6.4 — Prueba de validación en tiempo real (`/api/auth/validate` -> `/auth`) y cambio de contraseña (`/api/auth/change-password` -> `/user/changepw`)**
  - Confirmar respuesta exitosa con token válido y rechazo con token inválido.
- [x] **T6.5 — Prueba de alternancia `AuthSettings:Provider = "Mock"` y verificación de controles de seguridad**
  - Confirmar que el modo Mock sigue operativo y que las cabeceras de seguridad / rate limiting responden según lo esperado.

---

## Registro de Seguimiento / Notas de Ejecución

| Fecha | Tarea | Responsable | Resultado / Observaciones |
|---|---|---|---|
| 2026-11-21 | Definición de requerimiento y plan | Agente / Usuario | Documentos creados en `reqs/` tras levantamiento de requisitos. |
| 2026-11-21 | Fases 1, 2 y 3 (Backend BFF .NET) | Especialista Backend BFF | Implementados contratos en `Vogel.Shared`, `MicroIdOptions`, `MicroIdSystemKeyProvider`, `MicroIdApiClient`, `MicroIdAuthBackendService`, `AuthController` y registro dinámico en `Program.cs`. `dotnet build Vogel.slnx` exitoso (0 errores). |
| 2026-11-21 | Fase 4 (Frontend Blazor WASM) | Especialista Frontend Blazor WASM | Actualizado `CustomAuthStateProvider` (`Token`, `RefreshToken`, `UserId`, `UpdatePrivilegesAsync`), extendido `IAuthService`/`AuthService` (`ValidateSessionAsync`, `ChangePasswordAsync`), verificados privilegios en `MainMenu`/`SecondaryMenu`/`NavMenu`/`WorkView`, y creada página `ChangePassword` (`/cambiar-clave`, `/change-password`) conectada desde `Header`. `dotnet build Vogel.slnx` exitoso (0 errores). |
| 2026-11-21 | T5.5 (Auditoría integral InfoSec) | `vogel-infosec-guardian` | **APROBADO** (0 hallazgos Críticos, 0 Altos). Verificadas remediaciones T5.1–T5.4 (CORS `VogelCors`, Rate Limiting `AuthStrict`, anti-IDOR en `ChangePasswordAsync`, cabeceras HTTP + `no-store`, `GlobalExceptionHandler`, DataAnnotations en DTOs, validación HTTPS en `MicroIdOptions`, sanitización de logs y eliminación de `WeatherForecast`). Auditado `Vogel.Client` sin exposición de `X-System-Key`, puerto `8021` ni XSS. `F:\LABORATORIO\2026\MICROID` intacto. `dotnet build Vogel.slnx` exitoso (0 errores; 2 warnings `NU1903` preexistentes en `Microsoft.AspNetCore.Components.WebAssembly.Server` 10.0.12 recomendados para actualización futura). |
| 2026-11-21 | Fase 6 (T6.1–T6.5 E2E QA) | Especialista QA E2E | **APROBADO** (`CA-01`–`CA-06`). Hallazgo corregido en E2E: `MicroId:CredRenovation` pasó de `"NONE"` a `"MONTHLY"` en `Vogel.Server/appsettings.json` y `MicroIdOptions.cs` (MicroID `/creds/create` exige `MONTHLY`/`ANNUAL`/`TEMPORARY`). `dotnet build Vogel.slnx`: 0 errores. `carlos.gomez` / `UserPass2026*`: `200 OK`, JWT (1311 chars), `authType: INTERNA`, `canChangePassword: true`, `privileges: []`; clave incorrecta: `401 Unauthorized` (`"Usuario o Clave incorrectos"`). `/api/auth/validate`: `200 OK` con token válido, `401 Unauthorized` con token inválido. `/api/auth/change-password` con clave débil `"123"`: `400 Bad Request` (`ERR_PASSWORD_POLICY`). Cabeceras (`X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Cache-Control: no-store, no-cache`) y Rate Limiting `AuthStrict` (`429 Too Many Requests` tras 15 req/min) verificados. Alternancia `--AuthSettings:Provider=Mock` (`admin`/`admin123`): `200 OK` (9 privilegios). Procesos `Vogel.Server` detenidos; `MICROID` intacto. |
