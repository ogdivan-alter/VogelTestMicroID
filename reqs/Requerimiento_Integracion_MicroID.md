# Documento de Requerimiento Formalizado: Integración de Backend Vogel con MicroID

**Proyecto:** `VogelTestMicroID`  
**Servicio Externo:** MicroID (`http://localhost:8021`)  
**Fecha:** 2026-11-21  
**Estado:** Aprobado para Implementación  

---

## 1. Propósito y Contexto

Integrar el backend de la aplicación **Vogel** (`Vogel.Server`, arquitectura Blazor WebAssembly Hosted) con el microservicio de identidad, autenticación y autorización **MicroID** publicado en `http://localhost:8021`, manteniendo `Vogel.Server` como capa **BFF (Backend-for-Frontend)** para proteger credenciales de sistema, encabezados de seguridad y comunicación con la API de MicroID.

Como prueba inicial de validación funcional contra el entorno real, se verificará la autenticación del usuario **`carlos.gomez`** con la contraseña **`UserPass2026*`**.

---

## 2. Alcance de la Integración

### 2.1 Incluido en el alcance
1. **Autenticación (`POST /login`):** Inicio de sesión delegando credenciales a MicroID y obteniendo JWT (`token`), `refresh_token` y la matriz de privilegios consolidados.
2. **Autorización y validación en tiempo real (`POST /auth`):** Verificación de vigencia del token Bearer y obtención de privilegios actualizados desde MicroID.
3. **Gestión de credenciales de usuario (`POST /user/changepw` y consulta de perfil `POST /user/get`):** Soporte para cambio/recuperación de contraseña delegado al IdP vía MicroID y determinación de `AuthType` / `CanChangePassword` según el campo `sysauth` del usuario.
4. **Gestión automática de `X-System-Key` (`POST /creds/create`):** Obtención y almacenamiento en memoria en `Vogel.Server` de la llave de sistema (`data.plain_api_key`) para el sistema `MICROID` (`id_system = 1` por defecto), con renovación automática ante ausencia, expiración o error `ERR_INVALID_SYSTEM_KEY`.
5. **Mapeo de privilegios:** Conversión de `PrivilegeNodeDto.codename` de MicroID hacia `Privilege.Name` en Vogel, conservando los 7 verbos booleanos de acceso (`create`, `modify`, `read`, `delete`, `validate`, `execute`, `approve`).
6. **Proveedor configurable (`MicroID` vs `Mock`):** Interruptor en configuración (`AuthSettings:Provider`) con `"MicroID"` como valor por defecto y `"Mock"` disponible como respaldo para pruebas aisladas sin dependencias externas.

### 2.2 Fuera de alcance
- Modificación de cualquier archivo, configuración, base de datos o código fuente dentro de `F:\LABORATORIO\2026\MICROID`.
- Construcción dinámica de árboles de menú en `Vogel.Client` a partir de `id_parent` / `group_name` / `icon` (se mantiene el menú actual validado por `Privilege.Name`).
- Administración CRUD de usuarios, roles, sistemas o privilegios desde Vogel.

---

## 3. Arquitectura y Flujo BFF

```text
+----------------+       HTTPS/JSON        +----------------+       HTTP POST        +-----------------------+
|  Vogel.Client  | ----------------------> |  Vogel.Server  | ---------------------> |       MicroID         |
|  (Blazor WASM) | <---------------------- |     (BFF)      | <--------------------- | http://localhost:8021 |
+----------------+   LoginResponse / DTOs  +----------------+  ResponseMessage<T>    +-----------------------+
                                                   |
                                                   +--> Caché en memoria: X-System-Key (via /creds/create)
```

### Principios de seguridad del BFF
- `Vogel.Client` **nunca** conoce `X-System-Key` ni invoca directamente `/creds/create` (protegido en MicroID por lista blanca de IPs).
- `Vogel.Server` firma e inyecta todas las cabeceras técnicas exigidas por `RequestValidatorMiddleware` de MicroID.
- El `token` (JWT) y `refresh_token` emitidos por MicroID se retornan a `Vogel.Client` (o se gestionan en sesión autenticada) para reenviar el Bearer token en operaciones protegidas (`/auth`, `/user/changepw`).

---

## 4. Contrato Técnico de Comunicación con MicroID

### 4.1 Reglas generales de HTTP y cabeceras
Según `RequestValidatorMiddleware` de MicroID:
- **Método HTTP:** Exclusivamente `POST` para todos los endpoints.
- **Content-Type:** `application/json`.
- **Cabeceras obligatorias en todas las peticiones:**
  - `X-Request-Id`: UUID v4 único por petición (`Guid.NewGuid().ToString()`).
  - `X-Timestamp`: Fecha/hora UTC en formato ISO 8601 (`DateTimeOffset.UtcNow.ToString("O")`), dentro de la ventana anti-replay (±5 minutos).
