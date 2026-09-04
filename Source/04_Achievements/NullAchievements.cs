using System;
using System.Collections.Generic;

namespace NGPN.Gameplay
{
    public sealed class NullAchievements : IAchievements
    {
        public void QueryAchievements(Action<IReadOnlyList<AchievementViewData>> onComplete)
        {
            List<AchievementViewData> achievements = new();

            foreach (AchievementKey key in Enum.GetValues(typeof(AchievementKey)))
            {
                if (key == AchievementKey.Null) continue;

                achievements.Add(new AchievementViewData
                {
                    Key = key,
                    Title = key.ToString(),
                    Description = string.Empty,
                    IsHidden = false,
                    IsUnlocked = false,
                    UnlockTimeUtc = 0,
                    Icon = null
                });
            }

            onComplete?.Invoke(achievements);
        }

        public void Unlock(AchievementKey key)
        {
        }
    }
}
