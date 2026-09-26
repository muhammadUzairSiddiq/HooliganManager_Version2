# Character production assets

Generated offline by CharacterProductionBuilder. Original FBX files remain untouched; registries hold source paths for rebuilding without loading those high-poly assets in gameplay.

- Nine supporter characters plus police have two skinned LOD tiers.
- Supporters: approximately 6.6–9.3k triangles nearby, 2.7–4.7k farther away.
- Verified shirt/hoodie geometry uses a texture-free shared material and per-character faction color. Skin, trousers and footwear retain their materials. Combined source meshes are split only by verified garment topology, never by whole-body tinting.
- Five generated generic-rig clips: chant, watch, treat, pickup and talk. Existing movement/combat clips are retained. These are procedural animation assets, not downloaded Mixamo motion capture.
- Android texture overrides: diffuse 512, normal 256, ASTC 6x6; mip streaming enabled.
- Offline mesh simplification uses UnityMeshSimplifier v3.1.1 (MIT), pinned in Packages/manifest.json. Source and license: https://github.com/Whinarn/UnityMeshSimplifier

Editor bridge commands: character-build, character-animations, character-checks, character-preview, character-gesture-preview. Reports and images are written to Artifacts/CityQA.

Production gates still outstanding: full campaign regression, Android build and sustained profiling on a named low-end phone, animation transitions in all gameplay contexts, and additive/addressable streaming of authored environment chunks. Ambient body streaming does not unload the entire city scene or its navigation/collision data. The 30 FPS mobile setting is a target, not a measured guarantee.
