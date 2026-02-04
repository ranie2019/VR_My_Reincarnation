using UnityEngine;
using System.Collections;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class MostroVida : MonoBehaviour
{
    [Header("Vida")]
    public int vidaMax = 3;
    public float invencivelPor = 0.1f;

    [Header("Efeitos/Animação")]
    public Animator anim;
    public GameObject efeitoMorte;

    [Header("Eventos")]
    public UnityEvent onMorte;

    private int vidaAtual;
    private bool morto = false;
    private bool invencivel = false;

    void Awake()
    {
        if (!anim) anim = GetComponent<Animator>();
        vidaAtual = vidaMax;
    }

    // ===== RECEBE DANO =====
    public void TakeDamage(int dano)
    {
        if (morto || invencivel) return;

        vidaAtual -= dano;

        if (anim) anim.SetTrigger("Dano");

        if (vidaAtual <= 0)
        {
            StartCoroutine(Morrer());
        }
        else
        {
            StartCoroutine(Invencibilidade());
        }
    }

    private IEnumerator Invencibilidade()
    {
        invencivel = true;
        yield return new WaitForSeconds(invencivelPor);
        invencivel = false;
    }

    // ===== MORTE =====
    private IEnumerator Morrer()
    {
        if (morto) yield break;
        morto = true;
        invencivel = true;

        if (anim) anim.SetBool("Morto", true);

        // espera um tempo fixo (mais confiável que pegar length do state errado)
        yield return new WaitForSeconds(0.6f);

        if (efeitoMorte != null)
        {
            // proteção: não deixa usar prefab com scripts de slime aqui
            if (efeitoMorte.GetComponent<MostroVida>() != null)
            {
                Debug.LogError("EfeitoMorte está com script de slime. Coloque um prefab de partículas aqui.");
            }
            else
            {
                Instantiate(efeitoMorte, transform.position, Quaternion.identity);
            }
        }

        // dispara evento (safe)
        onMorte?.Invoke();

        Destroy(gameObject, 0.1f);
    }

    // ===== INFO =====
    public int GetVidaAtual() => vidaAtual;
    public bool EstaMorto() => morto;
}
