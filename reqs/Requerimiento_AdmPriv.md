# Documento de Requerimiento Formalizado: Administración de Privilegios (`AdmPriv`)

**Proyecto:** `VogelTestMicroID`  
**Módulo:** Administración de Seguridad — Privilegios del Sistema (`/admsec/priv`)  
**Ubicación Frontend:** `Vogel.Client\Pages\TreeWork\AdmTools\AdmSecurity\AdmPriv`  
**Sistema Objetivo:** `"MICROID"` (`id_system = 1` en MicroID `http://localhost:8021`)  
**Fecha:** 2026-11-22  
**Estado:** Pendiente de Revisión del Usuario  

---

## 1. Propósito y Alcance

Implementar la vista general de trabajo para la **administración de los privilegios del sistema autenticado** (en este proyecto, `"MICROID"`), alojada en `Vogel.Client\Pages\TreeWork\AdmTools\AdmSecurity\AdmPriv`, basada en el componente contenedor `WorkViewFrame` y su mecanismo de vistas dinámicas `BlockWork.ViewSet`.

### 1.1 Incluido en este requerimiento
1. **Vista principal `AdmPriv.razor` (`@page "/admsec/priv"`) y `AdmPriv.razor.cs`:**
   - Inicializa directamente al inicio de `AdmPriv.razor` la instancia del parámetro `BlockWork` que se entrega a `<WorkViewFrame>`.
   - Configura `BlockWork.ActionSet.Exit = "/admsec/menu"` y `BlockWork.ViewSet` apuntando a las 4 vistas de contenido (`AdmPrivInfo`, `AdmPrivCapture`, `AdmPrivFilter`, `AdmPrivList`).
   - Mantiene toda la lógica en esquema **Code-Behind** (`.razor.cs`).
2. **Vistas de contenido en `Vogel.Client\Pages\TreeWork\AdmTools\AdmSecurity\AdmPriv\`:**
   - **`AdmPrivInfo.razor` + `.cs` (`Info`):** Muestra el sistema al que pertenecen los privilegios (`"MICROID"`).
   - **`AdmPrivCapture.razor` + `.cs` (`Capture`):** Contiene el botón **"Crear privilegio"**.
   - **`AdmPrivFilter.razor` + `.cs` (`Filter`):** Contiene:
     - Radio button de estado: **Activos** (`"A"`, por defecto), **Inactivos** (`"I"`) o **Todos** (sin filtro de estado).
     - Campo de texto para filtrar (*case-insensitive*) por el nombre (`title`) del privilegio.
     - Campo para filtrar por `id_parent`.
   - **`AdmPrivList.razor` + `.cs` (`List`):** Tabla de privilegios (basada en `ListViewFrame`) mostrando:
     - `codename`
     - `title`
     - `id_parent`
     - `groupname` (`group_name`)
     - `status` (representado por un checkbox no editable: `"A"` -> `checked`, `"I"` -> `unchecked`)
     - Columna adicional de acción con el **botón para editar** cada privilegio.
3. **Integración BFF (`Vogel.Server` y `Vogel.Shared`):**
   - Consumo de `POST /priv/list` de MicroID a través del BFF (`Vogel.Server`) de forma desacoplada (`IPrivilegeAdminProviderService`: modo `"Api"` contra MicroID y modo `"Mock"` contra `IMockDataService`).

### 1.2 Fuera de alcance en esta etapa (pendiente para siguiente fase)
- La implementación de la acción del botón **Crear privilegio** (en `AdmPrivCapture`).
- La implementación de la acción del botón **Editar** (en `AdmPrivList`) y la vista formulario de creación/edición del privilegio.

---

## 2. Especificación Funcional y de Interfaz

### 2.1 Inicialización en `AdmPriv.razor`
En `AdmPriv.razor` se declara la ruta `@page "/admsec/priv"` y se inicializa al inicio la instancia de `BlockWork` pasada a `WorkViewFrame`:

```razor
@page "/admsec/priv"
@namespace Vogel.Client.Pages.TreeWork.AdmTools.AdmSecurity.AdmPriv

@{
    var blockWork = new BlockWork
    {
        Codename = "adm_priv",
        Title = "Administración de Privilegios",
        ShowInfo = true,
        KeyMasterAttribute = "SystemId",
        KeyDetailAttribute = "IdPrivilege",
        ActionSet = new ActionBlockWork
        {
            Exit = "/admsec/menu"
        },
        ViewSet = new ViewBlockWork
        {
            Info = nameof(AdmPrivInfo),
            Capture = nameof(AdmPrivCapture),
            Filter = nameof(AdmPrivFilter),
            List = nameof(AdmPrivList)
        }
    };
}

