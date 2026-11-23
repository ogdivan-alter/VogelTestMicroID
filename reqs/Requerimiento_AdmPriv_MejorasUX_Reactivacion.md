# Requerimiento: Mejoras UX y Reactivación en Administración de Privilegios (Issues #3, #4 y #6)

- **Módulo:** Administración de Privilegios (`AdmPriv` — `/admsec/priv`)
- **Rama de trabajo:** `adm-priv-ux-reactivate` (creada desde `main`)
- **Issues consolidados:**
  - **#3** — *Admistración de Privilegios: UX Filtro*
  - **#4** — *Admnistración de Privilegios - Botones*
  - **#6** — *Administración de privilegios - Opción de reactivar*
- **Fecha:** 2026-11-23
- **Estado:** Aprobado

---

## 1. Propósito

Consolidar e implementar tres mejoras funcionales y de experiencia de usuario (UX) en la vista de Administración de Privilegios (`/admsec/priv`):

1. **Reorganización visual del panel de filtros (`#3`):** Apilar verticalmente los controles de filtro (`Estado`, `Título`, `ID_PARENT`) asignando a cada fila su propia etiqueta identificadora a la izquierda.
2. **Estandarización de botones e íconos SVG (`#4`):** Alinear a la derecha el botón principal de la barra `Capture` siguiendo la convención visual del botón `Salir` de `HeaderWork` (`FormMockView`), estandarizar los botones de acción de la grilla `List` en formato **solo ícono con tooltip**, y alojar todos los SVGs como archivos independientes en `Vogel.Client/wwwroot/icons/`.
3. **Reactivación lógica de privilegios inactivos (`#6`):** Reemplazar el botón `Editar` deshabilitado en registros inactivos (`Status == "I"`) por una acción **Reactivar** que restaure `Status = "A"` integrándose de extremo a extremo con el BFF (`Vogel.Server`), el nuevo endpoint `POST /priv/reactivate` de MicroID (modo `Api`) y `MockPrivilegeAdminService` (modo `Mock`).

---

## 2. Alcance

### 2.1 Incluido
- **Frontend (`Vogel.Client` — esquema Code-Behind `.razor` + `.razor.cs`):**
  - `AdmPrivFilter.razor` / `AdmPrivFilter.razor.css` / `AdmPrivFilter.razor.cs`: Diseño apilado vertical con filas `LABEL — Control` para `ESTADO`, `TÍTULO` e `ID_PARENT`.
  - `AdmPrivCapture.razor` / `AdmPrivCapture.razor.cs`: Botón **Crear privilegio** alineado a la derecha, siguiendo la convención visual de `HeaderWork` (estilo oscuro/traslúcido con borde sutil y hover coherente) y consumiendo el SVG desde `wwwroot/icons/plus.svg`.
  - `AdmPrivList.razor` / `AdmPrivList.razor.cs`:
    - Registros con `Status == "A"`: mostrar únicamente el botón **Editar** (solo ícono `wwwroot/icons/edit.svg`, sin texto visible, con `title="Editar privilegio"` y `aria-label="Editar privilegio"`).
    - Registros con `Status == "I"`: ocultar el botón `Editar` y mostrar en su lugar el botón **Reactivar** (solo ícono `wwwroot/icons/reactivate.svg`, sin texto visible, con `title="Reactivar privilegio"` y `aria-label="Reactivar privilegio"`).
    - Protección contra doble clic por fila (`ReactivatingPrivilegeId`), estado visual de carga y mensajes claros de éxito o error en la vista de lista.
  - `IAdmPrivStateService.cs` / `AdmPrivStateService.cs`: Nuevo método `ReactivateAsync(int id)` que invoca `POST api/privileges/{id}/reactivate`, refresca el catálogo local en caso de éxito y expone `SuccessMessage` / `ErrorMessage` controlado.
  - Nuevos archivos SVG en `Vogel.Client/wwwroot/icons/`: `plus.svg`, `edit.svg`, `reactivate.svg`.
