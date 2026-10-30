using System;
using System.Linq;
using ChopChopMeow;
using Godot;
using static Checks;
using static ModelSteps;

/// <summary>Ore deposits share cached geometry while each prop keeps independent visibility and fade state.</summary>
public static class NatureModelChecks {
    public static void Run() {
        for (int variant = 0; variant < NatureShape.Variants; variant++) CheckHidingOneOreVariantLeavesPropsSharingItsMesh(variant);
    }

    private static void CheckHidingOneOreVariantLeavesPropsSharingItsMesh(int variant) {
        string[] kinds = { "coal", "iron", "tin" };
        var props = new[] {
            WorldModels.Build(new Thing { Kind = ThingKind.Coal, Id = variant }),
            WorldModels.Build(new Thing { Kind = ThingKind.Coal, Id = variant + NatureShape.Variants }),
            WorldModels.Build(new Thing { Kind = ThingKind.Iron, Id = variant }),
            WorldModels.Build(new Thing { Kind = ThingKind.Iron, Id = variant + NatureShape.Variants }),
            WorldModels.Build(new Thing { Kind = ThingKind.Tin, Id = variant }),
            WorldModels.Build(new Thing { Kind = ThingKind.Tin, Id = variant + NatureShape.Variants })
        };
        try {
            var parts = props.Select(Parts).ToArray();
            for (int part = 0; part < 3; part++) {
                ulong shared = parts[0][part].Mesh!.GetInstanceId();
                Require(parts.All(model => model[part].Mesh!.GetInstanceId() == shared),
                    $"ore {PartName(part)} geometry should be cached across ore kinds and repeated deposits");
            }

            for (int part = 0; part < 3; part++) {
                for (int kind = 0; kind < 3; kind++)
                    Require(parts[kind * 2][part].MaterialOverride!.GetInstanceId() == parts[kind * 2 + 1][part].MaterialOverride!.GetInstanceId(),
                        $"repeated {kinds[kind]} deposits should reuse their {PartName(part)} palette material");
                Require(parts[0][part].MaterialOverride!.GetInstanceId() != parts[2][part].MaterialOverride!.GetInstanceId()
                    && parts[0][part].MaterialOverride!.GetInstanceId() != parts[4][part].MaterialOverride!.GetInstanceId()
                    && parts[2][part].MaterialOverride!.GetInstanceId() != parts[4][part].MaterialOverride!.GetInstanceId(),
                    $"{PartName(part)} palette materials should be distinct between coal, iron and tin");
            }
            Require(((StandardMaterial3D)parts[0][2].MaterialOverride!).AlbedoColor.IsEqualApprox(new Color("0c0e10")),
                "mineable coal chips should use the near-black palette");

            var coalHost = parts[0][0];
            var repeatedCoalHost = parts[1][0];
            var original = (StandardMaterial3D)coalHost.MaterialOverride!;
            props[0].Root.Visible = false;
            Require(props[1].Root.Visible && repeatedCoalHost.Visible, "hiding one ore prop should not hide another prop sharing its mesh");
            props[0].Root.Visible = true;
            ModelKit.SetFade(coalHost, original, .5f);
            Require(coalHost.MaterialOverride is StandardMaterial3D faded
                    && faded.GetInstanceId() != original.GetInstanceId()
                    && faded.VertexColorUseAsAlbedo
                    && faded.AlbedoColor.IsEqualApprox(original.AlbedoColor with { A = .5f })
                    && repeatedCoalHost.MaterialOverride!.GetInstanceId() == original.GetInstanceId(),
                "fading one ore prop should not replace the shared palette material on another prop");
            ModelKit.SetFade(coalHost, original, 0);
            Require(coalHost.MaterialOverride!.GetInstanceId() == original.GetInstanceId(),
                "resetting an ore prop fade should restore its palette material");
        } finally {
            foreach (var prop in props) prop.Root.Free();
        }
    }

}
