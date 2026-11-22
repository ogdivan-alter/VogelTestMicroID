# Requerimiento de Software para Proyecto Vogel


## 1. Contexto del Proyecto

El proyecto consiste en el desarrollo de un marco de trabajo o plantilla base para una aplicación **Blazor WebAssembly App (ASP.NET Core Hosted)**, utilizando la versión más reciente LTS de .NET (actualmente 10).

Este proyecto es una plantilla que corresponde a una aplicación Front End, por lo que toda persistencia se basa en servicios externos.

---

## 2. Elementos Estructurales Mínimos

### 2.1. Privilegios

Aunque la plantilla Vogel está concebida para recibir el detalle de manejo de privilegios de fuentes externas, en su interior estos privilegios deben amoldarse al siguiente esquema (por lo que para su integración con fuentes externas se requiere un mecanismo de transformación):

#### 2.1.1. Usuario (User)

El usuario debe contener al menos los siguientes atributos:

* **username**
* **name**: No es obligatorio.
* **avatar**: Imagen (jpg), no obligatorio.
* **type_authentication**: Referente a cómo se validan sus credenciales.
* **allow_password_change**: Si el usuario está habilitado para cambiar su contraseña. Esto está asociado inicialmente a que si el `type_authentication` es INTERNA, se permite; aunque otro tipo de autenticación, mediante integración, puede también permitirlo.
* **privileges**: Lista de privilegios que tiene el usuario durante la sesión. Estos privilegios se cargan al autenticarse, pero serán verificados al ingresar a las diferentes opciones.

#### 2.1.2. Privilegio (Privilege)

Cada instancia de privilegio tiene al menos los siguientes atributos, partiendo de que los privilegios se estructuran como un árbol de opciones (por lo tanto tiene nodos o privilegios padre — `parent`):

* **id_parent**: Identificador del privilegio padre. Este es un valor obligatorio, por lo que no deberia tener valor nulo y el valor 0 corresponde a que no tiene un nodo padre.
* **id**: Identificador propio del privilegio.
* **codename**: Nombre clave del privilegio.
* **group**: Nombre de grupo al que pertenece; aplica para menús secundarios, para agrupar los "botones" o equivalentes en la vista.
* **title**: Texto que se despliega en la opción o menú.
* **action**: Corresponde a la acción o ruta que se ejecuta al ingresar; alimenta al elemento de UI para dirigir el flujo.
* **icon**: Corresponde a la ruta relativa del ícono SVG que se carga en el objeto de acción en UI.
* **access**: Instancia del objeto que contiene los atributos de permisos de acceso; el listado completo de acciones válidas se define en el numeral 4.2. Aquí solo se mencionan a modo de ejemplo (`read`, `modify`, `write`, `delete`, `approve`, etc.), sin necesidad de repetir el mismo nivel de detalle.

### 2.2. Vistas Mínimas

#### 2.2.1. Layout General

La página general tiene una estructura compuesta por:

##### Encabezado

Contiene los siguientes ítems, de manera horizontal:

* **Izquierda:** Logotipo y nombre de la aplicación (por ejemplo, "APP DEMO"). Al hacer clic, redirige a la Vista de Menú Principal.
* **Derecha — Bloque de acción** (en responsive, en áreas pequeñas, estos elementos deben verse con botón tipo "burger"). El orden de aparición, de izquierda a derecha dentro de este bloque, es:
  1. **Info usuario** (username + avatar, si está habilitado en `appsettings.json`). Solo visible cuando el usuario está autenticado.
  2. **Cambio de clave** (botón o link que envía a la vista de cambio de clave). Solo visible si el usuario está autenticado **y** su atributo `allow_password_change` está activo; aparece siempre después de "Info usuario".
  3. **Cierre de sesión** (botón, preferiblemente con ícono). Visible siempre que la sesión esté activa.

##### Cuerpo

En esta área se carga la vista que se haya habilitado: Login, Vista de Menú Principal, Vista de Menú Secundario o una Vista de Trabajo.

##### Pie

Contiene información general que deba mostrarse siempre para toda la aplicación. Por defecto se carga la vista `DefaultFooter`, que contiene la versión de la aplicación.

