using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class AtivarObjeto : MonoBehaviour
{
    public GameObject objetoParaAtivar;  // O objeto que será ativado/desativado
    private bool objetoAtivo = false;    // Variável para acompanhar o estado do objeto
    private bool botaoXPressionadoAnteriormente = false; // Para detectar quando o botão X foi pressionado
    private bool botaoYPressionadoAnteriormente = false; // Para detectar quando o botão Y foi pressionado

    void Start()
    {
        // Certifica-se de que o objeto começa desabilitado
        if (objetoParaAtivar != null)
        {
            objetoParaAtivar.SetActive(false);
            objetoAtivo = false; // Define o estado inicial do objeto como desativado
        }
    }

    void Update()
    {
        // Obter a lista de dispositivos XR
        List<InputDevice> dispositivos = new List<InputDevice>();
        InputDevices.GetDevices(dispositivos);

        foreach (var dispositivo in dispositivos)
        {
            // Verificar se o botão X foi pressionado para ativar
            bool botaoX;
            if (dispositivo.TryGetFeatureValue(CommonUsages.primaryButton, out botaoX))
            {
                if (botaoX && !botaoXPressionadoAnteriormente && !objetoAtivo)
                {
                    // Ativar o objeto
                    objetoParaAtivar.SetActive(true);
                    objetoAtivo = true;
                    Debug.Log("Botão X pressionado: Objeto ativado.");
                }

                // Atualiza o estado do botão X
                botaoXPressionadoAnteriormente = botaoX;
            }

            // Verificar se o botão Y foi pressionado para desativar
            bool botaoY;
            if (dispositivo.TryGetFeatureValue(CommonUsages.secondaryButton, out botaoY))
            {
                if (botaoY && !botaoYPressionadoAnteriormente && objetoAtivo)
                {
                    // Desativar o objeto
                    objetoParaAtivar.SetActive(false);
                    objetoAtivo = false;
                    Debug.Log("Botão Y pressionado: Objeto desativado.");
                }

                // Atualiza o estado do botão Y
                botaoYPressionadoAnteriormente = botaoY;
            }
        }
    }
}
