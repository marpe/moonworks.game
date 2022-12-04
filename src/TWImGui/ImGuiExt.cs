using System.Buffers;
using System.Globalization;
using Mochi.DearImGui;
using Mochi.DearImGui.Internal;

namespace MyGame.TWImGui;

public static unsafe class ImGuiExt
{
    public const float FLT_MIN = 1.175494351e-38F;
    public const float FLT_MAX = 3.402823466e+38F;

    public const ImGuiTableFlags DefaultTableFlags = ImGuiTableFlags.Borders | ImGuiTableFlags.BordersOuter |
                                                     ImGuiTableFlags.Hideable | ImGuiTableFlags.Resizable |
                                                     ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.RowBg |
                                                     ImGuiTableFlags.NoPadOuterX;

    [CVar("imgui.debug", "Toggle inspector debug information")]
    public static bool DebugInspectors = false;

    public static Color CheckboxBorderColor = new(92, 92, 92);

    private static readonly Dictionary<uint, bool> _openFoldouts = new();

    private static readonly Stack<Color> _colorStack = new();
    public static Vector2 ButtonPadding => new(6f, 4f);

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
        if (!_openFoldouts.ContainsKey(id))
        {
            _openFoldouts.Add(id, false);
        }

        var dl = ImGui.GetWindowDrawList();

