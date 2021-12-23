﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MixItUp.Base.Model
{
    public enum StreamingPlatformTypeEnum
    {
        None = 0,

        [Obsolete]
        Mixer = 1,
        Twitch = 2,
        YouTube = 3,
        Trovo = 4,
        Glimesh = 5,
        [Obsolete]
        Facebook = 6,

        All = 99999,
    }

    public static class StreamingPlatforms
    {
        public const string TwitchLogoImageAssetFilePath = "/Assets/Images/Twitch.png";
        public const string YouTubeLogoImageAssetFilePath = "/Assets/Images/Youtube.png";
        public const string TrovoLogoImageAssetFilePath = "/Assets/Images/Trovo.png";
        public const string GlimeshLogoImageAssetFilePath = "/Assets/Images/Glimesh.png";

        public static IEnumerable<StreamingPlatformTypeEnum> SupportedPlatforms { get; private set; } = new List<StreamingPlatformTypeEnum>()
        {
            StreamingPlatformTypeEnum.Twitch,
            StreamingPlatformTypeEnum.YouTube,
            StreamingPlatformTypeEnum.Trovo,
            StreamingPlatformTypeEnum.Glimesh
        };

        public static IEnumerable<StreamingPlatformTypeEnum> SelectablePlatforms { get; private set; } = new List<StreamingPlatformTypeEnum>()
        {
            StreamingPlatformTypeEnum.All,
            StreamingPlatformTypeEnum.Twitch,
            StreamingPlatformTypeEnum.YouTube,
            StreamingPlatformTypeEnum.Trovo,
            StreamingPlatformTypeEnum.Glimesh
        };

        public static string GetPlatformImage(StreamingPlatformTypeEnum platform)
        {
            if (platform == StreamingPlatformTypeEnum.Twitch) { return TwitchLogoImageAssetFilePath; }
            else if (platform == StreamingPlatformTypeEnum.YouTube) { return YouTubeLogoImageAssetFilePath; }
            else if (platform == StreamingPlatformTypeEnum.Trovo) { return TrovoLogoImageAssetFilePath; }
            else if (platform == StreamingPlatformTypeEnum.Glimesh) { return GlimeshLogoImageAssetFilePath; }
            return string.Empty;
        }

        public static void ForEachPlatform(Action<StreamingPlatformTypeEnum> action)
        {
            foreach (StreamingPlatformTypeEnum platform in StreamingPlatforms.SupportedPlatforms)
            {
                action(platform);
            }
        }

        public static async Task ForEachPlatform(Func<StreamingPlatformTypeEnum, Task> function)
        {
            foreach (StreamingPlatformTypeEnum platform in StreamingPlatforms.SupportedPlatforms)
            {
                await function(platform);
            }
        }
    }
}