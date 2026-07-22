using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZombieWar;

namespace ZombieWar.Tests
{
    /// PlayMode tests cho scene-ownership cua Player spawn (fix orphan Slice 2):
    /// player spawn phai thuoc scene chua PlayerSpawner (khong phai active scene),
    /// va phai bi destroy khi scene do unload.
    public class PlayerSpawnerSceneTests
    {
        private static void SetPrivate(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        [UnityTest]
        public IEnumerator SpawnedPlayer_BelongsToSpawnerScene_NotActiveScene()
        {
            Scene spawnerScene = SceneManager.CreateScene("SpawnOwnershipTest");
            Assert.AreNotEqual(spawnerScene, SceneManager.GetActiveScene(),
                "Tien de: active scene phai KHAC scene chua spawner (tai hien dung bug Bootstrap).");

            var spawnerGO = new GameObject("TestSpawner");
            SceneManager.MoveGameObjectToScene(spawnerGO, spawnerScene);
            var spawner = spawnerGO.AddComponent<PlayerSpawner>();
            SetPrivate(spawner, "spawnOnStart", false);

            var prefabStandIn = new GameObject("PlayerPrefabStandIn");
            SetPrivate(spawner, "playerPrefab", prefabStandIn);

            GameObject spawned = spawner.Spawn();
            Assert.IsNotNull(spawned);
            Assert.AreEqual(spawnerScene, spawned.scene,
                "Player spawn phai duoc keo ve scene chua spawner, khong nam o active scene.");

            yield return SceneManager.UnloadSceneAsync(spawnerScene);
            Assert.IsTrue(spawned == null, "Unload scene gameplay phai destroy player — khong con orphan.");

            Object.Destroy(prefabStandIn);
        }

        [UnityTest]
        public IEnumerator RepeatedSceneCycles_LeaveNoOrphanPlayer()
        {
            var prefabStandIn = new GameObject("PlayerPrefabStandIn_Cycle");
            prefabStandIn.tag = "Player";

            for (int cycle = 0; cycle < 3; cycle++)
            {
                Scene scene = SceneManager.CreateScene($"SpawnCycleTest_{cycle}");
                var spawnerGO = new GameObject("TestSpawner");
                SceneManager.MoveGameObjectToScene(spawnerGO, scene);
                var spawner = spawnerGO.AddComponent<PlayerSpawner>();
                SetPrivate(spawner, "spawnOnStart", false);
                SetPrivate(spawner, "playerPrefab", prefabStandIn);

                var spawned = spawner.Spawn();
                Assert.AreEqual(scene, spawned.scene);

                // prefabStandIn cung tag Player nen tru 1 khi dem instance song.
                int liveDuringPlay = GameObject.FindGameObjectsWithTag("Player").Length - 1;
                Assert.AreEqual(1, liveDuringPlay, $"Cycle {cycle}: phai dung 1 player khi dang choi.");

                yield return SceneManager.UnloadSceneAsync(scene);

                int liveAfterUnload = GameObject.FindGameObjectsWithTag("Player").Length - 1;
                Assert.AreEqual(0, liveAfterUnload, $"Cycle {cycle}: unload xong khong duoc con player nao.");
            }

            Object.Destroy(prefabStandIn);
        }
    }
}
