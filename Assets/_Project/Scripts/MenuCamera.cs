using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// La "camara de los menus" (Paso 17). Es una segunda camara, hija de la Main Camera, que en vez
/// de dibujar en la pantalla dibuja en una textura (RenderTexture):
/// - En el inicio, esa textura se ve en la ventana enmarcada de la derecha, mientras TitleWalker
///   hace caminar al jugador solo por el laberinto.
/// - En la pausa, saca UNA foto de lo que se estaba viendo, la pasa a sepia y la pega en el afiche.
///
/// Por que una segunda camara y no la Main Camera: la Main Camera tambien dibuja la interfaz
/// (el Canvas es "Screen Space - Camera"), y la ventana necesita solo el callejon, centrado y con
/// otra proporcion. Como es hija de la Main Camera y su vista es mas angosta, todo lo que muestra
/// tambien lo "ve" el jugador: el laberinto nunca cambia algo delante de esta camara.
///
/// Va en el objeto "CamaraMenus", hijo de la Main Camera. Esta apagada casi siempre.
/// </summary>
[RequireComponent(typeof(Camera))]
public class MenuCamera : MonoBehaviour
{
    [Header("Ventana del inicio")]
    [SerializeField] private RawImage window;
    [Tooltip("Negro encima de la ventana, para los cortes entre tomas.")]
    [SerializeField] private CanvasGroup windowCut;
    [Tooltip("Pixeles de la ventana. Pocos a proposito: se ve igual de pixelada que el juego.")]
    [SerializeField] private Vector2Int windowResolution = new Vector2Int(220, 198);

    [Header("Foto de la pausa")]
    [SerializeField] private RawImage photo;
    [SerializeField] private Vector2Int photoResolution = new Vector2Int(156, 54);
    [Tooltip("Color de las sombras de la foto (tinta).")]
    [SerializeField] private Color photoDark = new Color32(0x1D, 0x0C, 0x07, 255);
    [Tooltip("Color de las luces de la foto (papel).")]
    [SerializeField] private Color photoLight = new Color32(0xE8, 0xD6, 0xB0, 255);

    // Patron de puntos 4 x 4 (Bayer): reparte los grises en pocos tonos, como una fotocopia.
    private static readonly float[] Bayer =
    {
        0f, 8f, 2f, 10f,
        12f, 4f, 14f, 6f,
        3f, 11f, 1f, 9f,
        15f, 7f, 13f, 5f,
    };

    private Camera cam;
    private RenderTexture windowTexture, photoTexture;
    private Texture2D photoImage, photoRead;
    private Vector3 baseLocalPosition;
    private float heightOffset;
    private float builtScale = -1f;
    private bool windowOn;