#### 2.2.2. LoginView

Contiene el componente `Login` y otros elementos requeridos en la vista habilitada para el login, como "Recordar Sesión" y reCAPTCHA.

#### 2.2.3. MainView

La vista contiene 2 elementos, dispuestos de manera horizontal (en responsive `sm` se apilan):

* Componente `MainMenu`
* Componente `MainContent`

`MainMenu` es parte de `MainView`, por lo que solo se muestra cuando `MainView` está activa; al navegar hacia una `SecondaryView` o una Vista de Trabajo, el cuerpo de la página deja de mostrar `MainView` (y, con ella, el menú lateral) hasta que el usuario regrese a la Vista de Menú Principal.

#### 2.2.4. SecondaryView

Esta vista tiene 2 elementos apilados:

* Componente `HeaderWork`
* Componente `SecondaryMenu`

Ocupa toda el área de trabajo (cuerpo del layout).

### 2.3. Componentes

La plantilla base Vogel se basa en el uso de componentes. De manera inicial se establecen los siguientes:

#### 2.3.1. Login

Incluye la captura de datos de usuario (`username`) y contraseña (`password`), el botón de ingresar y el botón de cancelar (este último se visualiza si el parámetro así lo define).

#### 2.3.2. MainMenu

Es el componente que despliega, a modo de barra lateral, el menú con 2 niveles (opciones y subopciones), visibles según los privilegios del usuario.

* Siempre se inicializa con todas las opciones colapsadas.
* Al hacer clic en una opción, se visualizan las subopciones que le corresponden.
* Al hacer clic en una opción, las subopciones de las demás opciones se ocultan.
* Las opciones y subopciones se crean a partir del listado de privilegios del usuario:
  * Las **opciones** son todos aquellos privilegios que no tienen padre, o cuyo `id_parent` es `0` (en caso que no reciba ningun valor o aparezca null, se asigna el valor 0).
  * Las **subopciones** son todos los privilegios cuyo `id_parent` corresponda a una opción.
  * Para las opciones, el atributo `action` no genera ninguna acción.
  * Para las subopciones, el atributo `action` invoca esa acción en el sistema; en general, abre la vista correspondiente (en el área de "Cuerpo" del layout).

Este componente se arma como árbol de `OptionMenu` y `SuboptionMenu`, similar a lo siguiente:

```
- OptionMenu 1
  - SuboptionMenu 1.1
  - SuboptionMenu 1.2
- OptionMenu 2
  - SuboptionMenu 2.1
  - SuboptionMenu 2.2
- OptionMenu ...
- OptionMenu N
  - SuboptionMenu N.1
  - SuboptionMenu N.2
  - SuboptionMenu N.3
```

#### 2.3.3. OptionMenu

Es el componente contenido por `MainMenu` y corresponde a cada uno de los ítems que se listan en el menú. Al ser componente, recibe parámetros como:

* Id
* Codename
* Title
* Icon
* Action

#### 2.3.4. SuboptionMenu

Es el componente contenido dentro de `OptionMenu` y corresponde a cada una de las subopciones. Al ser componente, recibe parámetros como:

* IdParent
* Codename
* Title
* Icon
* Action

#### 2.3.5. MainContent

Es el componente que recibe el contenido que se muestra en la página principal. Puede recibir una vista diseñada a la medida (`@customView`), pero de lo contrario se compone de:

* Objeto similar a un Jumbotron:
  * Ruta de la imagen de logo para la aplicación (parámetro `@mainImage`).
  * Texto que se visualiza en esta área (parámetro `@mainText`).

En la inicialización, si no hay una vista cargada (`@customView`), los valores de `@mainImage` y `@mainText` se inicializan con la información contenida en `appsettings.json`.

#### 2.3.6. HeaderWork

Es un componente que trabaja como encabezado y se visualiza como una franja de color (parámetro `@color`), que contiene:

* Label o `h2`, que recibe el valor del parámetro `@title`.
* Botón alineado a la derecha con el texto "Salir" o el valor del parámetro `@textExit`.
  * Este botón ejecuta la acción o dirige a la ruta indicada en el parámetro `@actionExit`.

#### 2.3.7. SecondaryMenu

