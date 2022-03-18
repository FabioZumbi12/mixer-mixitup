﻿using MixItUp.Base.Model;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Currency;
using MixItUp.Base.Model.User;
using MixItUp.Base.Model.User.Platform;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat;
using MixItUp.Base.ViewModel.Chat.Twitch;
using MixItUp.Base.ViewModel.User;
using StreamingClient.Base.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Twitch.Base.Clients;
using Twitch.Base.Models.Clients.Chat;
using Twitch.Base.Models.Clients.PubSub;
using Twitch.Base.Models.Clients.PubSub.Messages;
using Twitch.Base.Models.NewAPI.Users;

namespace MixItUp.Base.Services.Twitch
{
    public class TwitchSubEventModel
    {
        public UserV2ViewModel User { get; set; }

        public string PlanTier { get; set; }

        public int PlanTierNumber { get; set; } = 1;

        public string PlanName { get; set; }

        public string Message { get; set; } = string.Empty;

        public bool IsGiftedUpgrade { get; set; }

        public DateTimeOffset Processed { get; set; } = DateTimeOffset.Now;

        public TwitchSubEventModel(UserV2ViewModel user, PubSubSubscriptionsEventModel packet)
        {
            this.User = user;
            this.PlanTier = TwitchEventService.GetSubTierNameFromText(packet.sub_plan);
            this.PlanTierNumber = TwitchEventService.GetSubTierNumberFromText(packet.sub_plan);
            this.PlanName = !string.IsNullOrEmpty(packet.sub_plan_name) ? packet.sub_plan_name : TwitchEventService.GetSubTierNameFromText(packet.sub_plan);
            if (packet.sub_message.ContainsKey("message"))
            {
                this.Message = packet.sub_message["message"].ToString();
            }
        }

        public TwitchSubEventModel(UserV2ViewModel user, ChatUserNoticePacketModel userNotice)
        {
            this.User = user;
            if (this.User.IsPlatformSubscriber)
            {
                this.PlanTier = this.PlanName = this.User.SubscriberTierString;
            }
            else
            {
                this.PlanTier = this.PlanName = MixItUp.Base.Resources.Tier1;
            }
            this.PlanTierNumber = 1;

            this.IsGiftedUpgrade = true;
        }
    }

    public class TwitchGiftedSubEventModel
    {
        public UserV2ViewModel Gifter { get; set; }

        public UserV2ViewModel Receiver { get; set; }

        public bool IsAnonymous { get; set; }

        public int MonthsGifted { get; set; }

        public int PlanTierNumber { get; set; }

        public string PlanTier { get; set; }

        public string PlanName { get; set; }

        public DateTimeOffset Processed { get; set; } = DateTimeOffset.Now;

        public TwitchGiftedSubEventModel(UserV2ViewModel gifter, UserV2ViewModel receiver, PubSubSubscriptionsGiftEventModel packet)
        {
            this.Gifter = gifter;
            this.Receiver = receiver;
            this.IsAnonymous = packet.IsAnonymousGiftedSubscription;
            this.MonthsGifted = packet.IsMultiMonth ? packet.multi_month_duration : 1;
            this.PlanTierNumber = TwitchEventService.GetSubTierNumberFromText(packet.sub_plan);
            this.PlanTier = TwitchEventService.GetSubTierNameFromText(packet.sub_plan);
            this.PlanName = !string.IsNullOrEmpty(packet.sub_plan_name) ? packet.sub_plan_name : TwitchEventService.GetSubTierNameFromText(packet.sub_plan);
        }
    }

    public class TwitchMassGiftedSubEventModel
    {
        public const string AnonymousGiftedUserNoticeLogin = "ananonymousgifter";

        public static bool IsAnonymousGifter(ChatUserNoticePacketModel userNotice) { return string.Equals(userNotice.Login, TwitchMassGiftedSubEventModel.AnonymousGiftedUserNoticeLogin, StringComparison.InvariantCultureIgnoreCase); }

        public UserV2ViewModel Gifter { get; set; }

        public int TotalGifted { get; set; }

        public int LifetimeGifted { get; set; }

        public string PlanTier { get; set; }

        public int PlanTierNumber { get; set; }

        public bool IsAnonymous { get; set; }

        public List<TwitchGiftedSubEventModel> Subs { get; set; } = new List<TwitchGiftedSubEventModel>();

        public DateTimeOffset Processed { get; set; } = DateTimeOffset.Now;

