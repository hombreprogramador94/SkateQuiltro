using UnityEngine;

/// <summary>
/// Un perro corriendo. Dos modos segun esPersecutor:
///
///  - true  (JAURIA trasera): nace a la IZQUIERDA del jugador, corre en +X,
///          mira a la derecha. Alcanzar al player (brecha pequena) = Game Over.
///
///  - false (OBSTACULO frontal): spawnea a la DERECHA del jugador, corre en -X,
///          mira a la izquierda (flipX). Tocar al player = Game Over.
///          Se autodestruye al pasar bien lejos a la izquierda del player.
///
/// En ambos casos saltar NO te salva: la amenaza es horizontal, no un hueco.
/// </summary>
public class DogRunner : MonoBehaviour
{
    [Header("Animacion")]
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float fps = 10f;
    [SerializeField] private SpriteRenderer renderer2d;

    [Header("Comportamiento")]
    [Tooltip("Velocidad absoluta en u/s. La direccion la define esPersecutor.")]
    [SerializeField] private float velocidad = 4.5f;
    [Tooltip("true: viene de atras (jauria). false: viene de adelante (obstaculo).")]
    [SerializeField] private bool esPersecutor = true;
    [Tooltip("Umbral de proximidad en X para contar como choque.")]
    [SerializeField] private float umbralAlcance = 0.5f;
    [Tooltip("Si se aleja mas que esto del player, se autodestruye.")]
    [SerializeField] private float distanciaDespawn = 20f;

    private PlayerController player;
    private SpriteRenderer playerRenderer;
    private Collider2D playerCollider;
    private Collider2D selfCollider;
    private float animTimer;
    private int frameActual;
    private bool yaGolpeo;
    private bool yaSonoEsquivado;

    private void Start()
    {
        if (renderer2d == null) renderer2d = GetComponent<SpriteRenderer>();
        selfCollider = GetComponent<Collider2D>();
        // El flip lo controla el spawner antes de agregar este componente,
        // porque los sprites tienen orientaciones mixtas (ciclista mira
        // izquierda, nino_bandera mira derecha, etc.).

        var go = GameObject.FindGameObjectWithTag("Player");
        if (go != null)
        {
            player = go.GetComponent<PlayerController>();
            // El SpriteRenderer visible del jugador vive en el hijo "Skater".
            var skater = go.transform.Find("Skater");
            if (skater != null)
            {
                playerRenderer = skater.GetComponent<SpriteRenderer>();
                playerCollider = skater.GetComponent<Collider2D>();
            }
        }
    }

    private void Update()
    {
        float dir = esPersecutor ? 1f : -1f;
        transform.position += new Vector3(dir * velocidad * Time.deltaTime, 0f, 0f);

        if (frames != null && frames.Length > 1 && renderer2d != null)
        {
            animTimer += Time.deltaTime;
            float periodo = 1f / Mathf.Max(0.01f, fps);
            if (animTimer >= periodo)
            {
                animTimer = 0f;
                frameActual = (frameActual + 1) % frames.Length;
                renderer2d.sprite = frames[frameActual];
            }
        }

        if (player == null) return;

        float dx = player.transform.position.x - transform.position.x; // >0 si perro esta atras del player

        // Audio de la jauria: aqui solo se reporta la distancia. Que suena, cuando
        // y a que volumen lo decide AudioManager con el perro mas cercano, una vez
        // por frame.
        if (esPersecutor && !yaGolpeo && AudioManager.Instance != null)
            AudioManager.Instance.ReportarPerro(dx);

        if (!yaGolpeo)
        {
            bool choca;
            if (esPersecutor)
            {
                // Jauria: alcanza desde atras, saltar no te salva.
                choca = dx <= umbralAlcance;
            }
            else
            {
                // Obstaculo frontal: Physics2D.Distance entre dos PolygonCollider2D
                // (ambos autogenerados desde la forma fisica del sprite). isOverlapped
                // es true solo cuando los contornos reales se tocan, no las cajas.
                if (selfCollider != null && playerCollider != null)
                {
                    var d = Physics2D.Distance(selfCollider, playerCollider);
                    choca = d.isValid && d.isOverlapped;
                }
                else
                {
                    // Fallback por si falta algun collider: X + estado de salto.
                    choca = Mathf.Abs(dx) <= umbralAlcance && !player.EnAire;
                }
            }
            if (choca)
            {
                yaGolpeo = true;
                if (player != null) player.ForzarCaidaPorObstaculo();
                if (GameManager.Instance != null) GameManager.Instance.TerminarPorObstaculo();
            }
        }

        // Esquivado: el obstaculo frontal cruzo al jugador (dx pasa a positivo)
        // sin haberlo tocado. Suena una sola vez por obstaculo.
        if (!esPersecutor && !yaGolpeo && !yaSonoEsquivado && dx > 0.6f)
        {
            yaSonoEsquivado = true;
            if (AudioManager.Instance != null) AudioManager.Instance.SonarEsquivado();
        }

        // Despawn de obstaculos que ya pasaron bien lejos.
        if (!esPersecutor && dx > distanciaDespawn)
            Destroy(gameObject);
    }
}
