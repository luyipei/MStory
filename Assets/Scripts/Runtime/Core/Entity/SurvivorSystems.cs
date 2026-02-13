using UnityEngine;

namespace Runtime.Core.Entity
{
    // ===== 输入 =====
    public sealed class TouchMoveInputSystem : IEcsSystem
    {
        private bool _dragging;
        private int _touchId;
        private Vector2 _startPos;

        private const float DeadZonePixels = 8f;
        private const float MaxRadiusPixels = 120f;

        public void Update(EcsWorld world, float dt)
        {
            var dir = Vector2.zero;

            // 兼容 Device Simulator：以 touchCount 为准
            if (Input.touchCount > 0)
            {
                if (!_dragging)
                {
                    int startIndex = 0;
                    for (int i = 0; i < Input.touchCount; i++)
                    {
                        var t = Input.GetTouch(i);
                        if (t.phase == TouchPhase.Began)
                        {
                            startIndex = i;
                            break;
                        }
                    }

                    var t0 = Input.GetTouch(startIndex);
                    _touchId = t0.fingerId;
                    _startPos = t0.position;
                    _dragging = true;
                }

                Touch? active = null;
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var t = Input.GetTouch(i);
                    if (t.fingerId == _touchId) { active = t; break; }
                }

                if (active.HasValue)
                {
                    var t = active.Value;
                    if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                    {
                        _dragging = false;
                        _touchId = -1;
                        dir = Vector2.zero;
                    }
                    else
                    {
                        dir = CalcDir(_startPos, t.position);
                    }
                }
                else
                {
                    _dragging = false;
                    _touchId = -1;
                }
            }
            else
            {
                // Editor：鼠标模拟
                if (Input.GetMouseButtonDown(0))
                {
                    _dragging = true;
                    _startPos = Input.mousePosition;
                }
                else if (Input.GetMouseButtonUp(0))
                {
                    _dragging = false;
                }

                if (_dragging && Input.GetMouseButton(0))
                    dir = CalcDir(_startPos, Input.mousePosition);
            }

            var inputPool = world.GetPool<MoveInput2>();
            var playerPool = world.GetPool<PlayerTag>();
            var ents = playerPool.DenseEntities;

            for (int i = 0; i < playerPool.Count; i++)
            {
                var id = ents[i];
                if (!inputPool.Has(id)) continue;
                inputPool.GetRef(id).Value = dir;
            }
        }

