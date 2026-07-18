using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// Ap costume part (mesh skinned tu Layer Lab Characters.fbx) len skeleton
    /// QuickRigCharacter2_* luc runtime — dung cho ca Player in-game lan preview
    /// character ngoai MenuScene.
    ///
    /// Co che: catalog luu skinnedMesh + boneNames[] (thu tu khop bindposes).
    /// Applier build map ten-bone → Transform tu skeleton MOT lan, moi part equip
    /// tao GO con + SkinnedMeshRenderer, remap bones theo ten.
    /// </summary>
    public class CharacterModularApplier : MonoBehaviour
    {
        [SerializeField] private ModularCostumeCatalog catalog;

        [Tooltip("Root chua bones QuickRigCharacter2_*. De trong = tim tu chinh GO nay.")]
        [SerializeField] private Transform skeletonRoot;

        [Tooltip("Node cha de gan cac GO part. De trong = tao node 'Costume' duoi GO nay.")]
        [SerializeField] private Transform attachRoot;

        private readonly Dictionary<string, Transform> _boneMap = new();
        private readonly Dictionary<string, SkinnedMeshRenderer> _active = new(); // slot -> renderer dang mac
        private bool _built;

        public ModularCostumeCatalog Catalog => catalog;

        private void Awake()
        {
            EnsureBoneMap();
        }

        /// <summary>Build lai map bone (goi lai neu skeleton thay doi).</summary>
        public void EnsureBoneMap(bool force = false)
        {
            if (_built && !force) return;
            _boneMap.Clear();
            var root = skeletonRoot != null ? skeletonRoot : transform;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                _boneMap[t.name] = t; // ten bone la unique trong rig QuickRig
            _built = true;
        }

        /// <summary>Mac part theo ten trong slot. partName rong/null = thao slot.</summary>
        public bool Apply(string slotName, string partName)
        {
            if (catalog == null) { Debug.LogWarning("[Modular] catalog null", this); return false; }
            if (string.IsNullOrEmpty(partName)) { Clear(slotName); return true; }

            var slot = catalog.GetSlot(slotName);
            if (slot == null) { Debug.LogWarning($"[Modular] khong co slot '{slotName}'", this); return false; }

            for (int i = 0; i < slot.parts.Count; i++)
            {
                if (slot.parts[i].name == partName)
                    return Apply(slotName, slot.parts[i]);
            }
            Debug.LogWarning($"[Modular] khong thay part '{partName}' trong slot '{slotName}'", this);
            return false;
        }

        /// <summary>Mac part cu the vao slot (thay part cu neu co).</summary>
        public bool Apply(string slotName, in ModularCostumeCatalog.PartEntry entry)
        {
            if (entry.skinnedMesh == null || entry.boneNames == null || entry.boneNames.Length == 0)
            {
                Debug.LogWarning($"[Modular] part '{entry.name}' chua co skinned binding — chay lai extractor", this);
                return false;
            }

            EnsureBoneMap();

            // Remap bones theo ten
            var bones = new Transform[entry.boneNames.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                if (!_boneMap.TryGetValue(entry.boneNames[i], out bones[i]))
                {
                    Debug.LogWarning($"[Modular] skeleton thieu bone '{entry.boneNames[i]}' cho part '{entry.name}'", this);
                    return false;
                }
            }

            Clear(slotName);

            var go = new GameObject($"Costume_{slotName}");
            go.transform.SetParent(GetAttachRoot(), false);

            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = entry.skinnedMesh;
            smr.sharedMaterials = entry.materials;
            smr.bones = bones;
            if (!string.IsNullOrEmpty(entry.rootBoneName) && _boneMap.TryGetValue(entry.rootBoneName, out var rb))
                smr.rootBone = rb;
            smr.updateWhenOffscreen = false;

            _active[slotName] = smr;
            return true;
        }

        /// <summary>Thao part dang mac o slot.</summary>
        public void Clear(string slotName)
        {
            if (_active.TryGetValue(slotName, out var smr) && smr != null)
                Destroy(smr.gameObject);
            _active.Remove(slotName);
        }

        /// <summary>Thao het toan bo costume.</summary>
        public void ClearAll()
        {
            foreach (var kv in _active)
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            _active.Clear();
        }

        /// <summary>Ap nguyen loadout (slot → partName).</summary>
        public void ApplyLoadout(IReadOnlyDictionary<string, string> loadout)
        {
            if (loadout == null) return;
            foreach (var kv in loadout)
                Apply(kv.Key, kv.Value);
        }

        /// <summary>Ap costume da luu trong LoadoutState (guid -> entry). Goi sau khi spawn.</summary>
        public void ApplySavedParts()
        {
            if (catalog == null) return;
            var parts = LoadoutState.Parts;
            for (int i = 0; i < parts.Count; i++)
            {
                if (TryFindByGuid(parts[i].guid, out var slotName, out var entry))
                    Apply(slotName, entry);
            }
        }

        /// <summary>Tim part theo asset guid trong catalog.</summary>
        public bool TryFindByGuid(string guid, out string slotName, out ModularCostumeCatalog.PartEntry entry)
        {
            slotName = null;
            entry = default;
            if (catalog == null || string.IsNullOrEmpty(guid)) return false;
            var slots = catalog.slots;
            for (int i = 0; i < slots.Count; i++)
            {
                var ps = slots[i].parts;
                for (int j = 0; j < ps.Count; j++)
                {
                    if (ps[j].guid != guid) continue;
                    slotName = slots[i].slot;
                    entry = ps[j];
                    return true;
                }
            }
            return false;
        }

        /// <summary>Ten part dang mac o slot ("" neu trong).</summary>
        public string GetEquipped(string slotName)
        {
            if (_active.TryGetValue(slotName, out var smr) && smr != null && smr.sharedMesh != null)
                return smr.sharedMesh.name;
            return string.Empty;
        }

        private Transform GetAttachRoot()
        {
            if (attachRoot != null) return attachRoot;
            var existing = transform.Find("Costume");
            if (existing != null) { attachRoot = existing; return attachRoot; }
            var go = new GameObject("Costume");
            go.transform.SetParent(transform, false);
            attachRoot = go.transform;
            return attachRoot;
        }
    }
}
