namespace OriathHub.Plugins.AutoHotKeyTrigger
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Numerics;
    using ClickableTransparentOverlay.Win32;
    using Coroutine;
    using OriathHub;
    using OriathHub.CoroutineEvents;
    using OriathHub.Plugin;
    using OriathHub.RemoteEnums;
    using OriathHub.RemoteObjects.Components;
    using OriathHub.Utils;
    using ImGuiNET;
    using Newtonsoft.Json;
    using OriathHub.Plugins.AutoHotKeyTrigger.ProfileManager;
    using OriathHub.Plugins.AutoHotKeyTrigger.ProfileManager.DynamicConditions;

    /// <summary>
    ///     <see cref="AutoHotKeyTrigger" /> plugin.
    /// </summary>
    public sealed class AutoHotKeyTriggerCore : PluginBase
    {
        /// <inheritdoc />
        public override string Name => "Auto HotKey Trigger";

        /// <inheritdoc />
        public override string Description => "Presses keys (flasks / skills) when user-defined conditions are met, plus an optional AutoQuit safety.";

        /// <inheritdoc />
        public override string Author => "OriathHub";

        /// <inheritdoc />
        public override string Version => "1.0.0";

        private AutoHotKeyTriggerSettings Settings = new();

        private readonly string warningMsg = "The current condition you have put for AutoQuit is yielding true.\n" +
            "This mean you will automatically logout as soon as you leave town/hideout.\n" +
            "Please update your AutoQuit condition and/or disable it and/or fix your exile state.";

        private readonly List<(string name, Profile value)> clonesToAdd = new();
        private static Vector4 ImpTextColor => ImGuiHelper.WarningTextColor();
        private readonly Vector2 size = new(624, 380);
        private readonly List<string> keyPressInfo = new();
        private bool keyPressInfoAdded = false;
        private bool isDebugWindowHovered = false;
        private ActiveCoroutine? onAreaChange;
        private string debugMessage = string.Empty;
        private string newProfileName = string.Empty;
        private bool stopShowingAutoQuitWarning = false;
        private bool autoQuitHotkeyDown = false;
        private bool dumpStatusEffectHotkeyDown = false;

        private string SettingPathname => Path.Join(this.DllDirectory, "config", "settings.txt");
        private bool ShouldExecuteAutoQuit =>
            this.Settings.EnableAutoQuit &&
            this.Settings.AutoQuitCondition.Evaluate();

        /// <inheritdoc />
        public override void DrawSettings()
        {
            ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
            ImGui.TextColored(ImpTextColor, "Do not trust Settings.txt files for Auto Hokey Trigger from sources you have not personally verified. " +
                              "They may contain malicious content that can compromise your computer. " +
                              "Using profiles with incorrectly configured rules may also lead to you being kicked from the server, " +
                              "or your account being banned as a result of preforming to many actions repeatedly.");
            ImGui.NewLine();
            ImGui.TextColored(ImpTextColor, "Again, all profiles/rules created to use a specified flask(s) should have at a minimum " +
                              "the FLASK_EFFECT and an appropriate number of FLASK_CHARGES defined as part of the use condition of a given profile rule. " +
                              "Failing to to include these two conditions as part of a rule will likely result in Auto Hotkey Trigger spamming the flask(s), " +
                              "resulting in a possible kick or ban from the game servers because of sending to many actions to the server. " +
                              "You have been warrned, use common sense when creating profiles/rulse with this tool.");
            ImGui.PopTextWrapPos();
            if (ImGui.CollapsingHeader("Common Config"))
            {
                ImGui.Checkbox("Debug Mode", ref this.Settings.DebugMode);
                ImGui.SameLine();
                ImGui.Checkbox("Trigger rules or execute Autoquit in Hideout", ref this.Settings.ShouldRunInHideout);
                ImGuiHelper.ToolTip("The debug mode may prove to be a helpful tool in troubleshooting Auto HotKey Trigger profile rules that are not preforming as expected. " +
                                    "It can also be used to verify if AutoHotKeyTrigger is spamming the profile rule action or not based on the included conditions of a given profile rule. " +
                                    "It is highly suggested to create and test all new profiles/rules with the debug mode turned on to insure that all rules are preforming as expected.");
                ImGuiHelper.HotkeyComboBox("Dump Player Status Effects",
                    ref this.Settings.DumpStatusEffectOnMe);
                ImGuiHelper.ToolTip($"This hotkey will dump the current active player's buff(s), debuff(s) into a text file in the OriathHub -> Plugins -> " +
                                    $"AutoHotKeyTrigger folder. Use this hotkey if the AutoHotKeyTrigger plugin fails to detect for example: " +
                                    $"bleeds, corrupting blood, poison, freeze, ignites or other de(buffs) currently active on the character.");
                ImGuiHelper.IEnumerableComboBox("Profile", this.Settings.Profiles.Keys, ref this.Settings.CurrentProfile);
                if (ImGui.Button("Add/Reset and Activate League Start Default Profile"))
                {
                    this.CreateDefaultProfile();
                }
            }

            if (ImGui.CollapsingHeader("Add New Profile"))
            {
                ImGui.InputText("Name", ref this.newProfileName, 100);
                ImGui.SameLine();
                if (ImGui.Button("Add"))
                {
                    if (!string.IsNullOrEmpty(this.newProfileName))
                    {
                        this.Settings.Profiles.Add(this.newProfileName, new Profile());
                        this.newProfileName = string.Empty;
                    }
                }
            }

            // separate update to allow settings to draw correctly,
            // does not really hurt performance and only called
            // when the settings window is open
            DynamicCondition.UpdateState();
            if (ImGui.CollapsingHeader("Existing Profiles"))
            {
                this.DrawExistingProfilesBody();
            }
        }

        private void DrawExistingProfilesBody()
        {
            foreach (var (key, profile) in this.Settings.Profiles)
            {
                var isOpened = ImGui.TreeNode($"{key} (?)");
                ImGuiHelper.ToolTip("Rules (tabs) can be moved via drag and drop. They can be cloned by right click.");
                if (isOpened)
                {
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Delete Profile"))
                    {
                        this.Settings.Profiles.Remove(key);
                        if (this.Settings.CurrentProfile == key)
                        {
                            this.Settings.CurrentProfile = string.Empty;
                        }
                    }

                    ImGui.SameLine();
                    if (ImGui.SmallButton("Clone Profile"))
                    {
                        this.clonesToAdd.Add(($"{key}1", new(profile)));

                    }

                    profile.DrawSettings(key, this.Settings.Profiles);
                    ImGui.TreePop();
                }
            }

            this.clonesToAdd.RemoveAll(k => this.Settings.Profiles.TryAdd(k.name, k.value) || true); // remove even if add fails.
        }

        /// <inheritdoc />
        public override void DrawAdvancedSettings()
        {
            if (ImGui.CollapsingHeader("Auto Quit"))
            {
                this.DrawAutoQuitControls();
            }
        }

        private void DrawAutoQuitControls()
        {
            ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X / 6);
            ImGui.Checkbox("Enable AutoQuit", ref this.Settings.EnableAutoQuit);
            this.Settings.AutoQuitCondition.Display(true);
            ImGui.Separator();
            ImGui.Checkbox("Enable AutoQuit Manual Hotkey", ref this.Settings.EnableAutoQuitKey);
            ImGui.Text("Hotkey to manually quit game connection: ");
            ImGui.SameLine();
            ImGuiHelper.HotkeyComboBox("##Manual Quit HotKey", ref this.Settings.AutoQuitKey);
            ImGui.PopItemWidth();
        }

        /// <inheritdoc />
        public override IEnumerable<SettingSearchEntry> GetSearchableSettings() => new[]
        {
            new SettingSearchEntry("Common Config", "Debug Mode", () =>
            {
                ImGui.Checkbox("Debug Mode", ref this.Settings.DebugMode);
                ImGuiHelper.ToolTip("The debug mode may prove to be a helpful tool in troubleshooting Auto HotKey Trigger profile rules that are not preforming as expected. " +
                                    "It can also be used to verify if AutoHotKeyTrigger is spamming the profile rule action or not based on the included conditions of a given profile rule. " +
                                    "It is highly suggested to create and test all new profiles/rules with the debug mode turned on to insure that all rules are preforming as expected.");
            }, "debug troubleshoot"),
            new SettingSearchEntry("Common Config", "Trigger rules or execute Autoquit in Hideout",
                () => ImGui.Checkbox("Trigger rules or execute Autoquit in Hideout", ref this.Settings.ShouldRunInHideout), "hideout run autoquit"),
            new SettingSearchEntry("Common Config", "Dump Player Status Effects", () =>
            {
                ImGuiHelper.HotkeyComboBox("Dump Player Status Effects", ref this.Settings.DumpStatusEffectOnMe);
                ImGuiHelper.ToolTip($"This hotkey will dump the current active player's buff(s), debuff(s) into a text file in the OriathHub -> Plugins -> " +
                                    $"AutoHotKeyTrigger folder. Use this hotkey if the AutoHotKeyTrigger plugin fails to detect for example: " +
                                    $"bleeds, corrupting blood, poison, freeze, ignites or other de(buffs) currently active on the character.");
            }, "dump status effects buff debuff hotkey"),
            new SettingSearchEntry("Common Config", "Profile", () =>
            {
                ImGuiHelper.IEnumerableComboBox("Profile", this.Settings.Profiles.Keys, ref this.Settings.CurrentProfile);
                if (ImGui.Button("Add/Reset and Activate League Start Default Profile"))
                {
                    this.CreateDefaultProfile();
                }
            }, "profile select default league start"),

            new SettingSearchEntry("Add New Profile", "Add New Profile", () =>
            {
                ImGui.InputText("Name", ref this.newProfileName, 100);
                ImGui.SameLine();
                if (ImGui.Button("Add"))
                {
                    if (!string.IsNullOrEmpty(this.newProfileName))
                    {
                        this.Settings.Profiles.Add(this.newProfileName, new Profile());
                        this.newProfileName = string.Empty;
                    }
                }
            }, "add new profile create"),

            new SettingSearchEntry("Existing Profiles", "Existing Profiles", () =>
            {
                DynamicCondition.UpdateState();
                this.DrawExistingProfilesBody();
            }, "existing profiles rules clone delete edit"),

            new SettingSearchEntry("Advanced / Auto Quit", "Auto Quit", this.DrawAutoQuitControls,
                "auto quit autoquit hotkey condition disconnect"),
        };

        /// <inheritdoc />
        public override void DrawUI()
        {
            if (this.Settings.DebugMode)
            {
                ImGui.SetNextWindowSize(size, ImGuiCond.FirstUseEver);
                if (ImGui.Begin($"AHK Debug Window", ref this.Settings.DebugMode,
                    this.isDebugWindowHovered ? ImGuiWindowFlags.MenuBar : ImGuiWindowFlags.None))
                {
                    this.isDebugWindowHovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);
                    if (ImGui.BeginMenuBar())
                    {
                        if (ImGui.Button("Clear History"))
                        {
                            this.keyPressInfo.Clear();
                        }

                        ImGui.EndMenuBar();
                    }

                    for (var i = 0; i < this.keyPressInfo.Count; i++)
                    {
                        ImGui.Text($"{i}-{this.keyPressInfo[i]}");
                    }

                    if (this.keyPressInfoAdded)
                    {
                        ImGui.SetScrollHereY();
                        this.keyPressInfoAdded = false;
                    }

                    if (!string.IsNullOrEmpty(this.debugMessage))
                    {
                        ImGui.Separator();
                        ImGui.TextWrapped($"Issues: {this.debugMessage}");
                    }
                }

                ImGui.End();
            }

            this.AutoQuitWarningUi();
            if (!this.ShouldExecutePlugin())
            {
                return;
            }

            DynamicCondition.UpdateState();
            if (this.ShouldExecuteAutoQuit ||
                (this.Settings.EnableAutoQuitKey &&
                HotkeyHelper.IsPressedOnce(this.Settings.AutoQuitKey, ref this.autoQuitHotkeyDown)))
            {
                MiscHelper.KillTCPConnectionForProcess(Core.Process.Pid);
            }

            if (HotkeyHelper.IsPressedOnce(this.Settings.DumpStatusEffectOnMe, ref this.dumpStatusEffectHotkeyDown))
            {
                if (Core.States.InGameStateObject.CurrentAreaInstance.Player.TryGetComponent<Buffs>(out var buff))
                {
                    var data = "===========================================" + Environment.NewLine;
                    foreach (var statusEffect in buff.StatusEffects)
                    {
                        data += $"{statusEffect.Key} {statusEffect.Value}\n";
                    }

                    if (!string.IsNullOrEmpty(data))
                    {
                        File.AppendAllText(Path.Join(this.DllDirectory, "player_status_effect.txt"), data + Environment.NewLine);
                    }
                }
            }

            if (Core.OHSettings.EnableControllerMode)
            {
                // this is actually disabled in <see cref="MiscHelper.KeyUp"/> function.
                // follow is done just to provide debug msg to end users.
                this.debugMessage = "Controller mode enabled. this plugin doesn't support controllers";
                return;
            }

            if (string.IsNullOrEmpty(this.Settings.CurrentProfile))
            {
                this.debugMessage = "No Profile Selected.";
                return;
            }

            if (!this.Settings.Profiles.ContainsKey(this.Settings.CurrentProfile))
            {
                this.debugMessage = $"{this.Settings.CurrentProfile} not found.";
                return;
            }

            if (Core.States.InGameStateObject.GameUi.ChatParent.IsChatActive)
            {
                this.debugMessage = "Chat window is active, so can not drink flasks or trigger skills.";
                return;
            }

            foreach (var rule in this.Settings.Profiles[this.Settings.CurrentProfile].Rules)
            {
                rule.Execute(this.DebugLog);
            }
        }

        private void DebugLog(string logText)
        {
            if (this.Settings.DebugMode)
            {
                this.keyPressInfo.Add($"{DateTime.Now.TimeOfDay}: {logText}");
            }

            this.keyPressInfoAdded = true;
        }

        /// <inheritdoc />
        public override void OnDisable()
        {
            this.onAreaChange?.Cancel();
            this.onAreaChange = null;
        }

        /// <inheritdoc />
        public override void OnEnable(bool isGameOpened)
        {
            var jsonData2 = File.ReadAllText(this.DllDirectory + @"/StatusEffectGroup.json");
            JsonDataHelper.StatusEffectGroups = JsonConvert.DeserializeObject<
                Dictionary<string, List<string>>>(jsonData2)
                ?? new Dictionary<string, List<string>>();

            if (File.Exists(this.SettingPathname))
            {
                var content = File.ReadAllText(this.SettingPathname);
                this.Settings = JsonConvert.DeserializeObject<AutoHotKeyTriggerSettings>(
                    content,
                    new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.Auto
                    }) ?? new AutoHotKeyTriggerSettings();
            }
            else
            {
                this.CreateDefaultProfile();
            }

            this.onAreaChange = CoroutineHandler.Start(this.EnableAutoQuitWarningUiOnAreaChange());
        }

        /// <inheritdoc />
        public override void SaveSettings()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(this.SettingPathname) ?? string.Empty);
            var settingsData = JsonConvert.SerializeObject(this.Settings,
                Formatting.Indented,
                new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto
                });
            File.WriteAllText(this.SettingPathname, settingsData);
        }

        private bool ShouldExecutePlugin()
        {
            var cgs = Core.States.GameCurrentState;
            if (cgs != GameStateTypes.InGameState)
            {
                this.debugMessage = $"Current game state isn't InGameState, it's {cgs}.";
                return false;
            }

            if (!Core.Process.Foreground)
            {
                this.debugMessage = "Game is minimized.";
                return false;
            }

            var areaDetails = Core.States.InGameStateObject.CurrentWorldInstance.AreaDetails;
            if (areaDetails.IsTown)
            {
                this.debugMessage = "Player is in town.";
                return false;
            }

            if (!this.Settings.ShouldRunInHideout && areaDetails.IsHideout)
            {
                this.debugMessage = "Player is in hideout & hideout execution is turned off.";
                return false;
            }

            if (Core.States.InGameStateObject.CurrentAreaInstance.Player.TryGetComponent<Life>(out var lifeComp))
            {
                if (lifeComp.Health.Current <= 0)
                {
                    this.debugMessage = "Player is dead.";
                    return false;
                }
            }
            else
            {
                this.debugMessage = "Can not find player Life component.";
                return false;
            }

            if (Core.States.InGameStateObject.CurrentAreaInstance.Player.TryGetComponent<Buffs>(out var buffComp))
            {
                if (buffComp.StatusEffects.ContainsKey("grace_period"))
                {
                    this.debugMessage = "Player has Grace Period.";
                    return false;
                }
            }
            else
            {
                this.debugMessage = "Can not find player PlayerBuffs component.";
                return false;
            }

            if (!Core.States.InGameStateObject.CurrentAreaInstance.Player.TryGetComponent<Actor>(out var _))
            {
                this.debugMessage = "Can not find player Actor component.";
                return false;
            }

            this.debugMessage = string.Empty;
            return true;
        }

        /// <summary>
        ///     Creates a default profile that is only valid for flasks on newly created character.
        /// </summary>
        private void CreateDefaultProfile()
        {
            Profile profile = new();
            foreach (var rule in Rule.CreateDefaultRules())
            {
                profile.Rules.Add(rule);
            }

            this.Settings.Profiles["LeagueStartDefaultProfile"] = profile;
            this.Settings.CurrentProfile = "LeagueStartDefaultProfile";
            this.Settings.Profiles["ProfileMidGame"] = new();
            this.Settings.Profiles["ProfileEndGame"] = new();
        }

        private void AutoQuitWarningUi()
        {

            if (!this.stopShowingAutoQuitWarning &&
                (Core.States.InGameStateObject.CurrentWorldInstance.AreaDetails.IsTown ||
                Core.States.InGameStateObject.CurrentWorldInstance.AreaDetails.IsHideout) &&
                this.ShouldExecuteAutoQuit)
            {
                ImGui.OpenPopup("AutoQuitWarningUi");
            }

            if (ImGui.BeginPopup("AutoQuitWarningUi"))
            {
                ImGui.Text(this.warningMsg);
                if (ImGui.Button("I understand", new Vector2(ImGui.CalcTextSize(this.warningMsg).X, 50f)))
                {
                    this.stopShowingAutoQuitWarning = true;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }
        }

        private IEnumerator<Wait> EnableAutoQuitWarningUiOnAreaChange()
        {
            while (true)
            {
                yield return new Wait(RemoteEvents.AreaChanged);
                this.stopShowingAutoQuitWarning = false;
            }
        }
    }
}