- **Cabeceras de sistema (obligatorias en todos los endpoints excepto `/creds/*`):**
  - `X-System-Id`: `"MICROID"` (valor estático configurable).
  - `X-System-Key`: Llave en texto plano obtenida de `POST /creds/create` (`data.plain_api_key`).
- **Cabecera de usuario (obligatoria en `/auth`, `/user/get`, `/user/changepw` sin `verification_code`):**
  - `Authorization`: `Bearer <jwt_token>`.

### 4.2 Envoltorio estándar de entrada y salida
- **Request (`RequestMessage<T>`):**
  ```json
  {
    "data": { }
  }
  ```
- **Response (`ResponseMessage<T>`):**
  ```json
  {
    "code": "SUCCESS",
    "message": "Operación procesada exitosamente",
    "data": { }
  }
  ```

### 4.3 Endpoints de MicroID a consumir

| Operación | Endpoint MicroID | Requiere `X-System-Key` | Requiere `Bearer` | Payload `data` principal |
|---|---|---|---|---|
| Obtener llave de sistema | `POST /creds/create` | No (IP whitelist) | No | `{ "id_system": 1, "renovation": "NONE" }` |
| Autenticación | `POST /login` | Sí | No | `{ "username": "...", "credential": { "secret": "..." } }` |
| Validación tiempo real | `POST /auth` | Sí | Sí | `{ "username": "..." }` |
| Consulta de usuario | `POST /user/get` | Sí | Sí | `{ "username": "..." }` |
| Cambio/recuperación clave | `POST /user/changepw` | Sí | Sí (salvo con `verification_code`) | `{ "id_user": 0, "current_password": "...", "verification_code": "...", "new_password": "..." }` |

---

## 5. Requerimientos Funcionales (RF)

| ID | Requerimiento | Descripción |
|---|---|---|
| **RF-01** | Gestión automática de `X-System-Key` | `Vogel.Server` debe implementar un proveedor/caché thread-safe en memoria que, al no tener llave o al expirar/fallar con `ERR_INVALID_SYSTEM_KEY`, invoque `POST /creds/create` (con `id_system` configurado para `MICROID`) y almacene `data.plain_api_key` y su `date_expiration`. |
| **RF-02** | Autenticación vía MicroID | `POST /api/auth/login` en `Vogel.Server` debe invocar `POST /login` en MicroID enviando `username` y `credential.secret`. Si es exitoso, debe mapear `token`, `refresh_token` y `privileges` al `LoginResponse` de Vogel. |
| **RF-03** | Enriquecimiento de perfil (`AuthType` / `CanChangePassword`) | Tras un login exitoso (o bajo demanda), `Vogel.Server` debe poder consultar `POST /user/get` con el token del usuario para obtener `id_user` y `sysauth`, mapeando `sysauth == "self"` a `AuthType = "INTERNA"` y `CanChangePassword = true` (otros valores -> `EXTERNA` / `false`). Si `/user/get` falla por permisos o no aplica, usar valores por defecto seguros sin romper el login. |
| **RF-04** | Mapeo de privilegios | Cada `PrivilegeNodeDto` de MicroID debe mapearse a `Vogel.Shared.Models.Privilege` asignando `Name = node.Codename` y copiando los 7 booleanos de `Access` (`Create`, `Modify`, `Read`, `Delete`, `Validate`, `Execute`, `Approve`). |
| **RF-05** | Autorización en tiempo real (`/auth`) | Exponer en `Vogel.Server` un endpoint (p. ej. `POST /api/auth/validate`) que reenvíe el token Bearer a `POST /auth` en MicroID para validar vigencia de sesión y refrescar los privilegios efectivos en `Vogel.Client`. |
| **RF-06** | Cambio y recuperación de contraseña | Exponer en `Vogel.Server` `POST /api/auth/change-password` que consuma `POST /user/changepw` en MicroID (resolviendo `id_user` vía `/user/get` si no se envía), soportando cambio autenticado (`current_password` + `new_password`) y recuperación (`verification_code` + `new_password`), y propagando errores de política (`ERR_PASSWORD_POLICY`). |
| **RF-07** | Interruptor `MicroID` / `Mock` | Mediante `AuthSettings:Provider` (`"MicroID"` por defecto, `"Mock"` opcional), `Vogel.Server` debe seleccionar la implementación correspondiente sin requerir cambios de código para alternar. |

---

## 6. Requerimientos No Funcionales (RNF)