        private static Vector2 CalcDir(Vector2 start, Vector2 current)
        {
            var delta = current - start;
            var mag = delta.magnitude;
            if (mag < DeadZonePixels) return Vector2.zero;

            var clamped = Vector2.ClampMagnitude(delta, MaxRadiusPixels);
            var v = clamped / MaxRadiusPixels;
            var len = v.magnitude;
            return len > 1e-5f ? v / Mathf.Max(1f, len) : Vector2.zero;
        }
    }

    // ===== 移动 =====
    public sealed class MoveSystem : IEcsSystem
    {
        public void Update(EcsWorld world, float dt)
        {
            var posPool = world.GetPool<Position2>();
            var inputPool = world.GetPool<MoveInput2>();
            var speedPool = world.GetPool<MoveSpeed>();

            var ents = inputPool.DenseEntities;
            for (int i = 0; i < inputPool.Count; i++)
            {
                var id = ents[i];
                if (!posPool.Has(id) || !speedPool.Has(id)) continue;

                var dir = inputPool.GetRef(id).Value;
                if (dir.sqrMagnitude < 1e-6f) continue;

                posPool.GetRef(id).Value += dir * speedPool.GetRef(id).Value * dt;
            }
        }
    }

    // ===== 刷怪 =====
    public sealed class SpawnSystem : IEcsSystem
    {
        private readonly GameObject _enemyPrefab;
        private readonly float _enemyMoveSpeed;
        private readonly float _enemyMaxHealth;

        public SpawnSystem(GameObject enemyPrefab, float enemyMoveSpeed, float enemyMaxHealth)
        {
            _enemyPrefab = enemyPrefab;
            _enemyMoveSpeed = enemyMoveSpeed;
            _enemyMaxHealth = enemyMaxHealth;
        }

        public void Update(EcsWorld world, float dt)
        {
            var spawnerPool = world.GetPool<EnemySpawner>();
            if (spawnerPool.Count <= 0) return;

            var playerPool = world.GetPool<PlayerTag>();
            var playerPosPool = world.GetPool<Position2>();
            if (playerPool.Count <= 0) return;

            // 单玩家：取第一个
            var playerId = playerPool.DenseEntities[0];
            if (!playerPosPool.Has(playerId)) return;
            var playerPos = playerPosPool.GetRef(playerId).Value;

            var enemyPool = world.GetPool<EnemyTag>();
            var aliveEnemies = enemyPool.Count;

            for (int i = 0; i < spawnerPool.Count; i++)
            {
                ref var sp = ref spawnerPool.GetRef(spawnerPool.DenseEntities[i]);

                if (sp.MaxAlive > 0 && aliveEnemies >= sp.MaxAlive)
                    continue;

                sp.Elapsed += dt;
                var perSecond = sp.BaseSpawnPerSecond + (sp.Elapsed / 60f) * sp.SpawnPerSecondPerMinute;
                perSecond = Mathf.Max(0f, perSecond);

                sp.Accumulator += dt * perSecond;

                while (sp.Accumulator >= 1f)
                {
                    if (sp.MaxAlive > 0 && aliveEnemies >= sp.MaxAlive)
                        break;

                    sp.Accumulator -= 1f;

                    var angle = NextRand(ref sp.Seed) * Mathf.PI * 2f;
                    var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * sp.SpawnRadius;
                    var spawnPos = playerPos + offset;

                    var e = world.CreateEntity();
                    world.Add(e, new EnemyTag());
                    world.Add(e, new Position2 { Value = spawnPos });
                    world.Add(e, new MoveSpeed { Value = _enemyMoveSpeed });
                    world.Add(e, new Health { Current = _enemyMaxHealth, Max = _enemyMaxHealth });

                    if (_enemyPrefab != null)
                    {
                        var go = Object.Instantiate(_enemyPrefab, new Vector3(spawnPos.x, spawnPos.y, 0f), Quaternion.identity);
                        world.Add(e, new ViewRef { Transform = go.transform });
                    }

                    aliveEnemies++;
                }
            }
        }

        private static float NextRand(ref uint seed)
        {
            // xorshift
            seed ^= seed << 13;
            seed ^= seed >> 17;
            seed ^= seed << 5;
            // 0..1
            return (seed & 0x00FFFFFF) / 16777216f;
        }
    }

    // ===== 敌人追击 =====
    public sealed class EnemyChaseSystem : IEcsSystem
    {
        public void Update(EcsWorld world, float dt)
        {
            var playerPool = world.GetPool<PlayerTag>();
            var playerPosPool = world.GetPool<Position2>();
            if (playerPool.Count <= 0) return;

            var playerId = playerPool.DenseEntities[0];
            if (!playerPosPool.Has(playerId)) return;
            var playerPos = playerPosPool.GetRef(playerId).Value;

            var enemyPool = world.GetPool<EnemyTag>();
            var posPool = world.GetPool<Position2>();
            var speedPool = world.GetPool<MoveSpeed>();

            var ents = enemyPool.DenseEntities;
            for (int i = 0; i < enemyPool.Count; i++)
            {
                var id = ents[i];
                if (!posPool.Has(id) || !speedPool.Has(id)) continue;

                var pos = posPool.GetRef(id).Value;
                var to = playerPos - pos;
                var distSq = to.sqrMagnitude;
                if (distSq < 1e-6f) continue;

                var dir = to / Mathf.Sqrt(distSq);
                posPool.GetRef(id).Value = pos + dir * speedPool.GetRef(id).Value * dt;
            }
        }
    }

    // ===== 自动射击（最小能力） =====
    public sealed class AbilityAutoFireSystem : IEcsSystem
    {
        private readonly GameObject _projectilePrefab;
        private readonly float _cooldown;
        private readonly float _range;
        private readonly float _projSpeed;
        private readonly float _projDamage;
        private readonly float _hitRadius;
        private readonly float _lifetime;

        private float _cd;

        public AbilityAutoFireSystem(GameObject projectilePrefab, float cooldown, float range, float projSpeed, float projDamage, float hitRadius, float lifetime)
        {
            _projectilePrefab = projectilePrefab;
            _cooldown = Mathf.Max(0.01f, cooldown);
            _range = Mathf.Max(0.1f, range);
            _projSpeed = projSpeed;
            _projDamage = projDamage;
            _hitRadius = hitRadius;
            _lifetime = lifetime;
        }

        public void Update(EcsWorld world, float dt)
        {
            _cd -= dt;
            if (_cd > 0f) return;

            var playerPool = world.GetPool<PlayerTag>();
            var playerPosPool = world.GetPool<Position2>();
            if (playerPool.Count <= 0) return;
            var playerId = playerPool.DenseEntities[0];
            if (!playerPosPool.Has(playerId)) return;
            var playerPos = playerPosPool.GetRef(playerId).Value;

            var enemyPool = world.GetPool<EnemyTag>();
            var enemyPosPool = world.GetPool<Position2>();
            if (enemyPool.Count <= 0) return;

            var rangeSq = _range * _range;
            var bestDist = float.MaxValue;
            var bestPos = Vector2.zero;
            var found = false;

            var ents = enemyPool.DenseEntities;
            for (int i = 0; i < enemyPool.Count; i++)
            {
                var id = ents[i];
                if (!enemyPosPool.Has(id)) continue;
                var pos = enemyPosPool.GetRef(id).Value;
                var distSq = (pos - playerPos).sqrMagnitude;
                if (distSq > rangeSq) continue;
                if (distSq < bestDist)
                {
                    bestDist = distSq;
                    bestPos = pos;
                    found = true;
                }
            }

            if (!found) return;

            var dir = (bestPos - playerPos);
            var len = dir.magnitude;
            if (len < 1e-6f) return;
            dir /= len;

            var p = world.CreateEntity();
            world.Add(p, new ProjectileTag());
            world.Add(p, new Position2 { Value = playerPos });
            world.Add(p, new Velocity2 { Value = dir * _projSpeed });
            world.Add(p, new Lifetime { Remaining = _lifetime });
            world.Add(p, new Projectile { Damage = _projDamage, HitRadius = _hitRadius, OwnerEntityId = playerId });

            if (_projectilePrefab != null)
            {
                var go = Object.Instantiate(_projectilePrefab, new Vector3(playerPos.x, playerPos.y, 0f), Quaternion.identity);
                world.Add(p, new ViewRef { Transform = go.transform });
            }

            _cd = _cooldown;
        }
    }

    // ===== 子弹移动与寿命 =====
    public sealed class ProjectileMoveLifetimeSystem : IEcsSystem
    {
        public void Update(EcsWorld world, float dt)
        {
            var projTagPool = world.GetPool<ProjectileTag>();
            var posPool = world.GetPool<Position2>();
            var velPool = world.GetPool<Velocity2>();
            var lifePool = world.GetPool<Lifetime>();

            var ents = projTagPool.DenseEntities;
            for (int i = 0; i < projTagPool.Count; i++)
            {
                var id = ents[i];
                if (!posPool.Has(id) || !velPool.Has(id) || !lifePool.Has(id)) continue;

                posPool.GetRef(id).Value += velPool.GetRef(id).Value * dt;
                lifePool.GetRef(id).Remaining -= dt;
            }
        }
    }

    // ===== 子弹命中（距离判定） =====
    public sealed class ProjectileHitSystem : IEcsSystem
    {
        public void Update(EcsWorld world, float dt)
        {
            var projPool = world.GetPool<Projectile>();
            var projTagPool = world.GetPool<ProjectileTag>();
            var projPosPool = world.GetPool<Position2>();

            var enemyPool = world.GetPool<EnemyTag>();
            var enemyPosPool = world.GetPool<Position2>();
            var healthPool = world.GetPool<Health>();

            // 遍历子弹
            var pEnts = projTagPool.DenseEntities;
            for (int pi = 0; pi < projTagPool.Count; pi++)
            {
                var pid = pEnts[pi];
                if (!projPool.Has(pid) || !projPosPool.Has(pid)) continue;

                var p = projPool.GetRef(pid);
                var pPos = projPosPool.GetRef(pid).Value;
                var rSq = p.HitRadius * p.HitRadius;

                // 找任意一个命中敌人
                var eEnts = enemyPool.DenseEntities;
                for (int ei = 0; ei < enemyPool.Count; ei++)
                {
                    var eid = eEnts[ei];
                    if (!enemyPosPool.Has(eid) || !healthPool.Has(eid)) continue;

                    var ePos = enemyPosPool.GetRef(eid).Value;
                    if ((ePos - pPos).sqrMagnitude > rSq) continue;

                    // 扣血
                    healthPool.GetRef(eid).Current -= p.Damage;

                    // 标记子弹过期：让 DeathCleanupSystem 统一销毁
                    if (world.GetPool<Lifetime>().Has(pid))
                        world.GetPool<Lifetime>().GetRef(pid).Remaining = -1f;

                    break;
                }
            }
        }
    }

    // ===== 死亡与回收 =====
    public sealed class DeathCleanupSystem : IEcsSystem
    {
        private readonly SurvivorEcsRunner _runner;

        public DeathCleanupSystem(SurvivorEcsRunner runner)
        {
            _runner = runner;
        }

        public void Update(EcsWorld world, float dt)
        {
            // 1) Lifetime <= 0 的投射物
            var lifePool = world.GetPool<Lifetime>();
            var viewPool = world.GetPool<ViewRef>();

            // 注意：遍历 dense 时可能 Remove 导致 swap，所以用 while
            int i = 0;
            while (i < lifePool.Count)
            {
                var id = lifePool.DenseEntities[i];
                ref var life = ref lifePool.GetRef(id);
                if (life.Remaining <= 0f)
                {
                    if (viewPool.Has(id)) _runner.DestroyView(viewPool.GetRef(id).Transform);
                    world.DestroyEntity(id);
                    continue;
                }
                i++;
            }

            // 2) Health <= 0 的玩家/敌人
            var healthPool = world.GetPool<Health>();
            i = 0;
            while (i < healthPool.Count)
            {
                var id = healthPool.DenseEntities[i];
                ref var hp = ref healthPool.GetRef(id);
                if (hp.Current <= 0f)
                {
                    if (viewPool.Has(id)) _runner.DestroyView(viewPool.GetRef(id).Transform);
                    world.DestroyEntity(id);
                    continue;
                }
                i++;
            }
        }
    }

    // ===== 视图同步 =====
    public sealed class ViewSyncSystem : IEcsSystem
    {
        public void Update(EcsWorld world, float dt)
        {
            var viewPool = world.GetPool<ViewRef>();
            var posPool = world.GetPool<Position2>();

            var ents = viewPool.DenseEntities;
            for (int i = 0; i < viewPool.Count; i++)
            {
                var id = ents[i];
                if (!posPool.Has(id)) continue;
                var t = viewPool.GetRef(id).Transform;
                if (t == null) continue;
                var p = posPool.GetRef(id).Value;
                t.position = new Vector3(p.x, p.y, 0f);
            }
        }
    }
}
