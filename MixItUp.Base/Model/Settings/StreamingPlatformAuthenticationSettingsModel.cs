﻿using MixItUp.Base.Services;
using MixItUp.Base.Services.Glimesh;
using MixItUp.Base.Services.Trovo;
using MixItUp.Base.Services.Twitch;
using MixItUp.Base.Services.YouTube;
using Newtonsoft.Json;
using StreamingClient.Base.Model.OAuth;
using System;
using System.Runtime.Serialization;

namespace MixItUp.Base.Model.Settings
{
    [DataContract]
    public class StreamingPlatformAuthenticationSettingsModel : IEquatable<StreamingPlatformAuthenticationSettingsModel>
    {
        [DataMember]
        public StreamingPlatformTypeEnum Type { get; set; }

        [DataMember]
        public string UserID { get; set; }
        [DataMember]
        public OAuthTokenModel UserOAuthToken { get; set; }

        [DataMember]
        public string BotID { get; set; }
        [DataMember]
        public OAuthTokenModel BotOAuthToken { get; set; }

        [DataMember]
        public string ChannelID { get; set; }

        private StreamingPlatformAuthenticationSettingsModel() { }

        public StreamingPlatformAuthenticationSettingsModel(StreamingPlatformTypeEnum type) { this.Type = type; }

        [JsonIgnore]
        public bool IsEnabled { get { return this.UserOAuthToken != null; } }

        public override bool Equals(object obj)
        {
            if (obj is StreamingPlatformAuthenticationSettingsModel)
            {
                return this.Equals((StreamingPlatformAuthenticationSettingsModel)obj);
            }
            return false;
        }

        public bool Equals(StreamingPlatformAuthenticationSettingsModel other) { return this.Type == other.Type && this.UserID == other.UserID; }

        public override int GetHashCode() { return this.Type.GetHashCode(); }
    }
}
