using UnityEngine;

/// <summary>
/// Paso 32: dibuja en la vista Scene las marcas "para mas adelante" de la feria (no hace nada en el
/// juego): los dos carriles de la calle (por donde van a pasar los autos), el paseo por el pasillo
/// (por donde va a caminar la gente) y los lugares de los vendedores.
///
/// Cada hijo directo es un grupo de puntos; se unen en el orden de la jerarquia. Para moverlos,
/// mover los objetos vacios en la escena.
///
/// Va en el objeto "ParaMasAdelante" de la escena Feria.
/// </summary>
public class FairMarkers : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        foreach (Transform group in transform)
        {
            bool vendors = group.name.StartsWith("Vendedor");
            Gizmos.color = group.name.StartsWith("Carril") ? new Color(1f, 0.85f, 0.2f)
                : vendors ? new Color(0.4f, 1f, 0.5f) : new Color(0.4f, 0.8f, 1f);
            Vector3? last = null;
            foreach (Transform p in group)
            {
                Gizmos.DrawSphere(p.position + Vector3.up * 0.2f, vendors ? 0.18f : 0.3f);
                if (!vendors && last.HasValue) Gizmos.DrawLine(last.Value + Vector3.up * 0.2f, p.position + Vector3.up * 0.2f);
                last = p.position;
            }
        }
    }
}
