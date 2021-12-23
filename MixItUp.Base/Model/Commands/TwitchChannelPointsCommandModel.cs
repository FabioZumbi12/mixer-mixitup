﻿using System;
using System.Runtime.Serialization;

namespace MixItUp.Base.Model.Commands
{
    [DataContract]
    public class TwitchChannelPointsCommandModel : CommandModelBase
    {
        public Guid ChannelPointRewardID { get; set; } = Guid.Empty;

        public TwitchChannelPointsCommandModel(string name, Guid channelPointRewardID)
            : base(name, CommandTypeEnum.TwitchChannelPoints)
        {
            this.ChannelPointRewardID = channelPointRewardID;
        }

        protected TwitchChannelPointsCommandModel() : base() { }
    }
}
