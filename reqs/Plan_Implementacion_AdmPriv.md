# Plan de Implementación y Seguimiento: Administración de Privilegios (`AdmPriv`)

**Documento de Referencia:** [Requerimiento_AdmPriv.md](./Requerimiento_AdmPriv.md)  
**Ruta de Página:** `/admsec/priv`  
**Carpeta Destino:** `Vogel.Client\Pages\TreeWork\AdmTools\AdmSecurity\AdmPriv`  

---

## Tablero de Seguimiento General

| Fase | Descripción | Estado | Avance |
|---|---|---|---|
| Fase 1 | Contratos Compartidos (`Vogel.Shared`) y Proveedor BFF Desacoplado (`Vogel.Server`) | Pendiente | 0 / 4 |
| Fase 2 | Estado Compartido y Vistas en `AdmPriv` (`Vogel.Client`) | Completada | 6 / 6 |
| Fase 3 | Auditoría InfoSec (`vogel-infosec-guardian`) y Verificación QA (`vogel-qa-verifier`) | Pendiente | 0 / 4 |

---

## Fase 1: Contratos Compartidos y Proveedor BFF Desacoplado

- [ ] **T1.1 — Crear DTOs de administración de privilegios en `Vogel.Shared/DTOs/PrivilegeAdminDtos.cs`**
  - `PrivilegeAdminItemDto`: `IdPrivilege`, `IdSystem`, `IdParent`, `Codename`, `Title`, `GroupName`, `Action`, `Icon`, `Status`.
  - `PrivilegeAdminListResponseDto`: `SystemCode`, `IdSystem`, `Items`, `Total`.
- [ ] **T1.2 — Agregar modelos internos de MicroID `/priv/list` en `Vogel.Server/Models/MicroId/MicroIdPrivilegeModels.cs`**
  - `MicroIdListPrivilegeFilter` (`status`, `group_name`, `search`), `MicroIdListPrivilegeRequest` (`id_system`, `filter`), `MicroIdPrivilegeDetailResponse`, `MicroIdListPrivilegeResponse`.
- [ ] **T1.3 — Crear abstracción `IPrivilegeAdminProviderService` e implementaciones (`Api` y `Mock`) en `Vogel.Server/Services/`**
  - `IPrivilegeAdminProviderService` (sin referencias a MicroID).
  - `MicroIdPrivilegeAdminService : IPrivilegeAdminProviderService` (consume `POST /priv/list` en MicroID con `id_system = MicroIdOptions.SystemNumericId` y `status = null` para traer activos e inactivos del sistema `"MICROID"`).
  - `MockPrivilegeAdminService : IPrivilegeAdminProviderService` (adapta los privilegios de `IMockDataService`).
- [ ] **T1.4 — Crear `PrivilegesController` (`GET /api/privileges`) y registrar servicios en `Vogel.Server/Program.cs`**
  - Inyectar únicamente `IPrivilegeAdminProviderService`, validar token de sesión (`401` si falta), aplicar cabeceras `no-store` en `SecurityHeadersMiddleware` y resolver `IPrivilegeAdminProviderService` según `AuthProvider` (`"Api"` por defecto -> `MicroIdPrivilegeAdminService`, `"Mock"` -> `MockPrivilegeAdminService`).

---

## Fase 2: Estado Compartido y Vistas en `Vogel.Client`

- [x] **T2.1 — Implementar `IAdmPrivStateService` / `AdmPrivStateService` en `Vogel.Client/Services/` y registrarlo en `Vogel.Client/Program.cs`**
  - Consumir `GET api/privileges` enviando el token Bearer de `ISecureSessionStorageService`.
  - Mantener `SystemCode` (`"MICROID"`), lista completa de `PrivilegeAdminItemDto`, filtros (`StatusFilter = "A"` por defecto, `TitleFilter = ""`, `IdParentFilter = null`), lista filtrada paginada y evento `OnStateChanged`.
- [x] **T2.2 — Crear `AdmPriv.razor` y `AdmPriv.razor.cs` en `Vogel.Client/Pages/TreeWork/AdmTools/AdmSecurity/AdmPriv/`**
  - Configurar `@page "/admsec/priv"`.
  - Inicializar la instancia de `BlockWork` directamente al inicio de `AdmPriv.razor` (`ActionSet.Exit = "/admsec/menu"`, `ViewSet` con `AdmPrivInfo`, `AdmPrivCapture`, `AdmPrivFilter`, `AdmPrivList`) y pasarla a `<WorkViewFrame>`.
  - Mantener el resto del código en `AdmPriv.razor.cs`.
