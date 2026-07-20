using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    [CustomEditor(typeof(WeaponPoseAuthoring))]
    public sealed class WeaponPoseAuthoringEditor : UnityEditor.Editor
    {
        private SerializedProperty _liveSync;

        private void OnEnable()
        {
            _liveSync = serializedObject.FindProperty("liveSyncTargetsToGrips");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(_liveSync, new GUIContent("Live Follow Weapon Grips"));
            serializedObject.ApplyModifiedProperties();

            var authoring = (WeaponPoseAuthoring)target;
            var weapon = authoring.GetComponent<Weapon>();
            var ik = authoring.GetComponent<WeaponIKController>();

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Weapon Hand Pose Authoring", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Enter Play Mode, select the spawned Player, then use this component. " +
                    "Capture All saves the complete equipped pose into WeaponData and Player.prefab.",
                    MessageType.Info);
                return;
            }

            if (weapon.Current == null || weapon.EditorInstanceTransform == null || weapon.CurrentGrips == null)
            {
                EditorGUILayout.HelpBox("No equipped weapon instance or WeaponGripPoints found.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Equipped", weapon.Current.weaponName);

            if (authoring.LiveSyncTargetsToGrips)
            {
                EditorGUILayout.HelpBox(
                    "LIVE FOLLOW: move/rotate/scale the spawned weapon model. Targets and hands " +
                    "will follow its grip markers continuously.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "MANUAL HAND AUTHORING: move a target to place the hand, and rotate the target " +
                    "to orient the wrist. Grip position is stored per weapon; target rotation belongs " +
                    "to the Player IK rig. Never move hand bones directly.", MessageType.Warning);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Sync Targets Now")) ik.EditorSyncTargetsToCurrentGrips();
                if (GUILayout.Button("Manual Mode")) SetLiveSync(authoring, false);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select Right Target")) SelectTarget(ik.EditorRightHandTarget);
                using (new EditorGUI.DisabledScope(weapon.Current == null || !weapon.Current.twoHanded))
                {
                    if (GUILayout.Button("Select Left Target")) SelectTarget(ik.EditorLeftHandTarget);
                }
            }

            EditorGUILayout.Space(4f);
            using (new EditorGUI.DisabledScope(authoring.LiveSyncTargetsToGrips))
            {
                if (GUILayout.Button("CAPTURE ALL -> WEAPON DATA + PLAYER IK", GUILayout.Height(32f)))
                    CaptureAll(authoring, weapon, ik);
            }

            if (GUILayout.Button("CAPTURE TARGET ROTATIONS -> PLAYER PREFAB", GUILayout.Height(26f)))
                CaptureTargetRotations(authoring, ik, weapon.Current.twoHanded);

            EditorGUILayout.HelpBox(
                "Capture All stores weapon position/rotation/scale and both hand-grip positions in " +
                "WeaponData. Target rotations are stored in Player.prefab. Uncaptured weapons keep " +
                "using their prefab grip markers.", MessageType.None);

            EditorGUILayout.HelpBox(
                "If moving a target does not move the hand while its IK weight is 1, stop: the rig " +
                "graph/reference is broken. Do not compensate by repeatedly snapping transforms.",
                MessageType.None);

            Repaint();
        }

        private static void SetLiveSync(WeaponPoseAuthoring authoring, bool value)
        {
            Undo.RecordObject(authoring, "Change weapon pose authoring mode");
            authoring.LiveSyncTargetsToGrips = value;
            EditorUtility.SetDirty(authoring);
        }

        private static void SelectTarget(Transform target)
        {
            if (target == null) return;
            Selection.activeTransform = target;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private static void CaptureAll(WeaponPoseAuthoring authoring, Weapon weapon,
            WeaponIKController ik)
        {
            var data = weapon.Current;
            var runtimeRoot = weapon.EditorInstanceTransform;
            var runtimeGrips = weapon.CurrentGrips;
            if (data == null || runtimeRoot == null || runtimeGrips == null)
            {
                Debug.LogError("[WeaponPose] Cannot capture: equipped weapon data/root/grips are missing.", authoring);
                return;
            }

            if (ik.EditorRightHandTarget == null || runtimeGrips.RightHandGrip == null)
            {
                Debug.LogError("[WeaponPose] Cannot capture: right-hand target or grip is missing.", authoring);
                return;
            }

            if (data.twoHanded && (ik.EditorLeftHandTarget == null || runtimeGrips.LeftHandGrip == null))
            {
                Debug.LogError("[WeaponPose] Cannot capture two-handed weapon: left-hand target or grip is missing.", authoring);
                return;
            }

            Undo.RecordObject(data, "Capture complete weapon pose");
            data.gripLocalPosition = runtimeRoot.localPosition;
            data.gripLocalEuler = runtimeRoot.localRotation.eulerAngles;
            data.gripLocalScale = runtimeRoot.localScale;
            data.rightHandGripRootPosition = runtimeRoot.InverseTransformPoint(ik.EditorRightHandTarget.position);
            if (data.twoHanded)
                data.leftHandGripRootPosition = runtimeRoot.InverseTransformPoint(ik.EditorLeftHandTarget.position);
            data.useAuthoredGripPositions = true;
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            weapon.EditorApplyAuthoredGripPositions();
            bool rotationsSaved = CaptureTargetRotations(authoring, ik, data.twoHanded);

            SetLiveSync(authoring, true);
            ik.EditorSyncTargetsToCurrentGrips();
            if (rotationsSaved)
            {
                Debug.Log(
                    $"[WeaponPose] Capture All saved model TRS and {(data.twoHanded ? 2 : 1)} grip position(s) " +
                    $"to {AssetDatabase.GetAssetPath(data)}; target rotation(s) saved to Player.prefab.", data);
            }
            else
            {
                Debug.LogWarning(
                    $"[WeaponPose] Partial capture: model TRS and grip position(s) were saved to " +
                    $"{AssetDatabase.GetAssetPath(data)}, but Player target rotations were not saved. " +
                    "Fix the reported Player.prefab error, then capture again.", data);
            }
        }

        private static bool CaptureTargetRotations(WeaponPoseAuthoring authoring,
            WeaponIKController ik, bool includeLeft)
        {
            const string playerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
            var runtimeTargets = new List<Transform> { ik.EditorRightHandTarget };
            if (includeLeft) runtimeTargets.Add(ik.EditorLeftHandTarget);

            GameObject prefabRoot = null;
            try
            {
                prefabRoot = PrefabUtility.LoadPrefabContents(playerPrefabPath);
                foreach (var runtimeTarget in runtimeTargets)
                {
                    if (runtimeTarget == null)
                        throw new System.InvalidOperationException("IK target reference is missing.");

                    string relativePath = AnimationUtility.CalculateTransformPath(
                        runtimeTarget, authoring.transform);
                    var prefabTarget = prefabRoot.transform.Find(relativePath);
                    if (prefabTarget == null)
                        throw new System.InvalidOperationException(
                            $"Target path '{relativePath}' does not exist in Player.prefab.");

                    prefabTarget.localRotation = runtimeTarget.localRotation;
                }

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, playerPrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[WeaponPose] Captured {runtimeTargets.Count} IK target rotation(s) into Player.prefab.", authoring);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[WeaponPose] Target rotation capture failed: {ex.Message}", authoring);
                return false;
            }
            finally
            {
                if (prefabRoot != null) PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }
}
