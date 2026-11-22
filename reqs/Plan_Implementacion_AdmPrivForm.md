# Plan de Implementación: Formulario de Privilegios (`AdmPrivForm`)

- **Rama de trabajo:** `adm-priv-form` (creada desde `main`)
- **Documento de requerimiento:** `reqs/Requerimiento_AdmPrivForm.md`
- **Estado general:** Completado (Fases 1–4 verificadas — pendiente aprobación de cierre de PR)

---

## Tablero de Seguimiento General

| Fase | Descripción | Estado | Avance | Commit de Fase |
| :--- | :--- | :--- | :---: | :--- |
| **Fase 1** | Contratos compartidos (`Vogel.Shared`) y Backend BFF (`Vogel.Server` — modos `Api` y `Mock`) | Completada | 100% | `9ed21aa` |
| **Fase 2** | Frontend Blazor (`Vogel.Client` — estado, `AdmPrivForm` y vinculación en `AdmPriv`) | Completada | 100% | `705c1df` |
| **Fase 3** | Pruebas Unitarias (`Vogel.Tests`) y Auditoría InfoSec (`vogel-infosec-guardian`) | Completada | 100% | `19fab86` |
| **Fase 4** | Verificación QA End-to-End (`vogel-qa-verifier`) y cierre de PR | Verificada (pendiente T4.3) | 90% | `docs(adm-priv-form): record phase 4 QA E2E verification evidence` |

---

## Fase 1 — Contratos Compartidos (`Vogel.Shared`) y Backend BFF (`Vogel.Server`)

- [x] **T1.1** Ampliar `Vogel.Shared/DTOs/PrivilegeAdminDtos.cs` con los DTOs de solicitud y respuesta para creación (`CreatePrivilegeRequestDto`, `CreatePrivilegeResponseDto`), edición (`UpdatePrivilegeRequestDto`, `UpdatePrivilegeResponseDto`) e inactivación (`DeactivatePrivilegeRequestDto`, `DeactivatePrivilegeResponseDto`), incluyendo atributos de validación (`DataAnnotations` / longitudes máximas).
- [x] **T1.2** Ampliar `Vogel.Server/Services/IPrivilegeAdminProviderService.cs` con los records de resultado y métodos `GetPrivilegeAsync`, `CreatePrivilegeAsync`, `UpdatePrivilegeAsync` y `DeactivatePrivilegeAsync`.
- [x] **T1.3** Ampliar `Vogel.Server/Models/MicroId/MicroIdPrivilegeModels.cs` con los modelos de solicitud y respuesta para `POST /priv/get`, `POST /priv/create`, `POST /priv/update` y `POST /priv/delete`.
- [x] **T1.4** Implementar los 4 nuevos métodos en `Vogel.Server/Services/MicroId/MicroIdPrivilegeAdminService.cs` (modo `"Api"`), inyectando siempre `_options.SystemNumericId`, validando pertenencia al sistema en `GetPrivilegeAsync`, y mapeando códigos de error de MicroID (`ERR_INVALID_PAYLOAD`, `ERR_INACTIVE_MASTER`, `ERR_SYSTEM_MISMATCH`, `ERR_AUTH_FAILED`, etc.) a códigos HTTP sanitizados.
- [x] **T1.5** Implementar los 4 nuevos métodos en `Vogel.Server/Services/MockPrivilegeAdminService.cs` (modo `"Mock"`) con estado en memoria sincronizado (thread-safe), validación de `id_parent` y prevención de autorreferencia/ciclos.
- [x] **T1.6** Ampliar `Vogel.Server/Controllers/PrivilegesController.cs` con los endpoints `GET /api/privileges/{id:int}`, `POST /api/privileges`, `PUT /api/privileges/{id:int}` y `DELETE /api/privileges/{id:int}`, exigiendo token de sesión y validando entrada antes de delegar en `IPrivilegeAdminProviderService`.
- [x] **T1.7** Verificar compilación limpia (`dotnet build`) y realizar commit de Fase 1 en `adm-priv-form`.

---

## Fase 2 — Frontend Blazor (`Vogel.Client`)

