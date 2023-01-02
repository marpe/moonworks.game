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
        ImGuiExt.ComboStep("AlphaOp", true, ref alphaBlendOpIndex, _blendOpNames);
        ImGuiExt.ComboStep("ColorOp", true, ref colorBlendOpIndex, _blendOpNames);
        ImGuiExt.ComboStep("SourceColor", true, ref sourceColorBlendFactorIndex, _blendFactorNames);
        ImGuiExt.ComboStep("SourceAlpha", true, ref sourceAlphaBlendFactorIndex, _blendFactorNames);
        ImGuiExt.ComboStep("DestColor", true, ref destColorBlendFactorIndex, _blendFactorNames);
        ImGuiExt.ComboStep("DestAlpha", true, ref destAlphaBlendFactorIndex, _blendFactorNames);
        state.BlendEnable = blendEnabled;
        state.AlphaBlendOp = (BlendOp)alphaBlendOpIndex;
        state.ColorBlendOp = (BlendOp)colorBlendOpIndex;
        state.SourceColorBlendFactor = (BlendFactor)sourceColorBlendFactorIndex;
        state.SourceAlphaBlendFactor = (BlendFactor)sourceAlphaBlendFactorIndex;
        state.DestinationColorBlendFactor = (BlendFactor)destColorBlendFactorIndex;
        state.DestinationAlphaBlendFactor = (BlendFactor)destAlphaBlendFactorIndex;
        // Blend equation is sourceColor * sourceBlend + destinationColor * destinationBlend
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
        
        ImGui.SameLine();
        if (ImGui.Button("Additive", default))
        {
            state = ColorAttachmentBlendState.Additive;
        }
        
        ImGui.PopID();
        return !AreEqual(prevState, state);
    }
}
