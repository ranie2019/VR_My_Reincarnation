using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DisallowMultipleComponent]
public class ExperienciaInimigo : MonoBehaviour
{
    [Header("Experiencia")]
    [SerializeField] private int experienciaAoMorrer = 10;

    private StatusPlayer primeiroAtacante;
    private bool primeiroAtacanteDefinido;
    private bool experienciaEntregue;

    private static readonly string[] MetodosStatusDono =
    {
        "GetStatusPlayerDonoAtual",
        "GetStatusPlayerDono",
        "GetStatusPlayerOwner"
    };

    private static readonly string[] MetodosObjetoDono =
    {
        "GetDonoAtual",
        "GetDono",
        "GetOwner",
        "ObterDonoAtual"
    };

    private static readonly string[] PropriedadesDono =
    {
        "DonoAtual",
        "Dono",
        "Owner",
        "StatusPlayerDonoAtual"
    };

    public int ExperienciaAoMorrer => experienciaAoMorrer;
    public bool ExperienciaEntregue => experienciaEntregue;
    public bool PrimeiroAtacanteDefinido => primeiroAtacanteDefinido;
    public StatusPlayer PrimeiroAtacante => primeiroAtacante;

    private void OnValidate()
    {
        experienciaAoMorrer = Mathf.Max(0, experienciaAoMorrer);
    }

    public void RegistrarPrimeiroAtacante(GameObject origemDano)
    {
        if (primeiroAtacanteDefinido || origemDano == null)
            return;

        StatusPlayer statusPlayer = ResolverStatusPlayerDaOrigem(origemDano);
        if (statusPlayer == null)
            return;

        primeiroAtacante = statusPlayer;
        primeiroAtacanteDefinido = true;
    }

    public void EntregarExperiencia()
    {
        if (experienciaEntregue)
            return;

        bool entregue = false;

        if (primeiroAtacante != null)
            entregue = EntregarParaStatusPlayer(primeiroAtacante, true);

        if (!entregue)
        {
            StatusPlayer fallback = EncontrarStatusPlayerFallback();
            entregue = EntregarParaStatusPlayer(fallback, false);
        }

        if (entregue)
            experienciaEntregue = true;
    }

    public void EntregarExperiencia(GameObject jogador)
    {
        if (experienciaEntregue || jogador == null)
            return;

        StatusPlayer statusPlayer = ObterStatusPlayerEmGameObject(jogador);
        if (EntregarParaStatusPlayer(statusPlayer, false))
            experienciaEntregue = true;
    }

    public void EntregarExperienciaParaPlayerMaisProximo()
    {
        if (experienciaEntregue)
            return;

        StatusPlayer fallback = EncontrarStatusPlayerFallback();
        if (EntregarParaStatusPlayer(fallback, false))
            experienciaEntregue = true;
    }

    private bool EntregarParaStatusPlayer(StatusPlayer statusPlayer, bool usarGrupoDoPrimeiroAtacante)
    {
        if (statusPlayer == null)
            return false;

        if (experienciaAoMorrer <= 0)
            return true;

        if (usarGrupoDoPrimeiroAtacante && TentarEntregarParaGrupo(statusPlayer))
            return true;

        statusPlayer.ReceberExperiencia(experienciaAoMorrer);
        return true;
    }

    private bool TentarEntregarParaGrupo(StatusPlayer atacante)
    {
        GrupoPlayer grupo = atacante.GetComponent<GrupoPlayer>();
        if (grupo == null)
            grupo = atacante.GetComponentInParent<GrupoPlayer>();

        if (grupo == null || !grupo.DeveDividirExperienciaComGrupo() || !grupo.TemGrupoValido())
            return false;

        List<StatusPlayer> membros = grupo.GetMembrosValidos();
        if (!membros.Contains(atacante))
            membros.Insert(0, atacante);

        if (membros.Count == 0)
            return false;

        int experienciaPorMembro = experienciaAoMorrer / membros.Count;
        int resto = experienciaAoMorrer % membros.Count;

        for (int i = 0; i < membros.Count; i++)
        {
            if (experienciaPorMembro > 0)
                membros[i].ReceberExperiencia(experienciaPorMembro);
        }

        if (resto > 0)
            atacante.ReceberExperiencia(resto);

        return true;
    }

