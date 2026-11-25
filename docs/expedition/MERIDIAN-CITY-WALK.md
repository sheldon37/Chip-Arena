# Meridian city walk · Claude handoff

The owner walked Expedition and described stairs with the railings underneath them and streets on different levels that felt disconnected. The first art pass is Crown Hill's north gate to City Hall. Walk it from a pilot's eye height. A useful result would be a new player recognizing where they are going at the first gate, then finding the turn at the top of the stairs without opening the map.

## Crown Hill walkthrough

| From | What the player should see next | Art placement |
| --- | --- | --- |
| Camp north gate, (0, 0, 25) | The Hundred Steps straight ahead and City Hall's clock tower beyond them | Frame the foot of the stairs with a pair of civic entrances. End the pavement against the first tread. Keep the tower silhouette visible from the gate. |
| Foot of the steps, (0, 0, 38), to the upper street, (0, 14, 75) | Four flights and three places to catch your bearings | Carry one style of rail and stone through all four flights. Align each landing's paving with its treads. Put the upper street's first entrance at its actual floor height. |
| Upper street to the Zigzag, (0, 14, 110) | The uphill road turns left here | Use a continuous curb and a corner frontage turned toward the junction. Continue that frontage along the first switchback. Place the City Hall direction sign where a pilot approaching from the steps can read it. |
| Last rise to the court, (0, 28, 192) | A civic square and a clear entrance | Open the street frontage to reveal the gateway. Keep storefronts and freestanding props clear of the final approach. |

## Owned assets to audition

The owner's Unity inventory lists the following packs. Prefab availability in the local project, render compatibility, scale and cost still need an Editor check.

- **City & Terrace Pack** (POLYBOX). Audition a street frontage and terrace trim together on Crown Hill. Repeat one facade material and bay spacing across the switchback before introducing variation.
- **Old Industry** (AndragorInc). Try one warehouse or loading frontage on the Vale Cold Store approach. Use the existing quay cranes as the landmark from the lower street.
- **Abandoned City Bus - Drivable** (QuadArt). Inspect the body mesh as a stationary prop for an existing bus collision footprint. Check its size and LOD cost before including it.
- **Modular Destroyed Buildings** (Hivemind). Try a damaged bay at an infected district entrance. Keep the inhabited civic street legible in the same shot.

Use the existing local licensed-asset import and bank workflow. Record the selected prefab paths, scale, material count and LODs. Walk the test route in both directions and capture the gate, middle landing and turn in the Windows player, including a low-quality pass. Compare frame time and draw calls against the same seed and camera path.

## Geometry for the dressing pass

In the generated walk, the primary stair and landing guard pieces are named `stair-guard-<segment>-{left|right}-{post|grip}-N`. The grip follows the walking slope at 1.05 m and the posts seat on the visible treads. A kit replacement can bind to those block transforms. Keep the existing collider and suppress its fallback drawing when the replacement is active. The iron under a fire escape encloses its collision wedge and serves a different purpose. Send any proposed walkable doorway, moved landing or solid prop footprint back to Astra with the plan, coordinates and dimensions.

After the Crown Hill review, carry the same route-reading test to The Navigation's canal crossings, The Arcades' covered passages, The Stride's viaduct, and The Yards' rail cut. Each district already has a reason for its levels. Use what the pilot sees before a climb and after its landing to make that reason apparent.