Es un componente que estructura la visualización de un conjunto de opciones habilitadas para el usuario.

El componente recibe:

* **IdParent**: corresponde al id del privilegio que permite abrir la vista en la que se despliega el menú secundario.

A partir de este Id se obtienen todos los privilegios hijos, o cuyo `IdParent` coincida.

Este listado de privilegios se despliega agrupado por el atributo `group` y organizado por el atributo `order`, así:

```
[group1]
[privilegio1][privilegio2] ... [privilegio4]
[group2]
[privilegio5][privilegio6] ... [privilegio8]
```

Por cada fila, máximo 4 privilegios. Cada uno de estos elementos corresponde a una instancia del componente `SecondaryMenuItem`.

#### 2.3.8. SecondaryMenuItem

Este componente recibe:

* **Title**: valor del atributo `title` del privilegio.
* **Icon**: valor del atributo `icon` del privilegio.
* **Action**: valor del atributo `action` del privilegio.

Se visualiza como un `Card` con 2 partes: la parte superior tiene el ícono y la parte inferior tiene el título. Al hacer clic, invoca el método indicado por la acción.

#### 2.3.9. WorkViewFrame

Este componente recibe como parámetro una instancia de `BlockWork` para construirse.

Divide la vista en tres elementos principales:

* **Header**: cargado con `HeaderWork` (asocia el parámetro `actionExit` con lo definido en la instancia `BlockWork` — `action_set.exit`).
* **Body**: cargado con las vistas dadas por parámetro:
  * `@InfoFrame` (si no se define algo distinto, carga la vista de `BlockWork` — `view_set.info`).
  * `@CaptureFrame` (si no se define algo distinto, carga la vista de `BlockWork` — `view_set.capture`).
  * `@FilterFrame` (si no se define algo distinto, carga la vista de `BlockWork` — `view_set.filter`).
  * `@ListFrame` (si no se define algo distinto, carga la vista de `BlockWork` — `view_set.list`).
* **Footer**: cargado con la vista del parámetro `@FooterFrame`.

En caso de no recibir una instancia de `BlockWork`, el componente puede recibir las vistas de cada sección y cargarlas con su propia operación.

#### 2.3.10. InfoViewFrame

Componente que se comporta como etiqueta HTML y permite anidar otros elementos; tiene sus propias clases de estilo.

Sirve para armar las vistas del `InfoFrame`, o las asociadas con un `BlockWork`.

#### 2.3.11. ListViewFrame

Componente que se comporta como etiqueta HTML y permite anidar otros elementos; tiene sus propias clases de estilo.

Ejemplo de uso:

```html
<ListViewFrame
  @PageSize=10,
  @PageMethod=MethodX
  @Data=SourceData
>
<thead>
  <th>Elemento 1</th>
  <th>Elemento 2</th>
  ...
  <th>Elemento N</th>
</thead>
<tbody>
  <tr>
  foreach(item in Data){
    <td>item.Elemento1</td>
    <td>item.Elemento2</td>
    ...
    <td>item.ElementoN</td>
  }
  <tr>
</tbody>
</ListViewFrame>
```

Sería ideal que el componente se encargara internamente del recorrido (`foreach`), limitándolo por el tamaño máximo de registros por página y por el desplazamiento (`offset`) correspondiente.

* **PageSize**: cantidad máxima de registros por página.
* **PageMethod**: método que debe recibir el número de página al que se debe desplazar.
* **Data**: método por el cual se accede al listado completo (o parcial).

El objetivo es que el componente reciba la data y, a partir de esta, cree la barra de navegación por páginas, conectando los eventos con el método de paginación. La barra de navegación debe visualizarse de manera similar a:

```
<< 1 2 3 ... 4 5 >>
```

Es decir, con los botones:

* `<<`: ir al inicio.
* `>>`: ir al fin.
* Botones para cada página (preferiblemente, mostrar máximo 10).

Sirve para armar las vistas del `ListFrame`, o las asociadas con un `BlockWork`.

### 2.4. Modelos

#### 2.4.1. BlockWork

Esta clase representa una entidad compuesta tipo ***entidad maestro-detalle***, orientada al despliegue de elementos en el front. Cuenta inicialmente con los siguientes atributos:

