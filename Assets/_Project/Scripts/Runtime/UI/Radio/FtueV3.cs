using UnityEngine;
using ZombieWar.Stations;

namespace ZombieWar.UI
{
    /// <summary>
    /// The FTUE v3 steps (canvas page "FTUE v3 · radio call", boards FR_01–FR_12): card text, size,
    /// height, channel context, speaking agent, corners, chip and hand, exactly as drawn. Heights are
    /// the boards' 390×693 coordinates scaled to the 1080×1920 reference. The FTUE v2 rules decide
    /// WHEN a step shows (screens call these); <see cref="FtueRadio"/> draws it.
    /// </summary>
    public static class FtueV3
    {
        static float Y(float boardTop) => boardTop * 1920f / 693f;

        // In a run the top-left corner holds the 3x2 skill grid (HUD 05/10): run cards start under it.
        const float RunTop = 152f;

        static FtueRadio.Call Card(string id, string voice, string agent, string ctx, string title, string body,
                                   FtueRadio.Size size, float boardTop) => new()
        {
            Id = id, VoiceId = voice, Agent = agent, Context = ctx, Title = title, Body = body, Size = size, Y = Y(boardTop),
        };

        static bool Live(Component c) => c != null && c.gameObject.activeInHierarchy;

        // FR_01 · Home, first visit: HQ says hit PLAY.
        public static void Home(RectTransform play)
        {
            if (play == null) return;
            var c = Card("home", "vo_riley_ftue_home", "riley", null, "YOUR FIRST RUN",
                "Hit PLAY. Drag to move, your gun fires by itself.", FtueRadio.Size.Normal, 440);
            c.Target = () => FtueRadio.ScreenRect(play);
            c.Hand = () => FtueRadio.TapPoint(play);
            c.Alive = () => Live(play) && PlayerProfile.RunsPlayed == 0;
            c.Modal = true;
            FtueRadio.Show(c);
        }

        // FR_02 · Drag to move: corners and energy ring on the thumb zone, "DRAG ANYWHERE".
        public static void Move()
        {
            var c = Card("move", "vo_riley_ftue_move", "riley", null, "DRAG TO MOVE",
                "Your gun fires at the nearest monster by itself.", FtueRadio.Size.Normal, RunTop);
            c.Dim = 0.22f;
            c.Ring = true;
            c.Chip = "DRAG ANYWHERE";
            // The joystick rests at the bottom centre (HUD 05/10): a 114 px square centred on it, on a
            // 390 px wide phone (rest centre 254 of 1080 px up = 92 of 390).
            c.Target = () => { float u = Screen.width / 390f; return new Rect(138f * u, 35f * u, 114f * u, 114f * u); };
            c.Hand = () => { float u = Screen.width / 390f; return new Vector2(195f * u, 92f * u); };
            c.Alive = () => !Ftue.Done(Ftue.Move);
            FtueRadio.Show(c);
        }

        // U4 · First Studio visit (owner-approved 05/10): Tiger, corners + hand on the TOP hotspot.
        // The card sits low over the piece strip so the HEAD and FACE hotspots stay visible; the chip
        // goes above the corners, clear of the hand. Text only until the owner picks how the new Tiger
        // line is voiced (backlog Q1).
        public static void Studio(RectTransform hotspot)
        {
            if (hotspot == null || Ftue.Done(Ftue.Studio)) return;
            var c = Card("studio", null, "mai", null, "TAP A PART TO CHANGE IT",
                "Hat, face, top, back, pants, shoes. Try anything on for free.", FtueRadio.Size.Normal, 452);
            c.Chip = "TAP THE TOP";
            c.ChipAbove = true;
            c.Target = () => FtueRadio.ScreenRect(hotspot);
            c.Hand = () => FtueRadio.TapPoint(hotspot);
            c.Alive = () => Live(hotspot) && !Ftue.Done(Ftue.Studio);
            c.Modal = true;
            FtueRadio.Show(c);
        }

        // F6 · WEAR NOW (backlog #13): the Studio opened on a gifted piece; Tiger points at it until
        // it is worn or the Studio closes. Shown on every gift, not once: it is the gift's last step.
        public static void WearNew(RectTransform tile, string slotName, System.Func<bool> worn)
        {
            if (tile == null) return;
            string name = string.IsNullOrEmpty(slotName) ? "PIECE" : slotName.ToUpperInvariant();
            var c = Card("wear", null, "mai", null, $"NEW {name}! WEAR IT",
                $"It opened on the {name.ToLowerInvariant()} slot. Tap it to put it on.", FtueRadio.Size.Small, 52);
            c.Chip = "NEW · TAP TO WEAR";
            c.Target = () => FtueRadio.ScreenRect(tile);
            c.Hand = () => FtueRadio.TapPoint(tile);
            c.Alive = () => Live(tile) && (worn == null || !worn());
            c.Modal = true;
            FtueRadio.Show(c);
        }

