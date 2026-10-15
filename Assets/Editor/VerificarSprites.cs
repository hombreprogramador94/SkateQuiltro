#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Diagnostico: lee los pixeles del sprite TAL COMO UNITY LO TIENE IMPORTADO,
/// no del PNG en disco. Sirve para distinguir un problema de arte (el archivo
/// esta mal) de uno de cache (el archivo esta bien pero Unity usa una version
/// vieja), que es justo lo que no se podia separar con el perro blanco.
/// </summary>
public static class VerificarSprites
{
    [MenuItem("Juego/Verificar alpha de los perros")]
    public static void VerificarPerros()
    {
        foreach (var color in new[] { "blanco", "cafe", "negro" })
        {
            for (int f = 1; f <= 4; f++)
            {
                string ruta = $"Assets/Art/Dogs/{color}/frame_{f}.png";
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
                if (tex == null)
                {
                    Debug.Log($"[VERIF] {color}/{f}: NO SE PUDO CARGAR");
                    continue;
                }

                // La textura importada no es readable por defecto: se habilita
                // temporalmente para poder muestrear el alpha real.
                var imp = AssetImporter.GetAtPath(ruta) as TextureImporter;
                bool eraReadable = imp != null && imp.isReadable;
                if (imp != null && !eraReadable)
                {
                    imp.isReadable = true;
                    imp.SaveAndReimport();
                    tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
                }

                var px = tex.GetPixels32();
                int cuerpo = 0, bajo250 = 0, grises = 0;
                long suma = 0, lum = 0;
                foreach (var p in px)
                {
                    if (p.a > 25)
                    {
                        cuerpo++;
                        suma += p.a;
                        if (p.a < 250) bajo250++;
                        int mx = Mathf.Max(p.r, Mathf.Max(p.g, p.b));
                        int mn = Mathf.Min(p.r, Mathf.Min(p.g, p.b));
                        lum += (p.r + p.g + p.b) / 3;
                        // Gris medio = el color del asfalto. Si hay muchos, el
                        // cuerpo se lee como "transparente" aunque sea opaco.
                        if (mx - mn < 25 && (p.r + p.g + p.b) / 3 > 85 && (p.r + p.g + p.b) / 3 < 150)
                            grises++;
                    }
                }
                float prom = cuerpo > 0 ? (float)suma / cuerpo : 0f;
                float pct = cuerpo > 0 ? 100f * bajo250 / cuerpo : 0f;
                float pctGris = cuerpo > 0 ? 100f * grises / cuerpo : 0f;
                float lumMed = cuerpo > 0 ? (float)lum / cuerpo : 0f;
                Debug.Log($"[VERIF] {color}/frame_{f}: alphaMedio={prom:F1} " +
                          $"%bajo250={pct:F1}% | %grisAsfalto={pctGris:F1}% lumMedia={lumMed:F0}");

                if (imp != null && !eraReadable)
                {
                    imp.isReadable = false;
                    imp.SaveAndReimport();
                }
            }
        }
    }
}
#endif
