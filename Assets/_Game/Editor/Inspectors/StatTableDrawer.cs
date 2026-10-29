using Game.Core;
using Game.Data;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Shows a <see cref="StatTable"/> as one labelled float field per <see cref="StatType"/>.</summary>
    [CustomPropertyDrawer(typeof(StatTable))]
    public sealed class StatTableDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            property.isExpanded ? (StatBlock.Count + 1) * (EditorGUIUtility.singleLineHeight + 2) : EditorGUIUtility.singleLineHeight;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var values = property.FindPropertyRelative("values");
            if (values.arraySize != StatBlock.Count) values.arraySize = StatBlock.Count;
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);
            if (!property.isExpanded) return;
            EditorGUI.indentLevel++;
            for (var i = 0; i < StatBlock.Count; i++)
            {
                line.y += EditorGUIUtility.singleLineHeight + 2;
                EditorGUI.PropertyField(line, values.GetArrayElementAtIndex(i), new GUIContent(((StatType)i).ToString()));
            }
            EditorGUI.indentLevel--;
        }
    }
}
