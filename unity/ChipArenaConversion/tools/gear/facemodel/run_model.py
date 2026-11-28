# blender --background --factory-startup --python run_model.py -- MODEL.py OUTDIR HEAD.obj: set the scene up (the pilot's head from
# HEAD.obj, headobj.py's), run the gear model (sculpted.py), export its records to OUTDIR/gear.json (and OUTDIR/model.blend).
import bpy, sys, os, runpy
argv = sys.argv[sys.argv.index('--') + 1:]
model, out, head = argv[0], argv[1], argv[2]
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__))); sys.path.insert(0, os.path.dirname(os.path.abspath(model)))
import blhelp
blhelp.HEAD_OBJ = head
blhelp.setup()
runpy.run_path(model, run_name='__main__')
blhelp.export(out)