* **codename**: nombre clave que identifica a la ***entidad***.
* **show_info**: si la sección de info es visible o no.
* **title**: texto que se presenta en el encabezado.
* **key_master_attribute**: nombre del campo clave que identifica al objeto ***maestro***.
* **key_detail_attribute**: nombre del campo clave que identifica al objeto ***detalle***.
* **action_set**: instancia de la clase `ActionBlockWork`.
* **view_set**: instancia de la clase `ViewBlockWork`.

#### 2.4.2. ActionBlockWork

Asociada a `BlockWork`, recibe las URLs (relativas) donde se ejecutan las acciones del bloque de trabajo de la entidad. Todos sus atributos son de tipo texto:

* **start**: acción de inicio o vista principal de gestión de la ***entidad***.
* **capture**: acción de captura de objetos detalle ya asociados o por asociar al objeto ***maestro***.
* **remove**: acción de desasociación de objetos detalle del objeto ***maestro***.
* **cancel_master**: acción de cancelación de cambios sobre el objeto ***maestro*** (incluye cancelación de creación).
* **apply_master**: acción de ejecución de cambios sobre el objeto ***maestro*** (incluye creación).
* **rev_apply_master**: acción para reversar cambios ejecutados sobre el objeto ***maestro***.
* **list_detail_master**: acción para aplicar filtros sobre la lista de objetos ***detalle***; este mismo método genera el listado inicial sin filtros.
* **doc_master**: acción para la descarga de un documento representativo de la ***entidad*** (por ejemplo, un PDF).
* **rep_master**: acción para la descarga de un documento tipo reporte detallado de la ***entidad*** (por ejemplo, Excel o CSV).
* **exit**: acción de salida de la vista principal de gestión de la ***entidad***.

#### 2.4.3. ViewBlockWork

Asociada a `BlockWork`, recibe las URLs (relativas) de las vistas internas de la operación con la ***entidad***:

* **info**: vista de despliegue de información detallada de la parte ***maestro*** de la ***entidad***.
* **capture**: vista donde se captura la información necesaria para asociar un elemento ***detalle*** con la ***entidad***.
* **filter**: vista de despliegue de los elementos para realizar filtros sobre la lista de objetos ***detalle***.
* **list**: vista de despliegue de la lista de registros representativos de los objetos ***detalle***.

---

## 3. Consideraciones Preliminares y Fases de Desarrollo

* Todas las vistas Blazor implementadas se regirán bajo el esquema **Code-Behind**.
* Los componentes se alojarán en la carpeta `Components`, dentro del proyecto correspondiente en donde se creen.
* El nombre de archivos, clases, funciones, variables e identificadores (por ejemplo, en etiquetas HTML o elementos CSS) será en **inglés**.
* La documentación (comentarios) se mantiene en **español**.
* **Control de operaciones y bloqueo de interfaz**: toda operación debe completarse antes de permitir la ejecución de otra tarea. Al hacer clic en botones de despliegue/colapso de paneles, no debe permitirse pulsar el mismo botón repetidamente para la misma acción; igualmente, al accionar el guardado en un formulario, debe bloquearse la edición y cualquier reenvío mediante elementos visuales de carga u overlays.
* **Vigilancia de consola**: los errores no deben reflejarse en la consola del navegador a menos que también sean visibles directamente en la página mediante mensajes controlados para el usuario. No deben quedar errores ocultos que solo se visualicen en consola.

Se plantean 3 fases de desarrollo:

### 3.1. Fase 1

Creación de los elementos principales, bajo los criterios mínimos de seguridad establecidos. No requiere ejecución funcional de la página.

### 3.2. Fase 2

