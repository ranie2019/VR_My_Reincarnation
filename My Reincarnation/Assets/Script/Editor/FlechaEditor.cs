#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Flecha))]
[CanEditMultipleObjects]
public sealed class FlechaEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

        DesenharClasses(serializedObject, "Classes, ataque e usos", "usosMinimos", "usosMaximos");
        DesenharBloco("Usos no popup", "textoUsosAtuais", "textoUsosMaximos");
        DesenharBloco("Atributos no popup", "textosValores");
        DesenharBloco("Restrição", "atributoRestricao", "valorMinimoParaUsar", "grupoRestricao",
            "textoAtributoRestricao", "textoValorRestricao", "textoMensagemRestricao");
        DesenharBloco("Popup", "popup", "nomeExibicao", "descricao", "textoNome", "textoClasse", "textoDescricao",
            "tempoPopup", "posicaoPopupNaCamera", "rotacaoPopupNaCamera", "espacamentoPopups");
        DesenharBloco("Direção e colisão", "traseiraDirecao", "pontaDirecao", "tagsIgnoradasColisao",
            "ignorarDonoDaFlecha", "ignorarTerrain", "tagsQueRecebemDano", "causarDanoUmaVez");
        DesenharBloco("Áudio", "somColisao", "volumeSomColisao");
        DesenharBloco("Efeitos", "efeitoAoCriar", "parentarEfeitoCriacaoNaFlecha", "efeitoAoColidir",
            "tempoDestruirEfeitoColisao");
        serializedObject.ApplyModifiedProperties();
    }

    internal static void DesenharClasses(SerializedObject serializedObject, string titulo, params string[] camposDeUso)
        => DesenharClasses(serializedObject, titulo, false, camposDeUso);

    internal static void DesenharClasses(SerializedObject serializedObject, string titulo, bool defesa, params string[] camposDeUso)
        => DesenharClasses(serializedObject, titulo, defesa
            ? new[] { "defesaMinima", "defesaMaxima", "textoDefesa" }
            : new[] { "ataqueMinimo", "ataqueMaximo", "textoAtaque" }, camposDeUso);

    internal static void DesenharClasses(SerializedObject serializedObject, string titulo, string[] camposPoder, params string[] camposDeUso)
    {
        var classes = serializedObject.FindProperty("classes");
        EditorGUILayout.Space();
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(titulo, EditorStyles.boldLabel);
            if (!serializedObject.isEditingMultipleObjects && classes.arraySize > 0)
            {
                float total = 0f;
                for (int i = 0; i < classes.arraySize; i++) total += LerChance(classes, i);
                if (Mathf.Abs(total - 100f) > 0.0001f)
                    RedistribuirChances(classes);
            }

            for (int i = 0; i < classes.arraySize; i++)
            {
                var classe = classes.GetArrayElementAtIndex(i);
                var tipo = classe.FindPropertyRelative("classe");
                classe.isExpanded = EditorGUILayout.Foldout(classe.isExpanded,
                    tipo.enumDisplayNames[tipo.enumValueIndex], true);
                if (!classe.isExpanded) continue;

                using (new EditorGUI.IndentLevelScope())
                {
                    var chance = classe.FindPropertyRelative("chance");
                    using (new EditorGUI.DisabledScope(serializedObject.isEditingMultipleObjects))
                    {
                        EditorGUI.BeginChangeCheck();
                        EditorGUILayout.PropertyField(chance, new GUIContent("Chance (%)"));
                        if (EditorGUI.EndChangeCheck()) RedistribuirChances(classes, i);
                    }
                    DesenharCamposClasse(classe, camposPoder);
                    DesenharCamposClasse(classe, camposDeUso);
                    DesenharCamposClasse(classe, "quantidadeMinima", "quantidadeMaxima", "atributos");
                }
            }
        }
    }

    private static void DesenharCamposClasse(SerializedProperty classe, params string[] campos)
    {
        foreach (string campo in campos)
            EditorGUILayout.PropertyField(classe.FindPropertyRelative(campo), includeChildren: true);
    }

    private static float LerChance(SerializedProperty classes, int indice)
    {
        float chance = classes.GetArrayElementAtIndex(indice).FindPropertyRelative("chance").floatValue;
        return float.IsNaN(chance) || float.IsInfinity(chance) ? 0f : Mathf.Clamp(chance, 0f, 100f);
    }

    private static void RedistribuirChances(SerializedProperty classes, int indiceEditado = -1)
    {
        int quantidade = classes.arraySize;
        float restante = indiceEditado >= 0 ? 100f - LerChance(classes, indiceEditado) : 100f;
        if (quantidade == 1)
        {
            classes.GetArrayElementAtIndex(0).FindPropertyRelative("chance").floatValue = 100f;
            return;
        }

        float total = 0f;
        int destino = -1;
        for (int i = 0; i < quantidade; i++)
        {
            if (i == indiceEditado) continue;
            float chance = LerChance(classes, i);
            total += chance;
            if (destino < 0 || chance > LerChance(classes, destino)) destino = i;
        }

        float saldo = restante;
        for (int i = 0; i < quantidade; i++)
        {
            if (i == indiceEditado || i == destino) continue;
            float chance = total > 0f ? restante * LerChance(classes, i) / total : 0f;
            saldo -= chance;
            classes.GetArrayElementAtIndex(i).FindPropertyRelative("chance").floatValue = chance;
        }
        if (destino >= 0)
            classes.GetArrayElementAtIndex(destino).FindPropertyRelative("chance").floatValue = Mathf.Max(0f, saldo);
    }

    private void DesenharBloco(string titulo, params string[] campos) =>
        DesenharBloco(serializedObject, titulo, campos);

    internal static void DesenharBloco(SerializedObject serializedObject, string titulo, params string[] campos)
    {
        EditorGUILayout.Space();
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(titulo, EditorStyles.boldLabel);
            foreach (string campo in campos)
                EditorGUILayout.PropertyField(serializedObject.FindProperty(campo), includeChildren: true);
        }
    }
}

