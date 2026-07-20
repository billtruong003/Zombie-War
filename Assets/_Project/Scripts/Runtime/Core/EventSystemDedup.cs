using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace ZombieWar
{
    /// <summary>
    /// Mỗi scene (Menu, Map_Level1...) đều có EventSystem riêng để chạy standalone được.
    /// Khi load additive sẽ bị 2 EventSystem active -> Unity disable input -> UI chết click.
    /// Guard này chạy tự động, giữ đúng 1 EventSystem (ưu tiên cái cũ nhất), tắt phần thừa.
    /// </summary>
    public static class EventSystemDedup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded += (_, _) => Dedup();
            SceneManager.sceneUnloaded += _ => Dedup();
            Dedup();
        }

        private static void Dedup()
        {
            var systems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (systems.Length <= 1) return;

            // Giữ cái được spawn sớm nhất (instance id nhỏ hơn thường là cũ hơn / thuộc scene gốc)
            EventSystem keep = systems[0];
            for (int i = 1; i < systems.Length; i++)
                if (systems[i].GetInstanceID() < keep.GetInstanceID()) keep = systems[i];

            foreach (var es in systems)
            {
                if (es == keep) continue;
                Debug.Log($"[EventSystemDedup] Disable duplicate EventSystem '{es.gameObject.scene.name}/{es.name}'");
                es.gameObject.SetActive(false);
            }
        }
    }
}
