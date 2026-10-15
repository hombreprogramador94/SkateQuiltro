using UnityEngine;

/// <summary>
/// Mantiene N copias del mismo sprite de fondo alineadas en X. Cuando una queda
/// atras de la camara, la reposiciona a la derecha de la mas adelantada e
/// invierte su flipX respecto al vecino, de modo que el borde derecho de una
/// calza siempre con el borde izquierdo de la siguiente (sin corte visible).
/// Las copias se escalan en Y para cubrir la altura ortografica de la camara.
/// </summary>
public class BackgroundScroller : MonoBehaviour
{
    [SerializeField] private Transform[] fondos;
    [SerializeField] private SpriteRenderer[] renderers;
    [SerializeField] private Transform referencia;
    [SerializeField] private float anchoFondo = 20f;
    [Tooltip("Multiplo del ancho desde el cual el fondo se recicla al frente.")]
    [SerializeField] private float factorReciclar = 1.5f;

    private void Update()
    {
        if (fondos == null || fondos.Length == 0 || referencia == null) return;

        float refX = referencia.position.x;

        for (int i = 0; i < fondos.Length; i++)
        {
            if (fondos[i] == null) continue;
            if (refX - fondos[i].position.x <= anchoFondo * factorReciclar) continue;

            // Busca el fondo mas a la derecha (que no sea este) y se posiciona a su derecha.
            float maxX = float.NegativeInfinity;
            int idxMax = -1;
            for (int j = 0; j < fondos.Length; j++)
            {
                if (j == i || fondos[j] == null) continue;
                if (fondos[j].position.x > maxX)
                {
                    maxX = fondos[j].position.x;
                    idxMax = j;
                }
            }
            if (idxMax == -1) continue;

            fondos[i].position = new Vector3(
                maxX + anchoFondo,
                fondos[i].position.y,
                fondos[i].position.z);

            // Para que el borde calce, el nuevo fondo debe tener flipX opuesto al vecino.
            if (renderers != null && i < renderers.Length && idxMax < renderers.Length
                && renderers[i] != null && renderers[idxMax] != null)
            {
                renderers[i].flipX = !renderers[idxMax].flipX;
            }
        }
    }
}
