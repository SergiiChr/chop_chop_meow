# Glass jar

The open glass jar is adapted from NVIDIA's SimReady “Jar”, distributed under the
[Creative Commons Attribution 4.0 International license](https://creativecommons.org/licenses/by/4.0/).

- [NVIDIA source](https://huggingface.co/datasets/nvidia/simready-assets/tree/524747d9b5e1f21b5b18b56041788c01d745f1a1/container_jar_glass_black_a02)
- [Patch conversion](https://patch.gg/create/toolbox/assets/simready:container_jar_glass_black_a02)

Patch converted the model from USD to glTF, replaced the MDL materials with UsdPreviewSurface textures, resized
the textures, mapped ambient occlusion from the ORM texture, added physics data and hulls, moved the pivot to
the bottom center, and added credit and license metadata.
Chop Chop Meow removed the lid, physics, and textures, welded texture seams, simplified the source glass mesh
to 988 triangles, centered its base at the origin, and gave it a translucent clear glass finish.
The 210-triangle mesh for heaps keeps the same bounds and open rim.
