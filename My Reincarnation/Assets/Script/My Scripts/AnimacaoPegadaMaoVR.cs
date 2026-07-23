using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
// Um único componente controla os Animators das duas mãos de forma independente.
public class AnimacaoPegadaMaoVR : MonoBehaviour
{
    private static readonly List<AnimacaoPegadaMaoVR> Instancias = new List<AnimacaoPegadaMaoVR>();
    private static int selecoesDireita;
    private static int selecoesEsquerda;
    private static int selecoesMirarDireita;
    private static int selecoesMirarEsquerda;

    [Header("Animators das mãos")]
    [FormerlySerializedAs("animatorMao")]
    [SerializeField] private Animator animatorMaoDireita;
    [SerializeField] private Animator animatorMaoEsquerda;
    [SerializeField] private string parametroPegada = "Pegada";
    [SerializeField] private string parametroMirar = "Mirar";

    [Header("Diagnóstico")]
    [SerializeField] private bool pegadaDireitaAtiva;
    [SerializeField] private bool pegadaEsquerdaAtiva;
    [SerializeField] private bool mirarDireitaAtiva;
    [SerializeField] private bool mirarEsquerdaAtiva;
    [SerializeField] private string statusMaoDireita;
    [SerializeField] private string statusMaoEsquerda;

    private int hashParametroPegada;
    private int hashParametroMirar;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ReiniciarEstadoEstatico()
    {
        Instancias.Clear();
        selecoesDireita = 0;
        selecoesEsquerda = 0;
        selecoesMirarDireita = 0;
        selecoesMirarEsquerda = 0;
    }

    private void Awake()
    {
        ConfigurarReferencias();
    }

    private void OnEnable()
    {
        if (!Instancias.Contains(this))
            Instancias.Add(this);

        ConfigurarReferencias();
        AplicarEstadosDosAnimators();
    }

    private void OnDisable()
    {
        Instancias.Remove(this);
    }

    public static void NotificarPegada(bool direita, bool segurando)
    {
        if (direita)
            selecoesDireita = Mathf.Max(0, selecoesDireita + (segurando ? 1 : -1));
        else
            selecoesEsquerda = Mathf.Max(0, selecoesEsquerda + (segurando ? 1 : -1));

        AtualizarTodasAsInstancias();
    }

    public static void NotificarMirar(bool direita, bool segurando)
    {
        if (direita)
            selecoesMirarDireita = Mathf.Max(0, selecoesMirarDireita + (segurando ? 1 : -1));
        else
            selecoesMirarEsquerda = Mathf.Max(0, selecoesMirarEsquerda + (segurando ? 1 : -1));

        AtualizarTodasAsInstancias();
    }

    private static void AtualizarTodasAsInstancias()
    {
        bool encontrouInstancia = false;

        for (int i = Instancias.Count - 1; i >= 0; i--)
        {
            AnimacaoPegadaMaoVR instancia = Instancias[i];
            if (instancia == null)
            {
                Instancias.RemoveAt(i);
                continue;
            }

            encontrouInstancia = true;
            instancia.ConfigurarReferencias();
            instancia.AplicarEstadosDosAnimators();
        }

        if (!encontrouInstancia)
            AplicarEstadosSemComponenteNaCena();
    }

    private void ConfigurarReferencias()
    {
        hashParametroPegada = Animator.StringToHash(parametroPegada);
        hashParametroMirar = Animator.StringToHash(parametroMirar);

        if (animatorMaoDireita != null && animatorMaoEsquerda != null)
            return;

        Animator[] animators = FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (!TemParametroBool(animator, hashParametroPegada) ||
                !TentarDetectarLado(animator.transform, out bool direita))
                continue;

            if (direita && animatorMaoDireita == null)
                animatorMaoDireita = animator;
            else if (!direita && animatorMaoEsquerda == null)
                animatorMaoEsquerda = animator;
        }
    }

    private void AplicarEstadosDosAnimators()
    {
        pegadaDireitaAtiva = selecoesDireita > 0;
        pegadaEsquerdaAtiva = selecoesEsquerda > 0;
        mirarDireitaAtiva = selecoesMirarDireita > 0;
        mirarEsquerdaAtiva = selecoesMirarEsquerda > 0;

        AplicarEstados(animatorMaoDireita, pegadaDireitaAtiva, mirarDireitaAtiva, "direita", out statusMaoDireita);
        AplicarEstados(animatorMaoEsquerda, pegadaEsquerdaAtiva, mirarEsquerdaAtiva, "esquerda", out statusMaoEsquerda);
    }

    private void AplicarEstados(Animator animator, bool pegadaAtiva, bool mirarAtiva, string lado, out string status)
    {
        if (animator == null)
        {
            status = $"Animator da mão {lado} não atribuído.";
            return;
        }

        bool temPegada = TemParametroBool(animator, hashParametroPegada);
        bool temMirar = TemParametroBool(animator, hashParametroMirar);

        if (temPegada)
            animator.SetBool(hashParametroPegada, pegadaAtiva);

        if (temMirar)
            animator.SetBool(hashParametroMirar, mirarAtiva);

        if (!temPegada || !temMirar)
        {
            string ausente = !temPegada && !temMirar
                ? $"'{parametroPegada}' e '{parametroMirar}'"
                : !temPegada ? $"'{parametroPegada}'" : $"'{parametroMirar}'";
            status = $"Bool {ausente} não encontrado na mão {lado}.";
            return;
        }

        if (mirarAtiva)
            status = "Mirar ativada pela mão da corda.";
        else if (pegadaAtiva)
            status = "Pegada ativada pela mão do arco.";
        else
            status = "Mão aberta.";
    }

    private static void AplicarEstadosSemComponenteNaCena()
    {
        int hashPegada = Animator.StringToHash("Pegada");
        int hashMirar = Animator.StringToHash("Mirar");
        Animator[] animators = FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (!TentarDetectarLado(animator.transform, out bool direita))
                continue;

            if (TemParametroBool(animator, hashPegada))
                animator.SetBool(hashPegada, direita ? selecoesDireita > 0 : selecoesEsquerda > 0);

            if (TemParametroBool(animator, hashMirar))
                animator.SetBool(hashMirar, direita ? selecoesMirarDireita > 0 : selecoesMirarEsquerda > 0);
        }
    }

    private static bool TentarDetectarLado(Transform atual, out bool direita)
    {
        direita = false;

        while (atual != null)
        {
            string nome = atual.name.Trim().ToLowerInvariant();
            if (nome.Contains("right") || nome.Contains("direita"))
            {
                direita = true;
                return true;
            }

            if (nome.Contains("left") || nome.Contains("esquerda"))
                return true;

            atual = atual.parent;
        }

        return false;
    }

    private static bool TemParametroBool(Animator animator, int hashParametro)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return false;

        AnimatorControllerParameter[] parametros = animator.parameters;
        for (int i = 0; i < parametros.Length; i++)
        {
            AnimatorControllerParameter parametro = parametros[i];
            if (parametro.nameHash == hashParametro && parametro.type == AnimatorControllerParameterType.Bool)
                return true;
        }

        return false;
    }
}
