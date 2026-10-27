/// <summary>
/// Interruptor unico para los textos de prueba (racha, miedo, miradas atras, salida).
/// Arranca apagado: el jugador no los ve. F1 los muestra u oculta (lo lee GameFlow).
/// Cada script con OnGUI de prueba pregunta "DebugOverlay.Visible" antes de dibujar.
///
/// Es una clase "static": no va en ningun objeto y existe una sola vez en todo el juego.
/// Sobrevive a recargar la escena, asi que si lo prendes, sigue prendido al volver al titulo.
/// </summary>
public static class DebugOverlay
{
    public static bool Visible { get; set; }
}
