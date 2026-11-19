using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Paso 40: los objetos de la feria con textura propia (antes eran cajitas de un color de la
/// paleta): peluches, mascaras, el pez en su bolsita, la bolita de la ramune y el papelito de la
/// adivina. Se usan en los puestos de la feria y, en el capitulo 2, cuando aparecen tirados en un
/// callejon (FairEchoes).
///
/// Una sola textura, "Objetos" (128 x 128, cuadros de 32 x 32), pintada aca con codigo como las
/// demas, y un material "Objetos". Cada modelo es una malla chica con sus UV en su cuadro:
///
///   fila 0: pelo marron | cara de oso marron | mascara de zorro | mascara de gato
///   fila 1: bolsita con pez | nudo de la bolsita | bolita de vidrio | papelito escrito
///   fila 2: pelo rosa | cara rosa | pelo amarillo | cara amarilla
///   fila 3: mascara de oni | mascara de zorro negro | pelo blanco | cara blanca
/// </summary>
public static partial class AtmosphereBuilder
{
    private const int ItemsCell = 32, ItemsSize = 128;
    private static Material itemsMaterial, itemsGlowMaterial;

    public enum BearColor { Marron, Rosa, Amarillo, Blanco }
    public enum MaskKind { Zorro, Gato, Oni, ZorroNegro }

    /// <summary>El material de los objetos (con su textura). Se arma una vez por menu.</summary>
    private static Material ItemsMaterial()
    {
        if (itemsMaterial != null) return itemsMaterial;
        itemsMaterial = Mat("Objetos", Color.white, 0.2f, baseMap: ItemsTexture());
        return itemsMaterial;
    }

    /// <summary>
    /// El mismo, con un poco de brillo propio: para los callejones, donde de noche un objeto de 30 cm
    /// tirado contra la pared no se veia.
    /// </summary>
    private static Material ItemsGlowMaterial()
    {
        if (itemsGlowMaterial != null) return itemsGlowMaterial;
        Material plain = ItemsMaterial();
        Texture2D tex = plain.GetTexture("_BaseMap") as Texture2D;
        itemsGlowMaterial = Mat("ObjetosEco", Color.white, 0.2f, 0f, null, Color.white * 0.45f, tex, tex);
        return itemsGlowMaterial;
    }

    // ------------------------------------------------------------------ la textura

    private static Texture2D ItemsTexture()
    {
        EnsureFolder(TexturesDir);
        string path = TexturesDir + "/Objetos.png";
        var px = new Color[ItemsSize * ItemsSize];
        var r = new System.Random(4001);
        PaintFur(px, 0, 0, Hex("#8A5A34"), r);
        PaintBearFace(px, 1, 0, Hex("#8A5A34"), r);
        PaintMask(px, 2, 0, MaskKind.Zorro, r);
        PaintMask(px, 3, 0, MaskKind.Gato, r);
        PaintFishBag(px, 0, 1, r);
        PaintBagKnot(px, 1, 1, r);
        PaintMarble(px, 2, 1);
        PaintNote(px, 3, 1, r);
        PaintFur(px, 0, 2, Hex("#D88AA0"), r);
        PaintBearFace(px, 1, 2, Hex("#D88AA0"), r);
        PaintFur(px, 2, 2, Hex("#E0B040"), r);
        PaintBearFace(px, 3, 2, Hex("#E0B040"), r);
        PaintMask(px, 0, 3, MaskKind.Oni, r);
        PaintMask(px, 1, 3, MaskKind.ZorroNegro, r);
        PaintFur(px, 2, 3, Hex("#E8E2D0"), r);
        PaintBearFace(px, 3, 3, Hex("#E8E2D0"), r);

        var tex = new Texture2D(ItemsSize, ItemsSize, TextureFormat.RGBA32, false);
        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = true;    // Paso 39: de lejos no "hierve"
        importer.mipMapBias = -0.3f;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Pinta un pixel del cuadro (cx, cy), con x e y desde arriba a la izquierda del cuadro.
    private static void Px(Color[] px, int cx, int cy, int x, int y, Color c)
    {
        if (x < 0 || y < 0 || x >= ItemsCell || y >= ItemsCell) return;
        int gx = cx * ItemsCell + x, gy = ItemsSize - 1 - (cy * ItemsCell + y);
        px[gy * ItemsSize + gx] = c;
    }

    private static void Fill(Color[] px, int cx, int cy, System.Func<int, int, Color> f)
    {
        for (int y = 0; y < ItemsCell; y++)
            for (int x = 0; x < ItemsCell; x++) Px(px, cx, cy, x, y, f(x, y));
    }

    private static void Ellipse(Color[] px, int cx, int cy, float x0, float y0, float rx, float ry, Color c)
    {
        for (int y = 0; y < ItemsCell; y++)
            for (int x = 0; x < ItemsCell; x++)
            {
                float dx = (x + 0.5f - x0) / rx, dy = (y + 0.5f - y0) / ry;
                if (dx * dx + dy * dy <= 1f) Px(px, cx, cy, x, y, c);
            }
    }

    private static void Rect(Color[] px, int cx, int cy, int x0, int y0, int w, int h, Color c)
    {
        for (int y = y0; y < y0 + h; y++)
            for (int x = x0; x < x0 + w; x++) Px(px, cx, cy, x, y, c);
    }

    private static void PaintFur(Color[] px, int cx, int cy, Color baseColor, System.Random r)
    {
        Fill(px, cx, cy, (x, y) =>
        {
            float n = (float)r.NextDouble();
            float k = n < 0.12f ? 0.78f : n > 0.9f ? 1.12f : 0.95f + 0.08f * Mathf.Sin(x * 1.7f + y * 0.9f);
            return baseColor * k;
        });
    }

    private static void PaintBearFace(Color[] px, int cx, int cy, Color fur, System.Random r)
    {
        PaintFur(px, cx, cy, fur, r);
        Color muzzle = Color.Lerp(fur, Hex("#F2E6CC"), 0.6f), ink = Hex("#1A1010");
        Ellipse(px, cx, cy, 16f, 21f, 8f, 6f, muzzle);
        Rect(px, cx, cy, 9, 12, 3, 3, ink);   // ojos
        Rect(px, cx, cy, 20, 12, 3, 3, ink);
        Px(px, cx, cy, 10, 12, Color.white);  // brillo de los ojos
        Px(px, cx, cy, 21, 12, Color.white);
        Rect(px, cx, cy, 14, 18, 4, 3, ink);  // nariz
        Rect(px, cx, cy, 15, 21, 2, 2, ink);
        Rect(px, cx, cy, 12, 23, 3, 1, ink);  // boca
        Rect(px, cx, cy, 17, 23, 3, 1, ink);
        Rect(px, cx, cy, 6, 18, 3, 2, Hex("#E88A8A")); // cachetes
        Rect(px, cx, cy, 23, 18, 3, 2, Hex("#E88A8A"));
    }

    private static void PaintMask(Color[] px, int cx, int cy, MaskKind kind, System.Random r)
    {
        Color face = kind == MaskKind.Oni ? Hex("#B8301E") : kind == MaskKind.ZorroNegro ? Hex("#1E1A1A") : Hex("#F0E8DA");
        Color mark = kind == MaskKind.ZorroNegro ? Hex("#E0B040") : kind == MaskKind.Oni ? Hex("#E0B040") : Hex("#C8281E");
        Color ink = kind == MaskKind.ZorroNegro ? Hex("#E8E2D0") : Hex("#141010");
        Fill(px, cx, cy, (x, y) => face * (0.95f + 0.05f * (float)r.NextDouble()) * (1f - Mathf.Abs(x - 15.5f) / 70f));
        // Las orejas: arriba en las esquinas (los triangulos de las orejas toman de aca).
        if (kind != MaskKind.Oni)
        {
            Ellipse(px, cx, cy, 6f, 4f, 4f, 4f, kind == MaskKind.Gato ? Hex("#1E1A1A") : face);
            Ellipse(px, cx, cy, 26f, 4f, 4f, 4f, face);
            Ellipse(px, cx, cy, 6f, 5f, 2f, 2.5f, Hex("#E88A8A"));
            Ellipse(px, cx, cy, 26f, 5f, 2f, 2.5f, Hex("#E88A8A"));
        }
        switch (kind)
        {
            case MaskKind.Zorro:
            case MaskKind.ZorroNegro:
                // Ojos rasgados con trazos rojos (o dorados) hacia arriba, marca en la frente.
                for (int i = 0; i < 6; i++)
                {
                    Px(px, cx, cy, 7 + i, 14 - i / 2, mark); Px(px, cx, cy, 7 + i, 15 - i / 2, mark);
                    Px(px, cx, cy, 24 - i, 14 - i / 2, mark); Px(px, cx, cy, 24 - i, 15 - i / 2, mark);
                }
                Rect(px, cx, cy, 9, 15, 5, 2, ink);
                Rect(px, cx, cy, 18, 15, 5, 2, ink);
                Ellipse(px, cx, cy, 16f, 8f, 2f, 3.5f, mark);
                Rect(px, cx, cy, 15, 25, 3, 2, ink);   // nariz
                Rect(px, cx, cy, 11, 28, 10, 1, mark); // boca
                break;
            case MaskKind.Gato:
                // Media cara negra (como Manchas), ojos verdes con pupila, bigotes.
                Fill(px, cx, cy, (x, y) => x < 15 && y > 6 && y < 27 ? Hex("#1E1A1A") : px[(ItemsSize - 1 - (cy * ItemsCell + y)) * ItemsSize + cx * ItemsCell + x]);
                Ellipse(px, cx, cy, 10f, 15f, 3f, 2.5f, Hex("#8AB040"));
                Ellipse(px, cx, cy, 22f, 15f, 3f, 2.5f, Hex("#8AB040"));
                Rect(px, cx, cy, 10, 13, 1, 5, Hex("#141010"));
                Rect(px, cx, cy, 22, 13, 1, 5, Hex("#141010"));
                Rect(px, cx, cy, 15, 21, 3, 2, Hex("#D88AA0"));
                for (int i = 0; i < 6; i++)
                {
                    Px(px, cx, cy, 3 + i, 22 + (i % 2), Hex("#C8C0B0"));
                    Px(px, cx, cy, 28 - i, 22 + (i % 2), Hex("#3A3030"));
                }
                Rect(px, cx, cy, 13, 25, 7, 1, Hex("#3A3030"));
                break;
            case MaskKind.Oni:
                // Cejas gruesas, ojos dorados, colmillos blancos, cuernos.
                Rect(px, cx, cy, 6, 9, 8, 3, Hex("#141010"));
                Rect(px, cx, cy, 18, 9, 8, 3, Hex("#141010"));
                Ellipse(px, cx, cy, 10f, 15f, 3f, 2f, mark);
                Ellipse(px, cx, cy, 22f, 15f, 3f, 2f, mark);
                Rect(px, cx, cy, 10, 15, 1, 1, Hex("#141010"));
                Rect(px, cx, cy, 22, 15, 1, 1, Hex("#141010"));
                Rect(px, cx, cy, 8, 24, 16, 4, Hex("#3A0A06"));
                Rect(px, cx, cy, 9, 24, 2, 3, Color.white);
                Rect(px, cx, cy, 21, 24, 2, 3, Color.white);
                Rect(px, cx, cy, 6, 0, 3, 5, Hex("#E8E2D0"));
                Rect(px, cx, cy, 23, 0, 3, 5, Hex("#E8E2D0"));
                break;
        }
    }

    private static void PaintFishBag(Color[] px, int cx, int cy, System.Random r)
    {
        Fill(px, cx, cy, (x, y) => Color.Lerp(Hex("#BFE0F0"), Hex("#4A86B0"), y / 31f) * (0.96f + 0.04f * (float)r.NextDouble()));
        // El pez naranja, nadando de costado.
        Ellipse(px, cx, cy, 15f, 19f, 6f, 3.5f, Hex("#E86A20"));
        for (int i = 0; i < 4; i++) Rect(px, cx, cy, 21 + i, 17 - i / 2, 1, 5 + i, Hex("#E86A20")); // cola
        Rect(px, cx, cy, 11, 18, 1, 1, Hex("#141010"));
        Rect(px, cx, cy, 13, 16, 4, 1, Hex("#F8A050"));
        // Burbujas y un brillo del plastico.
        Ellipse(px, cx, cy, 8f, 10f, 1.2f, 1.2f, Hex("#EAF6FF"));
        Ellipse(px, cx, cy, 11f, 7f, 0.9f, 0.9f, Hex("#EAF6FF"));
        Rect(px, cx, cy, 4, 4, 1, 18, new Color(1f, 1f, 1f, 1f));
    }

    private static void PaintBagKnot(Color[] px, int cx, int cy, System.Random r)
    {
        Fill(px, cx, cy, (x, y) => Hex("#DCE8F0") * (0.9f + 0.1f * Mathf.Abs(Mathf.Sin(x * 0.8f))));
        Rect(px, cx, cy, 0, 26, 32, 3, Hex("#C8281E")); // la cinta roja del nudo
    }

    private static void PaintMarble(Color[] px, int cx, int cy)
    {
        Fill(px, cx, cy, (x, y) =>
        {
            float a = Mathf.Atan2(y - 16f, x - 16f), d = new Vector2(x - 16f, y - 16f).magnitude;
            float swirl = Mathf.Sin(a * 2f + d * 0.4f);
            return Color.Lerp(Hex("#3A9AB0"), Hex("#BFF0E8"), 0.5f + 0.5f * swirl);
        });
        Ellipse(px, cx, cy, 11f, 10f, 4f, 3f, Color.white);
    }

    private static void PaintNote(Color[] px, int cx, int cy, System.Random r)
    {
        Fill(px, cx, cy, (x, y) => Hex("#EFE2C6") * (0.95f + 0.05f * (float)r.NextDouble()));
        Color ink = Hex("#2A1E1A");
        for (int line = 0; line < 4; line++)
        {
            int y0 = 6 + line * 6, len = line == 3 ? 14 : 24;
            for (int x = 4; x < 4 + len; x++)
                if (r.NextDouble() > 0.2) Px(px, cx, cy, x, y0 + (int)(Mathf.Sin(x * 1.3f + line) * 1.2f), ink);
        }
        // Una patita roja chiquita en la esquina.
        Rect(px, cx, cy, 24, 26, 3, 3, Hex("#B8301E"));
        Px(px, cx, cy, 23, 24, Hex("#B8301E")); Px(px, cx, cy, 25, 23, Hex("#B8301E")); Px(px, cx, cy, 27, 24, Hex("#B8301E"));
    }

    // ------------------------------------------------------------------ mallas con UV

    /// <summary>UV de un punto (u, v de 0 a 1) dentro del cuadro (cx, cy), con 1 pixel de margen.</summary>
    private static Vector2 CellUV(int cx, int cy, float u, float v)
    {
        float x0 = (cx * ItemsCell + 1f) / ItemsSize, x1 = (cx * ItemsCell + ItemsCell - 1f) / ItemsSize;
        float yTop = 1f - (cy * ItemsCell + 1f) / ItemsSize, yBottom = 1f - (cy * ItemsCell + ItemsCell - 1f) / ItemsSize;
        return new Vector2(Mathf.Lerp(x0, x1, u), Mathf.Lerp(yBottom, yTop, v));
    }

    private static void TexTri(MeshDraft d, Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, Vector3 outward)
    {
        int first = d.v.Count;
        AddTriangle(d.v, d.n, d.t, a, b, c, outward);
        d.uv.Add(ua); d.uv.Add(ub); d.uv.Add(uc);
        for (int i = first; i < d.v.Count; i++) d.c.Add(FairNoWindow);
    }

    /// <summary>Un cuadrilatero a-b-c-e (en orden alrededor) con el cuadro entero de la textura.</summary>
    private static void TexQuad(MeshDraft d, Vector3 a, Vector3 b, Vector3 c, Vector3 e, int cx, int cy, Vector3 outward,
        float u0 = 0f, float u1 = 1f, float v0 = 0f, float v1 = 1f)
    {
        // a = abajo izquierda, b = abajo derecha, c = arriba derecha, e = arriba izquierda
        Vector2 ua = CellUV(cx, cy, u0, v0), ub = CellUV(cx, cy, u1, v0), uc = CellUV(cx, cy, u1, v1), ue = CellUV(cx, cy, u0, v1);
        TexTri(d, a, b, c, ua, ub, uc, outward);
        TexTri(d, a, c, e, ua, uc, ue, outward);
    }

    /// <summary>Una caja con todas las caras de un cuadro, salvo el frente (+Z), que puede ser otro.</summary>
    private static void TexBox(MeshDraft d, Vector3 c, Vector3 s, int sideX, int sideY, int frontX = -1, int frontY = -1)
    {
        Vector3 h = s * 0.5f;
        if (frontX < 0) { frontX = sideX; frontY = sideY; }
        Vector3 P(float x, float y, float z) => c + new Vector3(x * h.x, y * h.y, z * h.z);
        TexQuad(d, P(1, -1, 1), P(-1, -1, 1), P(-1, 1, 1), P(1, 1, 1), frontX, frontY, Vector3.forward);    // frente (+Z)
        TexQuad(d, P(-1, -1, -1), P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), sideX, sideY, Vector3.back);
        TexQuad(d, P(1, -1, -1), P(1, -1, 1), P(1, 1, 1), P(1, 1, -1), sideX, sideY, Vector3.right);
        TexQuad(d, P(-1, -1, 1), P(-1, -1, -1), P(-1, 1, -1), P(-1, 1, 1), sideX, sideY, Vector3.left);
        TexQuad(d, P(-1, 1, -1), P(1, 1, -1), P(1, 1, 1), P(-1, 1, 1), sideX, sideY, Vector3.up);
        TexQuad(d, P(-1, -1, 1), P(1, -1, 1), P(1, -1, -1), P(-1, -1, -1), sideX, sideY, Vector3.down);
    }

    // ------------------------------------------------------------------ los modelos

    private static (int fx, int fy, int cx, int cy) BearCells(BearColor color) => color switch
    {
        BearColor.Rosa => (0, 2, 1, 2),
        BearColor.Amarillo => (2, 2, 3, 2),
        BearColor.Blanco => (2, 3, 3, 3),
        _ => (0, 0, 1, 0),
    };

    /// <summary>Un osito sentado, mirando a +Z, con la base en 'at'. Mide unos 30 cm (escala 1).</summary>
    private static void BearModel(MeshDraft d, Vector3 at, float scale, BearColor color, float yaw = 0f)
    {
        var (fx, fy, cx, cy) = BearCells(color);
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        var part = new MeshDraft();
        TexBox(part, new Vector3(0f, 0.09f, 0f), new Vector3(0.16f, 0.17f, 0.12f), fx, fy);            // cuerpo
        TexBox(part, new Vector3(0f, 0.235f, 0.01f), new Vector3(0.15f, 0.13f, 0.12f), fx, fy, cx, cy); // cabeza
        TexBox(part, new Vector3(-0.055f, 0.31f, 0.01f), new Vector3(0.04f, 0.04f, 0.03f), fx, fy);    // orejas
        TexBox(part, new Vector3(0.055f, 0.31f, 0.01f), new Vector3(0.04f, 0.04f, 0.03f), fx, fy);
        TexBox(part, new Vector3(-0.1f, 0.11f, 0.02f), new Vector3(0.04f, 0.1f, 0.05f), fx, fy);       // brazos
        TexBox(part, new Vector3(0.1f, 0.11f, 0.02f), new Vector3(0.04f, 0.1f, 0.05f), fx, fy);
        TexBox(part, new Vector3(-0.045f, 0.025f, 0.08f), new Vector3(0.055f, 0.05f, 0.09f), fx, fy);  // piernas (sentado)
        TexBox(part, new Vector3(0.045f, 0.025f, 0.08f), new Vector3(0.055f, 0.05f, 0.09f), fx, fy);
        Append(d, part, at, q, scale);
    }

    private static (int x, int y) MaskCell(MaskKind kind) => kind switch
    {
        MaskKind.Gato => (3, 0),
        MaskKind.Oni => (0, 3),
        MaskKind.ZorroNegro => (1, 3),
        _ => (2, 0),
    };

    /// <summary>Una mascara: un frente curvo de tres tiras (mira a +Z) y dos orejas. Centro en 'at'.</summary>
    private static void MaskModel(MeshDraft d, Vector3 at, float scale, MaskKind kind, float yaw = 0f)
    {
        var (mx, my) = MaskCell(kind);
        var part = new MeshDraft();
        const float w = 0.22f, h = 0.26f, bulge = 0.035f;
        float[] xs = { -w / 2f, -w / 6f, w / 6f, w / 2f };
        float[] zs = { 0f, bulge, bulge, 0f };
        for (int i = 0; i < 3; i++)
        {
            Vector3 a = new Vector3(xs[i + 1], -h / 2f, zs[i + 1]), b = new Vector3(xs[i], -h / 2f, zs[i]);
            Vector3 c = new Vector3(xs[i], h / 2f, zs[i]), e = new Vector3(xs[i + 1], h / 2f, zs[i + 1]);
            // Vista de frente: x crece hacia la izquierda del que mira, asi que la u va al reves.
            TexQuad(part, a, b, c, e, mx, my, Vector3.forward, 1f - (i + 1) / 3f, 1f - i / 3f, 0f, 1f);
            TexQuad(part, b, a, e, c, mx, my, Vector3.back, 0.45f, 0.55f, 0.4f, 0.5f); // reves: un color liso
        }
        if (kind != MaskKind.Oni)
            foreach (float sx in new[] { -1f, 1f })
            {
                Vector3 p0 = new Vector3(sx * 0.1f, h / 2f, 0.005f), p1 = new Vector3(sx * 0.03f, h / 2f, 0.02f), p2 = new Vector3(sx * 0.085f, h / 2f + 0.08f, 0.01f);
                float u = sx > 0f ? 0.18f : 0.82f;
                TexTri(part, p0, p1, p2, CellUV(mx, my, u, 0.82f), CellUV(mx, my, u, 0.82f), CellUV(mx, my, u, 0.92f), Vector3.forward);
                TexTri(part, p1, p0, p2, CellUV(mx, my, u, 0.82f), CellUV(mx, my, u, 0.82f), CellUV(mx, my, u, 0.92f), Vector3.back);
            }
        Append(d, part, at, Quaternion.Euler(0f, yaw, 0f), scale);
    }

    /// <summary>El pez en su bolsita: un barril de ocho caras que se angosta arriba en el nudo. Base en 'at'.</summary>
    private static void FishBagModel(MeshDraft d, Vector3 at, float scale)
    {
        var part = new MeshDraft();
        const int sides = 8;
        float[] ys = { 0f, 0.04f, 0.13f, 0.18f, 0.22f };
        float[] rs = { 0.045f, 0.075f, 0.07f, 0.03f, 0.012f };
        for (int ring = 0; ring < ys.Length - 1; ring++)
            for (int s = 0; s < sides; s++)
            {
                float a0 = s * Mathf.PI * 2f / sides, a1 = (s + 1) * Mathf.PI * 2f / sides;
                Vector3 b0 = new Vector3(Mathf.Sin(a0) * rs[ring], ys[ring], Mathf.Cos(a0) * rs[ring]);
                Vector3 b1 = new Vector3(Mathf.Sin(a1) * rs[ring], ys[ring], Mathf.Cos(a1) * rs[ring]);
                Vector3 t1 = new Vector3(Mathf.Sin(a1) * rs[ring + 1], ys[ring + 1], Mathf.Cos(a1) * rs[ring + 1]);
                Vector3 t0 = new Vector3(Mathf.Sin(a0) * rs[ring + 1], ys[ring + 1], Mathf.Cos(a0) * rs[ring + 1]);
                Vector3 outward = new Vector3(Mathf.Sin((a0 + a1) * 0.5f), 0.2f, Mathf.Cos((a0 + a1) * 0.5f));
                bool water = ring < 2;
                // El agua con el pez da la vuelta en dos mitades (el pez se ve de los dos lados).
                float u0 = (s % 4) / 4f, u1 = (s % 4 + 1) / 4f;
                if (water) TexQuad(part, b1, b0, t0, t1, 0, 1, outward, u1, u0, ring == 0 ? 0f : 0.25f, ring == 0 ? 0.25f : 1f);
                else TexQuad(part, b1, b0, t0, t1, 1, 1, outward, u1, u0, ring == 2 ? 0f : 0.5f, ring == 2 ? 0.5f : 1f);
            }
        // El fondo.
        for (int s = 0; s < sides; s++)
        {
            float a0 = s * Mathf.PI * 2f / sides, a1 = (s + 1) * Mathf.PI * 2f / sides;
            TexTri(part, Vector3.zero, new Vector3(Mathf.Sin(a1) * rs[0], 0f, Mathf.Cos(a1) * rs[0]), new Vector3(Mathf.Sin(a0) * rs[0], 0f, Mathf.Cos(a0) * rs[0]),
                CellUV(0, 1, 0.5f, 0.05f), CellUV(0, 1, 0.5f, 0.05f), CellUV(0, 1, 0.5f, 0.05f), Vector3.down);
        }
        Append(d, part, at, Quaternion.identity, scale);
    }

    /// <summary>La bolita de vidrio: una bola de ocho caras, centro en 'at'.</summary>
    private static void MarbleModel(MeshDraft d, Vector3 at, float radius)
    {
        Vector3 up = at + Vector3.up * radius, down = at + Vector3.down * radius;
        Vector3[] ring = { at + Vector3.right * radius, at + Vector3.forward * radius, at + Vector3.left * radius, at + Vector3.back * radius };
        Vector2 top = CellUV(2, 1, 0.35f, 0.7f), bottom = CellUV(2, 1, 0.6f, 0.2f);
        Vector2[] ringUV = { CellUV(2, 1, 0.9f, 0.5f), CellUV(2, 1, 0.5f, 0.9f), CellUV(2, 1, 0.1f, 0.5f), CellUV(2, 1, 0.5f, 0.1f) };
        for (int i = 0; i < 4; i++)
        {
            int j = (i + 1) % 4;
            TexTri(d, up, ring[i], ring[j], top, ringUV[i], ringUV[j], (up + ring[i] + ring[j]) / 3f - at);
            TexTri(d, down, ring[j], ring[i], bottom, ringUV[j], ringUV[i], (down + ring[i] + ring[j]) / 3f - at);
        }
    }

    /// <summary>El papelito de la adivina: dos mitades apenas dobladas, apoyado en 'at'.</summary>
    private static void NoteModel(MeshDraft d, Vector3 at, float yaw)
    {
        var part = new MeshDraft();
        const float w = 0.11f, l = 0.08f, fold = 0.008f;
        Vector3 a = new Vector3(-w / 2f, 0.001f, -l / 2f), b = new Vector3(0f, fold, -l / 2f), c = new Vector3(0f, fold, l / 2f), e = new Vector3(-w / 2f, 0.001f, l / 2f);
        TexQuad(part, a, b, c, e, 3, 1, Vector3.up, 0f, 0.5f, 0f, 1f);
        Vector3 a2 = b, b2 = new Vector3(w / 2f, 0.001f, -l / 2f), c2 = new Vector3(w / 2f, 0.001f, l / 2f), e2 = c;
        TexQuad(part, a2, b2, c2, e2, 3, 1, Vector3.up, 0.5f, 1f, 0f, 1f);
        Append(d, part, at, Quaternion.Euler(0f, yaw, 0f), 1f);
    }

    /// <summary>Copia 'part' en 'd' movida, girada y escalada.</summary>
    private static void Append(MeshDraft d, MeshDraft part, Vector3 at, Quaternion q, float scale)
    {
        int offset = d.v.Count;
        for (int i = 0; i < part.v.Count; i++)
        {
            d.v.Add(at + q * (part.v[i] * scale));
            d.n.Add(q * part.n[i]);
            d.uv.Add(part.uv[i]);
            d.c.Add(i < part.c.Count ? part.c[i] : FairNoWindow);
        }
        foreach (int index in part.t) d.t.Add(index + offset);
    }
}
