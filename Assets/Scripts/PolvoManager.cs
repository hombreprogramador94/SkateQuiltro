using UnityEngine;

/// <summary>
/// Nubes de polvo bajo las ruedas. Reproduce la secuencia de sprites de
/// Assets/Art/Effects/Dust al aterrizar y al despegar.
///
/// Usa un pool fijo creado en Awake en vez de Instantiate/Destroy por evento:
/// el jugador salta constantemente y crear GameObjects en cada aterrizaje genera
/// basura que el GC termina cobrando con tirones a mitad de partida.
///
/// Cada instancia recorre los frames, se desvanece y crece un poco, que es lo que
/// lee el ojo como polvo disipandose.
/// </summary>
public class PolvoManager : MonoBehaviour
{
    public static PolvoManager Instance { get; private set; }

    [Header("Sprites")]
    [SerializeField] private Sprite[] frames;

    [Header("Pool")]
    [Tooltip("Nubes simultaneas. Con mas de esto, la mas vieja se reutiliza.")]
    [SerializeField] private int cantidadPool = 6;

    [Header("Animacion")]
    [Tooltip("Cuanto dura una nube, en segundos.")]
    [SerializeField] private float duracion = 0.45f;
    [Tooltip("Altura de la nube en unidades de mundo.")]
    [SerializeField] private float alturaNube = 0.55f;
    [Tooltip("Cuanto crece la nube mientras se disipa (1.4 = +40%).")]
    [SerializeField] private float crecimiento = 1.45f;
    [Tooltip("Desplazamiento horizontal por segundo, hacia atras del jugador.")]
    [SerializeField] private float deriva = -1.2f;
    [SerializeField] private int sortingOrder = 7;

    private class Nube
    {
        public GameObject go;
        public SpriteRenderer sr;
        public float t;
        public bool activa;
        public float escalaBase;
        public float fuerza;
    }

    private Nube[] pool;
    private int siguiente;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (frames == null || frames.Length == 0) return;

        // Escala para que el sprite mida alturaNube en el mundo.
        float alto = frames[0].bounds.size.y;
        float escala = alto > 0.01f ? alturaNube / alto : 1f;

        pool = new Nube[Mathf.Max(1, cantidadPool)];
        for (int i = 0; i < pool.Length; i++)
        {
            var go = new GameObject($"Polvo_{i}");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0];
            sr.sortingOrder = sortingOrder;
            go.SetActive(false);
            pool[i] = new Nube { go = go, sr = sr, escalaBase = escala };
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Suelta una nube en el mundo. fuerza escala el tamano y la opacidad: un
    /// aterrizaje fuerte levanta mas polvo que un despegue.
    /// </summary>
    public void Emitir(Vector3 posicion, float fuerza = 1f)
    {
        if (pool == null || frames == null || frames.Length == 0) return;

        var n = pool[siguiente];
        siguiente = (siguiente + 1) % pool.Length;

        n.go.transform.position = posicion;
        n.t = 0f;
        n.fuerza = Mathf.Clamp(fuerza, 0.3f, 2f);
        n.activa = true;
        n.sr.sprite = frames[0];
        // Se voltea al azar para que dos nubes seguidas no salgan calcadas.
        n.sr.flipX = Random.value > 0.5f;
        n.go.transform.localScale = Vector3.one * (n.escalaBase * n.fuerza);
        n.go.SetActive(true);
    }

    private void Update()
    {
        if (pool == null) return;

        for (int i = 0; i < pool.Length; i++)
        {
            var n = pool[i];
            if (!n.activa) continue;

            n.t += Time.deltaTime;
            float u = n.t / Mathf.Max(0.01f, duracion);
            if (u >= 1f)
            {
                n.activa = false;
                n.go.SetActive(false);
                continue;
            }

            // Frame segun el avance, sin repetir el ultimo de golpe.
            int idx = Mathf.Min(frames.Length - 1, (int)(u * frames.Length));
            n.sr.sprite = frames[idx];

            // Crece y se desvanece. El alpha cae mas rapido al final.
            float escala = n.escalaBase * n.fuerza * Mathf.Lerp(1f, crecimiento, u);
            n.go.transform.localScale = new Vector3(escala, escala, 1f);

            var c = n.sr.color;
            c.a = Mathf.Clamp01((1f - u * u) * n.fuerza);
            n.sr.color = c;

            // Deriva hacia atras: el polvo queda donde se levanto, no sigue al player.
            n.go.transform.position += new Vector3(deriva * Time.deltaTime, 0f, 0f);
        }
    }
}
