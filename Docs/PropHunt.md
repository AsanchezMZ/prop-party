# Prop Hunt · mapas, props y herramientas del Editor

Guía de creación de contenido y mantenimiento desde Unity. Las generalidades del juego, controles, reglas, presentación, red y pruebas están en [Game.md](Game.md).

## Montaje inicial

El montaje inicial se hace con **PropHunt → Configure enabled build scenes**. Configura las escenas habilitadas que tengan `SpawnPoint`, crea los datos en `Assets/PropHuntConfig`, conecta el prefab de jugador del `GameBootstrap` y añade el proyectil a la lista de prefabs de red existente. Guarda escenas y assets: úsalo después de guardar tu trabajo. No ejecutes esta migración para retocar mapas ya personalizados: edita sus referencias en el Inspector.

## Añadir un mapa

1. Crea una escena con tu geometría y colliders. Añádela habilitada a Build Profiles.
2. Coloca `SpawnPoint` sobre el suelo. `Any` sirve a ambos equipos; también puedes separar `Props` y `Hunters`. Debe existir un punto compatible con cada equipo.
3. Crea **Create → PropHunt → Map** y un **Prop Catalog**. Asigna nombre, escena (nombre o ruta de Build Profiles), descripción, miniatura y catálogo.
4. Añade un objeto con `NetworkObject`, `MapSetup` y `PropHuntMatch`. Asigna definición, reglas y escena de menú. Ejecuta **Collect scene spawn points** en `MapSetup`.
5. Añade `MatchHUD` al mismo objeto; asigna partida y escena de menú. En su `UIDocument`, asigna `Assets/UI/MatchHUD.uxml` como Source Asset y `Assets/PropHuntConfig/PartyPanel.asset` como Panel Settings (también lo usa el menú).
6. Añade un objeto con `NetworkObject` y `MapPropSpawner`; ejecuta **Collect and assign props IDs** después de distribuir los props.
7. Añade la definición a `LobbyManager.mapDefinitions` en el menú. Usa las mismas reglas en lobby y mapa.

La arquitectura no busca nombres de mapas ni carga recursos 3D desde rutas. Las referencias del juego son serializadas. Las rutas de la herramienta de editor solo indican dónde guardar la configuración generada.

## Añadir un prop

- Prefab raíz con `Prop`; hijo `Visual` con el modelo y colliders. El visual debe contener solo presentación: no scripts de gameplay ni `NetworkObject`.
- Asigna un `id` positivo único por tipo y `visual`. El catálogo contiene los prefabs permitidos de ese mapa. El ID 0 significa sin disfraz.
- Selecciona `collisionShape` en `Prop`: `Capsule` (predeterminado, con `disguiseRadius` y `disguiseHeight`), `Box` (con `disguiseBoxSize`, apoyada en la base) o `Model` (las mallas de los `MeshFilter` activos de `Visual`, incluidos sus hijos). Al seleccionar el prop, el editor dibuja la forma en cian. La selección configura los impactos sobre el disfraz, no sustituye los colliders del objeto original del mapa.
- El movimiento sigue usando la cápsula de `CharacterController`; sus dimensiones se ajustan con `disguiseRadius` y `disguiseHeight` en los tres modos. Caja y modelo giran con el disfraz. `Model` sin mallas vuelve a la cápsula con un aviso; los modelos animados con solo `SkinnedMeshRenderer` no están soportados.
- Las dimensiones guardadas como `NaN` o infinito se recuperan desde los límites locales de las mallas antes de configurar el disfraz; sin malla se usan las dimensiones predeterminadas. Los valores finitos se conservan respetando los mínimos de la cápsula.
- Coloca instancias del prefab en la escena. `canDisappear` decide si participan en el sorteo; `spawnProbability` es la probabilidad individual de aparecer. Con 0 nunca aparece, con 1 siempre aparece. Los permanentes ignoran esa probabilidad.
- `slotId` es único **por instancia en el mapa**, distinto del ID del tipo. El recolector asigna estos IDs y guarda las referencias, incluidos objetos inactivos. Recolecta otra vez al añadir o quitar objetos.
- El servidor publica una semilla por ronda. Cada cliente calcula presencia y orientación con la misma semilla y slots. Son sorteos independientes Bernoulli: no garantizan una cantidad fija de objetos ni de tipos presentes.
- **Rebuild Prop Catalog** actúa sobre el catálogo seleccionado y recoge los prefabs `Prop` del proyecto; para un catálogo exclusivo de un mapa, asigna su lista manualmente. El montaje inicial recoge los tipos presentes en cada escena.

**PropHunt → Repair project prop IDs and map catalogs** revisa todos los tipos e instancias, conserva los IDs positivos no repetidos y asigna IDs libres a duplicados, valores no positivos y variantes sin ID propio. Reconstruye los catálogos existentes ordenados por ID, conservando sus tipos permitidos y añadiendo los presentes en su mapa; no crea un catálogo global ni modifica los `slotId`. Guarda los cambios y deja una copia previa fuera de `Assets` (ruta en la consola). **Validate project prop IDs and map catalogs** comprueba unicidad global, coherencia de instancias y referencias de catálogos y escenas.

## Herramientas del menú PropHunt del Editor

Estas son las ocho opciones del desplegable **PropHunt** de Unity. Los mensajes de resultado y los errores aparecen en la **Consola**. Las herramientas que recorren escenas solicitan guardar los cambios pendientes y restauran las escenas que tenías abiertas al terminar; esto **no deshace** los archivos que hayan guardado durante la operación.

### Configuración y reparación: modifican archivos

