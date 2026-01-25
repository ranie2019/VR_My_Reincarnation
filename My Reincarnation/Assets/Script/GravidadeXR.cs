using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class GravidadeXR : MonoBehaviour
{
    [Header("Gravidade")]
    public float gravity = -9.81f;
    public float groundedForce = -2f;

    private CharacterController controller;
    private float verticalVelocity;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        bool grounded = controller.isGrounded;

        if (grounded && verticalVelocity < 0f)
        {
            // Mantém o player colado no chão
            verticalVelocity = groundedForce;
        }
        else
        {
            // Aplica gravidade
            verticalVelocity += gravity * Time.deltaTime;
        }

        // Move só no eixo Y (queda)
        controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
    }
}
