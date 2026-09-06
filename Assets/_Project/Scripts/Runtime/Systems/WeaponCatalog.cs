using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// The single authoritative roster. Adding weapon 26 must be ONE entry here and nothing else —
    /// no consumer edits, no folder-scan assumptions, no serialized array to re-order by hand.
    ///
    /// Naming note (M7.0): the editor contact-sheet renderer previously held this name. It was
    /// renamed to `VendorWeaponSheetRenderer`, which is what it actually does. This type owns the
    /// name now.
    ///
    /// Identity rules, in force:
    /// <list type="bullet">
    /// <item><b>weaponId is save identity.</b> It never changes and is never reused. Ownership,
    /// equip slots and unlock state are all keyed by it.</item>
    /// <item><b>catalogOrder is presentation only.</b> Re-ordering it must never change what a
    /// player owns or starts with — which is why the starter is an explicit
    /// <see cref="UnlockMethod.Starter"/> flag rather than "whichever has the lowest order".</item>
    /// <item><b>Variants group by variantGroupId</b> and do not inflate the family count.</item>
    /// </list>
    /// </summary>
    [CreateAssetMenu(menuName = "ZombieWar/Weapon Catalog", fileName = "WeaponCatalog")]
    public class WeaponCatalog : ScriptableObject
    {
        public enum UnlockMethod
        {
            /// Owned from the first run. Exactly one entry may carry this.
            Starter,
            /// Bought in the Hub. Under the W6 lock the unlock resource is Blueprint; the resource
            /// itself is not implemented in M7.0, so this only marks "acquirable".
            Purchase,
            /// Present in the arsenal but not offered anywhere. Used for a variant that is folded
            /// into its base for display.
            Disabled,
        }

        [System.Serializable]
        public class Entry
        {
            [Tooltip("SAVE IDENTITY. Never change, never reuse.")]
            public string weaponId;

            public WeaponData data;

            [Tooltip("Sidearm / SMG / AssaultRifle / Shotgun / Marksman / LMG")]
            public string family;

            [Tooltip("Weapons sharing this id are the same weapon visually; UI shows the base only.")]
            public string variantGroupId;

            [Tooltip("Empty when this entry IS the base of its variant group.")]
            public string baseWeaponId;

            [Tooltip("Presentation order ONLY. Safe to reorder.")]
            public int catalogOrder;

            public UnlockMethod unlockMethod = UnlockMethod.Purchase;

            [Tooltip("Mirrors WeaponData.tier, which is bound to the measured power band.")]
            public WeaponTier tier = WeaponTier.Common;

            public bool IsVariant => !string.IsNullOrEmpty(baseWeaponId);
        }

        [SerializeField] private List<Entry> entries = new();

        public IReadOnlyList<Entry> Entries => entries;

        // ---------------------------------------------------------------- lookup

        public Entry ById(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) return null;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null && entries[i].weaponId == weaponId) return entries[i];
            return null;
        }

        public WeaponData DataById(string weaponId) => ById(weaponId)?.data;

        /// <summary>The declared starter, or null. Deliberately NOT "lowest catalogOrder".</summary>
        public Entry Starter
        {
            get
            {
                for (int i = 0; i < entries.Count; i++)
                    if (entries[i] != null && entries[i].unlockMethod == UnlockMethod.Starter) return entries[i];
                return null;
            }
        }

        /// <summary>Base entry of a variant group, following baseWeaponId one hop.</summary>
        public Entry BaseOf(string weaponId)
        {
            var e = ById(weaponId);
            if (e == null) return null;
            return e.IsVariant ? (ById(e.baseWeaponId) ?? e) : e;
        }

        /// <summary>Entries a shop/armory should show: bases only, variants folded away.</summary>
        public List<Entry> DisplayEntries()
        {
            var list = new List<Entry>();
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null || e.IsVariant || e.unlockMethod == UnlockMethod.Disabled) continue;
                // Hub/shop/loadout must not see a weapon the owner has not authored anchors for.
                if (e.data != null && !e.data.IsPlayable) continue;
                list.Add(e);
            }
            list.Sort((a, b) => a.catalogOrder.CompareTo(b.catalogOrder));
            return list;
        }

        public List<WeaponData> AllData()
        {
            var list = new List<WeaponData>();
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null && entries[i].data != null) list.Add(entries[i].data);
            return list;
        }

        // ---------------------------------------------------------------- active instance

        private static WeaponCatalog _active;

        /// <summary>
        /// Resolved lazily from Resources so pure-data callers (PlayerProfile) can consult it
        /// without a scene reference. Settable so EditMode tests can inject a fixture, and so a
        /// missing asset degrades to the previous behaviour instead of throwing.
        /// </summary>
        public static WeaponCatalog Active
        {
            get
            {
                if (_active == null) _active = Resources.Load<WeaponCatalog>("WeaponCatalog");
                return _active;
            }
            set => _active = value;
        }

        // ---------------------------------------------------------------- authoring / validation

        public void SetEntries(List<Entry> newEntries) => entries = newEntries ?? new List<Entry>();

        /// <summary>
        /// Contract checks that must hold for the catalog to be trustworthy. Returns human-readable
        /// failures; empty means the catalog is sound.
        /// </summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            var seenIds = new HashSet<string>();
            int starters = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null) { errors.Add($"entry {i} is null"); continue; }
                if (string.IsNullOrEmpty(e.weaponId)) { errors.Add($"entry {i} has an empty weaponId"); continue; }
                if (!seenIds.Add(e.weaponId)) errors.Add($"duplicate weaponId '{e.weaponId}'");
                if (e.data == null) errors.Add($"'{e.weaponId}' has no WeaponData");
                else if (e.data.WeaponId != e.weaponId)
                    errors.Add($"'{e.weaponId}' points at asset '{e.data.name}' whose WeaponId is '{e.data.WeaponId}'");
                if (e.unlockMethod == UnlockMethod.Starter) starters++;
                if (e.IsVariant && e.baseWeaponId == e.weaponId)
                    errors.Add($"'{e.weaponId}' lists itself as its own base");
            }

            foreach (var e in entries)
            {
                if (e == null || !e.IsVariant) continue;
                if (ById(e.baseWeaponId) == null)
                    errors.Add($"variant '{e.weaponId}' references missing base '{e.baseWeaponId}'");
            }

            if (starters != 1) errors.Add($"expected exactly 1 Starter entry, found {starters}");
            return errors;
        }
    }
}
