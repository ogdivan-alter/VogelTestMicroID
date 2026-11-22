# Documento de Requerimiento Formalizado: Integración de Backend Vogel con MicroID

**Proyecto:** `VogelTestMicroID` (.NET 10 Blazor Web App — Interactive WebAssembly + BFF)  
**Servicio Externo:** MicroID (`http://localhost:8021`)  
**Fecha:** 2026-11-22  
**Estado:** Reevaluado sobre fuentes base (`aaf0722`) — Listo para Implementación  

---

## 1. Propósito y Contexto

Integrar el BFF de **Vogel** (`Vogel.Server`) con el microservicio de identidad, autenticación y autorización **MicroID** publicado en `http://localhost:8021`, conservando la arquitectura existente de **Vogel** (`AuthController`, `IBackendApiService`, `IMockDataService`, `SecurityHeadersMiddleware`, `GlobalExceptionFilter`, `CookiePolicyOptions`, `Serilog` y `SecureSessionStorageService` en `Vogel.Client`).

Como prueba inicial de validación funcional contra el entorno real, se verificará la autenticación del usuario **`carlos.gomez`** con la contraseña **`UserPass2026*`**.

---

## 2. Alcance de la Integración

### 2.1 Incluido en el alcance
1. **Autenticación (`POST /login`):** `Vogel.Server/Controllers/AuthController` (`POST /api/auth/login`) delegará credenciales a MicroID `POST /login` cuando `AuthProvider == "MicroID"`, obteniendo JWT (`token`), `refresh_token` y la lista jerárquica de privilegios consolidados.
2. **Enriquecimiento de perfil (`POST /user/get`):** Tras un login exitoso en MicroID, consultar `POST /user/get` para poblar `Name`, `Avatar`, `TypeAuthentication` (`sysauth == "self"` -> `"INTERNA"`, otros -> `"EXTERNA"`) y `AllowPasswordChange`, con fallback seguro si no está disponible.
3. **Autorización y validación en tiempo real (`POST /auth`):** Soportar `GET /api/auth/privileges` (y/o `POST /api/auth/validate`) en `AuthController` consultando `POST /auth` en MicroID con el `Authorization: Bearer <token>` vigente para refrescar los privilegios efectivos del usuario.
4. **Gestión de cambio/recuperación de contraseña (`POST /user/changepw`):** Exponer `POST /api/auth/change-password` en `AuthController` consumiendo `POST /user/changepw` de MicroID, y reemplazar la simulación actual en `Vogel.Client/Pages/Auth/ChangePasswordView.razor.cs` conectándola a `IAuthenticationService.ChangePasswordAsync`.
5. **Gestión automática de `X-System-Key` (`POST /creds/create`):** Proveedor Singleton *thread-safe* en memoria en `Vogel.Server` que obtenga `data.plain_api_key` para `X-System-Id: "MICROID"` (`id_system = 1`, `renovation = "MONTHLY"`), con renovación automática ante ausencia, expiración o error `ERR_INVALID_SYSTEM_KEY`.
6. **Mapeo jerárquico 1:1 de privilegios:** Mapear `PrivilegeNodeDto` de MicroID directamente al modelo `Vogel.Shared.Models.Privilege` (`Id`, `IdParent`, `Codename`, `Group`, `Title`, `Action`, `Icon`, `Order`) y los 7 verbos booleanos de `PrivilegeAccess` (`Read`, `Modify`, `Create`, `Delete`, `Approve`, `Validate`, `Execute`).
7. **Proveedores configurables (`AuthProvider`):** Mantener `"MicroID"` como proveedor por defecto y conservar `"Mock"` (`IMockDataService`) y `"Api"` (`IBackendApiService`) como opciones seleccionables por configuración.
8. **Hardening de seguridad del servicio Vogel:** Reforzar la postura actual (`SecurityHeadersMiddleware`, `GlobalExceptionFilter`, `CookiePolicyOptions`, `Serilog`) incorporando `Cache-Control: no-store` en `/api/auth/*`, *Rate Limiting* en autenticación y cambio de clave, validación anti-BOLA/IDOR, sanitización de logs y validación estricta de DTOs.

### 2.2 Fuera de alcance
- Modificación de cualquier archivo, configuración, base de datos o código fuente dentro de `F:\LABORATORIO\2026\MICROID`.
- Alteración del comportamiento de `BlockWorkController` contra `ApiBackend` cuando se usa el backend de negocio.

---

## 3. Arquitectura y Flujo BFF

```text
+----------------+       HTTPS/JSON        +----------------+       HTTP POST        +-----------------------+
|  Vogel.Client  | ----------------------> |  Vogel.Server  | ---------------------> |       MicroID         |
|  (Blazor WASM) | <---------------------- |     (BFF)      | <--------------------- | http://localhost:8021 |
+----------------+  LoginResponseDto /     +----------------+  ResponseMessage<T>    +-----------------------+
                    X-Session-Token                |
                                                   +--> Caché en memoria: X-System-Key (via /creds/create)
```

