using UnityEngine;

public class AvatarInputConverter : MonoBehaviour
{
    // Transforms do Avatar
    [Header("Transforms do Avatar")]
    public Transform MainAvatarTransform;  // Transform principal do avatar
    public Transform AvatarHead;           // Transform da cabeça do avatar
    public Transform AvatarBody;           // Transform do corpo do avatar

    // Transforms do XR Rig
    [Header("Transforms do XR Rig")]
    public Transform XRHead;               // Transform da cabeça do XR Rig
    public Transform XRHand_Left;          // Transform da mão esquerda do XR Rig
    public Transform XRHand_Right;         // Transform da mão direita do XR Rig

    // Deslocamentos (Offsets)
    [Header("Offsets")]
    public Vector3 headPositionOffset;     // Offset de posição para a cabeça
    public Vector3 handRotationOffset;     // Offset de rotação para as mãos

    // Fatores de suavização
    [Header("Suavização")]
    public float positionLerpSpeed = 5f;   // Velocidade de suavização da posição
    public float rotationLerpSpeed = 5f;   // Velocidade de suavização da rotação
    public float bodyRotationLerpSpeed = 2f; // Velocidade de suavização da rotação do corpo

    // Update é chamado uma vez por frame
    void Update()
    {
        // Sincroniza a posição/rotação da cabeça e do corpo do avatar com o XR Rig
        SyncHeadAndBody();
    }

    // Sincroniza a cabeça e o corpo do avatar com a cabeça do XR Rig
    private void SyncHeadAndBody()
    {
        // Atualiza a posição do avatar com uma interpolação suave, movendo gradualmente para a posição da cabeça do XR mais o offset
        MainAvatarTransform.position = Vector3.Lerp(
            MainAvatarTransform.position,
            XRHead.position + headPositionOffset,
            Time.deltaTime * positionLerpSpeed
        );

        // Suaviza a rotação da cabeça do avatar para alinhar com a rotação da cabeça do XR
        AvatarHead.rotation = Quaternion.Lerp(
            AvatarHead.rotation,
            XRHead.rotation,
            Time.deltaTime * rotationLerpSpeed
        );

        // Sincroniza a rotação do corpo do avatar para alinhar com a direção horizontal da cabeça (desconsiderando inclinações verticais)
        AvatarBody.rotation = Quaternion.Lerp(
            AvatarBody.rotation,
            Quaternion.Euler(0, AvatarHead.rotation.eulerAngles.y, 0),
            Time.deltaTime * bodyRotationLerpSpeed
        );
    }

    // Aqui, futuramente, você pode adicionar a sincronização das mãos
    // Sincroniza as mãos do avatar com as mãos do XR Rig
}