        public TwitchMassGiftedSubEventModel(ChatUserNoticePacketModel userNotice, UserV2ViewModel gifter)
        {
            this.IsAnonymous = TwitchMassGiftedSubEventModel.IsAnonymousGifter(userNotice);
            this.Gifter = gifter;
            this.TotalGifted = userNotice.SubTotalGifted;
            this.LifetimeGifted = userNotice.SubTotalGiftedLifetime;
            this.PlanTier = TwitchEventService.GetSubTierNameFromText(userNotice.SubPlan);
            this.PlanTierNumber = 1;
        }
    }

    public class TwitchUserBitsCheeredModel
    {
        public UserV2ViewModel User { get; set; }

        public int Amount { get; set; }

        public TwitchChatMessageViewModel Message { get; set; }

        public bool IsAnonymous { get { return this.User.Platform == StreamingPlatformTypeEnum.None; } }

        public TwitchUserBitsCheeredModel(UserV2ViewModel user, PubSubBitsEventV2Model bitsEvent)
        {
            this.User = user;
            this.Amount = bitsEvent.bits_used;
            this.Message = new TwitchChatMessageViewModel(user, bitsEvent);
        }
    }

    public class TwitchWebhookUserFollowModel
    {
        public string ID { get; set; }
        public string Username { get; set; }
        public string DisplayName { get; set; }
    }

    public class TwitchEventService : StreamingPlatformServiceBase
    {
        public const string PrimeSubPlan = "Prime";

        public const int BackgroundGiftedSubProcessorTime = 3000;

        public static int GetSubTierNumberFromText(string subPlan)
        {
            if (!string.IsNullOrEmpty(subPlan) && int.TryParse(subPlan, out int subPlanNumber) && subPlanNumber >= 1000)
            {
                return subPlanNumber / 1000;
            }
            return 1;
        }

        public static string GetSubTierNameFromText(string subPlan)
        {
            if (string.Equals(subPlan, PrimeSubPlan, StringComparison.OrdinalIgnoreCase))
            {
                return PrimeSubPlan;
            }

            int subTier = TwitchEventService.GetSubTierNumberFromText(subPlan);
            if (subTier > 0)
            {
                return $"{MixItUp.Base.Resources.Tier} {subTier}";
            }

            return subPlan;
        }

        private static readonly List<PubSubTopicsEnum> topicTypes = new List<PubSubTopicsEnum>()
        {
            PubSubTopicsEnum.ChannelBitsEventsV2,
            PubSubTopicsEnum.ChannelBitsBadgeUnlocks,
            PubSubTopicsEnum.ChannelSubscriptionsV1,
            PubSubTopicsEnum.UserWhispers,
            PubSubTopicsEnum.ChannelPointsRedeemed
        };

        public HashSet<string> FollowCache { get; private set; } = new HashSet<string>();

        private PubSubClient pubSub;

        private CancellationTokenSource cancellationTokenSource;

        private DateTimeOffset streamStartCheckTime = DateTimeOffset.Now;

        private List<TwitchGiftedSubEventModel> newGiftedSubTracker = new List<TwitchGiftedSubEventModel>();
        private List<TwitchMassGiftedSubEventModel> newMassGiftedSubTracker = new List<TwitchMassGiftedSubEventModel>();
        private Task giftedSubProcessorTask = null;

        public override string Name { get { return MixItUp.Base.Resources.TwitchEvents; } }

        public bool IsConnected { get; private set; }

        public TwitchEventService() { }

        public async Task<Result> Connect()
        {
            this.IsConnected = false;
            if (ServiceManager.Get<TwitchSessionService>().UserConnection != null)
            {
                return await this.AttemptConnect((Func<Task<Result>>)(async () =>
                {
                    try
                    {
                        this.pubSub = new PubSubClient(ServiceManager.Get<TwitchSessionService>().UserConnection.Connection);

                        if (ChannelSession.AppSettings.DiagnosticLogging)
                        {
                            this.pubSub.OnSentOccurred += PubSub_OnSentOccurred;
                            this.pubSub.OnTextReceivedOccurred += PubSub_OnTextReceivedOccurred;
                            this.pubSub.OnMessageReceived += PubSub_OnMessageReceived;
                        }
                        this.pubSub.OnReconnectReceived += PubSub_OnReconnectReceived;
                        this.pubSub.OnDisconnectOccurred += PubSub_OnDisconnectOccurred;
                        this.pubSub.OnPongReceived += PubSub_OnPongReceived;
                        this.pubSub.OnResponseReceived += PubSub_OnResponseReceived;

                        this.pubSub.OnWhisperReceived += PubSub_OnWhisperReceived;
                        this.pubSub.OnBitsV2Received += PubSub_OnBitsV2Received;
                        this.pubSub.OnSubscribedReceived += PubSub_OnSubscribedReceived;
                        this.pubSub.OnSubscriptionsGiftedReceived += PubSub_OnSubscriptionsGiftedReceived;
                        this.pubSub.OnChannelPointsRedeemed += PubSub_OnChannelPointsRedeemed;

                        await this.pubSub.Connect();

                        await Task.Delay(1000);

                        List<PubSubListenTopicModel> topics = new List<PubSubListenTopicModel>();
                        foreach (PubSubTopicsEnum topic in TwitchEventService.topicTypes)
                        {
                            topics.Add(new PubSubListenTopicModel(topic, (string)ServiceManager.Get<TwitchSessionService>().UserID));
                        }

                        await this.pubSub.Listen(topics);

                        await Task.Delay(1000);

                        await this.pubSub.Ping();

                        this.cancellationTokenSource = new CancellationTokenSource();

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        AsyncRunner.RunAsyncBackground(this.BackgroundEventChecks, this.cancellationTokenSource.Token, 60000);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

                        this.IsConnected = true;

                        return new Result();
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex);
                        return new Result(ex);
                    }
                }));
            }
            return new Result(Resources.TwitchConnectionFailed);
        }

