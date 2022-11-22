using Mochi.DearImGui;

namespace MyGame.TWImGui;

public static unsafe class BlendStateEditor
{
    private static readonly string[] _blendOpNames;
    private static readonly string[] _blendFactorNames;

    static BlendStateEditor()
    {
        _blendOpNames = Enum.GetNames<BlendOp>();
        _blendFactorNames = Enum.GetNames<BlendFactor>();
    }

    private static bool AreEqual(ColorAttachmentBlendState a, ColorAttachmentBlendState b)
    {
        return a.BlendEnable == b.BlendEnable &&
               a.AlphaBlendOp == b.AlphaBlendOp &&
               a.ColorBlendOp == b.ColorBlendOp &&
               a.SourceColorBlendFactor == b.SourceColorBlendFactor &&
               a.SourceAlphaBlendFactor == b.SourceAlphaBlendFactor &&
               a.DestinationColorBlendFactor == b.DestinationColorBlendFactor &&
               a.DestinationAlphaBlendFactor == b.DestinationAlphaBlendFactor;
    }

    public static bool ComboStep(string label, ref int currentIndex, string[] items)
    {
        var result = false;

        ImGui.PushStyleColor(ImGuiCol.Button, Color.Transparent.PackedValue);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Color.Transparent.PackedValue);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Color.Transparent.PackedValue);

        if (ImGui.Button(FontAwesome6.AngleLeft + "##Left" + label, default))
        {
            currentIndex = (items.Length + currentIndex - 1) % items.Length;
            result = true;
        }

        ImGui.SameLine(0, 0);
        if (ImGui.Button(FontAwesome6.AngleRight + "##Right" + label, default))
        {
            currentIndex = (items.Length + currentIndex + 1) % items.Length;
            result = true;
        }

        ImGui.PopStyleColor(3);

        ImGui.SameLine();
        var itemsByZero = string.Join('0', items);

        if (ImGui.BeginCombo(label, items[currentIndex]))
        {
            for (var i = 0; i < items.Length; i++)
            {
                var isSelected = i == currentIndex;
                if (ImGui.Selectable(items[i], isSelected, ImGuiSelectableFlags.None, default))
                {
                    currentIndex = i;
                    result = true;
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        // result |= ImGui.Combo(label, ImGuiExt.RefPtr(ref currentIndex), itemsByZero, items.Length);

        return result;
    }

    public static bool Draw(string name, ref ColorAttachmentBlendState state)
    {
        ImGui.PushID(name);
        var alphaBlendOpIndex = (int)state.AlphaBlendOp;
        var colorBlendOpIndex = (int)state.ColorBlendOp;
        var destColorBlendFactorIndex = (int)state.DestinationColorBlendFactor;
        var destAlphaBlendFactorIndex = (int)state.DestinationAlphaBlendFactor;
        var sourceColorBlendFactorIndex = (int)state.SourceColorBlendFactor;
        var sourceAlphaBlendFactorIndex = (int)state.SourceAlphaBlendFactor;
        var blendEnabled = state.BlendEnable;
        var prevState = state;
        ImGui.Checkbox("Enabled", ImGuiExt.RefPtr(ref blendEnabled));
        ComboStep("AlphaOp", ref alphaBlendOpIndex, _blendOpNames);
        ComboStep("ColorOp", ref colorBlendOpIndex, _blendOpNames);
        ComboStep("SourceColor", ref sourceColorBlendFactorIndex, _blendFactorNames);
        ComboStep("SourceAlpha", ref sourceAlphaBlendFactorIndex, _blendFactorNames);
        ComboStep("DestColor", ref destColorBlendFactorIndex, _blendFactorNames);
        ComboStep("DestAlpha", ref destAlphaBlendFactorIndex, _blendFactorNames);
        state.BlendEnable = blendEnabled;
        state.AlphaBlendOp = (BlendOp)alphaBlendOpIndex;
        state.ColorBlendOp = (BlendOp)colorBlendOpIndex;
        state.SourceColorBlendFactor = (BlendFactor)sourceColorBlendFactorIndex;
        state.SourceAlphaBlendFactor = (BlendFactor)sourceAlphaBlendFactorIndex;
        state.DestinationColorBlendFactor = (BlendFactor)destColorBlendFactorIndex;
        state.DestinationAlphaBlendFactor = (BlendFactor)destAlphaBlendFactorIndex;
        /// Blend equation is sourceColor * sourceBlend + destinationColor * destinationBlend
        ImGui.Text($"sourceColor * {state.SourceColorBlendFactor.ToString()} + destColor * {state.DestinationColorBlendFactor.ToString()}");
        ImGui.Text($"sourceAlpha * {state.SourceAlphaBlendFactor.ToString()} + destAlpha * {state.DestinationAlphaBlendFactor.ToString()}");
        if (ImGui.Button("AlphaBlend", default))
        {
            state = ColorAttachmentBlendState.AlphaBlend;
        }

        ImGui.SameLine();
        if (ImGui.Button("NonPremultiplied", default))
        {
            state = ColorAttachmentBlendState.NonPremultiplied;
        }

        ImGui.SameLine();
        if (ImGui.Button("Opaque", default))
        {
            state = ColorAttachmentBlendState.Opaque;
        }

        ImGui.PopID();
        return !AreEqual(prevState, state);
    }
}
