#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace BillGameCore.BillFav
{
    /// <summary>
    /// A single favorited item. Serializable, survives domain reload.
    /// Stores a GlobalObjectId string so it can reference both assets and scene objects.
    /// </summary>
    [Serializable]
    public class BillFavItem
    {
        [SerializeField] string _globalId;
        [SerializeField] string _typeName;
        [SerializeField] string _cachedName;
        [SerializeField] ItemKind _kind;
        [SerializeField] string _tag;
        [SerializeField] int _id;

        // Serialized object reference — Unity maintains this across save/load/domain reload.
        // Mirrors vFavorites' pattern: direct ref + GlobalObjectId fallback.
        [SerializeField] Object _objCache;

        public enum ItemKind { Asset, Folder, SceneObject }

        // ───────────────────────────────────────────
        // Construction
        // ───────────────────────────────────────────

        public BillFavItem() { }

        public BillFavItem(Object obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            var gid = GlobalObjectId.GetGlobalObjectIdSlow(obj);
            _globalId = gid.ToString();
            _typeName = obj.GetType().AssemblyQualifiedName;
            _cachedName = obj.name;
            _objCache = obj;

            if (obj is GameObject go && go.scene.rootCount != 0)
                _kind = ItemKind.SceneObject;
            else if (AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(obj)))
                _kind = ItemKind.Folder;
            else
                _kind = ItemKind.Asset;

            if (_id == 0) _id = Guid.NewGuid().GetHashCode();
        }

        // ───────────────────────────────────────────
        // Properties
        // ───────────────────────────────────────────

        public int Id
        {
            get
            {
                if (_id == 0) _id = Guid.NewGuid().GetHashCode();
                return _id;
            }
        }

        public string Name
        {
            get
            {
                if (IsLoaded) _cachedName = Obj.name;
                return _cachedName ?? "(unknown)";
            }
        }

        public string Tag { get => _tag ?? ""; set => _tag = value; }
        public ItemKind Kind => _kind;

        public bool IsFolder => _kind == ItemKind.Folder;
        public bool IsAsset => _kind == ItemKind.Asset;
        public bool IsSceneObject => _kind == ItemKind.SceneObject;

        public Object Obj
        {
            get
            {
                if (_objCache != null) return _objCache;

                if (GlobalObjectId.TryParse(_globalId, out var gid))
                {
                    _objCache = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);

                    // Fallback: GlobalObjectIdentifierToObjectSlow can fail for assets.
                    // Load via GUID → asset path instead (like vFav's globalId.guid.ToPath()).
                    if (_objCache == null)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(gid.assetGUID.ToString());
                        if (!string.IsNullOrEmpty(path))
                            _objCache = AssetDatabase.LoadAssetAtPath<Object>(path);
                    }
                }

                return _objCache;
            }
        }

        public bool IsLoaded => Obj != null;

        public bool IsDeleted
        {
            get
            {
                if (IsLoaded) return false;
                if (!IsSceneObject) return true;

                // Scene object: deleted if scene is loaded but object is gone
                if (!GlobalObjectId.TryParse(_globalId, out var gid)) return true;
                var scenePath = AssetDatabase.GUIDToAssetPath(gid.assetGUID.ToString());
                if (string.IsNullOrEmpty(scenePath)) return true;

                // If scene is loaded and object not found → deleted
                for (int i = 0; i < UnityEditor.SceneManagement.EditorSceneManager.sceneCount; i++)
                    if (UnityEditor.SceneManagement.EditorSceneManager.GetSceneAt(i).path == scenePath)
                        return true;

                return false; // Scene not loaded → just not loaded, not deleted
            }
        }

        public string AssetPath
        {
            get
            {
                if (!GlobalObjectId.TryParse(_globalId, out var gid)) return "";
                return AssetDatabase.GUIDToAssetPath(gid.assetGUID.ToString());
            }
        }

        public Type ResolvedType
        {
            get
            {
                if (IsLoaded) return Obj.GetType();
                if (!string.IsNullOrEmpty(_typeName))
                    return Type.GetType(_typeName) ?? typeof(DefaultAsset);
                return typeof(DefaultAsset);
            }
        }

        // ───────────────────────────────────────────
        // Cache management
        // ───────────────────────────────────────────

        public void InvalidateCache()
        {
            _objCache = null;
        }

        public override bool Equals(object other)
            => other is BillFavItem item && item._globalId == _globalId;

        public override int GetHashCode() => _globalId?.GetHashCode() ?? 0;
    }
}
#endif
