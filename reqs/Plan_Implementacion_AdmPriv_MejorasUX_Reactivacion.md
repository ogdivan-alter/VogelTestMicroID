# Plan de Implementación: Mejoras UX y Reactivación en Administración de Privilegios (Issues #3, #4 y #6)

- **Requerimiento asociado:** [`Requerimiento_AdmPriv_MejorasUX_Reactivacion.md`](./Requerimiento_AdmPriv_MejorasUX_Reactivacion.md)
- **Rama de trabajo:** `adm-priv-ux-reactivate`
- **Issues cubiertos:** `#3`, `#4`, `#6`
- **Fecha de creación:** 2026-11-23

---

## Tablero de Seguimiento General

| Fase | Descripción | Estado | Responsable |
| :--- | :--- | :---: | :--- |
| **Fase 1** | Contratos compartidos (`Vogel.Shared`) y Backend BFF (`Vogel.Server` — `Api` y `Mock`) | ✅ Completada | Backend |
| **Fase 2** | Assets SVG (`wwwroot/icons/`), Estado Cliente y Mejoras UI/UX (`Vogel.Client` — Issues #3, #4, #6) | 🔄 En progreso | Frontend |
| **Fase 3** | Pruebas Unitarias y de Integración (`Vogel.Tests`) | ⏳ Pendiente | Backend / Frontend |
| **Fase 4** | Auditoría de Seguridad de la Información (`vogel-infosec-guardian`) | ⏳ Pendiente | InfoSec |
| **Fase 5** | Verificación QA End-to-End (`vogel-qa-verifier` — Modo `Api` MicroID real y Modo `Mock`) | ⏳ Pendiente | QA |

---

## Fase 1 — Contratos Compartidos y Backend BFF (`Vogel.Shared`, `Vogel.Server`)

- [x] **T1.1** Agregar `ReactivatePrivilegeResponseDto` en `Vogel.Shared/DTOs/PrivilegeAdminDtos.cs` (`IdPrivilege`, `Status`, `DateModification`).
- [x] **T1.2** Agregar `ReactivatePrivilegeResult` y el método `ReactivatePrivilegeAsync(string token, int idPrivilege, CancellationToken cancellationToken = default)` en `Vogel.Server/Services/IPrivilegeAdminProviderService.cs`.
- [x] **T1.3** Agregar los modelos `MicroIdReactivatePrivilegeRequest` y `MicroIdReactivatePrivilegeResponse` en `Vogel.Server/Models/MicroId/MicroIdPrivilegeModels.cs` alineados al contrato real de MicroID (`POST /priv/reactivate`).
- [x] **T1.4** Implementar `ReactivatePrivilegeAsync` en `Vogel.Server/Services/MicroId/MicroIdPrivilegeAdminService.cs`:
  - Validar token (`401`) e `idPrivilege > 0` (`400`).
  - Ejecutar *ownership check* contra el `IdSystem` configurado mediante `ListPrivilegesAsync(token, status: "I", cancellationToken)` (prevención de BOLA/IDOR sin usar `/priv/get`).
  - Invocar `POST /priv/reactivate` usando el `HttpClient` configurado por `MicroId:BaseUrl` (cero URLs hardcodeadas) con `RequestMessage<MicroIdReactivatePrivilegeRequest>`.
  - Mapear errores de MicroID (`400`, `401`, `403`, `422`, `502`/`503`) a `BackendErrorDto` normalizado y sanitizar logs.
- [x] **T1.5** Implementar `ReactivatePrivilegeAsync` en `Vogel.Server/Services/MockPrivilegeAdminService.cs` replicando las validaciones de negocio (token, `id > 0`, existencia, estado inactivo, padre activo si `IdParent > 0`) y cambiando `Status = "A"` de forma thread-safe (`_syncLock`).
- [x] **T1.6** Exponer el endpoint `POST api/privileges/{id:int}/reactivate` (con alias `api/v1/privileges/{id:int}/reactivate`) en `Vogel.Server/Controllers/PrivilegesController.cs` delegando exclusivamente en `IPrivilegeAdminProviderService`.

---

## Fase 2 — Assets SVG, Estado Cliente y Mejoras UI/UX (`Vogel.Client`)

- [ ] **T2.1** Crear los archivos SVG limpios (sin scripts ni eventos inline) en `Vogel.Client/wwwroot/icons/`:
  - `plus.svg` (Crear privilegio en `AdmPrivCapture`).
  - `edit.svg` (Editar privilegio en `AdmPrivList`).
  - `reactivate.svg` (Reactivar privilegio en `AdmPrivList`).
- [ ] **T2.2** Extender `IAdmPrivStateService` y `AdmPrivStateService`:
  - Agregar `ReactivateAsync(int id)` llamando a `POST api/privileges/{id}/reactivate` con cabecera de sesión.
  - Recargar el catálogo local (`LoadAsync()`) tras reactivación exitosa y manejar mensajes de éxito/error limpios.
- [ ] **T2.3** Refactorizar `AdmPrivFilter.razor` y `AdmPrivFilter.razor.css` (Issue #3):
  - Apilar verticalmente las 3 opciones de filtro con etiquetas explícitas a la izquierda (`ESTADO`, `TÍTULO`, `ID_PARENT`) y controles a la derecha.
  - Verificar asociación semántica `for`/`id` y `aria-labelledby`, comportamiento responsive y botones limpiar (`×`).
- [ ] **T2.4** Refactorizar `AdmPrivCapture.razor` y estilos asociados (Issue #4):
  - Alinear a la derecha el botón **Crear privilegio**.
  - Aplicar la convención visual del botón `Salir` de `HeaderWork` (`FormMockView`) y referenciar el SVG desde `wwwroot/icons/plus.svg`.
- [ ] **T2.5** Refactorizar `AdmPrivList.razor` y `AdmPrivList.razor.cs` (Issues #4 y #6):
  - Para registros activos (`Status == "A"`): renderizar únicamente el botón **Editar** con patrón de botón de lista (solo ícono `icons/edit.svg`, sin texto, `title="Editar privilegio"`, `aria-label="Editar privilegio"`).
  - Para registros inactivos (`Status == "I"`): ocultar **Editar** y renderizar el botón **Reactivar** (solo ícono `icons/reactivate.svg`, sin texto, `title="Reactivar privilegio"`, `aria-label="Reactivar privilegio"`).
  - Implementar `HandleReactivateClickAsync(PrivilegeAdminItemDto item)` en Code-Behind con protección contra doble clic por fila (`ReactivatingPrivilegeId`), manejo de errores y alerta de éxito/error descartable.

---

## Fase 3 — Pruebas Unitarias y Compilación (`Vogel.Tests`)

- [ ] **T3.1** Agregar pruebas en `Vogel.Tests/PrivilegesControllerTests.cs` para `POST /api/privileges/{id}/reactivate` (sin token → `401`, `id <= 0` → `400`, éxito → `200`, propagación de error de negocio → `422`).
- [ ] **T3.2** Agregar pruebas en `Vogel.Tests/PrivilegeAdminServicesTests.cs` para `MicroIdPrivilegeAdminService.ReactivatePrivilegeAsync` (éxito, ownership check BOLA/IDOR, padre inactivo `422`, ya activo `400`) y `MockPrivilegeAdminService.ReactivatePrivilegeAsync` (éxito, inexistente, ya activo, padre inactivo).
- [ ] **T3.3** Agregar pruebas en `Vogel.Tests/AdmPrivStateServiceTests.cs` para `ReactivateAsync` (éxito con recarga del catálogo, manejo de error `422` y sin token).
- [ ] **T3.4** Ejecutar `dotnet build` y `dotnet test` verificando 0 errores y 100% de pruebas en verde.

---

## Fase 4 — Auditoría de Seguridad de la Información (`vogel-infosec-guardian`)

- [ ] **T4.1** Invocar a `vogel-infosec-guardian` para auditar:
  - Prevención de BOLA/IDOR en `ReactivatePrivilegeAsync` (ownership check contra `IdSystem` configurado).
  - Desacoplamiento en `PrivilegesController` y ausencia de URLs hardcodeadas.
  - Manejo seguro de tokens, sanitización de logs y validación de archivos SVG en `wwwroot/icons/`.
- [ ] **T4.2** Corregir cualquier hallazgo reportado por `vogel-infosec-guardian` y confirmar aprobación de seguridad.

---

## Fase 5 — Verificación QA End-to-End (`vogel-qa-verifier`)

- [ ] **T5.1** Verificar E2E en modo **`Api`** contra MicroID real (`http://localhost:8021`):
  - Validar filtros apilados con labels (`CA-01`, `CA-02`).
  - Validar botón **Crear privilegio** alineado a la derecha con estilo `HeaderWork` y SVG en `wwwroot/icons/` (`CA-03`).
  - Validar botones **Editar** / **Reactivar** en `AdmPrivList` en formato solo ícono + tooltip (`CA-04`, `CA-05`).
  - Ejecutar ciclo completo: inactivar un privilegio de prueba → filtrar por Inactivos → verificar que solo aparece **Reactivar** → reactivar → verificar cambio real a `Status == "A"` en MicroID y aparición nuevamente en Activos (`CA-06`, `CA-07`).
- [ ] **T5.2** Verificar E2E en modo **`Mock`**:
  - Validar que todos los criterios `CA-01` a `CA-07` funcionan correctamente sin dependencia externa.
- [ ] **T5.3** Confirmar cierre del plan y, cuando el usuario solicite crear el Pull Request hacia `main`, eliminar ambos documentos de `reqs/` antes de abrir el PR.

---

## Registro de Seguimiento

| Fecha | Fase / Tarea | Evento / Observación |
| :--- | :--- | :--- |
| 2026-11-23 | Levantamiento | Se consolidaron los issues `#3`, `#4` y `#6`, se verificó el contrato real `POST /priv/reactivate` en MicroID (solo lectura), se creó la rama `adm-priv-ux-reactivate` desde `main` y se generaron los 2 documentos en `reqs/`. Aprobado por el usuario para iniciar ejecución. |
| 2026-11-23 | Fase 1 (`T1.1`–`T1.6`) | Implementados DTOs, contratos, modelos MicroID, `MicroIdPrivilegeAdminService.ReactivatePrivilegeAsync` (con ownership check anti-BOLA/IDOR), `MockPrivilegeAdminService.ReactivatePrivilegeAsync` y endpoint `POST api/privileges/{id}/reactivate`. Build `Vogel.Server` verde (0 errores, 0 warnings). |
