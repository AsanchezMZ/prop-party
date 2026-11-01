# Prop Hunt · guía de montaje

## Punto de entrada

Abre la escena de menú habilitada en Build Profiles y ejecuta con dos instancias (Multiplayer Play Mode ya está instalado). Crea una sala y conecta la otra instancia a la IP del host, puerto UDP 7777. Ambos jugadores deben pulsar **Estoy listo**; el host elige mapa y comienza. Cambiar mapa desmarca los preparados.

El montaje inicial se hace con **PropHunt → Configure enabled build scenes**. Configura las escenas habilitadas que tengan `SpawnPoint`, crea los datos en `Assets/PropHuntConfig`, conecta el prefab de jugador del `GameBootstrap` y añade el proyectil a la lista de prefabs de red existente. Guarda escenas y assets: úsalo después de guardar tu trabajo. No ejecutes esta migración para retocar mapas ya personalizados: edita sus referencias en el Inspector.

## Añadir un mapa

1. Crea una escena con tu geometría y colliders. Añádela habilitada a Build Profiles.
2. Coloca `SpawnPoint` sobre el suelo. `Any` sirve a ambos equipos; también puedes separar `Props` y `Hunters`. Debe existir un punto compatible con cada equipo.
3. Crea **Create → PropHunt → Map** y un **Prop Catalog**. Asigna nombre, escena (nombre o ruta de Build Profiles), descripción, miniatura y catálogo.
4. Añade un objeto con `NetworkObject`, `MapSetup` y `PropHuntMatch`. Asigna definición, reglas y escena de menú. Ejecuta **Collect scene spawn points** en `MapSetup`.
5. Añade `MatchHUD` al mismo objeto; asigna partida, escena de menú y `Assets/UI/PartyMenu.uss`. En su `UIDocument`, asigna `Assets/PropHuntConfig/PartyPanel.asset` (también lo usa el menú; incluye el tema base para dibujar los controles).
6. Añade un objeto con `NetworkObject` y `MapPropSpawner`; ejecuta **Collect and assign props IDs** después de distribuir los props.
7. Añade la definición a `LobbyManager.mapDefinitions` en el menú. Usa las mismas reglas en lobby y mapa.

La arquitectura no busca nombres de mapas ni carga recursos 3D desde rutas. Las referencias del juego son serializadas. Las rutas de la herramienta de editor solo indican dónde guardar la configuración generada.

## Añadir un prop

- Prefab raíz con `Prop`; hijo `Visual` con el modelo y colliders. El visual debe contener solo presentación: no scripts de gameplay ni `NetworkObject`.
- Asigna un `id` positivo único por tipo y `visual`. El catálogo contiene los prefabs permitidos de ese mapa. El ID 0 significa sin disfraz.
- Ajusta `disguiseRadius` y `disguiseHeight`: la colisión del jugador disfrazado es una cápsula configurable, no una malla exacta.
- Coloca instancias del prefab en la escena. `canDisappear` decide si participan en el sorteo; `spawnProbability` es la probabilidad individual de aparecer. Con 0 nunca aparece, con 1 siempre aparece. Los permanentes ignoran esa probabilidad.
- `slotId` es único **por instancia en el mapa**, distinto del ID del tipo. El recolector asigna estos IDs y guarda las referencias, incluidos objetos inactivos. Recolecta otra vez al añadir o quitar objetos.
- El servidor publica una semilla por ronda. Cada cliente calcula presencia y orientación con la misma semilla y slots. Son sorteos independientes Bernoulli: no garantizan una cantidad fija de objetos ni de tipos presentes.
- **Rebuild Prop Catalog** actúa sobre el catálogo seleccionado y recoge los prefabs `Prop` del proyecto; para un catálogo exclusivo de un mapa, asigna su lista manualmente. El montaje inicial recoge los tipos presentes en cada escena.

## Arma, audio y presentación

`ThrowableDefinition`: prefab de proyectil, velocidad, gravedad, radio de impacto, vida, daño, cadencia, máscara, sonido de lanzamiento/impacto y efecto de impacto.

El prefab necesita `NetworkObject`, `NetworkTransform` **server authority**, `HunterProjectile` y un hijo visual. Asigna el mismo `ThrowableDefinition` al proyectil y al jugador. Registra el prefab en la lista del `NetworkManager`. Sustituye el hijo visual por un huevo, piedra u otro modelo; no necesita Rigidbody ni collider, el servidor realiza barridos esféricos. El prefab generado inicialmente tiene un hijo visual vacío para asignar tu recurso.

`PropHuntPlayer`: raíz del disfraz, modelo normal, arma, AudioSource 3D, silbido, transformación, captura, paso y salto. Pasos y salto pueden llamarse también desde eventos de animación; evita duplicarlos si decides usar esos eventos en lugar del controlador. Sin clips asignados no se reproduce sonido.

