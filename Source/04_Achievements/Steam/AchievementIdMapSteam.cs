using System;
using System.Collections.Generic;

namespace NGPN.Gameplay
{
    public static class AchievementIdMapSteam
    {
        private static readonly Dictionary<AchievementKey, string> Map = new()
        {
            { AchievementKey.NohitWave, "ACH_NOHIT_WAVE" },
            { AchievementKey.UpgradeAll, "ACH_UPGRADE_ALL" },
            { AchievementKey.Victory, "ACH_REACH_VICTORY" },
            { AchievementKey.VictorySolo, "ACH_SOLO_CLEAR" },
            { AchievementKey.ClimbBuilding, "ACH_CLIMB_HIGH_BUILDING" },
            { AchievementKey.TeamBarbarian, "ACH_SINGLE_TEAM_BARBARIAN" },
            { AchievementKey.TeamTanker, "ACH_SINGLE_TEAM_TANKER" },
            { AchievementKey.TeamLancer, "ACH_SINGLE_TEAM_LANCER" },
            { AchievementKey.TeamRanger, "ACH_SINGLE_TEAM_RANGER" },
            { AchievementKey.TeamPirate, "ACH_SINGLE_TEAM_PIRATE" },
            { AchievementKey.TeamVoodoo, "ACH_SINGLE_TEAM_VOODOO" },
            { AchievementKey.GimmickBarbarian, "ACH_GIMMICK_BARBARIAN" },
            { AchievementKey.GimmickTanker, "ACH_GIMMICK_TANKER" },
            { AchievementKey.GimmickLancer, "ACH_GIMMICK_LANCER" },
            { AchievementKey.GimmickRanger, "ACH_GIMMICK_RANGER" },
            { AchievementKey.GimmickPirate, "ACH_GIMMICK_PIRATE" },
            { AchievementKey.GimmickVoodoo, "ACH_GIMMICK_VOODOO" },
            { AchievementKey.VictoryRandom, "ACH_RANDOM_CLEAR" }
        };


        public static string ToSteamId(AchievementKey key)
        {
            if (key == AchievementKey.Null)
                throw new ArgumentException("AchievementKey.Null is not a valid achievement key.");

            // 등록 누락을 조용히 넘기지 않고 즉시 노출
            if (!Map.TryGetValue(key, out string id) || string.IsNullOrWhiteSpace(id))
                throw new KeyNotFoundException($"No Steam achievement id mapping for key: {key}");

            return id;
        }
    }
}
