using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// <see cref="TextIdDropdownAttribute"/> 가 붙은 string 필드를 <see cref="GameText"/> 표의
/// ID 목록 드롭다운으로 표시합니다. 오타 대신 목록에서 고르게 하고,
/// 이미 지정된 값이 표에 없으면 "Missing:" 으로 경고합니다.
/// (사운드 ID 드롭다운(<see cref="SoundIdDropdownDrawer"/>)과 같은 방식입니다.)
/// </summary>
[CustomPropertyDrawer(typeof(TextIdDropdownAttribute))]
public sealed class TextIdDropdownDrawer : PropertyDrawer
{
    private const string NoneLabel = "<None>";
    private const string MissingPrefix = "Missing: ";

    // <None>/Missing 줄이 붙은 목록. 드로어는 필드마다 하나씩 살아 있으므로 여기 쥐고 재사용한다 —
    // OnGUI 는 인스펙터가 다시 그려질 때마다 돌아서, 매번 새로 만들면 계속 쌓인다.
    private string[] _options;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        string currentValue = property.stringValue;
        string[] ids = GameText.AllIds();

        // 지정하지 않은 값과, 표에서 사라진 값 모두 목록 맨 위에 자기 자리를 갖는다 —
        // 그러지 않으면 드롭다운을 여는 순간 엉뚱한 ID 로 조용히 바뀐다.
        string extraRow = null;
        if (string.IsNullOrEmpty(currentValue)) extraRow = NoneLabel;
        else if (!Contains(ids, currentValue)) extraRow = MissingPrefix + currentValue;

        string[] options = ids;
        if (extraRow != null)
        {
            if (_options == null || _options.Length != ids.Length + 1) _options = new string[ids.Length + 1];
            _options[0] = extraRow;
            ids.CopyTo(_options, 1);
            options = _options;
        }

        int selectedIndex = 0;
        for (int i = 0; i < options.Length; i++)
        {
            if (options[i] != currentValue) continue;
            selectedIndex = i;
            break;
        }

        EditorGUI.BeginProperty(position, label, property);
        EditorGUI.BeginChangeCheck();
        int nextIndex = EditorGUI.Popup(position, label.text, selectedIndex, options);
        if (EditorGUI.EndChangeCheck())
        {
            string selectedValue = options[nextIndex];
            if (!selectedValue.StartsWith(MissingPrefix, StringComparison.Ordinal))
            {
                property.stringValue = selectedValue == NoneLabel ? string.Empty : selectedValue;
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