- **Contratos compartidos (`Vogel.Shared`):**
  - Nuevo DTO `ReactivatePrivilegeResponseDto` en `PrivilegeAdminDtos.cs`.
- **Backend BFF (`Vogel.Server`):**
  - `IPrivilegeAdminProviderService.cs`: Nuevo resultado `ReactivatePrivilegeResult` y método `ReactivatePrivilegeAsync(string token, int idPrivilege, CancellationToken cancellationToken = default)`.
  - `PrivilegesController.cs`: Nuevo endpoint agnóstico `POST api/privileges/{id:int}/reactivate` (con alias `api/v1/privileges/{id:int}/reactivate`) delegando exclusivamente en `IPrivilegeAdminProviderService`.
  - `MicroIdPrivilegeModels.cs`: Nuevos records `MicroIdReactivatePrivilegeRequest` y `MicroIdReactivatePrivilegeResponse`.
  - `MicroIdPrivilegeAdminService.cs`: Implementación de `ReactivatePrivilegeAsync` conectada a `POST /priv/reactivate` (modo `Api`, sin URLs hardcodeadas) con validación previa de pertenencia al sistema configurado (`IdSystem`).
  - `MockPrivilegeAdminService.cs`: Implementación completa de `ReactivatePrivilegeAsync` en modo `Mock`, replicando las validaciones de negocio de MicroID (existencia, estado inactivo y padre activo).
- **Pruebas automatizadas (`Vogel.Tests`):**
  - Pruebas unitarias para `PrivilegesController`, `MicroIdPrivilegeAdminService`, `MockPrivilegeAdminService` y `AdmPrivStateService`.
- **Seguridad y QA:**
  - Auditoría obligatoria con `vogel-infosec-guardian`.
  - Verificación End-to-End con `vogel-qa-verifier` en modo `Api` (contra MicroID real en `http://localhost:8021`) y en modo `Mock`.

### 2.2 Fuera de alcance
- Modificaciones en el repositorio externo `MicroID` (`F:\LABORATORIO\2026\MICROID` es estrictamente de solo lectura; ya expone `POST /priv/reactivate`).
- Restauración automática de asignaciones históricas `role_privilege_hst` al reactivar un privilegio (MicroID únicamente reactiva el maestro en `privilege` cambiando `status = 'A'`).
- Cambios en otros módulos distintos a Administración de Privilegios (`AdmPriv`).

---

## 3. Diseño de UI y Arquitectura

### 3.1 Panel de Filtros Apilados (`AdmPrivFilter` — Issue #3)
Se cambia la distribución horizontal actual por una grilla/flex vertical apilada de 3 filas uniformes (`label` alineado a la izquierda con ancho consistente + control a la derecha):

```text
ESTADO    [ (o) Activos   ( ) Inactivos   ( ) Todos ]
TÍTULO    [ 🔍 Filtrar por título (title)...     (x) ]
ID_PARENT [ Filtrar por id_parent...             (x) ]
```

- Cada `<label>` estará asociado semánticamente a su control (`for` / `id` en inputs, `id` + `aria-labelledby` en el `radiogroup`).
- Se mantiene intacta la lógica reactiva de filtrado en `AdmPrivStateService` (`StatusFilter`, `TitleFilter`, `IdParentFilter`).
- En pantallas pequeñas (`max-width: 768px`), cada fila se adapta limpiamente apilando etiqueta sobre control sin desbordamientos horizontales.

