using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Parte de CorridorTreadmill (Paso 31): dos cosas que necesitan conocer el camino.
///
/// 1. LA FALSA SALIDA. Cuando FalseExit la pide (RequestFalseExit), el proximo cruce que se arme
///    adelante recibe, en uno de sus lados, un ramal recto de falseExitStraights rectos que termina
///    en un callejon sin salida (Fondo). Desde el cruce se ve el fondo del ramal, a unos 10 m: ahi
///    FalseExit pone la luz de feria. El otro lado del cruce sigue normal, asi que el jugador
///    nunca queda encerrado (se revisa con HasEscape antes de cada pieza del ramal).
///    Si algo no entra (otra pieza ocupa el lugar) o el ramal se recicla a medias, el plan se
///    abandona y queda pedido para el proximo cruce.
///
/// 2. LAS ESQUINAS DE ADELANTE (GetMouthsAhead). Las bocas de las esquinas y cruces que llevan
///    hacia adelante por el camino. FigurePresence las usa para asomarse detras de una.
/// </summary>
public partial class CorridorTreadmill
{
    [Header("Falsa salida (Paso 31)")]
    [Tooltip("Rectos entre el cruce y el callejon sin salida. 1 = el fondo queda a unos 10 m del cruce.")]
    [SerializeField, Range(1, 3)] private int falseExitStraights = 1;

    /// <summary>Rectos entre el cruce y el callejon (FalseExit calcula donde queda el cruce).</summary>
    public int FalseExitStraights => falseExitStraights;

    /// <summary>true mientras FalseExit espera que se arme una falsa salida.</summary>
    public bool FalseExitRequested { get; private set; }

    /// <summary>Se dispara cuando se pone el callejon sin salida del final del ramal.</summary>
    public event System.Action<CorridorSegment> FalseExitPlaced;

    private bool falsePlanOpen;      // hay un ramal empezado (todavia sin su callejon)
    private long falseStartKey = -1; // la conexion del cruce donde se ofrecio empezar el ramal

    /// <summary>Lo llama FalseExit: true = quiero una; false = ya no (se cancela lo empezado).</summary>
    public void RequestFalseExit(bool value)
    {
        FalseExitRequested = value;
        if (!value)
        {
            falsePlanOpen = false;
            falseStartKey = -1;
        }
    }

    /// <summary>Se llama desde BuildOptions: agrega con mucho peso la pieza que le toca al ramal.</summary>
    private void AddFalseExitOptions(Node parent, int socket, Transform s)
    {
        falseStartKey = -1;
        if (!FalseExitRequested) return;

        // Ya dentro del ramal: rectos y, al final, el callejon (solo si el jugador sigue teniendo
        // otro camino: con esa conexion cerrada tiene que quedar alguna salida).
        if (parent.falseLeft == 0)
        {
            if (HasEscape(null, parent, socket)) AddIfValid(SegmentKind.Fondo, 0, 5000f, s);
            return;
        }
        if (parent.falseLeft > 0)
        {
            AddIfValid(SegmentKind.Recto, 0, 5000f, s);
            return;
        }

        // Empezar uno: un lado de un cruce a mas de 12 m por el camino, fuera de un bucle. (No se usa
        // IsAhead, el de la salida: mide el avance sobre el eje del laberinto, y despues de un giro el
        // camino va de costado y casi nunca se cumple.) Hacia atras ya no llega: ahi solo hay Fondo.
        if (falsePlanOpen || parent.loop != null || parent.seg.Kind != SegmentKind.Cruce) return;
        float pathDist = parent.dist + parent.seg.GetSocket(socket).localPosition.magnitude;
        if (pathDist < 12f || !HasEscape(null, parent, socket)) return;
        int before = options.Count;
        AddIfValid(SegmentKind.Recto, 0, 5000f, s);
        if (options.Count > before) falseStartKey = Key(parent, socket);
    }

    /// <summary>Se llama desde Place: sigue, termina o abandona el ramal.</summary>
    private void OnPlacedForFalseExit(Node parent, int socket, Node node)
    {
        if (!FalseExitRequested) return;

        // El primer recto del ramal.
        if (falseStartKey >= 0 && Key(parent, socket) == falseStartKey)
        {
            falseStartKey = -1;
            if (node.seg.Kind != SegmentKind.Recto) return;
            node.falseLeft = Mathf.Max(0, falseExitStraights - 1);
            falsePlanOpen = true;
            return;
        }

        if (parent.falseLeft < 0) return;
        int left = parent.falseLeft;
        parent.falseLeft = -1; // el plan pasa a la pieza nueva (o se termina aca)

        if (left > 0 && node.seg.Kind == SegmentKind.Recto)
        {
            node.falseLeft = left - 1;
        }
        else if (left == 0 && node.seg.Kind == SegmentKind.Fondo)
        {
            falsePlanOpen = false;
            FalseExitRequested = false;
            Debug.Log($"[CorridorTreadmill] Falsa salida armada en '{node.seg.name}'.");
            FalseExitPlaced?.Invoke(node.seg);
        }
        else
        {
            falsePlanOpen = false; // entro otra pieza: se abandona, sigue pedida
        }
    }

    /// <summary>El ramal no puede seguir (tapado o reciclado): se abandona, sigue pedida.</summary>
    private void BreakFalsePlan(Node node)
    {
        node.falseLeft = -1;
        falsePlanOpen = false;
    }

    // ------------------------------------------------------------------ esquinas de adelante

    /// <summary>Una boca de una esquina o un cruce: donde esta y hacia donde lleva.</summary>
    public struct Mouth
    {
        public Vector3 position;
        public Vector3 outward;
        public float pathDistance;
    }

    /// <summary>
    /// Las bocas de esquinas y cruces que estan entre minDist y maxDist por el camino y llevan
    /// hacia adelante (a una pieza mas lejana que la propia). Sin las cerradas ni las tapadas.
    /// </summary>
    public void GetMouthsAhead(float minDist, float maxDist, List<Mouth> result)
    {
        result.Clear();
        foreach (Node n in nodes.Values)
        {
            if (n.seg.Kind != SegmentKind.Esquina && n.seg.Kind != SegmentKind.Cruce) continue;
            if (n.dist < minDist || n.dist > maxDist) continue;
            for (int i = 0; i < n.links.Length; i++)
            {
                Node next = n.links[i];
                if (next == null || next.dist <= n.dist || n.seg.IsSocketClosed(i) || next.seg.IsBlocking) continue;
                Transform s = n.seg.GetSocket(i);
                result.Add(new Mouth { position = s.position, outward = s.forward, pathDistance = n.dist });
            }
        }
    }
}
