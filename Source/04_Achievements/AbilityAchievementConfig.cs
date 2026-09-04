using System;
using System.Collections.Generic;
using UnityEngine;

namespace NGPN.Gameplay
{
    public enum AbilityId
    {
        BarbarianSkill,
        TankerSkill,
        LancerSkill,
        RangerUltimate,
        PirateUltimate,
        VoodooUltimate
    }

    public enum AbilityMetric
    {
        KillCountThisCast,
        PushCountThisCast,
        DurationSecondsThisCast,
        PolymorphCountThisCast
    }

    [Serializable]
    public class AbilityAchievementEntry
    {
        public AbilityId ability;
        public AbilityMetric metric;

        [Tooltip("달성 시 언락할 업적 키")] public AchievementKey achievementKey;

        [Tooltip("목표치")] public float target;
    }

    [CreateAssetMenu(menuName = "Achievements/Ability Achievement Config", fileName = "AbilityAchievementConfig")]
    public class AbilityAchievementConfig : ScriptableObject
    {
        public List<AbilityAchievementEntry> entries = new();

        private Dictionary<(AbilityId, AbilityMetric), AbilityAchievementEntry> _map;

        public void BuildCache()
        {
            // Inspector 목록을 런타임 조회용 튜플 키로 변환
            _map = new Dictionary<(AbilityId, AbilityMetric), AbilityAchievementEntry>();
            foreach (AbilityAchievementEntry e in entries)
            {
                if (e == null) continue;
                _map[(e.ability, e.metric)] = e;
            }
        }

        public bool TryGet(AbilityId ability, AbilityMetric metric, out AbilityAchievementEntry entry)
        {
            // 직접 호출 시에도 캐시 초기화 보장
            _map ??= new Dictionary<(AbilityId, AbilityMetric), AbilityAchievementEntry>();
            if (_map.Count == 0) BuildCache();
            return _map.TryGetValue((ability, metric), out entry);
        }
    }
}
