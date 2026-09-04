// 플랫폼별 업적 정보를 UI 공통 형식으로 전달하는 데이터

using System;
using UnityEngine;

namespace NGPN.Gameplay
{
    [Serializable]
    public struct AchievementViewData
    {
        public AchievementKey Key { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public bool IsHidden { get; set; }
        public bool IsUnlocked { get; set; }
        public uint UnlockTimeUtc { get; set; }
        public Sprite Icon { get; set; }
    }
}
