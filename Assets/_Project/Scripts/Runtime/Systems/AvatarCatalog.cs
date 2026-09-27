using System;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// Owner 2026-09-27: the profile picture need not be the character; there are avatar styles
    /// (art drawn from reference renders) and frames that can be customised and won.
    /// Avatars: "You" (the live character) plus two per style. Art is loaded from
    /// Resources/UI/Avatars/&lt;id&gt; when it exists; until then the picker shows a placeholder.
    /// Frames: twelve, won by level, milestones, the stamp card, the Pass and the Gacha.
    /// </summary>
    public static class AvatarCatalog
    {
        public enum Style { Live, Chibi, Badge, Mascot, Weapon, Sticker }

        public sealed class Avatar
        {
            public string id, name; public Style style; public string unlockText; public Func<bool> unlocked;
            public Sprite Art => style == Style.Live ? null : Resources.Load<Sprite>("UI/Avatars/" + id);
        }

        /// <summary>Visual recipe of a frame for the AvatarFrame shader.</summary>
        public sealed class Frame
        {
            public string id, name, unlockText; public Func<bool> unlocked;
            public Color a, b, glow; public float sheen, glowStrength, width; public bool rainbow;
        }

        public const string LiveAvatar = "avatar.live";
        public const string DefaultFrame = "frame.default";

        static bool Level(int n) => PlayerProfile.AccountLevel >= n;
        static bool Owns(string frame) => PlayerProfile.OwnedFrames != null && Contains(PlayerProfile.OwnedFrames, frame);
        static bool Contains(System.Collections.Generic.IReadOnlyList<string> l, string s) { for (int i = 0; i < l.Count; i++) if (l[i] == s) return true; return false; }
        static Color C(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }

        public static readonly Avatar[] Avatars =
        {
            new() { id = LiveAvatar, name = "You", style = Style.Live, unlockText = "Always", unlocked = () => true },
            new() { id = "avatar.chibi_1", name = "Chibi Gunner", style = Style.Chibi, unlockText = "Free", unlocked = () => true },
            new() { id = "avatar.chibi_2", name = "Chibi Hero", style = Style.Chibi, unlockText = "Reach level 8", unlocked = () => Level(8) },
            new() { id = "avatar.badge_1", name = "Squad Badge", style = Style.Badge, unlockText = "Free", unlocked = () => true },
            new() { id = "avatar.badge_2", name = "Elite Badge", style = Style.Badge, unlockText = "Reach level 12", unlocked = () => Level(12) },
            new() { id = "avatar.mascot_1", name = "Zombie Pal", style = Style.Mascot, unlockText = "Play 25 runs", unlocked = () => PlayerProfile.RunsPlayed >= 25 },
            new() { id = "avatar.mascot_2", name = "Zombie King", style = Style.Mascot, unlockText = "Kill 5,000 zombies", unlocked = () => PlayerProfile.TotalKills >= 5000 },
            new() { id = "avatar.weapon_1", name = "Crossed Guns", style = Style.Weapon, unlockText = "Free", unlocked = () => true },
            new() { id = "avatar.weapon_2", name = "Golden Rifle", style = Style.Weapon, unlockText = "Own 10 guns", unlocked = () => PlayerProfile.OwnedWeaponIds.Count >= 10 },
            new() { id = "avatar.sticker_1", name = "Thumbs Up", style = Style.Sticker, unlockText = "Play 50 runs", unlocked = () => PlayerProfile.RunsPlayed >= 50 },
            new() { id = "avatar.sticker_2", name = "Victory", style = Style.Sticker, unlockText = "Survive 15:00", unlocked = () => PlayerProfile.BestSurvivalSeconds >= 900f },
        };

        public static readonly Frame[] Frames =
        {
            new() { id = DefaultFrame, name = "Classic", unlockText = "Always", unlocked = () => true, a = C("e8ecf2"), b = C("b9c2d0"), glow = C("ffffff"), sheen = 0f, glowStrength = 0f, width = 0.07f },
            new() { id = "frame.bronze", name = "Bronze", unlockText = "Reach level 5", unlocked = () => Level(5), a = C("f0b27a"), b = C("9c5a2e"), glow = C("ffcf9e"), sheen = 0.4f, glowStrength = 0.1f, width = 0.08f },
            new() { id = "frame.silver", name = "Silver", unlockText = "Reach level 10", unlocked = () => Level(10), a = C("ffffff"), b = C("8e9aad"), glow = C("dfe9ff"), sheen = 0.6f, glowStrength = 0.15f, width = 0.08f },
            new() { id = "frame.gold", name = "Gold", unlockText = "Reach level 20", unlocked = () => Level(20), a = C("fff1a8"), b = C("d99a1c"), glow = C("ffd65a"), sheen = 0.8f, glowStrength = 0.35f, width = 0.09f },
            new() { id = DailyRewards.StampMasterFrame, name = "Stamp Master", unlockText = "All 28 stamps", unlocked = () => Owns(DailyRewards.StampMasterFrame), a = C("ffd65a"), b = C("ff8a3d"), glow = C("ffe38a"), sheen = 0.7f, glowStrength = 0.4f, width = 0.09f },
            new() { id = PassRewards.SeasonFrame, name = "Season 1", unlockText = "Pass level 20", unlocked = () => Owns(PassRewards.SeasonFrame), a = C("7fd6ff"), b = C("5b3bc4"), glow = C("9fd8ff"), sheen = 0.7f, glowStrength = 0.4f, width = 0.09f },
            new() { id = "frame.neon", name = "Neon", unlockText = "Pull x10 on an event banner", unlocked = () => Owns("frame.neon"), a = C("ff4fd8"), b = C("35f0ff"), glow = C("ff7ae6"), sheen = 0.5f, glowStrength = 0.7f, width = 0.08f },
            new() { id = "frame.toxic", name = "Toxic", unlockText = "Kill 10,000 zombies", unlocked = () => PlayerProfile.TotalKills >= 10000, a = C("b6ff5c"), b = C("1f8a3a"), glow = C("9dff6a"), sheen = 0.5f, glowStrength = 0.55f, width = 0.09f },
            new() { id = "frame.flame", name = "Flame", unlockText = "Survive 20:00", unlocked = () => PlayerProfile.BestSurvivalSeconds >= 1200f, a = C("ffd23f"), b = C("e5361b"), glow = C("ff8a2a"), sheen = 0.6f, glowStrength = 0.65f, width = 0.1f },
            new() { id = "frame.ice", name = "Frost", unlockText = "Defeat 25 bosses", unlocked = () => PlayerProfile.BossesDefeated >= 25, a = C("ffffff"), b = C("5cc8ff"), glow = C("bff0ff"), sheen = 0.8f, glowStrength = 0.5f, width = 0.09f },
            new() { id = "frame.royal", name = "Royal", unlockText = "Win a Legendary in the Gacha", unlocked = () => Owns("frame.royal"), a = C("c9a7ff"), b = C("ffcf4a"), glow = C("e2c6ff"), sheen = 0.9f, glowStrength = 0.6f, width = 0.1f },
            new() { id = "frame.legend", name = "Legend", unlockText = "Earn every badge", unlocked = () => Owns("frame.legend"), a = C("ff5f6d"), b = C("ffc371"), glow = C("ffffff"), sheen = 1f, glowStrength = 0.8f, width = 0.1f, rainbow = true },
        };

        public static Avatar FindAvatar(string id) { foreach (var a in Avatars) if (a.id == id) return a; return Avatars[0]; }
        public static Frame FindFrame(string id) { foreach (var f in Frames) if (f.id == id) return f; return Frames[0]; }

        /// <summary>The avatar in use (falls back to "You" if its unlock was lost).</summary>
        public static Avatar CurrentAvatar { get { var a = FindAvatar(PlayerProfile.AvatarId); return a.unlocked() ? a : Avatars[0]; } }
        public static Frame CurrentFrame { get { var f = FindFrame(PlayerProfile.FrameId); return f.unlocked() ? f : Frames[0]; } }

        public static int UnlockedFrameCount { get { int n = 0; foreach (var f in Frames) if (f.unlocked()) n++; return n; } }
    }
}
