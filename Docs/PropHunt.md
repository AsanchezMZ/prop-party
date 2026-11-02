# Prop Hunt · guía de montaje

## Punto de entrada

Abre la escena de menú habilitada en Build Profiles y ejecuta con dos instancias (Multiplayer Play Mode ya está instalado). Crea una sala y conecta la otra instancia a la IP del host, puerto UDP 7777. Ambos jugadores deben pulsar **Estoy listo**; el host elige mapa y comienza. Cambiar mapa desmarca los preparados.

El selector incluye **Aleatorio**: el host sortea entre los mapas configurados con escena habilitada al iniciar la partida y todos cargan el mismo resultado. Elegir este modo también desmarca los preparados.

**Ajustes del juego:** el botón **AJUSTES** de la cabecera abre un diálogo local con pestañas **Vídeo** (resolución, calidad, pantalla completa sin bordes, VSync y límite de FPS) y **Audio** (general, música y efectos/interfaz/ambiente). Está disponible en inicio y sala, también para invitados; no modifica las reglas de la ronda ni desmarca preparados. Los controles usan `uiClick`. **Aplicar** guarda en `PlayerPrefs` y activa los cambios; **Volver** o Escape descartan lo pendiente. El nombre y **TU CARA** permanecen en la cabecera. No hay remapeo de teclas. La resolución y el modo de pantalla solo se cambian en el ejecutable, no en la Game view del editor.

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
- Coloca instancias del prefab en la escena. `canDisappear` decide si participan en el sorteo; `spawnProbability` es la probabilidad individual de aparecer. Con 0 nunca aparece, con 1 siempre aparece. Los permanentes ignoran esa probabilidad.
- `slotId` es único **por instancia en el mapa**, distinto del ID del tipo. El recolector asigna estos IDs y guarda las referencias, incluidos objetos inactivos. Recolecta otra vez al añadir o quitar objetos.
- El servidor publica una semilla por ronda. Cada cliente calcula presencia y orientación con la misma semilla y slots. Son sorteos independientes Bernoulli: no garantizan una cantidad fija de objetos ni de tipos presentes.
- **Rebuild Prop Catalog** actúa sobre el catálogo seleccionado y recoge los prefabs `Prop` del proyecto; para un catálogo exclusivo de un mapa, asigna su lista manualmente. El montaje inicial recoge los tipos presentes en cada escena.

## Arma, audio y presentación

**Audio central:** asigna las pistas y efectos opcionales en `Assets/PropHuntConfig/Audio.asset`. Ya está conectado al NetworkManager mediante `PartyAudio`. Catálogo completo, eventos, recomendaciones de duración y propuestas adicionales: **`Docs/Audio.md`**. Los campos de audio existentes del jugador/arma conservan prioridad sobre los equivalentes centrales. Los slots vacíos se quedan en silencio.

`ThrowableDefinition`: prefab de proyectil, velocidad, gravedad, radio de impacto, vida, daño, cadencia, máscara, sonido de lanzamiento/impacto y efecto de impacto.

El prefab necesita `NetworkObject`, `NetworkTransform` **server authority**, `HunterProjectile` y un hijo visual. Asigna el mismo `ThrowableDefinition` al proyectil y al jugador. Registra el prefab en la lista del `NetworkManager`. El huevo ya usa `Assets/PropHuntConfig/Egg.obj` y `Egg.mat`: trayectoria recta (gravedad 0), velocidad 45 m/s y recarga de 0.25 s. No necesita Rigidbody ni collider; el servidor realiza barridos esféricos. Se desactiva el búfer de interpolación y el cliente sitúa el visual de los tiros rectos según su origen, velocidad y reloj de servidor; no elimina la latencia real del envío del disparo por red.

`PropHuntPlayer`: raíz del disfraz, modelo normal, arma, AudioSource 3D, transformación, captura, clips de pasos y salto. Los silbidos usan la colección central `Audio.asset.whistles` para ambos tipos, recogible por prefijo `whistle_{nombre}` desde el menú contextual del asset. El servidor elige el mismo clip para todos. Los pasos se disparan en servidor por desplazamiento; el salto mantiene su sonido local desde el controlador. Sin clips asignados no se reproduce sonido.

