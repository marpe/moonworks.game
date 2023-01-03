using System.Buffers;
using System.Globalization;
using Mochi.DearImGui;
using Mochi.DearImGui.Internal;
using MyGame.Editor;
using MyGame.WorldsRoot;

namespace MyGame.TWImGui;

public static unsafe class ImGuiExt
{
    public const float FLT_MIN = 1.175494351e-38F;
    public const float FLT_MAX = 3.402823466e+38F;
    public const float FLT_EPSILON = 1.192092896e-07F;

    public const ImGuiTableFlags DefaultTableFlags = ImGuiTableFlags.Borders | ImGuiTableFlags.BordersOuter |
                                                     ImGuiTableFlags.Hideable | ImGuiTableFlags.Resizable |
                                                     ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.RowBg |
                                                     ImGuiTableFlags.NoPadOuterX;

    [CVar("imgui.debug", "Toggle inspector debug information")]
    public static bool DebugInspectors = false;

    public static Color CheckboxBorderColor = new(92, 92, 92);

    public static readonly Dictionary<uint, bool> OpenFoldouts = new();

    private static readonly Stack<Color> _colorStack = new();
    private static readonly Stack<int> _labelWidthStack = new();
    public static Num.Vector2 ButtonPadding => new(6f, 4f);

    public static Color[] Colors =
    {
        new(32, 109, 255),
        new(11, 117, 196),
        new(0xA6, 0x1D, 0x1D),
        new(0x13, 0xAE, 0xB5),
        new(0xB5, 0x70, 0x27),
        new(0xAB, 0x10, 0x8F),
        Color.Fuchsia,
        Color.GreenYellow,
        Color.LightGray,
        Color.LightGreen,
        Color.Linen,
        Color.MintCream,
        Color.MistyRose,
        Color.NavajoWhite,
        Color.MediumBlue,
        Color.OliveDrab,
    };

    private static Num.Vector2 CollapsingHeaderFramePadding = new(6, 4);

    public static Color GetColor(int i = 0)
    {
        return Colors[i % Colors.Length];
    }

    public static void PushLabelWidth(int width)
    {
        _labelWidthStack.Push(width);
    }

    public static void PopLabelWidth()
    {
        _labelWidthStack.Pop();
    }

    public static bool Begin(string name, ref bool isOpen, ImGuiWindowFlags flags = ImGuiWindowFlags.None)
    {
        var framePadding = ImGui.GetStyle()->FramePadding;
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Num.Vector2(framePadding.X, 8));
        flags |= ImGuiWindowFlags.NoCollapse; // | ImGuiWindowFlags.NoTitleBar;
        var result = ImGui.Begin(name, RefPtr(ref isOpen), flags);
        ImGui.PopStyleVar();