        var avail = ImGui.GetContentRegionAvail();
        var size = new Num.Vector2(avail.X, 20);
        var cursorStart = ImGui.GetCursorScreenPos();
        if (ImGui.InvisibleButton(label, size))
        {
            _openFoldouts[id] = !_openFoldouts[id];
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

        dl->AddRectFilled(cursorStart, cursorEnd, backgroundColor.PackedValue);

        var padding = new Num.Vector2(0, (size.Y - ImGui.GetTextLineHeight()) * 0.5f);
        var textDisabledColor = ImGui.GetColorU32(ImGuiCol.TextDisabled);
        dl->AddText(cursorStart + padding, textDisabledColor, _openFoldouts[id] ? FontAwesome6.ChevronDown : FontAwesome6.ChevronRight);
        dl->AddText(cursorStart + padding + new Num.Vector2(labelOffsetX, 0), textDisabledColor, label);

        return _openFoldouts[id];
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


    public static bool DrawXy(string label, ref Num.Vector2 value, string xLabel = "X", string xTooltip = "", string yLabel = "Y", string yTooltip = "",
        float step = 1f,
        float min = 0f, float max = 0f, string format = "%g")
    {
        ImGui.PushID(label);
        ImGui.BeginGroup();

        var itemWidth = ImGui.CalcItemWidth();
        var itemInnerSpacing = ImGui.GetStyle()->ItemInnerSpacing.X;

        var labelSize = ImGui.CalcTextSize(label, true);
        if (labelSize.X > 0)
        {
            var cursorX = ImGui.GetCursorPosX();
            ImGui.PushFont(GetFont(ImGuiFont.MediumBold));
            ImGui.PushStyleColor(ImGuiCol.Text, GetStyleColor(ImGuiCol.TextDisabled).PackedValue());
            ImGui.Text(label);
            ImGui.PopStyleColor();
            ImGui.PopFont();
            ImGui.SameLine();
            ImGui.SetCursorPosX(cursorX + itemWidth * 0.7f + itemInnerSpacing);
        }

        var isEdited = false;

        var inputWidth = ImGui.GetContentRegionAvail().X / 2;
        isEdited |= DrawScalarButton(xLabel, xTooltip, "##x", ref value.X, inputWidth - itemInnerSpacing, step, min, max, format);
        ImGui.SameLine();
        isEdited |= DrawScalarButton(yLabel, yTooltip, "##y", ref value.Y, inputWidth, step, min, max, format);

        ImGui.EndGroup();
        isEdited |= DrawCopyPasteMenu(ref value);
        ImGui.PopID();

        return isEdited;
    }

    private static bool DrawScalarButton(string label, string tooltip, string valueLabel, ref float value, float width, float step = 1f, float min = 0f,
        float max = 0f,
        string format = "%g")
    {
        var textColor = label switch
        {
            "X" => 0xff8888ffu,
            "Y" => 0xff88ff88u,
            "Z" => 0xffff8888u,
            _ => 0xff7f7f7fu,
        };

        var textWidth = Math.Max(ImGui.CalcTextSize(label).X, 16);
        if (TextButton(label, tooltip, textColor, new Num.Vector2(textWidth, ImGui.GetTextLineHeight())))
        {
            ImGui.SetKeyboardFocusHere(0);
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(width - textWidth - ImGui.GetStyle()->ItemInnerSpacing.X);
        return ImGui.DragFloat(valueLabel, RefPtr(ref value), step, min, max, format);
    }


    private static bool DrawCopyPasteMenu(ref Num.Vector2 value)
    {
        var result = false;
        if (ImGui.BeginPopupContextItem("ContextMenu"))
        {
            if (ImGui.Selectable(FontAwesome6.Copy + " Copy", false, ImGuiSelectableFlags.None, default))
            {
                SetVectorInClipboard(value);
            }

            result |= ImGui.Selectable(FontAwesome6.Paste + " Paste", false, ImGuiSelectableFlags.None, default) &&
                      ParseVectorFromClipboard(out value);

            ImGui.EndPopup();
        }

        return result;
    }

    public static void SetVectorInClipboard(Num.Vector2 v)
    {
        var str = $"{v.X.ToString("F0", CultureInfo.InvariantCulture)},{v.Y.ToString("F0", CultureInfo.InvariantCulture)}";
        SDL.SDL_SetClipboardText(str);
    }

    public static bool ParseVectorFromClipboard(out Num.Vector2 vector)
    {
        var clipboard = SDL.SDL_GetClipboardText();
        var split = clipboard.Split(',');
        if (split.Length != 2)
        {
            vector = Num.Vector2.Zero;
        }

        var _x = float.TryParse(split[0], out var x);
        var _y = float.TryParse(split[1], out var y);
        vector = new Num.Vector2(_x ? x : 0, _y ? y : 0);
        return _x && _y;
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
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, Num.Vector2.Zero);
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

    public static bool ColoredButton(string label, Color buttonColor, string? tooltip = null)
    {
        return ColoredButton(label, buttonColor, Vector2.Zero, tooltip);
    }

    public static bool ColoredButton(string label, Color buttonColor, Vector2 size, string? tooltip = null)
    {
        var text = ImGui.GetStyle()->Colors[(int)ImGuiCol.Text];
        return ColoredButton(label, text.ToColor(), buttonColor, tooltip, size, ButtonPadding);
    }

    public static bool ColoredButton(string label, Color textColor, Color buttonColor, string? tooltip = null)
    {
        return ColoredButton(label, textColor, buttonColor, tooltip, Vector2.Zero, ButtonPadding);
    }

    public static bool ColoredButton(string label, Color textColor, Color buttonColor, Vector2 size, string? tooltip = null)
    {
        return ColoredButton(label, textColor, buttonColor, tooltip, size, ButtonPadding);
    }

    public static bool ColoredButton(string label, Color textColor, Color buttonColor, string? tooltip, Vector2 size, Vector2 padding)
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
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, padding.ToNumerics());
        var result = ImGui.Button(label, size.ToNumerics());

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

    public static bool ColorEdit(string label, ref Color color)
    {
        var colorNum = color.ToNumerics();
        var valueChanged = false;
        var openPicker = false;

        ImGui.BeginGroup();
        ImGui.PushID(label);

        ImGui.PushStyleColor(ImGuiCol.Border, CheckboxBorderColor.PackedValue);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0);

        var frameHeight = ImGui.GetFrameHeight();
        var colorButtonSize = new Num.Vector2(frameHeight * 2.0f, frameHeight);
        if (ImGui.ColorButton("##button", colorNum, ImGuiColorEditFlags.None, colorButtonSize))
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
            ImGui.Text(label);

            ImGui.Spacing();
            ImGui.SetNextItemWidth(frameHeight * 12.0f);
            if (ImGui.ColorPicker4("##picker", RefPtr(ref colorNum)))
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
        ImGui.PushStyleVar(ImGuiStyleVar.ButtonTextAlign, new Num.Vector2(0, 0.5f));
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
            var yAdjust = new Num.Vector2(0, ImGui.GetFrameHeightWithSpacing() - 6);
            drawList->AddLine(
                borderMin + yAdjust,
                new Num.Vector2(borderMax.X, borderMin.Y) + yAdjust,
                ColorExt.MultiplyAlpha(color, 0.8f).PackedValue,
                1f
            );
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
        dl->AddRectFilledOutlined(min, max, color, borderColor.ToColor());
        var padding = new Vector2(35, 4).ToNumerics();
        var textColor = ImGui.GetStyle()->Colors[(int)ImGuiCol.Text];
        dl->AddText(c + padding, textColor.ToColor().PackedValue, header);
    }

    public static void AddRectFilledOutlined(this ImDrawList self, Num.Vector2 min, Num.Vector2 max, Color fill,
        Color outline, float thickness = 1.0f)
    {
        self.AddRectFilled(min, max, fill.PackedValue);
        self.AddRect(min, max, outline.PackedValue, thickness);
    }

    public static void AddRect(this ImDrawList self, Vector2 min, Vector2 max, Color color, float thickness = 1.0f)
    {
        self.AddRect(
            min.ToNumerics(),
            max.ToNumerics(),
            color.PackedValue,
            0,
            ImDrawFlags.None,
            thickness
        );
    }

    public static void AddRectFilled(this ImDrawList self, Vector2 min, Vector2 max, Color color)
    {
        self.AddRectFilled(
            min.ToNumerics(),
            max.ToNumerics(),
            color.PackedValue
        );
    }

    public static ImFont* GetFont(ImGuiFont fontType)
    {
        return ((MyEditorMain)Shared.Game).ImGuiRenderer.GetFont(fontType);
    }