### Principios de seguridad del BFF
- `Vogel.Client` **nunca** conoce `X-System-Key` ni invoca directamente `/creds/create` (protegido en MicroID por lista blanca de IPs).
- `Vogel.Server` inyecta todas las cabeceras técnicas exigidas por `RequestValidatorMiddleware` de MicroID.
- El `token` (JWT) emitido por MicroID se entrega al cliente vía cabeceras `X-Session-Token` / `Authorization: Bearer` (como espera `AuthenticationService.cs`) y se persiste cifrado en `SecureSessionStorageService`.

---

## 4. Contrato Técnico de Comunicación con MicroID

### 4.1 Reglas generales de HTTP y cabeceras
- **Método HTTP:** Exclusivamente `POST` para todos los endpoints de MicroID.
- **Content-Type:** `application/json`.
- **Cabeceras obligatorias en todas las peticiones:**
  - `X-Request-Id`: UUID v4 único por petición (`Guid.NewGuid().ToString()`).
  - `X-Timestamp`: Fecha/hora UTC en formato ISO 8601 (`DateTimeOffset.UtcNow.ToString("O")`), dentro de la ventana anti-replay (±5 minutos).
- **Cabeceras de sistema (obligatorias en todos los endpoints excepto `/creds/*`):**
  - `X-System-Id`: `"MICROID"` (configurable en `MicroId:SystemId`).
  - `X-System-Key`: Llave en texto plano obtenida de `POST /creds/create` (`data.plain_api_key`).
- **Cabecera de usuario (obligatoria en `/auth`, `/user/get`, `/user/changepw` sin `verification_code`):**
  - `Authorization`: `Bearer <jwt_token>`.

### 4.2 Envoltorio estándar de entrada y salida
- **Request:** `{ "data": { ... } }`
- **Response:** `{ "code": "SUCCESS", "message": "...", "data": { ... } }`

### 4.3 Endpoints de MicroID a consumir

| Operación | Endpoint MicroID | Requiere `X-System-Key` | Requiere `Bearer` | Notas de contrato |
|---|---|---|---|---|
| Obtener llave de sistema | `POST /creds/create` | No (IP whitelist) | No | `{ "id_system": 1, "renovation": "MONTHLY" }` (`"MONTHLY"`, `"ANNUAL"` o `"TEMPORARY"`) |
| Autenticación | `POST /login` | Sí | No | `{ "username": "...", "credential": { "secret": "..." } }` |
| Validación tiempo real | `POST /auth` | Sí | Sí | `{ "username": "..." }` |
| Consulta de usuario | `POST /user/get` | Sí | Sí | `{ "username": "..." }` |
| Cambio/recuperación clave | `POST /user/changepw` | Sí | Sí (salvo con `verification_code`) | `{ "id_user": 0, "current_password": "...", "verification_code": "...", "new_password": "..." }` |

---

## 5. Requerimientos Funcionales (RF)

| ID | Requerimiento | Descripción |
|---|---|---|
| **RF-01** | Gestión automática de `X-System-Key` | `Vogel.Server` debe implementar `IMicroIdSystemKeyProvider` (Singleton, `SemaphoreSlim`) que invoque `POST /creds/create` (`id_system = 1`, `renovation = "MONTHLY"`) y almacene `plain_api_key` y `date_expiration` solo en memoria, renovando automáticamente si expira o falla con `ERR_INVALID_SYSTEM_KEY`. |
| **RF-02** | Autenticación vía MicroID | `AuthController.Login` (`POST /api/auth/login`), cuando `AuthProvider == "MicroID"`, debe autenticar contra `POST /login` en MicroID, emitir `X-Session-Token` / `Authorization: Bearer` y retornar `LoginResponseDto` con la entidad `User` completa. |
| **RF-03** | Enriquecimiento de perfil | Tras `POST /login`, consultar `POST /user/get` con el JWT obtenido para poblar `Name`, `Avatar`, `TypeAuthentication` (`sysauth == "self" ? "INTERNA" : "EXTERNA"`) y `AllowPasswordChange`, con fallback seguro si falla. |
| **RF-04** | Mapeo jerárquico 1:1 de privilegios | Mapear `PrivilegeNodeDto` -> `Vogel.Shared.Models.Privilege` (`Id`, `IdParent`, `Codename`, `Group`, `Title`, `Action`, `Icon`, `Order`) incluyendo los 7 verbos booleanos de `PrivilegeAccess` (`Read`, `Modify`, `Create`, `Delete`, `Approve`, `Validate`, `Execute`). |
| **RF-05** | Autorización en tiempo real (`/auth`) | `AuthController.Privileges` (`GET /api/auth/privileges`, y opcionalmente `POST /api/auth/validate`) en modo `"MicroID"` debe extraer el token Bearer y el `username` (desde claims del JWT o parámetro) y consultar `POST /auth` en MicroID retornando `BackendPrivilegesResponseDto` / privilegios vigentes. |
| **RF-06** | Cambio de contraseña real | Exponer `POST /api/auth/change-password` en `AuthController` consumiendo `POST /user/changepw` (resolviendo `id_user` vía `/user/get` con el `Bearer` token del usuario autenticado) y conectar `Vogel.Client/Pages/Auth/ChangePasswordView.razor.cs` mediante `IAuthenticationService.ChangePasswordAsync`. |
| **RF-07** | Selector `AuthProvider` | Soportar `AuthProvider = "MicroID"` (por defecto), `"Mock"` (`IMockDataService`) y `"Api"` (`IBackendApiService`) desde `appsettings.json`. |

