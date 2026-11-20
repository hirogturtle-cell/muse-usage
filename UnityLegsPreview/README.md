# Legs Preview: cloth skirt and sheer stockings (Unity)

A self-contained Unity project that builds a studio preview scene. It contains a pair of anatomically proportioned legs in **white sheer stockings**, plus a **short pleated skirt** simulated with Unity's built-in **Cloth** component. The skirt collides with **capsule colliders on both legs**.

## Open and run

1. Open `UnityLegsPreview/` in **Unity 2022.3 LTS** (Unity 6 also works) using the Built-in Render Pipeline.
2. On first load the scene builds automatically. You can also build it with **Tools ▸ Legs Preview ▸ Build Scene** (or **Build Scene and Play**).
3. Open `Assets/LegsPreview/Scenes/LegsPreview.unity` and press **Play**.
   - Use the on-screen panel to switch motion (Idle / Walk / Twirl / Still), skirt fabric (Wool / Cotton / Chiffon / Denim), stocking sheerness, camera views, turntable and the studio fan.
   - Drag to orbit, middle-drag to pan, and use the mouse wheel to zoom.
   - After about 3 s the console prints `[LegsPreview] Play-mode cloth check PASSED — …`. The check covers: particle count, NaN check, waist drift, drape amount, and the deepest penetration of the skirt into the leg capsules.

## Verify from the command line

```bash
# 1. generate scene, meshes and materials
Unity -batchmode -projectPath UnityLegsPreview -executeMethod LegsPreview.EditorTools.LegsPreviewSceneBuilder.BuildFromCommandLine -quit -logFile -

# 2. Play-mode tests: load scene, simulate idle / walk / twirl, assert the cloth is stable
Unity -batchmode -projectPath UnityLegsPreview -runTests -testPlatform PlayMode -testResults playmode-results.xml -logFile -

# 3. Edit-mode tests: skinning, normals, collider wiring, preset ranges, shaders
Unity -batchmode -projectPath UnityLegsPreview -runTests -testPlatform EditMode -testResults editmode-results.xml -logFile -
```

## What's in the scene

| Part | Details |
|---|---|
| Legs | Procedural meshes, about 20k vertices each (`Runtime/LegGeometry.cs`). Elliptical cross-sections with anatomical landmarks (gastrocnemius heads, patella, popliteal fossa, malleoli, Achilles hollows, vastus medialis) and a natural Q-angle stance. Circumferences: ankle 19 cm, calf 35 cm, knee 32 cm, upper thigh 55 cm. Each leg is skinned to a thigh bone and a shin bone, with a soft ±4.5 cm knee blend. |
| Skirt | Navy wool-blend twill, A-line, 16 pleats, 38 cm long, about 3k vertices. Uses `SkinnedMeshRenderer` + `Cloth` + `SkirtCloth`. The waist rows are pinned (`maxDistance = 0`), and the stitched-down pleat zone gets limited travel. |
| Colliders | 6 `CapsuleCollider`s: upper thigh, lower thigh and shin on each leg, each 3 mm proud of the skin. Also one hip `ClothSphereColliderPair` so the skirt can't sink into the pelvis. |
| Cloth parameters (default: wool) | stretching 0.97 · bending 0.25 · damping 0.18 · friction 0.40 · solver 240 Hz · virtual particles · tethers · world velocity 0.5 · collision mass 0.1 |
| Other presets | Cotton (0.95 / 0.12 / 0.12), Silk chiffon (0.90 / 0.02 / 0.05, 300 Hz), Denim (0.99 / 0.60 / 0.30) |
| Stocking material | `LegsPreview/SheerStocking`. Coverage depends on viewing angle (the "denier" effect: skin shows through face-on, nylon turns opaque at grazing angles). Also: procedural knit micro-normal with anti-aliasing fade, fibre sheen, wrap-lit skin subsurface, and a lace thigh band with a scalloped edge. |
| Skirt material | `LegsPreview/WovenFabric`. Double-sided (VFACE) with a lining colour on the inside, a procedural 2/2 twill normal, and a wool-fuzz sheen. |
| Lighting | Key (soft shadows), fill, two rim lights, an overhead light (soft shadows) and a backdrop wash. Each has an emissive softbox panel that a realtime reflection probe picks up, so highlights are rectangular. Trilight ambient, an infinity-cove cyclorama, linear colour space, 8× MSAA. |
| Camera | 26° vertical FOV (about an 85 mm portrait lens). Orbit, pan and zoom, with six preset views. |

## Notes

- **Render pipeline**: the custom shaders target the Built-in pipeline. Under URP or HDRP the builder falls back to the pipeline's Lit shader, using matching colours and a double-sided skirt. You lose the angle-dependent stocking look, which would need a Shader Graph port.
- **Photorealism**: the legs are procedural so the project has no external assets. For a true photographic result, swap the `Leg_L/Leg_R` meshes for a scanned model. Keep the bone names and re-run the builder's collider sizing, or adjust the capsules by hand. HDRP with its skin diffusion profile is the natural upgrade path.
- Generated meshes and materials live in `Assets/LegsPreview/Generated/`. Rebuilding updates them in place, so their GUIDs stay stable.
