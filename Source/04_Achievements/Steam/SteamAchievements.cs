// Steam 사용자 통계 기반 업적 조회와 해금

namespace NGPN.Gameplay
{
#if !DISABLESTEAMWORKS
    using System;
    using System.Collections.Generic;
    using Steamworks;
    using UnityEngine;

    public sealed class SteamAchievements : IAchievements
    {
        private readonly Dictionary<int, Sprite> _iconCache = new();

        private Sprite GetOrCreateSpriteFromSteamImage(int imageHandle)
        {
            if (imageHandle <= 0) return null;
            if (_iconCache.TryGetValue(imageHandle, out Sprite cached) && cached != null)
                return cached;

            if (!SteamUtils.GetImageSize(imageHandle, out uint width, out uint height))
                return null;
            if (width == 0 || height == 0) return null;

            byte[] source = new byte[width * height * 4];
            if (!SteamUtils.GetImageRGBA(imageHandle, source, source.Length))
                return null;

            // Steam 이미지와 Unity 텍스처의 행 방향 변환
            byte[] flipped = new byte[source.Length];
            int rowSize = (int)width * 4;

            for (int row = 0; row < height; row++)
            {
                Buffer.BlockCopy(
                    source,
                    row * rowSize,
                    flipped,
                    ((int)height - 1 - row) * rowSize,
                    rowSize);
            }

            Texture2D texture = new((int)width, (int)height, TextureFormat.RGBA32, false, true)
            {
                name = $"SteamAchievementIcon_{imageHandle}",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            texture.LoadRawTextureData(flipped);
            texture.Apply(false, true);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));

            _iconCache[imageHandle] = sprite;
            return sprite;
        }

        public void QueryAchievements(Action<IReadOnlyList<AchievementViewData>> onComplete)
        {
            List<AchievementViewData> achievements = new();

            foreach (AchievementKey key in Enum.GetValues(typeof(AchievementKey)))
            {
                if (key == AchievementKey.Null) continue;

                string steamId = AchievementIdMapSteam.ToSteamId(key);
                bool unlocked = false;
                uint unlockTime = 0;

                if (!SteamUserStats.GetAchievementAndUnlockTime(steamId, out unlocked, out unlockTime))
                    SteamUserStats.GetAchievement(steamId, out unlocked);

                string hiddenValue = SteamUserStats.GetAchievementDisplayAttribute(steamId, "hidden");
                int iconHandle = SteamUserStats.GetAchievementIcon(steamId);

                achievements.Add(new AchievementViewData
                {
                    Key = key,
                    Title = SteamUserStats.GetAchievementDisplayAttribute(steamId, "name"),
                    Description = SteamUserStats.GetAchievementDisplayAttribute(steamId, "desc"),
                    IsHidden = !string.IsNullOrEmpty(hiddenValue) && hiddenValue != "0",
                    IsUnlocked = unlocked,
                    UnlockTimeUtc = unlockTime,
                    Icon = GetOrCreateSpriteFromSteamImage(iconHandle)
                });
            }

            onComplete?.Invoke(achievements);
        }

        public void Unlock(AchievementKey achievementKey)
        {
            if (achievementKey == AchievementKey.Null) return;

            string steamId = AchievementIdMapSteam.ToSteamId(achievementKey);
            SteamUserStats.SetAchievement(steamId);
            SteamUserStats.StoreStats();
        }
    }
#endif
}
