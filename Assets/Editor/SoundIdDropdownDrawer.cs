using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// <see cref="SoundIdDropdownAttribute"/> 가 붙은 string 필드를
/// 코드 상수(<see cref="SfxId"/>, <see cref="BgmId"/>) 목록 드롭다운으로 표시합니다.
/// 오타 대신 목록에서 고르게 하고, 이미 지정된 값이 목록에 없으면 "Missing:" 으로 경고합니다.
/// </summary>
[CustomPropertyDrawer(typeof(SoundIdDropdownAttribute))]
public sealed class SoundIdDropdownDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        SoundIdDropdownAttribute dropdown = (SoundIdDropdownAttribute)attribute;
        IReadOnlyList<string> ids = SoundIdCatalog.GetIds(dropdown.Kind);
        string currentValue = property.stringValue;

        List<string> options = new();
        if (string.IsNullOrEmpty(currentValue))
        {
            options.Add("<None>");
        }
        else if (!Contains(ids, currentValue))
        {
            options.Add($"Missing: {currentValue}");
        }

        for (int i = 0; i < ids.Count; i++)
        {
            options.Add(ids[i]);
        }

        if (options.Count == 0)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        int selectedIndex = 0;
        if (!string.IsNullOrEmpty(currentValue) && Contains(options, currentValue))
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i] != currentValue) continue;
                selectedIndex = i;
                break;
            }
        }

        EditorGUI.BeginProperty(position, label, property);
        EditorGUI.BeginChangeCheck();
        int nextIndex = EditorGUI.Popup(position, label.text, selectedIndex, options.ToArray());
        if (EditorGUI.EndChangeCheck())
        {
            string selectedValue = options[nextIndex];
            if (!selectedValue.StartsWith("Missing: "))
            {
                property.stringValue = selectedValue == "<None>" ? string.Empty : selectedValue;
            }
        }
        EditorGUI.EndProperty();
    }

    private static bool Contains(IReadOnlyList<string> values, string target)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (values[i] == target) return true;
        }

        return false;
    }
}
