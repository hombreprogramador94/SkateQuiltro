using UnityEngine;

/// <summary>
/// Audio del juego. Los clips viven en Assets/Audio/SFX y Assets/Audio/Music, y
/// SceneSetup los asigna por referencia (mismo patron que los sprites).
///
/// Tres grupos de canales:
///   - pool de AudioSources: los efectos puntuales rotan entre varios sources.
///     Hace falta un pool (y no un solo PlayOneShot) porque el pitch es una
///     propiedad del AudioSource: con un source compartido, variar el pitch de un
///     efecto nuevo desafinaria los que todavia estan sonando.
///   - ruedasSource: loop continuo con fade, segun si el skater va rodando.
///   - musicaSource: loop de fondo (bgm_juego o bgm_menu, lo decide SceneSetup).
///
/// Los efectos que se repiten mucho (trucos, saltos, aterrizajes, perros) salen
/// con una variacion de pitch de +-variacionPitch para que la repeticion no se
/// vuelva mecanica. Los de UI y los de evento unico (zona, fin, record, click)
/// suenan siempre al pitch original.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Volumenes (0..1)")]
    [SerializeField] private float volumenSfx = 0.5f;
    [SerializeField] private float volumenRuedas = 0.28f;
    [SerializeField] private float volumenMusica = 0.62f;
    [Tooltip("Segundos que tarda el loop de ruedas en entrar o salir.")]
    [SerializeField] private float fadeRuedas = 0.12f;

    [Header("Variacion de pitch")]
    [Tooltip("Variacion aleatoria +-fraccion en los efectos repetidos. 0.05 = +-5%.")]
    [SerializeField] private float variacionPitch = 0.05f;

    [Header("Jauria")]
    [Tooltip("Distancia en X bajo la cual se empieza a oir la jauria.")]
    [SerializeField] private float distanciaGrunido = 30f;
    [Tooltip("Distancia en X bajo la cual ladran (peligro inminente).")]
    [SerializeField] private float distanciaLadrido = 5f;
    [Tooltip("Segundos entre grunidos cuando la jauria esta en el borde del rango.")]
    [SerializeField] private float cooldownGrunidoLejos = 4f;
    [Tooltip("Segundos entre grunidos cuando ya viene pegada.")]
    [SerializeField] private float cooldownGrunidoCerca = 1.2f;
    [Tooltip("Minimo entre ladridos de peligro inminente.")]
    [SerializeField] private float cooldownLadrido = 1.5f;

    [Header("SFX - salto y trucos")]
    [SerializeField] private AudioClip sfxOllie;
    [SerializeField] private AudioClip sfxKickflip;
    [SerializeField] private AudioClip sfxFlip360;
    [SerializeField] private AudioClip sfxDobleSalto;
    [SerializeField] private AudioClip sfxAterrizaje;
    [SerializeField] private AudioClip sfxCaida;

    [Header("SFX - progreso")]
    [SerializeField] private AudioClip sfxZona;
    [SerializeField] private AudioClip sfxCombo;
    [SerializeField] private AudioClip sfxComboCortado;
    [SerializeField] private AudioClip sfxEsquivado;
    [SerializeField] private AudioClip sfxFin;
    [SerializeField] private AudioClip sfxRecord;

    [Header("SFX - perros y UI")]
    // Ladrido de la jauria OIDO DE LEJOS: mismo sample que sfxLadrido pero
    // filtrado y con reflexiones, como lo atenuaria la distancia.
    [SerializeField] private AudioClip sfxGrunido;
    [SerializeField] private AudioClip sfxLadrido;
    [SerializeField] private AudioClip sfxClick;

    [Header("Ruedas")]
    [Tooltip("Golpe de la rueda al pasar una junta del pavimento. One-shot, no loop.")]
    [SerializeField] private AudioClip sfxRuedaJunta;

    [Header("Loops")]
    [SerializeField] private AudioClip sfxRuedasLoop;
    [SerializeField] private AudioClip musicaFondo;

    private const int TamanoPool = 8;

    // Ganancia por clip. Los .wav vienen con niveles muy dispares (medidos por
    // RMS: kickflip -25 dB, combo_cortado -10 dB, 15 dB de diferencia), asi que
    // cada uno se compensa hacia ~-17 dB en vez de tocar los archivos.
    private const float GanOllie = 0.80f;
    private const float GanKickflip = 1.26f;
    private const float GanFlip360 = 1.20f;
    private const float GanDobleSalto = 0.95f;
    private const float GanAterrizaje = 1.0f;
    private const float GanCaida = 1.0f;
    private const float GanEsquivado = 1.0f;
    private const float GanGrunido = 0.95f;
    private const float GanLadrido = 0.68f;
    private const float GanCombo = 0.6f;
    private const float GanComboCortado = 0.45f;
    private const float GanZona = 1.0f;
    private const float GanFin = 1.3f;
    private const float GanRecord = 0.5f;
    private const float GanClick = 1.0f;
    // Las juntas suenan ~4 veces por segundo mientras avanzas, asi que van bajas
    // para que no fatiguen el oido.
    private const float GanJunta = 1.0f;

    private AudioSource[] pool;
    private int poolIdx;
    private AudioSource ruedasSource;
    private AudioSource musicaSource;
    // Canal propio para la jauria: el grunido dura 1.2s y en el pool rotatorio
    // se cortaba en seco cuando pasaban 8 efectos cortos por encima.
    private AudioSource perrosSource;

    private bool rodando;
    private float tGrunido;
    private float tLadrido;
    // Distancia del perro MAS CERCANO reportada en el frame. Centralizarlo aqui
    // evita que los 4 perros compitan por el cooldown cada uno por su cuenta.
    private float dxPerroMin = float.MaxValue;

    private StageManager stageSuscrito;
    private ScoreManager scoreSuscrito;
    private int ultimaZonaVista = -1;

    // ----------------------------------------------------------------- ciclo

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        pool = new AudioSource[TamanoPool];
        for (int i = 0; i < TamanoPool; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;
            pool[i] = src;
        }

        perrosSource = gameObject.AddComponent<AudioSource>();
        perrosSource.playOnAwake = false;
        perrosSource.loop = false;
        perrosSource.spatialBlend = 0f;

        ruedasSource = gameObject.AddComponent<AudioSource>();
        ruedasSource.playOnAwake = false;
        ruedasSource.loop = true;
        ruedasSource.spatialBlend = 0f;
        ruedasSource.volume = 0f;
        ruedasSource.clip = sfxRuedasLoop;

        musicaSource = gameObject.AddComponent<AudioSource>();
        musicaSource.playOnAwake = false;
        musicaSource.loop = true;
        musicaSource.spatialBlend = 0f;
        musicaSource.volume = volumenMusica;
        musicaSource.clip = musicaFondo;
    }

    private void Start()
    {
        if (ruedasSource.clip != null) ruedasSource.Play();
        if (musicaSource.clip != null) musicaSource.Play();

        // Zona: se compara el indice para no sonar con la zona inicial, que
        // StageManager aplica en su propio Start (el orden entre Starts no esta
        // garantizado, asi que comparar el indice funciona en ambos casos).
        stageSuscrito = FindObjectOfType<StageManager>();
        if (stageSuscrito != null)
        {
            ultimaZonaVista = stageSuscrito.IndiceActual;
            stageSuscrito.OnZonaCambio += OnZonaCambio;
        }

        scoreSuscrito = ScoreManager.Instance;
        if (scoreSuscrito != null)
        {
            scoreSuscrito.OnMultiplicadorCambio += OnMultiplicador;
            scoreSuscrito.OnComboCortado += OnComboCortado;
        }
    }

    private void OnDestroy()
    {
        if (stageSuscrito != null) stageSuscrito.OnZonaCambio -= OnZonaCambio;
        if (scoreSuscrito != null)
        {
            scoreSuscrito.OnMultiplicadorCambio -= OnMultiplicador;
            scoreSuscrito.OnComboCortado -= OnComboCortado;
        }
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (tGrunido > 0f) tGrunido -= Time.deltaTime;
        if (tLadrido > 0f) tLadrido -= Time.deltaTime;
        ProcesarJauria();

        float objetivo = rodando ? volumenRuedas : 0f;
        float paso = Time.deltaTime / Mathf.Max(0.01f, fadeRuedas) * Mathf.Max(0.01f, volumenRuedas);
        ruedasSource.volume = Mathf.MoveTowards(ruedasSource.volume, objetivo, paso);
    }

    private void OnZonaCambio(ZonaData _)
    {
        int idx = stageSuscrito != null ? stageSuscrito.IndiceActual : 0;
        if (idx == ultimaZonaVista) return;
        ultimaZonaVista = idx;
        Reproducir(sfxZona, GanZona, variar: false);
    }

    private void OnMultiplicador(int mult)
    {
        // Solo al SUBIR a x2 o mas; volver a x1 lo cubre OnComboCortado.
        if (mult > 1) Reproducir(sfxCombo, GanCombo);
    }

    private void OnComboCortado() => Reproducir(sfxComboCortado, GanComboCortado, variar: false);

    // ------------------------------------------------------------ API publica

    /// <summary>Despegue desde el suelo: el pop de la tabla.</summary>
    public void SonarSalto() => Reproducir(sfxOllie, GanOllie);

    public void SonarDobleSalto() => Reproducir(sfxDobleSalto, GanDobleSalto);

    /// <summary>Truco completado en el aire. El Ollie ya sono al despegar.</summary>
    public void SonarTruco(PlayerController.Truco truco)
    {
        switch (truco)
        {
            case PlayerController.Truco.Kickflip:
                Reproducir(sfxKickflip, GanKickflip); break;
            case PlayerController.Truco.Flip360:
                Reproducir(sfxFlip360, GanFlip360); break;
        }
    }

    public void SonarAterrizaje() => Reproducir(sfxAterrizaje, GanAterrizaje);

    public void SonarCaida()
    {
        Reproducir(sfxCaida, GanCaida);
        rodando = false;
        ruedasSource.volume = 0f;
    }

    /// <summary>Un obstaculo frontal paso de largo sin tocar al jugador.</summary>
    public void SonarEsquivado() => Reproducir(sfxEsquivado, GanEsquivado);

    /// <summary>
    /// Lo llama cada perro de la jauria con su distancia en X al jugador. Solo se
    /// guarda la menor: la decision de que suena la toma ProcesarJauria una vez
    /// por frame, no cada perro por separado.
    /// </summary>
    public void ReportarPerro(float dx)
    {
        if (dx < dxPerroMin) dxPerroMin = dx;
    }

    /// <summary>
    /// Audio de la jauria, una vez por frame. Mientras haya un perro dentro de
    /// distanciaGrunido se oye algo: grunidos cada vez mas seguidos y mas fuertes
    /// a medida que se acercan, y ladridos cuando ya estan encima. Antes el rango
    /// era de 15u fijos y la jauria se quedaba muda a los ~18s, cuando el jugador
    /// ya le habia sacado distancia.
    /// </summary>
    private void ProcesarJauria()
    {
        float dx = dxPerroMin;
        dxPerroMin = float.MaxValue; // se vuelve a llenar en el frame siguiente

        if (dx > distanciaGrunido) return;

        // 0 = recien entrando al rango, 1 = pegada al jugador.
        float cerca = 1f - Mathf.Clamp01(
            (dx - distanciaLadrido) / Mathf.Max(0.01f, distanciaGrunido - distanciaLadrido));

        if (dx <= distanciaLadrido)
        {
            if (tLadrido > 0f) return;
            tLadrido = cooldownLadrido;
            ReproducirEnCanal(perrosSource, sfxLadrido, GanLadrido);
            return;
        }

        if (tGrunido > 0f) return;
        tGrunido = Mathf.Lerp(cooldownGrunidoLejos, cooldownGrunidoCerca, cerca);
        ReproducirEnCanal(perrosSource, sfxGrunido, GanGrunido * Mathf.Lerp(0.6f, 1f, cerca));
    }

    public void SonarClick() => Reproducir(sfxClick, GanClick, variar: false);

    /// <summary>
    /// Una rueda pasando una junta del pavimento. Lo dispara PlayerController
    /// cada cierta DISTANCIA recorrida, no cada cierto tiempo, asi la cadencia
    /// sale de la velocidad real y no hay que sincronizar nada.
    /// </summary>
    public void SonarJunta()
    {
        // Variacion extra de volumen: las juntas no son todas iguales y sin esto
        // la repeticion suena a metronomo.
        Reproducir(sfxRuedaJunta, GanJunta * Random.Range(0.75f, 1.15f));
    }

    /// <summary>Fin de partida. Corta la musica y distingue si hubo record nuevo.</summary>
    public void SonarFin(bool nuevoRecord)
    {
        rodando = false;
        ruedasSource.volume = 0f;
        if (musicaSource != null) musicaSource.Stop();
        if (nuevoRecord && sfxRecord != null) Reproducir(sfxRecord, GanRecord, variar: false);
        else Reproducir(sfxFin, GanFin, variar: false);
    }

    /// <summary>Lo llama PlayerController cada frame: true si rueda por el suelo.</summary>
    public void SetRodando(bool activo) => rodando = activo;

    // --------------------------------------------------------------- interno

    /// <summary>
    /// Reproduce en un canal fijo en vez del pool. Para clips largos que no
    /// deben cortarse (grunido, ladrido): el pool los interrumpiria al rotar.
    /// </summary>
    private void ReproducirEnCanal(AudioSource src, AudioClip clip, float volEscala)
    {
        if (src == null || clip == null) return;
        src.Stop();
        src.clip = clip;
        src.volume = Mathf.Clamp01(volumenSfx * volEscala);
        src.pitch = 1f + Random.Range(-variacionPitch, variacionPitch);
        src.Play();
    }

    private void Reproducir(AudioClip clip, float volEscala = 1f, bool variar = true)
    {
        if (clip == null || pool == null) return;

        var src = pool[poolIdx];
        poolIdx = (poolIdx + 1) % pool.Length;

        src.Stop();
        src.clip = clip;
        src.volume = Mathf.Clamp01(volumenSfx * volEscala);
        src.pitch = variar
            ? 1f + Random.Range(-variacionPitch, variacionPitch)
            : 1f;
        src.Play();
    }
}