[CustomEditor(typeof(Espada))]
[CanEditMultipleObjects]
public sealed class EspadaEditor : Editor
{
    public override void OnInspectorGUI() => DesenharArma(serializedObject, false);

    internal static void DesenharArma(SerializedObject serializedObject, bool arco)
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

        FlechaEditor.DesenharClasses(serializedObject, "Classes, ataque, durabilidade e atributos",
            new[] { "danoMinimo", "danoMaximo", "textoAtaque" },
            "vidaMinima", "vidaMaxima", "textoDurabilidadeAtual", "textoDurabilidadeMaxima");
        FlechaEditor.DesenharBloco(serializedObject, "Desgaste e quebra",
            "destruirAoZerarDurabilidade", arco ? "desgastePorDisparo" : "desgastePorDanoCausado");
        FlechaEditor.DesenharBloco(serializedObject, "Restrição",
            "atributoRestricao", "valorMinimoParaUsar",
            "grupoRestricao", "textoRotuloRestricao", "textoAtributoRestricao", "textoValorRestricao",
            "textoMensagemRestricao", "rotuloRestricaoPopup", "mensagemAbaixoMinimo");
        FlechaEditor.DesenharBloco(serializedObject, "Popup", "popup", "prefabObjeto", "descricao",
            "textoNome", "textoClasse", "textoDescricao", "textosValores", "cameraJogador",
            "posicaoPopupNaCamera", "rotacaoPopupNaCamera", "espacamentoHorizontalPopups", "tempoParaFecharPopup");
        if (arco)
            FlechaEditor.DesenharBloco(serializedObject, "Áudio da quebra", "somQuebra", "volumeQuebra");
        else
            FlechaEditor.DesenharBloco(serializedObject, "Áudio", "audioSource", "somHit", "somQuebra",
                "volumeHit", "volumeQuebra", "cooldownSomHit");

        serializedObject.ApplyModifiedProperties();
    }
}

[CustomEditor(typeof(EquiparElmo))]
[CanEditMultipleObjects]
public sealed class EquiparElmoEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

        FlechaEditor.DesenharClasses(serializedObject, "Classes e atributos", camposPoder: System.Array.Empty<string>());
        FlechaEditor.DesenharBloco(serializedObject, "Durabilidade", "vidaMaxima", "vidaAtual",
            "desgasteNaturalPorMinuto", "textoDurabilidadeAtual", "textoDurabilidadeTotal");
        FlechaEditor.DesenharBloco(serializedObject, "Restrição", "atributoRestricao", "valorMinimoParaUsar",
            "grupoRestricao", "textoAtributoRestricao", "textoValorRestricao", "textoMensagemRestricao");
        FlechaEditor.DesenharBloco(serializedObject, "Equipamento", "cabecaAvatar", "pontoEquipamento",
            "posicaoLocalNaCabeca", "rotacaoLocalNaCabeca");
        FlechaEditor.DesenharBloco(serializedObject, "Visão local em VR", "ocultarParaCameraLocalQuandoEquipado",
            "cameraLocalProprietario", "diagnosticoOcultacaoLocal");
        FlechaEditor.DesenharBloco(serializedObject, "Popup", "popupInfo", "prefabObjeto", "nomeExibicao", "descricao",
            "textoNome", "textoClasse", "textoDescricao", "textosValores", "cameraJogador", "tempoParaFecharPopup",
            "posicaoPopupNaCamera", "rotacaoPopupNaCamera", "manterPopupNaFrenteDaCamera", "espacamentoHorizontalPopups");
        serializedObject.ApplyModifiedProperties();
    }
}

