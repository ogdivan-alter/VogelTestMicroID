# Requerimiento Funcional y Técnico: Control Granular de Permisos (RBAC) en Administración de Privilegios (`adm_priv`) y Skill de Módulos CRUD

| Campo | Valor |
|---|---|
| **Módulo / Alcance** | Administración de Privilegios (`adm_priv`) — Front (`Vogel.Client`), BFF (`Vogel.Server`), Contratos (`Vogel.Shared`) y Skill CRUD (`.agents/skills/vogel-crud-module/`) |
| **Rama Git** | `adm-priv-rbac-crud-skill` |
| **Fecha** | 2026-11-24 |
| **Estado** | Aprobado |

---

## 1. Propósito y Contexto

En la arquitectura de Vogel + MicroID, cada privilegio asignado a un usuario incluye una matriz booleana de permisos granulares (`PrivilegeAccess`: `Read`, `Create`, `Modify`, `Delete`, `Validate`, `Execute`, `Approve`).

Actualmente:
- `Vogel.Client` recibe y almacena los privilegios con su objeto `Access` dentro de `SessionState.CurrentUser.Privileges`, pero `SessionState` solo verifica presencia del privilegio (`PrivilegeFunction`) y ningún componente del módulo `AdmPriv` evalúa los permisos granulares.
- `Vogel.Server` (`PrivilegesController`) valida únicamente la existencia del token de sesión, y los endpoints `/priv/*` de MicroID tampoco validan permisos funcionales granulares del privilegio `adm_priv` (únicamente autenticación JWT + `X-System-Key`).
- Sin embargo, MicroID expone `POST /auth` (ya integrado en `Vogel.Server` vía `IAuthProviderService.GetPrivilegesAsync(token)`), que valida el token en tiempo real y retorna la matriz efectiva consolidada de privilegios y permisos del usuario.

El objetivo de este requerimiento es:
1. Implementar la aplicación estricta de los permisos **`read`**, **`create`**, **`modify`** y **`delete`** del privilegio `adm_priv` tanto en el **Frontend (`Vogel.Client`)** (sin renderizar en HTML/DOM los controles no autorizados y bloqueando rutas/acciones directas) como en el **BFF (`Vogel.Server`)** (validación autoritativa en servidor sin confiar en el cliente, en modos `Api` y `Mock`).
2. Establecer un patrón reutilizable de autorización por privilegio para futuros módulos CRUD y, una vez aprobada la implementación, formalizar un **Skill dedicado para la creación de módulos CRUD** en `.agents/skills/vogel-crud-module/SKILL.md`.

---

## 2. Alcance

### 2.1 Incluido
- Extensión de contratos compartidos en `Vogel.Shared` (`PrivilegeAction` enum y utilidades necesarias) y adición del privilegio `adm_priv` (con perfiles de prueba completos y restringidos) en `MockDataService`.
- Servicio de autorización granular en `Vogel.Server` (`IPrivilegeAuthorizationService` / `PrivilegeAuthorizationService`) desacoplado del proveedor (delegando en `IAuthProviderService.GetPrivilegesAsync(token)`).
- Protección de todos los endpoints de `PrivilegesController` según la matriz de permisos de `adm_priv` (retornando `403 Forbidden` cuando el usuario autenticado carezca del permiso requerido).
- Soporte en `MockAuthProviderService` para asociar el token de sesión emitido en `LoginAsync` con el usuario mock autenticado, de modo que `GetPrivilegesAsync(token)` devuelva los permisos reales de ese usuario en modo `Mock`.
- Helpers reutilizables de consulta de permisos en `Vogel.Client/Services/SessionState.cs`.
- Restricciones de UI, DOM y navegación en `AdmPriv`, `AdmPrivCapture`, `AdmPrivList`, `AdmPrivForm` y `AdmPrivStateService`, incluyendo el ícono vectorial `Vogel.Client/wwwroot/icons/view.svg` para el botón **Ver detalle**.
- Pruebas unitarias en `Vogel.Tests` cubriendo autorización BFF, `PrivilegesController`, `SessionState` y componentes/flujos RBAC.
- Auditoría obligatoria de seguridad (`vogel-infosec-guardian`) y verificación QA End-to-End (`vogel-qa-verifier`) en modos `Api` (MicroID) y `Mock`.
- Creación del Skill dedicado para módulos CRUD en `.agents/skills/vogel-crud-module/SKILL.md` al concluir la verificación de la implementación.

