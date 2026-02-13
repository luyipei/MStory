using System;

namespace Runtime.Core.Entity
{
    internal interface IComponentPool
    {
        void Remove(int entityId);
    }

    /// <summary>
    /// SparseSet：O(1) Add/Remove/Has/Get，内存连续，适合海量对象。
    /// denseEntities: [entityId]
    /// dense:         [component]
    /// sparse: entityId -> denseIndex+1 (0 表示不存在)
    /// </summary>
    public sealed class SparseSet<T> : IComponentPool where T : struct
    {
        private int[] _denseEntities = new int[256];
        private T[] _dense = new T[256];
        private int _count;

        private int[] _sparse = new int[1024];

        public int Count => _count;

        public int[] DenseEntities => _denseEntities;

        public bool Has(int entityId)
        {
            if (entityId <= 0 || entityId >= _sparse.Length) return false;
            return _sparse[entityId] != 0;
        }

        public void Add(int entityId, in T component)
        {
            EnsureSparseCapacity(entityId);

            var slot = _sparse[entityId];
            if (slot != 0)
            {
                _dense[slot - 1] = component;
                return;
            }

            EnsureDenseCapacity(_count + 1);
            _denseEntities[_count] = entityId;
            _dense[_count] = component;
            _sparse[entityId] = _count + 1;
            _count++;
        }

        public ref T GetRef(int entityId)
        {
            if (!Has(entityId))
                throw new ArgumentException($"Entity {entityId} does not have component {typeof(T).Name}");

            var idx = _sparse[entityId] - 1;
            return ref _dense[idx];
        }

        public bool Remove(int entityId)
        {
            if (!Has(entityId)) return false;

            var idx = _sparse[entityId] - 1;
            var last = _count - 1;

            if (idx != last)
            {
                var movedEntity = _denseEntities[last];
                _denseEntities[idx] = movedEntity;
                _dense[idx] = _dense[last];
                _sparse[movedEntity] = idx + 1;
            }

            _denseEntities[last] = 0;
            _dense[last] = default;
            _sparse[entityId] = 0;
            _count--;
            return true;
        }

        void IComponentPool.Remove(int entityId) => Remove(entityId);

        private void EnsureDenseCapacity(int size)
        {
            if (size <= _dense.Length) return;
            var newLen = _dense.Length;
            while (newLen < size) newLen *= 2;
            Array.Resize(ref _denseEntities, newLen);
            Array.Resize(ref _dense, newLen);
        }

        private void EnsureSparseCapacity(int entityId)
        {
            if (entityId < _sparse.Length) return;
            var newLen = _sparse.Length;
            while (newLen <= entityId) newLen *= 2;
            Array.Resize(ref _sparse, newLen);
        }
    }
}
