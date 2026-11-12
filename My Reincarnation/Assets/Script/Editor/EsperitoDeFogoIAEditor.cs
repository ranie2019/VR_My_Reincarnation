using UnityEditor;

[CustomEditor(typeof(EsperitoDeFogoIA))]
public sealed class EsperitoDeFogoIAEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

        Bloco("Vida e barra de vida", "vidaMaxima", "vidaAtual", "nivelMonstro", "textoNivel",
            "barraVida", "canvasVida", "buscarBarraVidaAutomaticamente", "esconderCanvasAoMorrer");
        Bloco("Patrulha", "pontosPatrulha", "velocidadePatrulha", "distanciaChegadaPonto", "tempoObservando");
        Bloco("Detecção", "tagPlayer", "distanciaVisao", "anguloVisao", "layerObstaculos", "tempoMemoriaJogador");
        Bloco("Combate", "velocidadePerseguicao", "distanciaAtaque", "cooldownAtaque", "danoAtaque", "tempoAnimacaoAtaque");
        Bloco("Morte", "tempoParaDestruir");

        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.LabelField("XP ao morrer", ((EsperitoDeFogoIA)target).ExperienciaAoMorrer.ToString());
    }

    private void Bloco(string titulo, params string[] campos) =>
        FlechaEditor.DesenharBloco(serializedObject, titulo, campos);
}
