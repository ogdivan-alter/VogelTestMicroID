---
name: vogel-crud-module
description: Guía prescriptiva para diseñar, implementar y verificar un módulo CRUD completo en VogelTestMicroID (contratos compartidos en Vogel.Shared, BFF desacoplado con autorización RBAC granular y proveedores Api/Mock en Vogel.Server, vistas Blazor Code-Behind con WorkViewFrame/ViewSet/Form y restricción estricta en DOM en Vogel.Client, pruebas en Vogel.Tests, auditoría InfoSec y QA End-to-End). Úsalo cada vez que se construya o extienda un módulo CRUD (Privilegios, Roles, Usuarios, Sistemas, catálogos administrativos, etc.).
---

# Skill: Construcción de Módulos CRUD en `VogelTestMicroID`

Este skill estandariza el diseño, la implementación y la verificación de cualquier módulo CRUD administrativo en `VogelTestMicroID`, tomando como implementación de referencia el módulo **Administración de Privilegios (`adm_priv`)**.

---

## 1. Principios Arquitectónicos Obligatorios

1. **Desacoplamiento estricto del BFF (`Vogel.Server`):**
   - Los controladores en `Vogel.Server/Controllers/` **nunca** importan ni referencian implementaciones concretas (`MicroId*` ni `Mock*`).
   - Delegan exclusivamente en interfaces genéricas (`I<Modulo>AdminProviderService`, `IPrivilegeAuthorizationService`).
2. **Soporte dual `Api` y `Mock` (`AuthProvider`):**
   - `"Api"` (por defecto): conectado al backend configurado por `MicroId:BaseUrl` mediante `IMicroIdApiClient`.
   - `"Mock"` (explícito): en memoria, thread-safe (`SemaphoreSlim` / `ConcurrentDictionary`), basado en `IMockDataService` y `MockSessionStore`.
3. **Cero URLs hardcodeadas y cero secretos en código fuente:**
   - Toda URL externa proviene de configuración (*fail-fast*). Las llamadas internas usan rutas relativas (`api/<recurso>`, `priv/<accion>`, etc.).
4. **Esquema Blazor Code-Behind:**
   - Todo componente `.razor` separa su lógica en `.razor.cs` y sus estilos en `.razor.css`.
   - Cero `MarkupString` o HTML crudo; el encoding nativo de Razor previene XSS.
5. **Seguridad *Fail-Closed* y OWASP A01:**
   - Todo endpoint BFF valida autenticación (`401`) y autorización granular (`403`) en servidor antes de ejecutar cualquier lógica.
   - En el Frontend, ningún control no autorizado debe existir en el HTML/DOM (prohibido `display: none`, `visibility: hidden`, `hidden` o solo `disabled` para ocultar acciones sin permiso).

---

## 2. Matriz Estándar RBAC para Módulos CRUD

Todo módulo CRUD queda gobernado por un `codename` de privilegio (p. ej. `adm_priv`, `adm_roles`, `adm_users`) y aplica las 4 acciones de `Vogel.Shared.Enums.PrivilegeAction`:

| Permiso (`PrivilegeAction`) | Endpoints BFF (`403 Forbidden` si `false`) | Restricciones Frontend (`Vogel.Client`, ausente del DOM si `false`) |
|---|---|---|
| **`Read`** | `GET api/<recurso>`<br>`GET api/<recurso>/{id}` | - Si `false`: la página principal omite `LoadAsync()`, limpia catálogo residual y la lista muestra aviso de lectura restringida (sin renderizar tabla/filas).<br>- Acceso directo a `/.../edit` bloqueado (redirige al listado con mensaje de error).<br>- *Excepción:* si `Create == true && Read == false`, `/.../create` sigue operando sin invocar `LoadAsync()`. |
| **`Create`** | `POST api/<recurso>`<br>`POST api/<recurso>/{id}/reactivate` | - Botón **Crear** en `*Capture` envuelto en `@if (CanCreate)`.<br>- Botón **Reactivar** en filas inactivas (`Status == "I"`) de `*List` envuelto en `@if (CanCreate)`.<br>- Acceso directo a `/.../create` bloqueado si `!CanCreate` (redirige al listado). |
| **`Modify`** | `PUT api/<recurso>/{id}` | - En filas activas (`Status == "A"`) de `*List`:<br>&nbsp;&nbsp;• `@if (CanModify)` → botón **Editar** (`icons/edit.svg`).<br>&nbsp;&nbsp;• `@else if (CanRead)` → botón **Ver detalle** (`icons/view.svg`).<br>- En `*Form` (`/edit`), si `!CanModify`: modo solo lectura (`IsReadOnlyView = IsEditMode && !CanModify`), título `"Detalle de <Entidad>"`, campos deshabilitados, botón **Guardar Cambios** ausente del DOM (`@if (CanSubmitForm)`), botón secundario `"Volver al listado"`. |
| **`Delete`** | `DELETE api/<recurso>/{id}` | - Sección **Inactivación** en `*Form` envuelta en `@if (CanShowDeactivateSection)` donde `CanShowDeactivateSection => IsEditMode && CanDelete && !IsDeactivated && !LoadFailed`.<br>- Si `Read && !Modify && Delete`, el formulario abre en modo solo lectura pero **sí renderiza y permite** la sección de inactivación. |