<WorkViewFrame BlockWork="@blockWork" ShowActionToolbar="false" />
```

*(Toda la lógica adicional de inicialización o ciclo de vida se ubica en `AdmPriv.razor.cs`).*

### 2.2 Detalle de las 4 Vistas de Contenido (`ViewSet`)

| Sección (`ViewSet`) | Componente | Contenido y Comportamiento |
|---|---|---|
| **Info** | `AdmPrivInfo.razor` / `.cs` | Envuelto en `<InfoViewFrame>`. Muestra el identificador/nombre del sistema al que pertenecen los privilegios (p. ej. **`MICROID`**). |
| **Capture** | `AdmPrivCapture.razor` / `.cs` | Contiene el botón **"Crear privilegio"**. Su evento `@onclick` invoca un método en Code-Behind dejado como *placeholder* pendiente para abrir el formulario de creación. |
| **Filter** | `AdmPrivFilter.razor` / `.cs` | 1. **Radio button de estado:** `Activos` (`"A"`, valor por defecto), `Inactivos` (`"I"`), `Todos` (`"ALL"` / `null`).<br>2. **Filtro por `title`:** Input de texto que filtra ignorando mayúsculas/minúsculas (`StringComparison.OrdinalIgnoreCase`).<br>3. **Filtro por `id_parent`:** Input numérico/texto que filtra por coincidencia exacta de `id_parent` cuando se ingresa un valor válido. |
| **List** | `AdmPrivList.razor` / `.cs` | Basado en `<ListViewFrame TItem="PrivilegeAdminItemDto">`. Columnas:<br>1. **`codename`**<br>2. **`title`**<br>3. **`id_parent`**<br>4. **`groupname`**<br>5. **`status`**: `<input type="checkbox" checked="@(item.Status == "A")" disabled />`<br>6. **Acción**: Botón **Editar** por fila (cuya acción en Code-Behind queda pendiente para abrir la vista formulario del privilegio). |

### 2.3 Coordinación Reactiva entre Vistas (`AdmPrivStateService`)
Dado que `WorkViewFrame` instancia `AdmPrivInfo`, `AdmPrivCapture`, `AdmPrivFilter` y `AdmPrivList` dinámicamente vía `<DynamicComponent>`, se implementará un servicio Scoped en `Vogel.Client` (`IAdmPrivStateService` / `AdmPrivStateService`) que:
- Cargue la lista de privilegios y el identificador del sistema desde el BFF (`GET /api/privileges`).
- Mantenga el estado de los filtros (`StatusFilter = "A"`, `TitleFilter = ""`, `IdParentFilter = null`).
- Aplique el filtrado reactivo y notifique mediante un evento `OnStateChanged` a `AdmPrivList` y `AdmPrivInfo`.

---

## 3. Contrato BFF (`Vogel.Server` <-> `MicroID`)

### 3.1 Endpoint en MicroID (`POST /priv/list`)
- **Request a MicroID:**
  ```json
  {
    "data": {
      "id_system": 1,
      "filter": {
        "status": null,
        "group_name": null,
        "search": null
      }
    }
  }
  ```
  *(Nota: enviando `status: null` a MicroID se obtienen tanto activos `"A"` como inactivos `"I"`, permitiendo alternar instantáneamente entre `Activos`, `Inactivos` y `Todos` en `AdmPrivFilter`, o filtrando por `status` según se requiera).*
- **Cabeceras requeridas:** `X-Request-Id`, `X-Timestamp`, `X-System-Id: "MICROID"`, `X-System-Key`, `Authorization: Bearer <token>`.

### 3.2 Endpoint en el BFF (`Vogel.Server`)
- **Ruta:** `GET /api/privileges` (y `/api/v1/privileges`) en `PrivilegesController`.
- **Abstracción desacoplada:** `IPrivilegeAdminProviderService` (registrada en `Program.cs` según `AuthProvider`: `"Api"` -> `MicroIdPrivilegeAdminService`, `"Mock"` -> `MockPrivilegeAdminService`).
- **Respuesta (`PrivilegeAdminListResponseDto`):**
  - `SystemCode` (`"MICROID"` en modo Api, `"VOGEL-MOCK"` en modo Mock)
  - `IdSystem` (`1`)
  - `Items`: Lista de `PrivilegeAdminItemDto` (`IdPrivilege`, `IdParent`, `Codename`, `Title`, `GroupName`, `Action`, `Icon`, `Status`).

---

## 4. Requerimientos de Seguridad de la Información (InfoSec)

- **RNF-SEC-01 (Autenticación BFF):** `GET /api/privileges` debe exigir un token de sesión válido (`Authorization: Bearer` o `X-Session-Token`); si no se proporciona, debe retornar `401 Unauthorized`.
- **RNF-SEC-02 (Anti-Caché):** Las respuestas de `/api/privileges` deben incluir `Cache-Control: no-store, no-cache` y `Pragma: no-cache`.
- **RNF-SEC-03 (Desacoplamiento):** `PrivilegesController` en `Vogel.Server` no debe tener referencias directas a MicroID; debe delegar exclusivamente en `IPrivilegeAdminProviderService`.
- **RNF-SEC-04 (Protección de Secretos):** `X-System-Key` nunca se expone al cliente ni en logs.

---

## 5. Criterios de Aceptación (CA)

- **CA-01:** Al navegar a `/admsec/priv`, se renderiza `AdmPriv.razor` usando `WorkViewFrame` con `BlockWork` inicializado al inicio del archivo `.razor` y el resto de la lógica en `.razor.cs`.
- **CA-02:** La sección **Info** (`AdmPrivInfo`) muestra el sistema `"MICROID"`.
- **CA-03:** La sección **Capture** (`AdmPrivCapture`) muestra el botón **"Crear privilegio"** (sin acción de formulario aún).
- **CA-04:** La sección **Filter** (`AdmPrivFilter`) muestra el radio button (`Activos`, `Inactivos`, `Todos`) con **`Activos` seleccionado por defecto**, el campo de filtro por `title` (*ignorecase*) y el campo de filtro por `id_parent`.
- **CA-05:** La sección **List** (`AdmPrivList`) muestra los privilegios del sistema con las columnas `codename`, `title`, `id_parent`, `groupname`, `status` (checkbox no editable: `A` marcado, `I` desmarcado) y la columna con el botón **Editar** (sin acción de formulario aún).
- **CA-06:** El botón **Salir** del encabezado regresa a `/admsec/menu`.