### 3.2 Convención de Botones e Íconos SVG (`AdmPrivCapture` y `AdmPrivList` — Issue #4)
- **Carpeta de íconos (`Vogel.Client/wwwroot/icons/`):**
  - `plus.svg`: ícono `+` para el botón **Crear privilegio** en `AdmPrivCapture`.
  - `edit.svg`: ícono de lápiz/edición para el botón **Editar** en `AdmPrivList`.
  - `reactivate.svg`: ícono de flecha circular / restauración para el botón **Reactivar** en `AdmPrivList`.
  - Los SVGs deben ser limpios (sin `<script>`, sin eventos inline `onload`/`onclick`, sin referencias externas) y renderizarse mediante `<img src="icons/..." alt="" aria-hidden="true" class="..." />` o CSS mask para respetar la paleta del tema y mantener el color en hover.
- **Barra `AdmPrivCapture.razor`:**
  - Contenedor alineado a la derecha (`justify-content: flex-end`).
  - El botón **Crear privilegio** adopta la convención visual del botón `Salir` de `HeaderWork` (`FormMockView`): fondo oscuro/traslúcido, borde sutil `1px solid rgba(255, 255, 255, 0.25)`, tipografía `0.875rem` semibold, transición suave y acento en hover.
- **Botones de acción en `AdmPrivList.razor`:**
  - Mantienen la convención de botones de la aplicación (estilo compacto de acción en tabla, cuadrado/simétrico, **solo ícono sin texto**).
  - Tooltip nativo (`title`) y atributo de accesibilidad (`aria-label`) obligatorios:
    - Activo (`Status == "A"`): ícono `edit.svg`, `title="Editar privilegio"`, `aria-label="Editar privilegio"`.
    - Inactivo (`Status == "I"`): ícono `reactivate.svg`, `title="Reactivar privilegio"`, `aria-label="Reactivar privilegio"`.

### 3.3 Flujo de Reactivación en `AdmPrivList` (Issue #6)
1. El usuario filtra por **Inactivos** (`I`) o **Todos** (`ALL`).
2. En cada fila con `Status == "I"`, no se renderiza el botón `Editar`; en su lugar se renderiza el botón `Reactivar` (solo ícono + tooltip).
3. Al hacer clic en `Reactivar`:
   - `AdmPrivList.razor.cs` bloquea el botón de esa fila (`ReactivatingPrivilegeId = item.IdPrivilege`) para evitar solicitudes duplicadas.
   - Invoca `AdmPrivState.ReactivateAsync(item.IdPrivilege)`.
   - El servicio cliente envía `POST api/privileges/{id}/reactivate` al BFF con el token de sesión del usuario autenticado.
   - Si el BFF responde `200 OK`:
     - `AdmPrivStateService` recarga el catálogo (`LoadAsync()`) y publica un mensaje de confirmación (p. ej. `"Privilegio reactivado exitosamente."`).
     - El registro actualizado pasa a `Status == "A"` (si el filtro activo es `Inactivos`, desaparece de la lista filtrada; si es `Todos`, pasa a mostrar el checkbox marcado y el botón `Editar`).
   - Si el BFF responde error (`400`, `401`, `422`, `502`/`503`, etc., por ejemplo cuando el privilegio padre sigue inactivo):
     - Se muestra un mensaje de error claro y sanitizado en `AdmPrivList` sin alterar el estado local del registro.

---

## 4. Contratos BFF y MicroID

### 4.1 Endpoint BFF (`Vogel.Server/Controllers/PrivilegesController.cs`)
- **Rutas:**
  - `POST /api/privileges/{id:int}/reactivate`
  - `POST /api/v1/privileges/{id:int}/reactivate`
- **Autenticación:** Requiere cabecera `Authorization: Bearer <token>` o `X-Session-Token: <token>`.
- **Body de solicitud:** Vacío (no se requiere payload; el identificador viaja como parámetro de ruta `id > 0`).
- **Respuesta exitosa (`200 OK`) — `ReactivatePrivilegeResponseDto`:**
  ```json
  {
    "idPrivilege": 12,
    "status": "A",
    "dateModification": "2026-11-23T01:07:10Z"
  }
  ```