---

## 3. Estructura de Archivos por Capa

### 3.1 Contratos Compartidos (`Vogel.Shared`)
- `Vogel.Shared/Enums/PrivilegeAction.cs`: reutilizar el enum existente (`Read`, `Create`, `Modify`, `Delete`, `Validate`, `Execute`, `Approve`).
- `Vogel.Shared/DTOs/<Entidad>AdminDtos.cs`:
  - `<Entidad>AdminItemDto`
  - `<Entidad>AdminListResponseDto`
  - `Create<Entidad>RequestDto` / `Create<Entidad>ResponseDto`
  - `Update<Entidad>RequestDto` / `Update<Entidad>ResponseDto`
  - `Deactivate<Entidad>RequestDto` / `Deactivate<Entidad>ResponseDto`
  - `Reactivate<Entidad>ResponseDto`
- `Vogel.Shared/Services/MockDataService.cs`:
  - Registrar el privilegio del módulo con permisos completos para `admin`/`jdoe` y combinaciones RBAC de prueba (`readonly_user`, `read_delete_user`, `create_only_user`) sin alterar colecciones que rompan pruebas existentes.

### 3.2 Backend BFF (`Vogel.Server`)
1. **Interfaz del proveedor:** `Vogel.Server/Services/I<Entidad>AdminProviderService.cs`
   - Resultados tipados (`*ListResult`, `*DetailResult`, `Create*Result`, `Update*Result`, `Deactivate*Result`, `Reactivate*Result`).
   - Métodos: `ListAsync`, `GetAsync`, `CreateAsync`, `UpdateAsync`, `DeactivateAsync`, `ReactivateAsync`.
2. **Implementación `Api` (MicroID):**
   - Modelos de contrato en `Vogel.Server/Models/MicroId/MicroId<Entidad>Models.cs`.
   - Servicio `Vogel.Server/Services/MicroId/MicroId<Entidad>AdminService.cs` usando `IMicroIdApiClient` con rutas relativas, *ownership check* anti-BOLA/IDOR contra el `SystemNumericId` configurado (en `Get`, `Update`, `Deactivate` y `Reactivate`), normalización a `BackendErrorDto` y `SanitizeForLog(...)` en logs.
3. **Implementación `Mock`:**
   - `Vogel.Server/Services/Mock<Entidad>AdminService.cs` replicando reglas de negocio de forma thread-safe (`SemaphoreSlim`).
4. **Controlador BFF agnóstico:** `Vogel.Server/Controllers/<Entidades>Controller.cs`
   - Rutas: `api/<recurso>` y alias `api/v1/<recurso>`.
   - Inyectar `I<Entidad>AdminProviderService`, `IPrivilegeAuthorizationService` e `ILogger`.
   - En cada acción:
     1. Extraer y validar token (`Authorization: Bearer` o `X-Session-Token`) → `401` si ausente.
     2. Invocar `_privilegeAuthorization.AuthorizeAsync(token, ModulePrivilegeCodename, requiredAction, HttpContext.RequestAborted)` → propagar `401`/`403`/`502` si `!IsAuthorized`.
     3. Validar parámetros/payload → `400`/`422`.
     4. Delegar en el proveedor y retornar respuesta normalizada.
5. **Registro DI (`Vogel.Server/Program.cs`):**
   - Registrar `I<Entidad>AdminProviderService` según `AuthProvider` (`"Api"` vs `"Mock"`).
   - Confirmar que `SecurityHeadersMiddleware` cubre la ruta con `Cache-Control: no-store, no-cache`.

### 3.3 Frontend Blazor (`Vogel.Client`)
1. **Servicio de Estado:** `Vogel.Client/Services/I<Modulo>StateService.cs` y `<Modulo>StateService.cs`
   - Mantiene en memoria: catálogo (`AllItems`), filtros reactivos (`StatusFilter = "A"`, filtros de texto/relación), paginación (`PagedItems`), `SelectedEditId` (evitando exponer IDs sensibles en URL), `SuccessMessage` / `ErrorMessage`, `ClearCatalog()`, `SetErrorMessage(...)`.
   - Adjunta `Authorization: Bearer <token>` leído de `ISecureSessionStorageService`.
   - Al completar mutaciones (`Create`, `Update`, `Deactivate`, `Reactivate`), solo invoca `LoadAsync()` si `SessionState.HasPrivilegeAccess(codename, PrivilegeAction.Read)`.
   - Respuestas `403 Forbidden` publican el error sanitizado **sin cerrar sesión**.
