# Plan de Implementación: Control Granular de Permisos (RBAC) en `adm_priv` y Skill de Módulos CRUD

| Campo | Valor |
|---|---|
| **Requerimiento** | `reqs/Requerimiento_AdmPriv_PermisosRBAC.md` |
| **Rama Git** | `adm-priv-rbac-crud-skill` |
| **Fecha de creación** | 2026-11-24 |
| **Estado general** | Aprobado — En ejecución |

---

## Tablero de Seguimiento General

| Fase | Descripción | Agente Responsable | Estado |
|---|---|---|---|
| **Fase 1** | Contratos compartidos (`Vogel.Shared`) y perfiles RBAC en `MockDataService` | `vogel-bff-integration` | ✅ Completada |
| **Fase 2** | Autorización granular RBAC en BFF (`Vogel.Server`) | `vogel-bff-integration` | ✅ Completada |
| **Fase 3** | Restricciones RBAC en Frontend Blazor (`Vogel.Client`) e ícono `view.svg` | `vogel-blazor-client` | ✅ Completada |
| **Fase 4** | Pruebas Unitarias e Integración (`Vogel.Tests`) | `vogel-bff-integration` / `vogel-blazor-client` | 🔄 En progreso |
| **Fase 5** | Auditoría de Seguridad de la Información (InfoSec) | `vogel-infosec-guardian` | ⬜ Pendiente |
| **Fase 6** | Verificación QA End-to-End (`Api` MicroID + `Mock`) | `vogel-qa-verifier` | ⬜ Pendiente |
| **Fase 7** | Creación del Skill dedicado para módulos CRUD (`.agents/skills/vogel-crud-module/SKILL.md`) | `vogel-req-analyst` | ⬜ Pendiente |
| **Fase 8** | Archivo en `VogelTestMicroID-Docs`, limpieza de `reqs/` y Pull Request hacia `main` | `vogel-req-analyst` | ⬜ Pendiente |

---

## Detalle de Fases y Tareas

### Fase 1: Contratos Compartidos y Datos Mock (`Vogel.Shared`)
- [x] **T1.1** Crear el enum `PrivilegeAction` (`Read`, `Create`, `Modify`, `Delete`, `Validate`, `Execute`, `Approve`) en `Vogel.Shared/Enums/PrivilegeAction.cs`.
- [x] **T1.2** Incorporar el privilegio `adm_priv` en `Vogel.Shared/Services/MockDataService.cs` (con permisos completos para `admin` y `jdoe`) y agregar usuarios mock de prueba con combinaciones RBAC (`readonly_user`, `read_delete_user`, `create_only_user`) sin alterar el comportamiento de pruebas existentes.
- [x] **T1.3** Verificar compilación de `Vogel.Shared`.

### Fase 2: Autorización Granular RBAC en BFF (`Vogel.Server`)
- [x] **T2.1** Crear la abstracción `IPrivilegeAuthorizationService` y `PrivilegeAuthorizationResult` en `Vogel.Server/Services/IPrivilegeAuthorizationService.cs`.
- [x] **T2.2** Implementar `PrivilegeAuthorizationService` (Scoped) en `Vogel.Server/Services/PrivilegeAuthorizationService.cs` delegando en `IAuthProviderService.GetPrivilegesAsync(token)`, con comportamiento estricto *fail-closed*, logging seguro y respuesta `403 Forbidden`.
- [x] **T2.3** Actualizar `MockAuthProviderService` (con registro de sesiones mock en memoria) para que `GetPrivilegesAsync(token)` retorne los privilegios reales del usuario autenticado con ese token, preservando compatibilidad con pruebas existentes.
- [x] **T2.4** Integrar `IPrivilegeAuthorizationService` en `PrivilegesController` validando los permisos de `adm_priv` en cada endpoint:
  - `List` (`GET /api/privileges`) → `PrivilegeAction.Read`
  - `GetById` (`GET /api/privileges/{id}`) → `PrivilegeAction.Read`
  - `Create` (`POST /api/privileges`) → `PrivilegeAction.Create`
  - `Update` (`PUT /api/privileges/{id}`) → `PrivilegeAction.Modify`
  - `Deactivate` (`DELETE /api/privileges/{id}`) → `PrivilegeAction.Delete`
  - `Reactivate` (`POST /api/privileges/{id}/reactivate`) → `PrivilegeAction.Create`