* Se implementa una clase `Mock` que contiene los datos de prueba de usuarios y privilegios.
* Aunque el proceso de autenticación dependerá, a futuro, de un servicio externo, en esta fase se emula la conexión a un servicio, pero los datos provienen de la clase `Mock`. El método o función en el que se hace el proceso de autenticación se mantiene igual para la integración real futura.
* Se implementa el flujo funcional inicial de la aplicación, de la siguiente manera:
  1. **Autenticación exitosa**: incluye las alternativas de "recordar sesión" y uso de reCAPTCHA. Todos los privilegios son habilitados para el escenario DEMO.
  2. **Carga de Vista Principal** (`MainView`): el menú se carga de acuerdo con todos los privilegios definidos en `Mock`, con los valores concretos **Arranque 1 / Arranque 2**.
  3. **Entrada a Opción de Trabajo** (privilegio de prueba — subopción "**Entre Aquí**").
  4. **Salida de Opción de Trabajo**, o carga nuevamente de `MainView`.
  5. **Entrada a Opción de Menú Secundario** (subopción "**2.1**"):
     * Carga la `SecondaryView`, desplegando el menú secundario correspondiente al privilegio.
     * Al hacer clic en un botón de operación de ejemplo, se carga la Vista de Trabajo correspondiente, que internamente usa el componente `WorkViewFrame`, recibiendo una instancia de la clase `BlockWork`.
  6. **Salida de Opción de Menú Secundario**: mediante clic en el botón de salida del encabezado de `WorkViewFrame`.

### 3.2.1. Pendientes de Ajuste (Previo a Fase 3)

* Se requiere realizar ajustes a la Fase 2 antes de dar paso a la integración definitiva.
* **Detalle de ajustes:** Aún por revisar.

### 3.3. Fase 3

Integración con servicios externos para visualizar el flujo real de la aplicación.

---

## 4. Consideraciones de Seguridad

### 4.1. Elementos Esenciales

* Uso de `AuthorizeView` con atributo `Roles` inicializado con una función `PrivilegeFunction("privilegio")`.
* La función `PrivilegeFunction` recibe el texto que identifica el privilegio y determina el rol:
  * Retorna **Negado** si no tiene el privilegio.
  * Retorna **Único** si lo tiene.
* Al autenticarse, el usuario recibe el rol **Único** como base.

### 4.2. Estructura de Privilegios

El atributo `access` es opcional, así como sus contenidos, dado que dependen de la estructura final de privilegios que entregue el servicio externo:

```json
{
  "username": "[usuario]",
  "privileges": [
    {
      "name": "[privilegio]",
      "access": {
        "create": true,
        "modify": true,
        "read": true,
        "delete": false,
        "validate": true,
        "execute": false,
        "approve": true
      }
    }
  ]
}
```


### 4.3. Flujo de Autenticación y Autorización

* La autenticación real se basa en integración con servicios externos (por ejemplo, el API de un backend, o servicios de autenticación de credenciales como Google, Microsoft, u otros).
* El objeto Usuario define, mediante su atributo de tipo de autenticación, si puede cambiar contraseña:
  * Si es **INTERNA**, se permite el cambio de contraseña.
  * Si es un proveedor externo (Google, Microsoft, otro), no se permite.
* Autorización basada en privilegios definidos por API.
* Al autenticarse, se obtienen los privilegios del usuario; de esto depende que una opción sea visible o no (por ejemplo, en el menú principal). Al ingresar a una opción controlada, **se validará primero la vigencia de la sesión conforme a lo dispuesto en 5.3**, y posteriormente se verificará el privilegio correspondiente antes de desplegar la vista o ejecutar una acción.
* **reCAPTCHA**: configurable por `appsettings.json`, con modalidades:
  * No exigirlo (no se muestra en pantalla).
  * Exigirlo siempre.
  * Exigirlo solo bajo ciertas condiciones (por ejemplo, reintentos fallidos), lo que hace parte de un atributo del objeto Usuario.
* Persistencia basada en APIs externas.
* Comunicación mediante API REST.
* Integración con otros servicios en fases posteriores.
* UI/UX basada en Blazor.

**Flujo funcional del login:**

1. El usuario ingresa sus credenciales.
2. El backend valida los datos.
3. Respuesta:
   * **Positiva**: se devuelven los privilegios y se construyen los menús dinámicos; se redirige a la Vista de Menú Principal.
   * **Negativa**: se muestra el mensaje "Usuario o Clave incorrectos".

**Requerimientos funcionales asociados:**

* Campos: usuario, contraseña, "recordar sesión".
* Validación de campos obligatorios.
* Redirección a la Vista de Menú Principal tras la autenticación.
* Construcción dinámica de menús según privilegios.

