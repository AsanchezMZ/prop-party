# Prop Hunt · juego y referencia general

Reglas, controles, red, presentación y comprobaciones del juego. Para crear mapas y props o consultar las herramientas del Editor, véase [PropHunt.md](PropHunt.md).

## Punto de entrada

Abre la escena de menú habilitada en Build Profiles y ejecuta con dos instancias (Multiplayer Play Mode ya está instalado). Crea una sala y conecta la otra instancia a la IP del host, puerto UDP 7777. Ambos jugadores deben pulsar **Estoy listo**; el host elige mapa y comienza. Cambiar mapa desmarca los preparados.

El selector incluye **Aleatorio**: el host sortea entre los mapas configurados con escena habilitada al iniciar la partida y todos cargan el mismo resultado. Elegir este modo también desmarca los preparados.

**Ajustes del juego:** el botón **AJUSTES** de la cabecera abre un diálogo local con pestañas **Vídeo** (resolución, calidad, pantalla completa sin bordes, VSync y límite de FPS) y **Audio** (general, música y efectos/interfaz/ambiente). Está disponible en inicio y sala, también para invitados; no modifica las reglas de la ronda ni desmarca preparados. Los controles usan `uiClick`. **Aplicar** guarda en `PlayerPrefs` y activa los cambios; **Volver** o Escape descartan lo pendiente. El nombre y **TU CARA** permanecen en la cabecera. No hay remapeo de teclas. La resolución y el modo de pantalla solo se cambian en el ejecutable, no en la Game view del editor.

## Arma, audio y presentación

**Audio central:** asigna las pistas y efectos opcionales en `Assets/PropHuntConfig/Audio.asset`. Ya está conectado al NetworkManager mediante `PartyAudio`. Catálogo completo, eventos, recomendaciones de duración y propuestas adicionales: **`Docs/Audio.md`**. Los campos de audio existentes del jugador/arma conservan prioridad sobre los equivalentes centrales. Los slots vacíos se quedan en silencio.

`ThrowableDefinition`: prefab de proyectil, velocidad, gravedad, radio de impacto, vida, daño, cadencia, máscara, sonido de lanzamiento/impacto y efecto de impacto.

El prefab necesita `NetworkObject`, `NetworkTransform` **server authority**, `HunterProjectile` y un hijo visual. Asigna el mismo `ThrowableDefinition` al proyectil y al jugador. Registra el prefab en la lista del `NetworkManager`. El huevo ya usa `Assets/PropHuntConfig/Egg.obj` y `Egg.mat`: trayectoria recta (gravedad 0), velocidad 45 m/s y recarga de 0.25 s. No necesita Rigidbody ni collider; el servidor realiza barridos esféricos. Se desactiva el búfer de interpolación y el cliente sitúa el visual de los tiros rectos según su origen, velocidad y reloj de servidor; no elimina la latencia real del envío del disparo por red.

`PropHuntPlayer`: raíz del disfraz, modelo normal, arma, AudioSource 3D, transformación, captura, clips de pasos y salto. Los silbidos usan la colección central `Audio.asset.whistles` para ambos tipos, recogible por prefijo `whistle_{nombre}` desde el menú contextual del asset. El servidor elige el mismo clip para todos. Los pasos se disparan en servidor por desplazamiento; el salto mantiene su sonido local desde el controlador. Sin clips asignados no se reproduce sonido.

La barra dentro del botón de silbido representa el tiempo hasta el siguiente silbido automático: baja de llena a vacía con el plazo replicado por el servidor y se reinicia al silbar automáticamente. Durante el escondite permanece llena. La recarga manual de 5 s es independiente: el fondo del botón recupera opacidad de izquierda a derecha mientras está desactivado. La tarjeta de rol usa fondo y borde azules para props, rojos para hunters y amarillos para espectadores.

`LobbyUI`: USS, fondo opcional, AudioSource, música y clic. `MapDefinition.preview` es la miniatura. El menú presenta una composición de 1280×720 que se escala completa y centrada a la ventana, sin scroll. Inicio, diálogo para unirse y sala tienen distribuciones propias. La ilustración de props está hecha con formas de UI, sin assets externos; colores y presentación se editan en el USS.

