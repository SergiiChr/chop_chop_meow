using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// One wind for all foliage, passed to shaders as global uniforms so every material shares it.
/// The weather drifts between long calm spells and breezy ones, and gusts come and go within a breezy spell.
/// </summary>
internal static class Wind {
    private static readonly StringName Uniform = "wind", PushUniform = "wind_push";
    // A matrix would hold more people to a uniform, but handing Godot one allocates on every call, and a vector does not.
    private static readonly StringName[] PairUniforms = ["wind_pushers_0", "wind_pushers_1", "wind_pushers_2", "wind_pushers_3", "wind_pushers_4", "wind_pushers_5", "wind_pushers_6", "wind_pushers_7"];
    private static float clock, strength, phase;
    /// <summary>How many people the grass parts round at once, two to each of the shader's eight vectors.</summary>
    public const int Pushers = 16;
    /// <summary>Metres out from a person the grass parts to.</summary>
    private const float PushRadius = .55f;

    /// <summary>
    /// Shader code for <c>wind_offset</c>: a world-space push for a vertex at <c>world</c>, scaled by <c>bend</c> (0 at a rooted base, 1 at the tips).
    /// <c>wind</c> holds the direction across the ground, the strength from 0 to 1 and a wave phase that wraps at τ.
    /// </summary>
    public const string ShaderCode = """
        global uniform vec4 wind;
        global uniform vec4 wind_push;
        global uniform vec4 wind_pushers_0;
        global uniform vec4 wind_pushers_1;
        global uniform vec4 wind_pushers_2;
        global uniform vec4 wind_pushers_3;
        global uniform vec4 wind_pushers_4;
        global uniform vec4 wind_pushers_5;
        global uniform vec4 wind_pushers_6;
        global uniform vec4 wind_pushers_7;
        vec3 wind_offset(vec3 world, float bend, float reach) {
            float strength = wind.z;
            // A wave runs downwind across the ground, so neighbours lean in turn rather than in step.
            float wave = sin(wind.w - dot(world.xz, wind.xy) * 0.55 + world.y * 0.2);
            float lean = strength * (0.55 + 0.45 * wave);
            float flutter = sin(wind.w * 3.0 + dot(world, vec3(4.1, 3.3, 5.7))) * strength * 0.3;
            vec2 across = wind.xy * (lean + flutter * 0.4) + vec2(-wind.y, wind.x) * flutter;
            return vec3(across.x, -0.25 * lean * bend, across.y) * bend * reach;
        }
        // Grass parts around whoever walks through it: wind_push holds how many people and how far out, and the eight
        // vectors where they stand, two to each.
        vec3 push_from(vec3 world, vec2 at) {
            vec2 away = world.xz - at;
            // Most grass stands beyond everyone's reach, which leaves it alone.
            if (dot(away, away) >= wind_push.y * wind_push.y) return vec3(0.0);
            float near = 1.0 - smoothstep(0.0, wind_push.y, length(away));
            vec2 out_way = normalize(away + vec2(0.0001));
            return vec3(out_way.x, -0.6, out_way.y) * near;
        }
        vec3 push_pair(vec3 world, vec4 pair, int first) {
            int count = int(wind_push.x);
            vec3 push = vec3(0.0);
            if (first < count) push += push_from(world, pair.xy);
            if (first + 1 < count) push += push_from(world, pair.zw);
            return push;
        }
        vec3 push_offset(vec3 world, float bend) {
            vec3 push = push_pair(world, wind_pushers_0, 0) + push_pair(world, wind_pushers_1, 2) + push_pair(world, wind_pushers_2, 4)
                + push_pair(world, wind_pushers_3, 6) + push_pair(world, wind_pushers_4, 8) + push_pair(world, wind_pushers_5, 10)
                + push_pair(world, wind_pushers_6, 12) + push_pair(world, wind_pushers_7, 14);
            return push * bend * 0.12;
        }
        // A world-space offset in the model's own axes; rotation and per-axis scale are undone, which the model matrices here are made of.
        vec3 to_local(mat4 model, vec3 offset) {
            return vec3(dot(model[0].xyz, offset) / dot(model[0].xyz, model[0].xyz),
                dot(model[1].xyz, offset) / dot(model[1].xyz, model[1].xyz),
                dot(model[2].xyz, offset) / dot(model[2].xyz, model[2].xyz));
        }
        """;

    /// <summary>Advances the weather by <paramref name="dt"/> seconds and parts the grass round the first <see cref="Pushers"/> of <paramref name="walkers"/>.</summary>
    public static void Update(float dt, ReadOnlySpan<Vector2> walkers) {
        clock += dt;
        // Two slow, unrelated waves make the weather: below the threshold it is calm for minutes at a time.
        float weather = .5f + .3f * MathF.Sin(clock * .011f) + .2f * MathF.Sin(clock * .0237f + 1.3f);
        float breezy = Mathf.SmoothStep(.42f, .62f, weather);
        float gust = .5f + .3f * MathF.Sin(clock * .41f) + .2f * MathF.Sin(clock * 1.07f + 2);
        float target = breezy * (.3f + .7f * gust);
        strength = Mathf.MoveToward(strength, target, dt * .35f);
        // The sway quickens in a stronger wind; whole turns are dropped so the phase keeps its precision.
        phase = (phase + dt * (1.1f + 1.8f * strength)) % Mathf.Tau;
        float heading = .6f + .5f * MathF.Sin(clock * .007f);
        RenderingServer.GlobalShaderParameterSet(Uniform, new Vector4(MathF.Cos(heading), MathF.Sin(heading), strength, phase));
        int count = Math.Min(walkers.Length, Pushers);
        RenderingServer.GlobalShaderParameterSet(PushUniform, new Vector4(count, PushRadius, 0, 0));
        for (int pair = 0; pair * 2 < count; pair++) {
            var second = pair * 2 + 1 < count ? walkers[pair * 2 + 1] : Vector2.Zero;
            RenderingServer.GlobalShaderParameterSet(PairUniforms[pair], new Vector4(walkers[pair * 2].X, walkers[pair * 2].Y, second.X, second.Y));
        }
    }
}
