using BillGameCore;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// One node in the campaign dot row: the dot itself plus the connector that joins it to the
    /// previous node. Authored once in the prefab and cloned per catalog entry.
    ///
    /// Size carries the state as well as colour - a colour-only difference is not enough to read at
    /// phone size, and the selected node has to be obvious at a glance.
    /// </summary>
    public sealed class CampaignDotView : MonoBehaviour
    {
        [SerializeField] private Image dot;
        [Tooltip("Line joining this dot to the one before it. Hidden on the first dot.")]
        [SerializeField] private Image connector;

        [SerializeField] private float selectedScale = 1.45f;
        [SerializeField] private float completedScale = 1.1f;
        [SerializeField] private float lockedScale = 0.72f;
        [SerializeField] private float popDuration = 0.18f;

        private float _appliedScale = -1f;

        public void Apply(CampaignSelection.DotState state, Color color, bool showConnector)
        {
            if (connector != null)
            {
                connector.enabled = showConnector;
                // A completed or selected node has a "travelled" link behind it; ahead of progress the
                // link is muted, which is what makes the path read as progress rather than decoration.
                bool travelled = state == CampaignSelection.DotState.Completed
                              || state == CampaignSelection.DotState.Selected;
                var c = color;
                c.a = travelled ? 0.9f : 0.35f;
                connector.color = c;
            }

            if (dot == null) return;
            dot.color = color;

            float target = state switch
            {
                CampaignSelection.DotState.Selected => selectedScale,
                CampaignSelection.DotState.Completed => completedScale,
                CampaignSelection.DotState.Available => 1f,
                _ => lockedScale,
            };

            // Only animate a real change, so a Refresh triggered by an unrelated campaign event does
            // not re-pop every dot in the row.
            if (Mathf.Approximately(_appliedScale, target)) return;
            _appliedScale = target;

            var rt = dot.rectTransform;
            if (!Application.isPlaying || Bill.Tween == null)
            {
                rt.localScale = Vector3.one * target;
                return;
            }

            // A single Float tween driving uniform scale, NOT BillTween.ScaleTo. ScaleTo builds three
            // axis tweens that are already in the active list and then also joins them into a
            // sequence, so attaching an ease to it double-ticks (see the billgamecore skill notes).
            float from = rt.localScale.x;
            BillTween.Float(from, target, popDuration, v => rt.localScale = Vector3.one * v)
                ?.SetEase(EaseType.OutBack).SetTarget(this);
        }
    }
}
