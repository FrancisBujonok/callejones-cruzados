using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>
/// Parte de AtmosphereBuilder: los menus del Paso 17, armados a partir de los mockups elegidos.
///   - Inicio C, "tapa de libro": columna oscura con 迷津, el titulo y el menu; a la derecha una
///     ventana enmarcada donde se ve el laberinto de verdad (MenuCamera + TitleWalker).
///   - Pausa A, "afiche de desaparecido" (失蹤): la foto es lo que se estaba viendo al pausar.
///   - Final B, "boleto de vuelta" (歸途): pasos, cambios descubiertos y tiempo.
///   - Opciones: volumen, brillo (con un cartel para calibrar) y sensibilidad del mouse.
///
/// Todo se mide en "pixeles de la interfaz": el Canvas usa una resolucion de referencia de
/// 640 x 360 (CanvasScaler), asi que (0, 0) es la esquina de arriba a la izquierda y (640, 360)
/// la de abajo a la derecha, en cualquier pantalla. Los mockups median 960 x 540: aca todo
/// esta multiplicado por 2/3, con algunas letras un poco mas grandes para que se lean a 240 lineas.
///
/// Letras: DotGothic16 (Google Fonts, licencia OFL), una fuente japonesa pixelada que tambien
/// tiene acentos, la eñe y flechas. Las palabras en chino son imagenes chicas (UI_*.png) que
/// genera Herramientas/generar_ui.py: asi no hace falta una fuente con miles de caracteres.
/// </summary>
public static partial class AtmosphereBuilder
{
    private const string UiDir = Root + "/Textures/UI";
    private const string FontsDir = Root + "/Fonts";
    private const string UiFontFile = FontsDir + "/DotGothic16-Regular.ttf";
    private const string UiFontAssetPath = FontsDir + "/DotGothic16 SDF.asset";
    private const string CalibrationSign = Root + "/Textures/Carteles/V5_tw_06_papel.png";

    // Los caracteres que usan los menus (la fuente agrega solos los que falten, pero asi ya estan).
    private const string UiChars =
        "ABCDEFGHIJKLMNÑOPQRSTUVWXYZÁÉÍÓÚÜabcdefghijklmnñopqrstuvwxyzáéíóúü0123456789" +
        " .,:;¡!¿?()[]-–—·…«»\"'/%+º°↑↓←→";

    private const int UiLayer = 5; // capa "UI" de Unity

    private static readonly Color UiInk = Hex("#1D0C07");
    private static readonly Color UiInkSoft = Hex("#3A1A0E");
    private static readonly Color UiBrown = Hex("#5A2A12");
    private static readonly Color UiRed = Hex("#B3261E");
    private static readonly Color UiNight = Hex("#0E0604");

    private static TMP_FontAsset uiFont;

    private class UiSprites
    {
        public Sprite farol, mijin, shizong, guitu, guituV, sheding;
        public Sprite segFull, segEmpty, dot, dashH, dashV, circle, vignette, barcode;
    }

    // ================================================================== armado

