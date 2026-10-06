using BillGameCore;
using UnityEngine;

namespace ZombieWar
{
    /// Lives on the persistent Bootstrap scene. Waits for Bill services to finish their
    /// [RuntimeInitializeOnLoadMethod] boot (order between that and MonoBehaviour Start is not
    /// guaranteed, so we poll IsReady instead of subscribing) and then drives the app into the menu.
    public class BootstrapEntry : MonoBehaviour
    {
        private bool _entered;
        private bool _cloudAsked, _cloudDone;

        private void Update()
        {
            if (_entered || !Bill.IsReady) return;
            // A reinstall gets its cloud profile back before the menu reads the local one.
            if (!_cloudAsked) { _cloudAsked = true; Online.CloudSave.RestoreAtBoot(() => _cloudDone = true); }
            if (!_cloudDone) return;
            _entered = true;
            GameSettings.Apply();
#if UNITY_EDITOR
            // ZombieWar/Dev/Play Skill Sandbox sets this for one play session.
            if (UnityEditor.SessionState.GetBool("zw.sandbox", false))
            {
                UnityEditor.SessionState.SetBool("zw.sandbox", false);
                GameFlow.StartSandbox();
                return;
            }
#endif
            GameFlow.EnterMenu();
        }
    }
}
