using System;
using Godot;

namespace ChopChopMeow;

/// <summary>
/// One wind for all foliage, passed to shaders as global uniforms so every material shares it.
/// The weather drifts between long calm spells and breezy ones, and gusts come and go within a breezy spell.
/// </summary>
internal static class Wind {
    private static readonly StringName Uniform = "wind", PushUniform = "wind_push";
    private static float clock, strength, phase;

    /// <summary>
    /// Shader code for <c>wind_offset</c>: a world-space push for a vertex at <c>world</c>, scaled by <c>bend</c> (0 at a rooted base, 1 at the tips).
    /// <c>wind</c> holds the direction across the ground, the strength from 0 to 1 and a wave phase that wraps at τ.
    /// </summary>
    public const string ShaderCode = """
        global uniform vec4 wind;
        global uniform vec4 wind_push;
        vec3 wind_offset(vec3 world, float bend, float reach) {
            float strength = wind.z;
            // A wave runs downwind across the ground, so neighbours lean in turn rather than in step.
            float wave = sin(wind.w - dot(world.xz, wind.xy) * 0.55 + world.y * 0.2);
            float lean = strength * (0.55 + 0.45 * wave);
            float flutter = sin(wind.w * 3.0 + dot(world, vec3(4.1, 3.3, 5.7))) * strength * 0.3;
            vec2 across = wind.xy * (lean + flutter * 0.4) + vec2(-wind.y, wind.x) * flutter;
            return vec3(across.x, -0.25 * lean * bend, across.y) * bend * reach;
        }
        // Grass parts around whoever walks through it.
        vec3 push_offset(vec3 world, float bend) {
            vec2 away = world.xz - wind_push.xy;
            float near = 1.0 - smoothstep(0.0, wind_push.z, length(away));
            return vec3(normalize(away + vec2(0.0001)).x, -0.6, normalize(away + vec2(0.0001)).y) * near * bend * 0.12;
        }
        // A world-space offset in the model's own axes; rotation and per-axis scale are undone, which the model matrices here are made of.
        vec3 to_local(mat4 model, vec3 offset) {
            return vec3(dot(model[0].xyz, offset) / dot(model[0].xyz, model[0].xyz),
                dot(model[1].xyz, offset) / dot(model[1].xyz, model[1].xyz),
                dot(model[2].xyz, offset) / dot(model[2].xyz, model[2].xyz));
        }
        """;

    /// <summary>Advances the weather by <paramref name="dt"/> seconds.</summary>
    public static void Update(float dt, Vector2 walker) {
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
        RenderingServer.GlobalShaderParameterSet(PushUniform, new Vector4(walker.X, walker.Y, .55f, 0));
    }
}