La estructura está guardada en `Assets/UI/MainMenu.uxml`, asignada al `UIDocument` de `MainMenu.unity`: se puede abrir con UI Builder sin pulsar Play. `PartyMenuStage` ajusta la composición tanto en editor como en ejecución. `LobbyUI` solo conecta controles y actualiza datos; la lista de jugadores se rellena al conectar. Los Canvas y controles del menú anterior se han eliminado de la escena.

**Tu cara** abre el diálogo de webcam desde inicio o lobby. `FaceCapture`, en el mismo objeto que `LobbyUI`, conserva la referencia a `Assets/Face/FaceConfig.asset` y a la guía `MaskReference`. Permite elegir webcam, pedir permiso, previsualizar con recorte cuadrado y orientación corregida, hacer la foto y volver. Cerrar, desactivar el objeto o cambiar de escena detiene la cámara. No se activa al abrir el menú ni el diálogo. Un fallo o cancelar no sustituye la foto anterior.

La foto JPG se guarda en `Application.persistentDataPath/face.jpg`. `PlayerFace.config` usa el mismo `FaceConfig` para componer la foto con la máscara y la textura base antes de asignarla al material del personaje. Máscara y base deben tener Read/Write activado y coincidir con `FaceConfig.size`. La captura se comparte en la siguiente aparición del jugador; cambiarla en el lobby no necesita volver a conectar. El diálogo indica si falla la escritura y solo puede conservarse en memoria durante esa sesión.

En el lobby se comparte solo una miniatura PNG de 64×64 de la foto guardada (máximo 20 KB, dimensiones comprobadas antes de decodificar). Sustituye los números de jugador y se estira para rellenar el bloque. Los recién llegados reciben las miniaturas existentes; las texturas se liberan al salir del lobby. Nunca se transmite vídeo en directo; sin foto, el bloque conserva su fondo de color.

## Reglas y red

`MatchRules` configura mínimo de jugadores, hunters iniciales, tiempo de escondite/caza, intervalo de silbidos, distancia para camuflarse, vida y conversión de capturados. Como mínimo siempre hay un hunter y un prop.

En los ajustes de sala, **Fallos del hunter** permite 1–50 vidas (10 por defecto); **0 = infinito**. Solo se descuenta una vida al impactar un objeto del mapa con componente `Prop`, nunca al suelo, paredes, aire u otros jugadores. Al agotar las vidas, el hunter pasa a espectador; si no quedan buscadores, ganan los escondedores. El presupuesto se reinicia por ronda y al convertirse de escondedor a hunter. El servidor resuelve impactos y descuentos; el HUD muestra las vidas restantes o ∞.

**Identificar escondedores** está activado por defecto y se replica como regla de partida. Tras mantener la retícula sobre el mismo escondedor durante 1,25 s continuos, aparece su nombre resaltado debajo. Se respeta la distancia de visión y los obstáculos; apartar la mira, cambiar de objetivo o abrir el menú reinicia la espera. No cambia las reglas de nametags de equipo. Los escondedores se mueven un 15 % más despacio que los hunters en los tres ritmos de desplazamiento; el salto no cambia.

- Espera → escondite (hunters inmóviles y pantalla cubierta) → caza → resultado.
- Durante el blackout del hunter solo se muestra el panel de espera y su cuenta atrás; el resto del HUD queda oculto, incluidos menús, nombres y efectos.
- Captura: convertir a hunter o pasar a espectador, según reglas.
- Si no quedan props ganan los hunters; si termina el tiempo o no quedan hunters ganan los props. Las desconexiones cuentan.
- El host puede repetir la ronda o volver al lobby. No se admiten incorporaciones durante la partida.
- Roles, vida, disfraz, rotación, suspensión, fases, plazos y semillas son `NetworkVariable` escritas por el servidor. Las peticiones de acciones requieren ownership; el servidor comprueba rol, fase, distancia, catálogo y cadencia.
- El movimiento conserva el `NetworkTransform` con autoridad del propietario del proyecto. Es una base para partidas entre amigos, no un controlador antitrampas para competición. Para servidores públicos competitivos habría que migrar el movimiento a simulación autoritativa con predicción.
- Conexión directa por IP; para Internet hace falta un puerto accesible. No incorpora Relay, matchmaking ni migración de host.

