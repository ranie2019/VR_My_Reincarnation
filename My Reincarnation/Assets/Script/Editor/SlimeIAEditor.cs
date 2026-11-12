using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SlimeIA))]
public sealed class SlimeIAEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

        DesenharBloco("Identidade e progressão", "nomeMonstro", "textoNomeMonstro", "nivelMonstro", "textoNivel", "atributosPorNivel");
        DesenharBloco("Vida e barra de vida", "vidaMaxima", "vidaAtual", "tempoAnimacaoDano",
            "tempoParaMorrer", "imagemBarraVida", "canvasVida", "buscarBarraVidaAutomaticamente", "esconderCanvasAoMorrer");
        DesenharBloco("Movimento", "rb", "usarNavMeshAgent", "velocidadePatrulha",
            "velocidadePerseguicao", "configuracaoMovimento");
        DesenharBloco("Patrulha", "pontosPatrulha", "distanciaChegadaPonto", "tempoObservando",
            "gerarPontosAutomaticamente", "quantidadePontosAuto", "raioPatrulhaAuto");
        DesenharBloco("Detecção", "tagPlayer", "raioCampoVisao", "anguloCampoVisao", "layersObstaculo",
            "distanciaIniciarPerseguicao", "alturaOlhos", "pontoOlhos", "configuracaoDeteccao");
        DesenharBloco("Combate", "distanciaAtaque", "danoAtaque", "pontoOrigemAtaque", "alturaOrigemAtaque",
            "raioAtaque", "alcanceAtaque", "layersAtaque");
        DesenharBloco("Investida", "distanciaInvestida", "alturaInvestida", "duracaoInvestida", "cooldownAtaque",
            "raioVerificacaoColisaoInvestida", "layersBloqueioInvestida", "margemParedeInvestida");
        DesenharBloco("Animação", "configuracaoAnimacao");
        DesenharBloco("Áudio", "audioSource", "somAndar", "somAtacar", "somDano", "somMorrer",
            "volumeAndar", "volumeAtacar", "volumeDano", "volumeMorrer");
        DesenharBloco("Respawn", "idRespawnMonstro");

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Drops", EditorStyles.boldLabel);
            DesenharDrop("Chances do drop normal", "prefabsSpawnNormal", "chanceNada");
            DesenharDrop("Chances do drop de missão", "prefabsSpawnMissao", "chanceNadaMissao");
        }
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            EditorGUILayout.LabelField("XP ao morrer", ((SlimeIA)target).ExperienciaAoMorrer.ToString());
        DesenharBloco("Recompensa REIN", "darReinAoMorrer", "reinMinimo", "reinMaximo", "casasDecimaisSorteioRein");
        DesenharBloco("Gizmos", "desenharGizmos", "desenharGizmosSempre");

        serializedObject.ApplyModifiedProperties();
    }

    private void DesenharBloco(string titulo, params string[] campos) =>
        FlechaEditor.DesenharBloco(serializedObject, titulo, campos);

    private void DesenharDrop(string titulo, string campoItens, string campoNada)
    {
        var itens = serializedObject.FindProperty(campoItens);
        var nada = serializedObject.FindProperty(campoNada);
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(itens, includeChildren: true);
        bool itensAlterados = EditorGUI.EndChangeCheck();

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(titulo, "Total: 100%", EditorStyles.boldLabel);
            int quantidade = itens.arraySize;
            var chances = new float[quantidade + 1];
            var prefabs = new Object[quantidade];
            for (int i = 0; i < quantidade; i++)
            {
                var item = itens.GetArrayElementAtIndex(i);
                prefabs[i] = item.FindPropertyRelative("prefab").objectReferenceValue;
                chances[i] = prefabs[i]
                    ? SlimeIA.SanitizarChanceDrop(item.FindPropertyRelative("chance").floatValue)
                    : float.NaN;
            }

            chances[quantidade] = nada.floatValue;
            SlimeIA.DistribuirChancesDrop(chances);

            int indiceEditado = -1;
            for (int linha = 0; linha <= quantidade; linha++)
            {
                bool ehNada = linha == 0;
                int i = ehNada ? quantidade : linha - 1;
                Object prefab = ehNada ? null : prefabs[i];
                string rotulo = ehNada ? "Nada" : (prefab ? prefab.name : "Sem prefab") + $" [{i + 1}]";

                using (new EditorGUI.DisabledScope(!ehNada && !prefab))
                {
                    EditorGUI.BeginChangeCheck();
                    chances[i] = EditorGUILayout.Slider(rotulo, chances[i], 0f, 100f);
                    if (EditorGUI.EndChangeCheck())
                        indiceEditado = i;
                }
            }

            if (indiceEditado < 0 && !itensAlterados)
                return;

            for (int i = 0; i < quantidade; i++)
                if (!prefabs[i])
                    chances[i] = float.NaN;
            SlimeIA.DistribuirChancesDrop(chances, indiceEditado);

            for (int i = 0; i < quantidade; i++)
                itens.GetArrayElementAtIndex(i).FindPropertyRelative("chance").floatValue = chances[i];
            nada.floatValue = chances[quantidade];
        }
    }
}
