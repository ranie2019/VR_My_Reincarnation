using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class AtivarObjeto : MonoBehaviour
{
    public GameObject objetoParaAtivar;  // O objeto que ser� ativado/desativado
    private bool objetoAtivo = false;    // Vari�vel para acompanhar o estado do objeto
    private bool botaoXPressionadoAnteriormente = false; // Para detectar quando o bot�o X foi pressionado
    private bool botaoYPressionadoAnteriormente = false; // Para detectar quando o bot�o Y foi pressionado

    void Start()
    {
        // Certifica-se de que o objeto come�a desabilitado
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
            // Verificar se o bot�o X foi pressionado para ativar
            if (VerificarBotao(dispositivo, CommonUsages.primaryButton, ref botaoXPressionadoAnteriormente, !objetoAtivo))
            {
                AtivarObjetoFuncao();
            }

            // Verificar se o bot�o Y foi pressionado para desativar
            if (VerificarBotao(dispositivo, CommonUsages.secondaryButton, ref botaoYPressionadoAnteriormente, objetoAtivo))
            {
                DesativarObjetoFuncao();
            }
        }
    }

    // M�todo para verificar se o bot�o foi pressionado
    private bool VerificarBotao(InputDevice dispositivo, InputFeatureUsage<bool> botao, ref bool botaoPressionadoAnteriormente, bool condicao)
    {
        bool estadoBotao;
        if (dispositivo.TryGetFeatureValue(botao, out estadoBotao) && estadoBotao && !botaoPressionadoAnteriormente && condicao)
        {
            botaoPressionadoAnteriormente = estadoBotao;
            return true;
        }
        botaoPressionadoAnteriormente = estadoBotao; // Atualiza o estado do bot�o
        return false;
    }

    private void AtivarObjetoFuncao()
    {
        if (objetoParaAtivar != null)
        {
            objetoParaAtivar.SetActive(true);
            objetoAtivo = true;
            Debug.Log("Bot�o X pressionado: Objeto ativado.");
        }
    }

    private void DesativarObjetoFuncao()
    {
        if (objetoParaAtivar != null)
        {
            objetoParaAtivar.SetActive(false);
            objetoAtivo = false;
            Debug.Log("Bot�o Y pressionado: Objeto desativado.");
        }
    }
}
