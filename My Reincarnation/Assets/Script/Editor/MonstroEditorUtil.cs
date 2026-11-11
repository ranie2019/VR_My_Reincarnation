using UnityEditor;
using UnityEngine;

/// <summary>Desenho do bloco de chances de drop, compartilhado pelos editores dos monstros.</summary>
internal static class MonstroEditorUtil
{
    internal static void DesenharDrop(SerializedObject so, string titulo, string campoItens, string campoNada)
    {
        var itens = so.FindProperty(campoItens);
        var nada = so.FindProperty(campoNada);
        if (itens == null || nada == null) return;

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
                    ? DropChances.Sanitizar(item.FindPropertyRelative("chance").floatValue)
                    : float.NaN;
            }

            chances[quantidade] = nada.floatValue;
            DropChances.Distribuir(chances);

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
            DropChances.Distribuir(chances, indiceEditado);

            for (int i = 0; i < quantidade; i++)
                itens.GetArrayElementAtIndex(i).FindPropertyRelative("chance").floatValue = chances[i];
            nada.floatValue = chances[quantidade];
        }
    }
}
