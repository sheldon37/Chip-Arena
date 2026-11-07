# Reference video to NOCTIEL's rig (tools/motion)

Owner, 2026-11-06: "Reproduce the reference videos as accurately as technically possible in Unity ... Prefer extracting and
retargeting actual skeletal motion ... Do not claim that motion was extracted directly from a video if it was actually approximated."

This folder turns the owner's reference clips into the game's baked motion (`Assets/ChipArena/Resources/FallenIntroMocap.json`,
read by Core `NativeFallenIntroMocap` and Runtime `NativeFallenIntroMocapBody.cs`). Nothing here runs in the game, the build or CI.

## What is extracted and what is not

| Part | How | Honest status |
| --- | --- | --- |
| Body (33 joints) | MediaPipe Pose Landmarker (heavy), every frame, 3D world landmarks | **Extracted** (a single-camera estimate: depth is inferred) |
| Claymore's way | `sword2d.py`: the blade tracked in the picture (a thin-ridge search from the hand), its foreshortening and the body give the depth's sign | **Extracted in 2D**, depth inferred |
| Rig motion | `solve.py`: each limb aimed along the reference's, hinges (elbow, knee) from the bend, solved on NOCTIEL's own rest skeleton | **Retargeted** |
| Clean-up | smoothing; held contacts kept on the floor (two-bone IK); backward elbow/knee bends straightened; wrist kept within 80 degrees | **Corrected**, counted per clip |
| Unreadable frames | named per clip in `clips/NN.json`: frames held, blade filled between readable frames or two keys read off the frames by eye | **Filled**, flagged per frame in the file |
| Depth errors seen in Unity | `correct.py`: the legs and hips (the solve got them wrong in depth), the arm on the plant, two stretches of the blade, the chest's lean in the kneel, read off the reference frames beside Unity | **Corrected by hand**, flagged per frame and limb in the file's `corrected` track (1 legs and hips, 2 right arm, 4 left arm, 8 blade, 16 chest). The spine, the head and everything else unflagged stay extracted |

## Setup (once, any Linux or Windows machine with Python 3.10+)

```bash
python3 -m venv .venv
.venv/bin/pip install mediapipe opencv-python-headless scipy
mkdir -p models
curl -O --output-dir models https://storage.googleapis.com/mediapipe-models/pose_landmarker/pose_landmarker_heavy/float16/latest/pose_landmarker_heavy.task
curl -O --output-dir models https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/float16/latest/hand_landmarker.task
```

On a headless Linux box MediaPipe also needs `libegl1 libgles2`. ffmpeg and ffprobe must be on the path.

## Running a clip

```bash
.venv/bin/python extract_reference.py 04-intro-reference.mp4 clip04.json models
.venv/bin/python sword2d.py 04-intro-reference.mp4 clip04.json sword04.json
.venv/bin/python solve.py clip04.json sword04.json solved04.json clips/04.json
.venv/bin/python render.py 04-intro-reference.mp4 clip04.json solved04.json compare04.jpg 0,48,96,126,134,160
.venv/bin/python bake.py solved/FallenIntroMocap.solved.json kneel-rise=solved02.json pull-salute=solved03.json raise-cut=solved04.json
.venv/bin/python correct.py solved/FallenIntroMocap.solved.json ../../Assets/ChipArena/Resources/FallenIntroMocap.json
```

`solved/FallenIntroMocap.solved.json` is the solve's own bake, kept so `correct.py` can be re-run without the clips: to change a
correction, edit `correct.py` and run its line alone (it needs only numpy and scipy). `correct.py` is written for this bake's frames;
a new solve of the clips means reading its corrections off the frames again.

`overlay.py` and `swordsheet.py` draw the extracted skeleton and the blade track over the frames, to check the extraction itself.
`render.py` puts the reference frame beside NOCTIEL's rig from the same camera and from the side (bones and sword, not the
game's renderer). The managed checks (`CoreTests --fallen-mocap`) then hold the baked file to its contract.

To compare in the game itself, the DEV preview takes a locked camera in the Editor: `Temp/fallen-preview-cam.txt` holds
"yaw distance height lookHeight fov". The Editor's slowed preview reads `Temp/fallen-preview-rate.txt`.

## Per-clip settings (`clips/NN.json`)

| Key | Meaning |
| --- | --- |
| `stand` | frames standing still (the camera's tilt and the body's facing are taken from them) |
| `body_from` | frames before it are held from it (MediaPipe found no body, or misread an arm) |
| `unreliable` | blade frames not to trust (blur, out of the frame, behind the body): filled |
| `blade_keys` | a blade way read off a frame by eye where blur hides it (boss frame) |
| `plant_until` | the fist is on the planted sword until this frame (the game's plant IK holds it) |
| `two_hands`, `two_dist`, `two_from` | the lunge's two-handed grip |
| `still_px` | a contact still in the picture below this many pixels a frame is held on the floor (1.5) |

## Adding a reference

Give the clip a settings file, run the steps, look at `overlay.py`, `swordsheet.py` and `render.py` sheets, bake, and run
`CoreTests --fallen-mocap`. The joint, floor, slide and blade checks report where the clip needs attention.
