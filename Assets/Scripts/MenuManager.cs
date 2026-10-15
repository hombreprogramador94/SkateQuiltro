using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    [Header("Nombres de escenas (deben coincidir con el Build Settings)")]
    [SerializeField] private string escenaSeleccion = "SeleccionPersonaje";
    [SerializeField] private string escenaJuego = "Juego";

    [Header("HUD")]
    [Tooltip("Record historico. En la portada estaba pintado fijo; aca se lee real.")]
    [SerializeField] private Text labelRecord;

    private void Start()
    {
        if (labelRecord != null)
        {
            int record = PlayerPrefs.GetInt(GameManager.RecordKey, 0);
            labelRecord.text = $"RECORD {record:N0}";
        }
    }

    public void IniciarJuego()
    {
        SceneManager.LoadScene(escenaJuego);
    }

    public void IrASeleccionPersonaje()
    {
        SceneManager.LoadScene(escenaSeleccion);
    }

    public void Salir()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