- [x] **T2.1** Ampliar `Vogel.Client/Services/IAdmPrivStateService.cs` y `Vogel.Client/Services/AdmPrivStateService.cs` con:
  - Estado en memoria `SelectedEditPrivilegeId` (`int?`) más métodos `SetSelectedEditPrivilegeId(int id)` y `ClearSelectedEditPrivilegeId()` para pasar el ID seleccionado hacia la ruta limpia `/admsec/priv/edit` sin exponerlo en la URL.
  - Operaciones contra el BFF (`GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `DeactivateAsync`) adjuntando el token Bearer descifrado y refrescando el catálogo local tras mutaciones exitosas.
- [x] **T2.2** Crear la vista reutilizable `Vogel.Client/Pages/TreeWork/AdmTools/AdmSecurity/AdmPriv/AdmPrivForm.razor` y su code-behind `AdmPrivForm.razor.cs` con rutas limpias `@page "/admsec/priv/create"` y `@page "/admsec/priv/edit"`, replicando la estética de `FormMockView.razor` (`HeaderWork`, `.vogel-form-mock-view`, `.vogel-form-card`, alertas y botonera).
- [x] **T2.3** Implementar en `AdmPrivForm`:
  - Protección de sesión (`/login` si no autenticado) y guardia de ruta en `/admsec/priv/edit` (si `SelectedEditPrivilegeId` no tiene valor en memoria, p. ej. acceso directo o F5, redirigir limpiamente a `/admsec/priv`).
  - Limpieza de `SelectedEditPrivilegeId` al salir, cancelar o destruir el componente (`Dispose`).
  - Carga del detalle fresco desde el BFF usando `SelectedEditPrivilegeId` y carga del catálogo de privilegios activos para el `<select>` de `id_parent` (con opción `"0 — Ninguno (Raíz)"` y exclusión del propio privilegio en modo edición).
  - Validación cliente de campos obligatorios y longitudes.
  - Panel de inactivación en modo edición con motivo opcional (`del_description`) y confirmación explícita en dos pasos.
  - Estados seguros post-creación (alerta + limpieza del formulario), post-actualización (alerta + datos sincronizados) y post-inactivación (alerta + formulario solo lectura/deshabilitado).
- [x] **T2.4** Vincular botones en el módulo `AdmPriv`:
  - `AdmPrivCapture.razor.cs`: `HandleCreatePrivilegeClick()` → limpiar `SelectedEditPrivilegeId` en `AdmPrivState` y navegar a `/admsec/priv/create`.
  - `AdmPrivList.razor` y `AdmPrivList.razor.cs`: `HandleEditClick(item)` → guardar `item.IdPrivilege` en `AdmPrivState` (`SetSelectedEditPrivilegeId`) y navegar a `/admsec/priv/edit` solo para activos (`status == "A"`); deshabilitar botón `Editar` para inactivos (`status == "I"`).
- [x] **T2.5** Agregar estilos mínimos complementarios en CSS (si se requiere para alerta de error o bloque de inactivación) respetando el diseño de `FormMockView`.
- [x] **T2.6** Verificar compilación limpia (`dotnet build`) y realizar commit de Fase 2 en `adm-priv-form`.

---

## Fase 3 — Pruebas Unitarias (`Vogel.Tests`) y Auditoría InfoSec

- [x] **T3.1** Agregar pruebas unitarias para `PrivilegesController` cubriendo `GET /{id}`, `POST`, `PUT /{id}` y `DELETE /{id}` (casos exitosos, 401 sin token, 400 payload/ID inválido y propagación de errores del proveedor).
- [x] **T3.2** Agregar pruebas unitarias para `MicroIdPrivilegeAdminService` y `MockPrivilegeAdminService` cubriendo creación, lectura individual (incluyendo validación de `id_system`), actualización, inactivación, prevención de autorreferencia/ciclos y mapeo de errores.
- [x] **T3.3** Agregar pruebas unitarias para `AdmPrivStateService` cubriendo `SelectedEditPrivilegeId` (set/clear), `GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `DeactivateAsync` y recarga del catálogo tras mutaciones.
- [x] **T3.4** Ejecutar `dotnet test` y confirmar 100% de pruebas en verde.
- [x] **T3.5** Ejecutar auditoría obligatoria con **`vogel-infosec-guardian`** sobre todos los cambios de la rama `adm-priv-form` y remediar cualquier hallazgo antes de pasar a Fase 4.
- [x] **T3.6** Realizar commit de Fase 3 en `adm-priv-form`.

---

## Fase 4 — Verificación QA End-to-End y Cierre de PR

- [x] **T4.1** Ejecutar verificación End-to-End con **`vogel-qa-verifier`** validando todos los criterios de aceptación (`CA-01` a `CA-12`):
  - Navegación desde `/admsec/priv` → Crear (`/admsec/priv/create`) → guardar → alerta de éxito y limpieza.
  - Navegación desde `/admsec/priv` → Editar (`/admsec/priv/edit`, verificando URL limpia sin ID) → actualizar → alerta de éxito y persistencia.
  - Acceso directo o recarga F5 en `/admsec/priv/edit` sin ID en memoria → redirección limpia a `/admsec/priv`.
  - Inactivación desde `/admsec/priv/edit` → confirmación + motivo → alerta de éxito + estado solo lectura + botón `Editar` deshabilitado en listado al volver a `/admsec/priv`.
  - Validación de consola sin errores y respuestas HTTP limpias.
- [x] **T4.2** Registrar evidencias y resultados finales en este plan de implementación.
- [ ] **T4.3** Una vez aprobado el cierre para Pull Request hacia `main`: eliminar los 2 documentos temporales de `reqs/` (`reqs/Requerimiento_AdmPrivForm.md` y `reqs/Plan_Implementacion_AdmPrivForm.md`), hacer commit de limpieza y abrir el PR hacia `main`.

---

## Registro de Seguimiento

| Fecha | Fase | Evento / Nota |
| :--- | :--- | :--- |
| 2026-11-22 | Planificación | Rama `adm-priv-form` creada desde `main`. Levantamiento de requerimientos completado con el usuario y generación de `Requerimiento_AdmPrivForm.md` y `Plan_Implementacion_AdmPrivForm.md` (`f82b8af`). |
| 2026-11-22 | Planificación | Ajuste aprobado (Opción 1): ruta de edición limpia `/admsec/priv/edit` sin exponer `IdPrivilege` en la URL, usando `SelectedEditPrivilegeId` en memoria dentro de `AdmPrivState` con redirección segura a `/admsec/priv` en acceso directo o recarga F5. |
| 2026-11-22 | Fase 1 | Contratos DTOs en `Vogel.Shared`, endpoints REST en `PrivilegesController` y proveedores `MicroIdPrivilegeAdminService` + `MockPrivilegeAdminService` completados (`9ed21aa`, `dotnet build` 0 errores/advertencias, 81/81 tests). |
| 2026-11-22 | Fase 2 | `AdmPrivStateService`, formulario reutilizable `AdmPrivForm.razor`/`.razor.cs` con rutas limpias `/admsec/priv/create` y `/admsec/priv/edit` y vinculación de botones en `AdmPrivCapture`/`AdmPrivList` completados (`705c1df`). |
| 2026-11-22 | Fase 3 | Pruebas unitarias en `Vogel.Tests` (`PrivilegesControllerTests`, `PrivilegeAdminServicesTests`, `AdmPrivStateServiceTests`) + auditoría `vogel-infosec-guardian` con remediaciones de H-01 (validación cross-system en `PUT`/`DELETE`), H-03 y H-04 aprobada (`APROBADO CON OBSERVACIONES`, 0 Críticos/Altos/Medios, 132/132 tests en verde, `19fab86`). |
| 2026-11-22 | Fase 4 | Verificación QA End-to-End (`vogel-qa-verifier`) **APROBADA (12/12 CAs)** contra MicroID real (`http://localhost:8021`, modo `Api`) y en modo `Mock` (`--AuthProvider=Mock`), con `132/132` pruebas en verde y catálogo limpio tras inactivar privilegio temporal de prueba. |
