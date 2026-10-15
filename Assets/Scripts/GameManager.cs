using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Orquesta la partida: timer, HUD de puntaje/multiplicador, toast de truco,
/// y pantalla de fin con record (PlayerPrefs).
/// </summary>
public class GameManager : MonoBehaviour
{
    /// <summary>Clave de PlayerPrefs del record. Publica: el menu la lee tambien.</summary>
    public const string RecordKey = "SkateInacap_Record";

    [SerializeField] private string escenaMenu = "Menu";

    [Header("HUD superior")]
    [SerializeField] private Text labelPersonaje;
    [SerializeField] private Text labelTimer;
    [SerializeField] private Text labelPuntaje;
    [SerializeField] private Text labelMultiplicador;
    [SerializeField] private Text labelToastTruco;
    [SerializeField] private CanvasGroup toastGroup;

    [Header("Timer")]
    [SerializeField] private float duracionPartida = 60f;

    [Header("Fin de partida")]
    [SerializeField] private GameObject panelFin;
    [SerializeField] private Text labelFinTitulo;
    [SerializeField] private Image panelFinFondo;

    [Header("Pantallas de resultado (arte de Assets/Art/UI)")]
    [SerializeField] private GameObject pantallaVictoria;
    [SerializeField] private Text vicPuntaje;
    [SerializeField] private Text vicZona;
    [SerializeField] private Text vicRecord;
    [SerializeField] private Text vicNuevoRecord;
    [SerializeField] private Image vicPersonaje;
    [SerializeField] private GameObject pantallaDerrota;
    [SerializeField] private Text derPuntaje;
    [SerializeField] private Text derZona;
    [SerializeField] private Text derRecord;
    [SerializeField] private Text derNuevoRecord;
    [SerializeField] private Image derPersonaje;
    [SerializeField] private Text labelFinPuntaje;
    [SerializeField] private Text labelFinZona;
    [SerializeField] private Text labelFinRecord;
    [SerializeField] private Button btnReintentar;
    [SerializeField] private Button btnMenu;

    [Header("Referencias")]
    [SerializeField] private StageManager stageManager;

    /// <summary>Como termino la partida. Cambia el titulo, el color y el sonido.</summary>
    private enum MotivoFin { SobrevivioElTiempo, LoAlcanzaron }

    private float tiempoRestante;
    private bool jugando = true;
    private Coroutine toastActivo;

    public static GameManager Instance { get; private set; }
    public bool Jugando => jugando;

    /// <summary>Lo invoca un obstaculo (jauria de perros, p.ej.) cuando toca al jugador.</summary>
    public void TerminarPorObstaculo()
    {
        if (!jugando) return;
        jugando = false;
        tiempoRestante = 0f;
        FinDePartida(MotivoFin.LoAlcanzaron);
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        tiempoRestante = duracionPartida;
        RefrescarLabelPersonaje();

        if (labelPuntaje != null) labelPuntaje.text = "0";
        if (labelMultiplicador != null) labelMultiplicador.text = "";
        if (toastGroup != null) toastGroup.alpha = 0f;
        if (panelFin != null) panelFin.SetActive(false);

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnPuntajeCambio += ActualizarPuntaje;
            ScoreManager.Instance.OnMultiplicadorCambio += ActualizarMultiplicador;
            ScoreManager.Instance.OnTrucoAnotado += MostrarToastTruco;
        }