**Requerimientos no funcionales asociados:**

* Seguridad: contraseñas encriptadas (HTTPS).
* Autenticación siempre del lado del servidor.
* Interfaz clara y minimalista.
* Escalabilidad: integración futura con OAuth / OpenID Connect.

### 4.4. Seguridad del Lado del Servidor

* **Cabeceras de seguridad**: configuración dinámica de políticas CSP (Content Security Policy), `X-Frame-Options`, `Referrer-Policy` y `X-XSS-Protection` a través de `appsettings.json`.
* **Políticas de cookies**: forzado de atributos `Secure`, `HttpOnly` y `SameSite=Strict` para todas las cookies emitidas.
* **Filtro de excepciones**: `GlobalExceptionFilter` captura errores no controlados en controladores API, los registra y redirige al usuario de forma segura.
* **OWASP y CWE**: mantener la aplicación alineada con las recomendaciones OWASP y CWE para reducir los riesgos de seguridad.

---

## 5. Gestión de Sesión e Inactividad

### 5.1. InactivityMonitor

Componente que monitorea la actividad del usuario (clics, teclado, movimiento del mouse) mediante JavaScript Interop.

* **Tiempo de espera**: configurable en `appsettings.json` (`InactivityTimeoutMinutes`).
* **Acción**: al agotarse el tiempo, la aplicación realiza un cierre de sesión automático (`LogoutAsync`) y redirige al usuario a la página de login.

### 5.2. Persistencia de Estado

Se utiliza el `sessionStorage` nativo del navegador (mediante JS Interop) con cifrado (vía `CryptoHelper`) para almacenar información sensible del estado de navegación, como el `idParent` de los menús activos.

Se puede almacenar información del lado del cliente, siempre que se mantenga cifrada.

### 5.3. Validación de sesión al ingresar a opciones controladas por privilegios

Con el fin de detectar el reinicio del servicio y garantizar la vigencia de la sesión, se establece lo siguiente:

* **Momento de validación:** La sesión deberá validarse cada vez que el usuario ingrese a una opción controlada por privilegios. Esto aplica a las opciones del Menú Principal, subopciones del Menú Secundario, Vistas de Trabajo (`WorkViewFrame` / `BlockWorkView`) y cualquier ruta protegida asociada a un privilegio.
* **Independiente a la autorización por privilegios:** Esta validación es distinta a la verificación de privilegios indicada en el numeral 4.3. La verificación de privilegios responde a *"¿El usuario posee este privilegio?"*, mientras que la validación de sesión responde a *"¿Sigue vigente la sesión para la instancia actual del servidor?"*.
* **Detección por instancia del servidor:** El servidor deberá generar un identificador único de instancia al momento de arrancar (`InstanceId` + `StartTimeUtc`). El cliente deberá almacenar dicho marcador asociado a la sesión activa. Al ingresar a una opción protegida, se deberá comparar el marcador almacenado con el que retorne el servidor.
* **Casos que invalidan la sesión:** Se deberá considerar inválida la sesión cuando:
  * El marcador almacenado no coincida con el marcador retornado por el servidor (cambio de instancia / reinicio).
  * Exista una sesión de usuario almacenada en el cliente, pero **no exista** el marcador de instancia del servidor (pérdida del marcador o reinicio del servicio).
  * La respuesta de validación del servidor no sea satisfactoria.
* **Acción ante sesión inválida:** En caso de invalidación, se deberá limpiar toda la información de sesión almacenada en el cliente (incluido el marcador de instancia), restablecer el estado de autenticación y redirigir obligatoriamente a `LoginView`.
* **Validación previa:** Esta validación deberá ejecutarse **antes** de desplegar la vista o ejecutar cualquier acción asociada a la opción.
* **Aplicación al ingreso a la Vista Principal:** Al navegar hacia la Vista Principal (`/`), ya sea desde el menú o desde el enlace del logotipo "Vogel", también se considerará ingreso a una opción controlada y deberá someterse a esta validación.
* **Optimización sin pérdida de rigor:** Para minimizar el impacto en el desempeño, se podrá implementar con un mecanismo de caché con un TTL corto. Dicho caché únicamente evitará validaciones redundantes en navegaciones consecutivas muy próximas, pero **no** deberá impedir la detección cuando exista un cambio real de instancia del servidor.

