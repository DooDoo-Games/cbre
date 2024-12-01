using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CBRE.Settings;
using ImGuiNET;
using NativeFileDialogNET;
using Num = System.Numerics;

namespace CBRE.Editor.Popup {
    public sealed class SettingsPopup : PopupUI {
        protected override bool canBeDefocused => false;

        private int fixedHeight = 0;
        
        public SettingsPopup() : base("Options") { }

        protected override void ImGuiLayout(out bool shouldBeOpen) {
            shouldBeOpen = true;
            ImGui.BeginTabBar("SettingsTabber");

            if (ImGui.BeginTabItem("Camera")) {
                CameraGui();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Directories")) {
                DirectoryGui("Texture", Directories.TextureDirs);
                DirectoryGui("Model", Directories.ModelDirs);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Hotkeys")) {
                HotkeysGui();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Misc")) {
                MiscGui();
                ImGui.EndTabItem();
            }

            if (fixedHeight <= 0) {
                fixedHeight = (int)ImGui.GetCursorPosY();
            }
        }

        private const int minNonFixedHeight = 24;
        private int GetNonFixedHeight()
            => fixedHeight > 0 ? Math.Max((int)ImGui.GetWindowHeight() - fixedHeight, minNonFixedHeight) : minNonFixedHeight;

        private void CameraGui() {
            ImGui.Text("Camera Settings");
            ImGui.Separator();
            int fov = View.CameraFOV;
            ImGui.SliderInt("FOV", ref fov, v_min: 60, v_max: 110, format: "%d°");
            View.CameraFOV = fov;
        }
        
        private void DirectoryGui(string type, List<string> dirs) {
            ImGui.Text($"{type} Directories");
            ImGui.Separator();
            bool addNew = ImGui.Button($"+##{type}");
            ImGui.SameLine();
            addNew |= ImGui.Selectable($"Click to add a new {type.ToLower()} directory", false);
            if (addNew) {
                var result = new NativeFileDialog()
                    .SelectFolder()
                    .Open(out string path, Directory.GetCurrentDirectory());
                if (!string.IsNullOrEmpty(path)) {
                    dirs.Add(path.Replace('\\', '/'));
                }
            }
            if (ImGui.BeginChild($"{type}Dirs", new Num.Vector2(0, GetNonFixedHeight() * 0.35f))) {
                for (int i = 0; i < dirs.Count; i++) {
                    var dir = dirs[i];

                    using (ColorPush.RedButton()) {
                        if (ImGui.Button($"X##{type}Dirs{i}")) {
                            dirs.RemoveAt(i);
                            break;
                        }
                    }
                    
                    ImGui.SameLine();

                    if (ImGui.Selectable(dir, false)) {
                        var result = new NativeFileDialog()
                            .SelectFolder()
                            .Open(out string path, Directory.GetCurrentDirectory());
                        if (!string.IsNullOrEmpty(path)) {
                            dirs[i] = path.Replace('\\', '/');
                        }
                        break;
                    }
                }
            }
            ImGui.EndChild();
            ImGui.Separator();
        }

        public static string GetActionName(string action)
            => Hotkeys.GetHotkeyDefinition(action)?.Name ?? action;

        private void HotkeysGui() {
            ImGui.Text("Hotkeys");
            ImGui.Separator();
            bool addNew = ImGui.Button("+");
            ImGui.SameLine();
            addNew |= ImGui.Selectable("Click to add new bind");
            if (addNew) {
                GameMain.Instance.Popups.Add(new HotkeyListenerPopup(hotkeyIndex: null));
            }
            if (ImGui.BeginChild("Hotkeys", new Num.Vector2(0, GetNonFixedHeight() * 0.5f))) {
                for (int i = 0; i < SettingsManager.Hotkeys.Count; i++) {
                    var hotkey = SettingsManager.Hotkeys[i];
                    
                    using (ColorPush.RedButton()) {
                        if (ImGui.Button($"X##hotkeys{i}")) {
                            SettingsManager.Hotkeys.RemoveAt(i);
                            break;
                        }
                    }
                    ImGui.SameLine();
                    if (ImGui.Selectable($"{GetActionName(hotkey.ID)} - {hotkey.HotkeyString}", false)) {
                        GameMain.Instance.Popups.Add(new HotkeyListenerPopup(hotkeyIndex: i) {
                            SelectedAction = Hotkeys.GetHotkeyDefinition(hotkey.ID),
                            Combo = hotkey.HotkeyString
                        });
                        break;
                    }
                }
            }
            ImGui.EndChild();
        }

        private void MiscGui() {
            ImGui.Text("Misc");
            ImGui.Separator();
            bool dc = Misc.DiscordIntegration;
            ImGui.Checkbox("Enable Discord integration", ref dc);
            if (dc != Misc.DiscordIntegration) {
                Misc.DiscordIntegration = dc;
                GameMain.Instance.SetDiscord(dc);
            }
        }

        public override void Dispose() {
            base.Dispose();
            
            Hotkeys.SetupHotkeys(SettingsManager.Hotkeys);
            SettingsManager.Write();
        }
    }
}
