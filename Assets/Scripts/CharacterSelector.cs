using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CharacterSelector : MonoBehaviour
{
    [SerializeField] private string escenaMenu = "Menu";
    [SerializeField] private string escenaJuego = "Juego";

    [Header("Opcional: texto que muestra el personaje seleccionado")]
    [SerializeField] private Text labelSeleccion;

    private void Start()
    {
        if (GameData.PersonajeSeleccionado == null)
            GameData.PersonajeSeleccionado = Resources.Load<PersonajeData>("Personajes/Felipe");
        ActualizarLabel();
    }

    public void SeleccionarFelipe()
    {
        GameData.PersonajeSeleccionado = Resources.Load<PersonajeData>("Personajes/Felipe");
        if (AudioManager.Instance != null) AudioManager.Instance.SonarClick();
        ActualizarLabel();
    }

    public void SeleccionarDaniel()
    {
        GameData.PersonajeSeleccionado = Resources.Load<PersonajeData>("Personajes/Daniel");
        if (AudioManager.Instance != null) AudioManager.Instance.SonarClick();
        ActualizarLabel();
    }

    public void VolverAlMenu()
    {
        SceneManager.LoadScene(escenaMenu);
    }

    public void IrAlJuego()
    {
        SceneManager.LoadScene(escenaJuego);
    }

    private void ActualizarLabel()
    {
        if (labelSeleccion != null)
            labelSeleccion.text = "Seleccionado: " + GameData.NombrePersonaje;
    }
}
