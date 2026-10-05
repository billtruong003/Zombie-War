#if UNITY_EDITOR || DEVELOPMENT_BUILD || ZW_CHEATS
using UnityEngine;
using ZombieWar.Bosses;

namespace ZombieWar.Dev
{
    /// <summary>
    /// Boss lab (backlog #39, owner 05/10: "a lab in Unity first, so we know how big the arena is, how
    /// the player meets the wall, how the boss fights and which minions come"). In a normal run on the
    /// real map: <c>zw.boss.lab [radius] [solid|shock]</c> rings the player in an arena and brings in the
    /// Titan with its HP bar. The owner picks the radius, the wall and the attack set from the
    /// recordings before the Titan joins the run at 5/10/15/20 min.
    /// </summary>
    public static class BossLab
    {
        public const string TitanPath = "Dev/BOSS_Titan", WallPath = "Dev/BossWall";
        public static BossArena Arena { get; private set; }
        public static TitanBoss Titan { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Register()
        {
            var cheat = BillGameCore.Bill.Cheat;
            if (cheat == null) return;
            cheat.Register<string>("zw.boss.lab", Start, "Boss lab: zw.boss.lab \"18 solid\" (radius, solid|shock)");
            cheat.Register("zw.boss.end", End, "Boss lab: remove the arena and the Titan");
        }

        public static void Start(string args)
        {
            float radius = 18f; var mode = BossArena.WallMode.Solid;
            if (!string.IsNullOrEmpty(args))
                foreach (var part in args.Split(' '))
                {
                    if (float.TryParse(part, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float r)) radius = r;
                    else if (part.ToLowerInvariant() == "shock") mode = BossArena.WallMode.Shock;
                }
            Begin(radius, mode, 6000f);
        }

        public static TitanBoss Begin(float radius, BossArena.WallMode mode, float health)
        {
            End();
            var player = PlayerMovement.Instance;
            if (player == null) return null;
            Vector3 c = player.transform.position;
            Arena = BossArena.Build(c, radius, mode, Resources.Load<GameObject>(WallPath));
            var prefab = Resources.Load<GameObject>(TitanPath);
            if (prefab == null) { Debug.LogError("[BossLab] missing Resources/" + TitanPath + " (run ZombieWar/Dev/Build Boss Lab)"); return null; }
            // Up the screen from the player (the camera looks north), so the entrance is in view.
            Vector3 at = c + Vector3.forward * Mathf.Min(8f, radius * 0.55f);
            var go = Object.Instantiate(prefab, new Vector3(at.x, 0f, at.z), Quaternion.LookRotation(c - at, Vector3.up));
            Titan = go.GetComponent<TitanBoss>();
            Titan.maxHealth = health;
            go.SetActive(false); go.SetActive(true);   // re-run OnEnable with the lab health
            BossBarView.Show(Titan, "TITAN");
            TitanBoss.Died += OnDied;
            return Titan;
        }

        static void OnDied(TitanBoss t)
        {
            TitanBoss.Died -= OnDied;
            if (Arena != null) Arena.Remove();
            Arena = null;
        }

        public static void End()
        {
            if (Titan != null) Object.Destroy(Titan.gameObject);
            if (Arena != null) Arena.Remove();
            Titan = null; Arena = null;
            TitanBoss.Died -= OnDied;
        }
    }
}
#endif
