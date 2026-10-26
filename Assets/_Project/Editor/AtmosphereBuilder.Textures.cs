using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Parte de AtmosphereBuilder: texturas PS1 generadas por codigo y mallas con UV en metros.
///
/// Texturas: 64x64 pixeles que cubren 2 x 2 metros (32 pixeles por metro, como la PS1),
/// filtro Point (pixeles duros) y sin mipmaps. Se repiten sin cortes en los bordes.
///
/// UV en metros: un cubo de Unity estira su textura de 0 a 1 en cada cara, sin importar
/// su tamano. Una pared de 6 m y un bloqueo de 2.8 m mostrarian ladrillos de distinto
/// tamano. Por eso las paredes y el piso se reemplazan por mallas generadas cuyo UV
/// es la posicion en metros dividida por 2: el ladrillo mide lo mismo en todas partes.
/// </summary>
public static partial class AtmosphereBuilder
{
    private const string TexturesDir = Root + "/Textures";
    private const float TextureMeters = 2f;

    private delegate void Painter(Color[] px, int w, int h, System.Random rng);

    private class Textures
    {
        public Texture2D ladrillo, piedra, madera, kanbanRojo, kanbanPapel;
    }

    private static Textures BuildTextures()
    {
        return new Textures
        {
            ladrillo = GetOrCreateTexture("Ladrillo", 64, 64, 11, PaintBrickWall),
            piedra = GetOrCreateTexture("Piedra", 64, 64, 23, PaintStoneFloor),
            madera = GetOrCreateTexture("Madera", 32, 32, 37, PaintWood),
            kanbanRojo = GetOrCreateTexture("CartelRojo", 16, 64, 41, PaintSign(Hex("#B8281A"), Hex("#F2E2C2"))),
            kanbanPapel = GetOrCreateTexture("CartelPapel", 16, 64, 53, PaintSign(Hex("#E8D6B0"), Hex("#2A1410"))),
        };
    }

    private static Texture2D GetOrCreateTexture(string name, int w, int h, int seed, Painter paint)
    {
        EnsureFolder(TexturesDir);
        string path = TexturesDir + "/" + name + ".png";

        var px = new Color[w * h];
        paint(px, w, h, new System.Random(seed)); // semilla fija: siempre sale la misma textura
        for (int i = 0; i < px.Length; i++) px[i].a = 1f;

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static float Rand(System.Random r) => (float)r.NextDouble();

    // Ladrillo con manchas de revoque y chorreaduras verticales.
    private static void PaintBrickWall(Color[] px, int w, int h, System.Random r)
    {
        Color brick = Hex("#7A5646"), mortar = Hex("#2E2622"), plaster = Hex("#A0968A");
        const int bh = 8, bw = 16; // 25 cm x 50 cm
        int rows = h / bh, cols = w / bw;
        var shade = new float[rows * cols];
        for (int i = 0; i < shade.Length; i++) shade[i] = 0.75f + 0.45f * Rand(r);

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int row = y / bh;
            int sx = (x + (row % 2) * (bw / 2)) % w; // hilada desplazada medio ladrillo
            bool joint = y % bh == 0 || sx % bw == 0;
            Color c = joint ? mortar : brick * shade[row * cols + sx / bw];
            px[y * w + x] = c * (0.9f + 0.18f * Rand(r));
        }

        // Revoque: manchas que tapan el ladrillo. Usan % para continuar del otro lado del borde.
        for (int i = 0; i < 6; i++)
        {
            int cx = r.Next(w), cy = r.Next(h), rad = r.Next(5, 13);
            for (int dy = -rad; dy <= rad; dy++)
            for (int dx = -rad; dx <= rad; dx++)
            {
                if (Mathf.Sqrt(dx * dx + dy * dy) + Rand(r) * 3f > rad) continue;
                int x = (cx + dx + w) % w, y = (cy + dy + h) % h;
                px[y * w + x] = plaster * (0.85f + 0.2f * Rand(r));
            }
        }

        // Chorreaduras de lluvia: columnas mas oscuras de arriba a abajo.
        for (int i = 0; i < 6; i++)
        {
            int x0 = r.Next(w), width = r.Next(1, 3);
            float k = 0.6f + 0.25f * Rand(r);
            for (int y = 0; y < h; y++)
            for (int dx = 0; dx < width; dx++)
                px[y * w + (x0 + dx) % w] *= k;
        }
    }

