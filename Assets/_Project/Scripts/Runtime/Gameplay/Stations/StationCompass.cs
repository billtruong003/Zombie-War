using UnityEngine;

namespace ZombieWar.Stations
{
    /// <summary>
    /// A chevron on the ground at the player's feet pointing at the nearest station that still has
    /// something to give. GDD §7 "orient": the world must always show a reason to move, and the HUD
    /// prefab is owner-authored, so the pointer lives in the world instead of on the canvas.
    ///
    /// Coloured by the station's kind (the same palette as its ring and beam), hidden once the player
    /// is close enough to see the station itself, and hidden when nothing is worth walking to.
    /// </summary>
    [DisallowMultipleComponent]
    public class StationCompass : MonoBehaviour
    {
        [Tooltip("Metres from the player's feet to the chevron's tip.")]
        [SerializeField] private float offset = 2.2f;
        [SerializeField] private float size = 0.7f;
        [SerializeField] private float width = 0.22f;
        [Tooltip("Closer than this, the station's own ring and beam do the job; the chevron hides.")]
        [SerializeField] private float hideWithin = 7f;
        [SerializeField] private Material material;

        LineRenderer _line;

        public bool Visible => _line != null && _line.enabled;

        public void SetMaterial(Material m)
        {
            material = m;
            if (_line != null) _line.sharedMaterial = m;
        }

        void Awake()
        {
            var go = new GameObject("StationCompass");
            go.transform.SetParent(transform, false);
            _line = go.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.positionCount = 3;
            _line.widthMultiplier = width;
            _line.numCapVertices = 2;
            _line.numCornerVertices = 2;
            _line.alignment = LineAlignment.TransformZ;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // lie flat on the ground
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            if (material != null) _line.sharedMaterial = material;
            _line.enabled = false;
        }

        /// <summary>Points at <paramref name="target"/> from <paramref name="player"/>, or hides.</summary>
        public void Point(Vector3 player, Station target)
        {
            if (target == null)
            {
                _line.enabled = false;
                return;
            }

            Vector3 to = target.transform.position - player;
            to.y = 0f;
            float distance = to.magnitude;
            if (distance <= hideWithin)
            {
                _line.enabled = false;
                return;
            }

            Vector3 dir = to / distance;
            Vector3 side = new Vector3(-dir.z, 0f, dir.x);
            Vector3 tip = player + dir * offset + Vector3.up * 0.06f;
            Vector3 back = tip - dir * size;

            _line.SetPosition(0, back + side * size * 0.6f);
            _line.SetPosition(1, tip);
            _line.SetPosition(2, back - side * size * 0.6f);

            var colour = WorldSignal.ColorOf(target.Anchor.kind);
            _line.startColor = colour;
            _line.endColor = colour;
            _line.enabled = true;
        }
    }
}
