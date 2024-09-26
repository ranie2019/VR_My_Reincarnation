using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class Menu : MonoBehaviour
{
    public Transform player; // Referência ao jogador
    public float distanciaJogador = 3.0f; // Distância do menu em relação ao jogador
    public GameObject menu; // O objeto do menu
    public InputActionProperty exibirBotao; // Ação que exibe o menu
    public float suavidadeMovimento = 5f; // Controla a suavidade do movimento
    public float suavidadeRotacao = 5f; // Controla a suavidade da rotação

    // Update é chamado a cada frame
    void Update()
    {
        if (player == null || menu == null) return; // Verifica se o player e o menu estão atribuídos

        // Verifica se o botão foi pressionado
        if (exibirBotao.action.WasPerformedThisFrame())
        {
            // Alterna a visibilidade do menu
            menu.SetActive(!menu.activeSelf);

            // Posiciona o menu à frente do jogador
            if (menu.activeSelf)
            {
                Vector3 novaPosicao = player.position + new Vector3(player.forward.x, 0, player.forward.z).normalized * distanciaJogador;
                menu.transform.position = novaPosicao;
            }
        }

        // Suaviza o movimento e mantém o menu ativo
        if (menu.activeSelf)
        {
            // Suaviza o movimento do menu para ficar à frente do jogador
            Vector3 novaPosicao = player.position + new Vector3(player.forward.x, 0, player.forward.z).normalized * distanciaJogador;
            menu.transform.position = Vector3.Lerp(menu.transform.position, novaPosicao, Time.deltaTime * suavidadeMovimento);

            // Gira o menu para olhar o jogador sem alterar a rotação no eixo Y
            Vector3 direcaoOlhar = player.position - menu.transform.position;
            direcaoOlhar.y = 0; // Mantém o eixo Y fixo
            Quaternion rotacaoOlhar = Quaternion.LookRotation(direcaoOlhar);
            menu.transform.rotation = Quaternion.Slerp(menu.transform.rotation, rotacaoOlhar, Time.deltaTime * suavidadeRotacao);
        }
    }
}
