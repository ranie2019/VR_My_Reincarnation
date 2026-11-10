using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Snapshot somente em memoria. Nunca serializado no save do jogador.
internal sealed class RecuperacaoItemCarga
{
    private readonly List<Action> restauracoes = new List<Action>();
    internal RecuperacaoItemCarga(Component item)
    {
        foreach (Transform t in item.GetComponentsInChildren<Transform>(true))
        {
            Transform alvo = t, pai = t.parent;
            int indice = t.GetSiblingIndex();
            Vector3 pos = t.localPosition, escala = t.localScale;
            Quaternion rot = t.localRotation;
            bool ativo = t.gameObject.activeSelf;
            restauracoes.Add(() => { if (alvo == null) return; alvo.SetParent(pai, false);
                alvo.localPosition = pos; alvo.localRotation = rot; alvo.localScale = escala;
                alvo.SetSiblingIndex(indice); alvo.gameObject.SetActive(ativo); });
        }
        foreach (MonoBehaviour c in item.GetComponentsInChildren<MonoBehaviour>(true))
        {
            // Preserva dados serializados e referencias dos componentes existentes.
            MonoBehaviour alvo = c;
            if (alvo == null) continue;
            string json = JsonUtility.ToJson(alvo);
            bool habilitado = alvo.enabled;
            restauracoes.Add(() => { if (alvo != null) { JsonUtility.FromJsonOverwrite(json, alvo); alvo.enabled = habilitado; } });
        }
        foreach (Renderer c in item.GetComponentsInChildren<Renderer>(true))
        { Renderer alvo = c; bool ativo = c.enabled; restauracoes.Add(() => { if (alvo != null) alvo.enabled = ativo; }); }
        foreach (Collider c in item.GetComponentsInChildren<Collider>(true))
        { Collider alvo = c; bool ativo = c.enabled; restauracoes.Add(() => { if (alvo != null) alvo.enabled = ativo; }); }
        foreach (Rigidbody c in item.GetComponentsInChildren<Rigidbody>(true))
        {
            Rigidbody alvo = c; bool k = c.isKinematic, g = c.useGravity, d = c.detectCollisions, dormindo = c.IsSleeping();
            var constraints = c.constraints; var modo = c.collisionDetectionMode; var interpolacao = c.interpolation;
            Vector3 v = c.linearVelocity, w = c.angularVelocity;
            restauracoes.Add(() => { if (alvo == null) return; alvo.isKinematic = k; alvo.useGravity = g;
                alvo.detectCollisions = d; alvo.constraints = constraints; alvo.collisionDetectionMode = modo;
                alvo.interpolation = interpolacao; if (!k) { alvo.linearVelocity = v; alvo.angularVelocity = w; }
                if (dormindo) alvo.Sleep(); else alvo.WakeUp(); });
        }
        // Equipado e callbacks de restauracao sao estado runtime, nao JSON.
        var luva = item.GetComponent<EquiparManoplas>();
        if (luva != null) restauracoes.Add(luva.CapturarEstadoEquipadoParaCarga());
        var elmo = item.GetComponent<EquiparElmo>();
        if (elmo != null) restauracoes.Add(elmo.CapturarEstadoEquipadoParaCarga());
        var peitoral = item.GetComponent<EquiparPeitoral>();
        if (peitoral != null) restauracoes.Add(peitoral.CapturarEstadoEquipadoParaCarga());
        var machado = item.GetComponent<Machado>();
        if (machado != null) restauracoes.Add(machado.CapturarSorteioParaCarga());
        var picareta = item.GetComponent<Picareta>();
        if (picareta != null) restauracoes.Add(picareta.CapturarSorteioParaCarga());
        var escudo = item.GetComponent<Escudo>();
        if (escudo != null) restauracoes.Add(escudo.CapturarSorteioParaCarga());
        var cogumelo = item.GetComponent<Cocumelo>();
        if (cogumelo != null) restauracoes.Add(cogumelo.CapturarRecuperacaoRespawn());
        var persistente = item.GetComponent<ItemPersistente>();
        if (persistente != null) restauracoes.Add(persistente.CapturarIdentidadeRuntime());
        var grab = item.GetComponent<XRGrabInteractable>();
        if (grab != null)
        {
            Vector3 escala = grab.GetTargetLocalScale();
            var seletores = new List<IXRSelectInteractor>(grab.interactorsSelecting);
            restauracoes.Add(() => {
                if (grab == null) return;
                grab.SetTargetLocalScale(escala);
                var manager = grab.interactionManager;
                if (manager == null || !grab.isActiveAndEnabled) return;
                foreach (var seletor in seletores)
                    if (seletor != null && manager.IsRegistered(seletor) && manager.IsRegistered((IXRInteractable)grab) &&
                        !grab.interactorsSelecting.Contains(seletor)) manager.SelectEnter(seletor, grab);
            });
        }
    }
    internal void Restaurar() { foreach (Action restaurar in restauracoes) restaurar(); }
}