    public void Setup(RawImage windowImage, CanvasGroup cut, RawImage photoImageTarget)
    {
        window = windowImage;
        windowCut = cut;
        photo = photoImageTarget;
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.enabled = false;
        baseLocalPosition = transform.localPosition;

        photoImage = new Texture2D(photoResolution.x, photoResolution.y, TextureFormat.RGBA32, false)
        {
            name = "Foto sepia",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        if (photo != null) photo.texture = photoImage;
        if (windowCut != null) windowCut.alpha = 1f; // la ventana arranca en negro
        EnsureTextures();
    }

    private void OnDestroy()
    {
        ReleaseTextures();
        Destroy(photoImage);
        if (photoRead != null) Destroy(photoRead);
    }

    /// <summary>
    /// URP dibuja TODAS las camaras a "Render Scale" del tamano de su destino (RetroSettings la baja
    /// para que el juego salga a 240 lineas). Para que la ventana tenga de verdad windowResolution
    /// pixeles, la textura se hace mas grande en la misma proporcion. Si la escala cambia (por
    /// ejemplo, al cambiar el tamano de la ventana del juego), las texturas se rehacen.
    /// </summary>
    private void EnsureTextures()
    {
        float scale = CurrentRenderScale();
        if (windowTexture != null && Mathf.Abs(scale - builtScale) < 0.01f) return;
        builtScale = scale;

        bool showingWindow = cam.targetTexture == windowTexture;
        ReleaseTextures();
        windowTexture = NewTexture(windowResolution, scale, "Ventana del inicio");
        photoTexture = NewTexture(photoResolution, scale, "Foto de la pausa");
        if (window != null) window.texture = windowTexture;
        if (showingWindow || cam.targetTexture == null) SetTarget(windowTexture);
    }

    private void ReleaseTextures()
    {
        if (cam != null && (cam.targetTexture == windowTexture || cam.targetTexture == photoTexture)) cam.targetTexture = null;
        if (windowTexture != null) { windowTexture.Release(); Destroy(windowTexture); }
        if (photoTexture != null) { photoTexture.Release(); Destroy(photoTexture); }
        windowTexture = photoTexture = null;
    }

    private static float CurrentRenderScale()
    {
        var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        return urp != null ? Mathf.Clamp(urp.renderScale, 0.1f, 2f) : 1f;
    }

    private static RenderTexture NewTexture(Vector2Int size, float scale, string name)
    {
        int w = Mathf.CeilToInt(size.x / scale), h = Mathf.CeilToInt(size.y / scale);
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
        {
            name = name,
            filterMode = FilterMode.Point, // pixeles duros al agrandarla
        };
        rt.Create();
        return rt;
    }

    // ------------------------------------------------------------------ ventana del inicio

    public void ShowWindow(bool visible)
    {
        windowOn = visible;
        EnsureTextures();
        SetTarget(windowTexture);
        cam.enabled = visible;
    }

    /// <summary>Lleva el negro de la ventana a 'alpha' (1 = negro). Se puede esperar con yield.</summary>
    public IEnumerator FadeWindow(float alpha, float seconds)
    {
        if (windowCut == null) yield break;
        float start = windowCut.alpha;
        for (float t = 0f; t < seconds; t += Mathf.Min(Time.unscaledDeltaTime, 0.05f))
        {
            windowCut.alpha = Mathf.Lerp(start, alpha, Mathf.SmoothStep(0f, 1f, t / seconds));
            yield return null;
        }
        windowCut.alpha = alpha;
    }

    /// <summary>Baja la camara sin mover al jugador (la toma "a la altura de un gato"). En metros.</summary>
    public void SetHeightOffset(float meters) => heightOffset = meters;

    // LateUpdate: despues de que el jugador y la cabeza se movieron en este frame.
    private void LateUpdate()
    {
        if (windowOn) EnsureTextures();
        Transform parent = transform.parent;
        Vector3 down = parent != null ? parent.InverseTransformDirection(Vector3.down) : Vector3.down;
        transform.localPosition = baseLocalPosition + down * heightOffset;
    }

    // ------------------------------------------------------------------ foto de la pausa

    /// <summary>Saca la foto de lo que se ve ahora y la pone en el afiche (tarda un frame).</summary>
    public void TakePhoto()
    {
        if (photo != null && isActiveAndEnabled) StartCoroutine(PhotoRoutine());
    }

    private IEnumerator PhotoRoutine()
    {
        EnsureTextures();
        bool wasOn = cam.enabled;
        SetTarget(photoTexture);
        cam.enabled = true;

        // Al final del frame la camara ya dibujo en la textura (aunque el juego este en pausa:
        // timeScale 0 detiene el tiempo, no el dibujo).
        yield return new WaitForEndOfFrame();

        // Se lee la textura entera (puede ser mas grande, ver EnsureTextures)...
        if (photoRead == null || photoRead.width != photoTexture.width || photoRead.height != photoTexture.height)
        {
            if (photoRead != null) Destroy(photoRead);
            photoRead = new Texture2D(photoTexture.width, photoTexture.height, TextureFormat.RGBA32, false);
        }
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = photoTexture;
        photoRead.ReadPixels(new Rect(0, 0, photoTexture.width, photoTexture.height), 0, 0, false);
        RenderTexture.active = previous;

        SetTarget(windowTexture);
        cam.enabled = wasOn;

        // ...y se achica a photoResolution tomando un pixel de cada bloque.
        Color32[] source = photoRead.GetPixels32();
        var pixels = new Color32[photoImage.width * photoImage.height];
        for (int y = 0; y < photoImage.height; y++)
            for (int x = 0; x < photoImage.width; x++)
            {
                int sx = Mathf.Min(photoRead.width - 1, (int)((x + 0.5f) * photoRead.width / photoImage.width));
                int sy = Mathf.Min(photoRead.height - 1, (int)((y + 0.5f) * photoRead.height / photoImage.height));
                pixels[y * photoImage.width + x] = source[sy * photoRead.width + sx];
            }
        photoImage.SetPixels32(pixels);
        ToSepia(photoImage);
    }

    private void SetTarget(RenderTexture target)
    {
        cam.targetTexture = target;
        cam.ResetAspect(); // la proporcion de la camara pasa a ser la de la textura
    }

    /// <summary>
    /// Pasa la foto a dos tintas (tinta y papel) con un patron de puntos.
    /// Antes estira el contraste segun la propia foto (como el "nivel automatico" de una
    /// fotocopiadora): lo mas oscuro pasa a tinta y lo mas claro a papel. Asi se entiende
    /// aunque se haya pausado mirando un rincon casi negro.
    /// </summary>
    private void ToSepia(Texture2D image)
    {
        Color32[] pixels = image.GetPixels32();
        int width = image.width;
        Color32 dark = photoDark, light = photoLight;
        const int levels = 4;

        // Brillo de cada pixel y un histograma de 256 casilleros.
        var lums = new float[pixels.Length];
        var histogram = new int[256];
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 p = pixels[i];
            lums[i] = (0.299f * p.r + 0.587f * p.g + 0.114f * p.b) / 255f;
            histogram[Mathf.Clamp((int)(lums[i] * 255f), 0, 255)]++;
        }
        // El 4 % mas oscuro va a tinta y el 3 % mas claro a papel (asi un farol no lo arruina).
        float low = Percentile(histogram, pixels.Length, 0.04f);
        float high = Mathf.Max(low + 0.08f, Percentile(histogram, pixels.Length, 0.97f));

        for (int i = 0; i < pixels.Length; i++)
        {
            float lum = Mathf.Clamp01((lums[i] - low) / (high - low));

            int x = i % width, y = i / width;
            float threshold = (Bayer[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f - 0.5f;
            float level = Mathf.Clamp(Mathf.Round(lum * levels + threshold), 0f, levels) / levels;
            pixels[i] = Color32.Lerp(dark, light, level);
        }

        image.SetPixels32(pixels);
        image.Apply(false);
    }

    /// <summary>El brillo (0 a 1) por debajo del cual queda la fraccion 'part' de los pixeles.</summary>
    private static float Percentile(int[] histogram, int total, float part)
    {
        int goal = Mathf.RoundToInt(total * part), sum = 0;
        for (int b = 0; b < histogram.Length; b++)
        {
            sum += histogram[b];
            if (sum >= goal) return b / 255f;
        }
        return 1f;
    }
}
