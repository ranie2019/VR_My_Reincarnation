#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(CajadoPopup))]
[CanEditMultipleObjects]
public class CajadoPopupEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

        FlechaEditor.DesenharClasses(serializedObject, "Classes, ataque, durabilidade e atributos",
            new[] { "danoMinimo", "danoMaximo", "textoAtaque" },
            "vidaMinima", "vidaMaxima", "textoDurabilidadeAtual", "textoDurabilidadeMaxima");
        FlechaEditor.DesenharBloco(serializedObject, "Restrição",
            "atributoRestricaoMagica", "valorMinimoParaUsar", "grupoRestricao", "textoRotuloRestricao", "textoAtributoRestricao",
            "textoValorRestricao", "textoMensagemRestricao", "rotuloRestricaoPopup", "mensagemAbaixoMinimo");
        FlechaEditor.DesenharBloco(serializedObject, "Desgaste e quebra",
            "destruirQuandoVidaZerar", "desgastePorMagiaLancada");
        FlechaEditor.DesenharBloco(serializedObject, "Popup", "popup", "prefabObjeto", "descricao",
            "textoNome", "textoClasse", "textoDescricao", "textosValores", "cameraJogador",
            "posicaoPopupNaCamera", "rotacaoPopupNaCamera", "espacamentoHorizontalPopups", "tempoParaFecharPopup");
        FlechaEditor.DesenharBloco(serializedObject, "Áudio da quebra", "audioSource", "somQuebra", "volumeQuebra");
        serializedObject.ApplyModifiedProperties();
    }
}
// Mantem os IDs serializados antigos, mas nao oferece atributos fora do catalogo do jogo.
[CustomPropertyDrawer(typeof(TipoAtributoManopla))]
public class TipoAtributoEquipamentoDrawer : PropertyDrawer
{
    public override void OnGUI(UnityEngine.Rect position, SerializedProperty property, UnityEngine.GUIContent label)
    {
        var valores = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 15 };
        var nomes = new[] { "Forca", "Constituicao", "Agilidade", "Velocidade", "Inteligencia", "Espirito", "Destreza", "Vigor", "Percepcao", "Resistencia", "Carisma", "Sorte", "MP (Mana)", "HP (Vida)" };
        EditorGUI.BeginProperty(position, label, property);
        int atual = System.Array.IndexOf(valores, property.intValue);
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        int escolhido = EditorGUI.Popup(position, label.text, atual, nomes);
        if (EditorGUI.EndChangeCheck() && escolhido >= 0) property.intValue = valores[escolhido];
        EditorGUI.showMixedValue = false;
        EditorGUI.EndProperty();
    }
}
#endif