## Controles

En **Apartamento**, buscadores y escondedores pueden abrir/cerrar puertas con **E** al apuntarlas a menos de 3 m. El estado se replica desde la partida; el servidor comprueba equipo, fase, distancia, línea de visión y cadencia. Los buscadores no interactúan durante el tiempo bloqueado de escondite. Las animaciones originales y sus colliders se conservan; el clic local del jugador de demostración está desactivado en esas puertas. **PropHunt → Configure apartment door interactions** vuelve a enlazar las puertas si se añaden nuevas instancias al mapa.

| Acción | Tecla |
|---|---|
| Mover / correr / andar despacio | WASD / Shift / Ctrl |
| Saltar | Espacio |
| Copiar prop apuntado / abrir o cerrar puerta del apartamento | E |
| Girar disfraz | Q / R, pasos de 15° |
| Giro fino del disfraz | Mantener clic derecho + mover el ratón horizontalmente; bloquea la orientación de cámara mientras se mantiene pulsado |
| Suspender o soltar, también en el aire | F |
| Alternar primera / tercera persona (solo escondedores) | V |
| Silbido manual (solo caza, 5 s de recarga) | G o botón 🎵 del HUD con cursor libre |
| Lanzar (buscador, siempre primera persona) | Clic izquierdo |
| Mostrar menú / recuperar control | Esc / Tab |
| Siguiente jugador como espectador | Flecha derecha |

## HUD, ajustes y puntuación

`Assets/UI/MatchHUD.uxml` y `MatchHUD.uss` definen las tarjetas de identidad/reloj/puntuación, retícula segmentada, toasts, indicadores de riesgo y panel de pausa/resultados. Los `UIDocument` de ambos mapas de prueba ya tienen este UXML asignado. El montaje de mapas también lo asigna. El HUD no cambia la activación de cámaras ni fuerza el cursor cada frame.

El prop válido bajo la retícula muestra `[E] nombre` dentro de distancia de camuflaje. Los nametags de escondedores son blancos y solo los ven sus compañeros; los de buscadores son rojos. Los compañeros se ven a través de paredes con flecha y tienen indicador en el borde si quedan fuera de pantalla. Los escondedores solo ven el nombre de un buscador si no está tapado por geometría. La espera del hunter muestra una cuenta atrás grande y el disparo está bloqueado tanto en la entrada local como en el RPC del servidor. Los resultados destacan `¡VICTORIA!`/`¡DERROTA!` y muestran aparte el equipo ganador.

En la sala, **⚙ Ajustes** abre el panel con buscadores, segundos de escondite/caza, silbido automático, vidas y conversión de capturados. Los invitados pueden consultarlo, pero solo el host puede aplicar cambios. Son valores de sesión replicados, validados y copiados al mapa, no cambios al ScriptableObject. Se conservan al volver al lobby mientras continúe el host y se reinician al crear una sesión nueva. El número efectivo de buscadores siempre deja al menos un escondedor.

Puntuación de ronda, siempre calculada en servidor y reiniciada con cada nueva ronda:

- Dentro de `proximityRadius` de cualquier buscador: `proximityPoints` (10 por defecto) cada **3 segundos continuos**.
- Dentro de `visibilityDistance`, del cono de visión y sin un obstáculo delante: `visiblePoints` (20 por defecto) cada **1 segundo continuo**. El servidor comprueba desde los ojos del buscador al centro de la cápsula del escondedor. No equivale a que el jugador humano lo haya identificado.
- Los bonos anteriores se suman, pero varios buscadores no multiplican un mismo bono. Perder proximidad/visibilidad reinicia su reloj correspondiente.
- Silbido manual: `manualWhistlePoints` (100 por defecto), una vez cada **5 segundos**, solo durante caza. El RPC valida ownership, rol, fase y recarga. El silbido automático jamás suma puntos.
- Captura: `capturePoints` (150 por defecto) para el buscador que impactó el último proyectil. Un impacto posterior a la captura no vuelve a puntuar.

