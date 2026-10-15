using UnityEngine;

/// <summary>
/// Datos persistentes entre escenas. No es MonoBehaviour: vive mientras corre el proceso.
/// </summary>
public static class GameData
{
    public static PersonajeData PersonajeSeleccionado;

    public static PersonajeData GetPersonajeOrDefault()
    {
        if (PersonajeSeleccionado != null)
            return PersonajeSeleccionado;

        var felipe = Resources.Load<PersonajeData>("Personajes/Felipe");
        if (felipe != null)
            PersonajeSeleccionado = felipe;
        return PersonajeSeleccionado;
    }

    public static string NombrePersonaje =>
        PersonajeSeleccionado != null ? PersonajeSeleccionado.nombreVisible : "Felipe";
}
