using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TopeiraIA), true)]
public sealed class TopeiraIAEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

        if (target is RaposaIA)
            DesenharBloco("Comportamento da raposa", "chanceAtaqueCauda", "chanceSentar");

        DesenharBloco("Vida e barra de vida", "vidaMaxima", "vidaAtual", "nivelMonstro", "tempoAnimacaoDano",
            "tempoParaMorrer", "imagemBarraVida", "canvasVida", "textoNivel",
            "buscarBarraVidaAutomaticamente", "esconderCanvasAoMorrer");
        DesenharBloco("Movimento", "rb", "ajustarCapsuleAoModelo", "velocidadePatrulha", "velocidadePerseguicao",
            "velocidadeRotacao", "camadasSolo", "inclinacaoMaxima", "alturaSondaSolo", "toleranciaSolo",
            "quedaMaxima", "distanciaPassoSeguro", "tempoRotaBloqueada", "deslocamentoMinimo", "distanciaParada");
        DesenharBloco("Patrulha", "pontosPatrulha", "distanciaChegadaPonto", "tempoObservando",
            "gerarPontosAutomaticamente", "quantidadePontosAuto", "raioPatrulhaAuto");
        DesenharBloco("Detecção", "tagPlayer", "raioCampoVisao", "anguloCampoVisao", "layersObstaculo",
            "distanciaIniciarPerseguicao", "tempoMemoriaAlvo", "alturaOlhos", "pontoOlhos");
        DesenharBloco("Combate", "distanciaAtaque", "danoAtaque", "pontoOrigemAtaque", "alturaOrigemAtaque",
            "raioAtaque", "alcanceAtaque", "layersAtaque");
        DesenharBloco("Investida", "distanciaInvestida", "alturaInvestida", "duracaoInvestida", "atrasoInvestida",
            "duracaoAnimacaoAtaque", "cooldownAtaque", "raioVerificacaoColisaoInvestida",
            "layersBloqueioInvestida", "margemParedeInvestida");
        DesenharBloco("Animação", "animator");
        DesenharBloco("Áudio", "audioSource", "somAndar", "somAtacar", "somDano", "somMorrer",
            "volumeAndar", "volumeAtacar", "volumeDano", "volumeMorrer");
        DesenharBloco("Respawn", "idRespawnMonstro");

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Drops", EditorStyles.boldLabel);
            MonstroEditorUtil.DesenharDrop(serializedObject, "Chances do drop normal", "prefabsSpawnNormal", "chanceNada");
            MonstroEditorUtil.DesenharDrop(serializedObject, "Chances do drop de missão", "prefabsSpawnMissao", "chanceNadaMissao");
        }
        DesenharBloco("Recompensa REIN", "darReinAoMorrer", "reinMinimo", "reinMaximo", "casasDecimaisSorteioRein");
        DesenharBloco("Gizmos", "desenharGizmos", "desenharGizmosSempre");

        serializedObject.ApplyModifiedProperties();
    }

    private void DesenharBloco(string titulo, params string[] campos) =>
        FlechaEditor.DesenharBloco(serializedObject, titulo, campos);
}
