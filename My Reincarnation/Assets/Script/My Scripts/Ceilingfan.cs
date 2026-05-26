using UnityEngine;

public class CeilingFan : MonoBehaviour
{
    // Velocidade de rotação da hélice em graus por segundo.
    [Tooltip("Velocidade de rotação da hélice em graus por segundo.")]
    public float rotationSpeed = 360.0f;

    void Update()
    {
        // Chama o método para girar a hélice a cada frame.
        RotateBlades();
    }

    // Método responsável por calcular e aplicar a rotação da hélice.
    private void RotateBlades()
    {
        // Calcula a rotação a ser aplicada na hélice com base na velocidade e no tempo decorrido.
        float rotationAmount = rotationSpeed * Time.deltaTime;

        // Aplica a rotação à hélice ao redor do eixo Y.
        transform.Rotate(Vector3.up, rotationAmount);
    }
}
