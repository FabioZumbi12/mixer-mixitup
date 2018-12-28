﻿using Mixer.Base.Util;
using MixItUp.Base;
using MixItUp.Base.Actions;
using MixItUp.Base.ViewModel.User;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using System.Linq;
using MixItUp.Base.ViewModel.Requirement;

namespace MixItUp.WPF.Controls.Actions
{
    /// <summary>
    /// Interaction logic for CurrencyActionControl.xaml
    /// </summary>
    public partial class CurrencyActionControl : ActionControlBase
    {
        private CurrencyAction action;

        public CurrencyActionControl(ActionContainerControl containerControl) : base(containerControl) { InitializeComponent(); }

        public CurrencyActionControl(ActionContainerControl containerControl, CurrencyAction action) : this(containerControl) { this.action = action; }

        public override Task OnLoaded()
        {
            this.CurrencyTypeComboBox.ItemsSource = ChannelSession.Settings.Currencies.Values;
            this.CurrencyActionTypeComboBox.ItemsSource = EnumHelper.GetEnumNames<CurrencyActionTypeEnum>().OrderBy(s => s);
            this.CurrencyPermissionsAllowedComboBox.ItemsSource = RoleRequirementViewModel.BasicUserRoleAllowedValues;

            this.CurrencyPermissionsAllowedComboBox.SelectedIndex = 0;

            if (this.action != null)
            {
                if (ChannelSession.Settings.Currencies.ContainsKey(this.action.CurrencyID))
                {
                    this.CurrencyTypeComboBox.SelectedItem = ChannelSession.Settings.Currencies[this.action.CurrencyID];
                }

                this.CurrencyActionTypeComboBox.SelectedItem = EnumHelper.GetEnumName(this.action.CurrencyActionType);
                this.CurrencyAmountTextBox.Text = this.action.Amount.ToString();
                this.CurrencyUsernameTextBox.Text = this.action.Username;
                this.CurrencyPermissionsAllowedComboBox.SelectedItem = EnumHelper.GetEnumName(this.action.RoleRequirement);
                this.DeductFromUserToggleButton.IsChecked = this.action.DeductFromUser;
            }
            return Task.FromResult(0);
        }

        public override ActionBase GetAction()
        {
            if (this.CurrencyTypeComboBox.SelectedIndex >= 0 && this.CurrencyActionTypeComboBox.SelectedIndex >= 0)
            {
                UserCurrencyViewModel currency = (UserCurrencyViewModel)this.CurrencyTypeComboBox.SelectedItem;
                CurrencyActionTypeEnum actionType = EnumHelper.GetEnumValueFromString<CurrencyActionTypeEnum>((string)this.CurrencyActionTypeComboBox.SelectedItem);

                if (actionType == CurrencyActionTypeEnum.Reset || !string.IsNullOrEmpty(this.CurrencyAmountTextBox.Text))
                {
                    if (actionType == CurrencyActionTypeEnum.AddToSpecificUser)
                    {
                        if (string.IsNullOrEmpty(this.CurrencyUsernameTextBox.Text))
                        {
                            return null;
                        }
                    }

                    MixerRoleEnum roleRequirement = MixerRoleEnum.User;
                    if (actionType == CurrencyActionTypeEnum.AddToAllChatUsers || actionType == CurrencyActionTypeEnum.SubtractFromAllChatUsers)
                    {
                        if (this.CurrencyPermissionsAllowedComboBox.SelectedIndex < 0)
                        {
                            return null;
                        }
                        roleRequirement = EnumHelper.GetEnumValueFromString<MixerRoleEnum>((string)this.CurrencyPermissionsAllowedComboBox.SelectedItem);
                    }

                    return new CurrencyAction(currency, actionType, this.CurrencyAmountTextBox.Text, this.CurrencyUsernameTextBox.Text, roleRequirement,
                        this.DeductFromUserToggleButton.IsChecked.GetValueOrDefault());
                }
            }
            return null;
        }

        private void CurrencyTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.CurrencyTypeComboBox.SelectedIndex >= 0)
            {
                this.CurrencyActionTypeComboBox.IsEnabled = this.CurrencyUsernameTextBox.IsEnabled = this.CurrencyAmountTextBox.IsEnabled =
                    this.DeductFromUserTextBlock.IsEnabled = this.DeductFromUserToggleButton.IsEnabled = true;
            }
        }

        private void CurrencyActionTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.CurrencyActionTypeComboBox.SelectedIndex >= 0)
            {
                CurrencyActionTypeEnum actionType = EnumHelper.GetEnumValueFromString<CurrencyActionTypeEnum>((string)this.CurrencyActionTypeComboBox.SelectedItem);
                this.GiveToGrid.Visibility = (actionType == CurrencyActionTypeEnum.AddToSpecificUser || actionType == CurrencyActionTypeEnum.AddToAllChatUsers ||
                    actionType == CurrencyActionTypeEnum.SubtractFromSpecificUser || actionType == CurrencyActionTypeEnum.SubtractFromAllChatUsers) ?
                    Visibility.Visible : Visibility.Collapsed;

                this.CurrencyUsernameTextBox.Visibility = (actionType == CurrencyActionTypeEnum.AddToSpecificUser || actionType == CurrencyActionTypeEnum.SubtractFromSpecificUser) ?
                    Visibility.Visible : Visibility.Collapsed;

                this.CurrencyPermissionsAllowedComboBox.Visibility = (actionType == CurrencyActionTypeEnum.AddToAllChatUsers || actionType == CurrencyActionTypeEnum.SubtractFromAllChatUsers) ?
                    Visibility.Visible : Visibility.Collapsed;

                this.DeductFromUserTextBlock.IsEnabled = this.DeductFromUserToggleButton.IsEnabled =
                    (actionType == CurrencyActionTypeEnum.AddToSpecificUser || actionType == CurrencyActionTypeEnum.AddToAllChatUsers) ? true : false;

                this.CurrencyAmountTextBox.IsEnabled = (actionType != CurrencyActionTypeEnum.Reset);
            }
        }
    }
}