`LobbyUI`: USS, fondo opcional, AudioSource, música y clic. `MapDefinition.preview` es la miniatura. El menú presenta una composición de 1280×720 que se escala completa y centrada a la ventana, sin scroll. Inicio, diálogo para unirse y sala tienen distribuciones propias. La ilustración de props está hecha con formas de UI, sin assets externos; colores y presentación se editan en el USS.

La estructura está guardada en `Assets/UI/MainMenu.uxml`, asignada al `UIDocument` de `MainMenu.unity`: se puede abrir con UI Builder sin pulsar Play. `PartyMenuStage` ajusta la composición tanto en editor como en ejecución. `LobbyUI` solo conecta controles y actualiza datos; la lista de jugadores se rellena al conectar. Los Canvas y controles del menú anterior se han eliminado de la escena.

**Tu cara** abre el diálogo de webcam desde inicio o lobby. `FaceCapture`, en el mismo objeto que `LobbyUI`, conserva la referencia a `Assets/Face/FaceConfig.asset` y a la guía `MaskReference`. Permite elegir webcam, pedir permiso, previsualizar con recorte cuadrado y orientación corregida, hacer la foto y volver. Cerrar, desactivar el objeto o cambiar de escena detiene la cámara. No se activa al abrir el menú ni el diálogo. Un fallo o cancelar no sustituye la foto anterior.

La foto JPG se guarda en `Application.persistentDataPath/face.jpg`. `PlayerFace.config` usa el mismo `FaceConfig` para componer la foto con la máscara y la textura base antes de asignarla al material del personaje. Máscara y base deben tener Read/Write activado y coincidir con `FaceConfig.size`. La captura se comparte en la siguiente aparición del jugador; cambiarla en el lobby no necesita volver a conectar. El diálogo indica si falla la escritura y solo puede conservarse en memoria durante esa sesión.

En el lobby se comparte solo una miniatura PNG de 64×64 de la foto guardada (máximo 20 KB, dimensiones comprobadas antes de decodificar). Sustituye los números de jugador y se estira para rellenar el bloque. Los recién llegados reciben las miniaturas existentes; las texturas se liberan al salir del lobby. Nunca se transmite vídeo en directo; sin foto, el bloque conserva su fondo de color.

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
| Alternar primera / tercera persona (solo escondedores) | V |
| Silbido manual (solo caza, 5 s de recarga) | G o botón 🎵 del HUD con cursor libre |
| Lanzar (buscador, siempre primera persona) | Clic izquierdo |
| Mostrar menú / recuperar control | Esc / Tab |
| Siguiente jugador como espectador | Flecha derecha |

## HUD, ajustes y puntuación

`Assets/UI/MatchHUD.uxml` y `MatchHUD.uss` definen la barra horizontal superior, la retícula de punto, toasts, indicadores de riesgo y panel de pausa/resultados. Los `UIDocument` de ambos mapas de prueba ya tienen este UXML asignado. El montaje de mapas también lo asigna. El HUD no cambia la activación de cámaras ni fuerza el cursor cada frame.

El prop válido bajo la retícula muestra `[E] nombre` dentro de distancia de camuflaje. Los nametags de escondedores son blancos y solo los ven sus compañeros; los de buscadores son rojos. Los compañeros se ven a través de paredes con flecha y tienen indicador en el borde si quedan fuera de pantalla. Los escondedores solo ven el nombre de un buscador si no está tapado por geometría. La espera del hunter muestra una cuenta atrás grande y el disparo está bloqueado tanto en la entrada local como en el RPC del servidor. Los resultados destacan `¡VICTORIA!`/`¡DERROTA!` y muestran aparte el equipo ganador.

En la sala, **⚙ Ajustes** abre el panel con buscadores, segundos de escondite/caza, silbido automático, vidas y conversión de capturados. Los invitados pueden consultarlo, pero solo el host puede aplicar cambios. Son valores de sesión replicados, validados y copiados al mapa, no cambios al ScriptableObject. Se conservan al volver al lobby mientras continúe el host y se reinician al crear una sesión nueva. El número efectivo de buscadores siempre deja al menos un escondedor.

Puntuación de ronda, siempre calculada en servidor y reiniciada con cada nueva ronda:

