# Lake surface trial

The lake uses a single flat radial mesh at the existing `y = 0.025` water
height. Its outer ring samples the same `Terrain.Shore(angle)` radius as the
walkability and shoreline props. Normalized UV radius drives an authored teal
shallow-to-blue center gradient; this is a visual bathymetry approximation,
because the ground plane has no lake-depth geometry. The shader adapts
Gerstner normal and Fresnel equations from the MIT-licensed
`Chrisknyfe/boujie_water_shader` source at commit
`25f3c2da458e0a4f65bdb5903dc068b050930c06`. It does not implement the upstream
open-ocean plugin, geometry displacement, refraction, or buoyancy, and it does
not read screen or depth textures.

Run the game with `--environment-review` to freeze the shader's time input at
1.25 seconds for comparable screenshots. Normal play uses Godot's `TIME`.
