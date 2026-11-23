using Godot;

namespace ChopChopMeow;

/// <summary>
/// The model of a tool lying in a station's tool slot: one model for each grade the slot can hold, turned onto its
/// side, of which the view shows the one slotted.
/// </summary>
public sealed class SlottedTool {
    private readonly Node3D root;
    private readonly (string Tool, Node3D Model)[] grades;

    /// <summary>Lays the grades at a spot on a station's body, each lifted so it rests on the surface there.</summary>
    public SlottedTool(Node3D body, Vector3 at, float yaw, params (string Tool, float Lift)[] grades) {
        root = ModelKit.Pivot(body, at, new Vector3(0, yaw, 0));
        this.grades = new (string, Node3D)[grades.Length];
        for (int i = 0; i < grades.Length; i++) {
            var model = ActorRig.Tool(grades[i].Tool);
            model.Position = new Vector3(0, grades[i].Lift, 0); model.RotationDegrees = new Vector3(0, 0, 90);
            root.AddChild(model);
            this.grades[i] = (grades[i].Tool, model);
        }
    }

    /// <summary>The tool whose model shows, or null while the slot is empty.</summary>
    public string? Shown { get; private set; }

    /// <summary>Shows the model of the tool in the slot, or none while it is empty.</summary>
    public void Show(string? tool) {
        Shown = tool;
        root.Visible = tool != null;
        foreach (var (grade, model) in grades) model.Visible = grade == tool;
    }
}
