using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Observa ScoreManager.Puntaje y cuando cruza el umbral de la siguiente
/// ZonaData hace:
///   1) fade a negro (fadeDuracion / 2)
///   2) cambia sprites de los 3 fondos + colorCielo de la camara
///   3) fade de vuelta (fadeDuracion / 2)
///   4) muestra un cartel con "Zona - Subzona" durante cartelDuracion seg
///
/// Expone ZonaActual para que el fin de partida pueda mostrarla.
/// </summary>
public class StageManager : MonoBehaviour
{
    [Header("Zonas (en orden de umbral ascendente)")]
    [SerializeField] private ZonaData[] zonas;

    [Header("Fondos de la escena (se les cambia el sprite)")]
    [SerializeField] private SpriteRenderer[] fondosRenderers;

    [Header("Camara (para colorCielo)")]
    [SerializeField] private Camera camara;

    [Header("UI")]
    [Tooltip("Image full-screen negro usado para el fade.")]
    [SerializeField] private Image fadeImage;
    [SerializeField] private Text cartelZona;
    [SerializeField] private CanvasGroup cartelGroup;

    [Header("Timings")]
    [SerializeField] private float fadeDuracion = 0.6f;
    [SerializeField] private float cartelDuracion = 2f;

    public ZonaData ZonaActual { get; private set; }
    public int IndiceActual => indiceActual;
    public int TotalZonas => zonas != null ? zonas.Length : 0;

    public event Action<ZonaData> OnZonaCambio;

    private int indiceActual = -1;
    private bool transicionEnCurso;

    private void Start()
    {
        if (zonas == null || zonas.Length == 0)
        {
            Debug.LogWarning("[StageManager] Sin zonas configuradas.");
            return;
        }
        AplicarZonaInmediata(0);

        if (ScoreManager.Instance != null)
            ScoreManager.Instance.OnPuntajeCambio += OnPuntaje;

        // Cartel inicial: muestra la zona de partida.
        StartCoroutine(MostrarCartel(ZonaActual.NombreCompleto));
    }

    private void OnDestroy()
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.OnPuntajeCambio -= OnPuntaje;
    }

    private void OnPuntaje(int puntaje)
    {
        if (transicionEnCurso) return;
        int nuevoIdx = IndiceSegunPuntaje(puntaje);
        if (nuevoIdx > indiceActual)
            StartCoroutine(TransicionAZona(nuevoIdx));
    }

    private int IndiceSegunPuntaje(int puntaje)
    {
        int idx = 0;
        for (int i = 0; i < zonas.Length; i++)
        {
            if (zonas[i] != null && puntaje >= zonas[i].umbral) idx = i;
        }
        return idx;
    }

    private void AplicarZonaInmediata(int idx)
    {
        indiceActual = idx;
        ZonaActual = zonas[idx];
        AplicarVisuales(ZonaActual);
        OnZonaCambio?.Invoke(ZonaActual);
    }

    private void AplicarVisuales(ZonaData z)
    {
        if (z == null) return;
        if (fondosRenderers != null && z.fondo != null)
        {
            foreach (var r in fondosRenderers)
            {
                if (r != null) r.sprite = z.fondo;
            }
        }
        if (camara != null) camara.backgroundColor = z.colorCielo;
    }

    private IEnumerator TransicionAZona(int nuevoIdx)
    {
        transicionEnCurso = true;
        float t = 0f;
        float mitad = Mathf.Max(0.01f, fadeDuracion * 0.5f);

        // Fade out.
        if (fadeImage != null)
        {
            while (t < mitad)
            {
                t += Time.deltaTime;
                fadeImage.color = new Color(0, 0, 0, Mathf.Clamp01(t / mitad));
                yield return null;
            }
            fadeImage.color = new Color(0, 0, 0, 1f);
        }

        AplicarZonaInmediata(nuevoIdx);

        // Fade in.
        if (fadeImage != null)
        {
            t = 0f;
            while (t < mitad)
            {
                t += Time.deltaTime;
                fadeImage.color = new Color(0, 0, 0, 1f - Mathf.Clamp01(t / mitad));
                yield return null;
            }
            fadeImage.color = new Color(0, 0, 0, 0f);
        }

        transicionEnCurso = false;
        yield return MostrarCartel(ZonaActual.NombreCompleto);
    }

    private IEnumerator MostrarCartel(string texto)
    {
        if (cartelZona == null || cartelGroup == null) yield break;
        cartelZona.text = texto;

        float fadeIn = 0.3f;
        float fadeOut = 0.5f;
        float sostenido = Mathf.Max(0.5f, cartelDuracion - fadeIn - fadeOut);

        float t = 0f;
        while (t < fadeIn)
        {
            t += Time.deltaTime;
            cartelGroup.alpha = Mathf.Clamp01(t / fadeIn);
            yield return null;
        }
        cartelGroup.alpha = 1f;
        yield return new WaitForSeconds(sostenido);
        t = 0f;
        while (t < fadeOut)
        {
            t += Time.deltaTime;
            cartelGroup.alpha = 1f - Mathf.Clamp01(t / fadeOut);
            yield return null;
        }
        cartelGroup.alpha = 0f;
    }
}