        // Horde Call telegraph (mockup U2 05/10): Raptor calls the side, the HUD draws the arrows and
        // the 3-2-1. Not an FTUE step, so it never waits on Ftue flags; it ends itself.
        public static void HordeCall(string side, float seconds)
        {
            var c = Card("horde", null, "lukas", null, "HORDE CALL",
                $"A wall of them, from the {side}. Hold your ground.", FtueRadio.Size.Normal, 196);
            c.Seconds = seconds;
            FtueRadio.Show(c);
        }

        // FR_02b · First kill: corners on the XP bar.
        public static void Xp(RectTransform xpBar)
        {
            var c = Card("xp", "vo_kaito_ftue_xp", "kaito", null, "XP",
                "The bar up top glows on each kill. Fill it to level up.", FtueRadio.Size.Small, RunTop);
            c.Target = () => FtueRadio.ScreenRect(xpBar);
            // A short tip that steps in front of "drag to move" for a moment, then hands back.
            c.Seconds = 5f;
            c.Modal = true;
            FtueRadio.Show(c);
        }

        // FR_03 · First level-up: no timer, corners + "SUGGESTED" on one card, hand on it.
        public static void FirstCard(RectTransform perk)
        {
            if (perk == null) return;
            var c = Card("card", "vo_chen_ftue_card", "chen", null, "PICK A CARD",
                "One card each level. No timer this time. I'd take this one.", FtueRadio.Size.Small, 586);
            c.Chip = "SUGGESTED";
            c.ChipAbove = true;   // the next card sits right under this one
            c.Target = () => FtueRadio.ScreenRect(perk);
            c.Hand = () => FtueRadio.TapPoint(perk);
            c.Alive = () => Live(perk) && !Ftue.Done(Ftue.Card);
            c.Modal = true;
            FtueRadio.Show(c);
        }

        // FR_04a–e · First station of each kind: corners on the pad, chip, card under it.
        public static void NewStation(ZombieWar.Stations.Station s, System.Func<bool> alive)
        {
            if (s == null) return;
            var kind = s.Anchor.kind;
            var (agent, voice, title, body, chip) = kind switch
            {
                StationKind.SignalRelay => ("jiho", "vo_jiho_ftue_relay", "SIGNAL RELAY",
                    "Stand inside for 12 s. Step out and it drains slowly. Reward: pick 1 of 3 cards.", "SIGNAL RELAY · 12 s"),
                StationKind.SupplyCache => ("mai", "vo_mai_ftue_cache", "SUPPLY CACHE",
                    "Stand inside and pay coins. The price rises each buy. Reward: pick 1 of 3 cards.", $"SUPPLY CACHE · {s.CachePrice}"),
                StationKind.BossBeacon => ("kaito", "vo_kaito_ftue_beacon", "BOSS BEACON",
                    "Step in to call a boss. It chases you until one of you falls. Reward: a chest.", "BOSS BEACON"),
                StationKind.SupplyDrop => ("mai", "vo_mai_ftue_drop", "SUPPLY DROP",
                    "Stand by the pod for 1.5 s to open it. Reward: an item and coins.", "SUPPLY DROP · 1.5 s"),
                _ => ("jiho", "vo_jiho_ftue_heal", "HEAL ZONE",
                    "Stand inside 1.5 s to switch it on. Heals 5% health per second for 8 s.", "HEAL ZONE · 1.5 s"),
            };
            var c = Card("station." + kind, voice, agent, "NEW STATION", title, body, FtueRadio.Size.Normal, 352);
            c.Chip = chip;
            float r = ZombieWar.Stations.Station.RadiusFor(kind);
            c.Target = () => s != null ? FtueRadio.WorldDisc(s.transform.position, r) : null;
            c.Alive = alive;
            FtueRadio.Show(c);
        }

        // FR_05a–c · First item of each kind: card with the item icon, play keeps going.
        public static void Item(PickupEffect effect, Sprite icon)
        {
            var (agent, voice, title, body) = effect switch
            {
                PickupEffect.Magnet => ("mai", "vo_mai_ftue_magnet", "MAGNET", "Pulls every coin and gem on the map to you."),
                PickupEffect.Bomb => ("lukas", "vo_lukas_ftue_bomb", "BOMB", "Wipes out the monsters on screen. Elites lose 30% health."),
                _ => ("jiho", "vo_jiho_ftue_freeze", "FREEZE CLOCK", "Freezes every monster on screen for 4 s."),
            };
            var c = Card("item." + effect, voice, agent, "NEW ITEM", title, body, FtueRadio.Size.Item, RunTop);
            c.Icon = icon;
            c.Seconds = 5f;
            FtueRadio.Show(c);
        }