### 2.2 Fuera de Alcance
- Modificaciones en el código fuente o base de datos de `MICROID` (solo lectura).
- Implementación de otros módulos CRUD (Roles, Usuarios, etc.), los cuales adoptarán el patrón y el Skill resultante en requerimientos posteriores.
- Aplicación de los flags `validate`, `execute` y `approve` en `adm_priv` (el modelo los conserva para futuros módulos).

---

## 3. Reglas Funcionales y Matriz de Permisos (`adm_priv`)

El codename del privilegio que gobierna el módulo es **`adm_priv`** (comparación insensible a mayúsculas/minúsculas y tolerante a guiones/guiones bajos, consistente con `SessionState.PrivilegeFunction`). Si el usuario no posee el privilegio `adm_priv`, todos sus permisos efectivos (`read`, `create`, `modify`, `delete`) se evalúan como **`false`** (*fail-closed*).

### 3.1 Matriz de Permisos: Frontend y BFF

| Permiso (`adm_priv`) | Endpoint(s) BFF (`PrivilegesController`) | Comportamiento BFF si permiso = `false` | Controles / Vistas en Frontend (`Vogel.Client`) | Comportamiento Frontend si permiso = `false` |
|---|---|---|---|---|
| **`read`** | `GET /api/privileges`<br>`GET /api/privileges/{id}` | `403 Forbidden` (`BackendErrorDto`) | - Listado en `AdmPrivList`<br>- Acceso a `AdmPrivForm` en modo detalle/edición (`/admsec/priv/edit`)<br>- Botón **Ver detalle** (`icons/view.svg`) cuando `read && !modify` | - `AdmPriv` no dispara carga del listado (`LoadAsync`) y muestra aviso informativo de acceso restringido a lectura.<br>- No se renderizan acciones de lectura/edición en filas.<br>- Acceso directo a `/admsec/priv/edit` bloqueado (redirige a `/admsec/priv` con mensaje de permiso insuficiente).<br>- *Excepción:* si `create = true` y `read = false`, sí puede acceder a `/admsec/priv/create` para crear nuevos registros (el selector de padre queda disponible con `0 — Ninguno (Raíz)` sin exigir `GET /api/privileges`). |
| **`create`** | `POST /api/privileges`<br>`POST /api/privileges/{id}/reactivate` | `403 Forbidden` (`BackendErrorDto`) | - Botón **Crear privilegio** en `AdmPrivCapture`<br>- Ruta `/admsec/priv/create` (`AdmPrivForm` modo creación)<br>- Botón **Reactivar** (`icons/reactivate.svg`) en filas inactivas de `AdmPrivList` | - El botón **Crear privilegio** **NO se renderiza en el HTML/DOM**.<br>- El botón **Reactivar** en filas inactivas **NO se renderiza en el HTML/DOM**.<br>- Acceso directo por URL a `/admsec/priv/create` bloqueado (redirige a `/admsec/priv` con mensaje de permiso insuficiente). |
| **`modify`** | `PUT /api/privileges/{id}` | `403 Forbidden` (`BackendErrorDto`) | - Botón **Editar** (`icons/edit.svg`) en filas activas de `AdmPrivList`<br>- Campos editables y botón **Guardar Cambios** en `AdmPrivForm` (`/admsec/priv/edit`) | - El botón **Editar** **NO se renderiza en el HTML/DOM**.<br>- Si `read = true` y `modify = false`, en su lugar se renderiza el botón **Ver detalle** (`icons/view.svg`) que abre `AdmPrivForm` en **modo solo lectura**.<br>- En modo solo lectura: todos los campos de datos están deshabilitados (`disabled`), el título del encabezado indica consulta (ej. `Detalle de Privilegio`) y el botón **Guardar Cambios** **NO se renderiza en el HTML/DOM** (ni puede invocarse `HandleSubmitAsync` para actualizar). |
| **`delete`** | `DELETE /api/privileges/{id}` | `403 Forbidden` (`BackendErrorDto`) | - Sección **Inactivación del Privilegio** (`txtDelDescription`, botón **Inactivar privilegio** y confirmación) en `AdmPrivForm` (`/admsec/priv/edit`) | - Toda la sección de inactivación y sus controles **NO se renderizan en el HTML/DOM**.<br>- Los métodos `RequestDeactivateConfirmation` y `HandleConfirmDeactivateAsync` bloquean cualquier invocación programática. |