        return result;
    }

    public static T* RefPtr<T>(ref T value) where T : unmanaged
    {
        fixed (T* valuePtr = &value)
        {
            return valuePtr;
        }
    }

    public static bool IsKeyboardShortcutPressed(ReadOnlySpan<char> keyboardShortcut)
    {
        var result = true;

        for (var j = 0; j < keyboardShortcut.Length; j++)
        {
            if (keyboardShortcut[j] == '^')
            {
                result &= ImGui.IsKeyDown(ImGuiKey.LeftCtrl) ||
                          ImGui.IsKeyDown(ImGuiKey.RightCtrl);
            }
            else if (keyboardShortcut[j] == '+')
            {
                result &= ImGui.IsKeyDown(ImGuiKey.LeftShift) ||
                          ImGui.IsKeyDown(ImGuiKey.RightShift);
            }
            else if (keyboardShortcut[j] == '!')
            {
                result &= ImGui.IsKeyDown(ImGuiKey.LeftAlt) ||
                          ImGui.IsKeyDown(ImGuiKey.RightAlt);
            }
            else
            {
                var keyStr = keyboardShortcut.Slice(j);
                var keyCode = Enum.Parse<ImGuiKey>(keyStr);
                return result && ImGui.IsKeyPressed(keyCode);
            }

            if (!result)
            {
                return false;
            }
        }

        return false;
    }

    public static bool Fold(string label)
    {
        var id = ImGui.GetID(label);
        if (!OpenFoldouts.ContainsKey(id))
        {
            OpenFoldouts.Add(id, false);
        }

        var avail = ImGui.GetContentRegionAvail();
        var size = new Num.Vector2(avail.X, 20).EnsureNotZero();

        var cursorStart = ImGui.GetCursorScreenPos();
        if (ImGui.InvisibleButton(label, size))
        {
            OpenFoldouts[id] = !OpenFoldouts[id];
        }

        var isHovered = ImGui.IsItemHovered();
        if (isHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var labelOffsetX = 20;

        var backgroundColor = isHovered switch
        {
            true => new Color(0.2f, 0.2f, 0.2f), // new Color(0.4f, 0.4f, 0.4f),
            _ => Color.Transparent,
        };

        var cursorEnd = cursorStart + size;

        var dl = ImGui.GetWindowDrawList();
        dl->AddRectFilled(cursorStart, cursorEnd, backgroundColor.PackedValue);

        var padding = new Num.Vector2(0, (size.Y - ImGui.GetTextLineHeight()) * 0.5f);
        var textDisabledColor = ImGui.GetColorU32(ImGuiCol.TextDisabled);
        dl->AddText(cursorStart + padding, textDisabledColor, OpenFoldouts[id] ? FontAwesome6.ChevronDown : FontAwesome6.ChevronRight);
        dl->AddText(cursorStart + padding + new Num.Vector2(labelOffsetX, 0), textDisabledColor, label);

        return OpenFoldouts[id];
    }

    public static bool BeginPropTable(string id, ImGuiTableFlags flags = DefaultTableFlags | ImGuiTableFlags.ContextMenuInBody)
    {
        if (ImGui.BeginTable(id, 2, flags, default))
        {
            ImGui.TableSetupColumn("Key", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Value");

            return true;
        }

        return false;
    }

    public static void PropRow(string key, string value, string? unit, Color color)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TextDisabled(key);
        ImGui.TableNextColumn();
        ImGui.TextColored(color.ToNumerics(), value);
        if (unit != null)
        {
            ImGui.SameLine();
            ImGui.TextDisabled(unit);
        }
    }

    public static void PropRow(string key, string value, string? unit)
    {
        var color = ImGui.GetStyle()->Colors[(int)ImGuiCol.Text];
        PropRow(key, value, unit, color.ToColor());
    }

    public static void PropRow(string key, string value)
    {
        PropRow(key, value, null);
    }

    public static void PropRow(string key, string value, Color color)
    {
        PropRow(key, value, null, color);
    }

    public static bool InspectNumVector2(string label, ref Num.Vector2 value, string xLabel = "X", string xTooltip = "", string yLabel = "Y",
        string yTooltip = "", float step = 1f, float min = 0f, float max = 0f, string format = "%g")
    {
        return InspectVector2(label, ref value.X, ref value.Y, xLabel, xTooltip, yLabel, yTooltip, step, min, max, format);
    }

    public static bool InspectPoint(string label, ref int x, ref int y, string xLabel = "X", string xTooltip = "", string yLabel = "Y", string yTooltip = "",
        int step = 1, int min = 0, int max = 0, string format = "%d")
    {
        var isEdited = false;
        ImGui.PushID(label);
        {
            ImGui.BeginGroup();
            {
                LabelPrefix(label);

                var itemInnerSpacingX = ImGui.GetStyle()->ItemInnerSpacing.X;
                var inputWidth = ImGui.GetContentRegionAvail().X / 2;
                isEdited |= DrawVectorComponent(xLabel, xTooltip, "##x", ref x, inputWidth - itemInnerSpacingX, step, min, max, format);
                ImGui.SameLine();
                isEdited |= DrawVectorComponent(yLabel, yTooltip, "##y", ref y, inputWidth, step, min, max, format);
            }
            ImGui.EndGroup();

            float x2 = x, y2 = y;
            if (DrawCopyPasteMenu(ref x2, ref y2))
            {
                x = (int)x2;
                y = (int)y2;
            }
        }
        ImGui.PopID();

        return isEdited;
    }

    public static bool InspectVector2(string label, ref float x, ref float y, string xLabel = "X", string xTooltip = "", string yLabel = "Y",
        string yTooltip = "", float step = 1f, float min = 0f, float max = 0f, string format = "%g")
    {
        var isEdited = false;
        ImGui.PushID(label);
        {
            ImGui.BeginGroup();
            {
                LabelPrefix(label);

                var itemInnerSpacingX = ImGui.GetStyle()->ItemInnerSpacing.X;
                var inputWidth = ImGui.GetContentRegionAvail().X / 2;
                isEdited |= DrawVectorComponent(xLabel, xTooltip, "##x", ref x, inputWidth - itemInnerSpacingX, step, min, max, format);
                ImGui.SameLine();
                isEdited |= DrawVectorComponent(yLabel, yTooltip, "##y", ref y, inputWidth, step, min, max, format);
            }
            ImGui.EndGroup();
            isEdited |= DrawCopyPasteMenu(ref x, ref y);
        }
        ImGui.PopID();

        return isEdited;
    }

    private static void DrawComponentLabel(string label, string tooltip, float width)
    {
        var textColor = label switch
        {
            "X" => 0xff8888ffu,
            "Y" => 0xff88ff88u,
            "Z" => 0xffff8888u,
            _ => 0xff7f7f7fu,
        };

        ImGui.PushAllowKeyboardFocus(false);
        var textWidth = Math.Max(ImGui.CalcTextSize(label).X, 16);
        if (TextButton(label, tooltip, textColor, new Num.Vector2(textWidth, 0)))
        {
            ImGui.SetKeyboardFocusHere();
        }

        ImGui.PopAllowKeyboardFocus();

        ImGui.SameLine(0, 0);
        ImGui.SetNextItemWidth(width - textWidth - ImGui.GetStyle()->ItemInnerSpacing.X);
    }

    private static bool DrawVectorComponent(string label, string tooltip, string valueLabel, ref int value, float width, int step = 1, int min = 0,
        int max = 0, string format = "%g")
    {
        ImGui.BeginGroup();
        DrawComponentLabel(label, tooltip, width);
        var result = ImGui.DragInt(valueLabel, RefPtr(ref value), step, min, max, format);
        ImGui.EndGroup();
        return result;
    }

    private static bool DrawVectorComponent(string label, string tooltip, string valueLabel, ref float value, float width, float step = 1f, float min = 0f,
        float max = 0f,
        string format = "%g")
    {
        ImGui.BeginGroup();
        DrawComponentLabel(label, tooltip, width);
        var result = ImGui.DragFloat(valueLabel, RefPtr(ref value), step, min, max, format);
        ImGui.EndGroup();
        return result;
    }

    private static bool DrawCopyPasteMenu(ref float x, ref float y)
    {
        var result = false;
        if (ImGui.BeginPopupContextItem("ContextMenu"))
        {
            if (ImGui.Selectable(FontAwesome6.Copy + " Copy", false, ImGuiSelectableFlags.None, default))
            {
                SetVectorInClipboard(x, y);
            }

            if (ImGui.Selectable(FontAwesome6.Paste + " Paste", false, ImGuiSelectableFlags.None, default))
            {
                result |= ParseVectorFromClipboard(out x, out y);
            }

            ImGui.EndPopup();
        }

        return result;
    }

    public static void SetVectorInClipboard(float x, float y)
    {
        var str = $"{x.ToString("F0", CultureInfo.InvariantCulture)},{y.ToString("F0", CultureInfo.InvariantCulture)}";
        SDL.SDL_SetClipboardText(str);
    }

    public static bool ParseVectorFromClipboard(out float x, out float y)
    {
        var clipboard = SDL.SDL_GetClipboardText();
        var split = clipboard.Split(',');
        if (split.Length != 2)
        {
            x = y = 0;
            return true;
        }

        float.TryParse(split[0], out x);
        float.TryParse(split[1], out y);
        return true;
    }

    public static bool DrawCheckbox(string label, ref bool value)
    {
        var borderColor = new Color(93, 93, 93, 255).PackedValue;
        var frameBgColor = new Color(41, 41, 41, 255).PackedValue;
        var frameBgHoveredColor = frameBgColor;
        var frameBgActiveColor = frameBgHoveredColor;
        var checkColor = new Color(1.0f, 1.0f, 1.0f, 1.0f).PackedValue;

        if (value)
        {
            frameBgColor = borderColor = frameBgHoveredColor = frameBgActiveColor = new Color(2, 115, 235, 255).PackedValue;
        }

        ImGui.PushStyleColor(ImGuiCol.Border, borderColor);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, frameBgColor);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, frameBgHoveredColor);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, frameBgActiveColor);
        ImGui.PushStyleColor(ImGuiCol.CheckMark, checkColor);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Num.Vector2(2, 2));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1);
        var result = ImGui.Checkbox(label, RefPtr(ref value));
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(5);

        var isHovered = ImGui.IsItemHovered();
        if (isHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return result;
    }

    public static bool ColoredButton(string label)
    {
        var color = ImGui.GetStyle()->Colors[(int)ImGuiCol.Button];
        return ColoredButton(label, color.ToColor());
    }

    public static bool ColoredButton(string label, Num.Vector2 size)
    {
        var color = ImGui.GetStyle()->Colors[(int)ImGuiCol.Button];
        return ColoredButton(label, color.ToColor(), size);
    }

    public static bool ColoredButton(string label, Color buttonColor, string? tooltip = null)
    {
        return ColoredButton(label, buttonColor, Num.Vector2.Zero, tooltip);
    }

    public static bool ColoredButton(string label, Color buttonColor, Num.Vector2 size, string? tooltip = null)
    {
        var text = ImGui.GetStyle()->Colors[(int)ImGuiCol.Text];
        return ColoredButton(label, text.ToColor(), buttonColor, tooltip, size, ButtonPadding);
    }

    public static bool ColoredButton(string label, Color textColor, Color buttonColor, string? tooltip = null)
    {
        return ColoredButton(label, textColor, buttonColor, tooltip, Num.Vector2.Zero, ButtonPadding);
    }

    public static bool ColoredButton(string label, Color textColor, Color buttonColor, Num.Vector2 size, string? tooltip = null)
    {
        return ColoredButton(label, textColor, buttonColor, tooltip, size, ButtonPadding);
    }

    public static bool ColoredButton(string label, Color textColor, Color buttonColor, string? tooltip, Num.Vector2 size, Num.Vector2 padding)
    {
        var (h, s, v) = ColorExt.RgbToHsv(buttonColor);
        var a = buttonColor.A / 255f;

        var normal = ColorExt.HsvToRgb(h, s * 0.8f, v * 0.6f) * a;
        var hovered = ColorExt.HsvToRgb(h, s * 0.9f, v * 0.7f) * a;
        var active = ColorExt.HsvToRgb(h, s * 1f, v * 0.8f) * a;
        var shadow = Color.Transparent;
        var border = ColorExt.HsvToRgb(h, s * 1f, v * 0.7f) * a;

        var (th, ts, tv) = ColorExt.RgbToHsv(textColor);
        var textActive = ColorExt.HsvToRgb(th, ts, tv * 2f);
        var textHovered = ColorExt.HsvToRgb(th, ts, tv * 1.8f);
        var textNormal = textColor * 0.95f;

        var wasHovered = ImGui.GetCurrentContext()->HoveredIdPreviousFrame == ImGui.GetID(label);
        var wasActive = ImGui.GetCurrentContext()->ActiveIdPreviousFrame == ImGui.GetID(label);
        var text = (wasActive, wasHovered) switch
        {
            (true, _) => textActive,
            (_, true) => textHovered,
            _ => textNormal
        };

        ImGui.PushStyleColor(ImGuiCol.Button, normal.ToNumerics());
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, hovered.ToNumerics());
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, active.ToNumerics());
        ImGui.PushStyleColor(ImGuiCol.BorderShadow, shadow.ToNumerics());
        ImGui.PushStyleColor(ImGuiCol.Border, border.ToNumerics());
        ImGui.PushStyleColor(ImGuiCol.Text, text.ToNumerics());
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, padding);
        var result = ImGui.Button(label, size);

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled | ImGuiHoveredFlags.DelayNormal) && tooltip != null)
        {
            var popupBg = ColorExt.HsvToRgb(h, s * 1f, v * 0.3f) * 0.8f;
            ImGui.PushStyleColor(ImGuiCol.PopupBg, popupBg.ToNumerics());
            ImGui.BeginTooltip();
            ImGui.Text(tooltip);
            ImGui.EndTooltip();
            ImGui.PopStyleColor();
        }

        ImGui.PopStyleColor(6);
        ImGui.PopStyleVar();
        return result;
    }

    public static bool ColorEdit(string label, ref Color color, Color? refColor = null, ImGuiColorEditFlags flags = ImGuiColorEditFlags.None)
    {
        var colorNum = color.ToNumerics();
        var valueChanged = false;
        var openPicker = false;

        ImGui.BeginGroup();
        ImGui.PushID(label);

        var borderColor = ColorExt.MultiplyAlpha(Color.White, 0.66f);
        ImGui.PushStyleColor(ImGuiCol.Border, borderColor.PackedValue);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0);

        var frameHeight = ImGui.GetFrameHeight();
        var colorButtonSize = new Num.Vector2(frameHeight * 1.2f, frameHeight);
        if (ImGui.ColorButton("##button", colorNum, flags, colorButtonSize))
        {
            openPicker = true;
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.DelayNormal))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        ImGui.PopStyleColor();
        ImGui.PopStyleVar();

        /*ImGui.SameLine();
        if (TextButton(label, string.Empty, textColor.ToColor()))
        {
            openPicker = true;
        }*/

        if (openPicker)
        {
            ImGui.OpenPopup("picker");
            var screenPos = ImGui.GetCursorScreenPos();
            ImGui.SetNextWindowPos(screenPos, default, default);
        }

        if (ImGui.BeginPopup("picker"))
        {
            var labelWidth = ImGui.CalcTextSize(label, true);
            if (labelWidth.X > 0)
            {
                RenderText(label, true);
                ImGui.NewLine();
            }

            ImGui.Spacing();
            ImGui.SetNextItemWidth(frameHeight * 12.0f);
            float* pRefColor = null;
            if (refColor != null)
            {
                var refVec4 = refColor.Value.ToNumerics();
                pRefColor = RefPtr(ref refVec4.X);
            }

            if (ImGui.ColorPicker4("##picker", RefPtr(ref colorNum), flags, pRefColor))
            {
                color = colorNum.ToColor();
                valueChanged = true;
            }

            ImGui.EndPopup();
        }

        ImGui.PopID();
        ImGui.EndGroup();

        return valueChanged;
    }

    public static bool TextButton(string text, string tooltip)
    {
        var color = GetStyleColor(ImGuiCol.Text);
        var size = ImGui.CalcTextSize(text);
        return TextButton(text, tooltip, color.PackedValue(), size);
    }

    public static bool TextButton(string text, string tooltip, uint color, Num.Vector2 size)
    {
        /*if (size.X == 0 || size.Y == 0)
            return false;

        var cursorPos = ImGui.GetCursorScreenPos();
        var result = ImGui.InvisibleButton(text, size);
        var isHovering = ImGui.IsItemHovered();

        if (isHovering && tooltip != string.Empty)
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(tooltip);
            ImGui.EndTooltip();
        }

        var dl = ImGui.GetWindowDrawList();
        dl->AddText(cursorPos, color, text);
        return result;*/

        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.PushStyleColor(ImGuiCol.Button, Color.Transparent.PackedValue);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Color.Transparent.PackedValue);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Color.Transparent.PackedValue);
        ImGui.PushStyleColor(ImGuiCol.BorderShadow, Color.Transparent.PackedValue);
        ImGui.PushStyleColor(ImGuiCol.Border, Color.Transparent.PackedValue);
        var style = ImGui.GetStyle();
        ImGui.PushStyleVar(ImGuiStyleVar.ButtonTextAlign, new Num.Vector2(0.5f, 0.5f));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Num.Vector2(0, style->FramePadding.Y));
        var result = ImGui.Button(text, size);
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(6);

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.DelayNormal) && tooltip != "")
        {
            ImGui.BeginTooltip();
            ImGui.Text(tooltip);
            ImGui.EndTooltip();
        }

        return result;
    }

    public static void DrawLabelWithCenteredText(string label, string text)
    {
        var width = ImGui.CalcItemWidth();
        var cursorPosX = ImGui.GetCursorPosX();
        var textWidth = ImGui.CalcTextSize(text).X;
        ImGui.SetCursorPosX(cursorPosX + (width - textWidth) * 0.5f);
        ImGui.TextColored(Color.DarkGray.ToNumerics(), text);
        ImGui.SameLine();
        ImGui.SetCursorPosX(cursorPosX + width + ImGui.GetStyle()->ItemInnerSpacing.X);
        ImGui.Text(label);
    }

    private static void DrawCollapsingHeaderBorder(Color color)
    {
        var drawList = ImGui.GetWindowDrawList();
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var padding = ImGui.GetStyle()->WindowPadding;
        max.X = min.X + ImGui.GetContentRegionAvail().X - 1;
        var packedColor = color.PackedValue;
        var borderMin = min - new Num.Vector2(padding.X * 0.5f - 1, 0);
        var borderMax = max + new Num.Vector2(padding.X * 0.5f + 1, 0);
        borderMin.X = MathF.Floor(borderMin.X);
        borderMax.X = MathF.Floor(borderMax.X);
        if (borderMax.X - borderMin.X > 0 && borderMax.Y - borderMin.Y > 0)
        {
            drawList->AddRect(
                borderMin,
                borderMax,
                packedColor,
                1f
            );
        }
    }

    public static void DrawCollapsableLeaf(ReadOnlySpan<char> header, Color headerColor)
    {
        // TODO (marpe): This is crashing for some reason...]
        var dl = ImGui.GetWindowDrawList();
        var avail = ImGui.GetContentRegionAvail();
        var c = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton(header, new Num.Vector2(avail.X, ImGui.GetFrameHeight() + 6));
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var (h, s, v) = ColorExt.RgbToHsv(headerColor);
        var color = ColorExt.HsvToRgb(h, s * 0.8f, v * 0.6f);
        var borderColor = ImGui.GetStyle()->Colors[(int)ImGuiCol.Border];
        AddRectFilledOutlined(dl, min, max, color, borderColor.ToColor());
        var padding = new Vector2(35, 4).ToNumerics();
        var textColor = ImGui.GetStyle()->Colors[(int)ImGuiCol.Text];
        dl->AddText(c + padding, textColor.ToColor().PackedValue, header);
    }

    public static void AddRectFilledOutlined(ImDrawList* self, Num.Vector2 min, Num.Vector2 max, Color fill,
        Color outline, float thickness = 1.0f)
    {
        self->AddRectFilled(min, max, fill.PackedValue);
        self->AddRect(min, max, outline.PackedValue, thickness);
    }

    public static void AddRect(ImDrawList* self, Vector2 min, Vector2 max, Color color, float thickness = 1.0f)
    {
        self->AddRect(
            min.ToNumerics(),
            max.ToNumerics(),
            color.PackedValue,
            0,
            ImDrawFlags.None,
            thickness
        );
    }

    public static void AddRectFilled(ImDrawList* self, Vector2 min, Vector2 max, Color color)
    {
        self->AddRectFilled(
            min.ToNumerics(),
            max.ToNumerics(),
            color.PackedValue
        );
    }

    public static ImFont* GetFont(ImGuiFont fontType)
    {
        return ((MyEditorMain)Shared.Game).ImGuiRenderer.GetFont(fontType);
    }

    /// <summary>
    /// Returns a vector where each component is >= minValue
    /// This is used to prevent calls to e.g InvisibleButton and DockBuilderSetNodeSize from blowing up 
    /// </summary>
    public static Num.Vector2 EnsureNotZero(this Num.Vector2 value, float minValue = 4.0f)
    {
        return new Num.Vector2(Math.Max(value.X, minValue), Math.Max(value.Y, minValue));
    }

    public static ReadOnlySpan<char> GetLabel(ReadOnlySpan<char> label)
    {
        for (var i = 0; i < label.Length; i++)
        {
            if (label[i] == '\0' || (i < label.Length - 1 && label[i] == '#' && label[i + 1] == '#'))
                return label.Slice(0, i);
        }

        return label;
    }

    public static string LabelPrefix(string label, int labelMaxWidth = -1)
    {
        var strippedLabel = GetLabel(label);
        if (strippedLabel.Length == 0)
            return label;

        // label
        ImGui.PushFont(((MyEditorMain)Shared.Game).ImGuiRenderer.GetFont(ImGuiFont.MediumBold));
        var textSize = ImGui.CalcTextSize(label, true);

        var itemWidth = ImGui.CalcItemWidth();
        var nextItemWidth = itemWidth;

        var defaultWidth = ImGui.GetCurrentContext()->CurrentWindow->ItemWidthDefault;
        if (Math.Abs(itemWidth - defaultWidth) < FLT_EPSILON)
            nextItemWidth = -FLT_MIN;

        if ((ImGui.GetCurrentContext()->NextItemData.Flags & ImGuiNextItemDataFlags.HasWidth) != 0)
        {
            nextItemWidth = ImGui.GetCurrentContext()->NextItemData.Width;
        }

        if (!_labelWidthStack.TryPeek(out var offset))
            offset = 0;

        var x = ImGui.GetCursorPosX();
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle()->Colors[(int)ImGuiCol.TextDisabled]);

        var frameHeight = ImGui.GetFrameHeight();
        var min = ImGui.GetCursorScreenPos();
        var itemInnerSpacingX = ImGui.GetStyle()->ItemInnerSpacing.X;
        float maxWidth;
        if (labelMaxWidth == -1)
        {
            if (offset != 0)
            {
                maxWidth = offset;
            }
            else
            {
                maxWidth = itemWidth;
            }
        }
        else
        {
            maxWidth = labelMaxWidth;
        }

        maxWidth -= itemInnerSpacingX;

        var labelWidth = Math.Min(textSize.X, maxWidth);
        var max = min + new Num.Vector2(labelWidth, frameHeight);
        ImGuiInternal.RenderTextClipped(min, max, label, &textSize, new Num.Vector2(0, 0.5f));

        ImGui.PopFont();
        ImGui.PopStyleColor();

        // button for label
        var buttonSize = max - min;
        ImGui.InvisibleButton(label, buttonSize.EnsureNotZero());

        if (textSize.X > maxWidth && ImGui.IsItemHovered(ImGuiHoveredFlags.DelayNormal))
        {
            ImGui.SetTooltip(label);
        }

        if (ImGui.IsItemClicked())
        {
            ImGui.SetKeyboardFocusHere();
        }

        ImGui.SameLine(offset, itemInnerSpacingX);
        ImGui.SetNextItemWidth(nextItemWidth);

        return "##" + label;
    }

    public static bool BeginCollapsingHeader(string header, Color color,
        ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.DefaultOpen, ImGuiFont font = ImGuiFont.Medium, string labelRight = "", bool hideHeader = false)
    {
        var labelWidthRatio = 0.4f;
        var labelWidth = (int)(ImGui.GetContentRegionAvail().X * labelWidthRatio);
        if (hideHeader)
        {
            ImGui.BeginGroup();
            ImGui.Spacing();
            Indent();
            PushLabelWidth(labelWidth);
            _colorStack.Push(Color.Transparent);
            return true;
        }

        var (h, s, v) = ColorExt.RgbToHsv(color);

        var (normal, hovered, active, border) = (
            ColorExt.HsvToRgb(h, s * 0.8f, v * 0.6f) * 0,
            ColorExt.HsvToRgb(h, s * 0.9f, v * 0.7f),
            ColorExt.HsvToRgb(h, s * 1f, v * 0.8f),
            ColorExt.HsvToRgb(h, s * 1f, v * 0.8f)
        );

        void PushStyles()
        {
            PushStyleColor(ImGuiCol.Header, normal);
            PushStyleColor(ImGuiCol.HeaderHovered, hovered);
            PushStyleColor(ImGuiCol.HeaderActive, active);
            PushStyleColor(ImGuiCol.Border, border);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, CollapsingHeaderFramePadding);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0);
            ImGui.PushFont(GetFont(font));
        }

        void PopStyles()
        {
            ImGui.PopStyleColor(4);
            ImGui.PopStyleVar(2);
            ImGui.PopFont();
        }

        var result = false;
        ImGui.BeginGroup();

        PushStyles();

        result = ImGui.CollapsingHeader(header, flags);

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();

        if (labelRight != "")
        {
            var labelRightSize = ImGui.CalcTextSize(labelRight);
            ImGui.SameLine();
            var cursorX = ImGui.GetCursorPosX();
            var labelRightX = ImGui.GetContentRegionMax().X - labelRightSize.X - ImGui.GetStyle()->FramePadding.X;
            if (labelRightX >= cursorX)
            {
                ImGui.SetCursorPosX(labelRightX);
                ImGui.Text(labelRight);
            }
        }

        PopStyles();

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.DelayNormal))
        {
            var popupBg = ColorExt.HsvToRgb(h, s * 1f, v * 0.3f) * 0.8f;
            ImGui.PushStyleColor(ImGuiCol.PopupBg, popupBg.ToNumerics());
            ImGui.PushStyleColor(ImGuiCol.Border, border.ToNumerics());
            ImGui.SetTooltip(header);
            ImGui.PopStyleColor(2);
        }

        if (result)
        {
            var dl = ImGui.GetWindowDrawList();
            dl->AddLine(
                new Num.Vector2(min.X, max.Y - 1),
                new Num.Vector2(max.X, max.Y - 1),
                border.PackedValue,
                1f
            );
            Indent();
            PushLabelWidth(labelWidth);
            _colorStack.Push(border);
        }
        else
        {
            ImGui.EndGroup();
            DrawCollapsingHeaderBorder(border);
        }

        return result;
    }

    public static void Indent()
    {
        var indentWidth = ImGui.GetStyle()->IndentSpacing * 0.4f;
        ImGui.Indent(indentWidth);
    }

    public static void Unindent()
    {
        var indentWidth = ImGui.GetStyle()->IndentSpacing * 0.4f;
        ImGui.Unindent(indentWidth);
    }

    public static void EndCollapsingHeader()
    {
        PopLabelWidth();
        Unindent();
        ImGui.Spacing();
        ImGui.EndGroup();
        var color = _colorStack.Pop();
        DrawCollapsingHeaderBorder(color);
    }

    public static void SeparatorText(string text)
    {
        var textDisabledColor = ImGui.GetStyle()->Colors[(int)ImGuiCol.TextDisabled].ToColor();
        SeparatorText(text, textDisabledColor, textDisabledColor);
    }

    public static void SeparatorText(string text, Color textColor)
    {
        var textDisabledColor = ImGui.GetStyle()->Colors[(int)ImGuiCol.TextDisabled].ToColor();
        SeparatorText(text, textColor, textDisabledColor);
    }

    public static void SeparatorText(string text, Color textColor, Color separatorColor)
    {
        var draw = ImGui.GetWindowDrawList();
        var halfLine = new Num.Vector2(0, ImGui.GetTextLineHeight() * 0.5f);
        draw->AddLine(
            ImGui.GetCursorScreenPos() + halfLine,
            ImGui.GetCursorScreenPos() + halfLine + new Num.Vector2(12, 0),
            separatorColor.PackedValue
        );
        ImGui.SetCursorPosX(ImGui.GetCursorPos().X + 20f);
        ImGui.PushFont(GetFont(ImGuiFont.MediumBold));
        ImGui.TextColored(textColor.ToNumerics(), text);
        ImGui.PopFont();
        ImGui.SameLine();
        draw->AddLine(
            ImGui.GetCursorScreenPos() + halfLine,
            ImGui.GetCursorScreenPos() + halfLine + new Num.Vector2(ImGui.GetWindowWidth() - ImGui.GetCursorPos().X, 0),
            separatorColor.PackedValue
        );
        ImGui.NewLine();
    }

    public static void ItemTooltip(string tooltip)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Num.Vector2(4, 4));
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(tooltip);
            ImGui.EndTooltip();
        }

        ImGui.PopStyleVar();
    }

    public static void MediumVerticalSpace()
    {
        ImGui.Dummy(new Num.Vector2(0, 10));
    }

    public static string StringFromPtr(byte* ptr)
    {
        var length = 0;
        while (ptr[length] != 0)
        {
            length += 1;
        }

        return Encoding.UTF8.GetString(ptr, length);
    }

    public static void PushStyleColor(ImGuiCol c, in Color color)
    {
        ImGui.PushStyleColor(c, color.ToNumerics());
    }

    public static Num.Vector4 GetStyleColor(ImGuiCol c)
    {
        return ImGui.GetStyle()->Colors[(int)c];
    }

    public static void RenderText(string text, bool hideTextAfterHash)
    {
        var pos = ImGui.GetCursorScreenPos();
        ImGuiInternal.RenderText(pos, text, hideTextAfterHash);
    }

    public static void AddRectDashed(ImDrawList* drawList, Num.Vector2 min, Num.Vector2 max, uint color, float thickness, int segmentLength,
        float lengthOfOnSegments = 0.5f, bool animate = true)
    {
        AddLineDashed(drawList, min, new Num.Vector2(max.X, min.Y), color, thickness, segmentLength, lengthOfOnSegments, animate);
        AddLineDashed(drawList, new Num.Vector2(max.X, min.Y), max, color, thickness, segmentLength, lengthOfOnSegments, animate);
        AddLineDashed(drawList, max, new Num.Vector2(min.X, max.Y), color, thickness, segmentLength, lengthOfOnSegments, animate);
        AddLineDashed(drawList, new Num.Vector2(min.X, max.Y), min, color, thickness, segmentLength, lengthOfOnSegments, animate);
    }

    public static void AddLineDashed(ImDrawList* drawList, Num.Vector2 start, Num.Vector2 end, uint color, float thickness, int segmentLength,
        float lengthOfOnSegments = 0.5f, bool animate = true, float animationSpeed = 2.0f)
    {
        var offset = (end - start);
        var length = offset.Length();
        if (MathF.IsNearZero(length))
            return;

        var dir = offset / length;
        var lineLength = segmentLength * lengthOfOnSegments;
        var t = animate ? Shared.Game.Time.TotalElapsedTime * animationSpeed : 0; // (float)ImGui.GetTime();
        var tt = (t - MathF.Floor(t)) - 1f;
        var initialOffset = segmentLength * tt;
        var p = start + dir * initialOffset;
        var traversedLength = initialOffset;

        if (initialOffset < 0)
        {
            var initialLength = lineLength + initialOffset;
            if (initialLength > 0)
                drawList->AddLine(start, start + initialLength * dir, color, thickness);
            p += dir * segmentLength;
            traversedLength += segmentLength;
        }

        for (; traversedLength + lineLength < length; traversedLength += segmentLength)
        {
            drawList->AddLine(p, p + lineLength * dir, color, thickness);
            p += dir * segmentLength;
        }

        var lengthLeft = length - traversedLength;
        if (lengthLeft > 0)
        {
            drawList->AddLine(p, p + lengthLeft * dir, color, thickness);
        }
    }

    /// <summary>
    /// from https://github.com/ocornut/imgui/issues/1901
    /// </summary>
    public static void Spinner(string label, float radius, int thickness, uint color, float speed = 0.5f)
    {
        var g = ImGui.GetCurrentContext();
        var style = g->Style;
        var id = ImGui.GetID(label);

        var pos = ImGui.GetCursorScreenPos();
        var size = new Num.Vector2(radius * 2, (radius + style.FramePadding.Y) * 2);

        var bb = new ImRect(pos, pos + size);
        ImGuiInternal.ItemSize(bb, style.FramePadding.Y);
        if (!ImGuiInternal.ItemAdd(bb, id))
            return;

        var drawList = ImGui.GetWindowDrawList();
        drawList->PathClear();

        var numSegments = 30;
        var start = (int)MathF.Abs(MathF.Sin((float)g->Time * 0.9f * speed) * (numSegments - 5));

        var aMin = MathHelper.TwoPi * start / numSegments;
        var aMax = MathHelper.TwoPi * ((float)numSegments - 3) / numSegments;

        var center = new Num.Vector2(pos.X + radius, pos.Y + radius + style.FramePadding.Y);

        for (var i = 0; i < numSegments; i++)
        {
            var a = aMin + i / (float)numSegments * (aMax - aMin);
            drawList->PathLineTo(
                new Num.Vector2(
                    center.X + MathF.Cos(a + (float)g->Time * 4 * speed) * radius,
                    center.Y + MathF.Sin(a + (float)g->Time * 4 * speed) * radius
                )
            );
        }

        drawList->PathStroke(color, ImDrawFlags.None, thickness);
    }

    public static void BufferingBar(string label, float value, Num.Vector2 sizeArg, uint bgCol, uint fgCol, float speed = 0.5f)
    {
        var id = ImGui.GetID(label);

        var pos = ImGui.GetCursorScreenPos();
        var size = sizeArg;
        size.X -= ImGui.GetStyle()->FramePadding.X * 2;

        var bb = new ImRect(pos, new Num.Vector2(pos.X + size.X, pos.Y + size.Y));
        ImGuiInternal.ItemSize(bb, ImGui.GetStyle()->FramePadding.Y);
        if (!ImGuiInternal.ItemAdd(bb, id))
            return;

        var circleStart = size.X * 0.7f;
        var circleEnd = size.X;
        var circleWidth = circleEnd - circleStart;

        var drawList = ImGui.GetWindowDrawList();
        drawList->AddRectFilled(bb.Min, new Num.Vector2(pos.X + circleStart, bb.Max.Y), bgCol);
        drawList->AddRectFilled(bb.Min, new Num.Vector2(pos.X + circleStart * value, bb.Max.Y), fgCol);

        var t = (float)ImGui.GetCurrentContext()->Time;
        var r = size.Y / 2;

        var a = speed * 0;
        var b = speed * 0.333f;
        var c = speed * 0.666f;

        var o1 = (circleWidth + r) * (t + a - speed * (int)((t + a) / speed)) / speed;
        var o2 = (circleWidth + r) * (t + b - speed * (int)((t + b) / speed)) / speed;
        var o3 = (circleWidth + r) * (t + c - speed * (int)((t + c) / speed)) / speed;

        drawList->AddCircleFilled(new Num.Vector2(pos.X + circleEnd - o1, bb.Min.Y + r), r, bgCol);
        drawList->AddCircleFilled(new Num.Vector2(pos.X + circleEnd - o2, bb.Min.Y + r), r, bgCol);
        drawList->AddCircleFilled(new Num.Vector2(pos.X + circleEnd - o3, bb.Min.Y + r), r, bgCol);
    }

    public struct ButtonColors
    {
        public Color Button;
        public Color Hovered;
        public Color Active;
        public Color Inactive;
        public Color Shadow;
        public Color BorderShadow;
        public Color Border;
    }

    public static ButtonColors GetButtonColors(Color color)
    {
        var (h, s, v) = ColorExt.RgbToHsv(color);
        return new ButtonColors()
        {
            Button = color,
            Hovered = ColorExt.HsvToRgb(h, s, v * 0.6f),
            Active = ColorExt.HsvToRgb(h, s, v * 0.7f),
            Inactive = ColorExt.HsvToRgb(h, s * 0.8f, v * 0.35f),
            Border = ColorExt.HsvToRgb(h, s * 0.5f, v * 0.1f)
        };
    }

    public static bool EnumButtons(string[] labels, int[] values, ref int value, bool isFlag, Color color, bool fullWidth = true, string[]? tooltips = null)
    {
        var result = false;
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Num.Vector2.Zero);

        var cursorScreenPos = ImGui.GetCursorScreenPos();

        var minSize = fullWidth ? 30f : FLT_MIN;

        for (var i = 0; i < labels.Length; i++)
        {
            var size = ImGui.CalcTextSize(labels[i]);
            minSize = Math.Max(minSize, size.X);
        }

        var framePaddingX = ImGui.GetStyle()->FramePadding.X;
        var minButtonSize = new Num.Vector2(minSize * (labels.Length + framePaddingX), 0);
        var avail = fullWidth ? ImGui.GetContentRegionAvail() : minButtonSize;

        var rightPadding = 0;
        var rowWidth = Math.Max(avail.X - rightPadding, minSize);

        var minItemsPerRow = (int)(rowWidth / minSize);

        var numRows = MathF.CeilToInt(labels.Length / (float)minItemsPerRow);
        var itemsPerRow = MathF.CeilToInt(labels.Length / (float)numRows);

        var enumRowHeight = ImGui.GetFrameHeightWithSpacing() + 2;

        var dl = ImGui.GetWindowDrawList();

        var btnColors = GetButtonColors(color);

        // var rounding = 0; // ImGui.GetStyle().FrameRounding;
        // dl->AddRectFilled(cursorScreenPos, cursorScreenPos + new Num.Vector2(rowWidth, enumRowHeight * numRows), btnColors.Inactive.PackedValue, rounding);

        var offsetX = 0f;
        for (var k = 0; k < labels.Length; k++)
        {
            var currRow = k / itemsPerRow;
            var indexInRow = k - (currRow * itemsPerRow);
            var itemsThisRow = Math.Min(itemsPerRow, labels.Length - currRow * itemsPerRow);
            var size = new Num.Vector2(Math.Max(minSize, (int)(rowWidth / itemsThisRow)), enumRowHeight);

            if (ImGui.InvisibleButton(labels[k], size))
            {
                value = isFlag ? value | values[k] : values[k];
                result = true;
            }

            var isHovered = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled | ImGuiHoveredFlags.DelayNormal);
            if (isHovered)
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(tooltips != null ? tooltips[k] : labels[k]);
                ImGui.EndTooltip();
            }

            var isCurrent = isFlag ? (value & values[k]) == values[k] : value == values[k];
            var btnColor = (isCurrent, isHovered, ImGui.IsItemActive()) switch
            {
                (_, _, true) => btnColors.Active,
                (true, _, _) => btnColors.Button,
                (false, true, _) => btnColors.Hovered,
                _ => btnColors.Inactive
            };
            var min = cursorScreenPos + new Num.Vector2(offsetX, 0);
            var max = min + size;

            dl->AddRectFilled(min, max, btnColor.PackedValue);

            var textColor = isCurrent || isHovered ? Color.White.PackedValue : GetStyleColor(ImGuiCol.TextDisabled).PackedValue();
            var textSize = ImGui.CalcTextSize(labels[k]);
            var center = min + size * 0.5f;
            var textPos = new Num.Vector2(center.X - textSize.X * 0.5f, min.Y + (size.Y - textSize.Y) * 0.5f);
            dl->AddText(textPos, textColor, labels[k]);

            if (indexInRow < itemsPerRow - 1 && k != labels.Length - 1)
            {
                offsetX += size.X;
                dl->AddLine(
                    new Num.Vector2(cursorScreenPos.X + offsetX - 1, cursorScreenPos.Y),
                    new Num.Vector2(cursorScreenPos.X + offsetX - 1, cursorScreenPos.Y + enumRowHeight),
                    btnColors.Border.PackedValue
                );
                ImGui.SameLine();
            }
            else
            {
                // new row
                offsetX = 0;
                cursorScreenPos = ImGui.GetCursorScreenPos();
                avail = fullWidth ? ImGui.GetContentRegionAvail() : minButtonSize;
                rowWidth = Math.Max(avail.X - rightPadding, minSize);
                dl->AddLine(
                    new Num.Vector2(cursorScreenPos.X, cursorScreenPos.Y - 1),
                    new Num.Vector2(cursorScreenPos.X + rowWidth, cursorScreenPos.Y - 1),
                    btnColors.Border.PackedValue
                );
            }
        }

        // dl->AddRect(rectMin, rectMax, btnColors.Border.PackedValue, rounding);

        ImGui.PopStyleVar();

        ImGui.Spacing();

        return result;
    }

    public static void FillWithStripes(ImDrawList* drawList, ImRect rect, uint stripesColor, float patternWidth = 16)
    {
        drawList->PushClipRect(rect.Min, rect.Max, true);
        var lineWidth = patternWidth / 2.7f;

        var height = rect.GetHeight();
        var stripeCount = (int)((rect.GetWidth() + height + 3 * lineWidth) / patternWidth);
        var position = rect.Min - new Num.Vector2(height + lineWidth, lineWidth);
        var offset = new Num.Vector2(height + 2 * lineWidth, height + 2 * lineWidth);

        for (var i = 0; i < stripeCount; i++)
        {
            drawList->AddLine(position, position + offset, stripesColor, lineWidth);
            position.X += patternWidth;
        }

        drawList->PopClipRect();
    }

    public static bool BeginWorkspaceWindow(string windowTitle, string dockspaceId, Action<uint> initializeLayoutCallback, bool* isOpen,
        ref ImGuiWindowClass windowClass, ImGuiDockNodeFlags dockSpaceFlags = ImGuiDockNodeFlags.None, bool forceRebuild = false, Action? drawContent = null)
    {
        ImGui.SetNextWindowSize(new Num.Vector2(1024, 768), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Num.Vector2(128, 128), new Num.Vector2(FLT_MAX, FLT_MAX));
        var windowFlags = ImGuiWindowFlags.NoCollapse; // |
        // ImGuiWindowFlags.NoTitleBar |
        // ImGuiWindowFlags.NoDecoration;
        var shouldDrawWindowContents = ImGui.Begin(windowTitle, isOpen, windowFlags);

        var dockspaceID = ImGui.GetID(dockspaceId);

        windowClass.ClassId = dockspaceID;
        windowClass.DockingAllowUnclassed = true;

        if (ImGuiInternal.DockBuilderGetNode(dockspaceID) == null || forceRebuild)
        {
            var dockFlags = ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_DockSpace |
                            ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoWindowMenuButton |
                            ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoCloseButton;
            ImGuiInternal.DockBuilderAddNode(dockspaceID, (ImGuiDockNodeFlags)dockFlags);
            var windowSize = ImGui.GetWindowSize();
            var size = new Num.Vector2(MathF.Max(4.0f, windowSize.X), MathF.Max(4.0f, windowSize.Y));
            ImGuiInternal.DockBuilderSetNodeSize(dockspaceID, size);
            //
            initializeLayoutCallback.Invoke(dockspaceID);
            //
            ImGuiInternal.DockBuilderFinish(dockspaceID);
        }

        dockSpaceFlags |= /*ImGuiDockNodeFlags.NoSplit |*/
            (ImGuiDockNodeFlags)ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_NoWindowMenuButton;
        // ImGuiDockNodeFlags.AutoHideTabBar |
        // (ImGuiDockNodeFlags)ImGuiDockNodeFlagsPrivate_.ImGuiDockNodeFlags_HiddenTabBar;

        ImGui.DockSpace(
            dockspaceID,
            ImGui.GetContentRegionAvail(),
            shouldDrawWindowContents ? dockSpaceFlags : ImGuiDockNodeFlags.KeepAliveOnly,
            RefPtr(ref windowClass)
        );

        drawContent?.Invoke();

        ImGui.End();

        return shouldDrawWindowContents;
    }

    public static void PrintVector(string label, Vector2 v)
    {
        var avail = ImGui.GetContentRegionAvail();
        ImGui.Text(label);
        ImGui.SameLine(0.6f * avail.X);
        ImGui.Text($"{v.X:0.##}");
        ImGui.SameLine(0.875f * avail.X);
        ImGui.Text($"{v.Y:0.##}");
    }

    public static bool PivotPointEditor(string label, ref double pivotX, ref double pivotY, float size, uint color)
    {
        if (ImGuiInternal.GetCurrentWindow()->SkipItems)
            return false;

        var pivotAnchors = new Num.Vector2[]
        {
            new(0, 0),
            new(0.5f, 0),
            new(1, 0),

            new(0, 0.5f),
            new(0.5f, 0.5f),
            new(1, 0.5f),

            new(0, 1),
            new(0.5f, 1),
            new(1, 1),
        };

        LabelPrefix(label);

        var result = false;

        var dl = ImGui.GetWindowDrawList();
        var cursor = ImGui.GetCursorScreenPos();
        // dl->AddRect(cursor, cursor + new Num.Vector2(size, size), Color.Red.PackedValue);
        var anchorRadius = MathF.Max(6, size / 8f);
        var rectTopLeft = cursor + new Num.Vector2(anchorRadius, anchorRadius);
        var rectSize = new Num.Vector2(size, size) - new Num.Vector2(anchorRadius, anchorRadius) * 2;
        dl->AddRectFilled(rectTopLeft, rectTopLeft + rectSize, color);
        dl->AddRect(rectTopLeft, rectTopLeft + rectSize, Color.White.PackedValue);
        for (var i = 0; i < pivotAnchors.Length; i++)
        {
            var anchorCenter = pivotAnchors[i] * rectSize;
            var isSelected = MathF.Approx((float)pivotX, pivotAnchors[i].X) && MathF.Approx((float)pivotY, pivotAnchors[i].Y);

            ImGui.SetCursorScreenPos(rectTopLeft + anchorCenter - Num.Vector2.One * anchorRadius);
            if (ImGui.InvisibleButton($"Anchor{i}", new Num.Vector2(anchorRadius * 2, anchorRadius * 2)))
            {
                pivotX = pivotAnchors[i].X;
                pivotY = pivotAnchors[i].Y;
                result = true;
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(pivotAnchors[i].ToString());
            }

            if (isSelected)
            {
                var fillColor = Color.White;
                var borderColor = Color.Blue;
                var scale = 1.25f;
                var innerScale = 0.5f * scale;
                dl->AddCircleFilled(rectTopLeft + anchorCenter, anchorRadius * scale, fillColor.PackedValue);
                dl->AddCircleFilled(rectTopLeft + anchorCenter, anchorRadius * innerScale, borderColor.PackedValue);
                dl->AddCircle(rectTopLeft + anchorCenter, anchorRadius * scale, borderColor.PackedValue);
            }
            else
            {
                var alpha = ImGui.IsItemHovered() ? 0.8f : 0.3f;
                var fillColor = Color.White.MultiplyAlpha(alpha);
                var borderColor = Color.Black.MultiplyAlpha(alpha);
                dl->AddCircleFilled(rectTopLeft + anchorCenter, anchorRadius, fillColor.PackedValue);
                dl->AddCircle(rectTopLeft + anchorCenter, anchorRadius, borderColor.PackedValue);
            }
        }

        return result;
    }

    public static void DrawGrid(ImDrawList* draw, int gridSize, Vector2 scale, Vector2 min, Vector2 max, Color color, Color axesColor, float thickness)
    {
        var dt = Vector2.One * gridSize / scale;

        var minSpaceBetweenLines = 10f;
        var xy0 = min + (max - min) * 0.5f;

        var lastLine = float.MinValue;
        for (var x = min.X - dt.X; x <= max.X + dt.X; x += dt.X)
        {
            if (x - lastLine < minSpaceBetweenLines)
                continue;
            draw->AddLine(
                new(x, min.Y - dt.Y),
                new(x, max.Y + dt.Y),
                color.PackedValue,
                thickness
            );
            lastLine = x;
        }

        lastLine = float.MinValue;
        for (var y = min.Y - dt.Y; y <= max.Y + dt.Y; y += dt.Y)
        {
            if (y - lastLine < minSpaceBetweenLines)
                continue;
            draw->AddLine(
                new(min.X - dt.X, y),
                new(max.X + dt.X, y),
                color.PackedValue,
                thickness
            );
            lastLine = y;
        }

        draw->AddLine(new(xy0.X, min.Y - dt.Y), new(xy0.X, max.Y + dt.Y), axesColor.PackedValue, 3f);
        draw->AddLine(new(min.X - dt.X, xy0.Y), new(max.X + dt.X, xy0.Y), axesColor.PackedValue, 3f);
    }

    public static void RectWithOutline(ImDrawList* dl, Num.Vector2 min, Num.Vector2 max, Color fillColor, Color outlineColor, float rounding = 4.0f)
    {
        dl->AddRectFilled(min, max, fillColor.PackedValue, rounding);
        dl->AddRect(min, max, outlineColor.PackedValue, rounding);
    }

    public static bool DrawTileSetIcon(string id, uint tileId, TileSetDef tileSetDef, Num.Vector2 iconPos, Num.Vector2 iconSize, bool drawOutline,
        Color outlineColor, float padding = 4f)
    {
        var dl = ImGui.GetWindowDrawList();

        var pad = new Num.Vector2(padding);
        if (drawOutline)
        {
            RectWithOutline(
                dl,
                iconPos,
                iconPos + iconSize + pad * 2,
                outlineColor.MultiplyAlpha(0.2f),
                outlineColor.MultiplyAlpha(0.6f),
                2f
            );
        }

        if (!drawOutline)
            FillWithStripes(dl, new ImRect(iconPos, iconPos + iconSize + pad * 2), Color.White.MultiplyAlpha(0.1f).PackedValue);

        var wasHovered = ImGui.GetCurrentContext()->HoveredIdPreviousFrame == ImGui.GetID(id);
        var transparent = Color.Transparent.ToNumerics();
        ImGui.PushStyleColor(ImGuiCol.Button, transparent);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, transparent);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, transparent);
        ImGui.PushStyleColor(ImGuiCol.BorderShadow, transparent);
        ImGui.PushStyleColor(ImGuiCol.Border,
            drawOutline ? transparent : (wasHovered ? Color.Yellow.ToNumerics() : Color.Yellow.MultiplyAlpha(0.33f).ToNumerics()));
        ImGui.PushStyleColor(ImGuiCol.Text, transparent);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Num.Vector2(padding));

        ImGui.SetCursorScreenPos(iconPos);

        var uvMin = Num.Vector2.Zero;
        var uvMax = Num.Vector2.One;
        if (SplitWindow.GetTileSetTexture(tileSetDef.Path, out var texture))
        {
            var tileSprite = LevelRenderer.GetTileSprite(texture, tileId, tileSetDef);
            uvMin = tileSprite.UV.TopLeft.ToNumerics();
            uvMax = tileSprite.UV.BottomRight.ToNumerics();
        }

        var result = ImGui.ImageButton(
            id,
            (void*)texture.Handle,
            iconSize,
            uvMin,
            uvMax,
            transparent,
            Color.White.ToNumerics()
        );

        ImGui.PopStyleColor(6);
        ImGui.PopStyleVar();

        return result;
    }

    public static void DrawCone(ImDrawList* dl, Num.Vector2 center, float coneAngle, float angle, float radius, Color fillColor, int numSegments = 64)
    {
        var numPoints = Math.Max(1, coneAngle / MathHelper.TwoPi * numSegments);
        var aStart = angle - coneAngle * 0.5f;
        var aEnd = angle + coneAngle * 0.5f;
        var deltaA = Math.Max(MathHelper.TwoPi / numSegments, (aEnd - aStart) / numPoints);
        dl->PathLineTo(center);
        dl->PathLineTo(center + new Num.Vector2(MathF.Cos(aStart), MathF.Sin(aStart)) * radius);
        for (var i = aStart + deltaA; i <= aEnd - deltaA; i += deltaA)
        {
            dl->PathLineTo(center + new Num.Vector2(MathF.Cos(i), MathF.Sin(i)) * radius);
        }

        dl->PathLineTo(center + new Num.Vector2(MathF.Cos(aEnd), MathF.Sin(aEnd)) * radius);

        dl->PathFillConvex(fillColor.PackedValue);
    }

    public static bool DrawSearchDialog(string name, string buttonLabel, ref bool isOpen, ref int selectedIndex, ReadOnlySpan<string> items,
        ref string searchPattern)
    {
        if (!isOpen)
            return false;

        ImGui.OpenPopup(name);

        var center = ImGui.GetMainViewport()->GetCenter();
        var vpSize = ImGui.GetMainViewport()->Size;
        var windowSize = new Num.Vector2(400, 0);
        ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Num.Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSizeConstraints(windowSize, new System.Numerics.Vector2(FLT_MAX, FLT_MAX));
        ImGui.SetNextWindowSize(new System.Numerics.Vector2(windowSize.X, windowSize.Y), ImGuiCond.FirstUseEver);

        var result = false;

        void PushStyles()
        {
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Num.Vector2(16, 16));
        }

        void PopStyles()
        {
            ImGui.PopStyleVar(1);
        }

        PushStyles();
        if (ImGui.BeginPopupModal(name, RefPtr(ref isOpen), ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoDocking))
        {
            PopStyles();
            var itemSelected = ComboWithFilter(
                "Filter",
                ref selectedIndex,
                ref searchPattern,
                items
            );

            if (itemSelected && !ImGui.IsPopupOpen("Filter"))
            {
                ImGui.SetKeyboardFocusHere();
            }

            ImGui.Spacing();

            result |= ColoredButton(
                buttonLabel,
                Color.White,
                Colors[0],
                null,
                new Num.Vector2(-FLT_MIN, 0),
                new Num.Vector2(12, 8)
            );

            if (ImGui.IsKeyPressed(ImGuiKey.Escape))
            {
                isOpen = false;
            }

            ImGui.EndPopup();
        }
        else
        {
            PopStyles();
        }

        return result;
    }

    private static void DrawComboButton(string popupLabel, int currentIdx, ReadOnlySpan<string> items, bool openOnAppear)
    {
        var style = ImGui.GetStyle();
        var previewValue = currentIdx >= 0 && currentIdx < items.Length ? items[currentIdx] : popupLabel;
        var buttonLabel = FontAwesome6.ChevronDown + " " + previewValue + "##ComboWithFilter_button";
        var buttonAlign = style->ButtonTextAlign.X;
        style->ButtonTextAlign.X = 0;
        var buttonSize = new Num.Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetFrameHeightWithSpacing());
        var bgColor = new Color(121, 121, 121, 255); // ImGui.GetStyle()->Colors[(int)ImGuiCol.FrameBgActive];
        if (ImGuiExt.ColoredButton(buttonLabel, Color.White, bgColor, buttonSize) || (openOnAppear && ImGui.IsWindowAppearing()))
        {
            ImGui.OpenPopup(popupLabel);
        }

        style->ButtonTextAlign.X = buttonAlign;
    }

    public static bool ComboWithFilter(string label, ref int currentItemIndex, ref string searchPattern, ReadOnlySpan<string> items,
        bool openOnAppear = true)
    {
        DrawComboButton(label, currentItemIndex, items, openOnAppear);

        void PushStyle()
        {
            var popupPos = new Num.Vector2(ImGui.GetItemRectMin().X, ImGui.GetItemRectMax().Y);
            ImGui.SetNextWindowPos(popupPos, ImGuiCond.Always, new Num.Vector2(0, 0));
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(ImGui.GetItemRectSize().X, 0));
            ImGui.SetNextWindowBgAlpha(1.0f);
            ImGui.PushStyleColor(ImGuiCol.Border, Color.Black.PackedValue);
            ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 0);
        }

        void PopStyle()
        {
            ImGui.PopStyleColor();
            ImGui.PopStyleVar(1);
        }

        var result = false;
        PushStyle();
        if (ImGui.BeginPopup(label, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoNavInputs))
        {
            PopStyle();

            result = FuzzySearchFilter(ref currentItemIndex, ref searchPattern, items);

            ImGui.EndPopup();
        }
        else
        {
            PopStyle();
        }

        return result;
    }

    private static bool FuzzySearchFilter(ref int currentItemIndex, ref string searchPattern, ReadOnlySpan<string> items)
    {
        var result = false;

        if (ImGui.IsWindowAppearing())
        {
            ImGui.SetKeyboardFocusHere();
        }

        ImGui.SetNextItemWidth(-MathF.Epsilon - 40f);
        var searchPatternBuffer = new ImGuiInputBuffer(searchPattern, 256);
        fixed (byte* searchData = searchPatternBuffer.Bytes)
        {
            if (ImGui.InputText("##ComboWithFilter_inputText", searchData, (nuint)searchPatternBuffer.MaxLength, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                result = true;
                ImGui.CloseCurrentPopup();
            }

            searchPattern = ImGuiExt.StringFromPtr(searchData);
        }

        searchPatternBuffer.Dispose();

        ImGui.SameLine();
        if (ImGui.Button(FontAwesome6.Xmark, Num.Vector2.Zero))
        {
            searchPattern = string.Empty;
        }

        ImGui.PushItemWidth(-MathF.Epsilon);

        var isFiltering = searchPattern.Length > 0;
        var filteredIndex = 0;

        List<(int index, int score)> itemScoreVector = new();
        if (isFiltering)
        {
            for (var i = 0; i < items.Length; i++)
            {
                var matched = FuzzySearch.fuzzy_match(searchPattern, items[i], out var score);
                if (matched)
                {
                    itemScoreVector.Add((i, score));
                }
            }

            itemScoreVector = itemScoreVector.OrderByDescending(match => match.score).ToList();

            for (var i = 0; i < itemScoreVector.Count; i++)
            {
                if (itemScoreVector[i].index == currentItemIndex)
                {
                    filteredIndex = i;
                    break;
                }
            }
        }

        var itemsToShow = isFiltering ? itemScoreVector.Count : items.Length;
        if (itemsToShow > 0)
        {
            var setScroll = false;
            if (ImGui.IsKeyPressed(ImGuiKey.DownArrow))
            {
                if (isFiltering)
                {
                    var nextIndex = Math.Min(filteredIndex + 1, itemScoreVector.Count - 1);
                    if (itemScoreVector.Count > 0)
                    {
                        currentItemIndex = itemScoreVector[nextIndex].index;
                    }
                }
                else
                {
                    currentItemIndex = Math.Min(currentItemIndex + 1, itemsToShow - 1);
                }

                setScroll = true;
                result = true;
            }
            else if (ImGui.IsKeyPressed(ImGuiKey.UpArrow))
            {
                if (isFiltering)
                {
                    var prevIndex = Math.Max(filteredIndex - 1, 0);
                    if (itemScoreVector.Count > 0)
                    {
                        currentItemIndex = itemScoreVector[prevIndex].index;
                    }
                }
                else
                {
                    currentItemIndex = Math.Max(currentItemIndex - 1, 0);
                }

                setScroll = true;
                result = true;
            }

            var maxSize = ImGui.GetMainViewport()->WorkSize.Y - ImGui.GetCursorScreenPos().Y;
            var contentHeight = (ImGui.GetFrameHeight() + ImGui.GetStyle()->FramePadding.Y) * itemsToShow;
            maxSize = Math.Min(maxSize, contentHeight);

            if (ImGui.BeginListBox("##ComboWithFilter_itemList", new Num.Vector2(0, maxSize)))
            {
                for (var i = 0; i < itemsToShow; i++)
                {
                    var itemIndex = isFiltering ? itemScoreVector[i].index : i;
                    ImGui.PushID(itemIndex);
                    var itemSelected = itemIndex == currentItemIndex;
                    // ImGui.PushStyleColor(ImGuiCol.FrameBg, Color.Transparent.ToNumerics());
                    if (ImGui.Selectable(items[itemIndex], itemSelected, ImGuiSelectableFlags.None, default))
                    {
                        currentItemIndex = itemIndex;
                        result = true;
                        ImGui.CloseCurrentPopup();
                    }
                    // ImGui.PopStyleColor();

                    if (itemSelected)
                    {
                        ImGui.SetItemDefaultFocus();
                        if (setScroll)
                        {
                            ImGui.SetScrollHereY();
                        }
                    }

                    ImGui.PopID();
                }

                ImGui.EndListBox();
            }
        }

        ImGui.PopItemWidth();
        return result;
    }

    public static bool ComboStep(string label, bool showPrevNextButtons, ref int currentIndex, string[] items)
    {
        var result = false;

        ImGui.BeginGroup();

        var comboWidth = ImGui.CalcItemWidth();

        ImGuiExt.LabelPrefix(label);

        if (showPrevNextButtons)
        {
            ImGui.BeginGroup();
            {
                ImGui.PushStyleColor(ImGuiCol.Button, Color.Transparent.PackedValue);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Color.Transparent.PackedValue);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, Color.Transparent.PackedValue);

                if (ImGui.Button(FontAwesome6.AngleLeft + "##Left" + label, default))
                {
                    currentIndex = (items.Length + currentIndex - 1) % items.Length;
                    result = true;
                }

                if (ImGui.IsItemHovered(ImGuiHoveredFlags.DelayNormal))
                    ImGui.SetTooltip("Previous");

                ImGui.SameLine();
                if (ImGui.Button(FontAwesome6.AngleRight + "##Right" + label, default))
                {
                    currentIndex = (items.Length + currentIndex + 1) % items.Length;
                    result = true;
                }

                if (ImGui.IsItemHovered(ImGuiHoveredFlags.DelayNormal))
                    ImGui.SetTooltip("Next");

                ImGui.PopStyleColor(3);
            }

            ImGui.EndGroup();
            comboWidth -= (ImGui.GetItemRectSize().X + ImGui.GetStyle()->ItemInnerSpacing.X);
            ImGui.SameLine();
        }

        ImGui.SetNextItemWidth(comboWidth);
        if (ImGui.BeginCombo("##" + label, items[currentIndex]))
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

        ImGui.EndGroup();

        return result;
    }
}

public ref struct ImGuiInputBuffer
{
    private byte[] _bufferArray;
    public Span<byte> Bytes => _bufferArray;
    public readonly int MaxLength;
    public int Length;

    public ImGuiInputBuffer(ReadOnlySpan<char> str, int maxMaxLength)
    {
        _bufferArray = ArrayPool<byte>.Shared.Rent(maxMaxLength);
        var encodedBytesCount = Encoding.UTF8.GetBytes(str, Bytes);
        Bytes[encodedBytesCount] = 0;
        Length = encodedBytesCount;
        MaxLength = maxMaxLength;
    }

    public void Dispose()
    {
        ArrayPool<byte>.Shared.Return(_bufferArray);
    }

    public override string ToString() => Encoding.UTF8.GetString(Bytes.Slice(0, Length));
}
