using System.Reflection;
using Godot;

namespace Yudian;

public partial class Main : Node3D
{
    public override void _Ready()
    {
        var asm = Assembly.GetExecutingAssembly().GetName();
        GD.Print($"[Yudian] C# assembly {asm.Name} v{asm.Version} | Godot {Engine.GetVersionInfo()["string"]}");
    }
}
