﻿using MixItUp.Base.Model.Commands;
using MixItUp.Base.Services;
using MixItUp.Base.Services.External;
using MixItUp.Base.Util;
using StreamingClient.Base.Util;
using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Actions
{
    public enum TwitterActionTypeEnum
    {
        SendTweet,
        UpdateName,
    }

    [DataContract]
    public class TwitterActionModel : ActionModelBase
    {
        public static bool CheckIfTweetContainsTooManyTags(string tweet) { return !string.IsNullOrEmpty(tweet) && tweet.Count(c => c == '@') > 0; }

        public static TwitterActionModel CreateTweetAction(string tweetText, string imagePath = null)
        {
            TwitterActionModel action = new TwitterActionModel(TwitterActionTypeEnum.SendTweet);
            action.TweetText = tweetText;
            action.ImagePath = imagePath;
            return action;
        }

        public static TwitterActionModel CreateUpdateProfileNameAction(string nameUpdate)
        {
            TwitterActionModel action = new TwitterActionModel(TwitterActionTypeEnum.UpdateName);
            action.NameUpdate = nameUpdate;
            return action;
        }

        [DataMember]
        public TwitterActionTypeEnum ActionType { get; set; }

        [DataMember]
        public string TweetText { get; set; }
        [DataMember]
        public string ImagePath { get; set; }

        [DataMember]
        public string NameUpdate { get; set; }

        public TwitterActionModel(TwitterActionTypeEnum actionType)
            : base(ActionTypeEnum.Twitter)
        {
            this.ActionType = actionType;
        }

        [Obsolete]
        public TwitterActionModel() { }

        protected override async Task PerformInternal(CommandParametersModel parameters)
        {
            if (ServiceManager.Get<TwitterService>().IsConnected)
            {
                if (this.ActionType == TwitterActionTypeEnum.SendTweet)
                {
                    string tweet = await ReplaceStringWithSpecialModifiers(this.TweetText, parameters);
                    string imagePath = await ReplaceStringWithSpecialModifiers(this.ImagePath, parameters);

                    if (!string.IsNullOrEmpty(tweet))
                    {
                        if (TwitterActionModel.CheckIfTweetContainsTooManyTags(tweet))
                        {
                            await ServiceManager.Get<ChatService>().SendMessage("The tweet you specified can not be sent because it contains an @mention", parameters.Platform);
                            return;
                        }

                        Result result = await ServiceManager.Get<TwitterService>().SendTweet(tweet, imagePath);
                        if (!result.Success)
                        {
                            await ServiceManager.Get<ChatService>().SendMessage("Twitter Error: " + result.Message, parameters.Platform);
                        }
                    }
                }
                else if (this.ActionType == TwitterActionTypeEnum.UpdateName)
                {
                    Result result = await ServiceManager.Get<TwitterService>().UpdateName(this.NameUpdate);
                    if (!result.Success)
                    {
                        await ServiceManager.Get<ChatService>().SendMessage("Twitter Error: " + result.Message, parameters.Platform);
                    }
                }
            }
        }
    }
}
