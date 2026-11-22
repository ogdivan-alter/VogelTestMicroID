# Requerimiento Funcional y Técnico: Formulario de Creación, Edición e Inactivación de Privilegios (`AdmPrivForm`)

## 1. Propósito y Contexto
Extender el módulo **Administración de Privilegios (`AdmPriv`)** del portal `Vogel` implementando el formulario reutilizable para la **creación**, **edición** e **inactivación lógica** de privilegios del sistema, tomando como base estética y estructural la vista de referencia `FormMockView.razor` (`Vogel.Client/Pages/Demo/FormMockView.razor`), y vinculándolo con los botones **Crear privilegio** (`AdmPrivCapture`) y **Editar** (`AdmPrivList`) ya existentes en `/admsec/priv`.

El diseño mantiene la arquitectura desacoplada de Vogel:
- El frontend Blazor (`Vogel.Client`) se comunica exclusivamente con el BFF (`Vogel.Server`).
- El controlador `PrivilegesController` delega únicamente en la interfaz agnóstica `IPrivilegeAdminProviderService`.
- Se soportan ambos modos de proveedor definidos por `AuthProvider`: `"Api"` (por defecto, contra MicroID) y `"Mock"` (explícito, en memoria sobre el catálogo simulado).

---

## 2. Alcance

### 2.1 Incluido
1. **Formulario único reutilizable (`AdmPrivForm.razor` + `AdmPrivForm.razor.cs`)** en `Vogel.Client/Pages/TreeWork/AdmTools/AdmSecurity/AdmPriv/` con dos rutas limpias e independientes (sin exponer identificadores en la URL):
   - Creación: `/admsec/priv/create`
   - Edición / Inactivación: `/admsec/priv/edit` (lee el ID del privilegio seleccionado desde memoria en `AdmPrivState`, ej. `SelectedEditPrivilegeId`, y consulta el detalle fresco al BFF)
2. **Base visual alineada con `FormMockView.razor`**:
   - Encabezado `HeaderWork` con título dinámico según el modo y botón `Salir` apuntando a `/admsec/priv`.
   - Contenedor `.vogel-form-mock-view` / `.vogel-form-mock-body` / `.vogel-form-mock-container` / `.vogel-form-card` reutilizando las clases CSS existentes en `Vogel.Client/wwwroot/css/app.css`.
   - Alertas de éxito (`.vogel-form-alert-success`) y de error, secciones claras (`.vogel-form-section`), controles estilizados y botonera inferior (`.vogel-form-actions`).
3. **Vinculación desde el módulo `AdmPriv` (`/admsec/priv`)**:
   - Botón **Crear privilegio** en `AdmPrivCapture` limpia cualquier selección previa de edición en `AdmPrivState` y navega a `/admsec/priv/create`.
   - Botón **Editar** por fila en `AdmPrivList` guarda `item.IdPrivilege` en memoria dentro de `AdmPrivState` (`SelectedEditPrivilegeId`) y navega a `/admsec/priv/edit` únicamente cuando el registro está activo (`status == "A"`); para registros inactivos (`status == "I"`) el botón **Editar** se renderiza deshabilitado con indicación clara.
4. **Endpoints BFF en `PrivilegesController`** (rutas `api/privileges` y `api/v1/privileges`):
   - `GET /api/privileges/{id:int}`: Consulta de detalle de un privilegio activo.
   - `POST /api/privileges`: Creación de un nuevo privilegio en el sistema configurado.
   - `PUT /api/privileges/{id:int}`: Actualización de atributos y jerarquía (`id_parent`) de un privilegio activo.
   - `DELETE /api/privileges/{id:int}`: Inactivación lógica (`status = "I"`) de un privilegio activo con motivo opcional (`del_description`), ejecutando en MicroID el traslado en cascada de asignaciones `role_privilege`.
5. **Integración completa en ambos proveedores BFF**:
   - `MicroIdPrivilegeAdminService` (modo `"Api"`): consumo de `POST /priv/get`, `POST /priv/create`, `POST /priv/update` y `POST /priv/delete` en MicroID.
   - `MockPrivilegeAdminService` (modo `"Mock"`): soporte completo de consulta individual, creación, actualización e inactivación en memoria para pruebas locales sin MicroID.
6. **Pruebas unitarias**, **auditoría InfoSec (`vogel-infosec-guardian`)** y **verificación QA End-to-End (`vogel-qa-verifier`)**.

