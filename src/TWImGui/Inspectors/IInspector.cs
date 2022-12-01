namespace MyGame.TWImGui.Inspectors;

public interface IInspector
{
    string? InspectorOrder { get; set; }
    void Draw();
}

public interface IInspectorWithTarget : IInspector
{
    void SetTarget(object target);
}

public interface IInspectorWithMemberInfo : IInspector
{
    void SetMemberInfo(MemberInfo memberInfo);
}

public interface IInspectorWithType : IInspector
{
    void SetType(Type type);
}