    public static string LabelPrefix(string label, bool preserveLabel = false)
    {
        if (label.StartsWith("##"))
        {
            ImGui.SetNextItemWidth(-1);
            return label;
        }

        var itemWidth = ImGui.CalcItemWidth();
        var x = ImGui.GetCursorPosX();
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle()->Colors[(int)ImGuiCol.TextDisabled]);
        ImGui.PushFont(((MyEditorMain)Shared.Game).ImGuiRenderer.GetFont(ImGuiFont.MediumBold));
        var textSize = ImGui.CalcTextSize(label);
        var min = ImGui.GetCursorScreenPos();
        var labelWidth = itemWidth * 0.7f + ImGui.GetStyle()->ItemInnerSpacing.X;
        var max = min + new Num.Vector2(Math.Min(textSize.X, labelWidth), textSize.Y);
        ImGuiInternal.RenderTextClipped(min, max, label, &textSize, Num.Vector2.Zero);
        ImGui.PopFont();
        ImGui.PopStyleColor();
        var buttonSize = max - min;
        if (buttonSize.X > 0 && buttonSize.Y > 0)
        {
            ImGui.InvisibleButton(label, buttonSize, ImGuiButtonFlags.None);

            if (labelWidth < textSize.X && ImGui.IsItemHovered(ImGuiHoveredFlags.DelayNormal))
            {
                ImGui.SetTooltip(label);
            }

            if (ImGui.IsItemClicked())
            {
                ImGui.SetKeyboardFocusHere();
            }
        
            ImGui.SameLine();    
        }
        ImGui.SetCursorPosX(x + itemWidth * 0.7f + ImGui.GetStyle()->ItemInnerSpacing.X);
        ImGui.SetNextItemWidth(-1);

        return preserveLabel ? label : "##" + label;
    }

    public static bool BeginCollapsingHeader(string header, Color color,
        ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.DefaultOpen, ImGuiFont font = ImGuiFont.Medium, string labelRight = "", bool hideHeader = false)
    {
        if (hideHeader)
        {
            ImGui.BeginGroup();
            ImGui.Spacing();
            Indent();
            ImGui.PushItemWidth(ImGui.GetWindowWidth() * 0.6f);
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

        var framePadding = new Num.Vector2(6, 4);

        void PushStyles()
        {
            PushStyleColor(ImGuiCol.Header, normal);
            PushStyleColor(ImGuiCol.HeaderHovered, hovered);
            PushStyleColor(ImGuiCol.HeaderActive, active);
            PushStyleColor(ImGuiCol.Border, border);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, framePadding);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0);
            ImGui.PushFont(((MyEditorMain)Shared.Game).ImGuiRenderer.GetFont(font));
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
        ImGui.BeginGroup();
        {
            result = ImGui.CollapsingHeader(header, flags);

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
        }
        ImGui.EndGroup();
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
            Indent();
            ImGui.PushItemWidth(ImGui.GetWindowWidth() * 0.6f);
            _colorStack.Push(border);
        }
        else
        {
            ImGui.EndGroup();
            // DrawCollapsingHeaderBorder(border);
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
        ImGui.PopItemWidth();
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
        ImGui.TextColored(textColor.ToNumerics(), text);
        ImGui.SameLine();
        draw->AddLine(
            ImGui.GetCursorScreenPos() + halfLine,
            ImGui.GetCursorScreenPos() + halfLine + new Num.Vector2(ImGui.GetWindowWidth() - ImGui.GetCursorPos().X, 0),
            separatorColor.PackedValue
        );
        ImGui.NewLine();
    }

    public static void ItemTooltip(ReadOnlySpan<char> tooltip)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Num.Vector2(4, 4));
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(tooltip.ToString());
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
        float lengthOfOnSegments = 0.5f)
    {
        AddLineDashed(drawList, min, new Num.Vector2(max.X, min.Y), color, thickness, segmentLength, lengthOfOnSegments);
        AddLineDashed(drawList, new Num.Vector2(max.X, min.Y), max, color, thickness, segmentLength, lengthOfOnSegments);
        AddLineDashed(drawList, max, new Num.Vector2(min.X, max.Y), color, thickness, segmentLength, lengthOfOnSegments);
        AddLineDashed(drawList, new Num.Vector2(min.X, max.Y), min, color, thickness, segmentLength, lengthOfOnSegments);
    }

    public static void AddLineDashed(ImDrawList* drawList, Num.Vector2 start, Num.Vector2 end, uint color, float thickness, int segmentLength,
        float lengthOfOnSegments = 0.5f)
    {
        var offset = (end - start);
        var length = offset.Length();
        var numSegments = (int)(length / segmentLength);
        var dir = offset / length;
        int i;
        var p = start;
        for (i = 0; i < numSegments; i++)
        {
            drawList->AddLine(p, p + dir * segmentLength * lengthOfOnSegments, color, thickness);
            p += dir * segmentLength;
        }
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