### 2.2 Fuera de Alcance
- Reactivación de privilegios inactivos (`status = "I"` → `"A"`), dado que MicroID no expone operación de reactivación para privilegios inactivados.
- Gestión de asignación de privilegios a roles (`role_privilege`).
- Eliminación física de registros en base de datos.
- Modificación de cualquier archivo en `F:\LABORATORIO\2026\MICROID` (solo lectura).

---

## 3. Diseño de Interfaz y Flujo Funcional (`Vogel.Client`)

### 3.1 Rutas y Protección de Sesión
- `@page "/admsec/priv/create"`
- `@page "/admsec/priv/edit"` *(ruta limpia sin `IdPrivilege` en la URL)*
- En `OnInitializedAsync` / `OnParametersSetAsync`:
  - Si `!SessionState.IsAuthenticated`, redirigir inmediatamente a `/login`.
  - Si la ruta activa es `/admsec/priv/edit` y `AdmPrivState.SelectedEditPrivilegeId` no tiene un valor válido (`<= 0` o `null`, por ejemplo por acceso directo por URL o recarga F5), redirigir limpiamente a `/admsec/priv`.
  - Al pulsar **Cancelar**, **Volver al listado** o salir de la vista (`Dispose`), limpiar `SelectedEditPrivilegeId` en `AdmPrivState`.
- El formulario usa esquema **Code-Behind** estricto (`AdmPrivForm.razor` + `AdmPrivForm.razor.cs`).

### 3.2 Estructura Visual (Basada en `FormMockView.razor`)
- **`HeaderWork`**:
  - En modo creación: `Title="Crear Privilegio"`, `Color="#0c0e14"`, `TextExit="Salir"`, `ActionExit="/admsec/priv"`.
  - En modo edición: `Title="Editar Privilegio"`, `Color="#0c0e14"`, `TextExit="Salir"`, `ActionExit="/admsec/priv"`.
- **Alertas superiores** (dentro de `.vogel-form-mock-container`):
  - Alerta de éxito (`.vogel-form-alert-success`, cerrable) cuando se crea, actualiza o inactiva un privilegio.
  - Alerta de error clara y sanitizada cuando falla una validación o una llamada al BFF.
- **Tarjeta principal (`.vogel-form-card`)**:
  1. **Sección 1 — Datos Generales**:
     - Fila 1 (`.vogel-form-row--2col`):
       - `Sistema` (solo lectura / informativo, muestra `SystemCode` y/o `IdSystem` del sistema configurado; no editable).
       - En modo edición: `ID Privilegio` y `Estado` (`Activo` / `Inactivo`, solo lectura informativo). En modo creación: `Estado` informativo (`Activo` por defecto).
     - Fila 2 (`.vogel-form-row--2col`):
       - `Codename` (`<input type="text">`, obligatorio, recortado con `.Trim()`).
       - `Título (Title)` (`<input type="text">`, obligatorio).
     - Fila 3 (`.vogel-form-row--2col`):
       - `Grupo (GroupName)` (`<input type="text">`, obligatorio).
       - `Acción / Ruta (Action)` (`<input type="text">`, obligatorio).
  2. **Sección 2 — Jerarquía y Presentación**:
     - Fila (`.vogel-form-row--2col`):
       - `Privilegio Padre (id_parent)`: desplegable (`<select class="vogel-form-select">`) con:
         - Opción por defecto: `0` → `"0 — Ninguno (Raíz)"`.
         - Opciones dinámicas cargadas desde los privilegios activos del sistema (`status == "A"`), formateadas como `"{IdPrivilege} — {Title} ({Codename})"`.
         - En **modo edición**, se **excluye el propio `IdPrivilege`** del desplegable para evitar autorreferencia directa.
       - `Ícono (Icon)`: campo de texto libre (`<input type="text">`, opcional).
  3. **Sección 3 — Inactivación del Privilegio (visible únicamente en modo edición mientras el privilegio esté activo)**:
     - Permite inactivar el privilegio invocando `DELETE /api/privileges/{id}` (`POST /priv/delete` en MicroID).
     - Campo opcional `Motivo de inactivación (del_description)` (`<textarea class="vogel-form-textarea">`, máximo 500 caracteres).
     - Flujo de confirmación explícita en dos pasos (por ejemplo, botón **"Inactivar privilegio"** que despliega panel/confirmación inline con advertencia de que la inactivación también revocará asignaciones en roles, más botones **"Confirmar inactivación"** y **"Cancelar inactivación"**).
  4. **Botonera de acciones (`.vogel-form-actions`)**:
     - Botón principal **"Crear Privilegio"** (modo creación) / **"Guardar Cambios"** (modo edición), deshabilitado mientras `_isSaving` o tras inactivar el registro.
     - Botón secundario **"Volver al listado"** / **"Cancelar"** que navega a `/admsec/priv`.