        // FR_06 · First chest: no timer, corners + hand on CLAIM.
        public static void Chest(RectTransform claim)
        {
            if (claim == null) return;
            var c = Card("chest", "vo_chen_ftue_chest", "chen", null, "YOUR FIRST CHEST",
                "No timer this time. Rank a power to 5 and own its partner card: the next chest evolves it.", FtueRadio.Size.Small, 452);
            c.Target = () => FtueRadio.ScreenRect(claim);
            c.Hand = () => FtueRadio.TapPoint(claim);
            c.Alive = () => Live(claim);
            c.Modal = true;
            FtueRadio.Show(c);
        }

        // FR_07 · First death: the free revive.
        public static void Revive(RectTransform button)
        {
            if (button == null) return;
            var c = Card("revive", "vo_riley_ftue_revive", "riley", null, "FIRST ONE'S ON US",
                "Your first revive is free, no ad. Later: one ad per run, then coins.", FtueRadio.Size.Normal, 520);
            c.Target = () => FtueRadio.ScreenRect(button);
            c.Hand = () => FtueRadio.TapPoint(button);
            c.Alive = () => Live(button) && !Ftue.Done(Ftue.Revive);
            c.Modal = true;
            FtueRadio.Show(c);
        }

        // FR_08 · First result: the newcomer gift, hand on "Arsenal ›".
        public static void Result(RectTransform arsenalLink)
        {
            if (arsenalLink == null) return;
            var c = Card("result", "vo_mai_ftue_result_gift", "mai", null, "NEWCOMER GIFT",
                "I topped you up to 400 coins. That's your first real gun.", FtueRadio.Size.Small, 470);
            c.Hand = () => FtueRadio.TapPoint(arsenalLink);
            c.Alive = () => Live(arsenalLink);
            c.Modal = true;
            FtueRadio.Show(c);
        }

        /// The card's claim comes from the weapon data, so it cannot go stale when guns are retuned
        /// (QA 04/10: "hits harder" while the Makarov's per-shot damage was lower than the Pistol's).
        static string GunPitch(WeaponData gun, WeaponData current)
        {
            if (current == null) return $"{gun.weaponName} is your first real gun. Tap BUY.";
            int a = Mathf.RoundToInt(CombatPower.WeaponPower(gun, 1)), b = Mathf.RoundToInt(CombatPower.WeaponPower(current, 1));
            string why = gun.damage > current.damage && gun.fireRate > current.fireRate ? "hits harder and fires faster"
                : gun.fireRate > current.fireRate ? "fires faster" : gun.damage > current.damage ? "hits harder" : "is stronger";
            return $"{gun.weaponName} {why} than your {current.weaponName}: power {a:N0} vs {b:N0}. Tap BUY.";
        }

        // FR_09 · First gun: spotlight + corners on the gun cell, hand on BUY.
        public static void Gun(RectTransform cell, RectTransform buy, WeaponData gun, WeaponData current)
        {
            if (cell == null || gun == null) return;
            var c = Card("gun", "vo_lukas_ftue_gun", "lukas", "ARMORY", "YOUR FIRST NEW GUN", GunPitch(gun, current),
                FtueRadio.Size.Normal, 246);
            c.Spotlight = true;
            c.Target = () => FtueRadio.ScreenRect(cell);
            c.Target2 = () => FtueRadio.ScreenRect(buy);   // the button the hand points at is lit too
            c.Hand = () => FtueRadio.TapPoint(buy);
            c.Alive = () => Live(cell) && !Ftue.Done(Ftue.Gun);
            c.Modal = true;
            FtueRadio.Show(c);
        }

        // FR_10–12 · Account unlocks: card over the popup, hand on its main button.
        public static void Unlock(int feature, RectTransform cta, System.Func<bool> alive)
        {
            if (cta == null) return;
            var (agent, voice, title, body) = feature switch
            {
                0 => ("chen", "vo_chen_ftue_lv2", "MISSIONS + PASS", "Missions are open. Clear them for Pass XP and free loot."),
                1 => ("mai", "vo_mai_ftue_lv3", "GACHA", "The gacha is open and your first pull is free."),
                _ => ("lukas", "vo_lukas_ftue_lv5", "GUN STARS", "Add stars to a gun for more damage and faster fire."),
            };
            var c = Card("unlock." + feature, voice, agent, null, title, body, FtueRadio.Size.Small, 430);
            c.Hand = () => FtueRadio.TapPoint(cta);
            c.Alive = () => Live(cta) && (alive == null || alive());
            c.Modal = true;
            FtueRadio.Show(c);
        }
    }
}
