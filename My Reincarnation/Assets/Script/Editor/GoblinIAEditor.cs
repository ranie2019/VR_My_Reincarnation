using UnityEditor;

[CustomEditor(typeof(GoblinIA))]
public sealed class GoblinIAEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

        Bloco("Identidade e progressão", "nomeMonstro", "textoNomeMonstro", "nivelMonstro", "textoNivel");
        Bloco("Vida e barra de vida", "vidaMaxima", "vidaAtual", "tempoAnimacaoDano", "tempoParaMorrer",
            "imagemBarraVida", "canvasVida", "buscarBarraVidaAutomaticamente", "esconderCanvasAoMorrer");
        Bloco("Movimento", "rb", "usarNavMeshAgent", "velocidadePatrulha", "velocidadePerseguicao");
        Bloco("Patrulha", "pontosPatrulha", "distanciaChegadaPonto", "tempoObservando",
            "gerarPontosAutomaticamente", "quantidadePontosAuto", "raioPatrulhaAuto");
        Bloco("Detecção", "tagPlayer", "raioCampoVisao", "anguloCampoVisao", "layersObstaculo",
            "distanciaIniciarPerseguicao", "alturaOlhos", "pontoOlhos");
        Bloco("Combate", "distanciaAtaque", "danoAtaque", "pontoOrigemAtaque", "alturaOrigemAtaque",
            "raioAtaque", "alcanceAtaque", "layersAtaque", "atrasoImpactoAtaque", "cooldownAtaque");
        Bloco("Animação", "animacao");
        Bloco("Áudio", "audioSource", "somAndar", "somAtacar", "somDano", "somMorrer",
            "volumeAndar", "volumeAtacar", "volumeDano", "volumeMorrer");
        Bloco("Respawn", "idRespawnMonstro");
        Bloco("Drops", "prefabsSpawnNormal", "prefabsSpawnMissao");

        serializedObject.ApplyModifiedProperties();
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            EditorGUILayout.LabelField("XP ao morrer", ((GoblinIA)target).ExperienciaAoMorrer.ToString());

        Bloco("Recompensa REIN", "darReinAoMorrer", "reinMinimo", "reinMaximo", "casasDecimaisSorteioRein");
        Bloco("Gizmos", "desenharGizmos", "desenharGizmosSempre");
        serializedObject.ApplyModifiedProperties();
    }

    private void Bloco(string titulo, params string[] campos) =>
        FlechaEditor.DesenharBloco(serializedObject, titulo, campos);
}
