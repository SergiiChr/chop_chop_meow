# Default lake and coastal water

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

The default water shader also shades the coastline. Coast mesh UVs encode
distance from its existing boundary, driving shallows and a narrow animated
foam wash. `bank.gdshader` blends wet ground into sand/soil and then grass along
irregular-width bank strips. Lake stones cluster in patches rather than an
even ring. Water height and the shared walking/collection boundaries remain
unchanged. The before images in `docs/reviews/environment` preserve the former
bank and coastline treatment for historical comparison.
