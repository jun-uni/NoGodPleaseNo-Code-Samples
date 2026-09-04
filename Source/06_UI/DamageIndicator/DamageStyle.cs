namespace NGPN.Gameplay
{
    using UnityEngine;

    public enum DamageStyleType
    {
        Normal = 0,
        Critical = 1
    }

    public struct DamageShowArgs
    {
        public readonly float amount;
        public readonly bool isCrit;
        public readonly Transform targetAnchor;
        public readonly DamageStyleType styleType;
        public readonly float lifetime;

        public DamageShowArgs(float amount, bool isCrit, Transform targetAnchor,
            DamageStyleType styleType = DamageStyleType.Normal,
            float lifetime = 0.5f)
        {
            this.amount = amount;
            this.isCrit = isCrit;
            this.targetAnchor = targetAnchor;
            this.styleType = styleType;
            this.lifetime = lifetime;
        }
    }

    [System.Serializable]
    public class DamageStyle
    {
        public TMPro.TMP_FontAsset font;
        public Material materialShared;
        public AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 1, 1, 1);
        public Gradient colorOverLife;
        public float baseScale = 1f;
        public float critScaleMul = 1.3f;
    }
}
