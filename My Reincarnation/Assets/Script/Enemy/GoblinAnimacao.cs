using UnityEngine;

public class GoblinAnimacao : MonoBehaviour
{
    private Animator anim;
    private string animacaoAtual = "";

    private void Awake()
    {
        anim = GetComponentInChildren<Animator>();
    }

    public void Parado()
    {
        if (animacaoAtual == "Parado") return;
        animacaoAtual = "Parado";

        if (anim == null) return;

        anim.SetBool("Andar", false);
        anim.SetBool("Correr", false);
        anim.SetBool("Ataque", false);
        anim.SetBool("Morto", false);
    }

    public void Andar()
    {
        if (animacaoAtual == "Andar") return;
        animacaoAtual = "Andar";

        if (anim == null) return;

        anim.SetBool("Andar", true);
        anim.SetBool("Correr", false);
        anim.SetBool("Ataque", false);
        anim.SetBool("Morto", false);
    }

    public void Correr()
    {
        if (animacaoAtual == "Correr") return;
        animacaoAtual = "Correr";

        if (anim == null) return;

        anim.SetBool("Andar", false);
        anim.SetBool("Correr", true);
        anim.SetBool("Ataque", false);
        anim.SetBool("Morto", false);
    }

    public void Atacar()
    {
        if (animacaoAtual == "Ataque") return;
        animacaoAtual = "Ataque";

        if (anim == null) return;

        anim.SetBool("Andar", false);
        anim.SetBool("Correr", false);
        anim.SetBool("Ataque", true);
        anim.SetBool("Morto", false);
    }

    public void TomarDano()
    {
        // Reseta o controle interno para permitir voltar para Parado/Andar depois
        animacaoAtual = "";

        if (anim == null) return;

        // Para a locomoção enquanto toma dano
        anim.SetBool("Andar", false);
        anim.SetBool("Correr", false);
        anim.SetBool("Ataque", false);
        // Não mexe no Morto
    }

    public void Morrer()
    {
        if (animacaoAtual == "Morto") return;
        animacaoAtual = "Morto";

        if (anim == null) return;

        anim.SetBool("Andar", false);
        anim.SetBool("Correr", false);
        anim.SetBool("Ataque", false);
        anim.SetBool("Morto", true);
    }
}