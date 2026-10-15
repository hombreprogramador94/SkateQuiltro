using UnityEngine;

/// <summary>
/// Movimiento horizontal + salto con trucos. La tabla es un GameObject hijo cuya
/// animacion (sprite + escala + leve inclinacion) la maneja este mismo script.
///
/// Reglas de los trucos:
///  - Ollie: solo desde el suelo.
///  - Kickflip y 360 Flip: desde el suelo o desde el aire DESPUES de un Ollie completado.
///  - La velocidad de salto se calcula para que el tiempo en el aire = duracion + margen,
///    asi ajustar la duracion en el Inspector nunca rompe el truco.
///  - Si se aterriza con un truco a medio terminar => Caerse (pose agachado durante
///    duracionCaida, sin control).
/// </summary>
public class PlayerController : MonoBehaviour
{
    public enum Truco { Ninguno, Ollie, Kickflip, Flip360 }

    [Header("Movimiento")]
    [SerializeField] private float velocidadMovimiento = 5f;
    [SerializeField] private float gravedad = -25f;
    [SerializeField] private float alturaSuelo = -2f;
    [Tooltip("Margen de tiempo entre que el truco termina y el jugador aterriza, en segundos.")]
    [SerializeField] private float margenAire = 0.15f;

    [Header("Duracion de trucos (seg)")]
    [SerializeField] private float duracionOllie = 0.6f;
    [SerializeField] private float duracionKickflip = 0.6f;
    [SerializeField] private float duracionFlip360 = 0.9f;

    [Header("Caida (truco mal aterrizado)")]
    [SerializeField] private float duracionCaida = 1f;

    [Header("Trucos encadenados")]
    [Tooltip("Trucos maximos en un mismo salto. 2 = K/L + Espacio + K/L, que es la cadena que cierra bien.")]
    [SerializeField] private int maxTrucosPorSalto = 2;

    [Header("Doble salto")]
    [Tooltip("Si esta activo, un segundo Space en el aire da un impulso extra.")]
    [SerializeField] private bool permitirDobleSalto = true;
    [Tooltip("Impulso vertical (u/s) del doble salto. Menor que el salto inicial (~9.4) para que no sea OP.")]
    [SerializeField] private float velocidadDobleSalto = 7.5f;

    [Header("Ruedas")]
    [Tooltip("Cada cuantas unidades recorridas suena una junta del pavimento.")]
    [SerializeField] private float distanciaEntreJuntas = 1.35f;

    [Header("Animacion de empuje (en suelo moviendose)")]
    [Tooltip("Segundos entre alternancias parado<->impulsando mientras se mueve.")]
    [SerializeField] private float periodoEmpuje = 0.3f;

    [Header("Balanceo idle / inclinacion al moverse")]
    [Tooltip("Amplitud del balanceo vertical (0..1 de la altura del sprite). ~0.02 = 2%.")]
    [SerializeField] private float balanceoAmplitud = 0.02f;
    [Tooltip("Periodo del balanceo (segundos por ciclo).")]
    [SerializeField] private float balanceoPeriodo = 1.5f;
    [Tooltip("Inclinacion Z en grados al avanzar (hacia adelante segun flipX).")]
    [SerializeField] private float inclinacionGrados = 3f;

    [Header("Sombra bajo el jugador")]
    [SerializeField] private Transform sombraTransform;
    [SerializeField] private SpriteRenderer sombraRenderer;
    [Tooltip("Altura (en unidades) a la que la sombra se achica/aclara hasta 0.")]
    [SerializeField] private float alturaMaxSombra = 3f;

    [Header("Sprites del skater (opcional, se autocargan si vacio)")]
    [SerializeField] private SpriteRenderer skaterRenderer;

    [Header("Tabla hija")]
    [SerializeField] private Transform tablaTransform;
    [SerializeField] private SpriteRenderer tablaRenderer;
    [SerializeField] private Sprite tablaSide;
    [SerializeField] private Sprite tablaBottom;
    [SerializeField] private Sprite tablaTop;

    private PersonajeData personaje;
    private float velocidadY;
    private bool enAire;
    private bool haciendoTruco;
    private bool trucoCompletadoEnEsteSalto; // habilita K/L en el aire
    private int trucosEnEsteSalto;           // cuantos van en este salto
    private bool dobleSaltoUsado; // true si ya se gasto el doble salto en este salto
    private float trucoTimer;
    private Truco trucoActual;