---

## 6. Sistema de Logging

### 6.1. Esquema General

Toda excepción no controlada debe capturarse de tal manera que permita registrar la información clave en el log, sin detener el levantamiento de la excepción.

Un mecanismo clave puede ser la implementación de una clase decoradora que decore todo controlador, de tal manera que si se presenta alguna excepción, el decorador la atrape y haga el registro.

Para las operaciones del lado del cliente, toda excepción igualmente debe capturarse y comunicarse al servidor para su registro.

### 6.2. Client-Side Logging

`ClientLogger` permite a los componentes del cliente enviar logs de información, advertencias o errores al servidor de forma asíncrona.

### 6.3. Server-Side Logging

Se implementa el servicio de Log, en donde por defecto se genera un archivo de texto, el cual se archiva de manera diaria y sigue las convenciones de registro con niveles (por ejemplo, `DEBUG`, `ERROR`, etc.). Se debe optar por el uso de librerías ya existentes para el manejo de logs (por ejemplo, `log4net`, `Serilog`) y no crear esta funcionalidad desde cero.

La configuración del servicio de logging debe contemplar: formato de registro y destino del registro (archivo de texto por defecto, base de datos, o servicio API).

---

## 7. Configuración Principal (`appsettings.json`)

* `InactivityTimeoutMinutes`: minutos de inactividad antes del logout.
* `Serilog`: configuración del registro estructurado (niveles mínimos, sinks de consola y archivo, ruta y patrón del archivo, rotación diaria y retención).
* `SecurityHeaders`: mapa de cabeceras HTTP de seguridad.
* `RememberMe.Enabled` (booleano) y `RememberMe.DisplayText` (texto): habilita y define la etiqueta de la opción "Recordar Sesión".
* `Recaptcha`: habilita el reCAPTCHA y define sus opciones (ver 4.3).

---

## 8. Roles y Permisos

* Los roles son dinámicos y se definen en base de datos (o en la fuente externa de privilegios, según 2.1).
* Los permisos controlan la visibilidad de opciones y subopciones en los menús (ver 4.1 y 4.2 para el mecanismo de verificación).

---

## 9. Consideraciones Técnicas Adicionales

* Privilegios inicialmente simulados con la clase `Mock`; luego integrados con servicios reales (ver Fase 2, sección 3.2).
* **Íconos de la aplicación** (para botones y tarjetas del Menú Secundario):
  * Formato estandarizado: SVG vectorial.
  * Almacenados en el servidor en una carpeta dedicada de recursos (`wwwroot/icons/` o equivalente) y servidos estáticamente o vía endpoint hacia el cliente.

---

## 10. Alcance, Criterios de Éxito y Entregables

### 10.1. Alcance Mínimo

* Arquitectura por capas: Cliente, Servidor y Shared.
* Configuración inicial de servicios: Dependency Injection y Logging.
* Responsabilidad exclusiva de Front End, conectándose a servicios externos para persistencia.
* Esquema de UI basado en Blazor.

### 10.2. Criterios de Éxito

* Implementación de las vistas iniciales: Login, Menú Principal, Menú Secundario y Vista de Trabajo.
* Ejecución de los flujos completos descritos (ver sección 3.2, Fase 2).
* Validación de casos exitosos y fallidos (por ejemplo, autenticación fallida).
* Compilación y despliegue sin errores en entorno local y en servidor ASP.NET Core.
* Código y pruebas comentadas para describir la operación.

### 10.3. Entregables

* Código fuente estructurado y funcional.
* Comentarios en el código y en las pruebas.
* Pruebas unitarias y E2E.

### 10.4. Restricciones y Supuestos

* Uso de librerías oficiales de Microsoft: deseable, no obligatorio.
* Compatibilidad con navegadores modernos (Chrome, Edge, Firefox, Safari).
* Exclusión de autenticación real y persistencia real en esta fase, con emulación de conexión (ver Fase 2, sección 3.2).

---




