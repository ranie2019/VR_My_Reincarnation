#if UNITY_EDITOR
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(Arco))]
[CanEditMultipleObjects]
public sealed class ArcoEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Usa IMGUI para evitar a atualização agendada das listas do Inspector
        // padrão quando o objeto serializado é substituído na transição de Play.
        serializedObject.Update();
        var ocultos = new List<string>();
        bool todosUsamPopup = true;
        foreach (var objeto in targets)
        {
            if (objeto is Arco arco && arco.GetComponent<Arcopopup>() != null) continue;
            todosUsamPopup = false;
            break;
        }
        if (todosUsamPopup)
            ocultos.AddRange(new[] { "durabilidadeMaxima", "durabilidadeAtual", "textoValorAtual",
                "textoValorTotal", "destruirAoQuebrar", "desativarAoQuebrar", "corTextoA",
                "corTextoB", "velocidadePiscarTexto", "usarEfeitoLedTexto" });

        var teste = serializedObject.FindProperty("permitirDisparoSemFlechaParaTeste");
        if (!teste.hasMultipleDifferentValues && !teste.boolValue) ocultos.Add("prefabFlecha");
        DrawPropertiesExcluding(serializedObject, ocultos.ToArray());
        serializedObject.ApplyModifiedProperties();
    }
}

[CustomEditor(typeof(Arcopopup))]
[CanEditMultipleObjects]
public sealed class ArcoPopupEditor : Editor
{
    public override void OnInspectorGUI() => EspadaEditor.DesenharArma(serializedObject, true);
}
#endif