        public async Task Disconnect()
        {
            try
            {
                if (this.pubSub != null)
                {
                    if (ChannelSession.AppSettings.DiagnosticLogging)
                    {
                        this.pubSub.OnSentOccurred -= PubSub_OnSentOccurred;
                        this.pubSub.OnTextReceivedOccurred -= PubSub_OnTextReceivedOccurred;
                        this.pubSub.OnMessageReceived -= PubSub_OnMessageReceived;
                    }
                    this.pubSub.OnReconnectReceived -= PubSub_OnReconnectReceived;
                    this.pubSub.OnDisconnectOccurred -= PubSub_OnDisconnectOccurred;
                    this.pubSub.OnPongReceived -= PubSub_OnPongReceived;
                    this.pubSub.OnResponseReceived -= PubSub_OnResponseReceived;

                    this.pubSub.OnWhisperReceived -= PubSub_OnWhisperReceived;
                    this.pubSub.OnBitsV2Received -= PubSub_OnBitsV2Received;
                    this.pubSub.OnSubscribedReceived -= PubSub_OnSubscribedReceived;
                    this.pubSub.OnSubscriptionsGiftedReceived -= PubSub_OnSubscriptionsGiftedReceived;
                    this.pubSub.OnChannelPointsRedeemed -= PubSub_OnChannelPointsRedeemed;

                    await this.pubSub.Disconnect();
                }

                if (this.cancellationTokenSource != null)
                {
                    this.cancellationTokenSource.Cancel();
                    this.cancellationTokenSource = null;
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            this.IsConnected = false;
            this.pubSub = null;
        }

        public async Task AddFollow(UserV2ViewModel user)
        {
            if (!this.FollowCache.Contains(user.PlatformID))
            {
                this.FollowCache.Add(user.PlatformID);

#pragma warning disable CS0612 // Type or member is obsolete
                if (user.HasRole(UserRoleEnum.Banned))
#pragma warning restore CS0612 // Type or member is obsolete
                {
                    return;
                }

                CommandParametersModel parameters = new CommandParametersModel(user);
                if (ServiceManager.Get<EventService>().CanPerformEvent(EventTypeEnum.TwitchChannelFollowed, parameters))
                {
                    user.FollowDate = DateTimeOffset.Now;

                    ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestFollowerUserData] = user.ID;

                    foreach (CurrencyModel currency in ChannelSession.Settings.Currency.Values)
                    {
                        currency.AddAmount(user, currency.OnFollowBonus);
                    }

                    foreach (StreamPassModel streamPass in ChannelSession.Settings.StreamPass.Values)
                    {
                        if (user.MeetsRole(streamPass.UserPermission))
                        {
                            streamPass.AddAmount(user, streamPass.FollowBonus);
                        }
                    }

                    await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelFollowed, parameters);

                    GlobalEvents.FollowOccurred(user);

                    await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertFollow, user.FullDisplayName), ChannelSession.Settings.AlertFollowColor));
                }
            }
        }

        public async Task AddSub(TwitchSubEventModel subEvent)
        {
            CommandParametersModel parameters = new CommandParametersModel(subEvent.User);

            if (subEvent.IsGiftedUpgrade)
            {
                var subscription = await ServiceManager.Get<TwitchSessionService>().UserConnection.GetUserSubscription(ServiceManager.Get<TwitchSessionService>().User, ((TwitchUserPlatformV2Model)subEvent.User.PlatformModel).GetTwitchNewAPIUserModel());
                if (subscription != null)
                {
                    subEvent.PlanTier = TwitchEventService.GetSubTierNameFromText(subscription.tier);
                    subEvent.PlanName = subscription.tier;
                }
            }

            if (ServiceManager.Get<EventService>().CanPerformEvent(EventTypeEnum.TwitchChannelSubscribed, parameters))
            {
                parameters.SpecialIdentifiers["message"] = subEvent.Message;
                parameters.SpecialIdentifiers["usersubplanname"] = subEvent.PlanName;
                parameters.SpecialIdentifiers["usersubplan"] = subEvent.PlanTier;

                ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestSubscriberUserData] = subEvent.User.ID;
                ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestSubscriberSubMonthsData] = 1;

                subEvent.User.Roles.Add(UserRoleEnum.Subscriber);
                subEvent.User.SubscribeDate = DateTimeOffset.Now;
                subEvent.User.SubscriberTier = subEvent.PlanTierNumber;
                subEvent.User.TotalMonthsSubbed++;

                foreach (CurrencyModel currency in ChannelSession.Settings.Currency.Values)
                {
                    currency.AddAmount(subEvent.User, currency.OnSubscribeBonus);
                }

                foreach (StreamPassModel streamPass in ChannelSession.Settings.StreamPass.Values)
                {
                    if (parameters.User.MeetsRole(streamPass.UserPermission))
                    {
                        streamPass.AddAmount(subEvent.User, streamPass.SubscribeBonus);
                    }
                }

                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelSubscribed, parameters);
            }

            GlobalEvents.SubscribeOccurred(subEvent.User);

            if (subEvent.IsGiftedUpgrade)
            {
                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(subEvent.User, string.Format(MixItUp.Base.Resources.AlertContinuedGiftedSubscriptionTier, subEvent.User.FullDisplayName, subEvent.PlanTier), ChannelSession.Settings.AlertSubColor));
            }
            else
            {
                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(subEvent.User, string.Format(MixItUp.Base.Resources.AlertSubscribedTier, subEvent.User.FullDisplayName, subEvent.PlanTier), ChannelSession.Settings.AlertSubColor));
            }
        }

        public async Task AddMassGiftedSub(TwitchMassGiftedSubEventModel massGiftedSubEvent)
        {
            massGiftedSubEvent.Gifter.TotalSubsGifted = (uint)massGiftedSubEvent.LifetimeGifted;

            if (ChannelSession.Settings.MassGiftedSubsFilterAmount > 0)
            {
                if (massGiftedSubEvent.TotalGifted > ChannelSession.Settings.MassGiftedSubsFilterAmount)
                {
                    lock (this.newMassGiftedSubTracker)
                    {
                        this.newMassGiftedSubTracker.Add(massGiftedSubEvent);
                    }
                }
            }
            else
            {
                await ProcessMassGiftedSub(massGiftedSubEvent);
            }
        }

        private async Task BackgroundEventChecks(CancellationToken cancellationToken)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                if (ServiceManager.Get<MixItUpService>().IsWebhookHubConnected && ServiceManager.Get<MixItUpService>().IsWebhookHubAllowed)
                {
                    // We are using the new webhooks
                    return;
                }

                if (streamStartCheckTime != DateTimeOffset.MaxValue)
                {
                    DateTimeOffset startTime = await UptimePreMadeChatCommandModel.GetStartTime();
                    Logger.Log(LogLevel.Debug, "Check for stream start: " + startTime + " - " + streamStartCheckTime);
                    if (startTime > streamStartCheckTime)
                    {
                        Logger.Log(LogLevel.Debug, "Stream start detected");

                        streamStartCheckTime = DateTimeOffset.MaxValue;
                        await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelStreamStart, new CommandParametersModel(StreamingPlatformTypeEnum.Twitch));
                    }
                }

                IEnumerable<UserFollowModel> followers = await ServiceManager.Get<TwitchSessionService>().UserConnection.GetNewAPIFollowers(ServiceManager.Get<TwitchSessionService>().User, maxResult: 100);
                if (this.FollowCache.Count() > 0)
                {
                    foreach (UserFollowModel follow in followers)
                    {
                        UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatformID(StreamingPlatformTypeEnum.Twitch, follow.from_id);
                        if (user == null)
                        {
                            user = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(follow));
                        }

                        await this.AddFollow(user);
                    }
                }
                else
                {
                    foreach (UserFollowModel follow in followers)
                    {
                        this.FollowCache.Add(follow.from_id);
                    }
                }
            }
        }

        private async void PubSub_OnDisconnectOccurred(object sender, System.Net.WebSockets.WebSocketCloseStatus e)
        {
            ChannelSession.DisconnectionOccurred(MixItUp.Base.Resources.TwitchPubSub);

            Result result;
            await this.Disconnect();
            do
            {
                await Task.Delay(2500);

                result = await this.Connect();
            }
            while (!result.Success);

            ChannelSession.ReconnectionOccurred(MixItUp.Base.Resources.TwitchPubSub);
        }

        private void PubSub_OnReconnectReceived(object sender, System.EventArgs e)
        {
            ChannelSession.ReconnectionOccurred(MixItUp.Base.Resources.TwitchPubSub);
        }

        private void PubSub_OnSentOccurred(object sender, string packet)
        {
            Logger.Log(LogLevel.Debug, "PUB SUB SEND: " + packet);
        }

        private void PubSub_OnTextReceivedOccurred(object sender, string text)
        {
            Logger.Log(LogLevel.Debug, "PUB SUB TEXT: " + text);
        }

        private void PubSub_OnMessageReceived(object sender, PubSubMessagePacketModel packet)
        {
            Logger.Log(LogLevel.Debug, string.Format("PUB SUB MESSAGE: {0} {1} ", packet.type, packet.message));

            Logger.Log(LogLevel.Debug, JSONSerializerHelper.SerializeToString(packet));
        }

        private void PubSub_OnResponseReceived(object sender, PubSubResponsePacketModel packet)
        {
            Logger.Log("PUB SUB RESPONSE: " + packet.error);
        }

        private async void PubSub_OnBitsV2Received(object sender, PubSubBitsEventV2Model packet)
        {
            UserV2ViewModel user;
            if (packet.is_anonymous)
            {
                user = UserV2ViewModel.CreateUnassociated();
            }
            else
            {
                user = await ServiceManager.Get<UserService>().GetUserByPlatformID(StreamingPlatformTypeEnum.Twitch, packet.user_id);
                if (user == null)
                {
                    user = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(packet));
                }
            }

            TwitchUserBitsCheeredModel bitsCheered = new TwitchUserBitsCheeredModel(user, packet);

            if (!bitsCheered.IsAnonymous)
            {
                foreach (CurrencyModel bitsCurrency in ChannelSession.Settings.Currency.Values.Where(c => c.SpecialTracking == CurrencySpecialTrackingEnum.Bits))
                {
                    bitsCurrency.AddAmount(user, bitsCheered.Amount);
                }

                foreach (StreamPassModel streamPass in ChannelSession.Settings.StreamPass.Values)
                {
                    if (user.MeetsRole(streamPass.UserPermission))
                    {
                        streamPass.AddAmount(user, (int)Math.Ceiling(streamPass.BitsBonus * bitsCheered.Amount));
                    }
                }

                if (user.HasPlatformData(StreamingPlatformTypeEnum.Twitch))
                {
                    ((TwitchUserPlatformV2Model)user.PlatformModel).TotalBitsCheered = (uint)packet.total_bits_used;
                }

                ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestBitsCheeredUserData] = user.ID;
                ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestBitsCheeredAmountData] = bitsCheered.Amount;
            }

            if (string.IsNullOrEmpty(await ServiceManager.Get<ModerationService>().ShouldTextBeModerated(user, bitsCheered.Message.PlainTextMessage)))
            {
                CommandParametersModel parameters = new CommandParametersModel(user, bitsCheered.Message.ToArguments());
                parameters.SpecialIdentifiers["bitsamount"] = bitsCheered.Amount.ToString();
                parameters.SpecialIdentifiers["bitslifetimeamount"] = packet.total_bits_used.ToString();
                parameters.SpecialIdentifiers["messagenocheermotes"] = bitsCheered.Message.PlainTextMessageNoCheermotes;
                parameters.SpecialIdentifiers["message"] = bitsCheered.Message.PlainTextMessage;
                parameters.SpecialIdentifiers["isanonymous"] = bitsCheered.IsAnonymous.ToString();
                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelBitsCheered, parameters);
            }
            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertTwitchBitsCheered, user.FullDisplayName, bitsCheered.Amount), ChannelSession.Settings.AlertTwitchBitsCheeredColor));
            GlobalEvents.BitsOccurred(bitsCheered);
        }

        private async void PubSub_OnSubscribedReceived(object sender, PubSubSubscriptionsEventModel packet)
        {
            UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatformID(StreamingPlatformTypeEnum.Twitch, packet.user_id);
            if (user == null)
            {
                user = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(packet));
            }

            if (packet.IsSubscription || packet.cumulative_months == 1)
            {
                await this.AddSub(new TwitchSubEventModel(user, packet));
            }
            else
            {
                int months = Math.Max(packet.streak_months, packet.cumulative_months);
                string planTier = TwitchEventService.GetSubTierNameFromText(packet.sub_plan);

                CommandParametersModel parameters = new CommandParametersModel(user);
                if (ServiceManager.Get<EventService>().CanPerformEvent(EventTypeEnum.TwitchChannelResubscribed, parameters))
                {
                    string message = (packet.sub_message.ContainsKey("message") && packet.sub_message["message"] != null) ? packet.sub_message["message"].ToString() : string.Empty;
                    parameters.Arguments = new List<string>(message.Split(new char[] { ' ' }));
                    parameters.SpecialIdentifiers["message"] = message;
                    parameters.SpecialIdentifiers["usersubmonths"] = months.ToString();
                    parameters.SpecialIdentifiers["usersubplanname"] = !string.IsNullOrEmpty(packet.sub_plan_name) ? packet.sub_plan_name : TwitchEventService.GetSubTierNameFromText(packet.sub_plan);
                    parameters.SpecialIdentifiers["usersubplan"] = planTier;
                    parameters.SpecialIdentifiers["usersubstreak"] = packet.streak_months.ToString();

                    ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestSubscriberUserData] = user.ID;
                    ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestSubscriberSubMonthsData] = months;

                    user.Roles.Add(UserRoleEnum.Subscriber);
                    user.SubscribeDate = DateTimeOffset.Now.SubtractMonths(months - 1);
                    user.SubscriberTier = TwitchEventService.GetSubTierNumberFromText(packet.sub_plan);
                    user.TotalMonthsSubbed++;

                    foreach (CurrencyModel currency in ChannelSession.Settings.Currency.Values)
                    {
                        currency.AddAmount(user, currency.OnSubscribeBonus);
                    }

                    foreach (StreamPassModel streamPass in ChannelSession.Settings.StreamPass.Values)
                    {
                        if (parameters.User.MeetsRole(streamPass.UserPermission))
                        {
                            streamPass.AddAmount(user, streamPass.SubscribeBonus);
                        }
                    }

                    if (string.IsNullOrEmpty(await ServiceManager.Get<ModerationService>().ShouldTextBeModerated(user, message)))
                    {
                        await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelResubscribed, parameters);
                    }
                }

                GlobalEvents.ResubscribeOccurred(new Tuple<UserV2ViewModel, int>(user, months));
                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertResubscribedTier, user.FullDisplayName, months, planTier), ChannelSession.Settings.AlertSubColor));
            }
        }

        private async void PubSub_OnSubscriptionsGiftedReceived(object sender, PubSubSubscriptionsGiftEventModel packet)
        {
            UserV2ViewModel gifter = packet.IsAnonymousGiftedSubscription ? UserV2ViewModel.CreateUnassociated() : ServiceManager.Get<UserService>().GetActiveUserByPlatformID(StreamingPlatformTypeEnum.Twitch, packet.user_id);
            if (gifter == null)
            {
                gifter = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(packet));
            }

            UserV2ViewModel receiver = ServiceManager.Get<UserService>().GetActiveUserByPlatformID(StreamingPlatformTypeEnum.Twitch, packet.recipient_id);
            if (receiver == null)
            {
                receiver = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(new UserModel()
                {
                    id = packet.recipient_id,
                    login = packet.recipient_user_name,
                    display_name = packet.recipient_display_name
                }));
            }

            TwitchGiftedSubEventModel giftedSubEvent = new TwitchGiftedSubEventModel(gifter, receiver, packet);
            if (ChannelSession.Settings.MassGiftedSubsFilterAmount > 0)
            {
                lock (this.newGiftedSubTracker)
                {
                    this.newGiftedSubTracker.Add(giftedSubEvent);
                }

                if (giftedSubProcessorTask == null)
                {
                    giftedSubProcessorTask = Task.Run(BackgroundGiftedSubProcessor);
                }
            }
            else
            {
                await ProcessGiftedSub(giftedSubEvent);
            }
        }

        private async Task BackgroundGiftedSubProcessor()
        {
            Dictionary<Guid, List<TwitchGiftedSubEventModel>> giftedSubs = new Dictionary<Guid, List<TwitchGiftedSubEventModel>>();
            List<TwitchMassGiftedSubEventModel> massGiftedSubs = new List<TwitchMassGiftedSubEventModel>();

            List<TwitchGiftedSubEventModel> tempGiftedSubs = new List<TwitchGiftedSubEventModel>();
            List<TwitchMassGiftedSubEventModel> tempMassGiftedSubs = new List<TwitchMassGiftedSubEventModel>();

            do
            {
                await Task.Delay(BackgroundGiftedSubProcessorTime);

                lock (this.newGiftedSubTracker)
                {
                    tempGiftedSubs = this.newGiftedSubTracker.ToList();
                    this.newGiftedSubTracker.Clear();
                }

                lock (this.newMassGiftedSubTracker)
                {
                    tempMassGiftedSubs = this.newMassGiftedSubTracker.ToList();
                    this.newMassGiftedSubTracker.Clear();
                }

                foreach (var group in tempGiftedSubs.GroupBy(s => s.Gifter.ID, s => s))
                {
                    Guid id = group.First().IsAnonymous ? Guid.Empty : group.Key;
                    if (!giftedSubs.ContainsKey(id))
                    {
                        giftedSubs[id] = new List<TwitchGiftedSubEventModel>();
                    }
                    giftedSubs[id].AddRange(group.OrderBy(s => s.Processed));
                }

                massGiftedSubs.AddRange(tempMassGiftedSubs.OrderBy(s => s.Processed));

            } while (tempGiftedSubs.Count > 0 || tempMassGiftedSubs.Count > 0);

            giftedSubProcessorTask = null;

            foreach (TwitchMassGiftedSubEventModel massGiftedSub in massGiftedSubs)
            {
                Guid gifterID = (massGiftedSub.IsAnonymous) ? Guid.Empty : massGiftedSub.Gifter.ID;
                if (giftedSubs.ContainsKey(gifterID))
                {
                    for (int i = 0; i < massGiftedSub.TotalGifted && giftedSubs[gifterID].Count > 0; i++)
                    {
                        TwitchGiftedSubEventModel giftedSub = giftedSubs[gifterID][0];
                        giftedSubs[gifterID].Remove(giftedSub);

                        massGiftedSub.Subs.Add(giftedSub);

                        await ProcessGiftedSub(giftedSub, fireEventCommand: false);
                    }
                }
                await ProcessMassGiftedSub(massGiftedSub);
            }

            foreach (TwitchGiftedSubEventModel giftedSub in giftedSubs.SelectMany(kvp => kvp.Value))
            {
                await ProcessGiftedSub(giftedSub);
            }
        }

        private async Task ProcessGiftedSub(TwitchGiftedSubEventModel giftedSubEvent, bool fireEventCommand = true)
        {
            ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestSubscriberUserData] = giftedSubEvent.Receiver.ID;
            ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestSubscriberSubMonthsData] = giftedSubEvent.MonthsGifted;

            giftedSubEvent.Receiver.Roles.Add(UserRoleEnum.Subscriber);
            giftedSubEvent.Receiver.SubscribeDate = DateTimeOffset.Now;
            giftedSubEvent.Receiver.SubscriberTier = giftedSubEvent.PlanTierNumber;
            giftedSubEvent.Receiver.TotalSubsReceived += (uint)giftedSubEvent.MonthsGifted;
            giftedSubEvent.Receiver.TotalMonthsSubbed += (uint)giftedSubEvent.MonthsGifted;

            foreach (CurrencyModel currency in ChannelSession.Settings.Currency.Values)
            {
                for (int i = 0; i < giftedSubEvent.MonthsGifted; i++)
                {
                    currency.AddAmount(giftedSubEvent.Gifter, currency.OnSubscribeBonus);
                }
            }

            foreach (StreamPassModel streamPass in ChannelSession.Settings.StreamPass.Values)
            {
                if (giftedSubEvent.Gifter.MeetsRole(streamPass.UserPermission))
                {
                    streamPass.AddAmount(giftedSubEvent.Gifter, streamPass.SubscribeBonus);
                }
            }

            if (fireEventCommand)
            {
                CommandParametersModel parameters = new CommandParametersModel(giftedSubEvent.Gifter);
                parameters.SpecialIdentifiers["usersubplanname"] = giftedSubEvent.PlanName;
                parameters.SpecialIdentifiers["usersubplan"] = giftedSubEvent.PlanTier;
                parameters.SpecialIdentifiers["usersubmonthsgifted"] = giftedSubEvent.MonthsGifted.ToString();
                parameters.SpecialIdentifiers["isanonymous"] = giftedSubEvent.IsAnonymous.ToString();
                parameters.Arguments.Add(giftedSubEvent.Receiver.Username);
                parameters.TargetUser = giftedSubEvent.Receiver;
                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelSubscriptionGifted, parameters);

                await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(giftedSubEvent.Gifter, string.Format(MixItUp.Base.Resources.AlertSubscriptionGiftedTier, giftedSubEvent.Gifter.FullDisplayName, giftedSubEvent.PlanTier, giftedSubEvent.Receiver.FullDisplayName), ChannelSession.Settings.AlertGiftedSubColor));
            }

            GlobalEvents.SubscriptionGiftedOccurred(giftedSubEvent.Gifter, giftedSubEvent.Receiver);
        }

        private async Task ProcessMassGiftedSub(TwitchMassGiftedSubEventModel massGiftedSubEvent)
        {
            CommandParametersModel parameters = new CommandParametersModel(massGiftedSubEvent.Gifter);
            parameters.SpecialIdentifiers["subsgiftedamount"] = massGiftedSubEvent.TotalGifted.ToString();
            parameters.SpecialIdentifiers["subsgiftedlifetimeamount"] = massGiftedSubEvent.LifetimeGifted.ToString();
            parameters.SpecialIdentifiers["usersubplan"] = massGiftedSubEvent.PlanTier;
            parameters.SpecialIdentifiers["isanonymous"] = massGiftedSubEvent.IsAnonymous.ToString();

            foreach (TwitchGiftedSubEventModel sub in massGiftedSubEvent.Subs)
            {
                parameters.Arguments.Add(sub.Receiver.Username);
            }

            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelMassSubscriptionsGifted, parameters);

            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(massGiftedSubEvent.Gifter, string.Format(MixItUp.Base.Resources.AlertMassSubscriptionsGiftedTier, massGiftedSubEvent.Gifter.FullDisplayName, massGiftedSubEvent.TotalGifted, massGiftedSubEvent.PlanTier), ChannelSession.Settings.AlertMassGiftedSubColor));
        }

        private async void PubSub_OnChannelPointsRedeemed(object sender, PubSubChannelPointsRedemptionEventModel packet)
        {
            PubSubChannelPointsRedeemedEventModel redemption = packet.redemption;

            UserV2ViewModel user = ServiceManager.Get<UserService>().GetActiveUserByPlatformID(StreamingPlatformTypeEnum.Twitch, redemption.user.id);
            if (user == null)
            {
                user = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(redemption.user));
            }

            List<string> arguments = null;
            Dictionary<string, string> eventCommandSpecialIdentifiers = new Dictionary<string, string>();
            eventCommandSpecialIdentifiers["rewardname"] = redemption.reward.title;
            eventCommandSpecialIdentifiers["rewardcost"] = redemption.reward.cost.ToString();
            if (!string.IsNullOrEmpty(redemption.user_input))
            {
                eventCommandSpecialIdentifiers["message"] = redemption.user_input;
                arguments = new List<string>(redemption.user_input.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            }

            if (string.IsNullOrEmpty(await ServiceManager.Get<ModerationService>().ShouldTextBeModerated(user, redemption.user_input)))
            {
                await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TwitchChannelPointsRedeemed, new CommandParametersModel(user, arguments, eventCommandSpecialIdentifiers));

                TwitchChannelPointsCommandModel command = ServiceManager.Get<CommandService>().TwitchChannelPointsCommands.FirstOrDefault(c => string.Equals(c.ChannelPointRewardID.ToString(), redemption.reward.id, StringComparison.CurrentCultureIgnoreCase));
                if (command == null)
                {
                    command = ServiceManager.Get<CommandService>().TwitchChannelPointsCommands.FirstOrDefault(c => string.Equals(c.Name, redemption.reward.title, StringComparison.CurrentCultureIgnoreCase));
                }

                if (command != null)
                {
                    Dictionary<string, string> channelPointSpecialIdentifiers = new Dictionary<string, string>(eventCommandSpecialIdentifiers);
                    await ServiceManager.Get<CommandService>().Queue(command, new CommandParametersModel(user, platform: StreamingPlatformTypeEnum.Twitch, arguments: arguments, specialIdentifiers: channelPointSpecialIdentifiers));
                }
            }
            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertTwitchChannelPointRedeemed, user.FullDisplayName, redemption.reward.title), ChannelSession.Settings.AlertTwitchChannelPointsColor));
        }

        private async void PubSub_OnWhisperReceived(object sender, PubSubWhisperEventModel packet)
        {
            if (!string.IsNullOrEmpty(packet.body))
            {
                UserV2ViewModel user = ServiceManager.Get<UserService>().GetActiveUserByPlatformID(StreamingPlatformTypeEnum.Twitch, packet.from_id.ToString());
                if (user == null)
                {
                    user = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(packet));
                }

                UserV2ViewModel recipient = ServiceManager.Get<UserService>().GetActiveUserByPlatformID(StreamingPlatformTypeEnum.Twitch, packet.recipient.id.ToString());
                if (recipient == null)
                {
                    recipient = await ServiceManager.Get<UserService>().CreateUser(new TwitchUserPlatformV2Model(packet.recipient));
                }

                await ServiceManager.Get<ChatService>().AddMessage(new TwitchChatMessageViewModel(packet, user, recipient));
            }
        }

        private void PubSub_OnPongReceived(object sender, EventArgs e)
        {
            Logger.Log(LogLevel.Debug, "Twitch Pong Received");
            Task.Run(async () =>
            {
                await Task.Delay(1000 * 60 * 3);
                await this.pubSub.Ping();
            });
        }
    }
}
