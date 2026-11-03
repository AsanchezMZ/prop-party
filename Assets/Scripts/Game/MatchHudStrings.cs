// Solo textos que MatchHUD cambia durante la partida según rol, fase, acciones o valores.
// Los rótulos fijos, botones y textos del blackout se editan directamente en MatchHUD.uxml.
// En los formatos conserva los {0}, {1}, etc. Puedes cambiar el resto y el formato numérico tras ':'.
public static class MatchHudStrings
{
    // Cuenta atrás del blackout: su contenido varía cada segundo.
    public const string CountdownFormat = "{0:00}"; // {0}: segundos hasta la salida.

    // Rol y objetivo del jugador local.
    public const string PropRole = "ESCONDEDOR";
    public const string HunterRole = "CAZADOR";
    public const string SpectatorRole = "ESPECTADOR";
    public const string FrozenObjective = "Congelado. Ni un movimiento.";
    public const string PropObjective = "Busca un sitio. Escondete. Baitealos.";
    public const string HunterObjective = "Busca a los escondedores y capturalos.";
    public const string SpectatorObjective = "Sigue el espectáculo desde fuera.";

    // Fase, avisos grandes y reloj.
    public const string WaitingPhase = "REUNIENDO AL EQUIPO";
    public const string HidingPhase = "BUSCA TU SITIO";
    public const string FinishedPhase = "FIN DE PARTIDA";
    public const string HuntingPhase = "LA CAZA HA COMENZADO";
    public const string HidingBanner = "¡A ESCONDERSE!";
    public const string FinishedBanner = "¡RONDA TERMINADA!";
    public const string WaitingBanner = "PREPARANDO LA FIESTA";
    public const string HuntingBanner = "¡EMPIEZA LA CAZA!";
    public const string WaitingTimer = "--:--";
    public const string TimerFormat = "{0:00}:{1:00}"; // {0}: minutos; {1}: segundos.

    // Vida, margen de error, puntuación y presencia.
    public const string UnlimitedHealth = "∞ ♥";
    public const string HealthFormat = "{0} ♥"; // {0}: vidas o fallos restantes; sin máximo ni caption.
    public const string ScoreFormat = "{0:0000}"; // {0}: puntos del jugador.
    public const string SeenRisk = "¡A LA VISTA!";
    public const string NearbyRisk = "¡Alguien anda cerca!";
    public const string CalmRisk = "Desapercibido";
    public const string WhistleCooldownFormat = "OTRO EN {0:0.0} s"; // {0}: segundos de recarga.
    public const string WhistleReady = "SILBAR  /  G";

    // Ayuda e interacción. Solo cambia el texto: esto NO cambia las teclas reales.
    public const string PropHintFormat = "E interactuar/camuflar · Q/R girar · Clic derecho + ratón giro fino · F suspender{0} · V {1} · G silbar · Esc menú";
    // PropHintFormat: {0} = marca de suspensión; {1} = nombre de la perspectiva.
    public const string FrozenHint = " ❄";
    public const string FirstPerson = "FPV";
    public const string ThirdPerson = "TPV";
    public const string HunterHint = "Clic lanzar · E puertas · Esc menú · Tab continuar";
    public const string SpectatorHint = "→ cambiar jugador observado · Esc menú · Tab cerrar";
    public const string CloseDoor = "[E]  Cerrar puerta";
    public const string OpenDoor = "[E]  Abrir puerta";
    public const string PropInteractionFormat = "[E]  {0}"; // {0}: nombre del prop.

    // Nombres: el nombre real sigue siendo el elegido por cada jugador.
    public const string ArrowRight = "→ ";
    public const string ArrowLeft = "← ";
    public const string ArrowUp = "↑ ";
    public const string ArrowDown = "↓ ";
    public const string OccludedArrow = "▼ ";
    public const string NameTagFormat = "{0}{1}"; // {0}: flecha; {1}: nombre.
    public const string IdentifiedNameFormat = "{0}"; // {0}: nombre al mirar directamente.
    public const string RankingRowFormat = "{0:00}  /  {1}     {2} pts"; // {0}: puesto; {1}: nombre; {2}: puntos.

    // Panel de pausa y resultados.
    public const string SpectatorResult = "¡RONDA TERMINADA!";
    public const string VictoryResult = "¡VICTORIA!";
    public const string DefeatResult = "¡DERROTA!";
    public const string WaitingResult = "ESPERANDO AL EQUIPO";
    public const string PauseResult = "UN RESPIRO...";
    public const string PropsWinResult = "GANAN LOS ESCONDEDORES";
    public const string HuntersWinResult = "GANAN LOS BUSCADORES";

    // Toasts: cambios de fase, avisos, acciones y daño.
    public const string CannotStartToast = "👥 Faltan jugadores o puntos de aparición.";
    public const string WaitingToast = "Se está reuniendo el reparto…";
    public const string HidingToast = "¡Busca tu sitio y no te delates!";
    public const string PropsWinToast = "Fin de partida. Ganan los escondedores.";
    public const string HuntersWinToast = "Fin de partida. Ganan los buscadores.";
    public const string HuntingToast = "¡Ve a por ellos!";
    public const string ScoreToastFormat = "+{0} puntos"; // {0}: puntos ganados desde el último aviso.
    public const string NearbyToast = "⚠ Un buscador está cerca.";
    public const string SeenToast = " ⚠ ¡Estás en su linea de visión! (¡Puntos extras!).";
    public const string HitToast = "¡Te han dado!";
    public const string CapturedToast = "Se acabó el camuflaje. ¡Te han encontrado!";
    public const string ManualWhistleToast = "Acabas de dar una pista.";
    public const string AutomaticWhistleToast = "Se ha activado un silbido automático.";
    public const string DisguiseToast = "Camuflaje listo.";
    public const string NoLivesToast = "Pasas a espectador.";
    public const string WrongPropToast = "❌ ¡Eso era un prop, no un jugador!";

}
