using Godot;
using static ChopChopMeow.ModelKit;

namespace ChopChopMeow;

/// <summary>Builds a cabin's model, with its door on a hinge, its front window and its lamp.</summary>
internal static class CabinModel {
    /// <summary>Small OSB cabin on block piers with a corrugated shed roof, solar panel and rain barrel.</summary>
    public static HutProp Build() {
        var prop = new HutProp();
        var body = prop.Body;
        foreach (var (x, z) in new[] { (-1.1f, -1f), (1.1f, -1f), (-1.1f, 1f), (1.1f, 1f) }) Box(body, new Vector3(.3f, .3f, .3f), new Vector3(x, .15f, z), Stone("9a9b97", .85f));
        Box(body, new Vector3(2.6f, .12f, 2.4f), new Vector3(0, .36f, 0), Wood("7d5f43"));
        Add(body, CabinShape.Walls, CabinShape.WallMaterial, Vector3.Zero);
        Add(body, CabinShape.Interior, CabinShape.WallMaterial, Vector3.Zero);
        var trim = Mat("3d4245");
        foreach (var (x, z) in new[] { (-1.2f, -1.1f), (1.2f, -1.1f), (-1.2f, 1f), (1.2f, 1f) }) {
            float height = CabinShape.WallTop(z) - .42f;
            Box(body, new Vector3(.08f, height, .08f), new Vector3(x, .42f + height / 2, z), trim);
        }
        foreach (float x in new[] { -.95f, .15f, 1.03f }) {
            float height = CabinShape.WallTop(1) - .42f;
            Box(body, new Vector3(.025f, height, .015f), new Vector3(x, .42f + height / 2, 1.005f), Wood("b08c55"));
        }
        foreach (float z in new[] { -.8f, .65f }) {
            float height = CabinShape.WallTop(z) - .42f;
            Box(body, new Vector3(.015f, height, .025f), new Vector3(1.205f, .42f + height / 2, z), Wood("b08c55"));
        }
        // Shed roof pitched toward the back, overhanging the door.
        var roof = Pivot(body, new Vector3(0, 2.68f, -.05f), new Vector3(-9, 0, 0));
        Add(roof, CabinShape.Roof, Metal("6f7a80", .45f, .6f), Vector3.Zero);
        Box(roof, new Vector3(1.1f, .05f, .8f), new Vector3(.55f, .085f, -.4f), Mat("1f2c44", .25f, .4f));
        Box(roof, new Vector3(1.14f, .03f, .84f), new Vector3(.55f, .065f, -.4f), Metal("c9ced2", .4f, .7f));
        Post(body, .08f, .08f, 1.1f, new Vector3(-.75f, 2.4f, -.6f), Metal("8e9396", .45f, .7f));
        Post(body, .14f, 0, .12f, new Vector3(-.75f, 3.48f, -.6f), Metal("8e9396", .45f, .7f), 8);
        // Door hinged on its left edge, painted teal.
        // The hinge axis sits just ahead of the jamb so the open leaf clears the frame.
        var door = Pivot(body, new Vector3(-.75f, .42f, 1.075f));
        Box(door, new Vector3(.82f, 1.86f, .05f), new Vector3(.41f, .93f, -.025f), Wood("3e6e6a", .6f));
        Sphere(door, .035f, new Vector3(.72f, .95f, .025f), Metal("c9ced2", .3f, .8f));
        prop.Door = door;
        foreach (float x in new[] { -.79f, .11f })
            Box(body, new Vector3(.06f, 1.93f, .1f), new Vector3(x, 1.385f, 1.015f), trim);
        Box(body, new Vector3(.96f, .06f, .1f), new Vector3(-.34f, 2.32f, 1.015f), trim);
        Box(body, new Vector3(1, .14f, .45f), new Vector3(-.34f, .07f, 1.375f), Wood("a9824f"));
        Box(body, new Vector3(1, .28f, .26f), new Vector3(-.34f, .14f, 1.25f), Wood("a9824f"));
        var glass = Mat("8fb4c4", .15f, .2f);
        Box(body, new Vector3(.7f, .6f, .06f), new Vector3(.55f, 1.65f, 1.0f), trim);
        prop.Window = Box(body, new Vector3(.6f, .5f, .07f), new Vector3(.55f, 1.65f, 1.0f), glass);
        Box(body, new Vector3(.06f, .7f, .9f), new Vector3(1.2f, 1.6f, 0), trim);
        Box(body, new Vector3(.07f, .6f, .8f), new Vector3(1.2f, 1.6f, 0), glass);
        var barrel = Pivot(body, new Vector3(1.5f, 0, .6f));
        var blue = new Color("2c5f9e"); var rim = new Color("244f85");
        ActorShape.Add(barrel, new[] {
            new ActorShape.Ring(.8f, .23f, .23f, rim), new ActorShape.Ring(.77f, .27f, .27f, rim),
            new ActorShape.Ring(.73f, .28f, .28f, blue), new ActorShape.Ring(.16f, .28f, .28f, blue),
            new ActorShape.Ring(.03f, .255f, .255f, blue), new ActorShape.Ring(0, .23f, .23f, blue) });
        prop.Light = WorldModels.Light(body, new Vector3(.55f, 1.65f, 1.4f), 2.5f);
        return prop;
    }
}