- **Respuestas de error normalizadas (`BackendErrorDto`):**
  - `400 Bad Request`: `id <= 0` o payload/estado inválido (p. ej. el privilegio ya se encuentra activo).
  - `401 Unauthorized`: token de sesión ausente o inválido/expirado en MicroID.
  - `403 Forbidden`: usuario sin autorización para ejecutar la operación en MicroID.
  - `422 Unprocessable Entity`: privilegio inexistente en el sistema configurado, sistema inactivo o privilegio padre (`id_parent`) inactivo.
  - `502 Bad Gateway` / `503 Service Unavailable`: falla de comunicación con el proveedor externo.

### 4.2 Contrato Real con MicroID (`F:\LABORATORIO\2026\MICROID` — Solo Lectura)
- **Endpoint MicroID:** `POST /priv/reactivate` (resuelto a partir de la URL base configurada en `MicroId:BaseUrl`, cero URLs hardcodeadas).
- **Request (`RequestMessage<MicroIdReactivatePrivilegeRequest>`):**
  ```json
  {
    "system": "<MicroId:SystemId>",
    "token": "<session_token>",
    "data": {
      "id_privilege": 12
    }
  }
  ```
- **Response (`ResponseMessage<MicroIdReactivatePrivilegeResponse>`):**
  ```json
  {
    "status": "OK",
    "data": {
      "id_privilege": 12,
      "status": "A",
      "date_modification": "2026-11-23T01:07:10Z"
    }
  }
  ```
- **Validaciones conocidas de MicroID (`PrivilegeService.ReactivatePrivilegeAsync`):**
  1. `id_privilege <= 0` → `InvalidPayloadException` (400).
  2. Privilegio no existe → `InactiveMasterException` (422).
  3. Privilegio ya está activo (`status == 'A'`) → `BusinessRuleException(ERR_INVALID_PAYLOAD)` (400/422).
  4. Sistema al que pertenece está inactivo → `InactiveMasterException` (422).
  5. Si `id_parent != null` y el padre no existe o está inactivo → `InactiveMasterException` (422).

### 4.3 Comportamiento en Modo `Mock` (`MockPrivilegeAdminService`)
- Valida `token` no vacío (`401`) e `idPrivilege > 0` (`400`).
- Verifica existencia en el catálogo en memoria (`422` si no existe).
- Verifica que el privilegio esté inactivo (`Status != "A"`; si ya está activo, retorna `400`).
- Verifica que si `IdParent > 0`, el privilegio padre exista y esté activo (`Status == "A"`; de lo contrario retorna `422`).
- Cambia `Status = "A"` bajo `_syncLock` y retorna `200 OK` con `ReactivatePrivilegeResponseDto`.

---

## 5. Requisitos de Seguridad de la Información (InfoSec)

1. **Prevención de BOLA / IDOR entre sistemas (Crítico):**
   - `POST /priv/get` en MicroID rechaza registros inactivos, por lo que no puede utilizarse para el *ownership check* de reactivación.
   - Antes de invocar `POST /priv/reactivate`, `MicroIdPrivilegeAdminService` debe verificar que `idPrivilege` pertenece al `IdSystem` configurado en el BFF consultando el listado del sistema (`ListPrivilegesAsync(token, status: "I", cancellationToken)`). Si el ID no pertenece al sistema configurado o no se encuentra inactivo, el BFF debe rechazar la operación con `422 Unprocessable Entity` sin reenviar la solicitud a MicroID.
2. **Desacoplamiento y Cero URLs Hardcodeadas:**
   - `PrivilegesController` solo conoce `IPrivilegeAdminProviderService` (sin referencias a MicroID).
   - La ruta relativa `priv/reactivate` se resuelve exclusivamente contra el `HttpClient` configurado por `MicroId:BaseUrl` (*fail-fast*).
3. **Autenticación y Manejo de Errores:**
   - El endpoint BFF exige obligatoriamente token de sesión (`Authorization: Bearer` o `X-Session-Token`).
   - Los errores de MicroID se mapean a mensajes controlados en `BackendErrorDto` sin exponer *stack traces*, detalles internos ni el token de sesión en logs o respuestas.
