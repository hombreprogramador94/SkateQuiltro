using UnityEngine;

[CreateAssetMenu(menuName = "Juego/PersonajeData", fileName = "Personaje")]
public class PersonajeData : ScriptableObject
{
    public string nombreVisible = "Felipe";

    [Header("Poses")]
    public Sprite parado;
    public Sprite impulsando;
    public Sprite agachado;
    public Sprite aireConTabla;
    public Sprite aireSinTabla;
}