    private bool cayendo;
    private float caidaTimer;
    private float distanciaDesdeJunta;
    private float velocidadAlCaer;

    public bool EnAire => enAire;
    public bool Cayendo => cayendo;

    /// <summary>Llamado por DogRunner (u otros obstaculos) al tocar al player en suelo.</summary>
    public void ForzarCaidaPorObstaculo()
    {
        if (cayendo || personaje == null) return;
        Caerse();
    }

    private Vector3 tablaScaleBase;
    private Quaternion tablaRotBase;

    private float empujeTimer;
    private bool empujeFaseImpulsando;

    private Vector3 skaterScaleBase = Vector3.one;
    private float balanceoTimer;

    private Vector3 sombraScaleBase = Vector3.one;
    private Color sombraColorBase = Color.white;
    private float sombraYLocalBase;

    private void Awake()
    {
        if (tablaTransform != null)
        {
            tablaScaleBase = tablaTransform.localScale;
            tablaRotBase = tablaTransform.localRotation;
        }
        if (skaterRenderer != null)
            skaterScaleBase = skaterRenderer.transform.localScale;
        if (sombraTransform != null)
        {
            sombraScaleBase = sombraTransform.localScale;
            sombraYLocalBase = sombraTransform.localPosition.y;
        }
        if (sombraRenderer != null)
            sombraColorBase = sombraRenderer.color;
    }

    private void Start()
    {
        personaje = GameData.GetPersonajeOrDefault();
        PonerPose(personaje != null ? personaje.parado : null);
        if (tablaRenderer != null && tablaSide != null)
            tablaRenderer.sprite = tablaSide;
        SetTablaVisible(false); // skate hijo oculto hasta que haya truco con pose aire_sin_tabla
    }

    private void Update()
    {
        if (personaje == null) return;

        if (cayendo)
        {
            caidaTimer -= Time.deltaTime;
            if (caidaTimer <= 0f)
            {
                cayendo = false;
                PonerPose(personaje.parado);
            }
            return;
        }

        // Movimiento horizontal siempre.
        float dx = Input.GetAxisRaw("Horizontal");
        transform.position += Vector3.right * (dx * velocidadMovimiento * Time.deltaTime);
        if (dx > 0.01f && skaterRenderer != null) skaterRenderer.flipX = false;
        else if (dx < -0.01f && skaterRenderer != null) skaterRenderer.flipX = true;

        // Doble salto: va POR FUERA del gate de haciendoTruco para poder combinar
        // trucos con salto extra (p.ej. L en suelo -> Space en el aire para extender).
        // Solo requiere estar en aire y no haber gastado el salto en este ciclo.
        if (enAire && permitirDobleSalto && !dobleSaltoUsado
            && Input.GetKeyDown(KeyCode.Space))
        {
            velocidadY = velocidadDobleSalto;
            dobleSaltoUsado = true;
            if (AudioManager.Instance != null) AudioManager.Instance.SonarDobleSalto();
        }

        // Input de trucos (no se solapan: durante un truco no se dispara otro).
        if (!haciendoTruco)
        {
            if (!enAire)
            {
                // En suelo: cualquier truco disponible.
                if (Input.GetKeyDown(KeyCode.Space)) IniciarTrucoDesdeSuelo(Truco.Ollie);
                else if (Input.GetKeyDown(KeyCode.K)) IniciarTrucoDesdeSuelo(Truco.Kickflip);
                else if (Input.GetKeyDown(KeyCode.L)) IniciarTrucoDesdeSuelo(Truco.Flip360);
                else PonerPoseSuelo(dx);
            }
            else if (trucoCompletadoEnEsteSalto && trucosEnEsteSalto < maxTrucosPorSalto)
            {
                // En aire tras CUALQUIER truco completado: K/L encadena otro.
                if (Input.GetKeyDown(KeyCode.K)) IniciarTrucoEnAire(Truco.Kickflip);
                else if (Input.GetKeyDown(KeyCode.L)) IniciarTrucoEnAire(Truco.Flip360);
            }
        }

        // Fisica vertical.
        if (enAire)
        {
            velocidadY += gravedad * Time.deltaTime;
            transform.position += Vector3.up * (velocidadY * Time.deltaTime);

            if (transform.position.y <= alturaSuelo)
            {
                var p = transform.position;
                p.y = alturaSuelo;
                transform.position = p;
                enAire = false;
                velocidadAlCaer = velocidadY;
                velocidadY = 0f;

                if (haciendoTruco)
                {
                    Caerse();
                }
                else
                {
                    trucoCompletadoEnEsteSalto = false;
                    dobleSaltoUsado = false;
                    trucosEnEsteSalto = 0;
                    PonerPoseSuelo(dx);
                    if (AudioManager.Instance != null)
                        AudioManager.Instance.SonarAterrizaje();
                    // Polvo del impacto: mas fuerte cuanto mas rapido se cae.
                    if (PolvoManager.Instance != null)
                        PolvoManager.Instance.Emitir(
                            new Vector3(transform.position.x, alturaSuelo, -0.1f),
                            Mathf.Clamp(Mathf.Abs(velocidadAlCaer) / 9f, 0.5f, 1.6f));
                }
            }
        }

        if (haciendoTruco)
            ActualizarAnimacionTruco();

        // Ruedas: en vez de un loop continuo, un golpe por cada junta del
        // pavimento. Se acumula DISTANCIA recorrida (no tiempo), asi la cadencia
        // depende de la velocidad sola. En el aire no suena nada.
        if (!enAire && Mathf.Abs(dx) > 0.01f)
        {
            distanciaDesdeJunta += Mathf.Abs(dx * velocidadMovimiento * Time.deltaTime);
            if (distanciaDesdeJunta >= distanciaEntreJuntas)
            {
                distanciaDesdeJunta -= distanciaEntreJuntas;
                if (AudioManager.Instance != null) AudioManager.Instance.SonarJunta();
            }
        }
        else
        {
            distanciaDesdeJunta = 0f;
        }

        ActualizarSombra();
    }