- [x] **T2.5** Registrar `IPrivilegeAuthorizationService` (y el soporte de sesiones mock) en `Vogel.Server/Program.cs` y verificar compilación de `Vogel.Server`.

### Fase 3: Restricciones RBAC en Frontend Blazor (`Vogel.Client`)
- [x] **T3.1** Agregar `GetPrivilege(string codename)` y `HasPrivilegeAccess(string codename, PrivilegeAction action)` en `Vogel.Client/Services/SessionState.cs` (evaluación *fail-closed*).
- [x] **T3.2** Crear el ícono vectorial `Vogel.Client/wwwroot/icons/view.svg` para el botón **Ver detalle**.
- [x] **T3.3** Actualizar `AdmPriv.razor.cs` para omitir `AdmPrivState.LoadAsync()` cuando `!CanRead` y limpiar cualquier dato residual.
- [x] **T3.4** Actualizar `AdmPrivCapture.razor` y `AdmPrivCapture.razor.cs` para renderizar el botón **Crear privilegio** exclusivamente cuando `CanCreate` (ausente del DOM si `false`), con guard defensivo en `HandleCreatePrivilegeClick`.
- [x] **T3.5** Actualizar `AdmPrivList.razor`, `AdmPrivList.razor.cs` y `AdmPrivList.razor.css` para:
  - Mostrar aviso de acceso de lectura restringido si `!CanRead`.
  - En filas activas: renderizar botón **Editar** (`icons/edit.svg`) si `CanModify`, o botón **Ver detalle** (`icons/view.svg`) si `CanRead && !CanModify`, o ningún botón en caso contrario.
  - En filas inactivas: renderizar botón **Reactivar** (`icons/reactivate.svg`) exclusivamente si `CanCreate` (ausente del DOM si `false`).
- [x] **T3.6** Actualizar `AdmPrivForm.razor`, `AdmPrivForm.razor.cs` y `AdmPrivStateService.cs` para:
  - Bloquear acceso directo a `/admsec/priv/create` si `!CanCreate` y a `/admsec/priv/edit` si `!CanRead` (redirigiendo a `/admsec/priv` con mensaje de error).
  - Evitar la llamada a `AdmPrivState.LoadAsync()` en creación cuando `!CanRead`.
  - Soportar modo solo lectura (`IsReadOnlyView = IsEditMode && !CanModify`): título `Detalle de Privilegio`, campos deshabilitados, botón **Guardar Cambios** ausente del DOM.
  - Renderizar la sección **Inactivación del Privilegio** exclusivamente cuando `IsEditMode && CanDelete && !IsDeactivated && !LoadFailed` (habilitada también en modo solo lectura si `CanRead && !CanModify && CanDelete`).
  - Manejar respuestas `403 Forbidden` sin cerrar la sesión.
- [x] **T3.7** Verificar compilación completa de la solución (`dotnet build`).

### Fase 4: Pruebas Unitarias e Integración (`Vogel.Tests`)
- [ ] **T4.1** Agregar pruebas unitarias para `PrivilegeAuthorizationService` (permiso concedido, denegado `403`, privilegio ausente, error del proveedor *fail-closed*, propagación `401`).
- [ ] **T4.2** Actualizar y ampliar pruebas unitarias de `PrivilegesController` verificando los 6 endpoints con permiso concedido y denegado (`403 Forbidden`), incluyendo `Reactivate` gobernado por `Create`.
- [ ] **T4.3** Agregar pruebas unitarias para `SessionState.HasPrivilegeAccess` y `MockAuthProviderService` con perfiles RBAC diferenciados.
- [ ] **T4.4** Ejecutar `dotnet test` y verificar 100% de pruebas en verde.