### 3.3 Comportamiento Post-Operación (Estados Seguros)
El usuario permanece en el formulario mostrando una alerta de éxito clara, manteniendo un estado consistente y seguro:
1. **Tras Crear exitosamente**:
   - Se muestra alerta de éxito indicando el ID del privilegio creado (p. ej., *"Privilegio creado exitosamente (ID: {IdPrivilege})."*).
   - Se recarga/actualiza la lista de privilegios en `AdmPrivState` (para que el catálogo y el desplegable de `id_parent` reflejen el nuevo registro).
   - El formulario **limpia los campos** a sus valores iniciales (listo para crear otro privilegio sin duplicar el envío por accidente) y ofrece la opción directa de volver al listado o crear uno nuevo.
2. **Tras Actualizar exitosamente**:
   - Se muestra alerta de éxito (*"Los cambios del privilegio se han guardado exitosamente."*).
   - Se sincronizan los datos mostrados en el formulario y en `AdmPrivState`.
   - El formulario permanece editable en la misma ruta `/admsec/priv/edit` conservando el `SelectedEditPrivilegeId` en memoria mientras permanezca en la vista.
3. **Tras Inactivar exitosamente**:
   - Se muestra alerta de éxito indicando la inactivación y, si aplica, la cantidad de asignaciones en roles trasladadas al histórico (`cascaded_role_privileges`).
   - El formulario pasa a **estado solo lectura / deshabilitado** (`_isDeactivated = true`, estado visual `"Inactivo"`), ocultando el panel de confirmación y deshabilitando los campos y el botón **Guardar Cambios**, dejando visible el botón **"Volver al listado"** (`/admsec/priv`) y `Salir` en `HeaderWork`.
   - Se refresca `AdmPrivState` para que al regresar al listado el cambio esté reflejado inmediatamente.

### 3.4 Vinculación desde `AdmPriv` (`/admsec/priv`)
- En `AdmPrivCapture.razor.cs`: `HandleCreatePrivilegeClick()` limpia `SelectedEditPrivilegeId` en `AdmPrivState` y navega a `/admsec/priv/create`.
- En `AdmPrivList.razor` y `AdmPrivList.razor.cs`:
  - El botón **Editar** está habilitado cuando `item.Status == "A"`; al pulsarlo guarda `item.IdPrivilege` en `AdmPrivState.SetSelectedEditPrivilegeId(item.IdPrivilege)` y navega a `/admsec/priv/edit`.
  - Cuando `item.Status != "A"` (ej. `"I"`), el botón **Editar** aparece deshabilitado (`disabled`) con `title="Solo se pueden editar privilegios activos"`.

---

## 4. Arquitectura y Contratos BFF / MicroID

### 4.1 Contratos Compartidos (`Vogel.Shared/DTOs/PrivilegeAdminDtos.cs`)
Se amplían los DTOs compartidos (agnósticos al proveedor):
- `CreatePrivilegeRequestDto`: `IdParent` (`int?`), `Codename` (`string`), `GroupName` (`string`), `Title` (`string`), `Action` (`string`), `Icon` (`string?`). *(Nota: NO incluye `IdSystem`; el BFF siempre asigna el sistema configurado).*
- `CreatePrivilegeResponseDto`: `IdPrivilege` (`int`), `IdSystem` (`int`), `Status` (`string`), `DateCreation` (`DateTimeOffset`).
- `UpdatePrivilegeRequestDto`: `IdParent` (`int?`), `Codename` (`string`), `GroupName` (`string`), `Title` (`string`), `Action` (`string`), `Icon` (`string?`).
- `UpdatePrivilegeResponseDto`: `IdPrivilege` (`int`), `Updated` (`bool`), `DateModification` (`DateTimeOffset`).
- `DeactivatePrivilegeRequestDto`: `DelDescription` (`string?`).
- `DeactivatePrivilegeResponseDto`: `IdPrivilege` (`int`), `Status` (`string`), `CascadedRolePrivileges` (`int`).