[CustomEditor(typeof(Machado))]
[CanEditMultipleObjects]
public sealed class MachadoEditor : Editor
{
    public override void OnInspectorGUI() => InspectorFerramenta.Desenhar(serializedObject, false);
}

[CustomEditor(typeof(Picareta))]
[CanEditMultipleObjects]
public sealed class PicaretaEditor : Editor
{
    public override void OnInspectorGUI() => InspectorFerramenta.Desenhar(serializedObject, true);
}

internal static class InspectorFerramenta
{
    internal static void Desenhar(SerializedObject dados, bool picareta)
    {
        dados.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(dados.FindProperty("m_Script"));

        FlechaEditor.DesenharClasses(dados, "Classes, ataque, durabilidade e atributos",
            "durabilidadeMinima", "durabilidadeMaxima", "textoDurabilidadeAtual", "textoDurabilidadeMaxima");
        FlechaEditor.DesenharBloco(dados, "Desgaste e quebra", "destruirQuandoVidaZerar", "desgastePorDano");
        FlechaEditor.DesenharBloco(dados, "Atributos no popup", "textosValores");
        FlechaEditor.DesenharBloco(dados, "Restrição", "atributoRestricao", "valorMinimoParaUsar", "grupoRestricao",
            "textoAtributoRestricao", "textoValorRestricao", "textoMensagemRestricao");
        FlechaEditor.DesenharBloco(dados, "Popup", "popup", "prefabObjeto", "nomeExibicao", "descricao", "textoNome",
            "textoClasse", "textoDescricao", "cameraJogador", "posicaoPopupNaCamera", "rotacaoPopupNaCamera",
            "espacamentoHorizontalPopups", "tempoParaFecharPopup");

        if (picareta)
            FlechaEditor.DesenharBloco(dados, "Áudio", "audioSource", "somPedra", "somMetal", "somOutros", "somQuebra",
                "volumePedra", "volumeMetal", "volumeOutros", "volumeQuebra", "cooldownSomColisao");
        else
            FlechaEditor.DesenharBloco(dados, "Áudio", "audioSource", "somCorte", "somOutros", "somQuebra",
                "volumeCorte", "volumeOutros", "volumeQuebra", "cooldownSomColisao");

        dados.ApplyModifiedProperties();
    }
}
[CustomEditor(typeof(Escudo))]
[CanEditMultipleObjects]
public sealed class EscudoEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

        FlechaEditor.DesenharClasses(serializedObject, "Classes, defesa, durabilidade e atributos", true,
            "durabilidadeMinima", "durabilidadeMaxima", "textoDurabilidadeAtual", "textoDurabilidadeMaxima");
        FlechaEditor.DesenharBloco(serializedObject, "Bloqueio e desgaste",
            "destruirQuandoVidaZerar", "desgastePorDano");
        FlechaEditor.DesenharBloco(serializedObject, "Restrição", "atributoRestricao", "valorMinimoParaUsar",
            "grupoRestricao", "textoAtributoRestricao", "textoValorRestricao", "textoMensagemRestricao");
        FlechaEditor.DesenharBloco(serializedObject, "Popup", "popup", "prefabObjeto", "descricao",
            "textoNome", "textoClasse", "textoDescricao", "textosValores",
            "cameraJogador", "posicaoPopupNaCamera", "rotacaoPopupNaCamera", "espacamentoHorizontalPopups", "tempoParaFecharPopup");
        FlechaEditor.DesenharBloco(serializedObject, "Áudio", "audioSource", "somBloqueio", "somQuebrar",
            "volumeBloqueio", "volumeQuebrar");
        serializedObject.ApplyModifiedProperties();
    }
}
#endif
