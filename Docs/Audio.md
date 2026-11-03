# Catálogo de audio · Prop Party

## Dónde asignarlo

Selecciona **`Assets/PropHuntConfig/Audio.asset`** en Project. Todos los slots son opcionales: arrastra únicamente los clips que elijas. Vacío significa silencio, no un recurso perdido.

Los WAV que tienen correspondencia por nombre en `Assets/Sound/` ya están asignados en ese asset; `footsteps_1`–`footsteps_5` van a la colección de pasos y todos los `whistle_{nombre}` a `whistles`. `victory.wav` y `defeat.wav` son las pistas largas de resultados. `explosion.wav` queda sin asignar porque no hay un slot de explosión y ya existe `impactSound.wav`; los slots sin un WAV correspondiente siguen vacíos.

`PartyAudio`, ya enlazado en el NetworkManager del menú, mantiene música y avisos al cambiar de escena. Hay canales separados para música, interfaz, avisos de ronda, peligro y ambiente. Durante la carga no suena música. Los avisos de ronda detienen la música mientras suenan: la pista de esconderse empieza cuando termina `roundStart`, no encima de ella. El inicio espera 2,5 s y los avisos obligatorios dejan al menos 5 s de separación tras acabar el anterior; los avisos obsoletos no se acumulan.

Los clips que ya asignes directamente al jugador o a `ThrowableDefinition` tienen prioridad sobre el slot central equivalente. Así puedes tener distintos impactos por arma sin modificar el catálogo común. Los avisos locales no generan RPC nuevos; las acciones espaciales aprovechan los eventos de juego existentes.

## Música y ambiente — slots conectados

| Slot | Cuándo | Propuesta | Duración orientativa |
|---|---|---|---|
| `menuMusic` | Menú sin conexión | Groove juguetón, percusión de objetos y bajo ligero | Bucle de 45–90 s |
| `lobbyMusic` | Sala y espera | Variante relajada del tema principal | Bucle de 30–60 s |
| `hidingMusic` | Tiempo para esconderse | Marimba/pizzicatos y prisa suave, no terror | Bucle de 20–40 s |
| `gameplayMusic` | Caza | Ritmo travieso, con espacio para escuchar pistas | Bucle de 60–120 s |
| `finalStretchMusic` | Últimos 30 s de caza **o** último escondedor | Una sola pista de tensión para ambos eventos | Bucle de 15–30 s |
| `ambienceLoop` | Mapa durante espera/escondite/caza | Aire de habitación o exterior suave, sin pasos falsos | Bucle de 30–90 s |

Si falta `finalStretchMusic`, sigue la música normal de caza. Los otros slots vacíos no reproducen música. Ambiente y música son independientes. Todas estas pistas se reproducen en 2D.

La condición de último escondedor está replicada por el servidor y solo se activa si la ronda empezó con más de un escondedor: no se activa en 1vs1. Su pista solo la oye el escondedor restante, no los hunters ni espectadores. Los últimos 30 s cambian la música de los jugadores activos; el espectador conserva su pista propia si está asignada. Ambos eventos usan el mismo canal y pista: coincidir no reinicia ni duplica el audio. El campo anterior `lastHider` se conserva, pero ya no dispara un efecto adicional.

El BGM general y las pistas de resultados usan un volumen base de 0,18 (antes 0,35), multiplicado por el ajuste de música del usuario. Esconderse y tensión usan además un factor de 0,7, para dejar más espacio a los efectos. Los avisos de inicio de ronda siguen separados del BGM.

## Transiciones de ronda — conectadas, 2D

| Slot | Evento | Propuesta |
|---|---|---|
| `roundStart` | Inicio de ronda | Mini fanfarria o pop de salida, 0.5–1.5 s |
| `huntStart` | Se libera a los buscadores | Timbre, silbato de árbitro o golpe musical, 0.3–1 s |
| `lastSecondsWarning` | Últimos 10 s, una sola vez | Alerta breve, no una cuenta atrás larga que se solape |
| `victory` | Ganó tu equipo | Pista completa de resultados de victoria, aproximadamente 30 s |
| `defeat` | Perdió tu equipo | Pista completa de resultados de derrota, aproximadamente 30 s |
| `spectatorEnter` | Estás como espectador durante la ronda | Música de espectador en el canal de música, con su volumen local; sustituye la pista de juego |

No hay pista genérica de resultados ni efectos separados de fin, victoria o derrota: `victory` y `defeat` son las pistas de resultado, reproducidas **en bucle** en un canal propio que se detiene al salir de resultados, repetir ronda o cambiar de escena. Deja vacío el slot que no quieras usar. Un capturado convertido cuenta como buscador; si pasa a espectador, conserva su último equipo activo para elegir victoria o derrota. Al terminar la ronda, la música de espectador se corta antes del resultado, incluso si eliminación y fin llegan en el mismo frame (1vs1). Ya no se reproduce como efecto con `effectsVolume`, sino como música con `musicVolume` y el ajuste local de música.

