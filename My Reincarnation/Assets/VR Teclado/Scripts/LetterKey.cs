using UnityEngine;
using System.Collections;
using TMPro;

namespace VRKeys
{

    /// <summary>
    /// Uma tecla individual do teclado.
    /// </summary>
    public class LetterKey : Key
    {
        public TextMeshPro shiftedLabel;

        public string character = "";

        public string shiftedChar = "";

        private bool _shifted = false;

        public bool shifted
        {
            get { return _shifted; }
            set
            {
                _shifted = value;
                label.text = _shifted ? shiftedChar : character;
                shiftedLabel.text = _shifted ? character : shiftedChar;
            }
        }

        public string GetCharacter()
        {
            return _shifted ? shiftedChar : character;
        }

        // Método chamado quando ocorre uma colisão com outro objeto
        private void OnTriggerEnter(Collider other)
        {
            // Verifica se o objeto que colidiu não é nulo
            if (other != null)
            {
                // Chama o método HandleTriggerEnter, que adiciona o caractere e ativa o efeito
                HandleTriggerEnter(other);
            }
        }

        // Lógica de quando a tecla é pressionada (em caso de colisão)
        public override void HandleTriggerEnter(Collider other)
        {
            // Adiciona o caractere da tecla pressionada no teclado virtual
            keyboard.AddCharacter(GetCharacter());

            // Ativa o efeito visual ou sonoro da tecla por 0.3 segundos
            ActivateFor(0.3f);
        }

        // Método que ativa efeitos visuais/sonoros por um período de tempo
        private void ActivateFor(float duration)
        {
            // Exemplo de ativação temporária (pode ser substituído por seu próprio efeito)
            // Aqui você pode ativar a animação ou um som para indicar que a tecla foi pressionada
            StartCoroutine(DeactivateEffect(duration));
        }

        // Método que desativa o efeito após o tempo especificado
        private IEnumerator DeactivateEffect(float duration)
        {
            // Aguarda o tempo especificado
            yield return new WaitForSeconds(duration);

            // Desativa o efeito aqui (pode ser uma animação ou outro efeito)
            // Exemplo de desativação: (caso você tenha um objeto de animação ou efeitos visuais)
            // EffectObject.SetActive(false);
        }
    }
}
