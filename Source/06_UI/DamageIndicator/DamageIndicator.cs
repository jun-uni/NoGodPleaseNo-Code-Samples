// 개별 피해 표시의 수명과 텍스트 상태 관리

using TMPro;
using UnityEngine;

namespace NGPN.Gameplay
{
    public class DamageIndicator : MonoBehaviour
    {
        [HideInInspector] public Transform anchor;
        [HideInInspector] public float lifetime;
        [HideInInspector] public float t;
        [HideInInspector] public bool isCrit;
        [HideInInspector] public DamageStyle style;
        [HideInInspector] public Vector3 localOffset;
        [HideInInspector] public float camPush;

        [SerializeField] private TextMeshProUGUI tmp;
        private Collider _anchorCollider;
        private Renderer _anchorRenderer;

        public void BindAnchor(Transform targetAnchor)
        {
            anchor = targetAnchor;
            _anchorCollider = anchor != null
                ? anchor.GetComponentInChildren<Collider>()
                : null;
            _anchorRenderer = anchor != null
                ? anchor.GetComponentInChildren<Renderer>()
                : null;
        }

        public Bounds GetAnchorBounds()
        {
            if (_anchorCollider != null)
                return _anchorCollider.bounds;

            if (_anchorRenderer != null)
                return _anchorRenderer.bounds;

            Vector3 fallbackPosition = anchor != null
                ? anchor.position
                : transform.position;
            return new Bounds(fallbackPosition, Vector3.one * 0.1f);
        }

        public void ClearAnchor()
        {
            anchor = null;
            _anchorCollider = null;
            _anchorRenderer = null;
        }

        public void Init(float amount, bool critical, DamageStyle damageStyle, float lifetimeSec)
        {
            isCrit = critical;
            style = damageStyle;
            lifetime = lifetimeSec;
            t = 0f;

            if (tmp == null)
                tmp = GetComponent<TextMeshProUGUI>();

            tmp.SetText(((int)amount).ToString());
            if (style.font != null) tmp.font = style.font;
            if (style.materialShared != null) tmp.fontSharedMaterial = style.materialShared;
        }

        public bool Tick(float deltaTime)
        {
            t += deltaTime;
            float normalizedTime = Mathf.Clamp01(t / Mathf.Max(0.0001f, lifetime));

            if (style.colorOverLife != null)
                tmp.color = style.colorOverLife.Evaluate(normalizedTime);

            return t >= lifetime;
        }

        public float GetBaseStyleScale()
        {
            float scale = style != null ? style.baseScale : 1f;
            if (isCrit && style != null)
                scale *= style.critScaleMul;

            return scale;
        }

        public float EvaluateAnimScale(float normalizedTime)
        {
            return style != null && style.scaleCurve != null
                ? style.scaleCurve.Evaluate(normalizedTime)
                : 1f;
        }
    }
}