## Peligro — conectado, solo lo oye el escondedor

| Slot | Evento | Propuesta |
|---|---|---|
| `nearbyEnter` | Un buscador entra en proximidad | Desasociado; el WAV se conserva |
| `nearbyExit` | Sales del radio durante la caza | Desasociado; el WAV se conserva |
| `nearbyLoop` | Hay buscadores cerca | `heartbeat.wav`, en bucle local solo para el escondedor |
| `spottedEnter` | Campo de visión con línea directa | Acento de cuerda cómico, 0.2–0.7 s |
| `spottedLoop` | Sigues a la vista | Variante más rápida del latido; sustituye el bucle anterior |

No revela al buscador dónde estás. «A la vista» es una comprobación geométrica, no que el humano te haya reconocido. La cercanía usa solo el latido, sin cambiar de bucle al entrar en visión. Estos avisos deben dejar oír pasos y silbidos.

## Acciones y combate — conectados

| Slot | Alcance | Propuesta |
|---|---|---|
| `whistles` | 3D | Colección compartida de clips `whistle_{nombre}` para silbidos automáticos y manuales; selección aleatoria en servidor |
| `disguise` | 3D | Poof o pop elástico de transformación, 0.2–0.6 s |
| `rotate` | 3D | Tick de juguete muy discreto, 0.05–0.15 s |
| `freeze` | 3D | Clic de bloqueo y desbloqueo, 0.1–0.3 s |
| `jump` | Local desde el jugador | Resorte o esfuerzo leve; opcional para no dar ruido a escondedores |
| `footsteps` | 3D, solo buscadores | 4–8 muestras neutras, 0.1–0.35 s, elegidas al azar |
| `throwSound` | 3D | Whoosh de lanzamiento, 0.1–0.4 s |
| `impactSound` | 3D, en el impacto | Huevo: splat; piedra: clac; puede sustituirse por arma |
| `hurt` | 3D | Golpe cartoon, 0.1–0.4 s |
| `captured` | 3D | Estallido de confeti/revelación, 0.4–1 s |
| `hitConfirmation` | Local del tirador | Tick limpio de acierto, 0.05–0.2 s |
| `scoreTick` | Local | Desasociado; se conservan el WAV y la puntuación visual |

Los sonidos 3D nacen en el jugador/proyectil y se atenúan con la distancia. El salto conserva su ejecución local, sin nuevo RPC. Una captura puede disparar impacto, daño y revelación: usa clips complementarios o deja algunos vacíos.

### Colección de silbidos

Importa archivos como `whistle_corto.wav`, `whistle_pajaro.wav` o `whistle_burla.ogg`. En el Inspector de `Audio.asset`, abre el menú contextual del componente y ejecuta **Collect whistle_* audio clips**. Recoge todos los AudioClip del proyecto cuyo nombre empiece exactamente por `whistle_` y tenga un nombre detrás, ordenados por nombre/ruta. La acción reemplaza la colección y admite Undo; guarda el asset después. También puedes asignar la lista a mano.

El juego usa referencias serializadas: no busca archivos ni carga por nombre durante la partida. El servidor sortea entre entradas no vacías con peso inversamente proporcional a su duración (mínimo de cálculo: 0,25 s), por lo que los silbidos cortos salen más que los largos. Transmite el índice a todos los clientes; todos oyen el mismo clip. Usa el mismo catálogo/build en todos los participantes. Repite la recolección al importar nuevos silbidos.

El intervalo automático predeterminado es de 45 s, con una variación adicional de 0–12 s por jugador y mínimo efectivo de 35 s. El primer aviso espera al menos 40 s tras esconderse y respeta la duración de los avisos iniciales. Entre silbidos automáticos de distintos jugadores se deja la duración del clip anterior más 8 s. Los manuales conservan su recarga de 5 s; un silbido nuevo del mismo jugador sustituye al anterior en vez de superponerse.

No hay diferencia sonora entre automático y manual, ni un override de silbido por jugador. La diferencia es únicamente de reglas: el manual suma puntos y tiene 5 segundos de recarga; el automático no suma. Ambos son solo audio, sin anillos ni otros VFX que revelen su posición. Una colección vacía o sin clips válidos se queda en silencio. La colección incluye `whistle_scream`.

## Interfaz y webcam — conectados, 2D

| Slot | Evento | Propuesta |
|---|---|---|
| `uiClick` | Botones de menú/lobby | Pop o toque de madera, 0.05–0.15 s |
| `uiBack` | Botón Volver en ajustes/conexión/webcam | Toque descendente, 0.05–0.2 s |
| `uiConfirm` | Aplicar ajustes | Doble pop/acorde, 0.15–0.4 s |
| `uiError` | Error de conexión/transporte | Alerta suave, 0.15–0.4 s |
| `connected` | Tú u otra persona entra en la sala | Mismo sonido de entrada para todos, sin reproducirlo por la lista inicial de participantes |
| `disconnected` | Tú u otra persona sale de la sala | Mismo sonido de salida para todos; sin duplicar tu propia desconexión |
| `ready` | Te marcas preparado | Sonido afirmativo, 0.1–0.3 s |
| `unready` | Cancelas tu estado preparado | Sonido distinto, por ejemplo una nota descendente, 0.1–0.3 s |
| `photoCaptured` | Foto capturada y preparada | Obturador simpático, 0.1–0.4 s |