    private void ActualizarSombra()
    {
        if (sombraTransform == null) return;
        // La sombra se queda pegada al suelo: posiciona en el mundo a alturaSuelo.
        var ps = sombraTransform.position;
        ps.y = alturaSuelo;
        sombraTransform.position = ps;

        float altura = Mathf.Max(0f, transform.position.y - alturaSuelo);
        float t = Mathf.Clamp01(altura / Mathf.Max(0.01f, alturaMaxSombra));
        // En aire -> sombra se achica y aclara.
        float escala = Mathf.Lerp(1f, 0.35f, t);
        sombraTransform.localScale = new Vector3(
            sombraScaleBase.x * escala,
            sombraScaleBase.y * escala,
            sombraScaleBase.z);
        if (sombraRenderer != null)
        {
            var c = sombraColorBase;
            c.a = Mathf.Lerp(sombraColorBase.a, sombraColorBase.a * 0.25f, t);
            sombraRenderer.color = c;
        }
    }

    private void IniciarTrucoDesdeSuelo(Truco t)
    {
        float duracion = DuracionDe(t);
        float tiempoAire = duracion + margenAire;
        velocidadY = -gravedad * tiempoAire / 2f;
        enAire = true;
        trucoCompletadoEnEsteSalto = false;
        dobleSaltoUsado = false;
        trucosEnEsteSalto = 1;
        ResetSkaterTransform(); // sin tilt ni balanceo mientras esta en el aire
        if (AudioManager.Instance != null) AudioManager.Instance.SonarSalto();
        if (PolvoManager.Instance != null)
            PolvoManager.Instance.Emitir(
                new Vector3(transform.position.x, alturaSuelo, -0.1f), 0.7f);
        IniciarTrucoComun(t);
    }

