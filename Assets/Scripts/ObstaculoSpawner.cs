using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawnea OBSTACULOS por la DERECHA del jugador. Dependen de la zona (y a veces
/// de la subzona) actual del StageManager:
///   - Pool "genericos" (cono, bache, baranda, rampa, barrera, charco) siempre.
///   - Pool por zona (puente_alto / nunoa / cordillera).
///   - Overrides por subzona: p.ej. "nino_bandera" solo aparece en Estadio Nacional.
///
/// Comportamiento por sprite:
///   - Estaticos (infraestructura, objetos abandonados): quedan quietos en el
///     mundo. El jugador avanza hacia ellos y hay que SALTAR para esquivarlos.
///   - Dinamicos (NPCs/animales en movimiento): vienen hacia el jugador en -X.
///     Saltar sobre ellos tambien esquiva (DogRunner chequea player.EnAire).
///
/// Flip (hacia donde apunta el sprite): los sprites del arte base vienen con
/// orientaciones mixtas. El spawner los flipea segun "nombresMiranDerecha" para
/// que todos los dinamicos terminen apuntando a la IZQUIERDA (hacia el jugador).
/// </summary>
public class ObstaculoSpawner : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform jugador;
    [SerializeField] private StageManager stageManager;

    [Header("Sprites de obstaculos (pool por zona)")]
    [SerializeField] private Sprite[] genericos;
    [SerializeField] private Sprite[] puenteAlto;
    [SerializeField] private Sprite[] nunoa;
    [SerializeField] private Sprite[] cordillera;

    [Header("Spawn")]
    [Tooltip("Distancia a la derecha del jugador donde aparece el obstaculo.")]
    [SerializeField] private float distanciaSpawnX = 14f;
    [Tooltip("Y mundo (= alturaSuelo del player).")]
    [SerializeField] private float ySpawn = -2f;
    [SerializeField] private float zSpawn = -0.05f;

    [Header("Dificultad por zona")]
    [SerializeField] private int zonaMinima = 0;
    [SerializeField] private float intervaloInicio = 3.5f;
    [SerializeField] private float intervaloFinal = 1.2f;
    [SerializeField] private float velocidadInicio = 5.5f;
    [SerializeField] private float velocidadFinal = 7.5f;
    [SerializeField] private float jitterIntervalo = 0.5f;

    [Header("Visual / Colision")]
    // El Ollie del player levanta los pies ~1.76 u (gravedad -25, duracionOllie 0.6 + margen 0.15).
    // Los sprites de infraestructura se escalan para quedar bajo ese techo pero cerca,
    // obligando a saltar a tiempo. Las personas quedan a tamano "normal" (no se saltan,
    // vienen caminando y pasan).
    [Tooltip("Altura objetivo en unidades para infraestructura estatica (cono, barrera, carrito...). Pensada para ser saltable con Ollie.")]
    [SerializeField] private float alturaObjetivo = 1.9f;
    [Tooltip("Altura objetivo para personas que caminan (NPCs dinamicos a pie).")]
    [SerializeField] private float alturaPersonas = 2.1f;
    [Tooltip("Altura objetivo para sprites 'bajos' (baches, charcos, hoyos en el piso).")]
    [SerializeField] private float alturaBajos = 0.35f;
    [Tooltip("Y spawn para sprites 'bajos' (debajo del suelo del player, para que aparezcan en la calle).")]
    [SerializeField] private float yOffsetBajos = -0.25f;
    [Tooltip("Altura objetivo para sprites 'pequenos' (perros chicos, cabras, animales).")]
    [SerializeField] private float alturaPequenos = 0.95f;
    [Tooltip("Altura objetivo para sprites 'aereos' (condor, ave).")]
    [SerializeField] private float alturaAereos = 1.3f;
    [Tooltip("Y spawn para sprites 'aereos' (en el cielo, muy por encima del player).")]
    [SerializeField] private float yOffsetAereos = 3.2f;
    [Tooltip("Altura objetivo para sprites 'grandes' (huaso a caballo, scooter con rider).")]
    [SerializeField] private float alturaGrandes = 1.95f;
    [Tooltip("Altura objetivo para sprites 'muy grandes' (carrito de completos, puestos con techo). Requiere doble salto.")]
    [SerializeField] private float alturaMuyGrandes = 2.0f;
    [Tooltip("Altura objetivo para sprites 'extra grandes' (huaso a caballo, ciclista). Los mas imponentes del set.")]
    [SerializeField] private float alturaExtraGrandes = 2.35f;
    [SerializeField] private float umbralAlcance = 0.45f;
    [SerializeField] private int sortingOrder = 6;

    [Header("Clasificacion por nombre de sprite")]
    [Tooltip("Nombres que SE MUEVEN hacia el jugador. El resto queda quieto y hay que saltar.")]
    [SerializeField] private string[] nombresDinamicos;
    [Tooltip("Nombres que en el arte miran a la DERECHA. Se flipean para que apunten al jugador.")]
    [SerializeField] private string[] nombresMiranDerecha;
    [Tooltip("Nombres restringidos a UNA subzona (p.ej. nino_bandera solo en 'Estadio Nacional'). Formato: 'nombre_sprite|subzona'.")]
    [SerializeField] private string[] restriccionSubzona;
    [Tooltip("Nombres que son 'bajos' (en el piso, planos): usan alturaBajos y yOffsetBajos.")]
    [SerializeField] private string[] nombresBajos;
    [Tooltip("Nombres que son 'pequenos' (animales chicos): usan alturaPequenos en vez de alturaObjetivo.")]
    [SerializeField] private string[] nombresPequenos;
    [Tooltip("Nombres que son 'aereos' (vuelan): usan alturaAereos y yOffsetAereos.")]
    [SerializeField] private string[] nombresAereos;
    [Tooltip("Nombres que son 'grandes' (huaso+caballo, scooter+rider): usan alturaGrandes.")]
    [SerializeField] private string[] nombresGrandes;
    [Tooltip("Nombres que son 'muy grandes' (carrito de completos con techo): usan alturaMuyGrandes.")]
    [SerializeField] private string[] nombresMuyGrandes;
    [Tooltip("Nombres que son 'extra grandes' (huaso a caballo, ciclista): usan alturaExtraGrandes.")]
    [SerializeField] private string[] nombresExtraGrandes;

    private readonly Dictionary<string, string> subzonaRequeridaPorNombre = new();
    private float timerProximoSpawn;

    private void Start()
    {
        if (jugador == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) jugador = go.transform;
        }
        if (stageManager == null) stageManager = FindObjectOfType<StageManager>();

        subzonaRequeridaPorNombre.Clear();
        if (restriccionSubzona != null)
        {
            foreach (var r in restriccionSubzona)
            {
                if (string.IsNullOrEmpty(r)) continue;
                int sep = r.IndexOf('|');
                if (sep <= 0 || sep >= r.Length - 1) continue;
                subzonaRequeridaPorNombre[r.Substring(0, sep)] = r.Substring(sep + 1);
            }
        }

        timerProximoSpawn = intervaloInicio;
    }

    private void Update()
    {
        if (jugador == null) return;
        int idxZona = stageManager != null ? stageManager.IndiceActual : 0;
        if (idxZona < zonaMinima) return;

        timerProximoSpawn -= Time.deltaTime;
        if (timerProximoSpawn > 0f) return;

        Spawnear(idxZona);
        timerProximoSpawn = IntervaloActual(idxZona) + Random.Range(-jitterIntervalo, jitterIntervalo);
        if (timerProximoSpawn < 0.3f) timerProximoSpawn = 0.3f;
    }

    private float Fraccion(int idxZona)
    {
        int total = stageManager != null ? Mathf.Max(1, stageManager.TotalZonas - 1) : 8;
        return Mathf.Clamp01((float)(idxZona - zonaMinima) / Mathf.Max(1, total - zonaMinima));
    }

    private float IntervaloActual(int z) => Mathf.Lerp(intervaloInicio, intervaloFinal, Fraccion(z));
    private float VelocidadActual(int z) => Mathf.Lerp(velocidadInicio, velocidadFinal, Fraccion(z));

    private Sprite ElegirSprite()
    {
        string zonaNombre = "";
        string subzona = "";
        if (stageManager != null && stageManager.ZonaActual != null)
        {
            zonaNombre = stageManager.ZonaActual.zona ?? "";
            subzona = stageManager.ZonaActual.subzona ?? "";
        }

        var pool = new List<Sprite>();
        if (genericos != null) pool.AddRange(genericos);
        Sprite[] especificos = null;
        if (zonaNombre.StartsWith("Puente", System.StringComparison.OrdinalIgnoreCase))
            especificos = puenteAlto;
        else if (zonaNombre.StartsWith("Ñuñoa", System.StringComparison.OrdinalIgnoreCase)
              || zonaNombre.StartsWith("Nunoa", System.StringComparison.OrdinalIgnoreCase))
            especificos = nunoa;
        else if (zonaNombre.StartsWith("Cordillera", System.StringComparison.OrdinalIgnoreCase))
            especificos = cordillera;
        if (especificos != null) pool.AddRange(especificos);

        // Filtrar los que estan restringidos a otra subzona.
        pool.RemoveAll(s =>
        {
            if (s == null) return true;
            if (subzonaRequeridaPorNombre.TryGetValue(s.name, out var requerida))
                return !subzona.Equals(requerida, System.StringComparison.OrdinalIgnoreCase);
            return false;
        });

        if (pool.Count == 0) return null;
        return pool[Random.Range(0, pool.Count)];
    }

    private void Spawnear(int idxZona)
    {
        var sprite = ElegirSprite();
        if (sprite == null) return;

        var go = new GameObject($"Obstaculo_{sprite.name}");

        bool esBajo = nombresBajos != null
            && System.Array.IndexOf(nombresBajos, sprite.name) >= 0;
        bool esPequeno = nombresPequenos != null
            && System.Array.IndexOf(nombresPequenos, sprite.name) >= 0;
        bool esAereo = nombresAereos != null
            && System.Array.IndexOf(nombresAereos, sprite.name) >= 0;
        bool esGrande = nombresGrandes != null
            && System.Array.IndexOf(nombresGrandes, sprite.name) >= 0;
        bool esMuyGrande = nombresMuyGrandes != null
            && System.Array.IndexOf(nombresMuyGrandes, sprite.name) >= 0;
        bool esExtraGrande = nombresExtraGrandes != null
            && System.Array.IndexOf(nombresExtraGrandes, sprite.name) >= 0;

        float yFinal = ySpawn;
        if (esAereo) yFinal = ySpawn + yOffsetAereos;
        else if (esBajo) yFinal = ySpawn + yOffsetBajos;
        go.transform.position = new Vector3(
            jugador.position.x + distanciaSpawnX,
            yFinal,
            zSpawn);

        bool esDinamico = nombresDinamicos != null
            && System.Array.IndexOf(nombresDinamicos, sprite.name) >= 0;
        bool esPersona = esDinamico && !esAereo && !esBajo && !esPequeno
            && !esGrande && !esExtraGrande;

        float alturaDeseada = alturaObjetivo;
        if (esAereo) alturaDeseada = alturaAereos;
        else if (esBajo) alturaDeseada = alturaBajos;
        else if (esPequeno) alturaDeseada = alturaPequenos;
        else if (esExtraGrande) alturaDeseada = alturaExtraGrandes;
        else if (esMuyGrande) alturaDeseada = alturaMuyGrandes;
        else if (esGrande) alturaDeseada = alturaGrandes;
        else if (esPersona) alturaDeseada = alturaPersonas;

        float alturaEnMundo = sprite.bounds.size.y;
        float anchoEnMundo = sprite.bounds.size.x;
        if (alturaEnMundo > 0.01f)
        {
            float escala = alturaDeseada / alturaEnMundo;
            // Personas: si el sprite viene ancho (p.ej. señora + manguera horizontal),
            // escalar solo por altura la hace gigante en X. Tomamos la escala minima
            // entre alto y ancho asi la figura entra en una caja del tamano del proto.
            if (esPersona && anchoEnMundo > 0.01f)
            {
                float escalaW = alturaDeseada / anchoEnMundo;
                if (escalaW < escala) escala = escalaW;
            }
            go.transform.localScale = new Vector3(escala, escala, 1f);
        }

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = sortingOrder;
        // Flip: si el arte original mira a la derecha, lo doy vuelta para que
        // apunte hacia el jugador (que viene por la izquierda).
        bool miraDerecha = nombresMiranDerecha != null
            && System.Array.IndexOf(nombresMiranDerecha, sprite.name) >= 0;
        sr.flipX = miraDerecha;

        // Hitbox real pegada al sprite: PolygonCollider2D se autogenera desde
        // la forma fisica del sprite (contorno de pixeles opacos). DogRunner
        // chequea overlap con el collider del skater via Physics2D.Distance.
        var col = go.AddComponent<PolygonCollider2D>();
        col.isTrigger = true;

        float velocidad = esDinamico ? VelocidadActual(idxZona) : 0f;

        var runner = go.AddComponent<DogRunner>();
        var tipo = typeof(DogRunner);
        var fs = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        tipo.GetField("frames", fs)?.SetValue(runner, new Sprite[] { sprite });
        tipo.GetField("fps", fs)?.SetValue(runner, 1f);
        tipo.GetField("velocidad", fs)?.SetValue(runner, velocidad);
        tipo.GetField("renderer2d", fs)?.SetValue(runner, sr);
        tipo.GetField("esPersecutor", fs)?.SetValue(runner, false);
        tipo.GetField("umbralAlcance", fs)?.SetValue(runner, umbralAlcance);
    }
}
