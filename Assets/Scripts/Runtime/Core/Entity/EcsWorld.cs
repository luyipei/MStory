using System;
using System.Collections.Generic;

namespace Runtime.Core.Entity
{
    /// <summary>
    /// 轻量自研 ECS：Entity 用 int 表示；组件用 SparseSet 存储（data-oriented）。
    /// 不依赖 DOTS/Jobs/Burst，适配 WebGL/小游戏等平台。
    /// </summary>
    public sealed class EcsWorld
    {
        private int _nextEntityId = 1;
        private readonly Stack<int> _freeIds = new Stack<int>(256);

        // 版本号（避免复用 id 造成野引用）；EntityHandle 为 (id, version)
        private int[] _versions = new int[1024];
        private bool[] _alive = new bool[1024];


        private readonly Dictionary<Type, IComponentPool> _pools = new Dictionary<Type, IComponentPool>(64);

        public EntityHandle CreateEntity()
        {
            int id;
            if (_freeIds.Count > 0)
            {
                id = _freeIds.Pop();
            }
            else
            {
                id = _nextEntityId++;
                EnsureVersionCapacity(id);
            }

            _alive[id] = true;

            // 新实体版本号不变；Destroy 时递增
            return new EntityHandle(id, _versions[id]);
        }

        public bool IsAlive(EntityHandle e)
        {
            if (e.Id <= 0) return false;
            if (e.Id >= _versions.Length) return false;
            if (!_alive[e.Id]) return false;
            return _versions[e.Id] == e.Version;
        }

        public bool IsAlive(int entityId)
        {
            if (entityId <= 0) return false;
            if (entityId >= _versions.Length) return false;
            return _alive[entityId];
        }

        public void DestroyEntity(EntityHandle e)
        {
            if (!IsAlive(e)) return;
            DestroyEntity(e.Id);
        }

        /// <summary>
        /// 按 id 销毁当前实体（系统内部遍历组件池时用这个更安全）。
        /// </summary>
        public void DestroyEntity(int entityId)
        {
            if (!IsAlive(entityId)) return;

            // 从所有 pool 移除该实体
            foreach (var kv in _pools)
                kv.Value.Remove(entityId);

            _alive[entityId] = false;

            // 递增版本号，使旧 handle 失效
            _versions[entityId]++;
            _freeIds.Push(entityId);
        }

        public SparseSet<T> GetPool<T>() where T : struct
        {
            var type = typeof(T);
            if (_pools.TryGetValue(type, out var pool))
                return (SparseSet<T>)pool;

            var created = new SparseSet<T>();
            _pools[type] = created;
            return created;
        }

        public bool Has<T>(EntityHandle e) where T : struct
        {
            if (!IsAlive(e)) return false;
            return GetPool<T>().Has(e.Id);
        }

        public void Add<T>(EntityHandle e, in T value) where T : struct
        {
            if (!IsAlive(e)) return;
            GetPool<T>().Add(e.Id, value);
        }

        public ref T Get<T>(EntityHandle e) where T : struct
        {
            return ref GetPool<T>().GetRef(e.Id);
        }

        public bool Remove<T>(EntityHandle e) where T : struct
        {
            if (!IsAlive(e)) return false;
            return GetPool<T>().Remove(e.Id);
        }

        private void EnsureVersionCapacity(int id)
        {
            if (id < _versions.Length) return;
            var newLen = _versions.Length;
            while (newLen <= id) newLen *= 2;
            Array.Resize(ref _versions, newLen);
            Array.Resize(ref _alive, newLen);
        }
    }

    public readonly struct EntityHandle
    {
        public readonly int Id;
        public readonly int Version;

        public EntityHandle(int id, int version)
        {
            Id = id;
            Version = version;
        }

        public override string ToString() => $"Entity({Id}:{Version})";
    }
}
