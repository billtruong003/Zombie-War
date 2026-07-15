using System.Collections.Generic;
using UnityEngine;

namespace BillGameCore
{
    public class PoolService : IPoolService, IInitializable, IDisposableService
    {
        private readonly Dictionary<string, Queue<GameObject>> _pools = new(16);
        private readonly Dictionary<string, GameObject> _prefabs = new(16);
        private readonly Dictionary<string, Transform> _containers = new(16);
        private readonly Dictionary<string, PoolDefinition> _defs = new(16);
        private readonly Dictionary<GameObject, string> _active = new(64);
        private Transform _root;

        public void Initialize()
        {
            _root = new GameObject("[Bill.Pools]").transform;
            _root.SetParent(CoroutineRunner.Instance.transform.parent);
            var cfg = BillBootstrapConfig.Instance;
            if (cfg?.defaultPools == null) return;
            foreach (var d in cfg.defaultPools)
                if (d?.prefab != null) Register(d);
        }

        public void Register(string key, GameObject prefab, int warmCount = 5)
            => Register(new PoolDefinition { key = key, prefab = prefab, warmCount = warmCount });

        private void Register(PoolDefinition def)
        {
            if (string.IsNullOrEmpty(def.key) || _pools.ContainsKey(def.key)) return;
            _prefabs[def.key] = def.prefab;
            _defs[def.key] = def;
            _pools[def.key] = new Queue<GameObject>(def.warmCount);
            var c = new GameObject($"Pool:{def.key}").transform;
            c.SetParent(_root);
            _containers[def.key] = c;
            WarmUp(def.key, def.warmCount);
        }

        // --- Spawn overloads ---
        public GameObject Spawn(string key) => SpawnImpl(key, Vector3.zero, Quaternion.identity, null);
        public GameObject Spawn(string key, Vector3 p, Quaternion r) => SpawnImpl(key, p, r, null);
        public GameObject Spawn(string key, Transform parent) => SpawnImpl(key, Vector3.zero, Quaternion.identity, parent);
        public GameObject Spawn(string key, Vector3 p, Quaternion r, Transform parent) => SpawnImpl(key, p, r, parent);
        public T Spawn<T>(string key) where T : Component => SpawnImpl(key, Vector3.zero, Quaternion.identity, null)?.GetComponent<T>();
        public T Spawn<T>(string key, Vector3 p, Quaternion r) where T : Component => SpawnImpl(key, p, r, null)?.GetComponent<T>();

        private GameObject SpawnImpl(string key, Vector3 pos, Quaternion rot, Transform parent)
        {
            if (!_pools.ContainsKey(key))
            {
                var pf = Resources.Load<GameObject>($"Pools/{key}");
                if (pf != null) Register(key, pf);
                else { Debug.LogError($"[Bill.Pool] '{key}' not found. Register or place in Resources/Pools/"); return null; }
            }

            var pool = _pools[key];
            GameObject obj = null;
            while (pool.Count > 0 && obj == null) obj = pool.Dequeue();
            if (obj == null) obj = CreateInstance(key);
            if (obj == null) return null;

            obj.transform.SetPositionAndRotation(pos, rot);
            obj.transform.SetParent(parent);
            obj.SetActive(true);
            _active[obj] = key;

            var po = obj.GetComponent<PooledObject>();
            if (po != null) po.OnSpawnedFromPool();

            if (_defs.TryGetValue(key, out var def) && def.autoReturnTime > 0f)
                Return(obj, def.autoReturnTime);

            return obj;
        }

        private GameObject CreateInstance(string key)
        {
            if (!_prefabs.TryGetValue(key, out var pf)) return null;
            var obj = Object.Instantiate(pf);
            obj.name = pf.name;
            if (obj.GetComponent<PooledObject>() == null) obj.AddComponent<PooledObject>();
            return obj;
        }

        // --- Return overloads ---
        public void Return(GameObject obj)
        {
            if (obj == null) return;
            if (!_active.TryGetValue(obj, out var key)) { Object.Destroy(obj); return; }
            ReturnImpl(obj, key);
        }

        public void Return(GameObject obj, float delay) { if (obj != null) CoroutineRunner.RunDelayed(delay, () => Return(obj)); }

        public void ReturnAll(string key)
        {
            var batch = new List<GameObject>();
            foreach (var kv in _active) if (kv.Value == key) batch.Add(kv.Key);
            foreach (var o in batch) ReturnImpl(o, key);
        }

        public void ReturnAll()
        {
            var batch = new List<KeyValuePair<GameObject, string>>(_active);
            foreach (var kv in batch) ReturnImpl(kv.Key, kv.Value);
        }

        private void ReturnImpl(GameObject obj, string key)
        {
            if (obj == null) return;
            var po = obj.GetComponent<PooledObject>();
            if (po != null) po.OnReturnedToPool();
            obj.SetActive(false);

            if (_defs.TryGetValue(key, out var def) && def.maxSize > 0 && _pools[key].Count >= def.maxSize)
                Object.Destroy(obj);
            else
            {
                if (_containers.TryGetValue(key, out var c)) obj.transform.SetParent(c);
                _pools[key].Enqueue(obj);
            }
            _active.Remove(obj);
        }

        public void WarmUp(string key, int count)
        {
            if (!_prefabs.ContainsKey(key)) return;
            for (int i = 0; i < count; i++)
            {
                var obj = CreateInstance(key);
                if (obj == null) continue;
                obj.SetActive(false);
                obj.transform.SetParent(_containers[key]);
                _pools[key].Enqueue(obj);
            }
        }

        public int GetPooledCount(string key) => _pools.TryGetValue(key, out var q) ? q.Count : 0;
        public int GetActiveCount(string key) { int c = 0; foreach (var kv in _active) if (kv.Value == key) c++; return c; }

        public string GetStats()
        {
            var sb = new System.Text.StringBuilder(256);
            sb.AppendLine("[Bill.Pool] Stats:");
            foreach (var kv in _pools) sb.AppendLine($"  {kv.Key}: pooled={kv.Value.Count} active={GetActiveCount(kv.Key)}");
            return sb.ToString();
        }

        public void Cleanup()
        {
            foreach (var kv in _pools) while (kv.Value.Count > 0) { var o = kv.Value.Dequeue(); if (o) Object.Destroy(o); }
            _pools.Clear(); _prefabs.Clear(); _containers.Clear(); _defs.Clear(); _active.Clear();
            if (_root != null) Object.Destroy(_root.gameObject);
        }
    }

    public class PooledObject : MonoBehaviour
    {
        public virtual void OnSpawnedFromPool() { }
        public virtual void OnReturnedToPool() { }
        public void ReturnToPool() => Bill.Pool?.Return(gameObject);
        public void ReturnToPool(float delay) => Bill.Pool?.Return(gameObject, delay);
    }
}
