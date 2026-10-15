#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Construye las pantallas de VICTORIA y DERROTA replicando las referencias de
/// Arte_nuevo/*.png sobre un Canvas de 1920x1080. Las coordenadas salen de medir
/// las previews, por eso van como constantes con el valor en pixeles de imagen
/// convertido a coordenadas de Canvas (origen al centro, Y hacia arriba).
/// </summary>
public static class SceneSetupUI
{
    private const string UI = "Assets/Art/UI";

    private static Sprite Spr(string n) =>
        AssetDatabase.LoadAssetAtPath<Sprite>($"{UI}/{n}.png");

    /// <summary>
    /// Condensada en negrita (Arial Narrow Bold), que es lo mas cercano a
    /// "Helvetica Condensed Bold" disponible en Windows. Si faltara, cae a la
    /// fuente por defecto para no dejar la UI sin texto.
    /// </summary>
    private static Font Fuente()
    {
        var f = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/ArialNarrowBold.ttf");
        return f != null ? f : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    /// <summary>px de imagen (origen arriba-izq) -> coords de Canvas 1920x1080.</summary>
    private static Vector2 P(float x, float y) => new Vector2(x - 960f, 540f - y);

    private static RectTransform Rect(GameObject go, Vector2 pos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    private static Image Img(Transform padre, string nombre, Sprite sprite,
        Vector2 pos, Vector2 size, bool raycast = false)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var im = go.AddComponent<Image>();
        im.sprite = sprite;
        im.raycastTarget = raycast;
        im.preserveAspect = true;
        Rect(go, pos, size);
        return im;
    }

    private static Text Txt(Transform padre, string nombre, string texto, int tam,
        Color color, TextAnchor align, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var t = go.AddComponent<Text>();
        t.text = texto; t.font = Fuente(); t.fontSize = tam;
        t.color = color; t.alignment = align; t.fontStyle = FontStyle.Bold;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        Rect(go, pos, size);
        return t;
    }

    /// <summary>Boton con sprite swap normal/hover/presionado.</summary>
    private static Button Boton(Transform padre, string nombre, string etiqueta,
        Vector2 pos, Vector2 size)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        var im = go.AddComponent<Image>();
        im.sprite = Spr("boton_normal");
        im.type = Image.Type.Simple;
        im.raycastTarget = true;
        Rect(go, pos, size);

        var btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.SpriteSwap;
        btn.targetGraphic = im;
        var st = btn.spriteState;
        st.highlightedSprite = Spr("boton_hover");
        st.pressedSprite = Spr("boton_presionado");
        st.selectedSprite = Spr("boton_normal");
        btn.spriteState = st;

        var lab = Txt(go.transform, "Label", etiqueta, 38,
            new Color(0.12f, 0.10f, 0.06f), TextAnchor.MiddleCenter,
            Vector2.zero, size);
        var lrt = lab.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
        return btn;
    }

    /// <summary>Las tres filas PUNTAJE / ZONA / RECORD dentro del panel crema.</summary>
    private static void FilasPanel(Transform panel, out Text vPuntaje,
        out Text vZona, out Text vRecord)
    {
        var gris = new Color(0.42f, 0.40f, 0.38f);
        var oscuro = new Color(0.13f, 0.12f, 0.11f);
        float[] ys = { 82f, -2f, -86f };
        string[] et = { "PUNTAJE", "ZONA", "RECORD" };
        var vals = new Text[3];
        for (int i = 0; i < 3; i++)
        {
            Txt(panel, "Et" + et[i], et[i], 30, gris, TextAnchor.MiddleLeft,
                new Vector2(-240f, ys[i]), new Vector2(300, 48));
            vals[i] = Txt(panel, "Val" + et[i], "0", 52, oscuro,
                TextAnchor.MiddleRight, new Vector2(230f, ys[i]), new Vector2(420, 64));
            if (i < 2)
            {
                var sep = new GameObject("Sep" + i, typeof(RectTransform));
                sep.transform.SetParent(panel, false);
                var si = sep.AddComponent<Image>();
                si.color = new Color(0.72f, 0.69f, 0.64f, 0.85f);
                si.raycastTarget = false;
                Rect(sep, new Vector2(0f, ys[i] - 42f), new Vector2(620, 2));
            }
        }
        vPuntaje = vals[0]; vZona = vals[1]; vRecord = vals[2];
    }

    /// <summary>Arma un panel de resultado completo y deja sus referencias en el GameManager.</summary>
    public static GameObject CrearPantalla(Transform canvas, bool victoria,
        out Text vPuntaje, out Text vZona, out Text vRecord,
        out Text nuevoRecord, out Image personaje, out Button btnReintentar,
        out Button btnMenu)
    {
        var raiz = new GameObject(victoria ? "PantallaVictoria" : "PantallaDerrota",
            typeof(RectTransform));
        raiz.transform.SetParent(canvas, false);
        var rrt = raiz.GetComponent<RectTransform>();
        rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one;
        rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;

        // Fondo a pantalla completa (sin preserveAspect: debe cubrir).
        var fondoGO = new GameObject("Fondo", typeof(RectTransform));
        fondoGO.transform.SetParent(raiz.transform, false);
        var fim = fondoGO.AddComponent<Image>();
        fim.sprite = Spr(victoria ? "fondo_victoria" : "fondo_derrota");
        fim.raycastTarget = true;
        var frt = fondoGO.GetComponent<RectTransform>();
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;

        // El personaje va detras del panel y los botones, pero sobre el fondo.
        personaje = Img(raiz.transform, "Personaje", null,
            victoria ? P(1675, 645) : P(775, 870),
            victoria ? new Vector2(430, 430) : new Vector2(330, 330));
        if (!victoria)
            personaje.rectTransform.localRotation = Quaternion.Euler(0, 0, -12f);

        Img(raiz.transform, "Titulo", Spr(victoria ? "titulo_victoria" : "titulo_derrota"),
            P(960, victoria ? 175 : 180),
            victoria ? new Vector2(938, 282) : new Vector2(1246, 287));

        nuevoRecord = Txt(raiz.transform, "NuevoRecord", "¡NUEVO RECORD!", 40,
            new Color(1f, 0.85f, 0.25f), TextAnchor.MiddleCenter,
            P(1190, 432), new Vector2(520, 56));

        var panel = Img(raiz.transform, "Panel", Spr("panel_resultado"),
            victoria ? P(951, 635) : P(1469, 625), new Vector2(776, 346));
        FilasPanel(panel.transform, out vPuntaje, out vZona, out vRecord);

        btnReintentar = Boton(raiz.transform, "BtnReintentar", "JUGAR DE NUEVO",
            victoria ? P(727, 915) : P(1245, 905), new Vector2(480, 132));
        btnMenu = Boton(raiz.transform, "BtnMenu", "MENU",
            victoria ? P(1127, 915) : P(1645, 905), new Vector2(330, 132));

        raiz.SetActive(false);
        return raiz;
    }
}
#endif
