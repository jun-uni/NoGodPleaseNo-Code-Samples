// 플랫폼 업적 구현 선택과 조건 해석

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGPN.Gameplay
{
    public class AchievementManager : MonoBehaviour
    {
        [SerializeField] private AbilityAchievementConfig abilityAchievementConfig;

        private IAchievements _implementation;

        private void Awake()
        {
            // 실행 환경에 따른 플랫폼 구현 선택
#if DISABLESTEAMWORKS
            _implementation = new NullAchievements();
#else
            _implementation = SteamManager.Initialized
                ? new SteamAchievements()
                : new NullAchievements();
#endif

            if (abilityAchievementConfig != null)
                abilityAchievementConfig.BuildCache();
        }

        public void Unlock(AchievementKey achievementKey)
        {
            _implementation?.Unlock(achievementKey);
        }

        public void QueryAchievements(Action<IReadOnlyList<AchievementViewData>> onComplete)
        {
            _implementation?.QueryAchievements(onComplete);
        }

        public bool TryResolveAbilityUnlock(
            AbilityId ability,
            AbilityMetric metric,
            float value,
            out AchievementKey key)
        {
            key = default;

            // 게임 지표와 업적 기준의 데이터 기반 매핑
            if (abilityAchievementConfig == null) return false;
            if (!abilityAchievementConfig.TryGet(ability, metric, out AbilityAchievementEntry entry))
                return false;
            if (value < entry.target) return false;

            key = entry.achievementKey;
            return true;
        }
    }
}
