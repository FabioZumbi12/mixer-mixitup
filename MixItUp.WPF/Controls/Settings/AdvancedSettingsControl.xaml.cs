﻿using MixItUp.Base;
using MixItUp.Base.Model.Settings;
using MixItUp.Base.Services;
using MixItUp.Base.Util;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace MixItUp.WPF.Controls.Settings
{
    /// <summary>
    /// Interaction logic for AdvancedSettingsControl.xaml
    /// </summary>
    public partial class AdvancedSettingsControl : SettingsControlBase
    {
        public AdvancedSettingsControl()
        {
            InitializeComponent();
        }

        protected override async Task InitializeInternal()
        {
            this.SettingsBackupRateComboBox.ItemsSource = Enum.GetValues(typeof(SettingsBackupRateEnum));

            this.SettingsBackupRateComboBox.SelectedItem = ChannelSession.Settings.SettingsBackupRate;
            if (!string.IsNullOrEmpty(ChannelSession.Settings.SettingsBackupLocation))
            {
                this.SettingsBackupRateComboBox.IsEnabled = true;
            }

            this.DisableDiagnosticLogsButton.Visibility = (ChannelSession.AppSettings.DiagnosticLogging) ? Visibility.Visible : Visibility.Collapsed;
            this.EnableDiagnosticLogsButton.Visibility = (ChannelSession.AppSettings.DiagnosticLogging) ? Visibility.Collapsed : Visibility.Visible;

            await base.InitializeInternal();
        }

        protected override async Task OnVisibilityChanged()
        {
            await this.InitializeInternal();
        }

        private void InstallationDirectoryButton_Click(object sender, RoutedEventArgs e)
        {
            ProcessHelper.LaunchFolder(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location));
        }

        private async void BackupSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            await this.Window.RunAsyncOperation(async () =>
            {
                string filePath = ChannelSession.Services.FileService.ShowSaveFileDialog(ChannelSession.Settings.Name + "." + SettingsV2Model.SettingsBackupFileExtension);
                if (!string.IsNullOrEmpty(filePath))
                {
                    await ChannelSession.Services.Settings.SavePackagedBackup(ChannelSession.Settings, filePath);
                }
            });
        }

        private async void RestoreSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ShowConfirmation("This will delete the settings of the currently logged in account and restore the settings from the backup. Are you sure you wish to do this?"))
            {
                string filePath = ChannelSession.Services.FileService.ShowOpenFileDialog(string.Format("Mix It Up Settings V2 Backup (*.{0})|*.{0}|All files (*.*)|*.*", SettingsV2Model.SettingsBackupFileExtension));
                if (!string.IsNullOrEmpty(filePath))
                {
                    Result<SettingsV2Model> result = await ChannelSession.Services.Settings.RestorePackagedBackup(filePath);
                    if (result.Success)
                    {
                        ChannelSession.AppSettings.BackupSettingsFilePath = filePath;
                        ChannelSession.AppSettings.BackupSettingsToReplace = ChannelSession.Settings.ID;
                        ((MainWindow)this.Window).Restart();
                    }
                    else
                    {
                        await DialogHelper.ShowMessage(result.Message);
                    }
                }
            }
        }

        private void SettingsBackupLocationButton_Click(object sender, RoutedEventArgs e)
        {
            string folderPath = ChannelSession.Services.FileService.ShowOpenFolderDialog();
            if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
            {
                ChannelSession.Settings.SettingsBackupLocation = folderPath;
                this.SettingsBackupRateComboBox.IsEnabled = true;
            }
        }

        private void SettingsBackupRateComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (this.SettingsBackupRateComboBox.SelectedIndex >= 0)
            {
                ChannelSession.Settings.SettingsBackupRate = (SettingsBackupRateEnum)this.SettingsBackupRateComboBox.SelectedItem;
            }
        }

        private async void ReRunWizardSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ShowConfirmation("Mix It Up will restart and the New User Wizard will be re-run when you log in. This will allow you to re-import your data, which could duplicate and overwrite your Commands & User data. Are you sure you wish to do this?"))
            {
                MainWindow mainWindow = (MainWindow)this.Window;
                mainWindow.ReRunWizard();
            }
        }

        private async void EnableDiagnosticLogsButton_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ShowConfirmation("This will enable diagnostic logging and restart Mix It Up. This should only be done with advised by a Mix It Up developer. Are you sure you wish to do this?"))
            {
                ChannelSession.AppSettings.DiagnosticLogging = true;
                ((MainWindow)this.Window).Restart();
            }
        }

        private async void DisableDiagnosticLogsButton_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ShowConfirmation("This will disable diagnostic logging and restart Mix It Up. Are you sure you wish to do this?"))
            {
                ChannelSession.AppSettings.DiagnosticLogging = false;
                ((MainWindow)this.Window).Restart();
            }
        }

        private async void DeleteSettingsDataButton_Click(object sender, RoutedEventArgs e)
        {
            if (await DialogHelper.ShowConfirmation("This will completely delete the settings of the currently logged in account and is not reversible. Are you sure you wish to do this?"))
            {
                ChannelSession.AppSettings.SettingsToDelete = ChannelSession.Settings.ID;
                ((MainWindow)this.Window).Restart();
            }
        }
    }
}
