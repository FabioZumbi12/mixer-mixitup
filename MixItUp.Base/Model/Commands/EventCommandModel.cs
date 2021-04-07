﻿using MixItUp.Base.Model.User;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using StreamingClient.Base.Util;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Commands
{
    [DataContract]
    public class EventCommandModel : CommandModelBase
    {
        private const string genericImage = "https://static-cdn.jtvnw.net/jtv_user_pictures/12caba55-1276-49b7-a8bc-88b960ecb5da-profile_image-70x70.png";

        private static SemaphoreSlim followEventsInQueueSemaphore = new SemaphoreSlim(1);
        public static int FollowEventsInQueue = 0;

        public static Dictionary<string, string> GetEventTestSpecialIdentifiers(EventTypeEnum eventType)
        {
            Dictionary<string, string> specialIdentifiers = CommandModelBase.GetGeneralTestSpecialIdentifiers();
            switch (eventType)
            {
                case EventTypeEnum.TwitchChannelRaided:
                    specialIdentifiers["hostviewercount"] = "123";
                    specialIdentifiers["raidviewercount"] = "123";
                    break;
                case EventTypeEnum.TwitchChannelSubscribed:
                    specialIdentifiers["message"] = "Test Message";
                    specialIdentifiers["usersubplanname"] = "Plan Name";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    break;
                case EventTypeEnum.TwitchChannelResubscribed:
                    specialIdentifiers["message"] = "Test Message";
                    specialIdentifiers["usersubplanname"] = "Plan Name";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    specialIdentifiers["usersubmonths"] = "5";
                    specialIdentifiers["usersubstreak"] = "3";
                    break;
                case EventTypeEnum.TwitchChannelSubscriptionGifted:
                    specialIdentifiers["usersubplanname"] = "Plan Name";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    specialIdentifiers["usersubmonthsgifted"] = "3";
                    specialIdentifiers["isanonymous"] = "false";
                    break;
                case EventTypeEnum.TwitchChannelMassSubscriptionsGifted:
                    specialIdentifiers["subsgiftedamount"] = "5";
                    specialIdentifiers["subsgiftedlifetimeamount"] = "100";
                    specialIdentifiers["usersubplan"] = "Tier 1";
                    specialIdentifiers["isanonymous"] = "false";
                    break;
                case EventTypeEnum.TwitchChannelBitsCheered:
                    specialIdentifiers["bitsamount"] = "10";
                    specialIdentifiers["Message"] = "Test Message";
                    break;
                case EventTypeEnum.TwitchChannelPointsRedeemed:
                    specialIdentifiers["rewardname"] = "Test Reward";
                    specialIdentifiers["rewardcost"] = "100";
                    specialIdentifiers["message"] = "Test Message";
                    break;
                case EventTypeEnum.ChatUserTimeout:
                    specialIdentifiers["timeoutlength"] = "5m";
                    break;
                case EventTypeEnum.StreamlabsDonation:
                case EventTypeEnum.TiltifyDonation:
                case EventTypeEnum.ExtraLifeDonation:
                case EventTypeEnum.TipeeeStreamDonation:
                case EventTypeEnum.TreatStreamDonation:
                case EventTypeEnum.StreamJarDonation:
                case EventTypeEnum.JustGivingDonation:
                case EventTypeEnum.StreamElementsDonation:
                    UserDonationModel donation = new UserDonationModel()
                    {
                        Amount = 12.34,
                        Message = "Test donation message",
                        ImageLink = genericImage
                    };

                    switch (eventType)
                    {
                        case EventTypeEnum.StreamlabsDonation: donation.Source = UserDonationSourceEnum.Streamlabs; break;
                        case EventTypeEnum.TiltifyDonation: donation.Source = UserDonationSourceEnum.Tiltify; break;
                        case EventTypeEnum.ExtraLifeDonation: donation.Source = UserDonationSourceEnum.ExtraLife; break;
                        case EventTypeEnum.TipeeeStreamDonation: donation.Source = UserDonationSourceEnum.TipeeeStream; break;
                        case EventTypeEnum.TreatStreamDonation: donation.Source = UserDonationSourceEnum.TreatStream; break;
                        case EventTypeEnum.StreamJarDonation: donation.Source = UserDonationSourceEnum.StreamJar; break;
                        case EventTypeEnum.JustGivingDonation: donation.Source = UserDonationSourceEnum.JustGiving; break;
                        case EventTypeEnum.StreamElementsDonation: donation.Source = UserDonationSourceEnum.StreamElements; break;
                    }

                    foreach (var kvp in donation.GetSpecialIdentifiers())
                    {
                        specialIdentifiers[kvp.Key] = kvp.Value;
                    }

                    if (eventType == EventTypeEnum.TreatStreamDonation)
                    {
                        specialIdentifiers["donationtype"] = "Pizza";
                    }
                    break;
                case EventTypeEnum.PatreonSubscribed:
                    specialIdentifiers[SpecialIdentifierStringBuilder.PatreonTierNameSpecialIdentifier] = "Super Tier";
                    specialIdentifiers[SpecialIdentifierStringBuilder.PatreonTierAmountSpecialIdentifier] = "12.34";
                    specialIdentifiers[SpecialIdentifierStringBuilder.PatreonTierImageSpecialIdentifier] = genericImage;
                    break;
                case EventTypeEnum.StreamlootsCardRedeemed:
                    specialIdentifiers["streamlootscardname"] = "Test Card";
                    specialIdentifiers["streamlootscardimage"] = "https://res.cloudinary.com/streamloots/image/upload/f_auto,c_scale,w_250,q_90/static/e19c7bf6-ca3e-49a8-807e-b2e9a1a47524/en_dl_character.png";
                    specialIdentifiers["streamlootscardvideo"] = "https://cdn.streamloots.com/uploads/5c645b78666f31002f2979d1/3a6bf1dc-7d61-4f93-be0a-f5dc1d0d33b6.webm";
                    specialIdentifiers["streamlootscardsound"] = "https://static.streamloots.com/b355d1ef-d931-4c16-a48f-8bed0076401b/alerts/default.mp3";
                    specialIdentifiers["streamlootsmessage"] = "Test Message";
                    break;
                case EventTypeEnum.StreamlootsPackPurchased:
                case EventTypeEnum.StreamlootsPackGifted:
                    specialIdentifiers["streamlootspurchasequantity"] = "1";
                    break;
            }
            return specialIdentifiers;
        }

        [DataMember]
        public EventTypeEnum EventType { get; set; }

        public EventCommandModel(EventTypeEnum eventType) : base(eventType.ToString(), CommandTypeEnum.Event) { this.EventType = eventType; }

#pragma warning disable CS0612 // Type or member is obsolete
        internal EventCommandModel(MixItUp.Base.Commands.EventCommand command)
            : base(command)
        {
            this.Name = command.EventCommandType.ToString();
            this.Type = CommandTypeEnum.Event;
            this.EventType = command.EventCommandType;
        }
#pragma warning restore CS0612 // Type or member is obsolete

        protected EventCommandModel() : base() { }

        public override Dictionary<string, string> GetTestSpecialIdentifiers() { return EventCommandModel.GetEventTestSpecialIdentifiers(this.EventType); }

        public override async Task<bool> CustomValidation(CommandParametersModel parameters)
        {
            if (this.UpdateFollowEventModerationCount())
            {
                bool allowFollowEvent = false;
                await EventCommandModel.followEventsInQueueSemaphore.WaitAndRelease(() =>
                {
                    if (EventCommandModel.FollowEventsInQueue < ChannelSession.Settings.ModerationFollowEventMaxInQueue)
                    {
                        EventCommandModel.FollowEventsInQueue++;
                        allowFollowEvent = true;
                    }
                    return Task.FromResult(0);
                });

                if (!allowFollowEvent)
                {
                    return false;
                }
            }

            return await base.CustomValidation(parameters);
        }

        public override async Task PostRun(CommandParametersModel parameters)
        {
            await base.PostRun(parameters);

            if (this.UpdateFollowEventModerationCount())
            {
                EventCommandModel.FollowEventsInQueue = Math.Max(EventCommandModel.FollowEventsInQueue - 1, 0);
            }
        }

        private bool UpdateFollowEventModerationCount()
        {
            if (ChannelSession.Settings.ModerationFollowEvent)
            {
                if (this.EventType == EventTypeEnum.TwitchChannelFollowed)
                {
                    return true;
                }
            }
            return false;
        }

        public override void TrackTelemetry() { ChannelSession.Services.Telemetry.TrackCommand(this.Type, this.EventType.ToString()); }
    }
}
