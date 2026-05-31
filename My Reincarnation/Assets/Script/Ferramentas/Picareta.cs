using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;

[DisallowMultipleComponent]
public class Picareta : MonoBehaviour
{
    [Header("Dano da picareta")]
    [SerializeField] private int danoPicareta = 1;

    [Header("Impacto opcional")]
    [SerializeField] private float impactForce = 0f;

    [Header("Tags aceitas")]
    [SerializeField] private string[] tagsAceitas = { "Rock", "Pedra", "Metal" };

    private Rigidbody rb;

    private readonly HashSet<Transform> mineraisDentroDoTrigger = new();
    private readonly Dictionary<Transform, int> contatosPorMineral = new();

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void OnDisable()
    {
        mineraisDentroDoTrigger.Clear();
        contatosPorMineral.Clear();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null)
            return;

        TentarAplicarDano(collision.collider);

        if (impactForce > 0f && rb != null)
            AplicarForcaImpacto(collision);
    }

    private void OnTriggerEnter(Collider other)
    {
        TentarAplicarDanoPorEntradaTrigger(other);
    }

    private void OnTriggerStay(Collider other)
    {
        // Nao aplica dano continuo.
        // Novo dano so depois de sair e entrar novamente no trigger.
    }

    private void OnTriggerExit(Collider other)
    {
        Transform mineral = BuscarMineral(other);

        if (mineral == null)
            return;

        if (!contatosPorMineral.TryGetValue(mineral, out int contatos))
            return;

        contatos--;

        if (contatos > 0)
        {
            contatosPorMineral[mineral] = contatos;
            return;
        }

        contatosPorMineral.Remove(mineral);
        mineraisDentroDoTrigger.Remove(mineral);
    }

    private void TentarAplicarDanoPorEntradaTrigger(Collider other)
    {
        Transform mineral = BuscarMineral(other);

        if (mineral == null)
            return;

        if (contatosPorMineral.TryGetValue(mineral, out int contatos))
            contatosPorMineral[mineral] = contatos + 1;
        else
            contatosPorMineral.Add(mineral, 1);

        if (!mineraisDentroDoTrigger.Add(mineral))
            return;

        TentarAplicarDanoFlexivel(mineral);
    }

    private void TentarAplicarDano(Collider other)
    {
        Transform mineral = BuscarMineral(other);

        if (mineral == null)
            return;

        TentarAplicarDanoFlexivel(mineral);
    }

    private Transform BuscarMineral(Collider alvo)
    {
        if (alvo == null)
            return null;

        Transform mineral = ObterTransformComTagMineral(alvo.transform);
        if (mineral != null)
            return mineral;

        Rigidbody alvoRb = alvo.attachedRigidbody;
        if (alvoRb != null)
        {
            mineral = ObterTransformComTagMineral(alvoRb.transform);
            if (mineral != null)
                return mineral;
        }

        Transform root = alvo.transform.root;
        if (root != null && TemTagMineral(root))
            return root;

        return null;
    }

    private bool TemTagMineral(Transform alvo)
    {
        return ObterTransformComTagMineral(alvo) != null;
    }

    private Transform ObterTransformComTagMineral(Transform alvo)
    {
        Transform atual = alvo;

        while (atual != null)
        {
            if (TemTagAceita(atual.tag))
                return atual;

            atual = atual.parent;
        }

        return null;
    }

    private bool TentarAplicarDanoFlexivel(Transform mineral)
    {
        if (mineral == null)
            return false;

        VidaRecursoMineral recurso = mineral.GetComponentInParent<VidaRecursoMineral>();
        if (recurso == null)
            recurso = mineral.GetComponentInChildren<VidaRecursoMineral>();

        if (recurso == null)
            return false;

        return TentarChamarMetodoDano(recurso);
    }

    private bool TentarChamarMetodoDano(VidaRecursoMineral recurso)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
        string[] nomesMetodos =
        {
            "ReceberDanoDePicareta",
            "ReceberDano",
            "TomarDano",
            "AplicarDano",
            "ReceberDanoDeFerramenta"
        };

        for (int i = 0; i < nomesMetodos.Length; i++)
        {
            if (TentarChamarMetodoDano(recurso, nomesMetodos[i], flags))
                return true;
        }

        return false;
    }

    private bool TentarChamarMetodoDano(VidaRecursoMineral recurso, string nomeMetodo, BindingFlags flags)
    {
        MethodInfo[] metodos = recurso.GetType().GetMethods(flags);

        for (int i = 0; i < metodos.Length; i++)
        {
            MethodInfo metodo = metodos[i];
            if (!string.Equals(metodo.Name, nomeMetodo, StringComparison.Ordinal))
                continue;

            ParameterInfo[] parametros = metodo.GetParameters();

            if (parametros.Length == 2 &&
                parametros[0].ParameterType == typeof(int) &&
                parametros[1].ParameterType == typeof(GameObject))
            {
                metodo.Invoke(recurso, new object[] { danoPicareta, gameObject });
                return true;
            }

            if (parametros.Length == 1 && parametros[0].ParameterType == typeof(int))
            {
                metodo.Invoke(recurso, new object[] { danoPicareta });
                return true;
            }
        }

        return false;
    }

    private static bool TagIgual(string tagAtual, string tagEsperada)
    {
        return !string.IsNullOrWhiteSpace(tagAtual) &&
               !string.IsNullOrWhiteSpace(tagEsperada) &&
               string.Equals(tagAtual, tagEsperada, StringComparison.OrdinalIgnoreCase);
    }

    private void AplicarForcaImpacto(Collision collision)
    {
        if (collision.contactCount == 0)
            return;

        Vector3 direcaoImpacto = collision.contacts[0].normal;
        rb.AddForce(-direcaoImpacto * impactForce, ForceMode.Impulse);
    }

    private bool TemTagAceita(string tagAtual)
    {
        if (tagsAceitas == null || tagsAceitas.Length == 0)
            return false;

        for (int i = 0; i < tagsAceitas.Length; i++)
        {
            if (TagIgual(tagAtual, tagsAceitas[i]))
                return true;
        }

        return false;
    }
}
