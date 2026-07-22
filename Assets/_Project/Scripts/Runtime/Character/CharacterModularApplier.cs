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
        private bool _bakedDisabled;
        // Slot noi bo cho Body composite (2 renderer): body full + head/tai.
        private const string BodyRendererSlot = "Body";
        private const string BodyHeadRendererSlot = "BodyHead";

        public ModularCostumeCatalog Catalog => catalog;

        /// <summary>Gan catalog o runtime (dung khi spawn preview tu prefab base khong co catalog).</summary>
        public void SetCatalog(ModularCostumeCatalog c)
        {
            catalog = c;
        }

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
            // Ke thua layer cua character: preview character nam tren layer CharacterPreview
            // (camera preview cullingMask rieng) — GO moi mac dinh layer 0 se bi cull → part
            // "tang hinh" tren preview. Player in-game layer 0 nen khong doi gi.
            go.layer = gameObject.layer;

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

        /// <summary>Thao part dang mac o slot. Don theo TEN child ("Costume_&lt;slot&gt;") chu khong chi
        /// theo dictionary — _active co the mat dong bo khi nhieu caller (Start cua stage, screen
        /// equip, ApplySavedParts) xen ke nhau, va don theo ten bao dam bat bien
        /// "moi slot toi da 1 renderer" trong moi truong hop (khong duplicate/stale mesh).</summary>
        public void Clear(string slotName)
        {
            _active.Remove(slotName);
            var root = GetAttachRoot();
            string childName = "Costume_" + slotName;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                if (child.name != childName) continue;
                child.gameObject.SetActive(false); // tat ngay -> khong flash 1 frame khi Destroy defer
                Destroy(child.gameObject);
            }
        }

        /// <summary>Thao het toan bo costume (don theo children that, khong chi dictionary).</summary>
        public void ClearAll()
        {
            _active.Clear();
            var root = GetAttachRoot();
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                if (child.name.StartsWith("Costume_")) Destroy(child.gameObject);
            }
        }

        /// <summary>Ap nguyen loadout (slot → partName).</summary>
        public void ApplyLoadout(IReadOnlyDictionary<string, string> loadout)
        {
            if (loadout == null) return;
            foreach (var kv in loadout)
                Apply(kv.Key, kv.Value);
        }

        /// <summary>Ap costume da luu trong LoadoutState (guid -> entry). Goi sau khi spawn.
        /// Truoc khi ap, repair idempotent outfit theo catalog.defaults (fresh profile nhan
        /// ownership + bo do mac dinh; profile cu duoc va slot bat buoc con thieu) — dam bao
        /// invariant "khong bao gio ra mannequin" o MOI diem ap costume.</summary>
        // Reconcile: ap dung ban Body + parts hien tai, roi go cac slot KHONG con equip. Moi Apply
        // tu Clear slot cua no (SetActive(false)+Destroy) nen khong flash; khong dung ClearAll
        // (Destroy defer -> chong renderer 1 frame). Goi lai moi lan CostumeChanged an toan.
        public void ApplySavedParts()
        {
            if (catalog == null) return;
            PlayerProfile.EnsureValidCostumeLoadout(catalog);
            DisableBakedRenderers();

            // Fantasy: authored color/ear composite. Pro Casual: Face is the one technical Head
            // mesh and Body_1..4 is selected automatically from glove/shoe occupancy.
            HashSet<string> desired;
            if (catalog.compositeBody)
            {
                desired = new HashSet<string> { BodyRendererSlot, BodyHeadRendererSlot };
                ApplyBody(PlayerProfile.BodyColor, PlayerProfile.BodyEar);
            }
            else
            {
                desired = new HashSet<string> { ModularCostumeCatalog.CasualBaseHeadSlot, ModularCostumeCatalog.BodySlot };
                ApplyCasualTechnicalBase(
                    !string.IsNullOrEmpty(PlayerProfile.GetPart("Hands")),
                    !string.IsNullOrEmpty(PlayerProfile.GetPart("Feet")));
            }

            var parts = LoadoutState.Parts;
            for (int i = 0; i < parts.Count; i++)
            {
                if ((catalog.compositeBody && parts[i].slot == ModularCostumeCatalog.BodySlot)
                    || catalog.IsTechnicalCasualSlot(parts[i].slot)) continue;
                if (TryResolvePartKey(parts[i].guid, out var slotName, out var entry))
                {
                    Apply(slotName, entry);
                    desired.Add(slotName);
                }
            }

            // Go slot khong con equip (vd Feet/Beard vua chon Khong mang).
            var attach = GetAttachRoot();
            for (int i = attach.childCount - 1; i >= 0; i--)
            {
                var child = attach.GetChild(i);
                if (!child.name.StartsWith("Costume_")) continue;
                string slot = child.name.Substring("Costume_".Length);
                if (!desired.Contains(slot)) Clear(slot);
            }
        }

        /// <summary>Ap Body composite: full-body mesh + head/tai, luon cung mau (khong lech mau).
        /// Body_&lt;Color&gt;_1 -> Costume_Body; Body_&lt;Color&gt;_Head_&lt;1|2&gt; -> Costume_BodyHead.</summary>
        public void ApplyBody(string color, string ear)
        {
            if (catalog == null) return;
            var body = catalog.FindPartByName(ModularCostumeCatalog.BodySlot, ModularCostumeCatalog.BodyMeshName(color));
            var head = catalog.FindPartByName(ModularCostumeCatalog.BodySlot, ModularCostumeCatalog.BodyHeadName(color, ear));
            if (body.HasValue) ApplyEntry(BodyRendererSlot, body.Value);
            if (head.HasValue) ApplyEntry(BodyHeadRendererSlot, head.Value);
        }

        /// <summary>
        /// Pro Casual infrastructure: Head is always present. Body variants hide hand/foot geometry
        /// where gloves/shoes cover it: 1=bare/bare, 2=gloves/bare, 3=bare/shoes, 4=gloves/shoes.
        /// These meshes are never owned, sold, rolled or stored in the player loadout.
        /// </summary>
        public void ApplyCasualTechnicalBase(bool hasGloves, bool hasShoes)
        {
            if (catalog == null || catalog.compositeBody) return;
            var head = catalog.FindPartByName(ModularCostumeCatalog.CasualBaseHeadSlot, "Head");
            var body = catalog.FindPartByName(ModularCostumeCatalog.BodySlot,
                ModularCostumeCatalog.CasualBodyMeshName(hasGloves, hasShoes));
            if (head.HasValue) ApplyEntry(ModularCostumeCatalog.CasualBaseHeadSlot, head.Value);
            if (body.HasValue) ApplyEntry(ModularCostumeCatalog.BodySlot, body.Value);
            if (!head.HasValue || !body.HasValue)
                Debug.LogError($"[CharacterModularApplier] Missing Pro Casual technical mesh: Head={head.HasValue}, Body={ModularCostumeCatalog.CasualBodyMeshName(hasGloves, hasShoes)}={body.HasValue}.");
        }

        // Ap 1 entry vao GO Costume_&lt;slot&gt; (dung logic Apply(slotName, entry) qua ten slot noi bo).
        private void ApplyEntry(string rendererSlot, in ModularCostumeCatalog.PartEntry entry) => Apply(rendererSlot, entry);

        // Tat het SkinnedMeshRenderer BAKED (Body/Brow/Eye/Mouth cua prefab) — khong nam duoi attachRoot
        // (Costume) — de tranh trung/lech voi mesh do applier ap. Chay 1 lan.
        private void DisableBakedRenderers()
        {
            if (_bakedDisabled) return;
            var attach = GetAttachRoot();
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (!smr.transform.IsChildOf(attach))
                    smr.gameObject.SetActive(false);
            _bakedDisabled = true;
        }

        /// <summary>Resolve a stored costume key to a part. Casual saves store the stable itemId
        /// (all Casual parts share the same source-fbx guid, so guid is ambiguous); Fantasy saves
        /// store the asset guid. itemId and 32-hex guids never collide, so try itemId first.</summary>
        private bool TryResolvePartKey(string key, out string slotName, out ModularCostumeCatalog.PartEntry entry)
        {
            if (catalog != null && catalog.TryFindByItemId(key, out slotName, out entry)) return true;
            return TryFindByGuid(key, out slotName, out entry);
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