        if (btnReintentar != null) btnReintentar.onClick.AddListener(Reintentar);
        if (btnMenu != null) btnMenu.onClick.AddListener(VolverAlMenu);
    }

    private void OnDestroy()
    {
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnPuntajeCambio -= ActualizarPuntaje;
            ScoreManager.Instance.OnMultiplicadorCambio -= ActualizarMultiplicador;
            ScoreManager.Instance.OnTrucoAnotado -= MostrarToastTruco;
        }
    }

    private void RefrescarLabelPersonaje()
    {
        if (labelPersonaje != null)
            labelPersonaje.text = "Skater: " + GameData.NombrePersonaje;
    }

    private void Update()
    {
        if (!jugando) return;

        tiempoRestante -= Time.deltaTime;
        if (tiempoRestante <= 0f)
        {
            tiempoRestante = 0f;
            jugando = false;
            // Aguanto los 120 s sin que lo alcanzaran: esto es ganar.
            FinDePartida(MotivoFin.SobrevivioElTiempo);
        }

        if (labelTimer != null)
        {
            int segundos = Mathf.CeilToInt(tiempoRestante);
            labelTimer.text = $"{segundos / 60:00}:{segundos % 60:00}";
        }

        if (Input.GetKeyDown(KeyCode.Escape))
            VolverAlMenu();
    }

    private void ActualizarPuntaje(int puntaje)
    {
        if (labelPuntaje != null) labelPuntaje.text = puntaje.ToString("N0");
    }

    private void ActualizarMultiplicador(int mult)
    {
        if (labelMultiplicador == null) return;
        labelMultiplicador.text = mult > 1 ? $"x{mult}" : "";
    }

    private void MostrarToastTruco(string nombre, int ganados, int mult)
    {
        if (labelToastTruco == null || toastGroup == null) return;
        labelToastTruco.text = mult > 1
            ? $"{nombre} +{ganados} x{mult}"
            : $"{nombre} +{ganados}";

        // Color por truco: se reconoce de un vistazo cual salio, sin leer.
        // El multiplicador alto lo empuja hacia el rojo para marcar la racha.
        Color baseColor = ColorDeTruco(nombre);
        if (mult >= 3)
            baseColor = Color.Lerp(baseColor, new Color(1f, 0.35f, 0.25f),
                Mathf.InverseLerp(3f, 5f, mult) * 0.75f);
        labelToastTruco.color = baseColor;

        // Mas combo = texto mas grande, hasta 1.35x.
        float escalaPico = Mathf.Lerp(1.18f, 1.35f, Mathf.InverseLerp(1f, 5f, mult));

        if (toastActivo != null) StopCoroutine(toastActivo);
        toastActivo = StartCoroutine(AnimarToast(escalaPico));
    }

    private static Color ColorDeTruco(string nombre)
    {
        if (string.IsNullOrEmpty(nombre)) return new Color(1f, 0.9f, 0.4f);
        string n = nombre.ToLowerInvariant();
        if (n.Contains("360")) return new Color(0.70f, 0.55f, 1f);    // violeta: el dificil
        if (n.Contains("kick")) return new Color(0.45f, 0.85f, 1f);   // celeste
        return new Color(1f, 0.9f, 0.4f);                              // ambar: ollie
    }

    /// <summary>
    /// Entrada con "punch": el texto aparece grande y se asienta con un rebote
    /// corto. Un fade suave solo no se nota en medio de la accion.
    /// </summary>
    private IEnumerator AnimarToast(float escalaPico)
    {
        var rt = labelToastTruco.rectTransform;
        float fadeIn = 0.09f;
        float asentar = 0.20f;
        float sostenido = 0.95f;
        float fadeOut = 0.38f;

        float t = 0f;
        while (t < fadeIn)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / fadeIn);
            toastGroup.alpha = u;
            rt.localScale = Vector3.one * Mathf.Lerp(0.75f, escalaPico, u);
            yield return null;
        }
        toastGroup.alpha = 1f;

        // Rebote: del pico baja a 1 con un pequeno sobrepaso.
        t = 0f;
        while (t < asentar)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / asentar);
            float rebote = Mathf.Sin(u * Mathf.PI * 1.5f) * (1f - u) * 0.10f;
            rt.localScale = Vector3.one * (Mathf.Lerp(escalaPico, 1f, u) + rebote);
            yield return null;
        }
        rt.localScale = Vector3.one;

        yield return new WaitForSeconds(sostenido);

        t = 0f;
        while (t < fadeOut)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / fadeOut);
            toastGroup.alpha = 1f - u;
            // Sube un poco al desvanecerse, como humo.
            rt.localScale = Vector3.one * (1f + u * 0.08f);
            yield return null;
        }
        toastGroup.alpha = 0f;
        rt.localScale = Vector3.one;
        toastActivo = null;
    }

    private void FinDePartida(MotivoFin motivo)
    {
        bool gano = motivo == MotivoFin.SobrevivioElTiempo;
        int puntaje = ScoreManager.Instance != null ? ScoreManager.Instance.Puntaje : 0;
        int record = PlayerPrefs.GetInt(RecordKey, 0);
        bool nuevoRecord = puntaje > record;
        if (nuevoRecord)
        {
            PlayerPrefs.SetInt(RecordKey, puntaje);
            PlayerPrefs.Save();
            record = puntaje;
        }

        // El sonido va aqui, no al entrar: necesita saber si hubo record.
        // Ganar tambien suena a logro aunque no se batiera el record.
        if (AudioManager.Instance != null) AudioManager.Instance.SonarFin(nuevoRecord || gano);

        string zonaTxt = stageManager != null && stageManager.ZonaActual != null
            ? stageManager.ZonaActual.NombreCompleto
            : "-";

        // Pantallas nuevas con el arte de Assets/Art/UI. El panelFin de texto
        // plano queda solo como respaldo si faltara alguna referencia.
        var pantalla = gano ? pantallaVictoria : pantallaDerrota;
        if (pantalla != null)
        {
            pantalla.SetActive(true);
            var tP = gano ? vicPuntaje : derPuntaje;
            var tZ = gano ? vicZona : derZona;
            var tR = gano ? vicRecord : derRecord;
            var tN = gano ? vicNuevoRecord : derNuevoRecord;
            var im = gano ? vicPersonaje : derPersonaje;

            if (tP != null) tP.text = puntaje.ToString("N0");
            if (tZ != null) tZ.text = (zonaTxt ?? "-").ToUpperInvariant();
            if (tR != null) tR.text = record.ToString("N0");
            if (tN != null) tN.gameObject.SetActive(nuevoRecord);

            // El personaje es el que eligio el jugador: en victoria sale en el
            // aire con la tabla, en derrota agachado.
            var per = GameData.GetPersonajeOrDefault();
            if (im != null && per != null)
            {
                var spr = gano ? per.aireConTabla : per.agachado;
                if (spr != null) { im.sprite = spr; im.enabled = true; }
                else im.enabled = false;
            }
        }
        else if (panelFin != null) panelFin.SetActive(true);

        // Victoria y derrota se leen de un vistazo: titulo, color del titulo y
        // tinte del fondo. Antes ambos casos mostraban "FIN DE PARTIDA" igual,
        // asi que sobrevivir los 120 s no se sentia como haber ganado.
        if (labelFinTitulo != null)
        {
            labelFinTitulo.text = gano ? "¡LO LOGRASTE!" : "TE ALCANZARON";
            labelFinTitulo.color = gano
                ? new Color(0.45f, 1f, 0.55f)
                : new Color(1f, 0.45f, 0.40f);
        }
        if (panelFinFondo != null)
            panelFinFondo.color = gano
                ? new Color(0.03f, 0.16f, 0.06f, 0.80f)
                : new Color(0.16f, 0.03f, 0.03f, 0.80f);

        if (labelFinPuntaje != null) labelFinPuntaje.text = $"Puntaje: {puntaje:N0}";
        if (labelFinZona != null)
            labelFinZona.text = gano
                ? $"Escapaste de la jauria. Llegaste a: {zonaTxt}"
                : $"Te pillaron en: {zonaTxt}";
        if (labelFinRecord != null)
            labelFinRecord.text = nuevoRecord
                ? $"NUEVO RECORD: {record:N0}"
                : $"Record: {record:N0}";
    }

    public void Reintentar()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.SonarClick();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void VolverAlMenu()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.SonarClick();
        SceneManager.LoadScene(escenaMenu);
    }
}