`LobbyUI`: USS, fondo opcional, AudioSource, música y clic. `MapDefinition.preview` es la miniatura. El menú presenta una composición de 1280×720 que se escala completa y centrada a la ventana, sin scroll. Inicio, diálogo para unirse y sala tienen distribuciones propias. La ilustración de props está hecha con formas de UI, sin assets externos; colores y presentación se editan en el USS.

La estructura está guardada en `Assets/UI/MainMenu.uxml`, asignada al `UIDocument` de `MainMenu.unity`: se puede abrir con UI Builder sin pulsar Play. `PartyMenuStage` ajusta la composición tanto en editor como en ejecución. `LobbyUI` solo conecta controles y actualiza datos; la lista de jugadores se rellena al conectar. Los Canvas y controles del menú anterior se han eliminado de la escena.

**Tu cara** abre el diálogo de webcam desde inicio o lobby. `FaceCapture`, en el mismo objeto que `LobbyUI`, conserva la referencia a `Assets/Face/FaceConfig.asset` y a la guía `MaskReference`. Permite elegir webcam, pedir permiso, previsualizar con recorte cuadrado y orientación corregida, hacer la foto y volver. Cerrar, desactivar el objeto o cambiar de escena detiene la cámara. No se activa al abrir el menú ni el diálogo. Un fallo o cancelar no sustituye la foto anterior.

La foto JPG se guarda en `Application.persistentDataPath/face.jpg`. `PlayerFace.config` usa el mismo `FaceConfig` para componer la foto con la máscara y la textura base antes de asignarla al material del personaje. Máscara y base deben tener Read/Write activado y coincidir con `FaceConfig.size`. La captura se comparte en la siguiente aparición del jugador; cambiarla en el lobby no necesita volver a conectar. El diálogo indica si falla la escritura y solo puede conservarse en memoria durante esa sesión.

## Reglas y red

`MatchRules` configura mínimo de jugadores, hunters iniciales, tiempo de escondite/caza, intervalo de silbidos, distancia para camuflarse, vida y conversión de capturados. Como mínimo siempre hay un hunter y un prop.

- Espera → escondite (hunters inmóviles y pantalla cubierta) → caza → resultado.
- Captura: convertir a hunter o pasar a espectador, según reglas.
- Si no quedan props ganan los hunters; si termina el tiempo o no quedan hunters ganan los props. Las desconexiones cuentan.
- El host puede repetir la ronda o volver al lobby. No se admiten incorporaciones durante la partida.
- Roles, vida, disfraz, rotación, suspensión, fases, plazos y semillas son `NetworkVariable` escritas por el servidor. Las peticiones de acciones requieren ownership; el servidor comprueba rol, fase, distancia, catálogo y cadencia.
- El movimiento conserva el `NetworkTransform` con autoridad del propietario del proyecto. Es una base para partidas entre amigos, no un controlador antitrampas para competición. Para servidores públicos competitivos habría que migrar el movimiento a simulación autoritativa con predicción.
- Conexión directa por IP; para Internet hace falta un puerto accesible. No incorpora Relay, matchmaking ni migración de host.

## Controles

| Acción | Tecla |
|---|---|
| Mover / correr / andar despacio | WASD / Shift / Ctrl |
| Saltar | Espacio |
| Copiar prop apuntado | E |
| Girar disfraz | Q / R, pasos de 15° |
| Suspender o soltar, también en el aire | F |
| Lanzar | Clic izquierdo |
| Mostrar menú / recuperar control | Esc / Tab |
| Siguiente jugador como espectador | Flecha derecha |

## Comprobaciones

**PropHunt → Run deterministic prop checks** comprueba extremos 0/1, repetibilidad, distribución y props permanentes. Se puede ejecutar en batch con `-executeMethod PropHuntChecks.Run`.

`-batchmode -nographics -executeMethod PropHuntPlayChecks.Run` (sin `-quit`) ejecuta una comprobación de integración en Play Mode: crea un host, prepara el lobby, carga un mapa por Netcode, añade un segundo jugador local de prueba y comprueba roles, suspensión, captura, victoria, repetición y vuelta al lobby. Termina el editor con código 0/1. Esta prueba no sustituye una sesión con un cliente remoto.

Validación manual con dos instancias: preparado/cambio de mapa; misma presencia de props en ambos clientes; espera del hunter; camuflaje cercano frente a lejano; rotación y suspensión visibles remotamente; proyectil bloqueado por paredes; daño, conversión y espectador; silbido espacial con un clip asignado; victoria por tiempo y por captura; desconexión; repetir ronda y volver a sala. Comprueba también el menú a 1280×720 y una ventana estrecha.
