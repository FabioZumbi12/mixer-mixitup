﻿using MixItUp.Base.Model;
using MixItUp.Base.Model.Actions;
using MixItUp.Base.Model.Commands;
using MixItUp.Base.Model.Commands.Games;
using MixItUp.Base.Model.Requirements;
using MixItUp.Base.Model.Settings;
using MixItUp.Base.Model.User;
using MixItUp.Base.Util;
using Newtonsoft.Json.Linq;
using StreamingClient.Base.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MixItUp.Base.Services
{
    public enum SettingsBackupRateEnum
    {
        None = 0,
        Daily,
        Weekly,
        Monthly,
    }

    public class SettingsService
    {
        private static SemaphoreSlim semaphore = new SemaphoreSlim(1);

        public void Initialize() { Directory.CreateDirectory(SettingsV3Model.SettingsDirectoryName); }

        public async Task<IEnumerable<SettingsV3Model>> GetAllSettings()
        {
            bool backupSettingsLoaded = false;
            bool settingsLoadFailure = false;

            List<SettingsV3Model> allSettings = new List<SettingsV3Model>();
            foreach (string filePath in Directory.GetFiles(SettingsV3Model.SettingsDirectoryName))
            {
                if (filePath.EndsWith(SettingsV3Model.SettingsFileExtension))
                {
                    SettingsV3Model setting = null;
                    try
                    {
                        setting = await this.LoadSettings(filePath);
                        if (setting != null)
                        {
                            allSettings.Add(setting);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(ex);
                    }

                    if (setting == null)
                    {
                        string localBackupFilePath = string.Format($"{filePath}.{SettingsV3Model.SettingsLocalBackupFileExtension}");
                        if (File.Exists(localBackupFilePath))
                        {
                            try
                            {
                                setting = await this.LoadSettings(localBackupFilePath);
                                if (setting != null)
                                {
                                    allSettings.Add(setting);
                                    backupSettingsLoaded = true;
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Log(ex);
                            }
                        }
                    }

                    if (setting == null)
                    {
                        settingsLoadFailure = true;
                    }
                }
            }

            if (!string.IsNullOrEmpty(ChannelSession.AppSettings.BackupSettingsFilePath) && ChannelSession.AppSettings.BackupSettingsToReplace != Guid.Empty)
            {
                Logger.Log(LogLevel.Debug, "Restored settings file detected, starting restore process");

                SettingsV3Model settings = allSettings.FirstOrDefault(s => s.ID.Equals(ChannelSession.AppSettings.BackupSettingsToReplace));
                if (settings != null)
                {
                    File.Delete(settings.SettingsFilePath);
                    File.Delete(settings.DatabaseFilePath);

                    // Adding delay to ensure the above files are actually deleted
                    await Task.Delay(2000);

                    await ServiceManager.Get<IFileService>().UnzipFiles(ChannelSession.AppSettings.BackupSettingsFilePath, SettingsV3Model.SettingsDirectoryName);

                    ChannelSession.AppSettings.BackupSettingsFilePath = null;
                    ChannelSession.AppSettings.BackupSettingsToReplace = Guid.Empty;

                    return await this.GetAllSettings();
                }
            }
            else if (ChannelSession.AppSettings.SettingsToDelete != Guid.Empty)
            {
                Logger.Log(LogLevel.Debug, "Settings deletion detected, starting deletion process");

                SettingsV3Model settings = allSettings.FirstOrDefault(s => s.ID.Equals(ChannelSession.AppSettings.SettingsToDelete));
                if (settings != null)
                {
                    File.Delete(settings.SettingsFilePath);
                    File.Delete(settings.DatabaseFilePath);

                    ChannelSession.AppSettings.SettingsToDelete = Guid.Empty;

                    return await this.GetAllSettings();
                }
            }

            if (backupSettingsLoaded)
            {
                await DialogHelper.ShowMessage(Resources.BackupSettingsLoadedError);
            }
            if (settingsLoadFailure)
            {
                await DialogHelper.ShowMessage(Resources.SettingsLoadFailure);
            }

            return allSettings;
        }

        public async Task Initialize(SettingsV3Model settings)
        {
            await settings.Initialize();
        }

        public async Task Save(SettingsV3Model settings)
        {
            if (settings != null)
            {
                Logger.Log(LogLevel.Debug, "Settings save operation started");

                await semaphore.WaitAndRelease(async () =>
                {
                    settings.CopyLatestValues();
                    await FileSerializerHelper.SerializeToFile(settings.SettingsFilePath, settings);
                    await settings.SaveDatabaseData();
                });

                Logger.Log(LogLevel.Debug, "Settings save operation finished");
            }
        }

        public async Task SaveLocalBackup(SettingsV3Model settings)
        {
            if (settings != null)
            {
                Logger.Log(LogLevel.Debug, "Settings local backup save operation started");

                if (ServiceManager.Get<IFileService>().GetFileSize(settings.SettingsFilePath) == 0)
                {
                    Logger.Log(LogLevel.Debug, "Main settings file is empty, aborting local backup settings save operation");
                    return;
                }

                await semaphore.WaitAndRelease(async () =>
                {
                    await FileSerializerHelper.SerializeToFile(settings.SettingsLocalBackupFilePath, settings);
                });

                Logger.Log(LogLevel.Debug, "Settings local backup save operation finished");
            }
        }

        public async Task SavePackagedBackup(SettingsV3Model settings, string filePath)
        {
            await this.Save(ChannelSession.Settings);

            try
            {
                if (Directory.Exists(Path.GetDirectoryName(filePath)))
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }

                    using (ZipArchive zipFile = ZipFile.Open(filePath, ZipArchiveMode.Create))
                    {
                        zipFile.CreateEntryFromFile(settings.SettingsFilePath, Path.GetFileName(settings.SettingsFilePath));
                        zipFile.CreateEntryFromFile(settings.DatabaseFilePath, Path.GetFileName(settings.DatabaseFilePath));
                    }
                }
                else
                {
                    Logger.Log(LogLevel.Error, $"Directory does not exist for saving packaged backup: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        public async Task<Result<SettingsV3Model>> RestorePackagedBackup(string filePath)
        {
            try
            {
                string tempFilePath = ServiceManager.Get<IFileService>().GetTempFolder();
                string tempFolder = Path.GetDirectoryName(tempFilePath);

                string settingsFile = null;
                string databaseFile = null;

                try
                {
                    using (ZipArchive zipFile = ZipFile.Open(filePath, ZipArchiveMode.Read))
                    {
                        foreach (ZipArchiveEntry entry in zipFile.Entries)
                        {
                            string extractedFilePath = Path.Combine(tempFolder, entry.Name);
                            if (File.Exists(extractedFilePath))
                            {
                                File.Delete(extractedFilePath);
                            }

                            if (extractedFilePath.EndsWith(SettingsV3Model.SettingsFileExtension, StringComparison.InvariantCultureIgnoreCase))
                            {
                                settingsFile = extractedFilePath;
                            }
                            else if (extractedFilePath.EndsWith(SettingsV3Model.DatabaseFileExtension, StringComparison.InvariantCultureIgnoreCase))
                            {
                                databaseFile = extractedFilePath;
                            }
                        }
                        zipFile.ExtractToDirectory(tempFolder);
                    }
                }
                catch (Exception ex) { Logger.Log(ex); }

                int currentVersion = -1;
                if (!string.IsNullOrEmpty(settingsFile))
                {
                    currentVersion = await SettingsV3Upgrader.GetSettingsVersion(settingsFile);
                }

                if (currentVersion == -1)
                {
                    return new Result<SettingsV3Model>(MixItUp.Base.Resources.SettingsBackupNotValid);
                }

                if (currentVersion > SettingsV3Model.LatestVersion)
                {
                    return new Result<SettingsV3Model>(MixItUp.Base.Resources.SettingsBackupTooNew);
                }

                return new Result<SettingsV3Model>(await FileSerializerHelper.DeserializeFromFile<SettingsV3Model>(settingsFile, ignoreErrors: true));
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return new Result<SettingsV3Model>(ex);
            }
        }

        public async Task<bool> PerformAutomaticBackupIfApplicable(SettingsV3Model settings)
        {
            if (settings.SettingsBackupRate != SettingsBackupRateEnum.None)
            {
                Logger.Log(LogLevel.Debug, "Checking whether to perform automatic backup");

                DateTimeOffset newResetDate = settings.SettingsLastBackup;

                if (settings.SettingsBackupRate == SettingsBackupRateEnum.Daily) { newResetDate = newResetDate.AddDays(1); }
                else if (settings.SettingsBackupRate == SettingsBackupRateEnum.Weekly) { newResetDate = newResetDate.AddDays(7); }
                else if (settings.SettingsBackupRate == SettingsBackupRateEnum.Monthly) { newResetDate = newResetDate.AddMonths(1); }

                if (newResetDate < DateTimeOffset.Now)
                {
                    string backupPath = Path.Combine(SettingsV3Model.SettingsDirectoryName, SettingsV3Model.DefaultAutomaticBackupSettingsDirectoryName);
                    if (!string.IsNullOrEmpty(settings.SettingsBackupLocation))
                    {
                        backupPath = settings.SettingsBackupLocation;
                    }

                    try
                    {
                        if (!Directory.Exists(backupPath))
                        {
                            Directory.CreateDirectory(backupPath);
                        }

                        string filePath = Path.Combine(backupPath, settings.Name + "-Backup-" + DateTimeOffset.Now.ToString("MM-dd-yyyy") + "." + SettingsV3Model.SettingsBackupFileExtension);

                        await this.SavePackagedBackup(settings, filePath);

                        settings.SettingsLastBackup = DateTimeOffset.Now;
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(LogLevel.Error, "Failed to create automatic backup directory");
                        Logger.Log(ex);
                        return false;
                    }
                }
            }
            return true;
        }

        private async Task<SettingsV3Model> LoadSettings(string filePath)
        {
            return await SettingsV3Upgrader.UpgradeSettingsToLatest(filePath);
        }
    }

    public static class SettingsV3Upgrader
    {
        public static async Task<SettingsV3Model> UpgradeSettingsToLatest(string filePath)
        {
            int currentVersion = await GetSettingsVersion(filePath);
            if (currentVersion < 0)
            {
                // Settings file is invalid, we can't use this
                return null;
            }
            else if (currentVersion > SettingsV3Model.LatestVersion)
            {
                // Future build, like a preview build, we can't load this
                return null;
            }
            else if (currentVersion < SettingsV3Model.LatestVersion)
            {
                await SettingsV3Upgrader.Version5Upgrade(currentVersion, filePath);
            }
            SettingsV3Model settings = await FileSerializerHelper.DeserializeFromFile<SettingsV3Model>(filePath, ignoreErrors: true);
            settings.Version = SettingsV3Model.LatestVersion;
            return settings;
        }

        public static async Task Version5Upgrade(int version, string filePath)
        {
            if (version < 5)
            {
                string fileData = await ServiceManager.Get<IFileService>().ReadFile(filePath);
                fileData = fileData.Replace("MixItUp.Base.Model.User.UserRoleEnum", "MixItUp.Base.Model.User.OldUserRoleEnum");

                SettingsV3Model settings = await FileSerializerHelper.DeserializeFromFile<SettingsV3Model>(filePath, ignoreErrors: true);
                await settings.Initialize();
                
                await ServiceManager.Get<IDatabaseService>().Write(settings.DatabaseFilePath, "CREATE TABLE \"ImportedUsers\" (\"ID\" TEXT NOT NULL, \"Platform\" INTEGER NOT NULL, \"PlatformID\" TEXT, \"PlatformUsername\" TEXT, \"Data\" TEXT NOT NULL, UNIQUE(\"Platform\",\"PlatformID\",\"PlatformUsername\"), PRIMARY KEY(\"ID\"))");

                foreach (StreamingPlatformTypeEnum type in settings.StreamingPlatformAuthentications.Keys.ToList())
                {
                    if (type != StreamingPlatformTypeEnum.Twitch)
                    {
                        settings.StreamingPlatformAuthentications.Remove(type);
                    }
                }
                settings.DefaultStreamingPlatform = StreamingPlatformTypeEnum.Twitch;

#pragma warning disable CS0612 // Type or member is obsolete
                settings.MassGiftedSubsFilterAmount = settings.TwitchMassGiftedSubsFilterAmount;

                settings.AlertTwitchBitsCheeredColor = settings.AlertBitsCheeredColor;
                settings.AlertTwitchChannelPointsColor = settings.AlertChannelPointsColor;
                settings.AlertTwitchHypeTrainColor = settings.AlertHypeTrainColor;

                settings.ModerationFilteredWordsExcemptUserRole = UserRoles.ConvertFromOldRole(settings.ModerationFilteredWordsExcempt);
                settings.ModerationChatTextExcemptUserRole = UserRoles.ConvertFromOldRole(settings.ModerationChatTextExcempt);
                settings.ModerationBlockLinksExcemptUserRole = UserRoles.ConvertFromOldRole(settings.ModerationBlockLinksExcempt);
                settings.ModerationChatInteractiveParticipationExcemptUserRole = UserRoles.ConvertFromOldRole(settings.ModerationChatInteractiveParticipationExcempt);

                foreach (var title in settings.UserTitles)
                {
                    title.UserRole = UserRoles.ConvertFromOldRole(title.Role);
                }

                List<HotKeyConfiguration> hotKeyConfigurations = settings.HotKeys.Values.ToList();
                settings.HotKeys.Clear();
                foreach (HotKeyConfiguration hotKey in hotKeyConfigurations)
                {
                    hotKey.VirtualKey = ServiceManager.Get<IInputService>().ConvertOldKeyEnum(hotKey.Key);
                    settings.HotKeys[hotKey.ToString()] = hotKey;
                }

                foreach (var kvp in settings.CustomUsernameColors)
                {
                    UserRoleEnum newRole = UserRoles.ConvertFromOldRole(kvp.Key);
                    settings.CustomUsernameRoleColors[newRole] = kvp.Value;
                }

                foreach (var kvp in settings.StreamPass)
                {
                    kvp.Value.UserPermission = UserRoles.ConvertFromOldRole(kvp.Value.Permission);
                }

                foreach (var commandSettings in settings.PreMadeChatCommandSettings)
                {
                    commandSettings.UserRole = UserRoles.ConvertFromOldRole(commandSettings.Role);
                }

                List<UserDataModel> oldUserData = new List<UserDataModel>();
                await ServiceManager.Get<IDatabaseService>().Read(settings.DatabaseFilePath, "SELECT * FROM Users", (Dictionary<string, object> data) =>
                {
                    oldUserData.Add(JSONSerializerHelper.DeserializeFromString<UserDataModel>(data["Data"].ToString()));
                });

                foreach (UserDataModel oldUser in oldUserData)
                {
                    UserV2Model user = oldUser.ToV2Model();
                    if (user != null)
                    {
                        settings.Users[user.ID] = user;
                    }
                }

                foreach (CommandModelBase command in settings.Commands.Values)
                {
                    SettingsV3Upgrader.MultiPlatformCommandUpgrade(command);
                    settings.Commands.ManualValueChanged(command.ID);
                }
#pragma warning restore CS0612 // Type or member is obsolete

                await ServiceManager.Get<SettingsService>().Save(settings);
            }
        }

        public static void MultiPlatformCommandUpgrade(CommandModelBase command)
        {
#pragma warning disable CS0612 // Type or member is obsolete
            if (command is BetGameCommandModel)
            {
                BetGameCommandModel gCommand = (BetGameCommandModel)command;
                gCommand.StarterUserRole = UserRoles.ConvertFromOldRole(gCommand.StarterRole);

                foreach (GameOutcomeModel outcome in gCommand.BetOptions)
                {
                    foreach (var kvp in outcome.RoleProbabilityPayouts)
                    {
                        UserRoleEnum role = UserRoles.ConvertFromOldRole(kvp.Key);
                        kvp.Value.Role = OldUserRoleEnum.Banned;
                        kvp.Value.UserRole = role;
                        outcome.UserRoleProbabilityPayouts[role] = kvp.Value;
                    }
                }
            }
            else if (command is BidGameCommandModel)
            {
                BidGameCommandModel gCommand = (BidGameCommandModel)command;
                gCommand.StarterUserRole = UserRoles.ConvertFromOldRole(gCommand.StarterRole);
            }
            else if (command is DuelGameCommandModel)
            {
                DuelGameCommandModel gCommand = (DuelGameCommandModel)command;
                foreach (var kvp in gCommand.SuccessfulOutcome.RoleProbabilityPayouts)
                {
                    UserRoleEnum role = UserRoles.ConvertFromOldRole(kvp.Key);
                    kvp.Value.Role = OldUserRoleEnum.Banned;
                    kvp.Value.UserRole = role;
                    gCommand.SuccessfulOutcome.UserRoleProbabilityPayouts[role] = kvp.Value;
                }
            }
            else if (command is HeistGameCommandModel)
            {
                HeistGameCommandModel gCommand = (HeistGameCommandModel)command;
                foreach (var kvp in gCommand.UserSuccessOutcome.RoleProbabilityPayouts)
                {
                    UserRoleEnum role = UserRoles.ConvertFromOldRole(kvp.Key);
                    kvp.Value.Role = OldUserRoleEnum.Banned;
                    kvp.Value.UserRole = role;
                    gCommand.UserSuccessOutcome.UserRoleProbabilityPayouts[role] = kvp.Value;
                }
            }
            else if (command is RouletteGameCommandModel)
            {
                RouletteGameCommandModel gCommand = (RouletteGameCommandModel)command;
                foreach (var kvp in gCommand.UserSuccessOutcome.RoleProbabilityPayouts)
                {
                    UserRoleEnum role = UserRoles.ConvertFromOldRole(kvp.Key);
                    kvp.Value.Role = OldUserRoleEnum.Banned;
                    kvp.Value.UserRole = role;
                    gCommand.UserSuccessOutcome.UserRoleProbabilityPayouts[role] = kvp.Value;
                }
            }
            else if (command is SpinGameCommandModel)
            {
                SpinGameCommandModel gCommand = (SpinGameCommandModel)command;
                foreach (GameOutcomeModel outcome in gCommand.Outcomes)
                {
                    foreach (var kvp in outcome.RoleProbabilityPayouts)
                    {
                        UserRoleEnum role = UserRoles.ConvertFromOldRole(kvp.Key);
                        kvp.Value.Role = OldUserRoleEnum.Banned;
                        kvp.Value.UserRole = role;
                        outcome.UserRoleProbabilityPayouts[role] = kvp.Value;
                    }
                }
            }
            else if (command is SlotMachineGameCommandModel)
            {
                SlotMachineGameCommandModel gCommand = (SlotMachineGameCommandModel)command;
                foreach (SlotMachineGameOutcomeModel outcome in gCommand.Outcomes)
                {
                    foreach (var kvp in outcome.RoleProbabilityPayouts)
                    {
                        UserRoleEnum role = UserRoles.ConvertFromOldRole(kvp.Key);
                        kvp.Value.Role = OldUserRoleEnum.Banned;
                        kvp.Value.UserRole = role;
                        outcome.UserRoleProbabilityPayouts[role] = kvp.Value;
                    }
                }
            }

            foreach (RequirementModelBase requirement in command.Requirements.Requirements)
            {
                if (requirement is RoleRequirementModel)
                {
                    RoleRequirementModel rRequirement = (RoleRequirementModel)requirement;
                    rRequirement.UserRole = UserRoles.ConvertFromOldRole(rRequirement.Role);
                    foreach (OldUserRoleEnum oldRole in rRequirement.UserRoleList)
                    {
                        rRequirement.UserRoleList.Add(UserRoles.ConvertFromOldRole(oldRole));
                    }
                }
            }

            foreach (ActionModelBase action in command.Actions)
            {
                if (action is ConsumablesActionModel)
                {
                    ConsumablesActionModel cAction = (ConsumablesActionModel)action;
                    cAction.UserRoleToApplyTo = UserRoles.ConvertFromOldRole(cAction.UsersToApplyTo);
                }
                else if (action is InputActionModel)
                {
                    InputActionModel iAction = (InputActionModel)action;
                    if (iAction.Key.HasValue)
                    {
                        iAction.VirtualKey = ServiceManager.Get<IInputService>().ConvertOldKeyEnum(iAction.Key.GetValueOrDefault());
                    }
                }
            }
#pragma warning restore CS0612 // Type or member is obsolete
        }

        public static async Task<int> GetSettingsVersion(string filePath)
        {
            string fileData = await ServiceManager.Get<IFileService>().ReadFile(filePath);
            if (string.IsNullOrEmpty(fileData))
            {
                return -1;
            }
            JObject settingsJObj = JObject.Parse(fileData);
            return (int)settingsJObj["Version"];
        }
    }
}