### 3.2 Reglas de Combinaciones Clave

1. **`read = true`, `modify = false`, `delete = false` (Solo lectura pura):**
   - `AdmPrivList` muestra el listado y, en cada fila activa, el botón **Ver detalle** (`icons/view.svg`).
   - Al hacer clic, abre `AdmPrivForm` (`/admsec/priv/edit`) en modo solo lectura: campos deshabilitados, sin botón **Guardar Cambios**, sin sección de **Inactivación**, solo botón **Volver al listado**.
2. **`read = true`, `modify = false`, `delete = true` (Solo lectura + inactivación):**
   - `AdmPrivList` muestra el botón **Ver detalle** (`icons/view.svg`) en filas activas.
   - Al abrir `AdmPrivForm`, los campos de datos permanecen deshabilitados y **no** se renderiza el botón **Guardar Cambios**, **pero sí se renderiza y funciona la sección de Inactivación del Privilegio** (porque el usuario posee `delete = true`).
3. **`read = false`, `modify = true` (Sin permiso de lectura):**
   - Por regla de negocio (`read` es prerrequisito para abrir `AdmPrivForm` en modo detalle/edición y consultar `GET /api/privileges/{id}`), el usuario no puede abrir `AdmPrivForm` en `/admsec/priv/edit`. El BFF rechaza `GET /api/privileges` y `GET /api/privileges/{id}` con `403`.
4. **`read = false`, `create = true` (Solo creación):**
   - En `/admsec/priv`, `AdmPrivCapture` renderiza **Crear privilegio**; `AdmPrivList` informa que no posee permiso de lectura del listado.
   - Al abrir `/admsec/priv/create`, `AdmPrivForm` no exige `GET /api/privileges` para operar (mantiene `0 — Ninguno (Raíz)` en el selector de padre) y permite invocar `POST /api/privileges`.
5. **Renderizado estricto (Anti-inspección HTML):**
   - Ningún botón o sección no autorizada debe existir en el árbol DOM (prohibido usar `display: none`, `visibility: hidden`, atributo `hidden` o solo `disabled` para ocultar acciones sin permiso). Todo control restringido debe envolverse en bloques `@if (Can...)` en Razor.

---

## 4. Diseño Arquitectónico y Contratos

### 4.1 Contratos Compartidos (`Vogel.Shared`)
- **Nuevo enum `Vogel.Shared/Enums/PrivilegeAction.cs`:**
  ```csharp
  public enum PrivilegeAction
  {
      Read,
      Create,
      Modify,
      Delete,
      Validate,
      Execute,
      Approve
  }
  ```
- **Actualización de `MockDataService.cs`:**
  - Incorporar el privilegio `adm_priv` (bajo la rama de Administración) en el catálogo mock.
  - Mantener `admin` / `jdoe` con permisos completos (`Read = true, Create = true, Modify = true, Delete = true`) sobre `adm_priv`.
  - Incorporar usuarios mock dedicados (o ajustar perfiles existentes sin romper pruebas actuales) para probar combinaciones RBAC en modo `Mock` (por ejemplo: solo lectura `readonly_user`, lectura + eliminación `read_delete_user`, sin lectura `noread_user`).

### 4.2 BFF (`Vogel.Server`)
- **Principio de desacoplamiento:** `PrivilegesController` sigue sin conocer MicroID ni Mock.
- **Nueva interfaz `Vogel.Server/Services/IPrivilegeAuthorizationService.cs`:**
  ```csharp
  public record PrivilegeAuthorizationResult(
      bool IsAuthorized,
      int StatusCode,
      BackendErrorDto? Error);

  public interface IPrivilegeAuthorizationService
  {
      Task<PrivilegeAuthorizationResult> AuthorizeAsync(
          string token,
          string privilegeCodename,
          PrivilegeAction requiredAction,
          CancellationToken cancellationToken = default);
  }
  ```
