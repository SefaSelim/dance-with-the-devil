using UnityEditor;
using UnityEngine;

namespace DanceWithTheDevil.Editor
{
    /// <summary>
    /// Tuning UI for <see cref="WeaponHold"/>. In Play mode, select Defne's "Body" (or press the button on the
    /// player): the Scene view shows a move/rotate handle on the weapon in her hands and one on the left-hand point;
    /// dragging them writes the active weapon's <see cref="WeaponHoldProfile"/> asset (so it survives leaving Play
    /// mode). "Edit" chooses whether the weapon handle changes the base grip (every clip) or only a correction for
    /// the clip playing most right now (e.g. run). Tip: pause (Ctrl+Shift+P) and step frames while tuning a clip.
    /// </summary>
    [CustomEditor(typeof(WeaponHold))]
    public sealed class WeaponHoldEditor : UnityEditor.Editor
    {
        private enum EditTarget { BaseGrip, CurrentClipCorrection }

        private static EditTarget _target = EditTarget.BaseGrip;
        private static bool _weaponHandle = true;
        private static bool _leftHandHandle = true;
        private UnityEditor.Editor _profileEditor;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var hold = (WeaponHold)target;
            WeaponHoldProfile profile = hold.ActiveProfile;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tuning", EditorStyles.boldLabel);
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play mode to tune with handles in the Scene view. The profile fields below " +
                                        "can be edited any time.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField("Weapon in hands", hold.ActiveWeapon != null ? hold.ActiveWeapon.name : "none");
                EditorGUILayout.LabelField("Clip playing most", string.IsNullOrEmpty(hold.DominantClip) ? "-" : hold.DominantClip);
            }

            _target = (EditTarget)EditorGUILayout.EnumPopup(new GUIContent("Weapon handle edits",
                "Base grip: every clip. Current clip correction: only the clip playing most now."), _target);
            _weaponHandle = EditorGUILayout.Toggle("Show weapon handle", _weaponHandle);
            _leftHandHandle = EditorGUILayout.Toggle("Show left-hand handle", _leftHandHandle);

            using (new EditorGUI.DisabledScope(profile == null))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Refit from animation"))
                {
                    Undo.RecordObject(profile, "Refit weapon hold");
                    profile.Fitted = false;
                    EditorUtility.SetDirty(profile);
                }
                using (new EditorGUI.DisabledScope(!Application.isPlaying || string.IsNullOrEmpty(hold.DominantClip)))
                {
                    if (GUILayout.Button("Clear this clip's correction"))
                    {
                        int index = profile.IndexOf(hold.DominantClip);
                        if (index >= 0)
                        {
                            Undo.RecordObject(profile, "Clear clip correction");
                            profile.Corrections.RemoveAt(index);
                            EditorUtility.SetDirty(profile);
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            if (profile != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"Profile: {profile.name}", EditorStyles.boldLabel);
                CreateCachedEditor(profile, null, ref _profileEditor);
                _profileEditor.OnInspectorGUI();
            }

            if (Application.isPlaying) Repaint(); // the playing clip changes every frame
        }

        private void OnSceneGUI()
        {
            var hold = (WeaponHold)target;
            WeaponHoldProfile profile = hold.ActiveProfile;
            Transform socket = hold.Socket;
            if (!Application.isPlaying || profile == null || socket == null || !hold.HasGoal) return;

            if (_weaponHandle)
            {
                Handles.Label(socket.position + Vector3.up * 0.12f, _target == EditTarget.BaseGrip
                    ? "Weapon (base grip)" : $"Weapon ({hold.DominantClip})");
                EditorGUI.BeginChangeCheck();
                Vector3 position = socket.position;
                Quaternion rotation = socket.rotation;
                Handles.TransformHandle(ref position, ref rotation);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(profile, "Move weapon grip");
                    hold.SetWeaponWorldPose(position, rotation, _target == EditTarget.CurrentClipCorrection);
                    EditorUtility.SetDirty(profile);
                }
            }

            if (_leftHandHandle && profile.UseLeftHandIK)
            {
                Vector3 position = socket.position + socket.rotation * profile.LeftHandPosition;
                Quaternion rotation = socket.rotation * Quaternion.Euler(profile.LeftHandRotation);
                Handles.color = new Color(0.4f, 0.9f, 1f);
                Handles.SphereHandleCap(0, position, Quaternion.identity, 0.04f, EventType.Repaint);
                Handles.Label(position + Vector3.up * 0.08f, "Left hand");
                EditorGUI.BeginChangeCheck();
                Handles.TransformHandle(ref position, ref rotation);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(profile, "Move left-hand grip");
                    hold.SetLeftHandWorldPose(position, rotation);
                    EditorUtility.SetDirty(profile);
                }
            }
        }

        private void OnDisable()
        {
            if (_profileEditor != null) DestroyImmediate(_profileEditor);
        }
    }
}