| ID | Categoría | Requisito |
|---|---|---|
| **RNF-01** | Seguridad | `X-System-Key` nunca debe serializarse hacia `Vogel.Client`, registrarse en logs en texto plano ni persistirse en el repositorio. |
| **RNF-02** | Resiliencia | Uso de `IHttpClientFactory` con timeout configurable, sincronización con `SemaphoreSlim` para la obtención/renovación de `X-System-Key` y reintento único automático si MicroID responde `ERR_INVALID_SYSTEM_KEY`. |
| **RNF-03** | Trazabilidad | Propagar o registrar en logs `X-Request-Id` y el `code` de respuesta de MicroID en caso de error (sin exponer secretos). |
| **RNF-04** | Compatibilidad | Mantener los contratos existentes de `Vogel.Client` (`LoginRequest`, `LoginResponse`, `PrivilegeService`, `CustomAuthStateProvider`) añadiendo solo propiedades opcionales compatibles hacia atrás (como `RefreshToken`, `UserId`). |
| **RNF-05** | Hardening CORS (`Vogel.Server`) | Reemplazar la política permisiva `AllowAll` (`AllowAnyOrigin`) por una política restrictiva basada en orígenes permitidos configurados en `appsettings.json`. |
| **RNF-06** | Cabeceras de Seguridad HTTP | Incluir middleware en `Vogel.Server` que agregue `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer` y `Cache-Control: no-store` en endpoints de `/api/auth/*`. |
| **RNF-07** | Rate Limiting / Anti-Brute Force | Aplicar limitación de peticiones (Rate Limiting) sobre `/api/auth/login` y `/api/auth/change-password` para mitigar ataques de fuerza bruta y credential stuffing. |
| **RNF-08** | Manejo Centralizado de Errores | Implementar manejo global de excepciones en `Vogel.Server` que registre el detalle internamente (con `X-Request-Id` / `TraceIdentifier`) y devuelva respuestas controladas sin stack traces al cliente. |
| **RNF-09** | Superficie de Exposición | Auditar los endpoints de `Vogel.Server` (incluyendo controladores existentes o de plantilla) para asegurar que no existan rutas desprotegidas o información innecesaria expuesta. |

---

## 7. Mapeo de Códigos de Error de MicroID

| Código MicroID | Significado | Acción en `Vogel.Server` / Respuesta a `Vogel.Client` |
|---|---|---|
| `SUCCESS` | Operación exitosa | HTTP 200 con `Success = true` y datos mapeados. |
| `ERR_INVALID_SYSTEM_KEY` | Llave de sistema inválida o expirada | Invalidar caché de `X-System-Key`, regenerar vía `/creds/create` y reintentar 1 vez. |
| `ERR_AUTH_FAILED` | Credenciales inválidas o token expirado/revocado | HTTP 401 (`Unauthorized`) con mensaje claro al usuario. |
| `ERR_INACTIVE_MASTER` / `ERR_SYSTEM_MISMATCH` | Usuario/sistema inactivo o no vinculado | HTTP 401/403 con mensaje descriptivo. |
| `ERR_EMPTY_EFFECTIVE_LIST` | Usuario sin roles/privilegios activos en el sistema | HTTP 401/403 informando ausencia de permisos. |
| `ERR_INCOMPATIBLE_SYSAUTH` | Método de autenticación incompatible | HTTP 400/401 con mensaje descriptivo. |
| `ERR_PASSWORD_POLICY` | Nueva contraseña incumple política | HTTP 400 con detalle de política devuelto por MicroID. |
| `ERR_IDP_COMMUNICATION` / `ERR_INTERNAL_SERVER` | Falla en Keycloak o servidor MicroID | HTTP 502/503 con mensaje controlado y log detallado. |

---

## 8. Criterios de Aceptación (CA)

- **CA-01:** Con `MicroID` activo en `http://localhost:8021` y `AuthSettings:Provider = "MicroID"`, al iniciar `Vogel.Server`, se obtiene automáticamente `X-System-Key` mediante `POST /creds/create` sin intervención manual.
- **CA-02:** Al iniciar sesión en `Vogel.Client` con `carlos.gomez` / `UserPass2026*`, `Vogel.Server` autentica contra `POST /login` de MicroID, devuelve `Success = true`, JWT válido y privilegios mapeados por `codename`.
- **CA-03:** Con credenciales inválidas para `carlos.gomez`, `Vogel.Server` retorna HTTP 401 y `Vogel.Client` muestra el mensaje de error sin autenticar al usuario.
- **CA-04:** La invocación al endpoint de validación en tiempo real (`/api/auth/validate` -> `POST /auth`) confirma el token vigente y actualiza los privilegios del usuario.
- **CA-05:** El cambio de contraseña (`/api/auth/change-password` -> `POST /user/changepw`) actualiza la contraseña en MicroID/IdP o retorna el error de política correspondiente.
- **CA-06:** Cambiando `AuthSettings:Provider` a `"Mock"`, la aplicación autentica usando `MockUserData` sin requerir conexión a `http://localhost:8021`.