- [x] **T2.3 — Crear `AdmPrivInfo.razor` y `AdmPrivInfo.razor.cs`**
  - Mostrar dentro de `<InfoViewFrame>` el sistema al que pertenecen los privilegios (`"MICROID"`).
- [x] **T2.4 — Crear `AdmPrivCapture.razor` y `AdmPrivCapture.razor.cs`**
  - Mostrar el botón **"Crear privilegio"** con su handler en Code-Behind listo para futura implementación del formulario.
- [x] **T2.5 — Crear `AdmPrivFilter.razor` y `AdmPrivFilter.razor.cs`**
  - Radio button de estado: **Activos** (`"A"`, por defecto), **Inactivos** (`"I"`), **Todos** (`"ALL"`).
  - Input de texto para filtrar (*ignorecase*) por `title`.
  - Input para filtrar por `id_parent`.
- [x] **T2.6 — Crear `AdmPrivList.razor` y `AdmPrivList.razor.cs`**
  - Tabla con `<ListViewFrame TItem="PrivilegeAdminItemDto">` mostrando las columnas: `codename`, `title`, `id_parent`, `groupname`, `status` (`<input type="checkbox" checked="@(item.Status == "A")" disabled />`) y la columna con el botón **Editar** (handler pendiente de implementación).

---

## Fase 3: Auditoría InfoSec y Verificación QA

- [ ] **T3.1 — Compilación y pruebas unitarias (`dotnet build Vogel.slnx` y `dotnet test Vogel.slnx`)**
  - Verificar 0 errores y 0 advertencias.
- [x] **T3.2 — Auditoría de `vogel-infosec-guardian`**
  - Verificar desacoplamiento de `PrivilegesController`, exigencia de token Bearer, cabeceras `no-store` y ausencia de secretos en cliente/logs.
- [ ] **T3.3 — Prueba funcional contra MicroID (`http://localhost:8021`)**
  - Verificar que `GET /api/privileges` con token de sesión válido consulta `POST /priv/list` en MicroID y retorna los privilegios del sistema `"MICROID"` con sus estados `"A"` / `"I"`.
- [ ] **T3.4 — Verificación de filtros y renderizado de `AdmPriv`**
  - Verificar filtro por estado (`Activos` por defecto, `Inactivos`, `Todos`), filtro *ignorecase* por `title`, filtro por `id_parent` y checkbox no editable de `status`.

---

## Registro de Seguimiento / Notas de Ejecución

| Fecha | Tarea | Responsable | Resultado / Observaciones |
|---|---|---|---|
| 2026-11-22 | Definición de requerimiento y plan `AdmPriv` | Agente / Usuario | Documentos creados en `reqs/` pendientes de revisión del usuario. |
| 2026-11-22 | T3.2 — Auditoría InfoSec (`AdmPriv`) | `vogel-infosec-guardian` | **APROBADO (0 Críticos, 0 Altos, 0 Medios, 2 Bajos / 1 Informativo no bloqueantes)**. Verificado: desacoplamiento total en `PrivilegesController`, validación de Bearer/`X-Session-Token` (401), lista blanca de `status` y cero secretos en logs (`MicroIdPrivilegeAdminService` / `MockPrivilegeAdminService`), `Cache-Control: no-store, no-cache` + `Pragma: no-cache` en `/api/privileges` y `/api/v1/privileges`, cliente sin `X-System-Key`/`8021`/`MarkupString` y `F:\LABORATORIO\2026\MICROID` intacto. |
| 2026-11-22 | T2.1–T2.6 — Estado compartido y vistas `AdmPriv` (`Vogel.Client`) | Especialista Frontend Blazor | Implementados `IAdmPrivStateService`/`AdmPrivStateService` (registrado Scoped en `Program.cs`) y las 5 vistas con Code-Behind en `AdmPriv` (`AdmPriv`, `AdmPrivInfo`, `AdmPrivCapture`, `AdmPrivFilter`, `AdmPrivList`). `dotnet build Vogel.slnx` (0 errores, 0 advertencias) y `dotnet test Vogel.slnx --no-build` (76/76 superadas). |