    private StatusPlayer EncontrarStatusPlayerFallback()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            StatusPlayer statusPlayer = ObterStatusPlayerEmGameObject(player);
            if (statusPlayer != null)
                return statusPlayer;
        }

        return FindFirstObjectByType<StatusPlayer>();
    }

    private StatusPlayer ResolverStatusPlayerDaOrigem(GameObject origemDano)
    {
        StatusPlayer statusPlayer = ObterStatusPlayerEmGameObject(origemDano);
        if (statusPlayer != null)
            return statusPlayer;

        Transform origem = origemDano.transform;

        if (TentarObterStatusPlayerPorDonoDeclarado(origem, out statusPlayer))
            return statusPlayer;

        if (TentarObterStatusPlayerPorXRGrab(origem, out statusPlayer))
            return statusPlayer;

        return null;
    }

    private StatusPlayer ObterStatusPlayerEmGameObject(GameObject alvo)
    {
        if (alvo == null)
            return null;

        StatusPlayer statusPlayer = alvo.GetComponent<StatusPlayer>();
        if (statusPlayer != null)
            return statusPlayer;

        statusPlayer = alvo.GetComponentInParent<StatusPlayer>();
        if (statusPlayer != null)
            return statusPlayer;

        Transform root = alvo.transform.root;
        return root != null ? root.GetComponentInChildren<StatusPlayer>(true) : null;
    }

    private bool TentarObterStatusPlayerPorDonoDeclarado(Transform origem, out StatusPlayer statusPlayer)
    {
        statusPlayer = null;
        if (origem == null)
            return false;

        List<Component> componentes = ColetarComponentesDaOrigem(origem);
        for (int i = 0; i < componentes.Count; i++)
        {
            Component componente = componentes[i];
            if (componente == null)
                continue;

            if (TentarObterStatusPlayerPorMetodo(componente, out statusPlayer))
                return true;

            if (TentarObterStatusPlayerPorPropriedade(componente, out statusPlayer))
                return true;
        }

        return false;
    }

    private List<Component> ColetarComponentesDaOrigem(Transform origem)
    {
        List<Component> componentes = new List<Component>();
        HashSet<Component> visitados = new HashSet<Component>();

        AdicionarComponentes(origem.GetComponentsInParent<Component>(true), componentes, visitados);
        AdicionarComponentes(origem.GetComponentsInChildren<Component>(true), componentes, visitados);

        return componentes;
    }

    private void AdicionarComponentes(Component[] origem, List<Component> destino, HashSet<Component> visitados)
    {
        if (origem == null)
            return;

        for (int i = 0; i < origem.Length; i++)
        {
            Component componente = origem[i];
            if (componente != null && visitados.Add(componente))
                destino.Add(componente);
        }
    }

    private bool TentarObterStatusPlayerPorMetodo(Component componente, out StatusPlayer statusPlayer)
    {
        statusPlayer = null;
        Type tipo = componente.GetType();

        for (int i = 0; i < MetodosStatusDono.Length; i++)
        {
            MethodInfo metodo = tipo.GetMethod(MetodosStatusDono[i], BindingFlags.Instance | BindingFlags.Public);
            if (metodo == null || metodo.GetParameters().Length != 0)
                continue;

            if (TentarResolverStatusPlayerDeValor(InvocarMetodoSeguro(metodo, componente), out statusPlayer))
                return true;
        }

        for (int i = 0; i < MetodosObjetoDono.Length; i++)
        {
            MethodInfo metodo = tipo.GetMethod(MetodosObjetoDono[i], BindingFlags.Instance | BindingFlags.Public);
            if (metodo == null || metodo.GetParameters().Length != 0)
                continue;

            if (TentarResolverStatusPlayerDeValor(InvocarMetodoSeguro(metodo, componente), out statusPlayer))
                return true;
        }

        return false;
    }

    private object InvocarMetodoSeguro(MethodInfo metodo, Component componente)
    {
        try
        {
            return metodo.Invoke(componente, null);
        }
        catch
        {
            return null;
        }
    }

    private bool TentarObterStatusPlayerPorPropriedade(Component componente, out StatusPlayer statusPlayer)
    {
        statusPlayer = null;
        Type tipo = componente.GetType();

        for (int i = 0; i < PropriedadesDono.Length; i++)
        {
            PropertyInfo propriedade = tipo.GetProperty(PropriedadesDono[i], BindingFlags.Instance | BindingFlags.Public);
            if (propriedade == null || propriedade.GetIndexParameters().Length != 0)
                continue;

            try
            {
                if (TentarResolverStatusPlayerDeValor(propriedade.GetValue(componente), out statusPlayer))
                    return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private bool TentarResolverStatusPlayerDeValor(object valor, out StatusPlayer statusPlayer)
    {
        statusPlayer = null;

        switch (valor)
        {
            case StatusPlayer status:
                statusPlayer = status;
                return true;
            case GameObject obj:
                statusPlayer = ObterStatusPlayerEmGameObject(obj);
                return statusPlayer != null;
            case Transform transformValor:
                statusPlayer = ObterStatusPlayerEmGameObject(transformValor.gameObject);
                return statusPlayer != null;
            case Component componente:
                statusPlayer = ObterStatusPlayerEmGameObject(componente.gameObject);
                return statusPlayer != null;
            default:
                return false;
        }
    }

    private bool TentarObterStatusPlayerPorXRGrab(Transform origem, out StatusPlayer statusPlayer)
    {
        statusPlayer = null;
        if (origem == null)
            return false;

        List<XRGrabInteractable> interactables = new List<XRGrabInteractable>();
        AdicionarInteractableSeExistir(origem.GetComponentInParent<XRGrabInteractable>(), interactables);

        XRGrabInteractable[] filhos = origem.GetComponentsInChildren<XRGrabInteractable>(true);
        for (int i = 0; i < filhos.Length; i++)
            AdicionarInteractableSeExistir(filhos[i], interactables);

        for (int i = 0; i < interactables.Count; i++)
        {
            XRGrabInteractable interactable = interactables[i];
            for (int j = 0; j < interactable.interactorsSelecting.Count; j++)
            {
                Transform interactorTransform = ObterTransformInteractor(interactable.interactorsSelecting[j]);
                if (interactorTransform == null)
                    continue;

                statusPlayer = ObterStatusPlayerEmGameObject(interactorTransform.gameObject);
                if (statusPlayer != null)
                    return true;
            }
        }

        return false;
    }

    private void AdicionarInteractableSeExistir(XRGrabInteractable interactable, List<XRGrabInteractable> lista)
    {
        if (interactable != null && !lista.Contains(interactable))
            lista.Add(interactable);
    }

    private Transform ObterTransformInteractor(IXRSelectInteractor interactor)
    {
        return (interactor as MonoBehaviour)?.transform;
    }
}
