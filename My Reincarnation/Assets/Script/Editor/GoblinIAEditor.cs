using UnityEditor;

[CustomEditor(typeof(GoblinIA))]
public sealed class GoblinIAEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.LabelField("XP ao morrer", ((GoblinIA)target).ExperienciaAoMorrer.ToString());
    }
}
