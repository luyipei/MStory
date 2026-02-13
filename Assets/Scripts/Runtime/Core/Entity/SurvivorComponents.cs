using UnityEngine;

namespace Runtime.Core.Entity
{
    // ===== 基础数据组件（SoA 存储在 SparseSet 中） =====

    public struct Position2
    {
        public Vector2 Value;
    }

    public struct Velocity2
    {
        public Vector2 Value;
    }

    public struct MoveInput2
    {
        public Vector2 Value; // 归一化方向
    }

    public struct MoveSpeed
    {
        public float Value;
    }

    public struct Health
    {
        public float Current;
        public float Max;
    }

    public struct Lifetime
    {
        public float Remaining;
    }

    public struct Projectile
    {
        public float Damage;
        public float HitRadius;
        public int OwnerEntityId;
    }

    public struct EnemySpawner
    {
        public float SpawnRadius;
        public float BaseSpawnPerSecond;
        public float SpawnPerSecondPerMinute;
        public int MaxAlive;

        public float Accumulator;
        public float Elapsed;
        public uint Seed;
    }

    // ===== Tag（空组件） =====
    public struct PlayerTag { }
    public struct EnemyTag { }
    public struct ProjectileTag { }

    // ===== 视图（可先用 Transform 引用；后续可替换成 ViewId + Pool） =====
    public struct ViewRef
    {
        public Transform Transform;
    }
}