- Dentro de `proximityRadius` de cualquier buscador: `proximityPoints` (10 por defecto) cada **3 segundos continuos**.
- Dentro de `visibilityDistance`, del cono de visión y sin un obstáculo delante: `visiblePoints` (20 por defecto) cada **1 segundo continuo**. El servidor comprueba desde los ojos del buscador al centro de la cápsula del escondedor. No equivale a que el jugador humano lo haya identificado.
- Los bonos anteriores se suman, pero varios buscadores no multiplican un mismo bono. Perder proximidad/visibilidad reinicia su reloj correspondiente.
- Silbido manual: `manualWhistlePoints` (100 por defecto), una vez cada **5 segundos**, solo durante caza. El RPC valida ownership, rol, fase y recarga. El silbido automático jamás suma puntos.
- Captura: `capturePoints` (150 por defecto) para el buscador que impactó el último proyectil. Un impacto posterior a la captura no vuelve a puntuar.

Los puntos/radios/FOV se editan en `MatchRules`; los sliders de sesión no modifican esos valores. La clasificación aparece en pausa/resultados. Una captura conserva los puntos previos del escondedor convertido.

VFX geométricos sin texturas: confeti de captura, destellos de impacto/camuflaje y halo local pulsante cuando hay riesgo. `PropHuntPlayer.effectMaterial` permite sustituir el material de las piezas sin tocar código. El halo solo lo ve el escondedor, no revela su posición a buscadores. Los silbidos/burlas no emiten VFX.

Los pasos solo se reproducen para buscadores activos, según distancia realmente recorrida y suelo detectado en servidor; saltar o teletransportarse no acumula pasos. Asigna `footstepClips` al jugador para variar muestras, o el campo anterior `footstepSound` como fallback. Sin clips no habrá audio. Un `AudioSource` 3D permite que los demás jugadores los oigan. Los escondedores no generan pasos.

La cámara del propietario se habilita una vez al aparecer y se deshabilita al desaparecer; V solo cambia su posición/modo, no alterna GameObjects o AudioListeners. En FPV se ocultan únicamente los renderers del propio jugador a nivel local; los demás siguen viéndolos. Las colisiones de TPV ignoran al propio jugador y al objetivo observado. `obstacles` debe incluir la geometría del mapa.

### Comprobaciones automatizadas

`PropHuntChecks` comprueba límites de ajustes, FOV y recorte de webcam; también añade reglas de visibilidad de nametags, validación de miniaturas y configuración/modelo del huevo. `PropHuntPlayChecks` cubre ajustes lobby→mapa→lobby, cámaras, HUD, puntuación, silbidos, pasos, captura y reset. Se añadieron checks de disparo bloqueado durante escondite, solicitud/cadencia del sonido de puertas y parada del audio de resultados. La versión anterior pasó en Unity 6000.6.4f1; estas ampliaciones todavía no se han ejecutado.

Las comprobaciones de proximidad y pasos usan un escenario de física aislado en el host; no sustituyen probar los colliders y capas de cada mapa final. Revisar manualmente el aspecto del HUD/VFX, la webcam y el audio con clips asignados. Los emojis dependen de los glifos/fallback de fuentes instalados en Unity: si faltan, asigna una fuente de símbolos compatible en Panel Text Settings.

**PropHunt → Run deterministic prop checks** comprueba extremos 0/1, repetibilidad, distribución y props permanentes. Se puede ejecutar en batch con `-executeMethod PropHuntChecks.Run`.

`-batchmode -nographics -executeMethod PropHuntPlayChecks.Run` (sin `-quit`) ejecuta una comprobación de integración en Play Mode: crea un host, prepara el lobby, carga un mapa por Netcode, añade un segundo jugador local de prueba y comprueba roles, suspensión, captura, victoria, repetición y vuelta al lobby. Termina el editor con código 0/1. Esta prueba no sustituye una sesión con un cliente remoto.

También comprueba la selección aleatoria (excluyendo mapas no disponibles y desmarcando preparados), el cierre repetido y la creación de una nueva sala después de cerrar la anterior, tratando los errores de consola como fallos.

Validación manual con dos instancias: preparado/cambio de mapa; misma presencia de props en ambos clientes; espera del hunter; camuflaje cercano frente a lejano; rotación y suspensión visibles remotamente; proyectil bloqueado por paredes; daño, conversión y espectador; silbido espacial con un clip asignado; victoria por tiempo y por captura; desconexión; repetir ronda y volver a sala. Comprueba también el menú a 1280×720 y una ventana estrecha.
