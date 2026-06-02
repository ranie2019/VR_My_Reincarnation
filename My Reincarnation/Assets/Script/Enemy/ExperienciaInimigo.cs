using UnityEngine;

[DisallowMultipleComponent]
public class ExperienciaInimigo : MonoBehaviour
{
    [Header("Experiencia")]
    [SerializeField] private int experienciaAoMorrer = 25;
    [SerializeField] private bool experienciaEntregue;

    public int ExperienciaAoMorrer => experienciaAoMorrer;
    public bool ExperienciaEntregue => experienciaEntregue;

    private void OnValidate()
    {
        experienciaAoMorrer = Mathf.Max(0, experienciaAoMorrer);
    }

    public void EntregarExperiencia(GameObject jogador)
    {
        if (experienciaEntregue || jogador == null)
            return;

        StatusPlayer statusPlayer = jogador.GetComponent<StatusPlayer>();
        if (statusPlayer == null)
            statusPlayer = jogador.GetComponentInParent<StatusPlayer>();

        EntregarParaStatusPlayer(statusPlayer);
    }

    public void EntregarExperienciaParaPlayerMaisProximo()
    {
        if (experienciaEntregue)
            return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            EntregarExperiencia(player);

        if (experienciaEntregue)
            return;

        StatusPlayer statusPlayer = FindFirstObjectByType<StatusPlayer>();
        EntregarParaStatusPlayer(statusPlayer);
    }

    private void EntregarParaStatusPlayer(StatusPlayer statusPlayer)
    {
        if (experienciaEntregue || statusPlayer == null)
            return;

        statusPlayer.ReceberExperiencia(experienciaAoMorrer);
        experienciaEntregue = true;
    }
}
