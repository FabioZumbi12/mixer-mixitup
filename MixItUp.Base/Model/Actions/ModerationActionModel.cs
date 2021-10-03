﻿using MixItUp.Base.Model.Commands;
using MixItUp.Base.Services;
using MixItUp.Base.ViewModel.User;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace MixItUp.Base.Model.Actions
{
    public enum ModerationActionTypeEnum
    {
        TimeoutUser,
        PurgeUser,
        ClearChat,
        BanUser,
        UnbanUser,
        ModUser,
        UnmodUser,
        AddModerationStrike,
        RemoveModerationStrike,
    }

    [DataContract]
    public class ModerationActionModel : ActionModelBase
    {
        [DataMember]
        public ModerationActionTypeEnum ActionType { get; set; }

        [DataMember]
        public string TargetUsername { get; set; }

        [DataMember]
        public string TimeoutAmount { get; set; }

        [DataMember]
        public string ModerationReason { get; set; }

        public ModerationActionModel(ModerationActionTypeEnum actionType, string targetUsername = null, string timeoutAmount = null, string moderationReason = null)
            : base(ActionTypeEnum.Moderation)
        {
            this.ActionType = actionType;
            this.TargetUsername = targetUsername;
            this.TimeoutAmount = timeoutAmount;
            this.ModerationReason = moderationReason;
        }

        private ModerationActionModel() { }

        protected override async Task PerformInternal(CommandParametersModel parameters)
        {
            if (this.ActionType == ModerationActionTypeEnum.ClearChat)
            {
                await ServiceManager.Get<ChatService>().ClearMessages();
            }
            else
            {
                UserV2ViewModel targetUser = null;
                if (!string.IsNullOrEmpty(this.TargetUsername))
                {
                    string username = await ReplaceStringWithSpecialModifiers(this.TargetUsername, parameters);
                    targetUser = ServiceManager.Get<UserService>().GetActiveUserByUsername(username, parameters.Platform);
                }
                else
                {
                    targetUser = parameters.User;
                }

                if (targetUser != null)
                {
                    if (this.ActionType == ModerationActionTypeEnum.PurgeUser)
                    {
                        await ServiceManager.Get<ChatService>().PurgeUser(targetUser);
                    }
                    else if (this.ActionType == ModerationActionTypeEnum.BanUser)
                    {
                        await ServiceManager.Get<ChatService>().BanUser(targetUser);
                    }
                    else if (this.ActionType == ModerationActionTypeEnum.UnbanUser)
                    {
                        await ServiceManager.Get<ChatService>().UnbanUser(targetUser);
                    }
                    else if (this.ActionType == ModerationActionTypeEnum.ModUser)
                    {
                        await ServiceManager.Get<ChatService>().ModUser(targetUser);
                    }
                    else if (this.ActionType == ModerationActionTypeEnum.UnmodUser)
                    {
                        await ServiceManager.Get<ChatService>().UnmodUser(targetUser);
                    }
                    else if (this.ActionType == ModerationActionTypeEnum.AddModerationStrike)
                    {
                        string moderationReason = "Manual Moderation Strike";
                        if (!string.IsNullOrEmpty(this.ModerationReason))
                        {
                            moderationReason = await ReplaceStringWithSpecialModifiers(this.ModerationReason, parameters);
                        }
                        await targetUser.AddModerationStrike(moderationReason);
                    }
                    else if (this.ActionType == ModerationActionTypeEnum.RemoveModerationStrike)
                    {
                        await targetUser.RemoveModerationStrike();
                    }
                    else if (this.ActionType == ModerationActionTypeEnum.TimeoutUser)
                    {
                        if (!string.IsNullOrEmpty(this.TimeoutAmount))
                        {
                            string timeAmountString = await ReplaceStringWithSpecialModifiers(this.TimeoutAmount, parameters);
                            if (uint.TryParse(timeAmountString, out uint timeAmount))
                            {
                                await ServiceManager.Get<ChatService>().TimeoutUser(targetUser, timeAmount);
                            }
                        }
                    }
                }
            }
        }
    }
}