Asigna clips diferentes a `connected` y `disconnected`, y también a `ready` y `unready`; los cuatro slots son independientes. Si un slot está vacío, ese evento queda en silencio (no reutiliza el clip opuesto). El botón Listo reproduce únicamente `ready`/`unready`, nunca el clic genérico. El clic genérico puede acompañar a otras confirmaciones específicas. No suena por cada paso de un slider. Esta conexión de clics cubre menú/lobby, no es un sonido universal en todos los botones del proyecto.

## Nuevos avisos conectados

| Slots | Evento | Funcionamiento |
|---|---|---|
| `lastHider` | El servidor detecta que queda un escondedor | Una vez por ronda, 2D y sin identidad/coordenadas. No suena si la ronda ya empezó con un único escondedor. También cuenta una desconexión que reduzca el equipo a uno. |
| `shotMiss` | Proyectil que golpea mundo/buscador o agota su vida sin acertar | Sonido 3D en el punto de choque o última posición. No se dispara al limpiar proyectiles por final de ronda. |

`ThrowableDefinition.missSound` permite sustituir el fallo por arma. Si está vacío, usa `shotMiss`; si ambos están vacíos, un choque conserva el impacto genérico existente. El fallo reemplaza al impacto sonoro, no añade dos golpes superpuestos. Agotar la vida sin clip de fallo se queda en silencio; no crea una explosión artificial en el aire.

## Puertas e interacción — slots y componente preparados

Las puertas físicas actuales con `HingeJoint` ya suenan al empujarlas mediante `PushObjects`: el propietario solicita el sonido, el servidor valida fase/movimiento, distancia, bisagra cercana y recarga de 0.6 s, y lo replica en 3D a todos los clientes. No necesitan añadirles un `NetworkObject` ni `DoorAudio`. Esto conecta el audio al empuje existente; no cambia la simulación ni la sincronización física de las puertas.

| Slot central | Método de `DoorAudio` | Propuesta |
|---|---|---|
| `doorKnock` | `Opened()`, `Closed()`, `HandleUsed()` o `Knocked()` | Único sonido compartido para cualquier evento de puerta, 0.2–0.6 s |

Añade **`DoorAudio`** al objeto de puerta que vaya a controlar la interacción. Requiere `NetworkObject` y `AudioSource` (se añaden automáticamente); configura distancia/rolloff del AudioSource y registra el prefab si la puerta se crea dinámicamente. Usa siempre `doorKnock` (o el override local `sound`) para todos los eventos. Para puertas colocadas en escena se usa el flujo normal de objetos de escena de Netcode.

El controlador definitivo debe validar la interacción **en servidor** y llamar al método correspondiente cuando la acción suceda. `DoorAudio` envía el sonido 3D a los clientes y admite clips por puerta con prioridad sobre los centrales. Ignora llamadas sin spawn, llamadas de cliente y duplicados del mismo evento en menos de 0.1 s.

Los cuatro métodos de `DoorAudio` son una alternativa para futuros controladores autoritativos; no deben conectarse además del empuje existente para un mismo evento, para evitar duplicados. No se añade una nueva mecánica de apertura.

## Más ideas para elegir después — propuestas, NO implementadas

- **Pasos por superficie**: madera, metal, baldosa y césped; requiere identificar superficies.
- **Foley del disfraz**: crujido de caja, cerámica o metal; cuidar que no contradiga el movimiento silencioso de escondedores.
- **Racha de riesgo**: escalones musicales según tiempo continuado cerca/a la vista, no por cada punto.
- **Voces/reacciones**: sorpresa, burla, risa y festejo, con control de frecuencia.
- **Ambiente por mapa**: viento, reloj, cocina, patio o fábrica, asignable a cada mapa final.
- **Música por equipo y capas**: percusión de buscadores, melodía pícara de escondedores y capa de tensión; requiere pistas alineadas.
- **Victoria por equipo**: fanfarrias temáticas distintas.
- **Accesibilidad sonora**: alertas suaves y controles de volumen en el menú; mantener equivalentes visuales.

## Preparación de archivos y primera selección

- WAV limpios para efectos; mono para 3D, estéreo para música/ambiente 2D.
- Bucles sin clics ni colas duplicadas. Evita silencios largos al principio de efectos.
- La música debe dejar espacio para pistas de juego; un silbido no debería quedar enterrado en la mezcla.
- **Para empezar elegiría**: música de menú/caza, inicio/fin de ronda, una colección de silbidos, lanzamiento/impacto, pasos, cercanía y captura. Todo lo demás puede quedarse vacío.
