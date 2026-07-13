using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class ArmaAttachDuasMao : XRGrabInteractable
{
    private enum LadoMao
    {
        Desconhecida,
        Direita,
        Esquerda
    }

    [Header("Attach de Segurar")]
    [SerializeField] private Transform attachSegurarDireita;
    [SerializeField] private Transform attachSegurarEsquerda;

    [Header("Referências opcionais das mãos")]
    [SerializeField] private Transform referenciaMaoDireita;
    [SerializeField] private Transform referenciaMaoEsquerda;

    [Header("Estado")]
    [SerializeField] private bool seguradoPelaMaoDireita;
    [SerializeField] private bool seguradoPelaMaoEsquerda;
    [SerializeField] private bool arcoEstaSegurado;
    [SerializeField] private Transform interactorMaoSegurando;
    [SerializeField] private Transform interactorMaoLivreParaPuxar;

    protected override void Awake()
    {
        base.Awake();
        ConfigurarComportamentoBase();
        CorrigirParentInteractableAutoReferencia();
    }

    protected override void Reset()
    {
        base.Reset();
        ConfigurarComportamentoBase();
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);

        if (InteractorEhSocketOuInventario(args.interactorObject))
        {
            LimparEstadoDuasMaos();
            DefinirModoInventarioArco(true);
            return;
        }

        DefinirModoInventarioArco(false);
        RegistrarMaoQueSegura(args.interactorObject);
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        Transform interactorSaindo = ObterTransformInteractor(args.interactorObject);

        base.OnSelectExited(args);

        if (InteractorEhSocketOuInventario(args.interactorObject))
        {
            if (!ExisteMaoSelecionando())
            {
                LimparEstadoDuasMaos();
                DefinirModoInventarioArco(true);
            }
            else
            {
                DefinirModoInventarioArco(false);
            }

            return;
        }

        if (interactorMaoSegurando == null || InteractorEhMaoSegurando(interactorSaindo))
            LimparEstadoDuasMaos();

        if (ExisteSocketOuInventarioSelecionando())
            DefinirModoInventarioArco(true);
        else
            DefinirModoInventarioArco(false);
    }

    protected override void OnDisable()
    {
        LimparEstadoDuasMaos();
        base.OnDisable();
    }

    protected override void OnEnable()
    {
        CorrigirParentInteractableAutoReferencia();
        base.OnEnable();
    }

    public override Transform GetAttachTransform(IXRInteractor interactor)
    {
        if (!InteractorEhMao(interactor))
            return base.GetAttachTransform(interactor);

        LadoMao ladoMao = DetectarLadoMao(interactor);

        if (ladoMao == LadoMao.Direita && attachSegurarDireita != null)
            return attachSegurarDireita;

        if (ladoMao == LadoMao.Esquerda && attachSegurarEsquerda != null)
            return attachSegurarEsquerda;

        return base.GetAttachTransform(interactor);
    }

    public override bool IsSelectableBy(IXRSelectInteractor interactor)
    {
        if (!base.IsSelectableBy(interactor))
            return false;

        if (IsSelected(interactor))
            return true;

        if (InteractorEhSocketOuInventario(interactor))
            return true;

        if (!InteractorEhMao(interactor))
            return !arcoEstaSegurado && !isSelected;

        if (!arcoEstaSegurado)
            return true;

        Transform interactorTransform = ObterTransformInteractor(interactor);
        return InteractorEhMaoSegurando(interactorTransform);
    }

    public bool DetectarSeInteractorEhMaoDireitaOuEsquerda(IXRInteractor interactor, out bool maoDireita)
    {
        if (!InteractorEhMao(interactor))
        {
            maoDireita = false;
            return false;
        }

        LadoMao ladoMao = DetectarLadoMao(interactor);
        maoDireita = ladoMao == LadoMao.Direita;
        return ladoMao != LadoMao.Desconhecida;
    }

    public bool EstaSeguradoPelaDireita()
    {
        return arcoEstaSegurado && seguradoPelaMaoDireita;
    }

    public bool EstaSeguradoPelaEsquerda()
    {
        return arcoEstaSegurado && seguradoPelaMaoEsquerda;
    }

    public bool ArcoEstaSegurado()
    {
        return arcoEstaSegurado;
    }

    public Transform ObterMaoQueSegura()
    {
        return arcoEstaSegurado ? interactorMaoSegurando : null;
    }

    public Transform ObterMaoQuePodePuxar()
    {
        return arcoEstaSegurado ? interactorMaoLivreParaPuxar : null;
    }

    public bool MaoPodePuxarCorda(Transform mao)
    {
        if (!arcoEstaSegurado || mao == null)
            return false;

        LadoMao ladoMao = DetectarLadoMao(mao);

        if (seguradoPelaMaoDireita)
            return ladoMao == LadoMao.Esquerda;

        if (seguradoPelaMaoEsquerda)
            return ladoMao == LadoMao.Direita;

        return false;
    }

    private void RegistrarMaoQueSegura(IXRInteractor interactor)
    {
        if (!InteractorEhMao(interactor))
        {
            LimparEstadoDuasMaos();
            return;
        }

        Transform interactorTransform = ObterTransformInteractor(interactor);
        LadoMao ladoMao = DetectarLadoMao(interactorTransform);

        if (ladoMao == LadoMao.Desconhecida)
        {
            LimparEstadoDuasMaos();
            return;
        }

        arcoEstaSegurado = true;
        interactorMaoSegurando = interactorTransform;
        seguradoPelaMaoDireita = ladoMao == LadoMao.Direita;
        seguradoPelaMaoEsquerda = ladoMao == LadoMao.Esquerda;
        interactorMaoLivreParaPuxar = ObterReferenciaMaoOposta(ladoMao);
    }

    private Transform ObterReferenciaMaoOposta(LadoMao ladoMaoSegurando)
    {
        if (ladoMaoSegurando == LadoMao.Direita)
            return referenciaMaoEsquerda != null ? referenciaMaoEsquerda : EncontrarMaoNaCena(interactorMaoSegurando, LadoMao.Esquerda);

        if (ladoMaoSegurando == LadoMao.Esquerda)
            return referenciaMaoDireita != null ? referenciaMaoDireita : EncontrarMaoNaCena(interactorMaoSegurando, LadoMao.Direita);

        return null;
    }

    private void ConfigurarComportamentoBase()
    {
        useDynamicAttach = false;
        selectMode = InteractableSelectMode.Multiple;
        throwOnDetach = false;
    }

    private void DefinirModoInventarioArco(bool noInventario)
    {
        Arco arco = GetComponent<Arco>();
        if (arco != null)
            arco.DefinirModoInventario(noInventario);
    }

    private bool InteractorEhMao(IXRInteractor interactor)
    {
        if (interactor == null || InteractorEhSocketOuInventario(interactor))
            return false;

        if (interactor is XRDirectInteractor || interactor is XRRayInteractor)
            return true;

        Transform interactorTransform = ObterTransformInteractor(interactor);
        return DetectarLadoMao(interactorTransform) != LadoMao.Desconhecida ||
               TransformTemNomeDeMaoOuControle(interactorTransform);
    }

    private bool InteractorEhSocketOuInventario(IXRInteractor interactor)
    {
        if (interactor == null)
            return false;

        if (interactor is XRSocketInteractor)
            return true;

        Transform atual = ObterTransformInteractor(interactor);
        while (atual != null)
        {
            string nome = NormalizarNome(atual.name);

            if (nome.Contains("socket") ||
                nome.Contains("slot") ||
                nome.Contains("inventario") ||
                nome.Contains("inventory") ||
                nome.Contains("pontoencaixe") ||
                nome.Contains("ponto_encaixe"))
                return true;

            atual = atual.parent;
        }

        return false;
    }

    private bool ExisteMaoSelecionando()
    {
        for (int i = 0; i < interactorsSelecting.Count; i++)
        {
            if (InteractorEhMao(interactorsSelecting[i]))
                return true;
        }

        return false;
    }

    private bool ExisteSocketOuInventarioSelecionando()
    {
        for (int i = 0; i < interactorsSelecting.Count; i++)
        {
            if (InteractorEhSocketOuInventario(interactorsSelecting[i]))
                return true;
        }

        return false;
    }

    private bool TransformTemNomeDeMaoOuControle(Transform origem)
    {
        Transform atual = origem;

        while (atual != null)
        {
            if (NomePareceControleMao(atual.name))
                return true;

            atual = atual.parent;
        }

        return false;
    }

    private Transform EncontrarMaoNaCena(Transform origem, LadoMao ladoMao)
    {
        if (origem == null)
            return null;

        Transform raiz = origem;
        while (raiz.parent != null)
            raiz = raiz.parent;

        return ProcurarMaoNaHierarquia(raiz, ladoMao);
    }

    private Transform ProcurarMaoNaHierarquia(Transform raiz, LadoMao ladoMao)
    {
        if (raiz == null)
            return null;

        if (DetectarLadoMao(raiz) == ladoMao && NomePareceControleMao(raiz.name))
            return raiz;

        for (int i = 0; i < raiz.childCount; i++)
        {
            Transform encontrado = ProcurarMaoNaHierarquia(raiz.GetChild(i), ladoMao);
            if (encontrado != null)
                return encontrado;
        }

        return null;
    }

    private static bool NomePareceControleMao(string nome)
    {
        string normalizado = NormalizarNome(nome);
        return normalizado.Contains("controller") || normalizado.Contains("controlador") || normalizado.Contains("hand") || normalizado.Contains("mao");
    }

    private LadoMao DetectarLadoMao(IXRInteractor interactor)
    {
        return DetectarLadoMao(ObterTransformInteractor(interactor));
    }

    private LadoMao DetectarLadoMao(Transform origem)
    {
        Transform atual = origem;

        while (atual != null)
        {
            string nome = NormalizarNome(atual.name);

            if (nome.Contains("left") || nome.Contains("esquerda"))
                return LadoMao.Esquerda;

            if (nome.Contains("right") || nome.Contains("direita"))
                return LadoMao.Direita;

            atual = atual.parent;
        }

        return LadoMao.Desconhecida;
    }

    private static Transform ObterTransformInteractor(IXRInteractor interactor)
    {
        return (interactor as MonoBehaviour)?.transform;
    }

    private void CorrigirParentInteractableAutoReferencia()
    {
        autoFindParentInteractableInHierarchy = false;

        if (ReferenceEquals(parentInteractable, (IXRInteractable)this))
            parentInteractable = null;

        XRBaseInteractable[] interactables = GetComponentsInChildren<XRBaseInteractable>(true);
        for (int i = 0; i < interactables.Length; i++)
        {
            XRBaseInteractable interactable = interactables[i];
            if (interactable == null)
                continue;

            if (ReferenceEquals(interactable.parentInteractable, (IXRInteractable)interactable))
                interactable.parentInteractable = null;
        }
    }

    private bool InteractorEhMaoSegurando(Transform interactorTransform)
    {
        if (interactorTransform == null || interactorMaoSegurando == null)
            return false;

        return interactorTransform == interactorMaoSegurando ||
               interactorTransform.IsChildOf(interactorMaoSegurando) ||
               interactorMaoSegurando.IsChildOf(interactorTransform);
    }

    private void LimparEstadoDuasMaos()
    {
        seguradoPelaMaoDireita = false;
        seguradoPelaMaoEsquerda = false;
        arcoEstaSegurado = false;
        interactorMaoSegurando = null;
        interactorMaoLivreParaPuxar = null;
    }

    private static string NormalizarNome(string nome)
    {
        return string.IsNullOrWhiteSpace(nome)
            ? string.Empty
            : nome.Trim().ToLowerInvariant();
    }
}
