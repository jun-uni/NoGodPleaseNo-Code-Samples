// 업적 조회와 해금을 위한 플랫폼 경계

using System;
using System.Collections.Generic;

namespace NGPN.Gameplay
{
    public interface IAchievements
    {
        void QueryAchievements(Action<IReadOnlyList<AchievementViewData>> onComplete);
        void Unlock(AchievementKey key);
    }
}
