using System.Diagnostics.CodeAnalysis;
using Mochi.DearImGui;

namespace MyGame.Coroutines;

[CustomInspector<CoroutineInspector>]
public class CoroutineManager : IDisposable
{
    private readonly List<Coroutine> _routines = new();
    private readonly List<Coroutine> _routinesToAddNextFrame = new();
    private readonly List<Coroutine> _routinesToUpdate = new();
    public bool IsDisposed { get; private set; }

    public Coroutine StartCoroutine(IEnumerator enumerator, float deltaSeconds = 0, bool runNextFrame = true,
        [CallerArgumentExpression(nameof(enumerator))]
        string name = "")
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(CoroutineManager));

        var coroutine = new Coroutine(enumerator, name);
        if (runNextFrame)
        {
            _routinesToAddNextFrame.Add(coroutine);
        }
        else
        {
            coroutine.Tick(deltaSeconds);
            if (!coroutine.IsDone)
                _routinesToAddNextFrame.Add(coroutine);
        }

        return coroutine;
    }

    public void StopAll()
    {
        foreach (var coroutine in _routines)
        {
            coroutine.Stop();
        }

        foreach (var coroutine in _routinesToAddNextFrame)
        {
            coroutine.Stop();
        }

        _routinesToAddNextFrame.Clear();
    }

    public void Dispose()
    {
        if (IsDisposed)
            return;

        _routines.Clear();
        _routinesToUpdate.Clear();
        _routinesToAddNextFrame.Clear();

        IsDisposed = true;
        GC.SuppressFinalize(this);
    }

    public void Update(float deltaSeconds)
    {
        if (IsDisposed)
            throw new ObjectDisposedException(nameof(CoroutineManager));

        _routinesToUpdate.Clear();
        _routinesToUpdate.AddRange(_routines);

        for (var i = 0; i < _routinesToUpdate.Count; i++)
        {
            var coroutine = _routinesToUpdate[i];
            coroutine.Tick(deltaSeconds);
            if (coroutine.IsDone)
                _routines.Remove(coroutine);
        }

        _routines.AddRange(_routinesToAddNextFrame);
        _routinesToAddNextFrame.Clear();
    }

    public class CoroutineInspector : IInspector, IInspectorWithTarget, IInspectorWithType, IInspectorWithMemberInfo
    {
        public string? InspectorOrder { get; set; }

        private Type? _type;
        private MemberInfo? _memberInfo;
        private object? _target;
        private bool _isInitialized;
        private CoroutineManager? _coroutineManager;

        public void SetType(Type type)
        {
            _type = type;
        }

        public void SetTarget(object target)
        {
            _target = target;
        }

        public void SetMemberInfo(MemberInfo memberInfo)
        {
            _memberInfo = memberInfo;
        }

        [MemberNotNull(nameof(_coroutineManager))]
        private void Initialize()
        {
            var actualTarget = _memberInfo switch
            {
                FieldInfo fieldInfo => fieldInfo.GetValue(_target),
                PropertyInfo propInfo => propInfo.GetValue(_target),
                _ => _target,
            };
            _coroutineManager = (CoroutineManager)(actualTarget ?? throw new Exception());
            _isInitialized = true;
        }

        public void Draw()
        {
            if (!_isInitialized)
                Initialize();

            if (_coroutineManager == null)
                throw new Exception();

            ImGuiExt.SeparatorText("Coroutines");
            var flags = ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchSame |
                        ImGuiTableFlags.RowBg | ImGuiTableFlags.ContextMenuInBody | ImGuiTableFlags.Hideable;
            if (ImGui.BeginTable("CoroutinesTable", 4, flags, default))
            {
                ImGui.TableSetupColumn("Name");
                ImGui.TableSetupColumn("Elapsed");
                ImGui.TableSetupColumn("NumUpdates", ImGuiTableColumnFlags.DefaultHide);
                ImGui.TableSetupColumn("Actions");
                for (var i = 0; i < _coroutineManager._routines.Count; i++)
                {
                    var routine = _coroutineManager._routines[i];
                    ImGui.PushID(i);
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    var icon = routine.IsDone ? FontAwesome6.Check : FontAwesome6.Play;
                    var color = routine.IsDone ? Color.Green : Color.Yellow;
                    ImGui.TextColored(color.ToNumerics(), icon);
                    ImGui.SameLine();
                    ImGui.TextColored(color.ToNumerics(), routine.Name);
                    ImGui.SameLine();
                    ImGui.Text($"[{routine.NumEnumerators}]");
                    ImGui.TableNextColumn();
                    var cursorPos = ImGui.GetCursorScreenPos();
                    ImGui.Text($"{routine.ElapsedTime:00.00} s");
                    ImGui.SetCursorScreenPos(cursorPos);
                    ImGui.InvisibleButton("ElapsedTooltipButton", ImGui.GetItemRectSize());
                    ImGuiExt.ItemTooltip($"NumUpdates: {routine.NumUpdates}");
                    ImGui.TableNextColumn();
                    ImGui.Text(routine.NumUpdates.ToString());
                    ImGui.TableNextColumn();
                    if (ImGuiExt.ColoredButton(FontAwesome6.Stop, ImGuiExt.Colors[2], "Stop"))
                    {
                        routine.Stop();
                    }
                    ImGui.PopID();
                }

                ImGui.EndTable();
            }
        }
    }
}
