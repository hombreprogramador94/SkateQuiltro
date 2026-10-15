#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildScript
{
    [MenuItem("Juego/Build Windows v0.1")]
    public static void BuildWindowsV01() => BuildVersion("v0.1");

    [MenuItem("Juego/Build Windows v0.2")]
    public static void BuildWindowsV02() => BuildVersion("v0.2");

    [MenuItem("Juego/Build Windows v0.3")]
    public static void BuildWindowsV03() => BuildVersion("v0.3");

    [MenuItem("Juego/Build Windows v0.3d")]
    public static void BuildWindowsV03d() => BuildVersion("v0.3d");

    [MenuItem("Juego/Build Windows v0.3e")]
    public static void BuildWindowsV03e() => BuildVersion("v0.3e");

    [MenuItem("Juego/Build Windows v0.3f")]
    public static void BuildWindowsV03f() => BuildVersion("v0.3f");

    [MenuItem("Juego/Build Windows v0.4")]
    public static void BuildWindowsV04() => BuildVersion("v0.4");

    [MenuItem("Juego/Build Windows v0.5")]
    public static void BuildWindowsV05() => BuildVersion("v0.5");

    [MenuItem("Juego/Build Windows v0.6")]
    public static void BuildWindowsV06() => BuildVersion("v0.6");

    [MenuItem("Juego/Build Windows v0.7")]
    public static void BuildWindowsV07() => BuildVersion("v0.7");

    [MenuItem("Juego/Build Windows v0.8")]
    public static void BuildWindowsV08() => BuildVersion("v0.8");

    [MenuItem("Juego/Build Windows v0.9")]
    public static void BuildWindowsV09() => BuildVersion("v0.9");

    [MenuItem("Juego/Build Windows v0.18")]
    public static void BuildWindowsV018() => BuildVersion("v0.18");

    [MenuItem("Juego/Build Windows v0.17")]
    public static void BuildWindowsV017() => BuildVersion("v0.17");

    [MenuItem("Juego/Build Windows v0.16")]
    public static void BuildWindowsV016() => BuildVersion("v0.16");

    [MenuItem("Juego/Build Windows v0.15")]
    public static void BuildWindowsV015() => BuildVersion("v0.15");

    [MenuItem("Juego/Build Windows v0.14")]
    public static void BuildWindowsV014() => BuildVersion("v0.14");

    [MenuItem("Juego/Build Windows v0.13")]
    public static void BuildWindowsV013() => BuildVersion("v0.13");

    [MenuItem("Juego/Build Windows v0.12")]
    public static void BuildWindowsV012() => BuildVersion("v0.12");

    [MenuItem("Juego/Build Windows v0.11")]
    public static void BuildWindowsV011() => BuildVersion("v0.11");

    [MenuItem("Juego/Build Windows v0.10")]
    public static void BuildWindowsV010() => BuildVersion("v0.10");

    /// <summary>
    /// Compila SIEMPRE a Builds/ULTIMA, sin importar la version. Tener una
    /// carpeta por version llevaba a jugar builds viejos sin darse cuenta y
    /// creer que los cambios no tomaban efecto. El acceso directo del
    /// Escritorio apunta aca, asi que esta carpeta es siempre la buena.
    /// </summary>
    public const string CarpetaUltima = "Builds/ULTIMA";

    public static void BuildUltima() => BuildVersion("ULTIMA");

    /// <summary>
    /// Reimporta todo Assets/Art a la fuerza. Hace falta cuando los PNG se editan
    /// POR FUERA del editor (scripts de Python, Photoshop con Unity cerrado): en
    /// batchmode Unity no nota el cambio de archivo y compila con la textura que
    /// tiene cacheada en Library, asi que el build sale con el arte viejo.
    /// </summary>
    [MenuItem("Juego/Forzar reimport del arte")]
    public static void ForzarReimportArte()
    {
        var pngs = Directory.GetFiles("Assets/Art", "*.png", SearchOption.AllDirectories);
        foreach (var p in pngs)
            AssetDatabase.ImportAsset(p.Replace('\\', '/'), ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();
        Debug.Log($"[BuildScript] Reimportados {pngs.Length} PNG de Assets/Art");
    }

    /// <summary>Reimport forzado del arte y build, para usar tras editar PNG por fuera.</summary>
    public static void ReimportarYBuild()
    {
        ForzarReimportArte();
        BuildVersion("ULTIMA");
    }

    /// <summary>
    /// Build para macOS (.app). Requiere el modulo "Mac Build Support (Mono)"
    /// instalado en Unity Hub para esta version del editor.
    ///
    /// OJO al copiarlo al Mac: un .app generado desde Windows pierde el permiso
    /// de ejecucion del binario interno, porque NTFS no guarda permisos Unix.
    /// En el Mac hay que devolverselo antes de abrirlo:
    ///     chmod +x "SkateQuiltro.app/Contents/MacOS/SkateQuiltro"
    /// Y como no esta firmado, Gatekeeper lo bloquea la primera vez: se abre
    /// con clic derecho > Abrir, o se le quita la cuarentena con
    ///     xattr -cr SkateQuiltro.app
    /// </summary>
    [MenuItem("Juego/Build macOS")]
    public static void BuildMac()
    {
        var escenas = EditorBuildSettings.scenes
            .Where(s => s.enabled).Select(s => s.path).ToArray();

        string outDir = "Builds/MAC";
        if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

        var opts = new BuildPlayerOptions
        {
            scenes = escenas,
            locationPathName = $"{outDir}/SkateQuiltro.app",
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(opts);
        var sum = report.summary;
        Debug.Log($"[BuildScript macOS] result={sum.result} size={sum.totalSize} time={sum.totalTime}");
        if (sum.result != BuildResult.Succeeded)
            Debug.LogError("[BuildScript macOS] Build FAILED - revisa que este instalado Mac Build Support");
    }

    /// <summary>
    /// Build WebGL: corre en el navegador de cualquier sistema. Es la via para
    /// jugarlo en Mac sin tener instalado Mac Build Support en este Unity.
    /// </summary>
    [MenuItem("Juego/Build WebGL")]
    public static void BuildWebGL()
    {
        var escenas = EditorBuildSettings.scenes
            .Where(s => s.enabled).Select(s => s.path).ToArray();

        string outDir = "Builds/WEB";
        if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

        var opts = new BuildPlayerOptions
        {
            scenes = escenas,
            locationPathName = outDir,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(opts);
        var sum = report.summary;
        Debug.Log($"[BuildScript WebGL] result={sum.result} size={sum.totalSize} time={sum.totalTime}");
        if (sum.result != BuildResult.Succeeded)
            Debug.LogError("[BuildScript WebGL] Build FAILED");
    }

    public static void BuildVersion(string version)
    {
        var scenesPath = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        // Todas las versiones escriben en la misma carpeta: una sola fuente de
        // verdad para probar. El numero de version queda solo en el log.
        string outDir = CarpetaUltima;
        if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
        string outPath = $"{outDir}/SkateQuiltro.exe";

        var opts = new BuildPlayerOptions
        {
            scenes = scenesPath,
            locationPathName = outPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };

        PlayerSettings.productName = "SkateInacap";
        PlayerSettings.companyName = "INACAP";

        BuildReport report = BuildPipeline.BuildPlayer(opts);
        var summary = report.summary;
        Debug.Log($"[BuildScript {version}] result={summary.result} size={summary.totalSize} time={summary.totalTime}");
        if (summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"[BuildScript {version}] Build FAILED");
            EditorApplication.Exit(1);
        }
    }
}
#endif