- **Implementación `Vogel.Server/Services/PrivilegeAuthorizationService.cs` (Scoped):**
  - Invoca `IAuthProviderService.GetPrivilegesAsync(token, username: null, cancellationToken)`.
  - Si el proveedor retorna fallo (`401`, `403`, `502`, etc.), propaga ese `StatusCode` y error (en modo *fail-closed*, un fallo de comunicación nunca autoriza).
  - Busca el privilegio cuyo `Codename` coincida con `privilegeCodename` (normalizando `_` y `-`, `OrdinalIgnoreCase`).
  - Evalúa el flag correspondiente en `BackendAccessDto` (`Read`, `Write` para `Create`, `Modify`, `Delete`, etc.).
  - Si no existe el privilegio o el flag es `false`, registra un evento de auditoría `LogWarning` (sin exponer tokens ni datos sensibles) y retorna `403 Forbidden` con `BackendErrorDto(403, "No cuenta con permisos suficientes para realizar esta operación.")`.
- **Integración en `PrivilegesController`:**
  - Constante `private const string ModulePrivilegeCodename = "adm_priv";`.
  - Cada acción valida token (`401`) y luego invoca `_privilegeAuthorization.AuthorizeAsync(token, ModulePrivilegeCodename, requiredAction, HttpContext.RequestAborted)` **antes** de ejecutar la operación en `IPrivilegeAdminProviderService`:
    - `List` → `PrivilegeAction.Read`
    - `GetById` → `PrivilegeAction.Read`
    - `Create` → `PrivilegeAction.Create`
    - `Update` → `PrivilegeAction.Modify`
    - `Deactivate` → `PrivilegeAction.Delete`
    - `Reactivate` → `PrivilegeAction.Create`
- **Soporte de sesiones por usuario en `MockAuthProviderService`:**
  - Mantener un diccionario concurrente (`ConcurrentDictionary<string, string>`) en un servicio Singleton o dentro de `IMockDataService` / registro de sesiones mock que asocie cada `mockToken` generado en `LoginAsync` con el `Username` autenticado, de modo que `MockAuthProviderService.GetPrivilegesAsync(token)` devuelva los privilegios exactos de ese usuario (y rechace con `401` tokens desconocidos si se requiere, manteniendo compatibilidad con pruebas unitarias existentes).

### 4.3 Frontend Blazor (`Vogel.Client`)
- **`SessionState.cs`:**
  - Agregar `Privilege? GetPrivilege(string codename)` y `bool HasPrivilegeAccess(string codename, PrivilegeAction action)` (evaluación *fail-closed*: retorna `false` si no autenticado, si el privilegio no existe o si `Access` es `null`).
- **Nuevo recurso SVG:**
  - `Vogel.Client/wwwroot/icons/view.svg`: ícono vectorial limpio (ojo) estilizado en coherencia con `edit.svg`, `plus.svg` y `reactivate.svg`.
