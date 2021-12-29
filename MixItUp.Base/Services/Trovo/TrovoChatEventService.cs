﻿using MixItUp.Base.Model;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Currency;
using MixItUp.Base.Model.User.Platform;
using MixItUp.Base.Util;
using MixItUp.Base.ViewModel.Chat;
using MixItUp.Base.ViewModel.Chat.Trovo;
using MixItUp.Base.ViewModel.User;
using StreamingClient.Base.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Trovo.Base.Clients;
using Trovo.Base.Models.Chat;
using Trovo.Base.Models.Users;

namespace MixItUp.Base.Services.Trovo
{
    public class TrovoChatEventService : StreamingPlatformServiceBase
    {
        private const string RaidMessageRegexFormat = " is carrying \\d+ raiders to this channel.";
        private const string OnlyDigitsRegexReplacementFormat = "[^0-9]";

        private const int MaxMessageLength = 250;

        private Dictionary<string, ChatEmoteModel> channelEmotes = new Dictionary<string, ChatEmoteModel>();
        private Dictionary<string, EventChatEmoteModel> eventEmotes = new Dictionary<string, EventChatEmoteModel>();
        private Dictionary<string, GlobalChatEmoteModel> globalEmotes = new Dictionary<string, GlobalChatEmoteModel>();

        private ChatClient userClient;
        private ChatClient botClient;

        private CancellationTokenSource cancellationTokenSource;

        private bool processMessages = false;
        private SemaphoreSlim messageSemaphore = new SemaphoreSlim(1);

        private HashSet<string> messagesProcessed = new HashSet<string>();
        private Dictionary<Guid, int> userSubsGiftedInstanced = new Dictionary<Guid, int>();

        private HashSet<string> currentViewers = new HashSet<string>();
        private HashSet<string> previousViewers = new HashSet<string>();

        public TrovoChatEventService() { }

        public override string Name { get { return "Trovo Chat"; } }

        public IDictionary<string, ChatEmoteModel> ChannelEmotes { get { return this.channelEmotes; } }
        public IDictionary<string, EventChatEmoteModel> EventEmotes { get { return this.eventEmotes; } }
        public IDictionary<string, GlobalChatEmoteModel> GlobalEmotes { get { return this.globalEmotes; } }

        public bool IsUserConnected { get { return this.userClient != null && this.userClient.IsOpen(); } }
        public bool IsBotConnected { get { return this.botClient != null && this.botClient.IsOpen(); } }

        public async Task<Result> ConnectUser()
        {
            if (ServiceManager.Get<TrovoSessionService>().IsConnected)
            {
                return await this.AttemptConnect((Func<Task<Result>>)(async () =>
                {
                    try
                    {
                        this.cancellationTokenSource = new CancellationTokenSource();

                        this.userClient = new ChatClient(ServiceManager.Get<TrovoSessionService>().UserConnection.Connection);

                        string token = await ServiceManager.Get<TrovoSessionService>().UserConnection.GetChatToken();
                        if (string.IsNullOrEmpty(token))
                        {
                            return new Result("Failed to get chat token from Trovo chat servers");
                        }

                        ChatEmotePackageModel emotePackage = await ServiceManager.Get<TrovoSessionService>().UserConnection.GetPlatformAndChannelEmotes(ServiceManager.Get<TrovoSessionService>().ChannelID);
                        if (emotePackage != null)
                        {
                            if (emotePackage.customizedEmotes?.channel != null)
                            {
                                foreach (ChannelChatEmotesModel channel in emotePackage.customizedEmotes.channel)
                                {
                                    foreach (ChatEmoteModel emote in channel.emotes)
                                    {
                                        this.ChannelEmotes[emote.name] = emote;
                                    }
                                }
                            }

                            if (emotePackage.eventEmotes != null)
                            {
                                foreach (EventChatEmoteModel emote in emotePackage.eventEmotes)
                                {
                                    this.EventEmotes[emote.name] = emote;
                                }
                            }

                            if (emotePackage.globalEmotes != null)
                            {
                                foreach (GlobalChatEmoteModel emote in emotePackage.globalEmotes)
                                {
                                    this.GlobalEmotes[emote.name] = emote;
                                }
                            }
                        }
                        else
                        {
                            Logger.Log(LogLevel.Error, "Failed to get available Trovo emotes");
                        }

                        if (ChannelSession.AppSettings.DiagnosticLogging)
                        {
                            this.userClient.OnSentOccurred += Client_OnSentOccurred;
                            this.userClient.OnTextReceivedOccurred += UserClient_OnTextReceivedOccurred;
                        }
                        this.userClient.OnDisconnectOccurred += UserClient_OnDisconnectOccurred;

                        this.userClient.OnChatMessageReceived += UserClient_OnChatMessageReceived;

                        this.processMessages = false;
                        if (!await this.userClient.Connect(token))
                        {
                            return new Result("Failed to connect to Trovo chat servers");
                        }

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        AsyncRunner.RunAsyncBackground(async (cancellationToken) =>
                        {
                            await Task.Delay(2000);
                            this.processMessages = true;
                        }, this.cancellationTokenSource.Token);

                        AsyncRunner.RunAsyncBackground(this.ChatterJoinLeaveBackground, this.cancellationTokenSource.Token, 60000);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

                        return new Result();
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex);
                        return new Result(ex);
                    }
                }));
            }
            return new Result("Trovo chat connection has not been established");
        }