### 4.2 Endpoints BFF (`Vogel.Server/Controllers/PrivilegesController.cs`)
Todos exigen token de sesión (`Authorization: Bearer <token>` o `X-Session-Token`); si falta, retornan `401 Unauthorized`.

| Método y Ruta BFF | Operación en `IPrivilegeAdminProviderService` | Endpoint MicroID (Modo `Api`) | Respuesta Exitosa |
| :--- | :--- | :--- | :--- |
| `GET /api/privileges/{id:int}` | `GetPrivilegeAsync(token, id, ct)` | `POST /priv/get` | `200 OK` (`PrivilegeAdminItemDto`) |
| `POST /api/privileges` | `CreatePrivilegeAsync(token, request, ct)` | `POST /priv/create` | `201 Created` (`CreatePrivilegeResponseDto`) |
| `PUT /api/privileges/{id:int}` | `UpdatePrivilegeAsync(token, id, request, ct)` | `POST /priv/update` | `200 OK` (`UpdatePrivilegeResponseDto`) |
| `DELETE /api/privileges/{id:int}` | `DeactivatePrivilegeAsync(token, id, request, ct)` | `POST /priv/delete` | `200 OK` (`DeactivatePrivilegeResponseDto`) |

*(Todas las rutas mantienen también el prefijo `api/v1/privileges` ya existente en `PrivilegesController`).*

### 4.3 Mapeo de Errores MicroID → HTTP BFF
- `ERR_AUTH_FAILED` → `401 Unauthorized`
- `ERR_INVALID_PAYLOAD` → `400 Bad Request`
- `ERR_INACTIVE_MASTER` → `404 Not Found` o `422 Unprocessable Entity` (mensaje sanitizado indicando que el privilegio o su padre no existe o está inactivo)
- `ERR_SYSTEM_MISMATCH` / jerarquía circular / autorreferencia → `422 Unprocessable Entity`
- `ERR_IDP_COMMUNICATION` / `ERR_INTERNAL_SERVER` → `502 Bad Gateway` (mensaje genérico sin filtrar detalles internos)

### 4.4 Comportamiento del Proveedor `Mock` (`MockPrivilegeAdminService`)
- Mantiene en memoria (con sincronización thread-safe) una copia mutable inicializada a partir de `IMockDataService` para que:
  - `GET /api/privileges` refleje las altas, ediciones e inactivaciones realizadas durante la sesión del servidor.
  - `GET /api/privileges/{id}` retorne el privilegio si existe y está activo (`"A"`), o error `422`/`404` si no existe o está inactivo.
  - `POST /api/privileges` valide campos obligatorios y `id_parent` (existente y activo si `> 0`), asigne el siguiente `IdPrivilege` autoincremental y retorne `201 Created`.
  - `PUT /api/privileges/{id}` valide que exista y esté activo, que `id_parent != id` y sin ciclos ascendentes, actualice campos y retorne `200 OK`.
  - `DELETE /api/privileges/{id}` valide que exista y esté activo, cambie `Status = "I"` y retorne `200 OK` con `CascadedRolePrivileges = 0`.

---

## 5. Requisitos de Seguridad de la Información (InfoSec)
1. **Autenticación obligatoria en BFF**: Todos los endpoints (`GET /{id}`, `POST`, `PUT /{id}`, `DELETE /{id}`) deben exigir token Bearer válido en cabecera; prohibido pasar tokens por query string o almacenarlos en logs.
2. **Prevención de manipulación de `id_system` y exposición de IDs en URL (IDOR / Mass Assignment)**:
   - El cliente **nunca** expone `IdPrivilege` en la URL de navegación (`/admsec/priv/edit` usa estado en memoria `SelectedEditPrivilegeId` y redirige a `/admsec/priv` si se accede directamente o tras recarga F5 sin selección).
   - El cliente **nunca** controla `id_system` en creación ni edición; el BFF inyecta siempre `_options.SystemNumericId` desde configuración.
   - Al consultar (`GET /{id}`), el BFF verifica que el privilegio devuelto pertenezca al `_options.SystemNumericId` configurado (en caso contrario rechaza con `403 Forbidden`).