- **Componentes `AdmPriv*`:**
  - **`AdmPriv.razor.cs`:** Si `!SessionState.HasPrivilegeAccess("adm_priv", PrivilegeAction.Read)`, omite `AdmPrivState.LoadAsync()` y limpia el listado/estado previo para evitar fugas visuales o llamadas `403`.
  - **`AdmPrivCapture.razor` / `.razor.cs`:** Envuelve el botón **Crear privilegio** (o todo el bloque si no hay otras acciones) en `@if (CanCreate)`. Guard defensivo en `HandleCreatePrivilegeClick()`.
  - **`AdmPrivList.razor` / `.razor.cs`:**
    - Si `!CanRead`, muestra un estado informativo claro indicando que el usuario no tiene permiso de lectura (`read`) para consultar privilegios (sin renderizar filas).
    - En filas activas:
      - `@if (CanModify)` → botón **Editar** (`icons/edit.svg`).
      - `@else if (CanRead)` → botón **Ver detalle** (`icons/view.svg`).
    - En filas inactivas:
      - `@if (CanCreate)` → botón **Reactivar** (`icons/reactivate.svg`).
    - Guards defensivos en `HandleEditClick` / `HandleViewClick` / `HandleReactivateClickAsync`.
  - **`AdmPrivForm.razor` / `.razor.cs`:**
    - Propiedades calculadas: `CanRead`, `CanCreate`, `CanModify`, `CanDelete`, `IsReadOnlyView => IsEditMode && !CanModify`.
    - En `OnParametersSetAsync`:
      - Modo creación (`/admsec/priv/create`): si `!CanCreate`, redirige a `/admsec/priv` registrando mensaje de error en `AdmPrivState`.
      - Modo edición/detalle (`/admsec/priv/edit`): si `!CanRead`, redirige a `/admsec/priv` registrando mensaje de error en `AdmPrivState`.
    - En `LoadCreateDataAsync`: solo invoca `AdmPrivState.LoadAsync()` si `CanRead` (evitando `403` cuando un usuario tiene `create = true` y `read = false`).
    - `HeaderTitle`: `"Crear Privilegio"` (creación), `"Editar Privilegio"` (edición con `CanModify`), `"Detalle de Privilegio"` (solo lectura con `CanRead && !CanModify`).
    - `IsFormDisabled`: incluye `IsReadOnlyView`.
    - `CanSubmitForm`: `!LoadFailed && !IsDeactivated && (IsEditMode ? CanModify : CanCreate)` → botón guardar ausente del DOM si no aplica.
    - `CanShowDeactivateSection`: `IsEditMode && CanDelete && !IsDeactivated && !LoadFailed` → sección de inactivación ausente del DOM si `!CanDelete`, y **visible/operativa** si `CanRead && !CanModify && CanDelete`.
    - `SecondaryButtonText`: `"Volver al listado"` cuando `IsDeactivated || LoadFailed || IsReadOnlyView`; `"Cancelar"` en otro caso.
    - Guards defensivos al inicio de `HandleSubmitAsync`, `RequestDeactivateConfirmation` y `HandleConfirmDeactivateAsync`.
  - **`AdmPrivStateService.cs`:**
    - Agregar método para publicar mensaje de error controlado (p. ej. al bloquear navegación directa) y asegurar que las respuestas `403 Forbidden` muestren el mensaje de error sin cerrar la sesión del usuario.

---

## 5. Diseño del Skill Dedicado para Módulos CRUD

Al finalizar y aprobar la implementación de `adm_priv`, se creará el skill en **`.agents/skills/vogel-crud-module/SKILL.md`** para estandarizar la construcción de cualquier módulo CRUD en `VogelTestMicroID`.

El skill cubrirá de forma prescriptiva:
1. **Arquitectura de capas:** Contratos (`Vogel.Shared/DTOs/`), proveedores desacoplados en `Vogel.Server` (`I*AdminProviderService`, implementación `MicroId*` y `Mock*`), controlador BFF agnóstico con `IPrivilegeAuthorizationService` y `PrivilegesController` como referencia.
2. **Matriz estándar RBAC por módulo CRUD:** Mapeo obligatorio de `read`, `create` (incluyendo reactivación), `modify` y `delete` en BFF (`403 Forbidden`, *fail-closed*) y en Frontend (renderizado condicional estricto sin DOM oculto, botón diferenciado `Editar` vs `Ver detalle`, modo solo lectura con sección `delete` independiente).
3. **Estructura de vistas Blazor (`Vogel.Client`):**
   - Ubicación en `Pages/TreeWork/.../<Modulo>/` con **Code-Behind (`.razor.cs`)** y CSS aislado (`.razor.css`).
   - Composición con `WorkViewFrame` + `BlockWork.ViewSet` (`Info`, `Capture`, `Filter`, `List`) + formulario reutilizable `*Form` (`/create` y `/edit` sin IDs sensibles en URL, usando estado en memoria `*StateService`).
   - Recursos SVG en `wwwroot/icons/` (`plus.svg`, `edit.svg`, `view.svg`, `reactivate.svg`).
4. **Checklist de calidad obligatorio:** Pruebas unitarias en `Vogel.Tests`, auditoría InfoSec (`vogel-infosec-guardian`) y verificación QA End-to-End (`vogel-qa-verifier`) en modos `Api` y `Mock`.

---

## 6. Requisitos de Seguridad de la Información (InfoSec)