| Opción del menú | Qué hace | Cuándo usarla y precauciones |
|---|---|---|
| **Configure enabled build scenes** | Monta Prop Hunt en las escenas habilitadas de Build Profiles. Busca el lobby y su prefab de jugador; configura jugador, arma, audio, paneles UI, mapas con `SpawnPoint`, partida, HUD y generador de props. Recoge props, reconstruye los catálogos de esos mapas, actualiza la lista de mapas del lobby y registra el proyectil en la lista de prefabs de red existente. Crea los assets que falten en `Assets/PropHuntConfig`, guarda los cambios y ejecuta `PropHuntChecks.Run`. | Para el montaje inicial. Necesita un lobby habilitado con `GameBootstrap`, `LobbyManager`, prefab de jugador y lista de prefabs de red. Omite mapas sin puntos de aparición. **Puede sobrescribir referencias, catálogos y configuración personalizada**; no usar como mantenimiento habitual de mapas ya editados. Si crea un proyectil nuevo, su hijo visual queda vacío: hay que asignar el modelo. |
| **Configure apartment door interactions** | Abre `Map_Apartment.unity`, localiza las puertas, armarios y puerta de ducha con los scripts de demostración compatibles, y añade o actualiza `ApartmentDoor`. Enlaza animador, collider y estados de apertura/cierre; mantiene el animador siempre activo, desactiva la entrada local de los scripts de demostración, registra las puertas en `MapSetup.doors` y guarda la escena. | Al añadir puertas compatibles al apartamento o rehacer sus enlaces. Conserva animaciones y colliders originales; no configura puertas arbitrarias de otros mapas. Falla si faltan referencias necesarias o varias puertas comparten el mismo animador. |
| **Rebuild Prop Catalog** | Reemplaza el contenido del **`PropCatalog` seleccionado en Project** por todos los prefabs de `Assets` que tengan `Prop` en la raíz, ordenados por ID. Asigna IDs libres a tipos sin ID válido, duplicados y variantes que necesiten un ID propio. Guarda el catálogo y los assets modificados. | Para reconstruir deliberadamente un catálogo con todos los tipos del proyecto. **No respeta la selección de tipos exclusiva de un mapa**. No sincroniza los IDs sobrescritos de instancias en escenas ni reconstruye otros catálogos; para una reparación global usa la opción siguiente. |
| **Repair project prop IDs and map catalogs** | Recorre los prefabs, escenas y catálogos del proyecto. Conserva IDs positivos únicos, corrige los restantes y sincroniza los IDs de instancias con su prefab canónico, sin cambiar `slotId`. Reconstruye cada catálogo conservando sus tipos e incorporando los presentes en su mapa; corrige rutas de escena no válidas cuando el destino es inequívoco. Guarda y ejecuta la validación global. | Para reparar incoherencias de IDs, referencias o catálogos. Hace copias previas fuera de `Assets`, en una carpeta temporal cuya ruta muestra la Consola. No elimina referencias rotas silenciosamente: puede detenerse para que las repares manualmente. **No es una operación transaccional**: un error posterior no revierte los cambios ya guardados. |

### Validación y pruebas

| Opción del menú | Qué comprueba | Alcance y efectos |
|---|---|---|
| **Validate project prop IDs and map catalogs** | IDs de tipo positivos y únicos en todo el proyecto; estabilidad del asignador; coincidencia entre IDs de instancias y prefabs canónicos; entradas válidas, únicas y resolubles en catálogos; correspondencia entre mapas, escenas y tipos presentes. | Recorre todos los prefabs, catálogos y escenas, no solo los habilitados para build. No reconstruye catálogos ni guarda reparaciones de escenas. La prueba de estabilidad **invoca el asignador de IDs sobre los assets cargados** y falla si este cambia alguno: no debe considerarse una inspección estrictamente libre de cambios en memoria. |
| **Validate apartment props and map** | Los 56 tipos de prop esperados del apartamento, sus IDs y slots, visuales, colisión `Model`, probabilidad del 75 % y aparición determinista. También revisa instancias y registro del generador, catálogo canónico, puntos de aparición de ambos equipos, partida, HUD, cámaras de demostración desactivadas e inclusión del mapa en build y lobby. | Es específica del apartamento y de su configuración actual: cambiar intencionadamente el número de tipos o su probabilidad requiere actualizar la prueba. Crea y destruye clones temporales para probar colisiones y aparición; abre el apartamento y el menú, pero no guarda reparaciones ni entra en Play Mode. |
| **Run deterministic prop checks** | Ejecuta la batería general `PropHuntChecks.Run`: aparición reproducible y distribución de props, colisiones y recuperación de dimensiones inválidas, límites de reglas, FOV, recorte de webcam, visibilidad de nombres, miniaturas, selección de audio, ajustes locales, HUD/VFX, silbidos, modelo del huevo y configuración de assets de colisión para build. | El nombre es histórico: **comprueba más que el sorteo de props**. Usa objetos temporales y revisa assets/configuración; no ejecuta una partida en red ni entra en Play Mode. Un resultado correcto no sustituye un build ni una prueba visual. |
| **Run presentation checks** | Revisa que la pantalla de carga incluida en `Resources` tenga progreso, reintento y salida; que el material de la estela del huevo use el shader y la transparencia esperados; y que los sonidos de clic y disfraz estén asignados y sean distintos. | Ejecuta solo `CheckPresentationAssets`, un subconjunto de la batería anterior. No comprueba toda la HUD, no reproduce los sonidos y no valida el aspecto renderizado ni entra en Play Mode. |

Las [pruebas de integración `PropHuntPlayChecks`](Game.md#comprobaciones-automatizadas) **no tienen una entrada en este desplegable**: se ejecutan mediante sus métodos de editor/batch. Ninguna herramienta de validación sustituye probar el juego con un cliente remoto.