    private void IniciarTrucoEnAire(Truco t)
    {
        trucoCompletadoEnEsteSalto = false;
        trucosEnEsteSalto++;

        // El truco necesita cierto tiempo de vuelo para completarse. Si desde la
        // altura actual no alcanza, se sube velocidadY lo justo para que quepa:
        // es el "tercer salto" de la cadena K/L + Espacio + K/L. Sin esto el
        // truco se iniciaba y el jugador aterrizaba a medias, lo que cuenta como
        // caida y hacia la cadena imposible de cerrar.
        float tiempoNecesario = DuracionDe(t) + margenAire;
        float altura = transform.position.y - alturaSuelo;
        // De h + v*T + g*T^2/2 = 0, despejando la v que da exactamente T de vuelo.
        float vNecesaria = (-altura - 0.5f * gravedad * tiempoNecesario * tiempoNecesario)
                           / tiempoNecesario;
        if (velocidadY < vNecesaria) velocidadY = vNecesaria;

        IniciarTrucoComun(t);
    }

    private void IniciarTrucoComun(Truco t)
    {
        haciendoTruco = true;
        trucoActual = t;
        trucoTimer = 0f;
        if (tablaTransform != null)
        {
            tablaTransform.localScale = tablaScaleBase;
            tablaTransform.localRotation = tablaRotBase;
        }
    }

    private float DuracionDe(Truco t) => t switch
    {
        Truco.Ollie => duracionOllie,
        Truco.Kickflip => duracionKickflip,
        Truco.Flip360 => duracionFlip360,
        _ => 0.6f,
    };

    private void ActualizarAnimacionTruco()
    {
        trucoTimer += Time.deltaTime;
        float duracion = DuracionDe(trucoActual);
        float t = Mathf.Clamp01(trucoTimer / duracion);

        if (trucoActual == Truco.Ollie)
        {
            // Ollie: el personaje usa aire_con_tabla (sprite incluye la tabla), hija oculta.
            PonerPose(personaje.aireConTabla);
            SetTablaVisible(false);
        }
        else if (trucoActual == Truco.Kickflip)
        {
            PonerPose(personaje.aireSinTabla);
            SetTablaVisible(true);
            AnimarTablaKickflip(t);
        }
        else if (trucoActual == Truco.Flip360)
        {
            PonerPose(personaje.aireSinTabla);
            SetTablaVisible(true);
            AnimarTabla360(t);
        }

        if (t >= 1f) CompletarTruco();
    }

    private void AnimarTablaKickflip(float t)
    {
        if (tablaTransform == null || tablaRenderer == null) return;
        float cos = Mathf.Cos(t * Mathf.PI * 2f);
        float sin = Mathf.Sin(t * Mathf.PI * 2f);
        float factor = 1f;

        if (Mathf.Abs(cos) >= 0.5f)
        {
            tablaRenderer.sprite = tablaSide;
            tablaTransform.localScale = new Vector3(tablaScaleBase.x, tablaScaleBase.y * cos, tablaScaleBase.z);
        }
        else
        {
            tablaRenderer.sprite = tablaBottom;
            tablaTransform.localScale = new Vector3(tablaScaleBase.x, tablaScaleBase.y * Mathf.Abs(sin) * factor, tablaScaleBase.z);
        }
        tablaTransform.localRotation = tablaRotBase;
    }

    private void AnimarTabla360(float t)
    {
        if (tablaTransform == null || tablaRenderer == null) return;
        float cos = Mathf.Cos(t * Mathf.PI * 2f);
        float sin = Mathf.Sin(t * Mathf.PI * 2f);
        float factor = 1f;

        // Y gira como el kickflip (mismo sprite + escala Y).
        if (Mathf.Abs(cos) >= 0.5f)
        {
            tablaRenderer.sprite = tablaSide;
            tablaTransform.localScale = new Vector3(tablaScaleBase.x * cos, tablaScaleBase.y * cos, tablaScaleBase.z);
        }
        else
        {
            tablaRenderer.sprite = tablaBottom;
            tablaTransform.localScale = new Vector3(tablaScaleBase.x * cos, tablaScaleBase.y * Mathf.Abs(sin) * factor, tablaScaleBase.z);
        }

        // Pequena inclinacion dinamica (+-10 deg).
        float tiltZ = sin * 10f;
        tablaTransform.localRotation = tablaRotBase * Quaternion.Euler(0f, 0f, tiltZ);
    }

    private void CompletarTruco()
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.RegistrarTruco(trucoActual);
        if (AudioManager.Instance != null)
            AudioManager.Instance.SonarTruco(trucoActual);

