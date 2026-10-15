using UnityEngine;

/// <summary>
/// Camara que sigue al objetivo en X con un pequeno offset.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform objetivo;
    [SerializeField] private float offsetX = 2f;
    [SerializeField] private float suavizado = 5f;
    [SerializeField] private float yFijo = 0f;

    private void LateUpdate()
    {
        if (objetivo == null) return;
        var destino = new Vector3(objetivo.position.x + offsetX, yFijo, transform.position.z);
        transform.position = Vector3.Lerp(transform.position, destino, Time.deltaTime * suavizado);
    }
}
