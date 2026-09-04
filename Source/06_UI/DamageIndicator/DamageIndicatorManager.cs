// 피해 표시 풀과 화면 배치 관리

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace NGPN.Gameplay
{
    public class DamageIndicatorManager : MonoBehaviour
    {
        public static DamageIndicatorManager Instance { get; private set; }

        [Header("Prefab and Style")]
        [SerializeField] private DamageIndicator indicatorPrefab;
        [SerializeField] private List<DamageStyle> styles = new();

        [Header("Pooling")]
        [SerializeField] private int prewarmCount = 100;
        [SerializeField] private int maxPoolSize = 300;

        [Header("Placement")]
        [SerializeField] private float pushTowardCamera = 0.35f;
        [SerializeField] private Vector2 planarJitter = new(0.05f, 0.08f);
        [SerializeField] private float riseDistance = 0.6f;
        [SerializeField] private bool yAxisOnlyBillboard = true;
        [SerializeField] private float minScale = 0.7f;
        [SerializeField] private float maxScale = 1.6f;
        [SerializeField] private float minDistance = 3f;
        [SerializeField] private float maxDistance = 30f;
        [SerializeField][Range(0f, 1f)] private float heightBias = 0.55f;
        [SerializeField][Range(0f, 1f)] private float edgeInset = 0.25f;

        private Camera _camera;
        private ObjectPool<DamageIndicator> _pool;
        private readonly List<DamageIndicator> _active = new(256);
        private readonly System.Random _random = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _pool = new ObjectPool<DamageIndicator>(
                () => Instantiate(indicatorPrefab, transform),
                indicator => indicator.gameObject.SetActive(true),
                indicator =>
                {
                    indicator.gameObject.SetActive(false);
                    indicator.ClearAnchor();
                },
                indicator =>
                {
                    if (indicator != null)
                        Destroy(indicator.gameObject);
                },
                false,
                prewarmCount,
                maxPoolSize
            );
        }

        private void Start()
        {
            // 표시 객체 사전 생성
            List<DamageIndicator> prewarmed = new(prewarmCount);
            for (int i = 0; i < prewarmCount; i++)
                prewarmed.Add(_pool.Get());

            foreach (DamageIndicator indicator in prewarmed)
                _pool.Release(indicator);
        }

        public void SetCamera(Camera playerCamera)
        {
            _camera = playerCamera;
        }

        public void Show(in DamageShowArgs args)
        {
            if (_camera == null || args.targetAnchor == null)
                return;

            DamageIndicator indicator = _pool.Get();
            indicator.BindAnchor(args.targetAnchor);
            indicator.style = GetStyle(args.styleType);

            Vector3 right = _camera.transform.right;
            Vector3 up = _camera.transform.up;
            // 표시 중첩 완화를 위한 카메라 기준 오프셋
            indicator.localOffset =
                right * RandomRange(planarJitter.x) +
                up * RandomRange(planarJitter.y);
            indicator.camPush = pushTowardCamera;

            indicator.Init(
                args.amount,
                args.isCrit,
                indicator.style,
                args.lifetime
            );

            _active.Add(indicator);
            PlaceIndicator(indicator, 0f);
        }

        private void LateUpdate()
        {
            if (_active.Count == 0)
                return;

            if (_camera == null)
            {
                for (int i = _active.Count - 1; i >= 0; i--)
                    ReleaseAt(i);
                return;
            }

            float deltaTime = Time.deltaTime;
            // 제거 중 인덱스 보존을 위한 역순 갱신
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                DamageIndicator indicator = _active[i];
                if (indicator == null || indicator.anchor == null)
                {
                    ReleaseAt(i);
                    continue;
                }

                bool finished = indicator.Tick(deltaTime);
                float normalizedTime = indicator.t / Mathf.Max(0.001f, indicator.lifetime);
                PlaceIndicator(indicator, normalizedTime);

                if (finished)
                    ReleaseAt(i);
            }
        }

        private void PlaceIndicator(DamageIndicator indicator, float normalizedTime)
        {
            Vector3 basePosition = GetSmartAnchor(indicator, _camera);
            basePosition += Vector3.up * (riseDistance * normalizedTime);

            Vector3 towardCamera = (_camera.transform.position - basePosition).normalized;
            Vector3 position =
                basePosition + towardCamera * indicator.camPush + indicator.localOffset;
            indicator.transform.position = position;

            if (yAxisOnlyBillboard)
            {
                Vector3 look = _camera.transform.position - position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.0001f)
                    indicator.transform.rotation =
                        Quaternion.LookRotation(-look.normalized, Vector3.up);
            }
            else
            {
                Vector3 look = -(_camera.transform.position - position).normalized;
                indicator.transform.rotation =
                    Quaternion.LookRotation(look, _camera.transform.up);
            }

            float distance = Vector3.Distance(_camera.transform.position, position);
            float distanceRatio = Mathf.InverseLerp(minDistance, maxDistance, distance);
            float distanceScale = Mathf.Lerp(maxScale, minScale, distanceRatio);
            float styleScale = indicator.GetBaseStyleScale();
            float animationScale = indicator.EvaluateAnimScale(normalizedTime);

            indicator.transform.localScale =
                Vector3.one * (styleScale * animationScale * distanceScale);
        }

        private Vector3 GetSmartAnchor(DamageIndicator indicator, Camera playerCamera)
        {
            Bounds bounds = indicator.GetAnchorBounds();
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            Vector3 cameraDirection =
                (playerCamera.transform.position - center).normalized;

            // 대상 Bounds의 카메라 방향 표면점 계산
            Vector3 front = new(
                cameraDirection.x >= 0f ? center.x + extents.x : center.x - extents.x,
                center.y,
                cameraDirection.z >= 0f ? center.z + extents.z : center.z - extents.z
            );

            front = Vector3.Lerp(front, center, Mathf.Clamp01(edgeInset));
            front.y = Mathf.Lerp(
                center.y - extents.y,
                center.y + extents.y,
                Mathf.Clamp01(heightBias)
            );

            return front;
        }

        private DamageStyle GetStyle(DamageStyleType styleType)
        {
            if (styles == null || styles.Count == 0)
                return new DamageStyle();

            int styleIndex = (int)styleType;
            if (styleIndex < 0 || styleIndex >= styles.Count)
                styleIndex = (int)DamageStyleType.Normal;

            return styles[styleIndex];
        }

        private float RandomRange(float range)
        {
            return (float)((_random.NextDouble() * 2.0 - 1.0) * range);
        }

        private void ReleaseAt(int index)
        {
            DamageIndicator indicator = _active[index];
            _active.RemoveAt(index);
            _pool.Release(indicator);
        }
    }
}
