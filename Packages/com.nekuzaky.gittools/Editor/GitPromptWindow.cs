using System;
using UnityEditor;
using UnityEngine;

namespace GitTools.EditorTools
{
    /// <summary>Small modal text prompt: branch names, tag names, stash messages.</summary>
    public class GitPromptWindow : EditorWindow
    {
        string m_Title = "";
        string m_Label = "";
        string m_Value = "";
        string m_ConfirmLabel = "OK";
        Action<string> m_OnConfirm;
        bool m_FocusRequested;

        public static void Show(string title, string label, string defaultValue, string confirmLabel, Action<string> onConfirm)
        {
            var window = CreateInstance<GitPromptWindow>();
            window.m_Title = title;
            window.m_Label = label;
            window.m_Value = defaultValue ?? "";
            window.m_ConfirmLabel = confirmLabel;
            window.m_OnConfirm = onConfirm;
            window.titleContent = new GUIContent(title);

            var size = new Vector2(360f, 108f);
            var mouse = GUIUtility.GUIToScreenPoint(Event.current != null ? Event.current.mousePosition : Vector2.zero);
            window.position = new Rect(mouse.x - size.x * 0.5f, mouse.y, size.x, size.y);
            window.ShowModalUtility();
        }

        void OnGUI()
        {
            GUILayout.Space(8f);
            GUILayout.Label(m_Title, GitStyles.Title);
            GUILayout.Space(4f);

            GUI.SetNextControlName("GitPromptField");
            m_Value = EditorGUILayout.TextField(m_Label, m_Value);

            if (!m_FocusRequested)
            {
                m_FocusRequested = true;
                EditorGUI.FocusTextInControl("GitPromptField");
            }

            var confirmed = false;
            var cancelled = false;

            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) confirmed = true;
                if (Event.current.keyCode == KeyCode.Escape) cancelled = true;
            }

            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel", GUILayout.Width(90f))) cancelled = true;

                using (new EditorGUI.DisabledScope(m_Value.Trim().Length == 0))
                {
                    if (GUILayout.Button(m_ConfirmLabel, GUILayout.Width(120f))) confirmed = true;
                }
            }
            GUILayout.Space(6f);

            if (cancelled)
            {
                Close();
                return;
            }

            if (confirmed && m_Value.Trim().Length > 0)
            {
                var value = m_Value.Trim();
                var callback = m_OnConfirm;
                Close();
                if (callback != null) callback(value);
            }
        }
    }
}