    // Losas de piedra con juntas oscuras, charcos frios y alguna grieta.
    private static void PaintStoneFloor(Color[] px, int w, int h, System.Random r)
    {
        Color stone = Hex("#5E5852"), joint = Hex("#1E1A18"), water = Hex("#1A1E22");
        const int s = 16; // losas de 50 cm
        int n = w / s;
        var shade = new float[n * n];
        for (int i = 0; i < shade.Length; i++) shade[i] = 0.7f + 0.5f * Rand(r);

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            bool j = x % s == 0 || y % s == 0;
            Color c = j ? joint : stone * shade[(y / s) * n + x / s];
            px[y * w + x] = c * (0.86f + 0.22f * Rand(r));
        }

        for (int i = 0; i < 4; i++)
        {
            int cx = r.Next(w), cy = r.Next(h), rad = r.Next(4, 10);
            for (int dy = -rad; dy <= rad; dy++)
            for (int dx = -rad; dx <= rad; dx++)
            {
                if (Mathf.Sqrt(dx * dx + dy * dy) + Rand(r) * 2f > rad) continue;
                int k = ((cy + dy + h) % h) * w + (cx + dx + w) % w;
                px[k] = Color.Lerp(px[k] * 0.6f, water, 0.45f);
            }
        }

        for (int i = 0; i < 3; i++)
        {
            int x = r.Next(w), y = r.Next(h);
            for (int step = 0; step < 14; step++)
            {
                px[y * w + x] *= 0.45f;
                x = (x + r.Next(-1, 2) + w) % w;
                y = (y + 1) % h;
            }
        }
    }

    // Tablas horizontales con vetas, para los cajones.
    private static void PaintWood(Color[] px, int w, int h, System.Random r)
    {
        Color wood = Hex("#6A4A32"), gap = Hex("#241810");
        const int plank = 8;
        var shade = new float[h / plank];
        for (int i = 0; i < shade.Length; i++) shade[i] = 0.8f + 0.35f * Rand(r);

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int row = y / plank;
            bool g = y % plank == 0 || x == 0;
            float grain = 0.9f + 0.1f * Mathf.Sin(x * 0.7f + row * 2.3f);
            Color c = g ? gap : wood * shade[row] * grain;
            px[y * w + x] = c * (0.9f + 0.15f * Rand(r));
        }
    }

    // Cartel vertical con cuatro "caracteres" inventados: trazos al azar que a baja
    // resolucion se leen como letras, sin copiar ningun idioma real.
    private static Painter PaintSign(Color paper, Color ink)
    {
        return (px, w, h, r) =>
        {
            for (int i = 0; i < px.Length; i++) px[i] = paper * (0.92f + 0.08f * Rand(r));
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (x == 0 || x == w - 1 || y == 0 || y == h - 1) px[y * w + x] = ink * 0.85f;

            const int glyphs = 4, gs = 12;
            int gap = (h - glyphs * gs) / (glyphs + 1);
            int ox = (w - gs) / 2;
            for (int g = 0; g < glyphs; g++)
            {
                int oy = h - gap - g * (gs + gap) - gs;
                int strokes = r.Next(3, 6);
                for (int k = 0; k < strokes; k++)
                {
                    if (r.Next(2) == 0)
                    {
                        int y = oy + r.Next(gs);
                        for (int x = ox + r.Next(3); x < ox + gs - r.Next(3); x++) px[y * w + x] = ink;
                    }
                    else
                    {
                        int x = ox + r.Next(gs);
                        for (int y = oy + r.Next(3); y < oy + gs - r.Next(3); y++) px[y * w + x] = ink;
                    }
                }
                if (r.Next(3) == 0) // un cuadradito, como en muchos caracteres
                {
                    int bx = ox + r.Next(2, 6), by = oy + r.Next(2, 6), bs = r.Next(3, 6);
                    for (int i = 0; i <= bs; i++)
                    {
                        px[by * w + bx + i] = ink; px[(by + bs) * w + bx + i] = ink;
                        px[(by + i) * w + bx] = ink; px[(by + i) * w + bx + bs] = ink;
                    }
                }
            }
        };
    }

    // ------------------------------------------------------------------ mallas con UV en metros

    /// <summary>
    /// Reemplaza el cubo o plano de Unity de una pared o piso por una malla generada del mismo
    /// tamano con UV en metros. La escala pasa a 1 y el collider se ajusta al tamano real.
    /// Se puede llamar varias veces: la segunda vez el tamano sale de la malla ya generada.
    /// </summary>
    private static void ConvertToMetric(Transform t, Vector3? forcedSize = null)
    {
        var mf = t.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;

        bool isFloor = t.name == "Piso";
        Vector3 size = forcedSize ?? Vector3.Scale(t.localScale, mf.sharedMesh.bounds.size);

        Mesh mesh = isFloor ? GetFloorMesh(size) : GetBoxMesh(size);
        mf.sharedMesh = mesh;
        t.localScale = Vector3.one;

        var box = t.GetComponent<BoxCollider>();
        if (box != null) { box.size = size; box.center = Vector3.zero; }
        var meshCollider = t.GetComponent<MeshCollider>();
        if (meshCollider != null) meshCollider.sharedMesh = mesh;
    }

    private static string SizeName(Vector3 s)
    {
        string f(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        return f(s.x) + "x" + f(s.y) + "x" + f(s.z);
    }

    private static Mesh GetBoxMesh(Vector3 size)
    {
        EnsureFolder(MeshesDir);
        Mesh mesh = GetOrCreateMesh("Caja_" + SizeName(size));
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        Vector3 h = size * 0.5f;

        // Cada cara: su normal y los dos ejes que la recorren (horizontal y vertical).
        AddBoxFace(v, n, uv, t, Vector3.right, Vector3.forward, Vector3.up, h);
        AddBoxFace(v, n, uv, t, Vector3.left, Vector3.forward, Vector3.up, h);
        AddBoxFace(v, n, uv, t, Vector3.forward, Vector3.right, Vector3.up, h);
        AddBoxFace(v, n, uv, t, Vector3.back, Vector3.right, Vector3.up, h);
        AddBoxFace(v, n, uv, t, Vector3.up, Vector3.right, Vector3.forward, h);
        AddBoxFace(v, n, uv, t, Vector3.down, Vector3.right, Vector3.forward, h);

        Apply(mesh, v, n, t, uv);
        return mesh;
    }

    private static void AddBoxFace(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t,
        Vector3 normal, Vector3 uAxis, Vector3 vAxis, Vector3 half)
    {
        Vector3 c = Vector3.Scale(normal, half);
        Vector3 du = Vector3.Scale(uAxis, half);
        Vector3 dv = Vector3.Scale(vAxis, half);
        Vector3 a = c - du - dv, b = c + du - dv, cc = c + du + dv, d = c - du + dv;
        AddQuadUV(v, n, uv, t, a, b, cc, d, uAxis, vAxis, normal);
    }

    // Piso: un solo cuadrado mirando hacia arriba, a la altura 0.
    private static Mesh GetFloorMesh(Vector3 size)
    {
        EnsureFolder(MeshesDir);
        Mesh mesh = GetOrCreateMesh("Suelo_" + SizeName(new Vector3(size.x, 0f, size.z)));
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        float hx = size.x * 0.5f, hz = size.z * 0.5f;
        AddQuadUV(v, n, uv, t,
            new Vector3(-hx, 0f, -hz), new Vector3(hx, 0f, -hz), new Vector3(hx, 0f, hz), new Vector3(-hx, 0f, hz),
            Vector3.right, Vector3.forward, Vector3.up);
        Apply(mesh, v, n, t, uv);
        return mesh;
    }

    /// <summary>Como AddQuad, pero agrega UV = posicion en metros / TextureMeters.</summary>
    private static void AddQuadUV(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 uAxis, Vector3 vAxis, Vector3 outward)
    {
        int first = v.Count;
        AddQuad(v, n, t, a, b, c, d, outward);
        for (int i = first; i < v.Count; i++)
            uv.Add(new Vector2(Vector3.Dot(v[i], uAxis), Vector3.Dot(v[i], vAxis)) / TextureMeters);
    }

    private static void Apply(Mesh mesh, List<Vector3> verts, List<Vector3> norms, List<int> tris, List<Vector2> uvs)
    {
        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
    }
}
