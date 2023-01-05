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

    private static Dictionary<uint, ColorAttachmentBlendState> _defaults = new();

    public static bool Draw(string name, ref ColorAttachmentBlendState state)
    {
        ImGui.PushID(name);

        var id = ImGui.GetID("Id");
        if (!_defaults.TryGetValue(id, out var defaultState))
        {
            defaultState = state;
            _defaults.Add(id, defaultState);
        }

        var result = false;

        result |= SimpleTypeInspector.InspectBool("Enabled", ref state.BlendEnable);
        result |= EnumInspector.InspectEnum("ColorOp", ref state.ColorBlendOp, false);
        result |= EnumInspector.InspectEnum("AlphaOp", ref state.AlphaBlendOp, false);
        ImGui.Separator();
        result |= EnumInspector.InspectEnum("SourceColor", ref state.SourceColorBlendFactor, false);
        result |= EnumInspector.InspectEnum("DestColor", ref state.DestinationColorBlendFactor, false);
        ImGui.Separator();
        result |= EnumInspector.InspectEnum("SourceAlpha", ref state.SourceAlphaBlendFactor, false);
        result |= EnumInspector.InspectEnum("DestAlpha", ref state.DestinationAlphaBlendFactor, false);

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
            result = true;
        }

        var blendStr = BuildBlendString(state);
        ImGui.PushTextWrapPos();
        ImGui.Text(blendStr);
        ImGui.PopTextWrapPos();

        if (!AreEqual(defaultState, state))
        {
            if (ImGuiExt.ColoredButton("Reset", new Num.Vector2(-ImGuiExt.FLT_MIN, 0)))
            {
                state = _defaults[id];
                result = true;
            }    
        }
        
        ImGui.PopID();
        return result;
    }
}
