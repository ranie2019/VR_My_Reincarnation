using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SelecionadorFlechasUI : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private GameObject painelSelecaoFlechas;
    [SerializeField] private Transform conteudoLista;
    [SerializeField] private GameObject prefabLinhaFlecha;
    [SerializeField] private InventarioFlechas inventarioFlechas;
    [SerializeField] private Arco arco;

    [Header("Textos")]
    [SerializeField] private TMP_Text textoFlechaEquipada;
    [SerializeField] private TMP_Text textoVazio;

    public void Alternar()
    {
        Alternar(arco, inventarioFlechas);
    }

    public void Alternar(Arco arcoOrigem, InventarioFlechas inventarioOrigem)
    {
        if (arcoOrigem != null)
            arco = arcoOrigem;

        if (inventarioOrigem != null)
            inventarioFlechas = inventarioOrigem;

        if (painelSelecaoFlechas == null)
            return;

        if (painelSelecaoFlechas.activeSelf)
            Fechar();
        else
            Abrir();
    }

    public void Abrir()
    {
        AtualizarLista();

        if (painelSelecaoFlechas != null)
            painelSelecaoFlechas.SetActive(true);
    }

    public void Fechar()
    {
        if (painelSelecaoFlechas != null)
            painelSelecaoFlechas.SetActive(false);
    }

    public void AtualizarLista()
    {
        if (inventarioFlechas == null)
        {
            InventarioFlechas[] inventarios = FindObjectsByType<InventarioFlechas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            if (inventarios != null && inventarios.Length > 0)
                inventarioFlechas = inventarios[0];
        }

        LimparLista();
        AtualizarTextoFlechaEquipada();

        List<InventarioFlechas.FlechaInventarioInfo> flechas = inventarioFlechas != null
            ? inventarioFlechas.ObterFlechasDisponiveis()
            : null;

        bool possuiFlechas = flechas != null && flechas.Count > 0;
        if (textoVazio != null)
            textoVazio.gameObject.SetActive(!possuiFlechas);

        if (!possuiFlechas || conteudoLista == null || prefabLinhaFlecha == null)
            return;

        for (int i = 0; i < flechas.Count; i++)
            CriarLinhaFlecha(flechas[i]);
    }

    private void CriarLinhaFlecha(InventarioFlechas.FlechaInventarioInfo info)
    {
        if (info == null)
            return;

        GameObject linha = Instantiate(prefabLinhaFlecha, conteudoLista);
        linha.SetActive(true);

        TMP_Text[] textos = linha.GetComponentsInChildren<TMP_Text>(true);
        if (textos.Length > 0)
            textos[0].text = $"{info.nomeExibicao} x{info.quantidade}";

        Image imagem = linha.GetComponentInChildren<Image>(true);
        if (imagem != null && info.icone != null)
        {
            imagem.sprite = info.icone;
            imagem.enabled = true;
        }

        Button botao = linha.GetComponentInChildren<Button>(true);
        if (botao == null)
            botao = linha.GetComponent<Button>();

        if (botao == null)
            return;

        string id = info.idTipoFlecha;
        botao.onClick.RemoveAllListeners();
        botao.onClick.AddListener(() =>
        {
            if (arco != null)
                arco.EquiparTipoFlecha(id);

            AtualizarTextoFlechaEquipada();
            Fechar();
        });
    }

    private void LimparLista()
    {
        if (conteudoLista == null)
            return;

        Transform modelo = prefabLinhaFlecha != null ? prefabLinhaFlecha.transform : null;
        for (int i = conteudoLista.childCount - 1; i >= 0; i--)
        {
            Transform filho = conteudoLista.GetChild(i);
            if (filho != null && filho != modelo)
                Destroy(filho.gameObject);
        }
    }

    private void AtualizarTextoFlechaEquipada()
    {
        if (textoFlechaEquipada == null)
            return;

        if (arco == null)
        {
            textoFlechaEquipada.text = "Flecha: nenhuma";
            return;
        }

        string id = arco.ObterIdTipoFlechaEquipada();
        int quantidade = arco.ObterQuantidadeFlechaEquipada();
        textoFlechaEquipada.text = string.IsNullOrWhiteSpace(id)
            ? "Flecha: nenhuma"
            : $"Flecha: {id} x{quantidade}";
    }
}
