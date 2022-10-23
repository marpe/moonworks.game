using ImGuiNET;

namespace MyGame.TWImGui;

public static class BlendStateEditor
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

        if (ImGui.Button(FontAwesome6.ChevronLeft + "##Left" + label))
        {
            currentIndex = (items.Length + currentIndex - 1) % items.Length;
            result = true;
        }

        ImGui.SameLine(0, 0);
        if (ImGui.Button(FontAwesome6.ChevronRight + "##Right" + label))
        {
            currentIndex = (items.Length + currentIndex + 1) % items.Length;
            result = true;
        }

        ImGui.PopStyleColor(3);

        ImGui.SameLine();
        result |= ImGui.Combo(label, ref currentIndex, items, items.Length);

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
        ImGui.Checkbox("Enabled", ref blendEnabled);
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
        ImGui.Text($"sourceColor * {state.SourceColorBlendFactor} + destColor * {state.DestinationColorBlendFactor}");
        ImGui.Text($"sourceAlpha * {state.SourceAlphaBlendFactor} + destAlpha * {state.DestinationAlphaBlendFactor}");
        if (ImGui.Button("AlphaBlend"))
        {
            state = ColorAttachmentBlendState.AlphaBlend;
        }

        ImGui.SameLine();
        if (ImGui.Button("NonPremultiplied"))
        {
            state = ColorAttachmentBlendState.NonPremultiplied;
        }

        ImGui.SameLine();
        if (ImGui.Button("Opaque"))
        {
            state = ColorAttachmentBlendState.Opaque;
        }

        ImGui.PopID();
        return !AreEqual(prevState, state);
    }
}