        public async Task DisconnectUser()
        {
            try
            {
                if (this.userClient != null)
                {
                    if (ChannelSession.AppSettings.DiagnosticLogging)
                    {
                        this.userClient.OnSentOccurred -= Client_OnSentOccurred;
                        this.userClient.OnTextReceivedOccurred -= UserClient_OnTextReceivedOccurred;
                    }
                    this.userClient.OnDisconnectOccurred -= UserClient_OnDisconnectOccurred;

                    this.userClient.OnChatMessageReceived -= UserClient_OnChatMessageReceived;

                    await this.userClient.Disconnect();
                }

                if (this.cancellationTokenSource != null)
                {
                    this.cancellationTokenSource.Cancel();
                    this.cancellationTokenSource = null;
                }

                this.processMessages = false;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            this.userClient = null;
        }

        public async Task<Result> ConnectBot()
        {
            if (ServiceManager.Get<TrovoSessionService>().IsConnected && ServiceManager.Get<TrovoSessionService>().BotConnection != null)
            {
                return await this.AttemptConnect((Func<Task<Result>>)(async () =>
                {
                    try
                    {
                        this.botClient = new ChatClient(ServiceManager.Get<TrovoSessionService>().BotConnection.Connection);

                        string token = await ServiceManager.Get<TrovoSessionService>().BotConnection.GetChatToken(ServiceManager.Get<TrovoSessionService>().ChannelID);
                        if (string.IsNullOrEmpty(token))
                        {
                            return new Result("Failed to get chat token from Trovo chat servers");
                        }

                        if (ChannelSession.AppSettings.DiagnosticLogging)
                        {
                            this.botClient.OnSentOccurred += Client_OnSentOccurred;
                            this.botClient.OnTextReceivedOccurred += BotClient_OnTextReceivedOccurred;
                        }
                        this.botClient.OnDisconnectOccurred += BotClient_OnDisconnectOccurred;

                        if (!await this.botClient.Connect(token))
                        {
                            return new Result("Failed to connect to Trovo chat servers");
                        }

                        return new Result();
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex);
                        return new Result(ex);
                    }
                }));
            }
            return new Result("Trovo chat connection has not been established");
        }

