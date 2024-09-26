using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class Botoes : MonoBehaviour
{
    // Update is called once per frame
    void Update()
    {
        // Obter informações do estado dos dispositivos XR (incluindo os controles)
        List<InputDevice> dispositivos = new List<InputDevice>();
        InputDevices.GetDevices(dispositivos);

        foreach (var dispositivo in dispositivos)
        {
            // Testa os botões primários (Botão A e Botão X)
            bool botaoPrimario;
            if (dispositivo.TryGetFeatureValue(CommonUsages.primaryButton, out botaoPrimario) && botaoPrimario)
            {
                Debug.Log("Botão Primário Pressionado! (A ou X)");
            }

            // Testa os botões secundários (Botão B e Botão Y)
            bool botaoSecundario;
            if (dispositivo.TryGetFeatureValue(CommonUsages.secondaryButton, out botaoSecundario) && botaoSecundario)
            {
                Debug.Log("Botão Secundário Pressionado! (B ou Y)");
            }

            // Testa os gatilhos (Trigger)
            float gatilho;
            if (dispositivo.TryGetFeatureValue(CommonUsages.trigger, out gatilho) && gatilho > 0.1f)
            {
                Debug.Log("Gatilho Pressionado! Valor: " + gatilho);
            }

            // Testa o grip (Aperto lateral)
            float grip;
            if (dispositivo.TryGetFeatureValue(CommonUsages.grip, out grip) && grip > 0.1f)
            {
                Debug.Log("Grip Pressionado! Valor: " + grip);
            }

            // Testa o thumbstick (direcional analógico)
            Vector2 thumbstick;
            if (dispositivo.TryGetFeatureValue(CommonUsages.primary2DAxis, out thumbstick) && thumbstick.magnitude > 0.1f)
            {
                Debug.Log("Thumbstick Movido! Valor: " + thumbstick);
            }
        }
    }
}