Los puntos/radios/FOV se editan en `MatchRules`; los sliders de sesión no modifican esos valores. La clasificación aparece en pausa/resultados. Una captura conserva los puntos previos del escondedor convertido.

La HUD usa una composición de expediente/cartel: tarjeta de identidad y objetivo, reloj central con tira de tiempo, sello de puntuación, segmentos de vida/margen de error y medidor de sospecha. Tiene una variante compacta y conserva los controles, nametags y clasificación. La confirmación de un impacto válido dibuja una marca breve al tirador; no indica aciertos contra objetos normales. El tooltip de interacción permanece estático y los toasts de la derecha son rectangulares, sin esquinas redondeadas.

El final de partida se presenta en tres pasos: aviso inmediato de ronda terminada; título grande **¡VICTORIA!** verde o **¡DERROTA!** rojo a los 3 s, con el equipo ganador debajo; y panel de resumen/clasificación a los 6 s. El título entra con fade y golpe de escala sobre bandas diagonales y se desvanece antes del resumen. El resumen aparece como un folio: entra desplazado y girado, recupera su escala y opacidad y queda quieto, con una esquina doblada. Los eliminados que pasan a espectador conservan su último equipo para el resultado; un observador sin equipo ve el aviso neutral. La música de victoria/derrota se reproduce en bucle hasta repetir ronda o abandonar resultados. Abrir el menú no adelanta el resumen. La secuencia usa tiempo no escalado desde que el cliente observa el fin de ronda y se reinicia al comenzar otra ronda. El giro fino solo actúa con un disfraz durante escondite o caza; funciona también estando suspendido y se replica con validación de ángulo y cadencia en el servidor. Al soltar clic derecho se recupera el control normal de cámara.

**Textos editables:** `Assets/Scripts/Game/MatchHudStrings.cs` contiene únicamente textos que cambian durante la partida: rol, fase, estados, ayudas, avisos y formatos de nombres/puntos. Conserva los marcadores `{0}`, `{1}`, etc., indicados en los comentarios. Los textos fijos (botones, rótulos, mensaje de impacto y frases del blackout) se editan en `Assets/UI/MatchHUD.uxml` y el código no los sobrescribe. Los IDs de controles y clases USS no se traducen. El blackout conserva el bloque y los estilos anteriores al rediseño.

**Presencia:** las barras se llenan según la distancia al buscador más cercano dentro de `MatchRules.proximityRadius`, usando las posiciones ya replicadas. No se cuentan compañeros ni espectadores. Se utiliza el número real de barras del UXML: verde con 1–2, amarillo desde 3; sin buscadores en el radio quedan vacías. La visión directa confirmada por el servidor solo cambia el color a rojo, sin rellenar barras. El heartbeat (sonido y pulso visual) se activa únicamente en amarillo o rojo. Esto no cambia las reglas de puntuación por proximidad o visibilidad. El aviso «¡A ESCONDERSE!» y su toast solo aparecen a escondedores.

El peligro es una viñeta periférica con degradado y cortes de tinta, ámbar por proximidad y coral al quedar a la vista. El daño añade una segunda capa de cortes que se disipa; el centro queda transparente, sin sacudidas de cámara ni fogonazos a pantalla completa. Solo se usa información de riesgo ya replicada; no revela dirección ni posición del buscador.

Los huevos se rompen en fragmentos curvos de cáscara, gotas de yema, neblina de clara, rosetas de salpicadura y un anillo de impacto, orientados con la normal de la superficie. Captura y camuflaje tienen composiciones propias de confeti y expansión. `Resources/PartyParticles.shader` dibuja las formas suaves sin texturas externas; `PropHuntPlayer.effectMaterial` sigue siendo un override para los fragmentos (debe admitir color de vértice para conservar la paleta). Las ráfagas integradas son locales, duran como máximo 1,6 s y se limitan a 18 activas; al superar el límite se retira la más antigua. No añaden colliders, luces ni objetos de red, y los silbidos siguen sin emitir VFX. La salpicadura es transitoria, no un decal persistente sobre objetos móviles. El prefab opcional `impactEffect` sigue siendo un efecto adicional del diseñador y no entra en ese límite.

