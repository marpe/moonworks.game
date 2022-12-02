using System.Buffers;
using System.Globalization;
using Mochi.DearImGui;

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


    public static bool DrawXy(string label, ref Num.Vector2 value, string xLabel = "X", string yLabel = "Y", float step = 1f,
        float min = 0f, float max = 0f, string format = "%g")
    {
        ImGui.PushID(label);
        var itemWidth = ImGui.CalcItemWidth();
        var cursorX = ImGui.GetCursorPosX();
        var itemInnerSpacing = ImGui.GetStyle()->ItemInnerSpacing.X;
        var itemSpacing = ImGui.GetStyle()->ItemSpacing.X;
        var isEdited = false;
        var isHovering = false;
        ImGui.PushItemWidth(itemWidth * 0.5f - itemSpacing * 0.5f);
        isEdited |= DrawScalarButton(xLabel, "##x_label", ref value.X, step, min, max, format);
        ImGui.SameLine();
        isEdited |= DrawScalarButton(yLabel, "##y_label", ref value.Y, step, min, max, format);
        ImGui.PopItemWidth();

        ImGui.SameLine();
        ImGui.SetCursorPosX(cursorX + itemWidth + itemInnerSpacing);
        var labelPos = ImGui.GetCursorPos();
        var labelColor = ImGui.GetStyle()->Colors[(int)ImGuiCol.Text];
        ImGui.PushStyleColor(ImGuiCol.Text, labelColor);
        ImGui.TextUnformatted(label);
        ImGui.PopStyleColor();

        ImGui.SetCursorPos(labelPos);
        if (DrawCopyPasteMenu(ref value))
        {
            isEdited = true;
        }

        ImGui.PopID();

        return isEdited;
    }

    private static bool DrawScalarButton(string label, string imguiLabel, ref float value, float step = 1f, float min = 0f, float max = 0f,
        string format = "%g")
    {
        var (textColor, backgroundColor) = label switch
        {
            "X" => (0xff8888ffu, 0x0u), // 0xff222266u),
            "Y" => (0xff88ff88u, 0x0u), // 0xff226622u),
            "Z" => (0xffff8888u, 0x0u), // 0xff662222u),
            _ => (0xff7f7f7f, 0x000000ffu), // 0xffd3d3d3 ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
        };
        ImGui.PushStyleColor(ImGuiCol.Text, textColor);
        ImGui.PushStyleColor(ImGuiCol.Button, backgroundColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, backgroundColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, backgroundColor);
        ImGui.PushStyleColor(ImGuiCol.BorderShadow, 0x0u);
        ImGui.PushStyleColor(ImGuiCol.Border, backgroundColor);
        var style = ImGui.GetStyle();
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Num.Vector2(0, style->FramePadding.Y));
        ImGui.Button(label, default);
        ImGui.PopStyleVar();
        ImGui.SameLine();
        var buttonSize = ImGui.GetItemRectSize();
        ImGui.PopStyleColor(6);
        var itemWidth = ImGui.CalcItemWidth();
        ImGui.PushItemWidth(itemWidth - (buttonSize.X + style->ItemSpacing.X));
        var result = ImGui.DragFloat(imguiLabel, RefPtr(ref value), step, min, max, format);
        ImGui.PopItemWidth();
        return result;
    }


    private static bool DrawCopyPasteMenu(ref Num.Vector2 value)
    {
        var labelSize = ImGui.GetItemRectSize();
        var frameHeight = ImGui.GetFrameHeight();

        ImGui.InvisibleButton("##invis_menu_btn", new Num.Vector2(labelSize.X, frameHeight));
        ImGui.OpenPopupOnItemClick("context");

        var result = false;
        if (ImGui.BeginPopup("context"))
        {
            if (ImGui.Selectable(FontAwesome6.Copy + " Copy", false, ImGuiSelectableFlags.None, default))
            {
                SetVectorInClipboard(value);
            }

            if (ImGui.Selectable(FontAwesome6.Paste + " Paste", false, ImGuiSelectableFlags.None, default))
            {
                if (ParseVectorFromClipboard(out var v))
                {
                    result = true;
                    value = v;
                }
            }

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

    public static bool ColoredButton(string label, Color color, string? tooltip = null)
    {
        return ColoredButton(label, color, tooltip, Vector2.Zero, ButtonPadding);
    }

    public static bool ColoredButton(string label, Color color, Vector2 size, string? tooltip = null)
    {
        return ColoredButton(label, color, tooltip, size, ButtonPadding);
    }

    public static bool ColoredButton(string label, Color color, string? tooltip, Vector2 size, Vector2 padding)
    {
        var (h, s, v) = ColorExt.RgbToHsv(color);
        ImGui.PushStyleColor(ImGuiCol.Button, ColorExt.HsvToRgb(h, s * 0.8f, v * 0.6f).ToNumerics());
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ColorExt.HsvToRgb(h, s * 0.9f, v * 0.7f).ToNumerics());
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, ColorExt.HsvToRgb(h, s * 1f, v * 0.8f).ToNumerics());
        ImGui.PushStyleColor(ImGuiCol.BorderShadow, Color.Transparent.ToNumerics());
        ImGui.PushStyleColor(ImGuiCol.Border, ColorExt.HsvToRgb(h, s * 1f, v * 0.7f).ToNumerics());
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, padding.ToNumerics());
        var result = ImGui.Button(label, size.ToNumerics());

        if (ImGui.IsItemHovered() && tooltip != null)
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(tooltip);
            ImGui.EndTooltip();
        }

        ImGui.PopStyleColor(5);
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

        if (ImGui.IsItemHovered())
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
        var color = ImGui.GetStyle()->Colors[(int)ImGuiCol.Text];
        return TextButton(text, tooltip, color.ToColor());
    }

    public static bool TextButton(string text, string tooltip, Color textColor)
    {
        var size = ImGui.CalcTextSize(text);
        var cursorPos = ImGui.GetCursorScreenPos();
        var result = ImGui.InvisibleButton(text, size);
        var isHovering = ImGui.IsItemHovered();
        if (isHovering)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (tooltip != string.Empty)
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(tooltip);
                ImGui.EndTooltip();
            }
        }

        var dl = ImGui.GetWindowDrawList();
        var yPadding = ImGui.GetStyle()->FramePadding.Y;
        dl->AddText(cursorPos + new Num.Vector2(0, yPadding), textColor.PackedValue, text);
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
        max.X = min.X + ImGui.GetContentRegionAvail().X;
        // var color = ImGui.GetColorU32(ImGuiCol.FrameBg);
        var packedColor = color.PackedValue;
        drawList->AddRect(
            min - new Num.Vector2(padding.X * 0.5f - 1.0f, 0),
            max + new Num.Vector2(padding.X * 0.5f, 0),
            packedColor
        );
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

    public static string LabelPrefix(string label, bool preserveLabel = false)
    {
        if (label.StartsWith("##"))
        {
            ImGui.SetNextItemWidth(-1);
            return label;
        }
        
        var width = ImGui.CalcItemWidth();
        float x = ImGui.GetCursorPosX();
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle()->Colors[(int)ImGuiCol.TextDisabled]);
        ImGui.PushFont(((MyEditorMain)Shared.Game).ImGuiRenderer.GetFont(ImGuiFont.MediumBold));
        ImGui.Text(label);
        ImGui.PopFont();
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.SetCursorPosX(x + width * 0.7f + ImGui.GetStyle()->ItemInnerSpacing.X);
        ImGui.SetNextItemWidth(-1);

        return preserveLabel ? label : "##" + label;
    }

    public static bool BeginCollapsingHeader(string header, Color color,
        ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.DefaultOpen, ImGuiFont font = ImGuiFont.Medium, bool hideHeader = false)
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
            ColorExt.HsvToRgb(h, s * 0.8f, v * 0.6f),
            ColorExt.HsvToRgb(h, s * 0.9f, v * 0.7f),
            ColorExt.HsvToRgb(h, s * 1f, v * 0.8f),
            GetStyleColor(ImGuiCol.Border).ToColor()
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

        ImGui.BeginGroup();

        PushStyles();
        var id = header;
        var result = ImGui.CollapsingHeader(id, flags);
        PopStyles();

        if (result)
        {
            Indent();
            ImGui.PushItemWidth(ImGui.GetWindowWidth() * 0.6f);
            _colorStack.Push(normal);
        }
        else
        {
            ImGui.EndGroup();
            DrawCollapsingHeaderBorder(normal);
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