2. **Vistas en `Vogel.Client/Pages/TreeWork/.../<Modulo>/` (Code-Behind `.razor` + `.razor.cs` + `.razor.css`):**
   - **`<Modulo>.razor` / `.razor.cs`:** Contenedor principal con `WorkViewFrame` y `BlockWork.ViewSet` (`Info`, `Capture`, `Filter`, `List`). En inicialización, solo llama `LoadAsync()` si `CanRead`; si `!CanRead`, ejecuta `ClearCatalog()`.
   - **`<Modulo>Info.razor` / `.razor.cs`:** Encabezado descriptivo y conteos en `InfoViewFrame`.
   - **`<Modulo>Capture.razor` / `.razor.cs` / `.razor.css`:** Botón **Crear** alineado a la derecha (`justify-content: flex-end`) con estilo `HeaderWork` y `<img src="icons/plus.svg" />`, envuelto en `@if (CanCreate)` + guard defensivo en el handler.
   - **`<Modulo>Filter.razor` / `.razor.cs` / `.razor.css`:** Filtros apilados verticalmente con etiqueta explícita a la izquierda (`for`/`id` y `aria-labelledby`), responsive y botones limpiar (`×`).
   - **`<Modulo>List.razor` / `.razor.cs` / `.razor.css`:**
     - `ListViewFrame` renderizado únicamente si `CanRead`; si `!CanRead`, aviso informativo de lectura restringida.
     - Botones de acción en tabla de **alto contraste** sobre filas blancas (`#1e293b`, borde `#334155`, tamaño cuadrado `32×32px`), **solo ícono sin texto** con `title` y `aria-label`:
       - Activo (`Status == "A"`): `icons/edit.svg` (`@if (CanModify)`) o `icons/view.svg` (`@else if (CanRead)`).
       - Inactivo (`Status == "I"`): `icons/reactivate.svg` (`@if (CanCreate)`), con bloqueo contra doble clic por fila (`ReactivatingId`).
   - **`<Modulo>Form.razor` / `.razor.cs`:**
     - Rutas separadas `/create` y `/edit` (sin IDs en la URL; el ID se toma de `<Modulo>State.SelectedEditId`).
     - Guards en `OnParametersSetAsync`: `/create` exige `CanCreate`; `/edit` exige `CanRead` (si falla, publica error en estado y redirige con `replace: true`).
     - En creación, omite `LoadAsync()` si `!CanRead`.
     - Soporta modo solo lectura (`IsReadOnlyView = IsEditMode && !CanModify`) con campos `disabled`, sin botón guardar en DOM (`@if (CanSubmitForm)`), manteniendo la sección de inactivación independiente bajo `@if (CanShowDeactivateSection)`.
     - Guards defensivos en todos los handlers de envío e inactivación.
3. **Íconos SVG (`Vogel.Client/wwwroot/icons/`):**
   - Reutilizar `plus.svg`, `edit.svg`, `view.svg` y `reactivate.svg` (o crear nuevos siguiendo el mismo estándar: `viewBox="0 0 24 24"`, `stroke="#ffffff"`, únicamente primitivas vectoriales seguras sin `<script>`, `foreignObject`, eventos inline ni referencias externas).

---

## 4. Checklist Obligatorio de Calidad, Seguridad y Cierre

Antes de considerar terminado cualquier módulo CRUD, se deben completar en orden:

1. **Pruebas Unitarias (`Vogel.Tests`):**
   - Controlador: los 6 endpoints con `401` (sin token), `403` (permiso denegado sin invocar proveedor), `400`/`422` (validación) y `200`/`201` (éxito), confirmando que `Reactivate` exige `PrivilegeAction.Create`.
   - Proveedores `Api` (incluyendo *ownership check* BOLA/IDOR y mapeo de errores) y `Mock` (ciclo completo y reglas de negocio).
   - Servicio de estado cliente (`LoadAsync`, filtros, CRUD, `ReactivateAsync`, manejo de `403` y no-recarga sin `Read`).
   - `dotnet build Vogel.slnx` (0 errores, 0 advertencias) y `dotnet test Vogel.slnx --no-build` (100% en verde).
2. **Auditoría InfoSec (`vogel-infosec-guardian`):**
   - Verificar OWASP A01, *fail-closed*, anti-BOLA/IDOR, ausencia de controles no autorizados en DOM, cero URLs hardcodeadas, sanitización de logs y SVGs limpios.
3. **Verificación QA End-to-End (`vogel-qa-verifier`):**
   - Probar en modo **`Mock`** con los 4 perfiles RBAC (`admin`, `readonly_user`, `read_delete_user`, `create_only_user`) verificando códigos HTTP reales (`200`/`201`/`401`/`403`) y DOM/Razor.
   - Probar en modo **`Api`** contra MicroID real validando autorización en tiempo real vía `POST /auth` y limpieza de datos temporales.
4. **Cierre Documental (`vogel-req-analyst`):**
   - Archivar `reqs/` + `Cierre_RFC.md` en `VogelTestMicroID-Docs/rfc/rfc_YYYYMMDD_<slug>/`, actualizar `README.md` y hacer `commit` + `push`.
   - Eliminar los archivos temporales de `reqs/` en la rama de código y abrir el Pull Request hacia `main`.
