using System.Collections.Generic;
using UnityEngine;

namespace Runtime.Core.Entity
{
    /// <summary>
    /// MonoBehaviour 驱动自研 ECS World 更新。
    /// </summary>
    public sealed class SurvivorEcsRunner : MonoBehaviour
    {
        [Header("Prefabs")]
        public GameObject playerPrefab;
        public GameObject enemyPrefab;
        public GameObject projectilePrefab;

        [Header("Player")]
        public float playerMoveSpeed = 5f;
        public float playerMaxHealth = 100f;

        [Header("Enemy")]
        public float enemyMoveSpeed = 2.5f;
        public float enemyMaxHealth = 15f;

        [Header("Spawner")]
        public float spawnRadius = 10f;
        public float baseSpawnPerSecond = 1f;
        public float spawnPerSecondPerMinute = 1f;
        public int maxAlive = 250;

        [Header("Ability")]
        public float shotCooldown = 0.35f;
        public float shotRange = 9f;
        public float projectileSpeed = 16f;
        public float projectileDamage = 6f;
        public float projectileHitRadius = 0.35f;
        public float projectileLifetime = 2.0f;

        [Header("Camera")]
        public bool autoSetupCameraFollow = true;
        public Vector2 cameraOffset = Vector2.zero;
        public float cameraSmoothTime = 0.08f;

        private EcsWorld _world;
        private readonly List<IEcsSystem> _systems = new List<IEcsSystem>(16);

        private EntityHandle _player;

        private void Awake()
        {
            _world = new EcsWorld();

            // 注册系统：顺序很关键（输入 -> 移动 -> 刷怪/AI -> 发射 -> 子弹 -> 命中 -> 死亡 -> 同步视图）
            _systems.Add(new TouchMoveInputSystem());
            _systems.Add(new MoveSystem());
            _systems.Add(new SpawnSystem(enemyPrefab, enemyMoveSpeed, enemyMaxHealth));
            _systems.Add(new EnemyChaseSystem());
            _systems.Add(new AbilityAutoFireSystem(projectilePrefab, shotCooldown, shotRange, projectileSpeed, projectileDamage, projectileHitRadius, projectileLifetime));
            _systems.Add(new ProjectileMoveLifetimeSystem());
            _systems.Add(new ProjectileHitSystem());
            _systems.Add(new DeathCleanupSystem(this));
            _systems.Add(new ViewSyncSystem());

            Bootstrap();

            if (autoSetupCameraFollow)
                SetupCameraFollow();
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            for (int i = 0; i < _systems.Count; i++)
                _systems[i].Update(_world, dt);
        }

        private void Bootstrap()
        {
            // Player entity
            _player = _world.CreateEntity();
            _world.Add(_player, new PlayerTag());
            _world.Add(_player, new Position2 { Value = Vector2.zero });
            _world.Add(_player, new MoveInput2 { Value = Vector2.zero });
            _world.Add(_player, new MoveSpeed { Value = playerMoveSpeed });
            _world.Add(_player, new Health { Current = playerMaxHealth, Max = playerMaxHealth });

            if (playerPrefab != null)
            {
                var go = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
                _world.Add(_player, new ViewRef { Transform = go.transform });
            }

            // Spawner entity
            var spawner = _world.CreateEntity();
            _world.Add(spawner, new EnemySpawner
            {
                SpawnRadius = spawnRadius,
                BaseSpawnPerSecond = baseSpawnPerSecond,
                SpawnPerSecondPerMinute = spawnPerSecondPerMinute,
                MaxAlive = maxAlive,
                Accumulator = 0f,
                Elapsed = 0f,
                Seed = 1
            });
        }

        /// <summary>
        /// 由系统回调销毁视图 GameObject。
        /// </summary>
        public void DestroyView(Transform t)
        {
            if (t == null) return;
            Destroy(t.gameObject);
        }

        public EntityHandle Player => _player;

        public Vector2 GetPlayerPosition()
        {
            if (_world == null) return Vector2.zero;
            if (!_world.IsAlive(_player)) return Vector2.zero;

            var posPool = _world.GetPool<Position2>();
            return posPool.Has(_player.Id) ? posPool.GetRef(_player.Id).Value : Vector2.zero;
        }

        private void SetupCameraFollow()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var follow = cam.GetComponent<SurvivorCameraFollow>();
            if (follow == null)
                follow = cam.gameObject.AddComponent<SurvivorCameraFollow>();

            follow.runner = this;
            follow.offset = cameraOffset;
            follow.smoothTime = cameraSmoothTime;
        }
    }
}
