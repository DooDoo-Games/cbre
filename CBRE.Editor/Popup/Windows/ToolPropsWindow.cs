using System;
using System.Linq;
using CBRE.Editor.Documents;
using CBRE.Editor.Rendering;
using ImGuiNET;
using Microsoft.Xna.Framework.Graphics;
using Num = System.Numerics;

namespace CBRE.Editor.Popup {
    public class ToolPropsWindow : DockableWindow
    {
        public ToolPropsWindow() : base("tool_properties", ImGuiWindowFlags.None)
        {
        }

        protected override void ImGuiLayout(out bool shouldBeOpen) {
            shouldBeOpen = true;
            
            var window = GameMain.Instance.Window;
            ImGui.SetWindowPos(new Num.Vector2(ViewportManager.VPRect.Right, 47), ImGuiCond.FirstUseEver);
            ImGui.SetWindowSize(new Num.Vector2(window.ClientBounds.Width - ViewportManager.VPRect.Right, window.ClientBounds.Height - 47 - 60), ImGuiCond.FirstUseEver);
            ImGui.SetNextItemOpen(is_open: true, ImGuiCond.FirstUseEver);
            if (ImGui.TreeNode("Tool")) {
                GameMain.Instance.SelectedTool?.UpdateGui();
                ImGui.TreePop();
            }
            if (ImGui.TreeNode("Contextual Help")) {
                GameMain.Instance.UpdateContextHelp();
                ImGui.TreePop();
            }
            if (ImGui.TreeNode("Viewport Options")) {
                foreach (var (viewport, i) in ViewportManager.Viewports.Select((x, i) => (x, i + 1))) {
                    ImGui.Separator();
                    ImGui.Text($"Viewport {i}");
                    if (ImGui.BeginCombo($"Render Type##{i}", viewport.Type.ToString())) {
                        var evals = Enum.GetValues<Viewport3D.ViewType>();
                        for (int j = 0; j < evals.Length; j++) {
                            if (ImGui.Selectable(evals[j].ToString(), viewport.Type == evals[j])) {
                                viewport.Type = evals[j];
                                ViewportManager.MarkForRerender();
                                DocumentManager.Documents.ForEach(p => p.ObjectRenderer.MarkDirty());
                            }
                        }
                        ImGui.EndCombo();
                    }
                    bool b = viewport.ShouldRenderModels;
                    if (ImGui.Checkbox($"Should Render 3D Models##{i}", ref b)) {
                        viewport.ShouldRenderModels = b;
                    }
                }
                ImGui.TreePop();
            }
        }
    }
}
