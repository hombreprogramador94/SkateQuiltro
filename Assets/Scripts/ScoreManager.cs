using System;
using UnityEngine;

/// <summary>
/// Singleton de escena. Lleva puntaje, combo y multiplicador.
///   - Ollie = 100, Kickflip = 300, 360 Flip = 500.
///   - Combo: si pasan < ventanaCombo seg entre dos trucos completados, el
///     multiplicador sube (max x5). Si no se encadena, decae a x1.
///   - Caerse resetea combo + multiplicador (NO el puntaje total).
///
/// Dispara eventos para que HUD / StageManager escuchen sin acoplarse.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("Puntos por truco")]
    [SerializeField] private int puntosOllie = 100;
    [SerializeField] private int puntosKickflip = 300;
    [SerializeField] private int puntos360 = 500;

    [Header("Combo")]
    [Tooltip("Segundos permitidos entre dos trucos para que el combo siga.")]
    [SerializeField] private float ventanaCombo = 2f;
    [Tooltip("Multiplicador maximo del combo.")]
    [SerializeField] private int multiplicadorMax = 5;

    public int Puntaje { get; private set; }
    public int Multiplicador { get; private set; } = 1;
    public float TiempoRestanteCombo { get; private set; }

    public event Action<int> OnPuntajeCambio;              // (nuevo puntaje total)
    public event Action<string, int, int> OnTrucoAnotado;  // (nombre, puntos ganados, multiplicador aplicado)
    public event Action<int> OnMultiplicadorCambio;        // (nuevo multiplicador)
    public event Action OnComboCortado;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (Multiplicador <= 1 || TiempoRestanteCombo <= 0f) return;
        TiempoRestanteCombo -= Time.deltaTime;
        if (TiempoRestanteCombo <= 0f)
        {
            TiempoRestanteCombo = 0f;
            CortarCombo();
        }
    }

    public void RegistrarTruco(PlayerController.Truco truco)
    {
        int baseScore = PuntosDe(truco);
        if (baseScore <= 0) return;

        if (TiempoRestanteCombo > 0f && Multiplicador < multiplicadorMax)
        {
            Multiplicador++;
            OnMultiplicadorCambio?.Invoke(Multiplicador);
        }
        TiempoRestanteCombo = ventanaCombo;

        int ganados = baseScore * Multiplicador;
        Puntaje += ganados;
        OnPuntajeCambio?.Invoke(Puntaje);
        OnTrucoAnotado?.Invoke(NombreDe(truco), ganados, Multiplicador);
    }

    public void NotificarCaida()
    {
        CortarCombo();
    }

    private void CortarCombo()
    {
        if (Multiplicador == 1 && TiempoRestanteCombo == 0f) return;
        Multiplicador = 1;
        TiempoRestanteCombo = 0f;
        OnMultiplicadorCambio?.Invoke(Multiplicador);
        OnComboCortado?.Invoke();
    }

    private int PuntosDe(PlayerController.Truco t) => t switch
    {
        PlayerController.Truco.Ollie => puntosOllie,
        PlayerController.Truco.Kickflip => puntosKickflip,
        PlayerController.Truco.Flip360 => puntos360,
        _ => 0,
    };

    private static string NombreDe(PlayerController.Truco t) => t switch
    {
        PlayerController.Truco.Ollie => "OLLIE",
        PlayerController.Truco.Kickflip => "KICKFLIP",
        PlayerController.Truco.Flip360 => "360 FLIP",
        _ => "",
    };
}
