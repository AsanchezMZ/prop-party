# Catálogo de audio · Prop Party

## Dónde asignarlo

Selecciona **`Assets/PropHuntConfig/Audio.asset`** en Project. Todos los slots son opcionales: arrastra únicamente los clips que elijas. Vacío significa silencio, no un recurso perdido.

`PartyAudio`, ya enlazado en el NetworkManager del menú, mantiene música y avisos al cambiar de escena. Hay canales y volúmenes separados para música, efectos, peligro y ambiente. La música tiene una transición corta de volumen; los avisos no la interrumpen.

Los clips que ya asignes directamente al jugador o a `ThrowableDefinition` tienen prioridad sobre el slot central equivalente. Así puedes tener distintos impactos por arma sin modificar el catálogo común. Los avisos locales no generan RPC nuevos; las acciones espaciales aprovechan los eventos de juego existentes.

## Música y ambiente — slots conectados

| Slot | Cuándo | Propuesta | Duración orientativa |
|---|---|---|---|
| `menuMusic` | Menú sin conexión | Groove juguetón, percusión de objetos y bajo ligero | Bucle de 45–90 s |
| `lobbyMusic` | Sala y espera | Variante relajada del tema principal | Bucle de 30–60 s |
| `hidingMusic` | Tiempo para esconderse | Marimba/pizzicatos y prisa suave, no terror | Bucle de 20–40 s |
| `gameplayMusic` | Caza | Ritmo travieso, con espacio para escuchar pistas | Bucle de 60–120 s |
| `finalStretchMusic` | Últimos 30 s de caza | Variante más rápida e intensa | Bucle de 15–30 s |
| `resultsMusic` | Resultados | Celebración ligera que no castigue a quien perdió | Bucle de 20–45 s |
| `ambienceLoop` | Mapa durante espera/escondite/caza | Aire de habitación o exterior suave, sin pasos falsos | Bucle de 30–90 s |

Si falta `finalStretchMusic`, sigue la música normal de caza. Los otros slots vacíos no reproducen música. Ambiente y música son independientes. Todas estas pistas se reproducen en 2D.

## Transiciones de ronda — conectadas, 2D

| Slot | Evento | Propuesta |
|---|---|---|
| `roundStart` | Inicio de ronda | Mini fanfarria o pop de salida, 0.5–1.5 s |
| `huntStart` | Se libera a los buscadores | Timbre, silbato de árbitro o golpe musical, 0.3–1 s |
| `lastSecondsWarning` | Últimos 10 s, una sola vez | Alerta breve, no una cuenta atrás larga que se solape |
| `roundEnd` | Termina cualquier ronda | Campana neutra, 0.5–1 s |
| `victory` | Tu rol actual coincide con el ganador | Fanfarria alegre, 1–3 s |
| `defeat` | Tu rol activo no coincide con el ganador | Caída musical cómica, 1–2 s |
| `spectatorEnter` | Pasas a espectador | Aire o plop suave, 0.2–0.6 s |

`roundEnd` puede sonar junto a victoria/derrota: deja uno vacío si prefieres una sola señal. Un capturado convertido cuenta como buscador; el espectador solo recibe el aviso neutro de fin.

## Peligro — conectado, solo lo oye el escondedor

| Slot | Evento | Propuesta |
|---|---|---|
| `nearbyEnter` | Un buscador entra en proximidad | Golpe de corazón o sorpresa, 0.2–0.5 s |
| `nearbyExit` | Sales del radio durante la caza | Resoplido de alivio o caída de tensión, 0.2–0.7 s |
| `nearbyLoop` | Hay buscadores cerca | Latido suave, bucle de 2–4 s |
| `spottedEnter` | Campo de visión con línea directa | Acento de cuerda cómico, 0.2–0.7 s |
| `spottedLoop` | Sigues a la vista | Variante más rápida del latido; sustituye el bucle anterior |

No revela al buscador dónde estás. «A la vista» es una comprobación geométrica, no que el humano te haya reconocido. Estos avisos deben dejar oír pasos y silbidos.

## Acciones y combate — conectados

| Slot | Alcance | Propuesta |
|---|---|---|
| `automaticWhistle` | 3D | Silbido corto y reconocible, 0.5–1.5 s; sin puntos |
| `manualWhistle` | 3D | Silbido descarado/distinto; mantiene recarga de 5 s |
| `disguise` | 3D | Poof o pop elástico de transformación, 0.2–0.6 s |
| `rotate` | 3D | Tick de juguete muy discreto, 0.05–0.15 s |
| `freeze` / `unfreeze` | 3D | Clic de bloqueo y variante inversa, 0.1–0.3 s |
| `jump` | Local desde el jugador | Resorte o esfuerzo leve; opcional para no dar ruido a escondedores |
| `footsteps` | 3D, solo buscadores | 4–8 muestras neutras, 0.1–0.35 s, elegidas al azar |
| `throwSound` | 3D | Whoosh de lanzamiento, 0.1–0.4 s |
| `impactSound` | 3D, en el impacto | Huevo: splat; piedra: clac; puede sustituirse por arma |
| `hurt` | 3D | Golpe cartoon, 0.1–0.4 s |
| `captured` | 3D | Estallido de confeti/revelación, 0.4–1 s |
| `hitConfirmation` | Local del tirador | Tick limpio de acierto, 0.05–0.2 s |
| `captureConfirmation` | Local del tirador | Confirmación más satisfactoria, 0.2–0.5 s |
| `scoreTick` | Local | Moneda/estrella suave al aumentar puntos, 0.05–0.2 s |

