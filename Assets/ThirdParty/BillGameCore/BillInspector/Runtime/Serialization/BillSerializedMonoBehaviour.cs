using System.Collections.Generic;
using UnityEngine;

namespace BillInspector
{
    /// <summary>
    /// MonoBehaviour with automatic serialization for Dictionary, HashSet, Tuple, etc.
    /// Fields that Unity can't serialize are automatically handled by BillSerializer.
    /// </summary>
    public abstract class BillSerializedMonoBehaviour : MonoBehaviour, ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector]
        private byte[] _billSerializedData;

        [SerializeField, HideInInspector]
        private List<Object> _billObjectReferences = new();

        public void OnBeforeSerialize()
        {
            BillSerializer.SerializeObject(this, ref _billSerializedData, ref _billObjectReferences);
        }

        public void OnAfterDeserialize()
        {
            BillSerializer.DeserializeObject(this, _billSerializedData, _billObjectReferences);
        }
    }
}
