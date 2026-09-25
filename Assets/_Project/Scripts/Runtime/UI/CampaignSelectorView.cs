using UnityEngine;

namespace ZombieWar.UI
{
    /// <summary>
    /// RETIRED. The endless world has no stages, so there is nothing to select.
    ///
    /// The owner-authored Hub prefab still carries this component and its labels, arrows and dot row.
    /// Agents may not edit that prefab, so the component survives only to hide its own widgets - a
    /// missing script would leave the stale "Recommended ... Combat Power" line on screen. Delete this
    /// file together with <see cref="CampaignDotView"/> once the owner removes the component.
    /// </summary>
    public sealed class CampaignSelectorView : MonoBehaviour
    {
        private void Awake()
        {
            for (int i = 0; i < transform.childCount; i++)
                transform.GetChild(i).gameObject.SetActive(false);
        }
    }
}
