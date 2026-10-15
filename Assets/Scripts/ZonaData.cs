using UnityEngine;

[CreateAssetMenu(menuName = "Juego/ZonaData", fileName = "Zona")]
public class ZonaData : ScriptableObject
{
    public string zona = "Puente Alto";
    public string subzona = "Pasaje";

    [Tooltip("Puntos minimos para entrar a esta zona. 0 = zona inicial.")]
    public int umbral = 0;

    [Header("Fondo")]
    public Sprite fondo;
    public Color colorCielo = new Color(0.45f, 0.65f, 0.85f);

    public string NombreCompleto => $"{zona} - {subzona}";
}