Los pasos solo se reproducen para buscadores activos, según distancia realmente recorrida y suelo detectado en servidor; saltar o teletransportarse no acumula pasos. Asigna `footstepClips` al jugador para variar muestras, o el campo anterior `footstepSound` como fallback. Sin clips no habrá audio. Un `AudioSource` 3D permite que los demás jugadores los oigan. Los escondedores no generan pasos.

La cámara del propietario se habilita una vez al aparecer y se deshabilita al desaparecer; V solo cambia su posición/modo, no alterna GameObjects o AudioListeners. En FPV se ocultan únicamente los renderers del propio jugador a nivel local; los demás siguen viéndolos. Las colisiones de TPV ignoran al propio jugador y al objetivo observado. `obstacles` debe incluir la geometría del mapa.

## Comprobaciones automatizadas

`PropHuntChecks` comprueba límites de ajustes, FOV y recorte de webcam; también añade reglas de visibilidad de nametags, validación de miniaturas y configuración/modelo del huevo. `PropHuntPlayChecks` cubre ajustes lobby→mapa→lobby, cámaras, HUD, puntuación, silbidos, pasos, captura y reset. Se añadieron checks de disparo bloqueado durante escondite, solicitud/cadencia del sonido de puertas y parada del audio de resultados. La versión anterior pasó en Unity 6000.6.4f1; estas ampliaciones todavía no se han ejecutado.

`CheckHudAndVfxDesign` comprueba los bindings de la HUD, capas que no interceptan clics, segmentos de vida y shader incluido en build. En Play Mode, `CheckVisualEffects` verifica orientación del huevo, sus cinco capas, presupuesto de ráfagas y ausencia de colliders/luces y cambios en el generador aleatorio del gameplay. Este rediseño se ha compilado con .NET sin ejecutar Unity; quedan pendientes la validación visual y el perfilado en juego.

`CheckBuildCollisionAssets` comprueba que la puerta de ducha tenga el mismo tipo `MeshCollider` en prefab y escena, sin perder su referencia de interacción. También valida Read/Write en las dos mallas de puertas con escala no uniforme y el precálculo de colisión triangular/convexa en los modelos señalados por el build. Read/Write se limita a esas dos mallas porque conserva una copia adicional en memoria; no se activa globalmente. Estos ajustes requieren reimportación y un nuevo build para confirmar los avisos.

Las comprobaciones de proximidad y pasos usan un escenario de física aislado en el host; no sustituyen probar los colliders y capas de cada mapa final. Revisar manualmente el aspecto del HUD/VFX, la webcam y el audio con clips asignados. Los emojis dependen de los glifos/fallback de fuentes instalados en Unity: si faltan, asigna una fuente de símbolos compatible en Panel Text Settings.

**PropHunt → Run deterministic prop checks** comprueba extremos 0/1, repetibilidad, distribución y props permanentes. Se puede ejecutar en batch con `-executeMethod PropHuntChecks.Run`.

`-batchmode -nographics -executeMethod PropHuntPlayChecks.Run` (sin `-quit`) ejecuta una comprobación de integración en Play Mode: crea un host, prepara el lobby, carga un mapa por Netcode, añade un segundo jugador local de prueba y comprueba roles, suspensión, captura, victoria, repetición y vuelta al lobby. Termina el editor con código 0/1. Esta prueba no sustituye una sesión con un cliente remoto.

También comprueba la selección aleatoria (excluyendo mapas no disponibles y desmarcando preparados), el cierre repetido y la creación de una nueva sala después de cerrar la anterior, tratando los errores de consola como fallos.

Validación manual con dos instancias: preparado/cambio de mapa; misma presencia de props en ambos clientes; espera del hunter; camuflaje cercano frente a lejano; rotación y suspensión visibles remotamente; proyectil bloqueado por paredes; daño, conversión y espectador; silbido espacial con un clip asignado; victoria por tiempo y por captura; desconexión; repetir ronda y volver a sala. Comprueba también el menú a 1280×720 y una ventana estrecha.