- **OWASP A01 — Broken Access Control:** La autorización es aplicada autoritativamente en el BFF (`Vogel.Server`) en cada endpoint de `PrivilegesController`, consultando al proveedor de identidad en tiempo real (`POST /auth` en MicroID / estado de sesión en Mock). Nunca se confía en headers, claims manipulables ni ocultamiento en el cliente.
- **Fail-Closed:** Si el servicio de autorización no puede validar los permisos (error de red, `502`, privilegio ausente, `Access == null`), el acceso se deniega.
- **No exposición de controles en DOM:** Los controles no autorizados no deben renderizarse en el HTML generado por Blazor (`@if`), evitando que un usuario los habilite vía DevTools.
- **Auditoría y Logging seguro:** Registrar eventos de denegación (`403`) con el codename del privilegio y la acción solicitada, sin registrar tokens JWT, contraseñas ni PII innecesaria, y sanitizando contra Log Injection.
- **Revisión obligatoria:** Dictamen formal de `vogel-infosec-guardian` antes de QA y cierre del PR.

---

## 7. Criterios de Aceptación

- **CA-01 (BFF `read`):** Un usuario autenticado sin permiso `read` en `adm_priv` recibe `403 Forbidden` al invocar `GET /api/privileges` y `GET /api/privileges/{id}` (tanto en modo `Api` como `Mock`).
- **CA-02 (BFF `create` y reactivación):** Un usuario autenticado sin permiso `create` en `adm_priv` recibe `403 Forbidden` al invocar `POST /api/privileges` y `POST /api/privileges/{id}/reactivate`. Con `create = true`, ambas operaciones son permitidas.
- **CA-03 (BFF `modify`):** Un usuario autenticado sin permiso `modify` en `adm_priv` recibe `403 Forbidden` al invocar `PUT /api/privileges/{id}`.
- **CA-04 (BFF `delete`):** Un usuario autenticado sin permiso `delete` en `adm_priv` recibe `403 Forbidden` al invocar `DELETE /api/privileges/{id}`.
- **CA-05 (UI `create` ausente en DOM):** Con `create = false`, ni el botón **Crear privilegio** en `AdmPrivCapture` ni el botón **Reactivar** en filas inactivas de `AdmPrivList` existen en el HTML/DOM; navegar directamente a `/admsec/priv/create` redirige a `/admsec/priv` con aviso.
- **CA-06 (UI `modify = false` + `read = true`):** Con `read = true` y `modify = false`, el botón **Editar** no existe en el HTML/DOM de `AdmPrivList`; en su lugar se renderiza el botón **Ver detalle** (`icons/view.svg`). Al abrir `AdmPrivForm`, todos los campos de datos están deshabilitados y el botón **Guardar Cambios** no existe en el HTML/DOM.
- **CA-07 (UI `read = true`, `modify = false`, `delete = true`):** Al abrir `AdmPrivForm` vía **Ver detalle**, los campos de datos están deshabilitados y sin botón **Guardar Cambios**, pero la sección de **Inactivación del Privilegio** sí se renderiza y permite inactivar el registro.
- **CA-08 (UI `delete = false` ausente en DOM):** Con `delete = false`, la sección de **Inactivación del Privilegio** y todos sus controles no existen en el HTML/DOM de `AdmPrivForm`.
- **CA-09 (UI `read = false`):** Con `read = false`, `AdmPrivList` no muestra registros ni dispara `GET /api/privileges` (muestra aviso de lectura restringida), y navegar directamente a `/admsec/priv/edit` redirige a `/admsec/priv`. Si además tiene `create = true`, puede abrir `/admsec/priv/create` y crear privilegios raíz sin errores.
- **CA-10 (Pruebas automatizadas):** Todas las pruebas nuevas y existentes en `Vogel.Tests` pasan al 100% (`dotnet test`).
- **CA-11 (InfoSec y QA):** Dictamen aprobado por `vogel-infosec-guardian` y verificación QA End-to-End exitosa por `vogel-qa-verifier` en modos `Api` (MicroID) y `Mock`.
- **CA-12 (Skill CRUD):** Existe el archivo `.agents/skills/vogel-crud-module/SKILL.md` completo, estructurado y alineado con el patrón implementado.