        // Cualquier truco completado abre la ventana para encadenar otro. Antes
        // solo lo hacia el Ollie, asi que tras un Kickflip o un 360 no habia forma
        // de seguir la cadena.
        trucoCompletadoEnEsteSalto = true;

        haciendoTruco = false;
        trucoActual = Truco.Ninguno;

        if (tablaTransform != null)
        {
            tablaTransform.localScale = tablaScaleBase;
            tablaTransform.localRotation = tablaRotBase;
        }
        if (tablaRenderer != null && tablaSide != null) tablaRenderer.sprite = tablaSide;
        SetTablaVisible(false); // tras el truco el personaje vuelve a tener skate pintado
        if (enAire) PonerPose(personaje.aireConTabla);
    }

    private void Caerse()
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.NotificarCaida();
        if (AudioManager.Instance != null)
            AudioManager.Instance.SonarCaida();
        if (PolvoManager.Instance != null)
            PolvoManager.Instance.Emitir(
                new Vector3(transform.position.x, alturaSuelo, -0.1f), 1.8f);

        cayendo = true;
        haciendoTruco = false;
        trucoActual = Truco.Ninguno;
        trucoCompletadoEnEsteSalto = false;
        dobleSaltoUsado = false;
        trucosEnEsteSalto = 0;
        caidaTimer = duracionCaida;
        if (tablaTransform != null)
        {
            tablaTransform.localScale = tablaScaleBase;
            tablaTransform.localRotation = tablaRotBase;
        }
        if (tablaRenderer != null && tablaSide != null) tablaRenderer.sprite = tablaSide;
        SetTablaVisible(false); // en caida/agachado el sprite ya trae skate pintado
        ResetSkaterTransform();
        PonerPose(personaje.agachado);
    }

    private void PonerPoseSuelo(float dx)
    {
        if (personaje == null) return;
        SetTablaVisible(false); // en suelo el personaje trae su propio skate pintado
        bool enMovimiento = Mathf.Abs(dx) > 0.01f;
        if (enMovimiento)
        {
            // Ciclo de empuje: alterna parado<->impulsando para simular empuje con el pie.
            empujeTimer += Time.deltaTime;
            if (empujeTimer >= periodoEmpuje)
            {
                empujeTimer = 0f;
                empujeFaseImpulsando = !empujeFaseImpulsando;
            }
            PonerPose(empujeFaseImpulsando ? personaje.impulsando : personaje.parado);
            balanceoTimer = 0f;
        }
        else
        {
            empujeTimer = 0f;
            empujeFaseImpulsando = false;
            PonerPose(personaje.parado);
        }

        ActualizarInclinacionYBalanceo(dx, enMovimiento);
    }

    private void ActualizarInclinacionYBalanceo(float dx, bool enMovimiento)
    {
        if (skaterRenderer == null) return;
        var tr = skaterRenderer.transform;

        if (enMovimiento)
        {
            // Inclinacion hacia adelante segun flipX (si mira izquierda, inclina +Z).
            float dir = skaterRenderer.flipX ? 1f : -1f;
            tr.localRotation = Quaternion.Euler(0f, 0f, inclinacionGrados * dir);
            tr.localScale = skaterScaleBase;
        }
        else
        {
            // Balanceo suave Y (escala) para que no se vea "clavado" en el suelo.
            balanceoTimer += Time.deltaTime;
            float fase = (balanceoTimer / Mathf.Max(0.01f, balanceoPeriodo)) * Mathf.PI * 2f;
            float factor = 1f + Mathf.Sin(fase) * balanceoAmplitud;
            tr.localScale = new Vector3(skaterScaleBase.x, skaterScaleBase.y * factor, skaterScaleBase.z);
            tr.localRotation = Quaternion.identity;
        }
    }

    private void ResetSkaterTransform()
    {
        if (skaterRenderer == null) return;
        skaterRenderer.transform.localScale = skaterScaleBase;
        skaterRenderer.transform.localRotation = Quaternion.identity;
    }

    private void PonerPose(Sprite s)
    {
        if (skaterRenderer != null && s != null) skaterRenderer.sprite = s;
    }

    private void SetTablaVisible(bool visible)
    {
        if (tablaRenderer != null && tablaRenderer.enabled != visible)
            tablaRenderer.enabled = visible;
    }
}
