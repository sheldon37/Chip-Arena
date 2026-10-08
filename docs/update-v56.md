# Primary melee motion and sky portraits

Standard left-click attacks now use separate hand and foot targets, hip rotation, torso counter-rotation, weight shifts, and continuous passing poses:

- Daggers alternate hands, drop into a low third cut, and turn through a complete fourth cut. Feet pivot with the turn.
- Greatsword alternates a rising two-handed overhead chop and a broad diagonal cut.
- Sword follows a three-cut phrase: cross cut, reverse cut, overhead finish.
- Shield coils through the hips, steps into an extended slam, and pivots the rear foot. Existing knockback and hitstun remain.

Foot placement keeps moving attacks above the floor. Combo changes and the actual end of each primary animation blend back into the incoming pose. Remote players use attack timestamps on the interpolation timeline so a future combo cannot replace a currently visible cut; the local player uses the current animation clock. E animation profiles and developer fighting styles are unchanged.

Death fireworks now reveal a large cyan head-and-shoulders portrait with the player's hat, scan lines, corner brackets, and their name. Each viewer gets a consistent upper-sky projection that remains visible from indoors and while scoped, with room for three simultaneous portraits. Existing queueing, attribution, launch/burst sounds, and cleanup remain. The kill feed is small plain text in the upper-left corner without a pill, border, or background.

Validation: TypeScript; 75 focused combat, recovery, networking, developer-class, feedback, and presentation checks; offline Three.js continuous-combo and portrait renders. New checks cover planted contacts, running foot clearance, full dagger turns, overhead cuts, transition continuity, network attack timing, and portrait framing across 288 camera/viewport/zoom combinations. The four supplied basic-attack GIFs and hologram image guided the motion and presentation. No browser playtest was available in this environment.