---

## 6. Requerimientos No Funcionales y Hardening de Seguridad (RNF)

| ID | Categoría | Requisito |
|---|---|---|
| **RNF-01** | Gestión de Secretos | `X-System-Key`, `plain_api_key`, contraseñas y tokens JWT nunca deben registrarse en logs (`Serilog` / `ILogger`) ni exponerse al navegador (`Vogel.Client`). |
| **RNF-02** | Resiliencia | Uso de `IHttpClientFactory` con timeout configurable, sincronización con `SemaphoreSlim` y reintento único automático si MicroID responde `ERR_INVALID_SYSTEM_KEY`. |
| **RNF-03** | Trazabilidad segura | Registrar `X-Request-Id` como correlación y sanitizar entradas (`\r`, `\n` en `username`) antes de escribir en `Serilog` (anti-Log Forging). |
| **RNF-04** | Cabeceras HTTP y Caché | Mantener `SecurityHeadersMiddleware` y añadir `Cache-Control: no-store, no-cache` y `Pragma: no-cache` en todas las respuestas de `/api/auth/*`. |
| **RNF-05** | Anti-Brute Force | Configurar `AddRateLimiter` (`AuthStrict`) sobre `POST /api/auth/login` y `POST /api/auth/change-password` retornando `429 Too Many Requests` en JSON estándar. |
| **RNF-06** | Anti-BOLA / IDOR | En cambio de contraseña autenticado, resolver `id_user` exclusivamente desde el perfil del token Bearer autenticado en MicroID (nunca confiar en un `id_user` arbitrario del cliente). |
| **RNF-07** | Validación y Transporte | Validar DTOs con `DataAnnotations` (`[Required]`, `[MaxLength]`) y exigir `https://` en `MicroId:BaseUrl` cuando no apunte a loopback (`localhost` / `127.0.0.1` / `::1`). |
| **RNF-08** | Superficie y Excepciones | Mantener `GlobalExceptionFilter` evitando fuga de información sensible en producción y auditar controladores/endpoints de `Vogel.Server`. |

---

## 7. Mapeo de Códigos de Error de MicroID

| Código MicroID | Significado | Respuesta BFF hacia `Vogel.Client` |
|---|---|---|
| `SUCCESS` | Operación exitosa | HTTP 200 con `Success = true` y datos mapeados. |
| `ERR_INVALID_SYSTEM_KEY` | Llave de sistema inválida o expirada | Invalidar caché en memoria, regenerar vía `/creds/create` y reintentar 1 vez. |
| `ERR_AUTH_FAILED` | Credenciales inválidas o token expirado/revocado | HTTP 401 (`Unauthorized`) con mensaje neutro `"Usuario o Clave incorrectos"` (en login) o sesión inválida. |
| `ERR_INACTIVE_MASTER` / `ERR_SYSTEM_MISMATCH` | Usuario/sistema inactivo o no vinculado | HTTP 401/403 con mensaje controlado. |
| `ERR_EMPTY_EFFECTIVE_LIST` | Usuario sin roles/privilegios activos | HTTP 400/403 informando ausencia de privilegios en el sistema. |
| `ERR_PASSWORD_POLICY` | Nueva contraseña incumple política | HTTP 400 con detalle de política devuelto por MicroID. |
| `ERR_IDP_COMMUNICATION` / `ERR_INTERNAL_SERVER` | Falla en Keycloak o MicroID | HTTP 502/503 con mensaje genérico y log de correlación. |

---

## 8. Criterios de Aceptación (CA)

- **CA-01:** Con `MicroID` activo en `http://localhost:8021` y `AuthProvider = "MicroID"`, `Vogel.Server` obtiene automáticamente `X-System-Key` vía `POST /creds/create` (`renovation = "MONTHLY"`).
- **CA-02:** Al iniciar sesión en `Vogel.Client` con `carlos.gomez` / `UserPass2026*`, `Vogel.Server` autentica contra `POST /login`, enriquece perfil vía `POST /user/get`, mapea privilegios jerárquicos y permite el acceso en `Vogel.Client`.
- **CA-03:** Con credenciales inválidas para `carlos.gomez`, `Vogel.Server` retorna HTTP 401 y `Vogel.Client` muestra el mensaje de error sin autenticar.
- **CA-04:** `GET /api/auth/privileges` (y validación de sesión) consulta `POST /auth` en MicroID con token válido y rechaza con `401` ante token inválido.
- **CA-05:** El cambio de contraseña desde `ChangePasswordView` (`POST /api/auth/change-password` -> `POST /user/changepw`) valida política (`ERR_PASSWORD_POLICY` -> `400`) y actualiza credenciales reales en MicroID.
- **CA-06:** Con `AuthProvider = "Mock"`, la aplicación autentica usando `IMockDataService` sin depender de `http://localhost:8021`.
