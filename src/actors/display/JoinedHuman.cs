using System;
using System.Collections.Generic;
using Godot;
using Ring = ChopChopMeow.ActorShape.Ring;

namespace ChopChopMeow;

/// <summary>A closed body surface from the neck to the shoes and palms, posed by the film rig's joints.</summary>
internal sealed class JoinedHuman {
    private const int Sides = 12;
    private enum Bone {
        Pelvis, Chest, LeftShoulder, LeftElbow, LeftWrist, RightShoulder, RightElbow, RightWrist,
        LeftHip, LeftKnee, LeftAnkle, RightHip, RightKnee, RightAnkle, LeftSocket, RightSocket,
        LeftUnderarm, RightUnderarm,
    }
    private static readonly Bone[] Bones = Enum.GetValues<Bone>();
    private static readonly Dictionary<(BodyBuild, Outfit), ArrayMesh> meshes = new();
    private static StandardMaterial3D? material;
    private readonly Skeleton3D skeleton = new() { Name = "JoinedHuman" };
    private readonly Transform3D chestRest;

    public JoinedHuman(Node3D parent, BodyBuild build, Outfit outfit) {
        var rest = RestPose(build);
        chestRest = rest[(int)Bone.Chest];
        var skin = new Skin();
        foreach (var bone in Bones) {
            int index = (int)bone;
            skeleton.AddBone(bone.ToString());
            skeleton.SetBoneRest(index, rest[index]);
            skin.AddBind(index, rest[index].AffineInverse());
        }
        skeleton.ResetBonePoses();
        var key = (build, outfit);
        if (!meshes.TryGetValue(key, out var mesh)) meshes[key] = mesh = new Surface(build, outfit, rest).Build();
        parent.AddChild(skeleton);
        skeleton.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = material ??= CreateMaterial(), Skin = skin, Skeleton = new NodePath("..") });
    }

    public void Chest(Transform3D chest) => Set(Bone.Chest, chest * chestRest);

    public void Arm(bool left, Transform3D chest, Transform3D shoulder, Transform3D elbow, Transform3D wrist) {
        var upper = chest * shoulder;
        var lower = upper * elbow;
        // The socket follows the lift of the arm; the full roll develops further down the upper arm.
        var bind = ShoulderBasis(left);
        var armDown = shoulder.Basis * Vector3.Down;
        var swing = new Basis(new Quaternion(bind * Vector3.Down, armDown));
        Set(left ? Bone.LeftSocket : Bone.RightSocket, chest * new Transform3D(swing * bind, shoulder.Origin));
        // The fold settles against the upper arm as it lowers, opening away from the ribcage as the arm lifts.
        float closed = Mathf.SmoothStep(.66f, .98f, armDown.Dot(Vector3.Down));
        var fold = new Vector3(left ? .006f : -.006f, -.022f, 0);
        Set(UnderarmBone(left), chest * At(chestRest.Origin + fold * closed));
        Set(left ? Bone.LeftShoulder : Bone.RightShoulder, upper);
        Set(left ? Bone.LeftElbow : Bone.RightElbow, lower);
        Set(left ? Bone.LeftWrist : Bone.RightWrist, lower * wrist);
    }

    public void Leg(bool left, Transform3D hip, Transform3D knee, Transform3D ankle) {
        Set(left ? Bone.LeftHip : Bone.RightHip, hip);
        Set(left ? Bone.LeftKnee : Bone.RightKnee, knee);
        Set(left ? Bone.LeftAnkle : Bone.RightAnkle, ankle);
    }

    private void Set(Bone bone, Transform3D transform) => skeleton.SetBonePose((int)bone, transform);

    private static Transform3D[] RestPose(BodyBuild build) {
        var rest = new Transform3D[Bones.Length];
        rest[(int)Bone.Pelvis] = Transform3D.Identity;
        rest[(int)Bone.Chest] = At(Vector3.Up * build.WaistHeight);
        foreach (bool left in new[] { true, false }) {
            int arm = (int)(left ? Bone.LeftShoulder : Bone.RightShoulder);
            rest[arm] = new Transform3D(ShoulderBasis(left), build.Shoulder(left));
            rest[(int)(left ? Bone.LeftSocket : Bone.RightSocket)] = rest[arm];
            rest[(int)UnderarmBone(left)] = rest[(int)Bone.Chest];
            rest[arm + 1] = rest[arm] * At(Vector3.Down * build.UpperArmLength);
            rest[arm + 2] = rest[arm + 1] * At(new Vector3(0, .05f - build.GripLength, ActorRig.Palm));
            int leg = (int)(left ? Bone.LeftHip : Bone.RightHip);
            rest[leg] = At(build.Hip(left));
            rest[leg + 1] = rest[leg] * At(Vector3.Down * ActorAnimation.Thigh);
            rest[leg + 2] = rest[leg + 1] * At(Vector3.Down * build.ShinLength);
        }
        return rest;
    }

    private static Basis ShoulderBasis(bool left) => new(Vector3.Back, left ? Mathf.Pi / 2 : -Mathf.Pi / 2);

    private static Bone UnderarmBone(bool left) => left ? Bone.LeftUnderarm : Bone.RightUnderarm;

    private static Transform3D At(Vector3 position) => new(Basis.Identity, position);

    private static StandardMaterial3D CreateMaterial() {
        using var cloth = Image.CreateFromData(2, 1, false, Image.Format.Rgba8, [0, 0, 0, 255, 255, 255, 255, 255]);
        var result = ModelKit.Vertex(ChopChopMeow.Surface.Cloth, .85f);
        result.DetailMask = ImageTexture.CreateFromImage(cloth);
        result.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps;
        return result;
    }

    private readonly record struct Paint(Color Color, float Weave) {
        public Paint Toward(Paint to, float weight) => new(Color.Lerp(to.Color, weight), Mathf.Lerp(Weave, to.Weave, weight));
    }

    private readonly record struct Weights(Bone First, Bone Second, Bone Third, float ToSecond, float ToThird) {
        public static Weights Between(Bone first, Bone second, float amount) => new(first, second, first, amount, 0);
    }

    private readonly record struct Vertex(Vector3 Point, Paint Paint, Weights Weights);

    private sealed class Surface(BodyBuild build, Outfit outfit, Transform3D[] rest) {
        private readonly List<Vertex> vertices = new();
        private readonly List<int> triangles = new();

        public ArrayMesh Build() {
            var trunk = build.Trunk ?? throw new ArgumentException("The joined body needs a trunk profile.", nameof(build));
            var topPaint = Paints(HumanShape.TrunkPalette(outfit));
            int top = Array.FindIndex(trunk, ring => AtHeight(ring, .379f));
            int bottom = Array.FindIndex(trunk, ring => AtHeight(ring, .305f));
            int pelvis = Array.FindIndex(trunk, ring => AtHeight(ring, .028f));
            if (top <= 0 || bottom - top != 4 || pelvis <= bottom)
                throw new ArgumentException("The trunk profile must contain the shoulder and hip rims.", nameof(build));
            var rows = new int[pelvis + 1][];
            for (int i = 0; i <= pelvis; i++) {
                var ring = trunk[i] with { Y = trunk[i].Y + HumanShape.TrunkOrigin };
                float chestWeight = Mathf.SmoothStep(build.WaistHeight - .065f, build.WaistHeight + .065f, ring.Y);
                rows[i] = Row(ring, Transform3D.Identity, Sample(topPaint, ring.Tone), Weights.Between(Bone.Pelvis, Bone.Chest, chestWeight));
                if (i < 1) continue;
                bool shoulders = i > top && i <= bottom;
                Sew(rows[i - 1], rows[i], shoulders);
            }
            Close(rows[0], true);
            int[] crotch = Crotch(rows[pelvis]);
            foreach (bool left in new[] { true, false }) {
                AddArm(ShoulderRim(rows, top, bottom, left), left);
                AddLeg(HipRim(rows[pelvis], crotch, left), left);
            }
            return Finish();
        }

        private static bool AtHeight(Ring ring, float y) => Math.Abs(ring.Y + HumanShape.TrunkOrigin - y) < .0001f;

        // The rectangular openings each have 12 edges. The rims follow the same winding as the arm sections.
        private int[] ShoulderRim(int[][] rows, int top, int bottom, bool left) {
            int side = left ? 0 : Sides / 2;
            int front = (side + (left ? 1 : -1) + Sides) % Sides;
            int back = (side + (left ? -1 : 1) + Sides) % Sides;
            int[] rim = [rows[top][side], rows[top][front], rows[top + 1][front], rows[top + 2][front], rows[top + 3][front],
                rows[bottom][front], rows[bottom][side], rows[bottom][back], rows[top + 3][back], rows[top + 2][back],
                rows[top + 1][back], rows[top][back]];
            // The chest sweeps out into the front fold, which is fuller than the hollow behind it.
            for (int i = 4; i <= 8; i++) {
                int offset = i - 6;
                float fullness = 1 - offset * offset / 9f;
                float fore = Math.Max(0, -offset) * .5f;
                int index = rim[i];
                var vertex = vertices[index];
                var point = vertex.Point + fullness * new Vector3((left ? 1 : -1) * .015f, .005f + .010f * fore, .023f * fore);
                vertices[index] = vertex with { Point = point, Weights = Weights.Between(Bone.Chest, UnderarmBone(left), .9f * fullness) };
            }
            if (left) return rim;
            var reversed = new int[Sides];
            for (int i = 0; i < Sides; i++) reversed[i] = rim[(Sides + Sides / 2 - i) % Sides];
            return reversed;
        }

        // The leg openings share this centre line from the front of the pelvis under to its back.
        private int[] Crotch(int[] pelvis) {
            var front = vertices[pelvis[3]];
            return [
                Add(front with { Point = new Vector3(0, -.022f, .079f) }),
                Add(front with { Point = new Vector3(0, -.068f, .041f) }),
                Add(front with { Point = new Vector3(0, -.083f, .007f) }),
                Add(front with { Point = new Vector3(0, -.066f, -.033f) }),
                Add(front with { Point = new Vector3(0, -.018f, -.076f) }),
            ];
        }

        private static int[] HipRim(int[] pelvis, int[] crotch, bool left) => left
            ? [pelvis[0], pelvis[1], pelvis[2], pelvis[3], crotch[0], crotch[1], crotch[2], crotch[3], crotch[4], pelvis[9], pelvis[10], pelvis[11]]
            : [crotch[2], crotch[1], crotch[0], pelvis[3], pelvis[4], pelvis[5], pelvis[6], pelvis[7], pelvis[8], pelvis[9], crotch[4], crotch[3]];

        private void AddArm(int[] rim, bool left) {
            var shoulder = left ? Bone.LeftShoulder : Bone.RightShoulder;
            var socket = left ? Bone.LeftSocket : Bone.RightSocket;
            var elbow = left ? Bone.LeftElbow : Bone.RightElbow;
            var wrist = left ? Bone.LeftWrist : Bone.RightWrist;
            var colors = Paints(HumanShape.ArmPalette(outfit.Top, outfit.Skin, outfit.Sleeves));
            var rings = left ? build.LeftArm : build.RightArm;
            int[] previous = rim;
            foreach (var ring in rings) {
                if (ring.Y > -.074f || ring.Y < -.427f) continue;
                float bend = Mathf.SmoothStep(build.UpperArmLength - .065f, build.UpperArmLength + .065f, -ring.Y);
                var weight = -ring.Y < .15f
                    ? Weights.Between(socket, shoulder, Mathf.SmoothStep(.08f, .15f, -ring.Y))
                    : Weights.Between(shoulder, elbow, bend);
                // Leave room against the ribcage when the arm hangs close to the body.
                float ease = .010f * (1 - Mathf.SmoothStep(.075f, .17f, -ring.Y));
                var profile = ring with { Width = ring.Width - ease, Sideways = ring.Sideways + (left ? ease : -ease) };
                var row = Row(profile, rest[(int)shoulder], Sample(colors, ring.Tone), weight);
                if (previous == rim) {
                    var inner = Bridge(rim, row, .29f, Bone.Chest, socket, 0);
                    var outer = Bridge(rim, row, .34f, Bone.Chest, socket, 1);
                    var deltoid = Bridge(rim, row, .66f, Bone.Chest, socket, 1);
                    RoundArmFold(inner, left, .012f);
                    RoundArmFold(outer, left, .013f);
                    RoundArmFold(deltoid, left, .009f);
                    Sew(rim, inner); Sew(inner, outer); Sew(outer, deltoid); Sew(deltoid, row);
                } else Sew(previous, row);
                previous = row;
            }
            AddPalm(previous, elbow, wrist);
        }

        /// <summary>Rounds the hollow on the underside of the arm as it leaves the chest.</summary>
        private void RoundArmFold(int[] row, bool left, float rise) {
            int crease = left ? Sides / 2 : 0;
            for (int offset = -2; offset <= 2; offset++) {
                int index = row[(Sides + crease + offset) % Sides];
                var vertex = vertices[index];
                float weight = 1 - offset * offset / 9f;
                weight *= weight;
                var weights = vertex.Weights;
                float fold = .9f * (1 - weights.ToSecond) * weight;
                vertices[index] = vertex with {
                    Point = vertex.Point + Vector3.Up * (rise * weight),
                    Weights = weights with { Third = UnderarmBone(left), ToThird = fold },
                };
            }
        }

        private void AddPalm(int[] previous, Bone elbow, Bone wrist) {
            var skin = new Paint(new Color(outfit.Skin), 0);
            Ring[] palm = [
                new(.008f, .017f, .019f, Colors.White), new(-.005f, .014f, .022f, Colors.White),
                new(-.019f, .013f, .027f, Colors.White), new(-.04f, .013f, .028f, Colors.White),
                new(-.061f, .011f, .025f, Colors.White), new(-.074f, .006f, .016f, Colors.White),
            ];
            foreach (var ring in palm) {
                float bend = 1 - Mathf.SmoothStep(-.019f, .021f, ring.Y);
                var row = Row(ring, rest[(int)wrist], skin, Weights.Between(elbow, wrist, bend));
                Sew(previous, row);
                previous = row;
            }
            Close(previous, false);
        }

        private void AddLeg(int[] rim, bool left) {
            var hip = left ? Bone.LeftHip : Bone.RightHip;
            var knee = left ? Bone.LeftKnee : Bone.RightKnee;
            var ankle = left ? Bone.LeftAnkle : Bone.RightAnkle;
            float length = ActorAnimation.Thigh + build.ShinLength;
            var (rings, material) = HumanShape.LegShape(length, outfit, build, left);
            var colors = Paints(material);
            int[] previous = rim;
            foreach (var ring in rings) {
                if (ring.Y > -.081f) continue;
                float bend = Mathf.SmoothStep(ActorAnimation.Thigh - .065f, ActorAnimation.Thigh + .065f, -ring.Y);
                float shoe = Mathf.SmoothStep(length - .03f, length + .015f, -ring.Y);
                var weights = new Weights(hip, knee, ankle, bend * (1 - shoe), bend * shoe);
                var row = Row(ring, rest[(int)hip], Sample(colors, ring.Tone), weights);
                if (previous == rim) {
                    var seat = Bridge(rim, row, .24f, Bone.Pelvis, hip, .24f);
                    var crease = Bridge(rim, row, .52f, Bone.Pelvis, hip, .52f);
                    var thigh = Bridge(rim, row, .75f, Bone.Pelvis, hip, .75f);
                    ShapeHip(seat, left, .003f, .011f, 0, 0);
                    ShapeHip(crease, left, .011f, .005f, .003f, .12f);
                    ShapeHip(thigh, left, .006f, -.003f, .002f, .07f);
                    Sew(rim, seat); Sew(seat, crease); Sew(crease, thigh); Sew(thigh, row);
                } else Sew(previous, row);
                previous = row;
            }
            Close(previous, false);
        }

        /// <summary>Shapes the hip and lets its outer edge follow the top of the thigh as the leg bends.</summary>
        private void ShapeHip(int[] row, bool left, float outside, float back, float front, float thighWeight) {
            float sign = left ? 1 : -1;
            for (int i = 0; i < Sides; i++) {
                float angle = Mathf.Tau * i / Sides;
                float lateral = Math.Max(0, sign * MathF.Cos(angle));
                lateral *= lateral;
                float depth = MathF.Sin(angle);
                float rear = Math.Max(0, -depth), fore = Math.Max(0, depth);
                int index = row[i];
                var vertex = vertices[index];
                var point = vertex.Point + new Vector3(sign * outside * lateral, 0, -back * rear * rear - front * fore * fore);
                var weights = vertex.Weights with { ToSecond = vertex.Weights.ToSecond + thighWeight * lateral };
                vertices[index] = vertex with { Point = point, Weights = weights };
            }
        }

        private int[] Row(Ring ring, Transform3D rest, Paint paint, Weights weights) {
            var row = new int[Sides];
            for (int i = 0; i < Sides; i++) {
                float angle = Mathf.Tau * i / Sides, x = MathF.Cos(angle), z = MathF.Sin(angle);
                float front = Math.Max(z, 0);
                float lobe = 6.75f * x * x * front * front * front * front * ring.FrontLobes;
                var point = new Vector3(ring.Width * x + ring.Sideways, ring.Y, ring.Depth * z + ring.Forward + lobe);
                row[i] = Add(new Vertex(rest * point, paint, weights));
            }
            return row;
        }

        private int[] Bridge(int[] from, int[] to, float amount, Bone first, Bone second, float paint) {
            var row = new int[Sides];
            for (int i = 0; i < Sides; i++) {
                var a = vertices[from[i]];
                var b = vertices[to[i]];
                var point = a.Point.Lerp(b.Point, amount);
                row[i] = Add(new Vertex(point, a.Paint.Toward(b.Paint, paint), Weights.Between(first, second, amount)));
            }
            return row;
        }

        private int Add(Vertex vertex) {
            int index = vertices.Count;
            vertices.Add(vertex);
            return index;
        }

        private void Sew(int[] upper, int[] lower, bool openings = false) {
            for (int i = 0; i < Sides; i++) {
                if (openings && i is 11 or 0 or 5 or 6) continue;
                int next = (i + 1) % Sides;
                Triangle(upper[i], lower[i], upper[next]);
                Triangle(upper[next], lower[i], lower[next]);
            }
        }

        private void Close(int[] row, bool top) {
            Vector3 point = Vector3.Zero;
            foreach (int index in row) point += vertices[index].Point;
            int centre = Add(vertices[row[0]] with { Point = point / Sides });
            for (int i = 0; i < Sides; i++) {
                int next = row[(i + 1) % Sides];
                Triangle(centre, top ? row[i] : next, top ? next : row[i]);
            }
        }

        private void Triangle(int a, int b, int c) {
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
        }

        private ArrayMesh Finish() {
            var normals = new Vector3[vertices.Count];
            for (int i = 0; i < triangles.Count; i += 3) {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                var normal = (vertices[c].Point - vertices[a].Point).Cross(vertices[b].Point - vertices[a].Point);
                normals[a] += normal; normals[b] += normal; normals[c] += normal;
            }
            using var surface = new SurfaceTool();
            surface.Begin(Mesh.PrimitiveType.Triangles);
            for (int i = 0; i < vertices.Count; i++) {
                var vertex = vertices[i];
                var weight = vertex.Weights;
                surface.SetNormal(normals[i].Normalized());
                surface.SetColor(vertex.Paint.Color);
                surface.SetUV(new Vector2(.25f + .5f * vertex.Paint.Weave, .5f));
                surface.SetBones([(int)weight.First, (int)weight.Second, (int)weight.Third, 0]);
                surface.SetWeights([1 - weight.ToSecond - weight.ToThird, weight.ToSecond, weight.ToThird, 0]);
                surface.AddVertex(vertex.Point);
            }
            foreach (int index in triangles) surface.AddIndex(index);
            return surface.Commit();
        }

        private static Paint[] Paints(StandardMaterial3D material) {
            using var color = material.AlbedoTexture.GetImage();
            using var mask = material.DetailMask.GetImage();
            var paints = new Paint[color.GetWidth()];
            for (int i = 0; i < paints.Length; i++) paints[i] = new Paint(color.GetPixel(i, 0), mask.GetPixel(i, 0).R);
            return paints;
        }

        private static Paint Sample(Paint[] paints, float tone) => paints[Math.Clamp((int)(tone * paints.Length), 0, paints.Length - 1)];
    }
}
