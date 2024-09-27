using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class Menu : MonoBehaviour
{
    public Transform player; // Refer�ncia ao jogador
    public float distanciaJogador = 3.0f; // Dist�ncia do menu em rela��o ao jogador
    public GameObject menu; // O objeto do menu
    public InputActionProperty exibirBotao; // A��o que exibe o menu
    public float suavidadeMovimento = 5f; // Controla a suavidade do movimento
    public float suavidadeRotacao = 5f; // Controla a suavidade da rota��o
    public float rotacaoOffset = 180f; // Ajuste para garantir que o menu esteja de frente para o jogador

    void Update()
    {
        if (player == null || menu == null) return; // Verifica se o player e o menu est�o atribu�dos

        // Verifica se o bot�o foi pressionado
        if (exibirBotao.action.WasPerformedThisFrame())
        {
            // Alterna a visibilidade do menu
            menu.SetActive(!menu.activeSelf);

            // Posiciona o menu � frente do jogador
            if (menu.activeSelf)
            {
                Vector3 novaPosicao = player.position + new Vector3(player.forward.x, 0, player.forward.z).normalized * distanciaJogador;
                menu.transform.position = novaPosicao;
            }
        }

        // Suaviza o movimento e mant�m o menu ativo
        if (menu.activeSelf)
        {
            // Suaviza o movimento do menu para ficar � frente do jogador
            Vector3 novaPosicao = player.position + new Vector3(player.forward.x, 0, player.forward.z).normalized * distanciaJogador;
            menu.transform.position = Vector3.Lerp(menu.transform.position, novaPosicao, Time.deltaTime * suavidadeMovimento);

            // Gira o menu para olhar o jogador diretamente, aplicando uma rota��o de 180� para evitar a invers�o
            Vector3 direcaoOlhar = player.position - menu.transform.position;
            direcaoOlhar.y = 0; // Mant�m o eixo Y fixo
            Quaternion rotacaoOlhar = Quaternion.LookRotation(direcaoOlhar);

            // Aplica o ajuste de 180� no eixo Y para garantir que o menu fique de frente para o jogador
            rotacaoOlhar *= Quaternion.Euler(0, rotacaoOffset, 0);

            menu.transform.rotation = Quaternion.Slerp(menu.transform.rotation, rotacaoOlhar, Time.deltaTime * suavidadeRotacao);
        }
    }
}