Los sonidos 3D nacen en el jugador/proyectil y se atenúan con la distancia. El salto conserva su ejecución local, sin nuevo RPC. Una captura puede disparar impacto, daño y revelación: usa clips complementarios o deja algunos vacíos.

## Interfaz y webcam — conectados, 2D

| Slot | Evento | Propuesta |
|---|---|---|
| `uiClick` | Botones de menú/lobby | Pop o toque de madera, 0.05–0.15 s |
| `uiBack` | Botón Volver en ajustes/conexión/webcam | Toque descendente, 0.05–0.2 s |
| `uiConfirm` | Aplicar ajustes | Doble pop/acorde, 0.15–0.4 s |
| `uiError` | Error de conexión/transporte | Alerta suave, 0.15–0.4 s |
| `connected` / `disconnected` | Tu entrada/salida de la sala | Campanilla/despedida, 0.2–0.6 s |
| `ready` / `unready` | Marcar/cancelar preparado | Afirmación y variante descendente, 0.1–0.3 s |
| `cameraOn` | La webcam entrega imagen válida | Encendido discreto, 0.1–0.4 s |
| `photoCaptured` | Foto capturada y preparada | Obturador simpático, 0.1–0.4 s |

El clic genérico puede acompañar a una confirmación específica. No suena por cada paso de un slider. Esta conexión de clics cubre menú/lobby, no es un sonido universal en todos los botones del proyecto.

## Nuevos avisos conectados

| Slots | Evento | Funcionamiento |
|---|---|---|
| `countdown3`, `countdown2`, `countdown1` | Últimos tres segundos del escondite y de la caza | Tres clips cortos programados con `PlayScheduled`, a partir del plazo y reloj de servidor. No usa la llegada de un RPC por segundo. |
| `mapSelectWhoosh` | El host elige otro mapa | Lo oye el host al seleccionar, no al refrescar el dropdown. |
| `mapSelectConfirm` | El servidor acepta el cambio de mapa | Confirmación para la sala, una vez por cambio real. Volver a seleccionar el mismo índice no desmarca preparados ni dispara audio. |
| `otherPlayerJoined`, `otherPlayerLeft` | Entra/sale otra persona | Solo para los demás jugadores. Sin alertas por la lista inicial ni por cambios de preparado. Tu propia conexión sigue usando los slots anteriores. |
| `lastHider` | El servidor detecta que queda un escondedor | Una vez por ronda, 2D y sin identidad/coordenadas. No suena si la ronda ya empezó con un único escondedor. También cuenta una desconexión que reduzca el equipo a uno. |
| `shotMiss` | Proyectil que golpea mundo/buscador o agota su vida sin acertar | Sonido 3D en el punto de choque o última posición. No se dispara al limpiar proyectiles por final de ronda. |

Para la cuenta atrás, usa tres tonos de 0.1–0.3 s, progresivos y sin silencios iniciales. Si una fase dura menos de tres segundos o un cliente llega tarde, solo se programan los tonos que aún estén por llegar; nunca se acumulan todos en un frame. Un cambio/cancelación de fase detiene los tonos pendientes.

`ThrowableDefinition.missSound` permite sustituir el fallo por arma. Si está vacío, usa `shotMiss`; si ambos están vacíos, un choque conserva el impacto genérico existente. El fallo reemplaza al impacto sonoro, no añade dos golpes superpuestos. Agotar la vida sin clip de fallo se queda en silencio; no crea una explosión artificial en el aire.

## Puertas e interacción — slots y componente preparados

| Slot central | Método de `DoorAudio` | Propuesta |
|---|---|---|
| `doorOpen` | `Opened()` | Bisagra suave/cartoon, 0.3–0.8 s |
| `doorClose` | `Closed()` | Tope de madera/metal, 0.2–0.5 s |
| `doorHandle` | `HandleUsed()` | Clic de manilla, 0.1–0.25 s |
| `doorKnock` | `Knocked()` | Dos golpes reconocibles, 0.2–0.6 s |

Añade **`DoorAudio`** al objeto de puerta que vaya a controlar la interacción. Requiere `NetworkObject` y `AudioSource` (se añaden automáticamente); configura distancia/rolloff del AudioSource y registra el prefab si la puerta se crea dinámicamente. Para puertas colocadas en escena se usa el flujo normal de objetos de escena de Netcode.

El controlador definitivo debe validar la interacción **en servidor** y llamar al método correspondiente cuando la acción suceda. `DoorAudio` envía el sonido 3D a los clientes y admite clips por puerta con prioridad sobre los centrales. Ignora llamadas sin spawn, llamadas de cliente y duplicados del mismo evento en menos de 0.1 s.

No se añade una mecánica de abrir puertas ni se conectan estos métodos a puertas físicas de prueba sin controlador: hacerlo ahora dispararía sonidos en momentos incorrectos o inventaría interacciones. Tampoco expone un RPC de interacción sin validar distancias/permisos. Los cuatro métodos pueden conectarse a UnityEvents del futuro controlador autoritativo.

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
- **Para empezar elegiría**: música de menú/caza, inicio/fin de ronda, los dos silbidos, lanzamiento/impacto, pasos, cercanía y captura. Todo lo demás puede quedarse vacío.