        public async Task DisconnectBot()
        {
            try
            {
                if (this.botClient != null)
                {
                    if (ChannelSession.AppSettings.DiagnosticLogging)
                    {
                        this.botClient.OnSentOccurred -= Client_OnSentOccurred;
                        this.botClient.OnTextReceivedOccurred -= BotClient_OnTextReceivedOccurred;
                    }
                    this.botClient.OnDisconnectOccurred -= BotClient_OnDisconnectOccurred;

                    await this.botClient.Disconnect();
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
            this.botClient = null;
        }

        public async Task SendMessage(string message, bool sendAsStreamer = false)
        {
            await this.messageSemaphore.WaitAndRelease(async () =>
            {
                ChatClient client = this.GetChatClient(sendAsStreamer);
                if (client != null)
                {
                    string subMessage = null;
                    do
                    {
                        message = ChatService.SplitLargeMessage(message, MaxMessageLength, out subMessage);
                        if (client == this.botClient)
                        {
                            await client.SendMessage(ServiceManager.Get<TrovoSessionService>().ChannelID, message);
                        }
                        else
                        {
                            await client.SendMessage(message);
                        }
                        message = subMessage;
                        await Task.Delay(500);
                    }
                    while (!string.IsNullOrEmpty(message));
                }
            });
        }

        public async Task<bool> DeleteMessage(ChatMessageViewModel message)
        {
            return await this.GetChatClient(sendAsStreamer: true).DeleteMessage(ServiceManager.Get<TrovoSessionService>().ChannelID, message.ID, message.User?.PlatformID);
        }

        public async Task<bool> ClearChat() { return await this.PerformChatCommand("clear"); }

        public async Task<bool> ModUser(UserV2ViewModel user) { return await this.PerformChatCommand("mod @" + user.Username); }

        public async Task<bool> UnmodUser(UserV2ViewModel user) { return await this.PerformChatCommand("unmod @" + user.Username); }

        public async Task<bool> TimeoutUser(UserV2ViewModel user, int duration) { return await this.PerformChatCommand($"ban @{user.Username} {duration}"); }

        public async Task<bool> BanUser(UserV2ViewModel user) { return await this.PerformChatCommand("ban @" + user.Username); }

        public async Task<bool> UnbanUser(UserV2ViewModel user) { return await this.PerformChatCommand("unban @" + user.Username); }

        public async Task<bool> PerformChatCommand(string command)
        {
            string result = await this.GetChatClient(sendAsStreamer: true).PerformChatCommand(ServiceManager.Get<TrovoSessionService>().ChannelID, command);
            if (!string.IsNullOrEmpty(result))
            {
                await ServiceManager.Get<ChatService>().SendMessage(result, StreamingPlatformTypeEnum.Trovo);
                return false;
            }
            return true;
        }

        private ChatClient GetChatClient(bool sendAsStreamer = false) { return (this.botClient != null && !sendAsStreamer) ? this.botClient : this.userClient; }

        private void Client_OnSentOccurred(object sender, string packet)
        {
            Logger.Log(LogLevel.Debug, string.Format("Trovo Chat Packet Sent: {0}", packet));
        }

        private void UserClient_OnTextReceivedOccurred(object sender, string packet)
        {
            Logger.Log(LogLevel.Debug, string.Format("Trovo Chat Packet Received: {0}", packet));
        }

        private void BotClient_OnTextReceivedOccurred(object sender, string packet)
        {
            Logger.Log(LogLevel.Debug, string.Format("Trovo Bot Chat Packet Received: {0}", packet));
        }

        private async void UserClient_OnChatMessageReceived(object sender, ChatMessageContainerModel messageContainer)
        {
            if (!this.processMessages)
            {
                return;
            }

            foreach (ChatMessageModel message in messageContainer.chats)
            {
                if (this.messagesProcessed.Contains(message.message_id))
                {
                    continue;
                }
                this.messagesProcessed.Add(message.message_id);

                UserV2ViewModel user = ServiceManager.Get<UserService>().GetActiveUserByPlatformID(StreamingPlatformTypeEnum.Trovo, message.sender_id.ToString());
                if (user == null)
                {
                    UserModel trovoUser = await ServiceManager.Get<TrovoSessionService>().UserConnection.GetUserByName(message.nick_name);
                    if (trovoUser != null)
                    {
                        user = await ServiceManager.Get<UserService>().CreateUser(new TrovoUserPlatformV2Model(trovoUser));
                    }
                    else
                    {
                        user = await ServiceManager.Get<UserService>().CreateUser(new TrovoUserPlatformV2Model(message));
                    }
                    await ServiceManager.Get<UserService>().AddOrUpdateActiveUser(user);
                }

                user.GetPlatformData<TrovoUserPlatformV2Model>(StreamingPlatformTypeEnum.Trovo).SetUserProperties(message);

                if (message.type == ChatMessageTypeEnum.FollowAlert)
                {
                    CommandParametersModel parameters = new CommandParametersModel(user);
                    if (ServiceManager.Get<EventService>().CanPerformEvent(EventTypeEnum.TrovoChannelFollowed, parameters))
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

                        await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TrovoChannelFollowed, parameters);

                        GlobalEvents.FollowOccurred(user);

                        await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format("{0} Followed", user.DisplayName), ChannelSession.Settings.AlertFollowColor));
                    }
                }
                else if (message.type == ChatMessageTypeEnum.SubscriptionAlert)
                {
                    CommandParametersModel parameters = new CommandParametersModel(user);
                    if (ServiceManager.Get<EventService>().CanPerformEvent(EventTypeEnum.TrovoChannelSubscribed, parameters))
                    {
                        parameters.SpecialIdentifiers["message"] = message.content;
                        //trigger.SpecialIdentifiers["usersubmonths"] = months.ToString();
                        //trigger.SpecialIdentifiers["usersubplanname"] = !string.IsNullOrEmpty(packet.sub_plan_name) ? packet.sub_plan_name : TwitchEventService.GetSubTierNameFromText(packet.sub_plan);
                        //trigger.SpecialIdentifiers["usersubplan"] = planTier;
                        //trigger.SpecialIdentifiers["usersubstreak"] = packet.streak_months.ToString();

                        ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestSubscriberUserData] = user.ID;
                        //ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestSubscriberSubMonthsData] = months;

                        user.SubscribeDate = DateTimeOffset.Now;
                        //user.Data.TwitchSubscriberTier = TwitchEventService.GetSubTierNumberFromText(packet.sub_plan);
                        //user.Data.TotalMonthsSubbed++;

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

                        if (string.IsNullOrEmpty(await ServiceManager.Get<ModerationService>().ShouldTextBeModerated(user, message.content)))
                        {
                            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TrovoChannelSubscribed, parameters);
                        }
                    }

                    GlobalEvents.ResubscribeOccurred(new Tuple<UserV2ViewModel, int>(user, 1));
                    await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format("{0} Subscribed", user.DisplayName), ChannelSession.Settings.AlertSubColor));
                }
                else if (message.type == ChatMessageTypeEnum.GiftedSubscriptionSentMessage)
                {
                    int totalGifted = 1;
                    int.TryParse(message.content, out totalGifted);

                    this.userSubsGiftedInstanced[user.ID] = totalGifted;

                    if (ChannelSession.Settings.MassGiftedSubsFilterAmount == 0 || totalGifted > ChannelSession.Settings.MassGiftedSubsFilterAmount)
                    {
                        CommandParametersModel parameters = new CommandParametersModel(user);
                        parameters.SpecialIdentifiers["subsgiftedamount"] = totalGifted.ToString();
                        //trigger.SpecialIdentifiers["subsgiftedlifetimeamount"] = massGiftedSubEvent.LifetimeGifted.ToString();
                        //trigger.SpecialIdentifiers["usersubplan"] = massGiftedSubEvent.PlanTier;
                        //trigger.SpecialIdentifiers["isanonymous"] = massGiftedSubEvent.IsAnonymous.ToString();
                        await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TrovoChannelMassSubscriptionsGifted, parameters);
                    }
                    await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format("{0} Gifted {1} Subs", user.DisplayName, totalGifted), ChannelSession.Settings.AlertMassGiftedSubColor));
                }
                else if (message.type == ChatMessageTypeEnum.GiftedSubscriptionMessage)
                {
                    string[] splits = message.content.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (splits.Length == 2)
                    {
                        string gifteeUsername = splits[1];
                        UserV2ViewModel giftee = ServiceManager.Get<UserService>().GetActiveUserByPlatformUsername(StreamingPlatformTypeEnum.Trovo, gifteeUsername);
                        if (giftee == null)
                        {
                            UserModel gifteeTrovoUser = await ServiceManager.Get<TrovoSessionService>().UserConnection.GetUserByName(gifteeUsername);
                            if (giftee == null)
                            {
                                giftee = user;
                            }
                            else
                            {
                                giftee = await ServiceManager.Get<UserService>().CreateUser(new TrovoUserPlatformV2Model(gifteeTrovoUser));
                            }
                        }

                        ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestSubscriberUserData] = giftee.ID;
                        //ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestSubscriberSubMonthsData] = giftedSubEvent.MonthsGifted;

                        giftee.SubscribeDate = DateTimeOffset.Now;
                        //giftedSubEvent.Receiver.Data.TwitchSubscriberTier = giftedSubEvent.PlanTierNumber;
                        user.TotalSubsGifted++;
                        giftee.TotalSubsReceived++;
                        //giftedSubEvent.Receiver.Data.TotalMonthsSubbed += (uint)giftedSubEvent.MonthsGifted;

                        foreach (CurrencyModel currency in ChannelSession.Settings.Currency.Values)
                        {
                            currency.AddAmount(user, currency.OnSubscribeBonus);
                        }

                        foreach (StreamPassModel streamPass in ChannelSession.Settings.StreamPass.Values)
                        {
                            if (user.MeetsRole(streamPass.UserPermission))
                            {
                                streamPass.AddAmount(user, streamPass.SubscribeBonus);
                            }
                        }

                        this.userSubsGiftedInstanced.TryGetValue(user.ID, out int totalGifted);
                        if (ChannelSession.Settings.MassGiftedSubsFilterAmount == 0 || this.userSubsGiftedInstanced[user.ID] <= ChannelSession.Settings.MassGiftedSubsFilterAmount)
                        {
                            CommandParametersModel parameters = new CommandParametersModel(user);
                            parameters.Arguments.Add(giftee.Username);
                            parameters.TargetUser = giftee;
                            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TrovoChannelSubscriptionGifted, parameters);
                        }

                        await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format("{0} Gifted A Subscription To {1}", user.DisplayName, giftee.DisplayName), ChannelSession.Settings.AlertGiftedSubColor));

                        GlobalEvents.SubscriptionGiftedOccurred(user, giftee);
                    }
                }
                else if (message.type == ChatMessageTypeEnum.WelcomeMessageFromRaid)
                {
                    Match match = Regex.Match(message.content, RaidMessageRegexFormat);
                    if (match.Success)
                    {
                        int raidCount = 0;
                        int.TryParse(Regex.Replace(match.Value, OnlyDigitsRegexReplacementFormat, string.Empty), out raidCount);

                        CommandParametersModel parameters = new CommandParametersModel(user);
                        if (ServiceManager.Get<EventService>().CanPerformEvent(EventTypeEnum.TrovoChannelRaided, parameters))
                        {
                            ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestRaidUserData] = user.ID;
                            ChannelSession.Settings.LatestSpecialIdentifiersData[SpecialIdentifierStringBuilder.LatestRaidViewerCountData] = raidCount.ToString();

                            foreach (CurrencyModel currency in ChannelSession.Settings.Currency.Values.ToList())
                            {
                                currency.AddAmount(user, currency.OnHostBonus);
                            }

                            foreach (StreamPassModel streamPass in ChannelSession.Settings.StreamPass.Values)
                            {
                                if (user.MeetsRole(streamPass.UserPermission))
                                {
                                    streamPass.AddAmount(user, streamPass.HostBonus);
                                }
                            }

                            GlobalEvents.RaidOccurred(user, raidCount);

                            parameters.SpecialIdentifiers["raidviewercount"] = raidCount.ToString();
                            await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TrovoChannelRaided, parameters);

                            await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format("{0} raided with {1} viewers", user.DisplayName, raidCount), ChannelSession.Settings.AlertRaidColor));
                        }
                    }
                }
                else if (message.type == ChatMessageTypeEnum.Spell || message.type == ChatMessageTypeEnum.CustomSpell)
                {
                    TrovoChatSpellViewModel spell = new TrovoChatSpellViewModel(message);
                    CommandParametersModel parameters = new CommandParametersModel(user, spell.GetSpecialIdentifiers());

                    await ServiceManager.Get<EventService>().PerformEvent(EventTypeEnum.TrovoSpellCast, parameters);

                    TrovoSpellCommandModel command = ServiceManager.Get<CommandService>().TrovoSpellCommands.FirstOrDefault(c => string.Equals(c.Name, spell.Name, StringComparison.CurrentCultureIgnoreCase));
                    if (command != null)
                    {
                        await ServiceManager.Get<CommandService>().Queue(command, parameters);
                    }

                    await ServiceManager.Get<AlertsService>().AddAlert(new AlertChatMessageViewModel(user, string.Format(MixItUp.Base.Resources.AlertTrovoSpellFormat, user.DisplayName, spell.Name, spell.ValueTotal, spell.ValueType), ChannelSession.Settings.AlertTrovoSpellCastColor));
                }

                if (TrovoChatMessageViewModel.ApplicableMessageTypes.Contains(message.type) && !string.IsNullOrEmpty(message.content))
                {
                    await ServiceManager.Get<ChatService>().AddMessage(new TrovoChatMessageViewModel(message, user));
                }
            }
        }

        private async Task ChatterJoinLeaveBackground(CancellationToken cancellationToken)
        {
            ChatViewersModel viewers = await ServiceManager.Get<TrovoSessionService>().UserConnection.GetViewers(ServiceManager.Get<TrovoSessionService>().ChannelID);
            if (viewers != null)
            {
                List<UserV2ViewModel> userJoins = new List<UserV2ViewModel>();
                List<UserV2ViewModel> userLeaves = new List<UserV2ViewModel>();

                foreach (string viewer in viewers.all.viewers)
                {
                    currentViewers.Add(viewer);

                    if (!previousViewers.Contains(viewer))
                    {
                        UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatformUsername(StreamingPlatformTypeEnum.Trovo, viewer);
                        if (user != null)
                        {
                            userJoins.Add(user);
                        }
                    }
                }

                await ServiceManager.Get<UserService>().AddOrUpdateActiveUser(userJoins);

                foreach (string viewer in previousViewers)
                {
                    if (!currentViewers.Contains(viewer))
                    {
                        UserV2ViewModel user = await ServiceManager.Get<UserService>().GetUserByPlatformUsername(StreamingPlatformTypeEnum.Trovo, viewer);
                        if (user != null)
                        {
                            userLeaves.Add(user);
                        }
                    }
                }

                await ServiceManager.Get<UserService>().RemoveActiveUsers(userLeaves);

                previousViewers.Clear();
                foreach (string viewer in currentViewers)
                {
                    previousViewers.Add(viewer);
                }
                currentViewers.Clear();
            }
        }

        private async void UserClient_OnDisconnectOccurred(object sender, System.Net.WebSockets.WebSocketCloseStatus e)
        {
            ChannelSession.DisconnectionOccurred("Trovo User Chat");

            Result result;
            await this.DisconnectUser();
            do
            {
                await Task.Delay(2500);

                result = await this.ConnectUser();
            }
            while (!result.Success);

            ChannelSession.ReconnectionOccurred("Trovo User Chat");
        }

        private async void BotClient_OnDisconnectOccurred(object sender, System.Net.WebSockets.WebSocketCloseStatus e)
        {
            ChannelSession.DisconnectionOccurred("Trovo Bot Chat");

            Result result;
            await this.DisconnectBot();
            do
            {
                await Task.Delay(2500);

                result = await this.ConnectBot();
            }
            while (!result.Success);

            ChannelSession.ReconnectionOccurred("Trovo Bot Chat");
        }
    }
}
