# Clown Love

An additional developer-only knockout cosmetic; Clown Getaway is unchanged.
Select **Customize → Kill Effect → Clown Love**, or select it in the local
Developer Sandbox and click **Preview Knockout**. Restore the opponent to replay.

The five-second timeline drops the opponent's weapon, runs the clown in,
connects their hands face-to-face, performs six knee bounces / sideways dips,
and ends with a sideways carry and run-off. Hearts and decorative `1 UP` labels
appear on each dip; these labels grant no health, score, or extra lives.

The first five seconds of the user's supplied romantic violin MP4 are encoded
as `public/audio/clown-love.mp3`, with brief entrance and exit fades. The effect
uses the effects volume and mute controls, and ducks background music. It shares
the authoritative death age with the animation: late snapshots seek the music
and schedule only remaining accents; resets stop all scheduled effect audio.

Validation: developer profile sanitization, forged hosted-room claims, opponent
effect attribution, finite poses at 60 Hz across the timeline, face-to-face
separation, effect cleanup, late audio scheduling/deduplication/cancellation,
and existing authentication/cosmetic/updater regressions. Key poses were rendered
with Three.js's SVG compatibility renderer for visual inspection. Interactive
browser and Windows execution were not available in this session.