4. **Sanitización de Logs y Assets SVG:**
   - Ningún token ni dato sensible se escribe en logs; los identificadores de texto se sanitizan con `SanitizeForLog`.
   - Los archivos SVG en `wwwroot/icons/` deben contener únicamente primitivas vectoriales seguras (sin `<script>`, `foreignObject` ni manejadores de eventos).
5. **Auditoría Obligatoria:**
   - Todo cambio debe pasar la auditoría de seguridad de `vogel-infosec-guardian` antes de considerarse listo para QA.

---

## 6. Criterios de Aceptación

- **CA-01 (Issue #3 — Filtros apilados):** En `/admsec/priv`, el panel de filtros muestra los tres criterios apilados verticalmente, cada uno con su etiqueta visible a la izquierda (`ESTADO`, `TÍTULO`, `ID_PARENT`) y su control correspondiente a la derecha.
- **CA-02 (Issue #3 — Funcionalidad de filtros preservada):** Filtrar por estado (`Activos`, `Inactivos`, `Todos`), por texto en `Título` (ignorecase) y por `id_parent` numérico sigue funcionando de forma combinada y reactiva, manteniendo los botones de limpiar (`×`).
- **CA-03 (Issue #4 — Botón Capture alineado y con SVG en `icons`):** En `AdmPrivCapture`, el botón **Crear privilegio** aparece alineado a la derecha, sigue la convención visual del botón `Salir` de `HeaderWork` (`FormMockView`), y carga su ícono desde un archivo `.svg` alojado en `Vogel.Client/wwwroot/icons/`.
- **CA-04 (Issue #4 — Botones de lista solo ícono con tooltip):** En `AdmPrivList`, los botones de acción (`Editar` y `Reactivar`) siguen la convención de botones de la aplicación, muestran únicamente su ícono `.svg` (alojado en `Vogel.Client/wwwroot/icons/`) sin texto visible, y poseen `title` (tooltip) y `aria-label` descriptivos.
- **CA-05 (Issue #6 — Visibilidad condicional Editar / Reactivar):**
  - Para registros con `Status == "A"`, la columna de acciones muestra únicamente el botón **Editar** habilitado (que navega a `/admsec/priv/edit`).
  - Para registros con `Status == "I"`, el botón **Editar** NO se renderiza y en su lugar se muestra el botón **Reactivar**.
- **CA-06 (Issue #6 — Reactivación exitosa E2E en modo `Api` y modo `Mock`):** Al hacer clic en **Reactivar** sobre un privilegio inactivo cuyo padre (si tiene) está activo, el BFF invoca `POST /api/privileges/{id}/reactivate` → proveedor activo (`POST /priv/reactivate` en MicroID o actualización en `MockPrivilegeAdminService`), cambia `Status` a `'A'`, refresca el listado y muestra feedback visual de éxito.
- **CA-07 (Issue #6 — Manejo de reglas de negocio y errores):** Si se intenta reactivar un privilegio cuyo padre sigue inactivo, que ya está activo o que no pertenece al sistema configurado, el BFF retorna el código HTTP correspondiente (`400`/`422`) y la UI muestra un mensaje de error claro sin corromper el estado de la lista.
- **CA-08 (Arquitectura, Seguridad y Pruebas):**
  - Se respeta el patrón Code-Behind (`.razor` + `.razor.cs`), desacoplamiento de `PrivilegesController` y cero URLs hardcodeadas.
  - La suite `dotnet test` pasa al 100% con nuevas pruebas unitarias cubriendo `Reactivate` en controlador, servicios `Api`/`Mock` y estado cliente.
  - `vogel-infosec-guardian` aprueba los cambios sin hallazgos bloqueantes y `vogel-qa-verifier` valida E2E contra MicroID real (`http://localhost:8021`) y modo `Mock`.
