using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.EditorTools
{
    /// <summary>
    /// M7.1 A3 — the owner's grip/muzzle authoring queue.
    ///
    /// Onboarding deliberately stops before every anchor, so ~29 weapons are sitting in
    /// <see cref="WeaponData.AuthoringStatus.PendingOwnerAuthoring"/> waiting for the owner to place
    /// their grips by hand. This window is the walk-list: it shows what is left, what each weapon is
    /// missing, and equips one on the live player in a single click so the owner can work through
    /// the queue without leaving Play Mode or hunting for assets.
    ///
    /// It is NOT a second authoring system. The actual authoring happens in the existing
    /// `WeaponPoseAuthoring` / `WeaponPoseAuthoringEditor` play-mode workflow; this only navigates to it.
    /// </summary>
    public class WeaponAuthoringQueueWindow : EditorWindow
    {
        const string DataDir = "Assets/_Project/Data/Weapons";

        Vector2 _scroll;
        bool _hideBlocked = true;
        string _familyFilter = "All";
        static readonly string[] Families =
            { "All", "Sidearm", "SMG", "AssaultRifle", "Shotgun", "Marksman", "LMG" };

        [MenuItem("ZombieWar/Weapons/Factory/Authoring Queue")]
        public static void Open() =>
            GetWindow<WeaponAuthoringQueueWindow>("Grip Authoring Queue").minSize = new Vector2(560, 320);

        class Item
        {
            public WeaponData data;
            public bool hasGrips, hasRight, hasLeft, hasMuzzle;
            public string Missing
            {
                get
                {
                    if (!hasGrips) return "WeaponGripPoints component";
                    var m = new List<string>();
                    if (!hasRight) m.Add("rightHandGrip");
                    if (!hasLeft) m.Add("leftHandGrip");
                    if (!hasMuzzle) m.Add("muzzlePoint");
                    return m.Count == 0 ? "— anchors present, needs sign-off" : string.Join(", ", m);
                }
            }
        }

        static List<Item> Scan()
        {
            var items = new List<Item>();
            foreach (var g in AssetDatabase.FindAssets("t:WeaponData", new[] { DataDir }))
            {
                var w = AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(g));
                if (w == null || w.IsPlayable) continue;   // Ready weapons are not in the queue

                var it = new Item { data = w };
                if (w.weaponPrefab != null)
                {
                    string path = AssetDatabase.GetAssetPath(w.weaponPrefab);
                    var contents = PrefabUtility.LoadPrefabContents(path);
                    var grips = contents.GetComponentInChildren<WeaponGripPoints>(true);
                    it.hasGrips = grips != null;
                    it.hasRight = grips != null && grips.RightHandGrip != null;
                    it.hasLeft = grips != null && grips.LeftHandGrip != null;
                    it.hasMuzzle = grips != null && grips.MuzzlePoint != null;
                    PrefabUtility.UnloadPrefabContents(contents);
                }
                items.Add(it);
            }
            return items.OrderBy(i => i.data.weaponClass.ToString())
                        .ThenBy(i => i.data.name).ToList();
        }

        List<Item> _cache;

        void OnEnable() => _cache = Scan();

        void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Rescan", EditorStyles.toolbarButton, GUILayout.Width(70))) _cache = Scan();
                _familyFilter = Families[EditorGUILayout.Popup(
                    System.Array.IndexOf(Families, _familyFilter), Families, EditorStyles.toolbarPopup, GUILayout.Width(110))];
                _hideBlocked = GUILayout.Toggle(_hideBlocked, "Hide blocked", EditorStyles.toolbarButton, GUILayout.Width(100));
                GUILayout.FlexibleSpace();
                GUILayout.Label(Application.isPlaying ? "Play Mode — equip enabled" : "Enter Play Mode to equip",
                                EditorStyles.miniLabel);
            }

            if (_cache == null) _cache = Scan();

            var shown = _cache.Where(i =>
                (_familyFilter == "All" || i.data.weaponClass.ToString() == _familyFilter) &&
                !(_hideBlocked && i.data.Authoring == WeaponData.AuthoringStatus.BlockedNeedsProjectileFireMode)).ToList();

            EditorGUILayout.HelpBox(
                $"{shown.Count} weapon(s) awaiting hand-authored anchors. Anchors are never inferred — " +
                "place them with the WeaponPoseAuthoring play-mode workflow, then set authoringStatus to " +
                "Ready to make the weapon equippable.", MessageType.Info);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var it in shown)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(it.data.name, EditorStyles.boldLabel, GUILayout.Width(230));
                        EditorGUILayout.LabelField(it.data.weaponClass.ToString(), GUILayout.Width(90));
                        EditorGUILayout.LabelField(it.data.tier.ToString(), GUILayout.Width(70));
                        if (it.data.Authoring == WeaponData.AuthoringStatus.BlockedNeedsProjectileFireMode)
                            EditorGUILayout.LabelField("BLOCKED: needs FireMode.Projectile", EditorStyles.miniBoldLabel);
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("missing:", GUILayout.Width(56));
                        EditorGUILayout.LabelField(it.Missing, EditorStyles.miniLabel);
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Select asset", GUILayout.Width(100)))
                            Selection.activeObject = it.data;
                        if (GUILayout.Button("Select prefab", GUILayout.Width(100)))
                            Selection.activeObject = it.data.weaponPrefab;

                        using (new EditorGUI.DisabledScope(!Application.isPlaying ||
                               it.data.Authoring == WeaponData.AuthoringStatus.BlockedNeedsProjectileFireMode))
                        {
                            if (GUILayout.Button("Equip on player", GUILayout.Width(120)))
                                EquipForAuthoring(it.data);
                        }

                        using (new EditorGUI.DisabledScope(!it.hasGrips || !it.hasRight || !it.hasMuzzle))
                        {
                            if (GUILayout.Button("Mark Ready (owner sign-off)", GUILayout.Width(200)))
                                MarkReady(it.data);
                        }
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Equips a pending weapon on the live player for authoring ONLY. `Weapon.EquipToSlot`
        /// deliberately refuses non-playable weapons, so this drives the authoring path directly and
        /// says so — it is a bypass with a stated reason, not a hole in the gate.
        /// </summary>
        static void EquipForAuthoring(WeaponData data)
        {
            var weapon = Object.FindFirstObjectByType<Weapon>();
            if (weapon == null) { Debug.LogError("[AuthoringQueue] No Weapon in the scene."); return; }

            var pose = weapon.GetComponent<WeaponPoseAuthoring>() ?? weapon.gameObject.AddComponent<WeaponPoseAuthoring>();
            pose.LiveSyncTargetsToGrips = true;

            // Temporarily promote so the equip path accepts it, then restore. The asset on disk is
            // untouched: this is an in-memory flip for the authoring session.
            var so = new SerializedObject(data);
            var prop = so.FindProperty("authoringStatus");
            int previous = prop.enumValueIndex;
            prop.enumValueIndex = (int)WeaponData.AuthoringStatus.Ready;
            so.ApplyModifiedPropertiesWithoutUndo();

            int slot = data.twoHanded ? 1 : 0;
            bool ok = weapon.EquipToSlot(slot, data);
            if (ok) weapon.EquipSlot(slot);

            prop.enumValueIndex = previous;
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log($"[AuthoringQueue] {(ok ? "Equipped" : "Failed to equip")} '{data.name}' in slot {slot} " +
                      "for authoring. Status on disk is unchanged.");
        }

        static void MarkReady(WeaponData data)
        {
            if (!EditorUtility.DisplayDialog("Owner sign-off",
                    $"Mark '{data.name}' as Ready?\n\nThis makes it equippable and lets it reach the Hub, " +
                    "shop and loadout. Only do this after you have authored and eyeballed its grips.",
                    "Mark Ready", "Cancel")) return;

            var so = new SerializedObject(data);
            so.FindProperty("authoringStatus").enumValueIndex = (int)WeaponData.AuthoringStatus.Ready;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AuthoringQueue] '{data.name}' signed off as Ready.");
        }
    }
}