    private static GameUI BuildMenus(Camera cam)
    {
        uiFont = EnsureUiFont();
        UiSprites s = LoadUiSprites();

        var root = new GameObject("Interfaz", typeof(RectTransform));
        root.layer = UiLayer;
        Undo.RegisterCreatedObjectUndo(root, "Crear Interfaz");

        // Canvas en modo "Screen Space - Camera": se dibuja DENTRO de la camara, asi que la interfaz
        // tambien sale a 240 lineas (pixelada como el juego) y le llegan el grano y la vineta.
        // La dibuja "CamaraInterfaz", una camara Overlay apilada sobre la Main Camera: sale a la
        // misma resolucion baja, pero DESPUES del post-procesado. Si no, el resplandor (bloom) del
        // papel se corre sobre las letras finas y las vuelve grises e ilegibles.
        Camera uiCam = SetupUiCamera(cam);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = uiCam;
        canvas.planeDistance = uiCam.nearClipPlane + 0.02f;

        // "Expand": el area de 640 x 360 siempre entra entera. En una pantalla mas ancha (o mas alta)
        // sobra espacio a los costados (o arriba y abajo), y ahi solo llegan los fondos.
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(640f, 360f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        root.AddComponent<GraphicRaycaster>();

        // Orden = orden de dibujo: lo de abajo en la jerarquia tapa a lo de arriba.
        // El fundido negro va arriba del inicio, la pausa y las opciones; el final, arriba del fundido.
        CanvasGroup title = UiPanel(root.transform, "Inicio");
        var titleRefs = BuildTitle(title.transform, s);

        CanvasGroup pause = UiPanel(root.transform, "Pausa");
        var pauseRefs = BuildPause(pause.transform, s);

        CanvasGroup options = UiPanel(root.transform, "Opciones");
        var optionRefs = BuildOptionsPanel(options.transform, s);

        CanvasGroup fade = UiPanel(root.transform, "Fundido");
        fade.blocksRaycasts = false;
        fade.interactable = false;
        UiFill(fade.transform, "Negro", Color.black);

        CanvasGroup ending = UiPanel(root.transform, "Final");
        var endRefs = BuildEnding(ending.transform, s);

        var ui = root.AddComponent<GameUI>();
        SetRefs(ui,
            ("titlePanel", title), ("pausePanel", pause), ("optionsPanel", options),
            ("endingPanel", ending), ("fade", fade),
            ("playButton", titleRefs.play), ("titleOptionsButton", titleRefs.options), ("titleQuitButton", titleRefs.quit),
            ("resumeButton", pauseRefs.resume), ("pauseOptionsButton", pauseRefs.options),
            ("backToTitleButton", pauseRefs.title), ("pauseQuitButton", pauseRefs.quit), ("pauseSeen", pauseRefs.seen),
            ("volumeSlider", optionRefs.volume), ("brightnessSlider", optionRefs.brightness),
            ("sensitivitySlider", optionRefs.sensitivity), ("optionsBackButton", optionRefs.back), ("calibrationSign", optionRefs.sign),
            ("ticket", endRefs.ticket), ("stepsValue", endRefs.steps), ("changesValue", endRefs.changes),
            ("timeValue", endRefs.time), ("ticketNumber", endRefs.number), ("endingButtons", endRefs.buttons),
            ("restartButton", endRefs.restart), ("endingTitleButton", endRefs.title));

        // Fuera de Play solo se ve el inicio; los otros paneles quedan con alpha 0 para no
        // encimarse. Para editar uno, subile el Alpha de su Canvas Group a 1 (y volvelo a 0).
        pause.alpha = 0f;
        options.alpha = 0f;
        fade.alpha = 0f;
        ending.alpha = 0f;
        endRefs.buttons.alpha = 0f;
        return ui;
    }

    // ------------------------------------------------------------------ inicio (mockup C)

    private struct TitleRefs { public Button play, options, quit; }

    private static TitleRefs BuildTitle(Transform panel, UiSprites s)
    {
        UiFill(panel, "Fondo", UiNight);
        Transform c = UiContent(panel); // el contenido va en el area de 640 x 360

        // Columna izquierda: 迷津 en vertical, el nombre y una raya naranja.
        UiImage(c, "MiJin", s.mijin, UiPaper, 35f, 32f, 43f, 88f);
        UiText(c, "Nombre", "CALLEJONES\nCRUZADOS", 92f, 33f, 200f, 70f, 25f, UiPaper, TextAlignmentOptions.TopLeft, 12f);
        UiBox(c, "Raya", UiAccent, 92f, 104f, 32f, 2f);

        // Menu abajo a la izquierda: el farolito marca la opcion elegida.
        var refs = new TitleRefs
        {
            play = UiMenuButton(c, "Entrar", "ENTRAR", 54f, 236f, s.farol, UiPaper, UiAccent),
            options = UiMenuButton(c, "Opciones", "OPCIONES", 54f, 268f, s.farol, UiPaper, UiAccent),
            quit = UiMenuButton(c, "Salir", "SALIR", 54f, 300f, s.farol, UiPaper, UiAccent),
        };
        UiChain(refs.play, refs.options, refs.quit);

        // La ventana: contorno oscuro, un espacio, el borde de papel y adentro el laberinto.
        UiBox(c, "Contorno", UiInkSoft, 266.7f, 21.7f, 350.6f, 317.6f);
        UiBox(c, "Espacio", UiNight, 268f, 23f, 348f, 315f);
        UiBox(c, "Borde", UiPaper, 272f, 27f, 340f, 307f);
        RectTransform window = UiRect(c, "Ventana", 276f, 31f, 332f, 299f);
        var view = window.gameObject.AddComponent<RawImage>();
        view.color = Color.white;
        view.raycastTarget = false;
        Image vignette = UiFill(window, "Vineta", new Color(UiNight.r, UiNight.g, UiNight.b, 0.85f));
        vignette.sprite = s.vignette;
        Image cut = UiFill(window, "Corte", Color.black);
        cut.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
        return refs;
    }

    // ------------------------------------------------------------------ pausa (mockup A)

    private struct PauseRefs { public Button resume, options, title, quit; public TMP_Text seen; }

    private static PauseRefs BuildPause(Transform panel, UiSprites s)
    {
        // El juego queda congelado detras, oscurecido.
        UiFill(panel, "Velo", new Color(0.04f, 0.02f, 0.015f, 0.86f));
        Transform c = UiContent(panel); // el contenido va en el area de 640 x 360

        // El afiche, apenas torcido, con su sombra y dos cintas.
        RectTransform poster = UiRect(c, "Afiche", 358f, 16f, 240f, 328f);
        poster.localRotation = Quaternion.Euler(0f, 0f, 1.5f);
        UiBox(poster, "Sombra", new Color(0f, 0f, 0f, 0.45f), 6f, 8f, 240f, 328f);
        UiBox(poster, "Contorno", UiDim, -1.3f, -1.3f, 242.6f, 330.6f);
        UiBox(poster, "Papel", UiPaper, 0f, 0f, 240f, 328f);
        var tape = new Color(0.78f, 0.75f, 0.63f, 0.8f);
        UiBox(poster, "CintaIzq", tape, -12f, -8f, 48f, 15f).rectTransform.localRotation = Quaternion.Euler(0f, 0f, 24f);
        UiBox(poster, "CintaDer", tape, 204f, -8f, 48f, 15f).rectTransform.localRotation = Quaternion.Euler(0f, 0f, -22f);

        // Encabezado: 失蹤 ("desaparecido"), PAUSA y cuanto hace que se te vio.
        UiImage(poster, "ShiZong", s.shizong, UiRed, 16f, 14f, 62f, 30f);
        UiText(poster, "Titulo", "PAUSA", 96f, 12f, 128f, 22f, 20f, UiInk, TextAlignmentOptions.TopRight, 16f);
        TMP_Text seen = UiText(poster, "Visto", "visto por última vez hace 0:00", 16f, 48f, 208f, 14f, 12f, UiBrown, TextAlignmentOptions.TopRight);
        UiBox(poster, "Regla", UiInk, 16f, 64f, 208f, 2f);

        // La foto: la llena MenuCamera al pausar.
        UiBox(poster, "MarcoFoto", UiInk, 16f, 72f, 208f, 74f);
        RectTransform photoRect = UiRect(poster, "Foto", 17.3f, 73.3f, 205.4f, 71.4f);
        var photo = photoRect.gameObject.AddComponent<RawImage>();
        photo.color = Color.white;
        photo.raycastTarget = false;

        // Las opciones, con un cuadradito rojo en la elegida.
        var refs = new PauseRefs
        {
            resume = UiPosterButton(poster, "Seguir", "Seguir", 34f, 156f),
            options = UiPosterButton(poster, "Opciones", "Opciones", 34f, 182f),
            title = UiPosterButton(poster, "VolverAlInicio", "Volver al inicio", 34f, 208f),
            quit = UiPosterButton(poster, "SalirDelJuego", "Salir del juego", 34f, 234f),
            seen = seen,
        };
        UiChain(refs.resume, refs.options, refs.title, refs.quit);

        // Las tiras para arrancar, con 歸途 ("el camino de vuelta"). Faltan dos: alguien ya se las llevo.
        const float cellW = 240f / 7f;
        UiTiled(poster, "Corte", s.dashH, UiDim, 0f, 285f, 240f, 2f);
        for (int i = 0; i < 7; i++)
        {
            float x = i * cellW;
            if (i > 0) UiTiled(poster, "Separador" + i, s.dashV, UiDim, x - 1f, 287f, 2f, 41f);
            if (i == 2 || i == 5) UiBox(poster, "Arrancada" + i, UiNight, x + 1f, 287f, cellW - 2f, 41f);
            else UiImage(poster, "Tira" + i, s.guituV, UiInk, x + (cellW - 12f) * 0.5f, 293f, 12f, 25f);
        }

        UiText(c, "Ayuda", "Esc seguir · ↑↓ elegir · Enter confirmar", 19f, 336f, 330f, 14f, 12f, UiDim, TextAlignmentOptions.TopLeft);
        return refs;
    }

    // ------------------------------------------------------------------ opciones

    private struct OptionRefs { public Slider volume, brightness, sensitivity; public Button back; public RawImage sign; }

    private static OptionRefs BuildOptionsPanel(Transform panel, UiSprites s)
    {
        // Detras queda el juego (o la caminata del inicio), casi a oscuras.
        UiFill(panel, "Velo", new Color(UiNight.r, UiNight.g, UiNight.b, 0.92f));
        Transform c = UiContent(panel); // el contenido va en el area de 640 x 360

        RectTransform sheet = UiRect(c, "Hoja", 107f, 32f, 427f, 296f);
        UiBox(sheet, "Contorno", UiDim, -4f, -4f, 435f, 304f);
        UiBox(sheet, "Borde", UiInk, -2.7f, -2.7f, 432.4f, 301.4f);
        UiBox(sheet, "Papel", UiPaper, 0f, 0f, 427f, 296f);

        UiText(sheet, "Titulo", "OPCIONES", 19f, 14f, 120f, 24f, 19f, UiInk, TextAlignmentOptions.TopLeft, 20f);
        UiImage(sheet, "SheDing", s.sheding, UiRed, 140f, 16f, 42f, 20f);
        UiBox(sheet, "Regla", UiInk, 19f, 42f, 389f, 2f);

        var refs = new OptionRefs
        {
            volume = UiOptionRow(sheet, "Volumen", "Volumen general", 58f, 0, s),
            brightness = UiOptionRow(sheet, "Brillo", "Brillo", 92f, 0, s),
            sensitivity = UiOptionRow(sheet, "Sensibilidad", "Sensibilidad del mouse", 126f, 1, s),
        };

        // Cartel para calibrar el brillo: muy oscuro a proposito. Con el brillo bien puesto,
        // apenas se tiene que leer (請勿回頭, "por favor, no mires atras").
        UiBox(sheet, "Calibracion", UiNight, 316f, 56f, 92f, 132f);
        RectTransform signRect = UiRect(sheet, "Cartel", 350f, 65.5f, 24f, 113f);
        var sign = signRect.gameObject.AddComponent<RawImage>();
        sign.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(CalibrationSign);
        sign.color = new Color(0.1f, 0.1f, 0.1f, 1f); // GameUI lo aclara u oscurece con el brillo
        refs.sign = sign;
        sign.raycastTarget = false;
        TMP_Text hint = UiText(sheet, "Explicacion", "Subí el brillo hasta que el cartel apenas se lea.",
            300f, 194f, 124f, 48f, 12f, UiInkSoft, TextAlignmentOptions.Top);
        hint.textWrappingMode = TextWrappingModes.Normal;

        // Pie: linea cortada y el boton Volver.
        UiTiled(sheet, "Corte", s.dashH, UiDim, 19f, 254f, 389f, 2f);
        RectTransform backRect = UiRect(sheet, "Volver", 19f, 263f, 84f, 24f);
        var backBox = backRect.gameObject.AddComponent<Image>();
        backBox.color = Color.white;
        UiText(backRect, "Texto", "VOLVER", 0f, 5f, 84f, 16f, 14f, UiPaper, TextAlignmentOptions.Top, 14f);
        refs.back = backRect.gameObject.AddComponent<Button>();
        refs.back.targetGraphic = backBox;
        refs.back.colors = UiColors(UiInk, UiRed);
        backRect.gameObject.AddComponent<SelectOnHover>();
        UiText(sheet, "Ayuda", "↑↓ elegir · ← → cambiar", 200f, 268f, 208f, 14f, 12f, UiBrown, TextAlignmentOptions.TopRight);

        UiChain(refs.volume, refs.brightness, refs.sensitivity, refs.back);
        return refs;
    }

    /// <summary>Una fila de opciones: raya roja (si esta elegida), nombre y barra de 10 casilleros.</summary>
    private static Slider UiOptionRow(Transform sheet, string name, string label, float y, int min, UiSprites s)
    {
        Image bar = UiBox(sheet, name + "Marca", UiRed, 13f, y, 3f, 26f);
        TMP_Text text = UiText(sheet, name + "Nombre", label, 22f, y + 5f, 170f, 16f, 14f, Color.white, TextAlignmentOptions.TopLeft);

        RectTransform rt = UiRect(sheet, name, 200f, y + 7f, 90f, 12f);
        Image empty = UiFill(rt, "Vacios", UiDim);
        empty.sprite = s.segEmpty;
        empty.type = Image.Type.Tiled;
        empty.raycastTarget = true; // para poder arrastrar la barra con el mouse

        var fillArea = new GameObject("Relleno", typeof(RectTransform));
        fillArea.layer = UiLayer;
        fillArea.transform.SetParent(rt, false);
        Stretch((RectTransform)fillArea.transform);
        Image full = UiFill(fillArea.transform, "Llenos", UiInk);
        full.sprite = s.segFull;
        full.type = Image.Type.Tiled;

        var slider = rt.gameObject.AddComponent<Slider>();
        slider.fillRect = full.rectTransform;
        slider.handleRect = null;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = GameSettings.Max;
        slider.wholeNumbers = true;
        slider.value = 5;
        // El nombre de la fila cambia de color cuando la barra esta elegida.
        slider.targetGraphic = text;
        slider.colors = UiColors(UiInkSoft, UiRed);
        rt.gameObject.AddComponent<SelectOnHover>();
        rt.gameObject.AddComponent<MenuItemMarker>().Setup(bar);
        return slider;
    }

    // ------------------------------------------------------------------ final (mockup B)

    private struct EndRefs
    {
        public RectTransform ticket;
        public TMP_Text steps, changes, time, number;
        public CanvasGroup buttons;
        public Button restart, title;
    }

    private static EndRefs BuildEnding(Transform panel, UiSprites s)
    {
        var refs = new EndRefs();
        Transform c = UiContent(panel); // el contenido va en el area de 640 x 360

        RectTransform ticket = UiRect(c, "Boleto", 186f, 16f, 230f, 328f);
        ticket.localRotation = Quaternion.Euler(0f, 0f, -2f);
        refs.ticket = ticket;
        UiBox(ticket, "Sombra", new Color(0f, 0f, 0f, 0.5f), 7f, 9f, 230f, 328f);
        UiBox(ticket, "Papel", UiPaper, 0f, 0f, 230f, 328f);

        // Banda roja: 歸途 ("el camino de vuelta") y BOLETO DE VUELTA.
        UiBox(ticket, "Banda", UiRed, 0f, 0f, 230f, 44f);
        UiImage(ticket, "GuiTu", s.guitu, UiPaper, 14f, 10f, 50f, 24f);
        TMP_Text kind = UiText(ticket, "Tipo", "BOLETO\nDE VUELTA", 100f, 8f, 116f, 30f, 11f, UiPaper, TextAlignmentOptions.TopRight, 12f);
        kind.lineSpacing = 10f;

        UiText(ticket, "Saliste", "Saliste.", 15f, 56f, 200f, 32f, 27f, UiInk, TextAlignmentOptions.TopLeft);
        refs.steps = UiTicketRow(ticket, "Pasos", "Pasos", 100f, s);
        refs.changes = UiTicketRow(ticket, "Cambios", "Cambios descubiertos", 122f, s);
        refs.time = UiTicketRow(ticket, "Tiempo", "Tiempo", 144f, s);

        // Linea para cortar, con una muesca a cada lado.
        UiTiled(ticket, "Corte", s.dashH, UiDim, 0f, 251f, 230f, 2f);
        var notch = new Color(0.02f, 0.01f, 0.008f, 1f);
        UiImage(ticket, "MuescaIzq", s.circle, notch, -9f, 243f, 18f, 18f);
        UiImage(ticket, "MuescaDer", s.circle, notch, 221f, 243f, 18f, 18f);

        refs.number = UiText(ticket, "Numero", "N.º 000001", 15f, 260f, 110f, 14f, 12f, UiBrown, TextAlignmentOptions.TopLeft);
        UiText(ticket, "Validez", "Un solo viaje", 105f, 260f, 110f, 14f, 12f, UiBrown, TextAlignmentOptions.TopRight);
        UiImage(ticket, "CodigoBarras", s.barcode, UiInk, 16f, 282f, 198f, 34f);

        // Botones a la derecha del boleto: aparecen un rato despues.
        RectTransform group = UiRect(c, "Botones", 442f, 140f, 198f, 60f);
        refs.buttons = group.gameObject.AddComponent<CanvasGroup>();
        refs.restart = UiMenuButton(group, "VolverAEntrar", "VOLVER A ENTRAR", 19f, 0f, s.farol, UiPaper, UiAccent, 15f);
        refs.title = UiMenuButton(group, "VolverAlInicio", "VOLVER AL INICIO", 19f, 30f, s.farol, UiPaper, UiAccent, 15f);
        UiChain(refs.restart, refs.title);
        return refs;
    }

    /// <summary>Un renglon del boleto: dato ..... valor. Los puntitos pasan por detras de los textos.</summary>
    private static TMP_Text UiTicketRow(Transform ticket, string name, string label, float y, UiSprites s)
    {
        UiTiled(ticket, name + "Puntos", s.dot, UiDim, 15f, y + 10f, 200f, 2f);

        TMP_Text text = UiText(ticket, name + "Dato", label, 15f, y, 160f, 16f, 12f, UiInk, TextAlignmentOptions.TopLeft);
        float width = text.GetPreferredValues(label, 300f, 20f).x + 4f;
        Image back = UiBox(ticket, name + "FondoDato", UiPaper, 15f, y - 1f, width, 16f);
        back.transform.SetSiblingIndex(text.transform.GetSiblingIndex()); // detras del texto

        UiBox(ticket, name + "FondoValor", UiPaper, 167f, y - 1f, 48f, 16f); // se crea antes: queda detras del valor
        TMP_Text value = UiText(ticket, name + "Valor", "0", 165f, y, 50f, 16f, 12f, UiInk, TextAlignmentOptions.TopRight);
        return value;
    }

    // ================================================================== camara y caminata

    /// <summary>"CamaraMenus": hija de la Main Camera, apagada, con los mismos ajustes pero sin la interfaz.</summary>
    private static MenuCamera SetupMenuCamera(Camera main, GameUI ui)
    {
        Transform existing = main.transform.Find("CamaraMenus");
        GameObject go;
        if (existing != null) go = existing.gameObject;
        else
        {
            go = new GameObject("CamaraMenus");
            Undo.RegisterCreatedObjectUndo(go, "Crear CamaraMenus");
            go.transform.SetParent(main.transform, false);
        }

        Camera cam = go.GetComponent<Camera>();
        if (cam == null) cam = go.AddComponent<Camera>();
        cam.CopyFrom(main); // copia tambien la posicion: se vuelve a poner en el centro de la Main Camera
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.layer = main.gameObject.layer;
        cam.cullingMask = main.cullingMask & ~(1 << UiLayer); // sin la interfaz
        cam.targetTexture = null;
        cam.depth = main.depth - 1;
        cam.enabled = false;

        UniversalAdditionalCameraData mainData = main.GetUniversalAdditionalCameraData();
        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = mainData.renderPostProcessing;
        data.antialiasing = AntialiasingMode.None;
        data.renderShadows = mainData.renderShadows;
        data.volumeLayerMask = mainData.volumeLayerMask;

        MenuCamera menu = go.GetComponent<MenuCamera>();
        if (menu == null) menu = go.AddComponent<MenuCamera>();
        Transform window = ui.transform.Find("Inicio/Contenido/Ventana");
        Transform photo = ui.transform.Find("Pausa/Contenido/Afiche/Foto");
        Transform cut = window != null ? window.Find("Corte") : null;
        menu.Setup(
            window != null ? window.GetComponent<RawImage>() : null,
            cut != null ? cut.GetComponent<CanvasGroup>() : null,
            photo != null ? photo.GetComponent<RawImage>() : null);
        EditorUtility.SetDirty(menu);
        return menu;
    }

    /// <summary>
    /// "CamaraInterfaz": camara Overlay hija de la Main Camera que solo ve la capa UI.
    /// Va en la pila (Stack) de la Main Camera, despues de ella, y sin post-procesado.
    /// </summary>
    private static Camera SetupUiCamera(Camera main)
    {
        Transform existing = main.transform.Find("CamaraInterfaz");
        GameObject go;
        if (existing != null) go = existing.gameObject;
        else
        {
            go = new GameObject("CamaraInterfaz");
            Undo.RegisterCreatedObjectUndo(go, "Crear CamaraInterfaz");
            go.transform.SetParent(main.transform, false);
        }
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        Camera cam = go.GetComponent<Camera>();
        if (cam == null) cam = go.AddComponent<Camera>();
        cam.cullingMask = 1 << UiLayer;
        cam.fieldOfView = main.fieldOfView;
        cam.nearClipPlane = main.nearClipPlane;
        cam.farClipPlane = main.nearClipPlane + 1f;

        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        data.renderType = CameraRenderType.Overlay;
        data.renderPostProcessing = false;
        data.antialiasing = AntialiasingMode.None;
        data.renderShadows = false;

        UniversalAdditionalCameraData mainData = main.GetUniversalAdditionalCameraData();
        if (!mainData.cameraStack.Contains(cam)) mainData.cameraStack.Add(cam);
        Undo.RecordObject(main, "Main Camera sin interfaz");
        main.cullingMask &= ~(1 << UiLayer); // la interfaz ya la dibuja la otra
        EditorUtility.SetDirty(main);
        EditorUtility.SetDirty(mainData);
        return cam;
    }

    private static TitleWalker SetupTitleWalker(FirstPersonController controller, MenuCamera view)
    {
        if (controller == null)
        {
            Debug.LogWarning("[Callejones] No encuentro el Player (FirstPersonController): el inicio no va a tener caminata.");
            return null;
        }

        // La "cabeza" que mira arriba y abajo es la misma que usa el controlador.
        var head = new SerializedObject(controller).FindProperty("cameraPivot").objectReferenceValue as Transform;

        TitleWalker walker = controller.GetComponent<TitleWalker>();
        if (walker == null) walker = Undo.AddComponent<TitleWalker>(controller.gameObject);
        walker.Setup(head, FindInScene<CorridorTreadmill>(null), view);
        walker.enabled = false; // lo prende GameFlow en el inicio
        EditorUtility.SetDirty(walker);
        return walker;
    }

    // ================================================================== recursos

    /// <summary>Crea (una sola vez) la fuente de TextMeshPro a partir del .ttf de DotGothic16.</summary>
    private static TMP_FontAsset EnsureUiFont()
    {
        var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UiFontAssetPath);
        if (asset != null) return asset;

        var font = AssetDatabase.LoadAssetAtPath<Font>(UiFontFile);
        if (font == null)
        {
            Debug.LogWarning("[Callejones] No encuentro " + UiFontFile + ": uso la fuente por defecto de TextMeshPro.");
            return TMP_Settings.defaultFontAsset;
        }

        // SDF: letras nitidas a cualquier tamano. Dinamica: si aparece una letra nueva, se agrega sola.
        asset = TMP_FontAsset.CreateFontAsset(font, 48, 6, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        if (asset == null) return TMP_Settings.defaultFontAsset;
        asset.name = "DotGothic16 SDF";
        AssetDatabase.CreateAsset(asset, UiFontAssetPath);

        // La textura y el material tienen que vivir DENTRO del archivo de la fuente:
        // si no, se pierden al cerrar Unity y las letras salen como cuadrados.
        asset.atlasTextures[0].name = "DotGothic16 Atlas";
        AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
        asset.material.name = "DotGothic16 Material";
        AssetDatabase.AddObjectToAsset(asset.material, asset);

        asset.TryAddCharacters(UiChars, out string missing);
        if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("[Callejones] DotGothic16 no tiene: " + missing);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    private static UiSprites LoadUiSprites()
    {
        return new UiSprites
        {
            farol = UiSprite("UI_Farol", false),
            mijin = UiSprite("UI_MiJin_V", false),
            shizong = UiSprite("UI_ShiZong", false),
            guitu = UiSprite("UI_GuiTu", false),
            guituV = UiSprite("UI_GuiTu_V", false),
            sheding = UiSprite("UI_SheDing", false),
            segFull = UiSprite("UI_Segmento_Lleno", true),
            segEmpty = UiSprite("UI_Segmento_Vacio", true),
            dot = UiSprite("UI_Punto", true),
            dashH = UiSprite("UI_Guion_H", true),
            dashV = UiSprite("UI_Guion_V", true),
            circle = UiSprite("UI_Circulo", false),
            vignette = UiSprite("UI_Vineta", false),
            barcode = UiSprite("UI_CodigoBarras", false),
        };
    }

    /// <summary>Importa un PNG de Textures/UI como Sprite con pixeles duros. 'tiled' = se repite.</summary>
    private static Sprite UiSprite(string name, bool tiled)
    {
        string path = UiDir + "/" + name + ".png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("[Callejones] Falta " + path + ". Corre Herramientas/generar_ui.py.");
            return null;
        }

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.textureType = TextureImporterType.Sprite;
        settings.spriteMode = (int)SpriteImportMode.Single;
        settings.spritePixelsPerUnit = 100f;             // 1 pixel del PNG = 1 pixel de interfaz
        settings.spriteMeshType = SpriteMeshType.FullRect; // necesario para repetirlo (Tiled)
        settings.filterMode = FilterMode.Point;
        settings.mipmapEnabled = false;
        settings.alphaIsTransparency = true;
        settings.wrapMode = tiled ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        settings.npotScale = TextureImporterNPOTScale.None;
        importer.SetTextureSettings(settings);
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ================================================================== ayudantes
    // Todos ubican las cosas desde la esquina de arriba a la izquierda del padre: (x, y) con y hacia abajo.

    private static RectTransform UiRect(Transform parent, string name, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = UiLayer;
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
        return rt;
    }

    /// <summary>
    /// El area de 640 x 360 centrada en la pantalla donde va el contenido de un panel.
    /// Con el CanvasScaler en "Expand" siempre entra entera; los fondos van afuera de ella.
    /// </summary>
    private static Transform UiContent(Transform panel)
    {
        var go = new GameObject("Contenido", typeof(RectTransform));
        go.layer = UiLayer;
        go.transform.SetParent(panel, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(640f, 360f);
        return rt;
    }

    private static CanvasGroup UiPanel(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = UiLayer;
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform);
        return go.AddComponent<CanvasGroup>();
    }

    /// <summary>Una imagen que ocupa todo el padre.</summary>
    private static Image UiFill(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = UiLayer;
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform);
        var image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Image UiBox(Transform parent, string name, Color color, float x, float y, float w, float h)
    {
        var image = UiRect(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Image UiImage(Transform parent, string name, Sprite sprite, Color color, float x, float y, float w, float h)
    {
        Image image = UiBox(parent, name, color, x, y, w, h);
        image.sprite = sprite;
        return image;
    }

    /// <summary>Un dibujo chico que se repite a lo largo (lineas cortadas, puntitos).</summary>
    private static Image UiTiled(Transform parent, string name, Sprite sprite, Color color, float x, float y, float w, float h)
    {
        Image image = UiImage(parent, name, sprite, color, x, y, w, h);
        image.type = Image.Type.Tiled;
        return image;
    }

    private static TextMeshProUGUI UiText(Transform parent, string name, string text, float x, float y, float w, float h,
        float size, Color color, TextAlignmentOptions align, float spacing = 0f)
    {
        var label = UiRect(parent, name, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = uiFont != null ? uiFont : TMP_Settings.defaultFontAsset;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.characterSpacing = spacing;
        label.alignment = align;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        return label;
    }

    /// <summary>Colores de una opcion: 'normal' en reposo y 'selected' al elegirla (teclado o mouse).</summary>
    private static ColorBlock UiColors(Color normal, Color selected)
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = normal;
        colors.highlightedColor = selected;
        colors.selectedColor = selected;
        colors.pressedColor = UiPressed;
        colors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        return colors;
    }

    /// <summary>
    /// Boton de solo texto con el farolito a la izquierda cuando esta elegido (inicio y final).
    /// El texto es blanco: el color real lo pone el tinte del boton.
    /// </summary>
    private static Button UiMenuButton(Transform parent, string name, string text, float x, float y,
        Sprite marker, Color normal, Color selected, float size = 17f)
    {
        TextMeshProUGUI label = UiText(parent, name, text, x, y, 200f, 28f, size, Color.white, TextAlignmentOptions.Left, 14f);
        label.raycastTarget = true; // el texto recibe el clic
        Image lantern = UiImage(label.transform, "Farol", marker, Color.white, -19f, 6.5f, 11f, 15f);

        var button = label.gameObject.AddComponent<Button>();
        button.targetGraphic = label;
        button.colors = UiColors(normal, selected);
        label.gameObject.AddComponent<SelectOnHover>();
        label.gameObject.AddComponent<MenuItemMarker>().Setup(lantern);
        return button;
    }

    /// <summary>Opcion del afiche: texto en tinta y un cuadradito rojo cuando esta elegida.</summary>
    private static Button UiPosterButton(Transform poster, string name, string text, float x, float y)
    {
        TextMeshProUGUI label = UiText(poster, name, text, x, y, 180f, 24f, 15f, Color.white, TextAlignmentOptions.Left);
        label.raycastTarget = true;
        Image square = UiBox(label.transform, "Marca", UiRed, -16f, 8f, 8f, 8f);

        var button = label.gameObject.AddComponent<Button>();
        button.targetGraphic = label;
        button.colors = UiColors(UiInkSoft, UiInk);
        label.gameObject.AddComponent<SelectOnHover>();
        label.gameObject.AddComponent<MenuItemMarker>().Setup(square);
        return button;
    }

    /// <summary>
    /// Arriba y abajo con el teclado: cada opcion apunta a la anterior y a la siguiente
    /// (la ultima vuelve a la primera). Explicito para que nunca salte a otro panel.
    /// </summary>
    private static void UiChain(params Selectable[] items)
    {
        for (int i = 0; i < items.Length; i++)
        {
            Navigation nav = items[i].navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp = items[(i - 1 + items.Length) % items.Length];
            nav.selectOnDown = items[(i + 1) % items.Length];
            nav.selectOnLeft = null;
            nav.selectOnRight = null;
            items[i].navigation = nav;
        }
    }
}
