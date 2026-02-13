using UnityEngine;

namespace Runtime.Core.Entity
{
    /// <summary>
    /// 幸存者相机跟随：跟随玩家位置（可平滑），适配竖屏搓玻璃移动。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SurvivorCameraFollow : MonoBehaviour
    {
        public SurvivorEcsRunner runner;

        [Header("Follow")]
        public Vector2 offset = Vector2.zero;
        public float smoothTime = 0.08f;
        public bool useLateUpdate = true;

        private float _z;
        private Vector3 _vel;

        private void Awake()
        {
            _z = transform.position.z;
        }

        private void Update()
        {
            if (!useLateUpdate)
                Tick(Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (useLateUpdate)
                Tick(Time.deltaTime);
        }

        private void Tick(float dt)
        {
            if (runner == null) return;

            var p = runner.GetPlayerPosition();
            var target = new Vector3(p.x + offset.x, p.y + offset.y, _z);

            if (smoothTime <= 0f)
            {
                transform.position = target;
                return;
            }

            transform.position = Vector3.SmoothDamp(transform.position, target, ref _vel, smoothTime, Mathf.Infinity, dt);
        }
    }
}
