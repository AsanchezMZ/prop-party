# 🎭 PropParty

**¡Escóndete a plena vista o descubre qué objeto está fuera de lugar!**

PropParty es un juego multijugador de escondite tipo **Prop Hunt**, creado con Unity para jugar entre amigos. Un equipo se transforma en objetos del escenario y el otro intenta encontrarlo… ¡lanzando huevos! 🥚

## 🎮 ¿Cómo se juega?

- 🪑 **Escondedores:** copia un objeto del mapa, busca un buen sitio y pasa desapercibido hasta que termine el tiempo.
- 🔎 **Buscadores:** espera a que los demás se escondan y después encuentra y captura a todos los escondedores.
- ⏱️ **Victoria:** los buscadores ganan si capturan a todos; los escondedores, si se acaba el tiempo o no quedan buscadores.
- 🔄 Según las reglas de la sala, los capturados se convierten en buscadores o pasan a espectador.

## ✨ Características

- 🌐 **Salas por IP:** crea una partida o únete a la de un amigo.
- 🗺️ **Cuatro mapas:** Apartamento, City, Town y HauntedHouse, con opción de selección aleatoria.
- 🎭 **Camuflaje:** conviértete en objetos, gira el disfraz y déjalo suspendido para encajar en el entorno.
- 🥚 **Caza a huevazos:** los buscadores lanzan huevos; acertar a objetos normales puede consumir su margen de error.
- 🎵 **Silbidos:** pistas sonoras automáticas y silbidos manuales para tentar a los buscadores.
- 🏆 **Puntuación y clasificación:** gana puntos por capturas, silbidos manuales y por arriesgarte cerca de los buscadores.
- 📸 **Tu cara en el personaje:** hazte una foto con la webcam. Se comparte la imagen, nunca vídeo en directo.
- ⚙️ **Reglas a tu gusto:** ajusta buscadores, tiempos, silbidos, vidas y destino de los capturados.
- 🎥 **Dos perspectivas:** los escondedores pueden alternar entre primera y tercera persona; los buscadores usan primera persona.

## 🚀 Jugar con amigos

1. Abre el juego y elige tu nombre.
2. Un jugador crea la sala y comparte su IP.
3. Los demás se unen a esa IP. La conexión usa el **puerto UDP 7777**.
4. Todos pulsan **Estoy listo** y el anfitrión elige el mapa e inicia la partida.

> 💡 Para jugar por Internet, el puerto del anfitrión debe ser accesible. No hay búsqueda automática de partidas ni Relay. No se puede entrar en una ronda ya empezada y, si el anfitrión se desconecta, la sesión no se transfiere a otro jugador.

## ⌨️ Controles básicos

| Acción | Control |
| --- | --- |
| Moverse | WASD |
| Correr / andar despacio | Shift / Ctrl |
| Saltar | Espacio |
| Copiar objeto / abrir o cerrar puertas del apartamento | E |
| Girar disfraz | Q / R |
| Girar disfraz con precisión | Clic derecho + movimiento horizontal del ratón |
| Suspender o soltar el disfraz | F |
| Cambiar de cámara como escondedor | V |
| Silbar durante la caza | G |
| Lanzar huevo como buscador | Clic izquierdo |
| Abrir o cerrar el menú | Esc / Tab |
| Ver al siguiente jugador como espectador | Flecha derecha |

## 🛠️ Abrir el proyecto

Usa **Unity 6000.6.4f1**, abre `Assets/Scenes/ui/MainMenu.unity` y pulsa **Play**. Para probar una partida necesitas al menos dos jugadores; puedes usar dos instancias con Multiplayer Play Mode.

## 📚 Más información

- [Reglas, controles y detalles del juego](Docs/Game.md)
- [Creación de mapas y objetos](Docs/PropHunt.md)
- [Configuración de audio](Docs/Audio.md)
