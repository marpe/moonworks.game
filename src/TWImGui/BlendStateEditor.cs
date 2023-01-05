using Mochi.DearImGui;

namespace MyGame.TWImGui;

public enum BlendMode
{
    AlphaBlend,
    NonPremultiplied,
    Opaque,
    Additive,
    Multiply,
    Combine,
}

public static unsafe class BlendStateEditor
{
    private static readonly string[] _blendOpNames;
    private static readonly string[] _blendFactorNames;
    private static readonly string[] _defaultBlendModeNames;

    static BlendStateEditor()
    {
        _blendOpNames = Enum.GetNames<BlendOp>();
        _blendFactorNames = Enum.GetNames<BlendFactor>();
        _defaultBlendModeNames = Enum.GetNames<BlendMode>();
    }

    private static bool AreEqual(ColorAttachmentBlendState a, ColorAttachmentBlendState b)
    {
        return a.BlendEnable == b.BlendEnable &&
               a.AlphaBlendOp == b.AlphaBlendOp &&
               a.ColorBlendOp == b.ColorBlendOp &&
               a.SourceColorBlendFactor == b.SourceColorBlendFactor &&
               a.SourceAlphaBlendFactor == b.SourceAlphaBlendFactor &&
               a.DestinationColorBlendFactor == b.DestinationColorBlendFactor &&
               a.DestinationAlphaBlendFactor == b.DestinationAlphaBlendFactor &&
               a.ColorWriteMask == b.ColorWriteMask;
    }

    private static string BuildBlendString(ColorAttachmentBlendState state)
    {
        string GetOpString(BlendOp op, BlendFactor srcBlend, BlendFactor destBlend)
        {
            return op switch
            {
                BlendOp.Add => $"Source * {srcBlend} + Dest * {destBlend}",
                BlendOp.Subtract => $"Source * {srcBlend} - Dest * {destBlend}",
                BlendOp.ReverseSubtract => $"Dest * {destBlend} - Source * {srcBlend}",
                BlendOp.Min => $"Min(Source * {srcBlend}, Dest * {destBlend})",
                BlendOp.Max => $"Max(Source * {srcBlend}, Dest * {destBlend})",
                _ => throw new ArgumentOutOfRangeException(),
            };
        }

        return
            $"RGB: {GetOpString(state.ColorBlendOp, state.SourceColorBlendFactor, state.DestinationColorBlendFactor)}\n" +
            $"A: {GetOpString(state.AlphaBlendOp, state.SourceAlphaBlendFactor, state.DestinationAlphaBlendFactor)}";
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

        var blendStateIdx = 0;
        if (ImGuiExt.Combo("Load Default", ref blendStateIdx, _defaultBlendModeNames))
        {
            var blendState = (BlendMode)blendStateIdx;
            state = blendState switch
            {
                BlendMode.AlphaBlend => ColorAttachmentBlendState.AlphaBlend,
                BlendMode.NonPremultiplied => ColorAttachmentBlendState.NonPremultiplied,
                BlendMode.Opaque => ColorAttachmentBlendState.Opaque,
                BlendMode.Additive => ColorAttachmentBlendState.Additive,
                BlendMode.Multiply => Pipelines.MultiplyBlendState,
                BlendMode.Combine => Pipelines.CombineBlendState,
                _ => throw new ArgumentOutOfRangeException(),
            };
        }

        var blendStr = BuildBlendString(state);
        ImGui.PushTextWrapPos();
        ImGui.Text(blendStr);
        ImGui.PopTextWrapPos();

        ImGui.PopID();
        return !AreEqual(prevState, state);
    }
}
