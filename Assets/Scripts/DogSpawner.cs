using System;
using UnityEngine;

/// <summary>
/// Crea la JAURIA que persigue al jugador al comienzo de la partida. Spawnea N
/// perros a la izquierda del player, separados entre si, cada uno con una
/// variante aleatoria. Luego NO vuelve a spawnear: la amenaza es constante y
/// crece solo porque los perros corren a velocidad fija y el jugador tiene que
/// mantenerse corriendo a la derecha para no ser alcanzado.
///
/// La velocidad escala con el indice de zona del StageManager: en la zona
/// inicial los perros van ligeramente mas lentos que el player, dando chance;
/// en la ultima zona van casi tan rapido, forzando movimiento continuo.
/// </summary>
public class DogSpawner : MonoBehaviour
{
    [Serializable]
    public class Variante
    {
        public string nombre = "perro";
        public Sprite[] frames;
    }

    [Header("Referencias")]
    [SerializeField] private Transform jugador;
    [SerializeField] private StageManager stageManager;

    [Header("Variantes (negro, cafe, blanco)")]
    [SerializeField] private Variante[] variantes;

    [Header("Jauria inicial")]
    [Tooltip("Cuantos perros nacen al Start.")]
    [SerializeField] private int cantidadInicial = 4;
    [Tooltip("Distancia (u) a la izquierda del jugador donde empieza el primer perro.")]
    [SerializeField] private float distanciaInicial = 6f;
    [Tooltip("Separacion horizontal entre perros adyacentes.")]
    [SerializeField] private float separacionEntrePerros = 1.3f;
    [Tooltip("Y mundo donde corren (= alturaSuelo del player).")]
    [SerializeField] private float ySpawn = -2f;
    [Tooltip("Z usada para ordenar (< 0 queda delante del fondo).")]
    [SerializeField] private float zSpawn = -0.05f;

    [Header("Dificultad")]
    [Tooltip("Velocidad en la zona 0 (u/s). Un poco < velocidadMovimiento del player (5).")]
    [SerializeField] private float velocidadInicio = 4.0f;
    [Tooltip("Velocidad en la ultima zona (u/s). Debe quedar bajo 5 (player) o huir es imposible.")]
    [SerializeField] private float velocidadFinal = 4.9f;

    [Header("Animacion")]
    [SerializeField] private float dogFps = 10f;
    [SerializeField] private int sortingOrder = 5;

    private void Start()
    {
        if (jugador == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) jugador = go.transform;
        }
        if (stageManager == null) stageManager = FindObjectOfType<StageManager>();
        if (jugador == null || variantes == null || variantes.Length == 0) return;

        float vel = VelocidadActual();
        for (int i = 0; i < cantidadInicial; i++)
        {
            var v = variantes[UnityEngine.Random.Range(0, variantes.Length)];
            // El primer perro mas cerca, los siguientes mas lejos a su izquierda.
            float offsetX = -distanciaInicial - i * separacionEntrePerros;
            Spawnear(v, offsetX, vel);
        }
    }

    private float FraccionDificultad()
    {
        if (stageManager == null) return 0f;
        int total = Mathf.Max(1, stageManager.TotalZonas - 1);
        return Mathf.Clamp01((float)stageManager.IndiceActual / total);
    }

    private float VelocidadActual() =>
        Mathf.Lerp(velocidadInicio, velocidadFinal, FraccionDificultad());

    private void Spawnear(Variante v, float offsetX, float velocidad)
    {
        if (v == null || v.frames == null || v.frames.Length == 0) return;

        var go = new GameObject($"Dog_{v.nombre}");
        go.transform.position = new Vector3(
            jugador.position.x + offsetX,
            ySpawn,
            zSpawn);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = v.frames[0];
        sr.sortingOrder = sortingOrder;

        var runner = go.AddComponent<DogRunner>();
        var fieldFrames = typeof(DogRunner).GetField("frames",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var fieldFps = typeof(DogRunner).GetField("fps",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var fieldVel = typeof(DogRunner).GetField("velocidad",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var fieldRnd = typeof(DogRunner).GetField("renderer2d",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var fieldPers = typeof(DogRunner).GetField("esPersecutor",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        fieldFrames?.SetValue(runner, v.frames);
        fieldFps?.SetValue(runner, dogFps);
        fieldVel?.SetValue(runner, velocidad);
        fieldRnd?.SetValue(runner, sr);
        fieldPers?.SetValue(runner, true); // jauria trasera
    }
}
