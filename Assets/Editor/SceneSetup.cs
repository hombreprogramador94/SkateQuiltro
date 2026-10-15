#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Crea los assets de arte (PersonajeData) + las 3 escenas con sprites conectados
/// y las registra en Build Settings.
///   Unity.exe -batchmode -quit -projectPath "..." -executeMethod SceneSetup.CreateAll
/// o menu "Juego/Crear escenas base".
/// </summary>
public static class SceneSetup
{
    private const string ScenesFolder = "Assets/Scenes";
    private const string ResourcesFolder = "Assets/Resources";
    private const string PersonajesFolder = "Assets/Resources/Personajes";
    private const string ZonasFolder = "Assets/Resources/Zonas";
    private const string ArtFolder = "Assets/Art";
    private const string AudioFolder = "Assets/Audio";

    private static readonly string MenuScenePath = $"{ScenesFolder}/Menu.unity";
    private static readonly string SeleccionScenePath = $"{ScenesFolder}/SeleccionPersonaje.unity";
    private static readonly string JuegoScenePath = $"{ScenesFolder}/Juego.unity";

    [MenuItem("Juego/Crear escenas base")]
    public static void CreateAll()
    {
        EnsureFolder(ScenesFolder);
        EnsureFolder(ResourcesFolder);
        EnsureFolder(PersonajesFolder);
        EnsureFolder(ZonasFolder);

        SetupArt();
        // Las ZonaData recien creadas necesitan quedar indexadas para que
        // CargarZonaAssetsOrdenados las encuentre con AssetDatabase.FindAssets.
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        CrearMenu();
        CrearSeleccion();
        CrearJuego();
        RegistrarEnBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SceneSetup] Arte + escenas creadas y registradas en Build Settings.");
    }

    private static void EnsureFolder(string path)
    {
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
    }

    // ============================================================
    // ART: configura PNGs como Sprite y crea los PersonajeData.asset
    // ============================================================
    private static void SetupArt()
    {
        var pngs = Directory.GetFiles(ArtFolder, "*.png", SearchOption.AllDirectories);
        foreach (var p in pngs)
        {
            string unityPath = p.Replace('\\', '/');
            string lower = unityPath.ToLowerInvariant();

            // PPU y pivot segun carpeta (los sprites de personaje son grandes y
            // deben apoyar los pies en el suelo).
            float ppu;
            Vector2 pivot;
            if (lower.Contains("/characters/"))
            {
                ppu = 200f;
                pivot = new Vector2(0.5f, 0f); // bottom-center => pies al pivot
            }
            else if (lower.Contains("/board/"))
            {
                ppu = 200f;
                pivot = new Vector2(0.5f, 1f); // top-center => la tabla cuelga desde arriba
            }
            else if (lower.Contains("/effects/"))
            {
                ppu = 200f;
                pivot = new Vector2(0.5f, 0.5f); // sombra con centro
            }
            else if (lower.Contains("/dogs/"))
            {
                ppu = 200f;
                pivot = new Vector2(0.5f, 0f); // perro apoya patas en el suelo
            }
            else if (lower.Contains("/obstacles/"))
            {
                ppu = 200f;
                pivot = new Vector2(0.5f, 0f); // base al suelo
            }
            else // Backgrounds
            {
                ppu = 100f;
                pivot = new Vector2(0.5f, 0.5f);
            }

            var importer = AssetImporter.GetAtPath(unityPath) as TextureImporter;
            if (importer == null) continue;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            bool dirty = false;
            if (settings.textureType != TextureImporterType.Sprite)
            {
                settings.textureType = TextureImporterType.Sprite;
                dirty = true;
            }
            // Fuerza spriteMode=Single: los .meta heredados del arte viejo venian con
            // spriteMode=Multiple y rects hardcodeados a dimensiones que ya no existen,
            // asi LoadAssetAtPath<Sprite> devolvia null y los SpriteRenderers quedaban vacios.
            if (settings.spriteMode != (int)SpriteImportMode.Single)
            {
                settings.spriteMode = (int)SpriteImportMode.Single;
                dirty = true;
            }
            if (Mathf.Abs(settings.spritePixelsPerUnit - ppu) > 0.01f)
            {
                settings.spritePixelsPerUnit = ppu;
                dirty = true;
            }
            if (settings.spriteAlignment != (int)SpriteAlignment.Custom || settings.spritePivot != pivot)
            {
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot;
                dirty = true;
            }
            // OJO con el orden: estos tres campos viven DENTRO de settings. Si se
            // asignan sobre el importer (importer.mipmapEnabled = ...), el
            // SetTextureSettings(settings) de mas abajo los vuelve a pisar con los
            // valores viejos y el cambio nunca se aplica. Eso dejaba a los perros
            // con el import del arte original.

            // Sin alphaIsTransparency Unity no hace alpha bleeding: los pixeles
            // invisibles conservan color basura y, al filtrar, ese color se mezcla
            // con el borde. Resultado: halos y cuerpos que se ven translucidos.
            if (!settings.alphaIsTransparency)
            {
                settings.alphaIsTransparency = true;
                dirty = true;
            }
            // Mipmaps fuera: al renderizar el sprite mas chico que su textura,
            // el mip promedia el alpha del contorno con el fondo transparente y
            // el cuerpo entero pierde opacidad. Es lo que le pasaba a los perros.
            if (settings.mipmapEnabled)
            {
                settings.mipmapEnabled = false;
                dirty = true;
            }
            if (settings.filterMode != FilterMode.Bilinear)
            {
                settings.filterMode = FilterMode.Bilinear;
                dirty = true;
            }
            if (importer.maxTextureSize < 2048)
            {
                importer.maxTextureSize = 2048;
                dirty = true;
            }
            if (dirty)
            {
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }

        CrearPersonajeAsset("Felipe", "Felipe");
        CrearPersonajeAsset("Daniel", "Daniel");

        CrearZonasAssets();
    }

    private static void CrearZonasAssets()
    {
        // (archivo asset, zona, subzona, umbral, archivo fondo, cielo)
        var defs = new (string file, string zona, string subzona, int umbral, string fondo, Color cielo)[]
        {
            ("Zona01_PuenteAlto_Pasaje", "Puente Alto", "Pasaje", 0,
                "puente_alto_pasaje.png", new Color(0.55f, 0.72f, 0.88f)),
            ("Zona02_PuenteAlto_Feria", "Puente Alto", "Feria", 2500,
                "puente_alto_feria.png", new Color(0.58f, 0.74f, 0.88f)),
            ("Zona03_PuenteAlto_Plaza", "Puente Alto", "Plaza", 5500,
                "puente_alto_plaza.png", new Color(0.60f, 0.76f, 0.90f)),
            ("Zona04_Nunoa_Avenida", "Nunoa", "Avenida", 9000,
                "nunoa_avenida.png", new Color(0.52f, 0.68f, 0.86f)),
            ("Zona05_Nunoa_EstadioNacional", "Nunoa", "Estadio Nacional", 12000,
                "nunoa_estadio_nacional.png", new Color(0.48f, 0.66f, 0.84f)),
            ("Zona06_Nunoa_VillaOlimpica", "Nunoa", "Villa Olimpica", 15500,
                "nunoa_villa_olimpica.png", new Color(0.46f, 0.64f, 0.82f)),
            ("Zona07_Cordillera_CajonDelMaipo", "Cordillera", "Cajon del Maipo", 19500,
                "cordillera_cajon_del_maipo.png", new Color(0.42f, 0.60f, 0.80f)),
            ("Zona08_Cordillera_EmbalseElYeso", "Cordillera", "Embalse El Yeso", 24000,
                "cordillera_embalse_el_yeso.png", new Color(0.38f, 0.56f, 0.78f)),
            ("Zona09_Cordillera_Farellones", "Cordillera", "Farellones", 29000,
                "cordillera_farellones.png", new Color(0.70f, 0.80f, 0.90f)),
        };

        foreach (var d in defs)
        {
            string assetPath = $"{ZonasFolder}/{d.file}.asset";
            var z = AssetDatabase.LoadAssetAtPath<ZonaData>(assetPath);
            if (z == null)
            {
                z = ScriptableObject.CreateInstance<ZonaData>();
                AssetDatabase.CreateAsset(z, assetPath);
            }
            z.zona = d.zona;
            z.subzona = d.subzona;
            z.umbral = d.umbral;
            z.fondo = CargarSprite($"{ArtFolder}/Backgrounds/Zonas/{d.fondo}");
            z.colorCielo = d.cielo;
            EditorUtility.SetDirty(z);
        }
    }

    private static void CrearPersonajeAsset(string carpetaPersonaje, string nombreVisible)
    {
        string assetPath = $"{PersonajesFolder}/{carpetaPersonaje}.asset";
        var personaje = AssetDatabase.LoadAssetAtPath<PersonajeData>(assetPath);
        if (personaje == null)
        {
            personaje = ScriptableObject.CreateInstance<PersonajeData>();
            AssetDatabase.CreateAsset(personaje, assetPath);
        }
        personaje.nombreVisible = nombreVisible;
        personaje.parado = CargarSprite($"{ArtFolder}/Characters/{carpetaPersonaje}/parado.png");
        personaje.impulsando = CargarSprite($"{ArtFolder}/Characters/{carpetaPersonaje}/impulsando.png");
        personaje.agachado = CargarSprite($"{ArtFolder}/Characters/{carpetaPersonaje}/agachado.png");
        personaje.aireConTabla = CargarSprite($"{ArtFolder}/Characters/{carpetaPersonaje}/aire_con_tabla.png");
        personaje.aireSinTabla = CargarSprite($"{ArtFolder}/Characters/{carpetaPersonaje}/aire_sin_tabla.png");
        EditorUtility.SetDirty(personaje);
    }

    private static Sprite CargarSprite(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Font CargarFuenteCondensada()
    {
        var f = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/ArialNarrowBold.ttf");
        return f != null ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    /// <summary>Boton del menu con sprite swap normal / hover / presionado.</summary>
    private static Button CrearBotonSprite(Transform parent, string nombre,
        string etiqueta, Vector2 pos)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var im = go.AddComponent<Image>();
        im.sprite = CargarSprite($"{ArtFolder}/UI/boton_normal.png");
        im.raycastTarget = true;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(572, 132);

        var btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.SpriteSwap;
        btn.targetGraphic = im;
        var st = btn.spriteState;
        st.highlightedSprite = CargarSprite($"{ArtFolder}/UI/boton_hover.png");
        st.pressedSprite = CargarSprite($"{ArtFolder}/UI/boton_presionado.png");
        st.selectedSprite = CargarSprite($"{ArtFolder}/UI/boton_normal.png");
        btn.spriteState = st;

        var txtGO = new GameObject("Text", typeof(RectTransform));
        txtGO.transform.SetParent(go.transform, false);
        var t = txtGO.AddComponent<Text>();
        t.text = etiqueta;
        t.font = CargarFuenteCondensada();
        t.fontSize = 42;
        t.fontStyle = FontStyle.Bold;
        t.color = new Color(0.12f, 0.10f, 0.06f);
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;
        var trt = txtGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        return btn;
    }

    private static AudioClip CargarAudio(string path)
    {
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    /// <summary>
    /// Crea el AudioManager de la escena. En el juego van todos los clips; en los
    /// menus solo el click y la musica de menu, que es lo unico que suena ahi.
    /// </summary>
    private static void CrearAudioManager(bool esJuego)
    {
        var audioGO = new GameObject("AudioManager");
        var am = audioGO.AddComponent<AudioManager>();
        var so = new SerializedObject(am);

        string sfx = $"{AudioFolder}/SFX";
        string music = $"{AudioFolder}/Music";

        so.FindProperty("sfxClick").objectReferenceValue = CargarAudio($"{sfx}/sfx_click.wav");

        if (esJuego)
        {
            so.FindProperty("sfxOllie").objectReferenceValue = CargarAudio($"{sfx}/sfx_ollie.wav");
            so.FindProperty("sfxKickflip").objectReferenceValue = CargarAudio($"{sfx}/sfx_kickflip.wav");
            so.FindProperty("sfxFlip360").objectReferenceValue = CargarAudio($"{sfx}/sfx_flip360.wav");
            so.FindProperty("sfxDobleSalto").objectReferenceValue = CargarAudio($"{sfx}/sfx_doble_salto.wav");
            so.FindProperty("sfxAterrizaje").objectReferenceValue = CargarAudio($"{sfx}/sfx_aterrizaje.wav");
            so.FindProperty("sfxCaida").objectReferenceValue = CargarAudio($"{sfx}/sfx_caida.wav");
            so.FindProperty("sfxZona").objectReferenceValue = CargarAudio($"{sfx}/sfx_zona.wav");
            so.FindProperty("sfxCombo").objectReferenceValue = CargarAudio($"{sfx}/sfx_combo.wav");
            so.FindProperty("sfxComboCortado").objectReferenceValue = CargarAudio($"{sfx}/sfx_combo_cortado.wav");
            so.FindProperty("sfxEsquivado").objectReferenceValue = CargarAudio($"{sfx}/sfx_esquivado.wav");
            so.FindProperty("sfxFin").objectReferenceValue = CargarAudio($"{sfx}/sfx_fin.wav");
            so.FindProperty("sfxRecord").objectReferenceValue = CargarAudio($"{sfx}/sfx_record.wav");
            so.FindProperty("sfxGrunido").objectReferenceValue = CargarAudio($"{sfx}/sfx_grunido.wav");
            so.FindProperty("sfxLadrido").objectReferenceValue = CargarAudio($"{sfx}/sfx_ladrido.wav");
            so.FindProperty("sfxRuedaJunta").objectReferenceValue = CargarAudio($"{sfx}/sfx_rueda_junta.wav");
            // Loop de ruedas DESACTIVADO: el clip sintetizado sonaba a burbujas y
            // el original tampoco servia. Para reactivarlo basta dejar un sample
            // real en Assets/Audio/SFX/sfx_ruedas_loop.wav y descomentar.
            // so.FindProperty("sfxRuedasLoop").objectReferenceValue = CargarAudio($"{sfx}/sfx_ruedas_loop.wav");
            so.FindProperty("musicaFondo").objectReferenceValue = CargarAudio($"{music}/bgm_juego.wav");
        }
        else
        {
            so.FindProperty("musicaFondo").objectReferenceValue = CargarAudio($"{music}/bgm_menu.wav");
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AsignarSpritesDeCarpeta(SerializedObject so, string propiedad, string carpetaUnityPath)
    {
        var prop = so.FindProperty(propiedad);
        if (prop == null)
        {
            Debug.LogWarning($"[SceneSetup] Propiedad '{propiedad}' no existe en {so.targetObject.name}");
            return;
        }
        if (!Directory.Exists(carpetaUnityPath))
        {
            Debug.LogWarning($"[SceneSetup] Carpeta no existe: {carpetaUnityPath}");
            prop.arraySize = 0;
            return;
        }
        var pngs = Directory.GetFiles(carpetaUnityPath, "*.png", SearchOption.TopDirectoryOnly);
        System.Array.Sort(pngs, System.StringComparer.OrdinalIgnoreCase);
        prop.arraySize = pngs.Length;
        for (int i = 0; i < pngs.Length; i++)
        {
            string path = pngs[i].Replace('\\', '/');
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            prop.GetArrayElementAtIndex(i).objectReferenceValue = sprite;
        }
    }

    // ============================================================
    // Escena 1: Menu
    // ============================================================
    private static void CrearMenu()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CrearCamara(new Color(0.08f, 0.08f, 0.12f));
        var canvas = CrearCanvas();
        CrearEventSystem();

        // Fondo: la portada ya trae titulo y botones dibujados, asi que no se
        // crea ningun titulo por codigo. Encima van botones invisibles alineados
        // sobre los dibujados y el record real, que en la imagen estaba fijo.
        var fondoGO = new GameObject("FondoMenu", typeof(RectTransform));
        fondoGO.transform.SetParent(canvas.transform, false);
        var fondoImg = fondoGO.AddComponent<Image>();
        fondoImg.sprite = CargarSprite($"{ArtFolder}/UI/menu_fondo.png");
        fondoImg.type = Image.Type.Simple;
        fondoImg.raycastTarget = false;
        var fondoRT = fondoGO.GetComponent<RectTransform>();
        fondoRT.anchorMin = Vector2.zero;
        fondoRT.anchorMax = Vector2.one;
        fondoRT.offsetMin = Vector2.zero;
        fondoRT.offsetMax = Vector2.zero;

        var managerGO = new GameObject("MenuManager");
        var manager = managerGO.AddComponent<MenuManager>();

        CrearAudioManager(esJuego: false);

        // Logo encima del que trae el fondo, para que el sprite mande.
        var logoGO = new GameObject("Logo", typeof(RectTransform));
        logoGO.transform.SetParent(canvas.transform, false);
        var logoImg = logoGO.AddComponent<Image>();
        logoImg.sprite = CargarSprite($"{ArtFolder}/UI/logo.png");
        logoImg.raycastTarget = false;
        logoImg.preserveAspect = true;
        var logoRT = logoGO.GetComponent<RectTransform>();
        logoRT.anchorMin = logoRT.anchorMax = new Vector2(0.5f, 0.5f);
        logoRT.pivot = new Vector2(0.5f, 0.5f);
        logoRT.anchoredPosition = new Vector2(-450f, 278f);
        logoRT.sizeDelta = new Vector2(860, 475);

        // Record dentro de la etiqueta amarilla, cuyo numero se limpio del fondo.
        var recordGO = new GameObject("LabelRecord", typeof(RectTransform));
        recordGO.transform.SetParent(canvas.transform, false);
        var recordTxt = recordGO.AddComponent<Text>();
        recordTxt.text = "RECORD 0";
        recordTxt.alignment = TextAnchor.MiddleCenter;
        recordTxt.font = CargarFuenteCondensada();
        recordTxt.fontSize = 36;
        recordTxt.fontStyle = FontStyle.Bold;
        recordTxt.color = new Color(0.12f, 0.10f, 0.06f);
        recordTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
        var recRT = recordGO.GetComponent<RectTransform>();
        recRT.anchorMin = recRT.anchorMax = new Vector2(0.5f, 0.5f);
        recRT.pivot = new Vector2(0.5f, 0.5f);
        recRT.anchoredPosition = new Vector2(780f, 467f);
        recRT.sizeDelta = new Vector2(300, 56);

        // Botones reales con sprite swap, tapando los dibujados en el fondo.
        var btnIniciar = CrearBotonSprite(canvas.transform, "BtnIniciar", "INICIAR",
            new Vector2(-566f, -105f));
        var btnSeleccion = CrearBotonSprite(canvas.transform, "BtnSeleccion", "PERSONAJES",
            new Vector2(-566f, -243f));
        var btnSalir = CrearBotonSprite(canvas.transform, "BtnSalir", "SALIR",
            new Vector2(-566f, -381f));

        var menuSO = new SerializedObject(manager);
        menuSO.FindProperty("labelRecord").objectReferenceValue = recordTxt;
        menuSO.ApplyModifiedPropertiesWithoutUndo();

        AgregarListener(btnIniciar, manager, nameof(MenuManager.IniciarJuego));
        AgregarListener(btnSeleccion, manager, nameof(MenuManager.IrASeleccionPersonaje));
        AgregarListener(btnSalir, manager, nameof(MenuManager.Salir));

        EditorSceneManager.SaveScene(scene, MenuScenePath);
    }

    // ============================================================
    // Escena 2: Seleccion de personaje
    // ============================================================
    private static void CrearSeleccion()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CrearCamara(new Color(0.10f, 0.12f, 0.18f));
        var canvas = CrearCanvas();
        CrearEventSystem();

        CrearTitulo(canvas.transform, "ELIGE TU SKATER");

        var selectorGO = new GameObject("CharacterSelector");
        var selector = selectorGO.AddComponent<CharacterSelector>();

        CrearAudioManager(esJuego: false);

        var labelGO = new GameObject("LabelSeleccion", typeof(RectTransform));
        labelGO.transform.SetParent(canvas.transform, false);
        var labelText = labelGO.AddComponent<Text>();
        labelText.text = "Seleccionado: Felipe";
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontSize = 32;
        labelText.color = Color.white;
        var labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchoredPosition = new Vector2(0, 100);
        labelRT.sizeDelta = new Vector2(600, 60);

        var btnFelipe = CrearBoton(canvas.transform, "BtnFelipe", "FELIPE", new Vector2(-180, 0), new Color(0.24f, 0.47f, 0.86f));
        var btnDaniel = CrearBoton(canvas.transform, "BtnDaniel", "DANIEL", new Vector2(180, 0), new Color(0.82f, 0.24f, 0.24f));
        var btnJugar = CrearBoton(canvas.transform, "BtnJugar", "JUGAR", new Vector2(0, -140), new Color(0.24f, 0.70f, 0.35f));
        var btnVolver = CrearBoton(canvas.transform, "BtnVolver", "VOLVER", new Vector2(0, -220), new Color(0.4f, 0.4f, 0.4f));

        AgregarListener(btnFelipe, selector, nameof(CharacterSelector.SeleccionarFelipe));
        AgregarListener(btnDaniel, selector, nameof(CharacterSelector.SeleccionarDaniel));
        AgregarListener(btnJugar, selector, nameof(CharacterSelector.IrAlJuego));
        AgregarListener(btnVolver, selector, nameof(CharacterSelector.VolverAlMenu));

        var selectorSO = new SerializedObject(selector);
        selectorSO.FindProperty("labelSeleccion").objectReferenceValue = labelText;
        selectorSO.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, SeleccionScenePath);
    }

    // ============================================================
    // Escena 3: Juego (gameplay v0.2)
    // ============================================================
    private static void CrearJuego()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // --- Fondo (3 copias con BackgroundScroller + flipX alterno) ---
        // Usa el fondo de la zona inicial (Puente Alto - Pasaje) como referencia
        // de dimensiones; StageManager va intercambiando el sprite al cruzar
        // umbrales. Asumimos que todos los PNG de Zonas tienen el mismo tamano.
        var fondoSprite = CargarSprite($"{ArtFolder}/Backgrounds/Zonas/puente_alto_pasaje.png");
        if (fondoSprite == null)
            fondoSprite = CargarSprite($"{ArtFolder}/Backgrounds/placeholder_ciudad.png");
        float anchoFondo = fondoSprite != null ? fondoSprite.bounds.size.x : 20f;
        float altoFondo = fondoSprite != null ? fondoSprite.bounds.size.y : 10f;

        // --- Camara ---
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        // Ajusta ortho a la mitad de la altura del fondo para que el fondo llene vertical.
        cam.orthographicSize = altoFondo > 0 ? altoFondo / 2f : 5f;
        cam.backgroundColor = new Color(0.45f, 0.65f, 0.85f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        camGO.transform.position = new Vector3(0, 0, -10);
        camGO.AddComponent<AudioListener>();

        var bgRoot = new GameObject("Backgrounds");
        // 3 copias: la camara ortografica vera a lo mas ~1.78*altoFondo de ancho con
        // aspect 16:9, asi que 3 copias de anchoFondo garantizan cubrir pantalla + margen.
        var bgA = CrearFondoSprite(bgRoot.transform, "FondoA", fondoSprite, new Vector3(-anchoFondo, 0, 5), false);
        var bgB = CrearFondoSprite(bgRoot.transform, "FondoB", fondoSprite, new Vector3(0, 0, 5), true);
        var bgC = CrearFondoSprite(bgRoot.transform, "FondoC", fondoSprite, new Vector3(anchoFondo, 0, 5), false);

        // --- Player ---
        var felipe = AssetDatabase.LoadAssetAtPath<PersonajeData>($"{PersonajesFolder}/Felipe.asset");
        var tablaSide = CargarSprite($"{ArtFolder}/Board/side.png");
        var tablaBottom = CargarSprite($"{ArtFolder}/Board/bottom.png");
        var tablaTop = CargarSprite($"{ArtFolder}/Board/top.png");

        // El suelo visual del fondo (vereda, no la calzada) esta aprox. en el 12%
        // inferior. alturaSuelo queda a -3.4 aprox. para fondo de altoFondo=8.96.
        float alturaSuelo = -(altoFondo / 2f) + altoFondo * 0.12f;

        var playerGO = new GameObject("Player");
        playerGO.tag = "Player"; // DogRunner usa FindGameObjectWithTag("Player")
        playerGO.transform.position = new Vector3(0, alturaSuelo, 0);

        // Hijo "Skater": el sprite del personaje va en un hijo propio para poder
        // aplicarle tilt/balanceo sin afectar a Board ni Sombra.
        var skaterGO = new GameObject("Skater");
        skaterGO.transform.SetParent(playerGO.transform, false);
        skaterGO.transform.localPosition = Vector3.zero;
        var skaterSR = skaterGO.AddComponent<SpriteRenderer>();
        skaterSR.sprite = felipe != null ? felipe.parado : null;
        skaterSR.sortingOrder = 10;
        // Hitbox del skater: PolygonCollider2D autogenerado desde el sprite
        // "parado" (silueta real de pixeles). DogRunner lo busca para hacer
        // la deteccion de choque pegada a la forma.
        var skaterCol = skaterGO.AddComponent<PolygonCollider2D>();
        skaterCol.isTrigger = true;

        // Hijo "Sombra": elipse semitransparente que queda a altura del suelo.
        var sombraSprite = CargarSprite($"{ArtFolder}/Effects/shadow.png");
        var sombraGO = new GameObject("Sombra");
        sombraGO.transform.SetParent(playerGO.transform, false);
        sombraGO.transform.localPosition = new Vector3(0, 0, 0.2f);
        sombraGO.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
        var sombraSR = sombraGO.AddComponent<SpriteRenderer>();
        sombraSR.sprite = sombraSprite;
        sombraSR.sortingOrder = 5; // debajo del skater (10) y la tabla (11)

        // Hijo "Board": solo visible durante Kickflip/360 (pose aire_sin_tabla).
        // El resto del tiempo el skater usa sprites que ya traen skate pintado.
        // Con pivot top-center (0.5,1) la tabla cuelga desde su borde superior,
        // asi localPosition.y=0 la deja JUSTO BAJO los pies del personaje.
        var boardGO = new GameObject("Board");
        boardGO.transform.SetParent(playerGO.transform, false);
        // Offset calibrado por _tools/medir_zapatillas.py: durante Kickflip/360
        // la pose activa es aire_sin_tabla, no aire_con_tabla, asi que el Board
        // debe sentarse a la Y de las ZAPATILLAS (+0.045 u arriba del pivot) y
        // no donde estaba la tabla PINTADA del Ollie (+0.38 u, que caia en la
        // cintura y "atravesaba los pies"). Scale 0.4 mantiene el ancho.
        boardGO.transform.localPosition = new Vector3(0f, 0.05f, 0.1f);
        boardGO.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
        var boardSR = boardGO.AddComponent<SpriteRenderer>();
        boardSR.sprite = tablaSide;
        boardSR.sortingOrder = 11; // encima del skater durante el truco
        boardSR.enabled = false;    // oculto por default; PlayerController lo prende

        var player = playerGO.AddComponent<PlayerController>();
        var playerSO = new SerializedObject(player);
        playerSO.FindProperty("skaterRenderer").objectReferenceValue = skaterSR;
        playerSO.FindProperty("tablaTransform").objectReferenceValue = boardGO.transform;
        playerSO.FindProperty("tablaRenderer").objectReferenceValue = boardSR;
        playerSO.FindProperty("tablaSide").objectReferenceValue = tablaSide;
        playerSO.FindProperty("tablaBottom").objectReferenceValue = tablaBottom;
        playerSO.FindProperty("tablaTop").objectReferenceValue = tablaTop;
        playerSO.FindProperty("alturaSuelo").floatValue = alturaSuelo;
        playerSO.FindProperty("sombraTransform").objectReferenceValue = sombraGO.transform;
        playerSO.FindProperty("sombraRenderer").objectReferenceValue = sombraSR;
        playerSO.ApplyModifiedPropertiesWithoutUndo();

        // --- BackgroundScroller (3 copias con flipX alterno) ---
        var scroller = bgRoot.AddComponent<BackgroundScroller>();
        var scrollerSO = new SerializedObject(scroller);
        var fondosProp = scrollerSO.FindProperty("fondos");
        fondosProp.arraySize = 3;
        fondosProp.GetArrayElementAtIndex(0).objectReferenceValue = bgA.transform;
        fondosProp.GetArrayElementAtIndex(1).objectReferenceValue = bgB.transform;
        fondosProp.GetArrayElementAtIndex(2).objectReferenceValue = bgC.transform;
        var renderersProp = scrollerSO.FindProperty("renderers");
        renderersProp.arraySize = 3;
        renderersProp.GetArrayElementAtIndex(0).objectReferenceValue = bgA.GetComponent<SpriteRenderer>();
        renderersProp.GetArrayElementAtIndex(1).objectReferenceValue = bgB.GetComponent<SpriteRenderer>();
        renderersProp.GetArrayElementAtIndex(2).objectReferenceValue = bgC.GetComponent<SpriteRenderer>();
        scrollerSO.FindProperty("referencia").objectReferenceValue = playerGO.transform;
        scrollerSO.FindProperty("anchoFondo").floatValue = anchoFondo;
        scrollerSO.ApplyModifiedPropertiesWithoutUndo();

        // --- CameraFollow ---
        var follow = camGO.AddComponent<CameraFollow>();
        var followSO = new SerializedObject(follow);
        followSO.FindProperty("objetivo").objectReferenceValue = playerGO.transform;
        followSO.ApplyModifiedPropertiesWithoutUndo();

        // --- Score / Stage managers ---
        var scoreGO = new GameObject("ScoreManager");
        var score = scoreGO.AddComponent<ScoreManager>();

        var stageGO = new GameObject("StageManager");
        var stage = stageGO.AddComponent<StageManager>();

        // --- AudioManager (clips de Assets/Audio) ---
        CrearAudioManager(esJuego: true);

        // --- PolvoManager (nubes bajo las ruedas) ---
        var polvoGO = new GameObject("PolvoManager");
        var polvo = polvoGO.AddComponent<PolvoManager>();
        var polvoSO = new SerializedObject(polvo);
        var polvoFramesProp = polvoSO.FindProperty("frames");
        polvoFramesProp.arraySize = 6;
        for (int i = 0; i < 6; i++)
            polvoFramesProp.GetArrayElementAtIndex(i).objectReferenceValue =
                CargarSprite($"{ArtFolder}/Effects/Dust/polvo_{i + 1}.png");
        polvoSO.ApplyModifiedPropertiesWithoutUndo();

        // --- Canvas HUD ---
        var canvas = CrearCanvas();
        CrearEventSystem();

        // Fade overlay (negro full-screen, alpha 0 por default).
        var fadeGO = new GameObject("FadeOverlay", typeof(RectTransform));
        fadeGO.transform.SetParent(canvas.transform, false);
        var fadeImage = fadeGO.AddComponent<Image>();
        fadeImage.color = new Color(0, 0, 0, 0);
        fadeImage.raycastTarget = false;
        var fadeRT = fadeGO.GetComponent<RectTransform>();
        fadeRT.anchorMin = Vector2.zero;
        fadeRT.anchorMax = Vector2.one;
        fadeRT.offsetMin = Vector2.zero;
        fadeRT.offsetMax = Vector2.zero;

        var labelPersonajeGO = new GameObject("LabelPersonaje", typeof(RectTransform));
        labelPersonajeGO.transform.SetParent(canvas.transform, false);
        var labelPersonaje = labelPersonajeGO.AddComponent<Text>();
        labelPersonaje.text = "Skater: Felipe";
        labelPersonaje.alignment = TextAnchor.MiddleLeft;
        labelPersonaje.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelPersonaje.fontSize = 44;
        labelPersonaje.color = Color.white;
        labelPersonaje.fontStyle = FontStyle.Bold;
        AgregarContornoYSombra(labelPersonajeGO);
        var lpRT = labelPersonajeGO.GetComponent<RectTransform>();
        lpRT.anchorMin = new Vector2(0, 1);
        lpRT.anchorMax = new Vector2(0, 1);
        lpRT.pivot = new Vector2(0, 1);
        lpRT.anchoredPosition = new Vector2(30, -30);
        lpRT.sizeDelta = new Vector2(600, 60);

        var labelTimerGO = new GameObject("LabelTimer", typeof(RectTransform));
        labelTimerGO.transform.SetParent(canvas.transform, false);
        var labelTimer = labelTimerGO.AddComponent<Text>();
        labelTimer.text = "02:00";
        labelTimer.alignment = TextAnchor.MiddleCenter;
        labelTimer.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelTimer.fontSize = 56;
        labelTimer.color = Color.white;
        labelTimer.fontStyle = FontStyle.Bold;
        AgregarContornoYSombra(labelTimerGO);
        var ltRT = labelTimerGO.GetComponent<RectTransform>();
        ltRT.anchorMin = new Vector2(0.5f, 1);
        ltRT.anchorMax = new Vector2(0.5f, 1);
        ltRT.pivot = new Vector2(0.5f, 1);
        ltRT.anchoredPosition = new Vector2(0, -25);
        ltRT.sizeDelta = new Vector2(280, 80);

        // Puntaje arriba-derecha.
        var labelPuntajeGO = new GameObject("LabelPuntaje", typeof(RectTransform));
        labelPuntajeGO.transform.SetParent(canvas.transform, false);
        var labelPuntaje = labelPuntajeGO.AddComponent<Text>();
        labelPuntaje.text = "0";
        labelPuntaje.alignment = TextAnchor.MiddleRight;
        labelPuntaje.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelPuntaje.fontSize = 56;
        labelPuntaje.color = Color.white;
        labelPuntaje.fontStyle = FontStyle.Bold;
        AgregarContornoYSombra(labelPuntajeGO);
        var lpuRT = labelPuntajeGO.GetComponent<RectTransform>();
        lpuRT.anchorMin = new Vector2(1, 1);
        lpuRT.anchorMax = new Vector2(1, 1);
        lpuRT.pivot = new Vector2(1, 1);
        lpuRT.anchoredPosition = new Vector2(-30, -25);
        lpuRT.sizeDelta = new Vector2(400, 80);

        // Multiplicador debajo del puntaje.
        var labelMultGO = new GameObject("LabelMultiplicador", typeof(RectTransform));
        labelMultGO.transform.SetParent(canvas.transform, false);
        var labelMult = labelMultGO.AddComponent<Text>();
        labelMult.text = "";
        labelMult.alignment = TextAnchor.MiddleRight;
        labelMult.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelMult.fontSize = 44;
        labelMult.color = new Color(1f, 0.85f, 0.3f);
        labelMult.fontStyle = FontStyle.Bold;
        AgregarContornoYSombra(labelMultGO);
        var lmRT = labelMultGO.GetComponent<RectTransform>();
        lmRT.anchorMin = new Vector2(1, 1);
        lmRT.anchorMax = new Vector2(1, 1);
        lmRT.pivot = new Vector2(1, 1);
        lmRT.anchoredPosition = new Vector2(-30, -100);
        lmRT.sizeDelta = new Vector2(300, 60);

        // Toast de truco (bajo el timer).
        var toastGO = new GameObject("ToastTruco", typeof(RectTransform), typeof(CanvasGroup));
        toastGO.transform.SetParent(canvas.transform, false);
        var toastGroup = toastGO.GetComponent<CanvasGroup>();
        toastGroup.alpha = 0f;
        var toastText = toastGO.AddComponent<Text>();
        toastText.text = "";
        toastText.alignment = TextAnchor.MiddleCenter;
        toastText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        toastText.fontSize = 48;
        toastText.color = new Color(1f, 0.9f, 0.4f);
        toastText.fontStyle = FontStyle.Bold;
        AgregarContornoYSombra(toastGO);
        var tsRT = toastGO.GetComponent<RectTransform>();
        tsRT.anchorMin = new Vector2(0.5f, 1);
        tsRT.anchorMax = new Vector2(0.5f, 1);
        tsRT.pivot = new Vector2(0.5f, 1);
        tsRT.anchoredPosition = new Vector2(0, -140);
        tsRT.sizeDelta = new Vector2(900, 70);

        // Cartel zona (centro, grande, con CanvasGroup para fade).
        var cartelGO = new GameObject("CartelZona", typeof(RectTransform), typeof(CanvasGroup));
        cartelGO.transform.SetParent(canvas.transform, false);
        var cartelGroup = cartelGO.GetComponent<CanvasGroup>();
        cartelGroup.alpha = 0f;
        var cartelText = cartelGO.AddComponent<Text>();
        cartelText.text = "";
        cartelText.alignment = TextAnchor.MiddleCenter;
        cartelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        cartelText.fontSize = 96;
        cartelText.color = Color.white;
        cartelText.fontStyle = FontStyle.Bold;
        AgregarContornoYSombra(cartelGO);
        var czRT = cartelGO.GetComponent<RectTransform>();
        czRT.anchorMin = new Vector2(0.5f, 0.5f);
        czRT.anchorMax = new Vector2(0.5f, 0.5f);
        czRT.pivot = new Vector2(0.5f, 0.5f);
        czRT.anchoredPosition = new Vector2(0, 120);
        czRT.sizeDelta = new Vector2(1600, 150);

        // Panel fin de partida (full screen, inactivo por default).
        var panelFinGO = new GameObject("PanelFin", typeof(RectTransform), typeof(Image));
        panelFinGO.transform.SetParent(canvas.transform, false);
        var panelFinBG = panelFinGO.GetComponent<Image>();
        panelFinBG.color = new Color(0f, 0f, 0f, 0.78f);
        var pfRT = panelFinGO.GetComponent<RectTransform>();
        pfRT.anchorMin = Vector2.zero;
        pfRT.anchorMax = Vector2.one;
        pfRT.offsetMin = Vector2.zero;
        pfRT.offsetMax = Vector2.zero;

        var finTitulo = CrearTitulo(panelFinGO.transform, "FIN DE PARTIDA");

        var finPuntajeGO = new GameObject("FinPuntaje", typeof(RectTransform));
        finPuntajeGO.transform.SetParent(panelFinGO.transform, false);
        var finPuntaje = finPuntajeGO.AddComponent<Text>();
        finPuntaje.text = "Puntaje: 0";
        finPuntaje.alignment = TextAnchor.MiddleCenter;
        finPuntaje.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        finPuntaje.fontSize = 56;
        finPuntaje.color = Color.white;
        finPuntaje.fontStyle = FontStyle.Bold;
        AgregarContornoYSombra(finPuntajeGO);
        var fpRT = finPuntajeGO.GetComponent<RectTransform>();
        fpRT.anchorMin = new Vector2(0.5f, 0.5f);
        fpRT.anchorMax = new Vector2(0.5f, 0.5f);
        fpRT.pivot = new Vector2(0.5f, 0.5f);
        fpRT.anchoredPosition = new Vector2(0, 120);
        fpRT.sizeDelta = new Vector2(900, 80);

        var finZonaGO = new GameObject("FinZona", typeof(RectTransform));
        finZonaGO.transform.SetParent(panelFinGO.transform, false);
        var finZona = finZonaGO.AddComponent<Text>();
        finZona.text = "Zona alcanzada: -";
        finZona.alignment = TextAnchor.MiddleCenter;
        finZona.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        finZona.fontSize = 36;
        finZona.color = new Color(0.85f, 0.9f, 1f);
        AgregarContornoYSombra(finZonaGO);
        var fzRT = finZonaGO.GetComponent<RectTransform>();
        fzRT.anchorMin = new Vector2(0.5f, 0.5f);
        fzRT.anchorMax = new Vector2(0.5f, 0.5f);
        fzRT.pivot = new Vector2(0.5f, 0.5f);
        fzRT.anchoredPosition = new Vector2(0, 40);
        fzRT.sizeDelta = new Vector2(900, 60);

        var finRecordGO = new GameObject("FinRecord", typeof(RectTransform));
        finRecordGO.transform.SetParent(panelFinGO.transform, false);
        var finRecord = finRecordGO.AddComponent<Text>();
        finRecord.text = "Record: 0";
        finRecord.alignment = TextAnchor.MiddleCenter;
        finRecord.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        finRecord.fontSize = 42;
        finRecord.color = new Color(1f, 0.85f, 0.3f);
        finRecord.fontStyle = FontStyle.Bold;
        AgregarContornoYSombra(finRecordGO);
        var frRT = finRecordGO.GetComponent<RectTransform>();
        frRT.anchorMin = new Vector2(0.5f, 0.5f);
        frRT.anchorMax = new Vector2(0.5f, 0.5f);
        frRT.pivot = new Vector2(0.5f, 0.5f);
        frRT.anchoredPosition = new Vector2(0, -30);
        frRT.sizeDelta = new Vector2(900, 70);

        var btnReintentar = CrearBoton(panelFinGO.transform, "BtnReintentar", "JUGAR DE NUEVO",
            new Vector2(0, -150), new Color(0.24f, 0.70f, 0.35f));
        var btnMenu = CrearBoton(panelFinGO.transform, "BtnMenu", "MENU",
            new Vector2(0, -230), new Color(0.4f, 0.4f, 0.4f));
        panelFinGO.SetActive(false);

        // --- Pantallas de victoria / derrota (arte de Assets/Art/UI) ---
        var pantVic = SceneSetupUI.CrearPantalla(canvas.transform, true,
            out var vicP, out var vicZ, out var vicR, out var vicN,
            out var vicPer, out var vicBtnRe, out var vicBtnMenu);
        var pantDer = SceneSetupUI.CrearPantalla(canvas.transform, false,
            out var derP, out var derZ, out var derR, out var derN,
            out var derPer, out var derBtnRe, out var derBtnMenu);

        // --- GameManager ---
        var managerGO = new GameObject("GameManager");
        var manager = managerGO.AddComponent<GameManager>();
        var managerSO = new SerializedObject(manager);
        managerSO.FindProperty("labelPersonaje").objectReferenceValue = labelPersonaje;
        managerSO.FindProperty("labelTimer").objectReferenceValue = labelTimer;
        managerSO.FindProperty("labelPuntaje").objectReferenceValue = labelPuntaje;
        managerSO.FindProperty("labelMultiplicador").objectReferenceValue = labelMult;
        managerSO.FindProperty("labelToastTruco").objectReferenceValue = toastText;
        managerSO.FindProperty("toastGroup").objectReferenceValue = toastGroup;
        managerSO.FindProperty("panelFin").objectReferenceValue = panelFinGO;
        managerSO.FindProperty("pantallaVictoria").objectReferenceValue = pantVic;
        managerSO.FindProperty("vicPuntaje").objectReferenceValue = vicP;
        managerSO.FindProperty("vicZona").objectReferenceValue = vicZ;
        managerSO.FindProperty("vicRecord").objectReferenceValue = vicR;
        managerSO.FindProperty("vicNuevoRecord").objectReferenceValue = vicN;
        managerSO.FindProperty("vicPersonaje").objectReferenceValue = vicPer;
        managerSO.FindProperty("pantallaDerrota").objectReferenceValue = pantDer;
        managerSO.FindProperty("derPuntaje").objectReferenceValue = derP;
        managerSO.FindProperty("derZona").objectReferenceValue = derZ;
        managerSO.FindProperty("derRecord").objectReferenceValue = derR;
        managerSO.FindProperty("derNuevoRecord").objectReferenceValue = derN;
        managerSO.FindProperty("derPersonaje").objectReferenceValue = derPer;
        managerSO.FindProperty("labelFinTitulo").objectReferenceValue = finTitulo;
        managerSO.FindProperty("panelFinFondo").objectReferenceValue = panelFinBG;
        managerSO.FindProperty("labelFinPuntaje").objectReferenceValue = finPuntaje;
        managerSO.FindProperty("labelFinZona").objectReferenceValue = finZona;
        managerSO.FindProperty("labelFinRecord").objectReferenceValue = finRecord;
        managerSO.FindProperty("btnReintentar").objectReferenceValue = btnReintentar;
        managerSO.FindProperty("btnMenu").objectReferenceValue = btnMenu;
        managerSO.FindProperty("stageManager").objectReferenceValue = stage;
        managerSO.ApplyModifiedPropertiesWithoutUndo();

        AgregarListener(vicBtnRe, manager, nameof(GameManager.Reintentar));
        AgregarListener(vicBtnMenu, manager, nameof(GameManager.VolverAlMenu));
        AgregarListener(derBtnRe, manager, nameof(GameManager.Reintentar));
        AgregarListener(derBtnMenu, manager, nameof(GameManager.VolverAlMenu));

        // --- StageManager wiring ---
        var zonaAssets = CargarZonaAssetsOrdenados();
        var stageSO = new SerializedObject(stage);
        var zonasProp = stageSO.FindProperty("zonas");
        zonasProp.arraySize = zonaAssets.Length;
        for (int i = 0; i < zonaAssets.Length; i++)
            zonasProp.GetArrayElementAtIndex(i).objectReferenceValue = zonaAssets[i];
        var fondosRProp = stageSO.FindProperty("fondosRenderers");
        fondosRProp.arraySize = 3;
        fondosRProp.GetArrayElementAtIndex(0).objectReferenceValue = bgA.GetComponent<SpriteRenderer>();
        fondosRProp.GetArrayElementAtIndex(1).objectReferenceValue = bgB.GetComponent<SpriteRenderer>();
        fondosRProp.GetArrayElementAtIndex(2).objectReferenceValue = bgC.GetComponent<SpriteRenderer>();
        stageSO.FindProperty("camara").objectReferenceValue = cam;
        stageSO.FindProperty("fadeImage").objectReferenceValue = fadeImage;
        stageSO.FindProperty("cartelZona").objectReferenceValue = cartelText;
        stageSO.FindProperty("cartelGroup").objectReferenceValue = cartelGroup;
        stageSO.ApplyModifiedPropertiesWithoutUndo();

        // --- DogSpawner (obstaculos perseguidores) ---
        var dogSpawnerGO = new GameObject("DogSpawner");
        var dogSpawner = dogSpawnerGO.AddComponent<DogSpawner>();
        var dogSO = new SerializedObject(dogSpawner);
        dogSO.FindProperty("jugador").objectReferenceValue = playerGO.transform;
        dogSO.FindProperty("stageManager").objectReferenceValue = stage;
        dogSO.FindProperty("ySpawn").floatValue = alturaSuelo;
        // Dificultad de la jauria: en zonas tempranas van un poco mas lento que el
        // player (vel=5), en las ultimas lo superan y hay que estar atento.
        dogSO.FindProperty("velocidadInicio").floatValue = 4.5f;
        dogSO.FindProperty("velocidadFinal").floatValue = 5.6f;

        // "blanco" descartado: sus 4 frames vienen con el cuerpo a ~45% de alpha y
        // con gris de fondo guardado bajo las zonas translucidas, asi que en pantalla
        // se lee como si se viera la calle a traves del perro. Repararlo exigiria
        // rehacer el arte. Los PNG siguen en Assets/Art/Dogs/blanco por si se arregla.
        string[] colores = { "negro", "cafe" };

        var variantesProp = dogSO.FindProperty("variantes");
        variantesProp.arraySize = colores.Length;
        for (int i = 0; i < colores.Length; i++)
        {
            var elem = variantesProp.GetArrayElementAtIndex(i);
            elem.FindPropertyRelative("nombre").stringValue = colores[i];
            var framesProp = elem.FindPropertyRelative("frames");
            framesProp.arraySize = 4;
            for (int f = 0; f < 4; f++)
            {
                var sprite = CargarSprite($"{ArtFolder}/Dogs/{colores[i]}/frame_{f + 1}.png");
                framesProp.GetArrayElementAtIndex(f).objectReferenceValue = sprite;
            }
        }
        dogSO.ApplyModifiedPropertiesWithoutUndo();

        // --- ObstaculoSpawner (obstaculos reales por la derecha, por zona) ---
        var obsGO = new GameObject("ObstaculoSpawner");
        var obs = obsGO.AddComponent<ObstaculoSpawner>();
        var obsSO = new SerializedObject(obs);
        obsSO.FindProperty("jugador").objectReferenceValue = playerGO.transform;
        obsSO.FindProperty("stageManager").objectReferenceValue = stage;
        obsSO.FindProperty("ySpawn").floatValue = alturaSuelo;
        // Las personas (NPCs a pie) se escalan al tamano real del protagonista parado,
        // asi quedan visualmente igual de altas. El sprite esta a scale 1.0 en el player.
        if (felipe != null && felipe.parado != null)
        {
            float alturaProta = felipe.parado.bounds.size.y;
            if (alturaProta > 0.01f)
                obsSO.FindProperty("alturaPersonas").floatValue = alturaProta;
        }
        AsignarSpritesDeCarpeta(obsSO, "genericos", $"{ArtFolder}/Obstacles/genericos");
        AsignarSpritesDeCarpeta(obsSO, "puenteAlto", $"{ArtFolder}/Obstacles/puente_alto");
        AsignarSpritesDeCarpeta(obsSO, "nunoa", $"{ArtFolder}/Obstacles/nunoa");
        AsignarSpritesDeCarpeta(obsSO, "cordillera", $"{ArtFolder}/Obstacles/cordillera");
        // Dinamicos: NPCs/animales que se desplazan hacia el player. El resto
        // (infraestructura y objetos abandonados: cono, carrito, mesa, roca, nieve, charco...)
        // queda quieto y hay que saltar sobre ellos.
        var dinaProp = obsSO.FindProperty("nombresDinamicos");
        string[] dinamicos = new[]
        {
            "quiltro", "senor_chaleco", "carretero_fruta",
            "ciclista", "nino_bandera", "paseador_poodle",
            "huaso", "biker_mtb", "cabra", "condor",
            "senora_manguera",
        };
        dinaProp.arraySize = dinamicos.Length;
        for (int i = 0; i < dinamicos.Length; i++)
            dinaProp.GetArrayElementAtIndex(i).stringValue = dinamicos[i];

        // Flip: los sprites que en el arte original miran a la DERECHA se dan
        // vuelta para que apunten hacia el jugador (que viene desde la izquierda).
        // Los demas (quiltro, ciclista, huaso, biker_mtb, cabra, condor) ya
        // miran izquierda en el arte, no necesitan flip.
        var flipProp = obsSO.FindProperty("nombresMiranDerecha");
        string[] miranDerecha = new[]
        {
            "senor_chaleco", "carretero_fruta",
            "nino_bandera",
        };
        flipProp.arraySize = miranDerecha.Length;
        for (int i = 0; i < miranDerecha.Length; i++)
            flipProp.GetArrayElementAtIndex(i).stringValue = miranDerecha[i];

        // Restriccion por subzona: el nino con la bandera chilena solo tiene
        // sentido fuera del Estadio Nacional (celebra ahi).
        var restrProp = obsSO.FindProperty("restriccionSubzona");
        string[] restricciones = new[]
        {
            "nino_bandera|Estadio Nacional",
        };
        restrProp.arraySize = restricciones.Length;
        for (int i = 0; i < restricciones.Length; i++)
            restrProp.GetArrayElementAtIndex(i).stringValue = restricciones[i];

        // Bajos: baches y charcos (en el piso, planos).
        var bajosProp = obsSO.FindProperty("nombresBajos");
        string[] bajos = new[] { "bache", "charco" };
        bajosProp.arraySize = bajos.Length;
        for (int i = 0; i < bajos.Length; i++)
            bajosProp.GetArrayElementAtIndex(i).stringValue = bajos[i];

        // Pequenos: animales chicos (perro quiltro, cabra).
        var peqProp = obsSO.FindProperty("nombresPequenos");
        string[] pequenos = new[] { "quiltro", "cabra" };
        peqProp.arraySize = pequenos.Length;
        for (int i = 0; i < pequenos.Length; i++)
            peqProp.GetArrayElementAtIndex(i).stringValue = pequenos[i];

        // Aereos: vuelan por el cielo (condor).
        var aerProp = obsSO.FindProperty("nombresAereos");
        string[] aereos = new[] { "condor" };
        aerProp.arraySize = aereos.Length;
        for (int i = 0; i < aereos.Length; i++)
            aerProp.GetArrayElementAtIndex(i).stringValue = aereos[i];

        // Grandes: NPCs sobre monturas/vehiculos (ocupan mas vertical).
        var grProp = obsSO.FindProperty("nombresGrandes");
        string[] grandes = new[] { "biker_mtb", "scooter" };
        grProp.arraySize = grandes.Length;
        for (int i = 0; i < grandes.Length; i++)
            grProp.GetArrayElementAtIndex(i).stringValue = grandes[i];

        // Extra grandes: el huaso a caballo y el ciclista son los mas imponentes
        // del set, quedan por sobre el resto sin dejar de ser saltables.
        var egProp = obsSO.FindProperty("nombresExtraGrandes");
        string[] extraGrandes = new[] { "huaso", "ciclista" };
        egProp.arraySize = extraGrandes.Length;
        for (int i = 0; i < extraGrandes.Length; i++)
            egProp.GetArrayElementAtIndex(i).stringValue = extraGrandes[i];

        // Muy grandes: puestos callejeros con techo. Pasan por encima del salto simple,
        // obligando a usar doble salto.
        var mgProp = obsSO.FindProperty("nombresMuyGrandes");
        string[] muyGrandes = new[] { "carrito_completos", "carrito_super" };
        mgProp.arraySize = muyGrandes.Length;
        for (int i = 0; i < muyGrandes.Length; i++)
            mgProp.GetArrayElementAtIndex(i).stringValue = muyGrandes[i];

        obsSO.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, JuegoScenePath);
    }

    private static ZonaData[] CargarZonaAssetsOrdenados()
    {
        var guids = AssetDatabase.FindAssets("t:ZonaData", new[] { ZonasFolder });
        var list = new System.Collections.Generic.List<ZonaData>();
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var z = AssetDatabase.LoadAssetAtPath<ZonaData>(path);
            if (z != null) list.Add(z);
        }
        list.Sort((a, b) => a.umbral.CompareTo(b.umbral));
        return list.ToArray();
    }

    private static GameObject CrearFondoSprite(Transform parent, string nombre, Sprite sprite, Vector3 pos, bool flipX = false)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = -10;
        sr.flipX = flipX;
        return go;
    }

    // ============================================================
    // Helpers UI
    // ============================================================
    private static void CrearCamara(Color fondo)
    {
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5;
        cam.backgroundColor = fondo;
        cam.clearFlags = CameraClearFlags.SolidColor;
        camGO.transform.position = new Vector3(0, 0, -10);
        camGO.AddComponent<AudioListener>();
    }

    private static Canvas CrearCanvas()
    {
        var canvasGO = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private static void CrearEventSystem()
    {
        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static void AgregarContornoYSombra(GameObject textGO)
    {
        var outline = textGO.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);
        var shadow = textGO.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(3f, -3f);
    }

    private static Text CrearTitulo(Transform parent, string texto)
    {
        var tituloGO = new GameObject("Titulo", typeof(RectTransform));
        tituloGO.transform.SetParent(parent, false);
        var txt = tituloGO.AddComponent<Text>();
        txt.text = texto;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 64;
        txt.color = Color.white;
        txt.fontStyle = FontStyle.Bold;
        var rt = tituloGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1);
        rt.anchorMax = new Vector2(0.5f, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, -60);
        rt.sizeDelta = new Vector2(900, 100);
        return txt;
    }

    /// <summary>
    /// Zona clickeable transparente, para portadas que ya traen el boton dibujado.
    /// No pinta nada en reposo; al pasar el mouse o pulsar aclara apenas la zona,
    /// que es el unico feedback que falta cuando el boton es parte de la imagen.
    /// </summary>
    private static Button CrearBotonInvisible(Transform parent, string nombre,
        Vector2 pos, Vector2 size)
    {
        var btnGO = new GameObject(nombre, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);
        var rt = btnGO.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var img = btnGO.GetComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0f); // invisible pero recibe el click
        img.raycastTarget = true;

        var btn = btnGO.GetComponent<Button>();
        var colores = btn.colors;
        colores.normalColor = new Color(1f, 1f, 1f, 0f);
        colores.highlightedColor = new Color(1f, 1f, 1f, 0.20f);
        colores.pressedColor = new Color(1f, 1f, 1f, 0.38f);
        colores.selectedColor = new Color(1f, 1f, 1f, 0f);
        colores.fadeDuration = 0.08f;
        btn.colors = colores;
        return btn;
    }

    private static Button CrearBoton(Transform parent, string nombre, string texto, Vector2 pos, Color color)
    {
        var btnGO = new GameObject(nombre, typeof(RectTransform), typeof(Image), typeof(Button));
        btnGO.transform.SetParent(parent, false);
        var rt = btnGO.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(360, 70);

        var img = btnGO.GetComponent<Image>();
        img.color = color;

        var textoGO = new GameObject("Text", typeof(RectTransform));
        textoGO.transform.SetParent(btnGO.transform, false);
        var txt = textoGO.AddComponent<Text>();
        txt.text = texto;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 28;
        txt.color = Color.white;
        txt.fontStyle = FontStyle.Bold;
        var txtRT = textoGO.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero;
        txtRT.offsetMax = Vector2.zero;

        return btnGO.GetComponent<Button>();
    }

    private static void AgregarListener(Button boton, MonoBehaviour target, string metodo)
    {
        var method = target.GetType().GetMethod(metodo);
        var action = (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(
            typeof(UnityEngine.Events.UnityAction), target, method);
        UnityEditor.Events.UnityEventTools.AddPersistentListener(boton.onClick, action);
    }

    private static void RegistrarEnBuildSettings()
    {
        var escenas = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(MenuScenePath, true),
            new EditorBuildSettingsScene(SeleccionScenePath, true),
            new EditorBuildSettingsScene(JuegoScenePath, true),
        };
        EditorBuildSettings.scenes = escenas.ToArray();
    }
}
#endif
