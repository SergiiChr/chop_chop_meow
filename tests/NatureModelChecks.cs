using System;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static ModelSteps;

/// <summary>
/// Ore deposits share cached geometry while each prop keeps independent visibility and fade state.
/// Coal is the stone deposits' rock group drawn as the coal scan.
/// </summary>
public static class NatureModelChecks {
    public static void Run() {
        for (int variant = 0; variant < NatureShape.Variants; variant++) {
            var props = new[] {
                WorldModels.Build(new Thing { Kind = ThingKind.Iron, Id = variant }),
                WorldModels.Build(new Thing { Kind = ThingKind.Iron, Id = variant + NatureShape.Variants }),
                WorldModels.Build(new Thing { Kind = ThingKind.Tin, Id = variant }),
                WorldModels.Build(new Thing { Kind = ThingKind.Tin, Id = variant + NatureShape.Variants })
            };
            var rocks = new[] {
                WorldModels.Build(new Thing { Kind = ThingKind.Coal, Id = variant }),
                WorldModels.Build(new Thing { Kind = ThingKind.Coal, Id = variant + NatureShape.Variants }),
                WorldModels.Build(new Thing { Kind = ThingKind.Stone, Id = variant })
            };
            try {
                CheckOreKindsShareGeometryButKeepTheirOwnPalettes(props);
                CheckHidingOneOreVariantLeavesPropsSharingItsMesh(props);
                CheckCoalIsTheStoneRockGroupInTheCoalScan(rocks);
            } finally {
                foreach (var prop in props.Concat(rocks)) prop.Root.Free();
            }
        }
    }

    private static void CheckOreKindsShareGeometryButKeepTheirOwnPalettes(Prop[] props) {
        string[] kinds = { "iron", "tin" };
        var parts = props.Select(Parts).ToArray();
        for (int part = 0; part < 3; part++) {
            ulong shared = parts[0][part].Mesh!.GetInstanceId();
            Require(parts.All(model => model[part].Mesh!.GetInstanceId() == shared),
                $"ore {PartName(part)} geometry should be cached across ore kinds and repeated deposits");
        }

        for (int part = 0; part < 3; part++) {
            for (int kind = 0; kind < 2; kind++)
                Require(parts[kind * 2][part].MaterialOverride!.GetInstanceId() == parts[kind * 2 + 1][part].MaterialOverride!.GetInstanceId(),
                    $"repeated {kinds[kind]} deposits should reuse their {PartName(part)} palette material");
            Require(parts[0][part].MaterialOverride!.GetInstanceId() != parts[2][part].MaterialOverride!.GetInstanceId(),
                $"{PartName(part)} palette materials should be distinct between iron and tin");
        }
    }

    private static void CheckCoalIsTheStoneRockGroupInTheCoalScan(Prop[] rocks) {
        var meshes = rocks.Select(prop => (MeshInstance3D)prop.Body.GetChild(0)).ToArray();
        Require(rocks[0].Body.GetChildCount() == 1, "a coal deposit should be one rock group, without seams or chips");
        Require(meshes.All(mesh => mesh.Mesh!.GetInstanceId() == meshes[2].Mesh!.GetInstanceId()),
            "coal deposits should share the stone deposits' rock group geometry");
        Require(meshes[0].MaterialOverride == NatureShape.CoalRock && meshes[1].MaterialOverride == NatureShape.CoalRock,
            "coal deposits should be drawn as the coal scan");
        Require(meshes[2].MaterialOverride == NatureShape.PlainRock, "stone deposits should be drawn as the rock scan");
    }

    private static void CheckHidingOneOreVariantLeavesPropsSharingItsMesh(Prop[] props) {
        var parts = props.Select(Parts).ToArray();
        var ironHost = parts[0][0];
        var repeatedIronHost = parts[1][0];
        var original = (StandardMaterial3D)ironHost.MaterialOverride!;
        props[0].Root.Visible = false;
        Require(props[1].Root.Visible && repeatedIronHost.Visible, "hiding one ore prop should not hide another prop sharing its mesh");
        props[0].Root.Visible = true;
        ModelKit.SetFade(ironHost, original, .5f);
        var faded = ironHost.MaterialOverride as StandardMaterial3D;
        Require(faded != null, "fading one ore prop should give it a material");
        Require(faded.GetInstanceId() != original.GetInstanceId(), "fading one ore prop should give it a copy of the shared palette material");
        Require(faded.VertexColorUseAsAlbedo, "the faded copy should keep vertex colours as albedo");
        Require(faded.AlbedoColor.IsEqualApprox(original.AlbedoColor with { A = .5f }), "the faded copy should be half transparent");
        Require(repeatedIronHost.MaterialOverride!.GetInstanceId() == original.GetInstanceId(), "fading one ore prop should leave another prop on the shared palette material");
        ModelKit.SetFade(ironHost, original, 0);
        Require(ironHost.MaterialOverride!.GetInstanceId() == original.GetInstanceId(),
            "resetting an ore prop fade should restore its palette material");
    }

}
