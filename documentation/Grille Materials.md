# Grille material conversion

## What changed

Automation COLLADA exports can label both UV inputs as `set="0"` while their source IDs explicitly end in `-UV0` and `-UV1`. The importer now preserves both channels using those source suffixes when the labels conflict. Explicit, distinct COLLADA set indices still take precedence. Ambiguous duplicates fail with an error instead of silently overwriting a channel.

This restores the intended repeating coordinates for albedo, normal, roughness and metallic detail. UV1 remains the source for body stamps and fixture cutouts.

For material names containing `grille` whose albedo is explicitly a `camso_t_grille<digits>_h_stepped.dds` map, the exporter interprets the black centers as openings and white wires as solid material. This is a KSP approximation of the source height pattern, not additional modeled wire geometry. It does not turn dark paint, rubber or generic grille albedo into holes.

Supported grilles get a dedicated atlas, with both detail and original stamp sampled into it. This avoids losing fine wires on small islands of the shared body stamp. Atlas size remains bounded by the selected texture quality. The generated MU uses the dedicated coordinates as its main UV channel; no new runtime module is required.

Stock KSP cutoff materials and optional Resurfaced AlphaTest materials receive the same opacity pixels. The existing stamp still clips fixture and body boundaries. PBR smoothness for masked surfaces is averaged only over pixels surviving the cutoff, excluding invisible atlas space and holes. AlphaTest still has a uniform smoothness approximation because the shader shares its alpha channel between opacity and gloss.

## Existing cars

Reconvert the original ZIP to apply corrected texture coordinates to a previously converted car. For a visual-only replacement, back up and replace that vehicle folder while KSP is closed. Preserve existing craft, placed seats, save files and the shared runtime. Updating the EXE alone does not change already converted models.

Rabbit Rally X is prepared separately for the player's test installation; Lizard GT remains the only included car in the repository. This update changes no runtime DLL or driving calibration.

## Limits

Unrecognized grille texture families retain the normal material translation rather than guessing holes from brightness. Fine wire patterns can still alias at long distances or lower quality. These are alpha-cutout surfaces, not physical wire collision meshes. Original source normal maps and metallic settings are retained.

The manuals cover the shared framework; this supplement describes the new material path. The 2026-10-15 manual refresh updates dependency installation instructions. See `release-manifest.json` for actual build and test evidence.