3. **Validación estricta de entrada (Cliente y Servidor)**:
   - `id > 0` en rutas con parámetro `{id:int}`.
   - `Codename`, `Title`, `GroupName` y `Action` obligatorios (no vacíos ni solo espacios) y con longitudes máximas acotadas (ej. `Codename <= 100`, `Title <= 150`, `GroupName <= 100`, `Action <= 250`, `Icon <= 100`, `DelDescription <= 500`).
   - `IdParent >= 0` (si se especifica), y en edición `IdParent != IdPrivilege`.
4. **Cero URLs hardcodeadas**: Toda comunicación hacia MicroID usa exclusivamente `IMicroIdApiClient` con rutas relativas (`priv/get`, `priv/create`, `priv/update`, `priv/delete`) sobre la `BaseUrl` configurada.
5. **Mensajes de error y logging seguros**: Nunca exponer stack traces, tokens ni detalles internos de infraestructura en respuestas HTTP o alertas de UI.
6. **Auditoría obligatoria**: Al finalizar las pruebas unitarias, el agente **`vogel-infosec-guardian`** debe auditar todos los archivos nuevos y modificados y emitir dictamen aprobatorio antes de la fase de QA/PR.

---

## 6. Criterios de Aceptación

| Código | Criterio de Aceptación |
| :--- | :--- |
| **CA-01** | Al hacer clic en **Crear privilegio** en `/admsec/priv`, la aplicación navega a `/admsec/priv/create` mostrando el formulario con estilo basado en `FormMockView` (`HeaderWork`, `.vogel-form-card`, secciones y botonera). |
| **CA-02** | Al hacer clic en **Editar** en una fila activa (`status == "A"`) de `/admsec/priv`, se almacena el `IdPrivilege` en memoria dentro de `AdmPrivState`, la aplicación navega a la ruta limpia `/admsec/priv/edit` (sin ID en la URL) y precarga los datos actuales del privilegio desde el BFF. |
| **CA-03** | En `/admsec/priv`, los privilegios inactivos (`status == "I"`) muestran el botón **Editar** deshabilitado. |
| **CA-04** | Si un usuario no autenticado intenta acceder directamente a `/admsec/priv/create` o `/admsec/priv/edit`, es redirigido a `/login`. Si un usuario autenticado accede directamente a `/admsec/priv/edit` o presiona F5 sin un ID seleccionado en memoria, es redirigido limpiamente a `/admsec/priv`. |
| **CA-05** | El campo `Privilegio Padre (id_parent)` se presenta como `<select>` con `"0 — Ninguno (Raíz)"` y los privilegios activos del sistema; en modo edición excluye al propio privilegio. |
| **CA-06** | El formulario valida campos obligatorios (`codename`, `title`, `group_name`, `action`) antes de enviar y muestra mensajes claros sin llamar al BFF si hay errores de validación. |
| **CA-07** | Al crear un privilegio válido, el BFF invoca `POST /priv/create` (modo `Api`) o persiste en memoria (modo `Mock`), retorna `201 Created`, el formulario muestra alerta de éxito con el ID generado, limpia los campos y actualiza el catálogo en `AdmPrivState`. |
| **CA-08** | Al editar un privilegio activo válido, el BFF invoca `POST /priv/update` (modo `Api`) o actualiza en memoria (modo `Mock`), retorna `200 OK`, el formulario muestra alerta de éxito y mantiene los valores actualizados. |
| **CA-09** | En modo edición, el usuario puede inactivar el privilegio tras una confirmación explícita (con motivo opcional `del_description`), invocando `DELETE /api/privileges/{id}` (`POST /priv/delete` en MicroID); tras el éxito, se muestra alerta de éxito y el formulario queda en estado solo lectura/deshabilitado con opción de volver a `/admsec/priv`. |
| **CA-10** | Todos los endpoints BFF rechazan peticiones sin token con `401 Unauthorized` y nunca permiten modificar `id_system` desde el cliente. |
| **CA-11** | La solución compila sin errores y todas las pruebas unitarias (existentes y nuevas en `Vogel.Tests`) pasan al 100%. |
| **CA-12** | La auditoría de seguridad (`vogel-infosec-guardian`) y la verificación End-to-End (`vogel-qa-verifier`) concluyen con dictamen aprobatorio. |