### Fase 5: Auditoría de Seguridad de la Información (`vogel-infosec-guardian`)
- [ ] **T5.1** Ejecutar auditoría completa con `vogel-infosec-guardian` verificando OWASP A01 (Broken Access Control), *fail-closed*, ausencia de controles no autorizados en DOM, ausencia de URLs hardcodeadas, sanitización de logs y no-filtración de errores.
- [ ] **T5.2** Resolver cualquier hallazgo bloqueante o de severidad media/alta y registrar el dictamen final.

### Fase 6: Verificación QA End-to-End (`vogel-qa-verifier`)
- [ ] **T6.1** Ejecutar verificación QA End-to-End en modo `Mock` probando combinaciones de permisos (`admin` completo, `readonly_user`, `read_delete_user`, `create_only_user`), inspeccionando el DOM real para confirmar ausencia de botones/controles no autorizados y verificando `403 Forbidden` en BFF.
- [ ] **T6.2** Ejecutar verificación QA End-to-End en modo `Api` contra MicroID real comprobando la validación en tiempo real vía `POST /auth` y la consistencia UI + BFF.
- [ ] **T6.3** Documentar resultados de QA y confirmar cumplimiento de `CA-01` a `CA-11`.

### Fase 7: Creación del Skill Dedicado para Módulos CRUD
- [ ] **T7.1** Crear `.agents/skills/vogel-crud-module/SKILL.md` documentando el estándar completo para módulos CRUD en Vogel (contratos, proveedores `Api`/`Mock`, autorización RBAC en BFF y Front, vistas `WorkViewFrame`/`ViewSet`/Form, íconos SVG, pruebas unitarias, auditoría InfoSec y QA).
- [ ] **T7.2** Validar que el skill refleje fielmente el patrón implementado y no contenga secretos ni URLs hardcodeadas.

### Fase 8: Archivo Documental, Limpieza de `reqs/` y Pull Request
- [ ] **T8.1** Archivar `Requerimiento_AdmPriv_PermisosRBAC.md`, `Plan_Implementacion_AdmPriv_PermisosRBAC.md` (con todas las tareas completadas) y `Cierre_RFC.md` en `VogelTestMicroID-Docs/rfc/rfc_YYYYMMDD_adm_priv_rbac_crud_skill/`, actualizando `README.md` y haciendo `commit` + `push`.
- [ ] **T8.2** Eliminar los 2 archivos temporales de `reqs/` en la rama `adm-priv-rbac-crud-skill`.
- [ ] **T8.3** Subir la rama y abrir el Pull Request hacia `main`.

---

## Registro de Seguimiento

| Fecha | Fase | Nota |
|---|---|---|
| 2026-11-24 | Levantamiento | Rama `adm-priv-rbac-crud-skill` creada desde `main`. Documentos de requerimiento y plan generados en `reqs/` y aprobados por el usuario para iniciar ejecución. |
| 2026-11-24 | Fase 1 (`T1.1`–`T1.3`) | Creado enum `PrivilegeAction` en `Vogel.Shared/Enums/PrivilegeAction.cs` e incorporados `adm_priv` + perfiles mock RBAC (`readonly_user`, `read_delete_user`, `create_only_user`) en `MockDataService.cs`. Build verde (0 errores/0 warnings) y 149/149 tests en verde. |
| 2026-11-24 | Fase 2 (`T2.1`–`T2.5`) | Creados `IPrivilegeAuthorizationService`, `PrivilegeAuthorizationService` (fail-closed) y `MockSessionStore`; actualizados `MockAuthProviderService`, `PrivilegesController` (6 endpoints con autorización `adm_priv`) y `Program.cs`. Build `Vogel.Server` verde (0 errores, 0 warnings). |
| 2026-11-24 | Fase 3 (`T3.1`–`T3.7`) | Agregado `SessionState.HasPrivilegeAccess`, ícono `view.svg` y restricciones RBAC estrictas en DOM/navegación en `AdmPriv`, `AdmPrivCapture`, `AdmPrivList`, `AdmPrivForm` y `AdmPrivStateService` (incluyendo modo solo lectura y sección `delete` independiente). Build `Vogel.Client` verde (0 errores, 0 warnings). |
