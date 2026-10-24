using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    // The visual body's own weapons (owner 2026-10-23: "the guns seem "comically" big"; docs/BOZO-INTEGRATION.md): life-size
    // models the bake carries under VisualWeapons, one entry per weapon spec, drawn on a visual body in place of the Standard
    // skin's model. The Prototype Series, the voxel body, first person and the specs without an entry keep the game's model.
    // An entry's Model is the weapon in the visual weapon frame (its fist point on the game handle's, its bore or blade along
    // +z), inactive in the prefab so the bake's other instances (first person, corpses) never show it. A gun's Fit lays the
    // Standard gun on it: the weapon frame scaled by Fit's scale about Fit's position puts the game's bore, muzzle and back on
    // the model's. While the model is drawn the right hand bone takes that fitted frame (VisualWeaponFit), so everything drawn
    // there (the muzzle flash and its light, where the rounds leave the barrel, the chip modules, the Q kit's pieces) lands on
    // the model at its size; Support and Stock replace the Standard gun's handguard hold and stock butt in the visual hands'
    // grasp. Blades need no fit: their models reach the game's blade tip, so trails, blood and enchants keep their place.
    // Presentation only: aim, rules, reach, hit volumes and the wire are the game's.
    public sealed partial class ArenaPresentation
    {
        private sealed class VisualWeapon
        {
            public Transform Mount; public GameObject Model; public Vector3 Centre; public float Scale=1; public bool Supported, Stocked; public Vector3 Support; public float Stock;
        }

        // The bake's entries under VisualWeapons (null without any), each model hidden until drawn.
        private static Dictionary<string,VisualWeapon> VisualWeaponsOf(Transform root)
        {
            var holder=root.Find("VisualWeapons");if(holder==null)return null;
            var weapons=new Dictionary<string,VisualWeapon>();
            foreach(Transform entry in holder)
            {
                var model=entry.Find("Model");if(model==null)continue;
                model.gameObject.SetActive(false);var w=new VisualWeapon{Mount=entry,Model=model.gameObject};
                var fit=entry.Find("Fit");if(fit!=null&&fit.localScale.x>.05f&&Mathf.Abs(fit.localScale.x-1)>1e-4f){w.Centre=fit.localPosition;w.Scale=fit.localScale.x;}
                var support=entry.Find("Support");if(support!=null){w.Supported=true;w.Support=support.localPosition;}
                var stock=entry.Find("Stock");if(stock!=null&&stock.localPosition.z<0){w.Stocked=true;w.Stock=-stock.localPosition.z;}
                weapons[entry.name]=w;
            }
            return weapons.Count>0?weapons:null;
        }

        // The entry pilot `a` shows on its visual body this frame: a weapon in hand on the Standard skin that the bake carries.
        private VisualWeapon VisualWeaponFor(VisualBody body,Actor a)
        {
            if(body.Weapons==null||a.WeaponAway||string.IsNullOrEmpty(a.SourceSpec)||WeaponSkin(a)!=NativeWeaponSkins.StandardIndex)return null;
            return body.Weapons.TryGetValue(a.SourceSpec,out var w)?w:null;
        }

        // The visual hands' grasp on it: the support fist cups its handguard and its butt shoulders (weapon frame; the source
        // frame mirrors x).
        private static void VisualWeaponGrasp(ref NativeVisualRig.Grasp g,VisualWeapon w)
        {
            if(w.Supported&&g.SupportHold.Seated)g.SupportHold.Center=new System.Numerics.Vector3(-w.Support.x,w.Support.y,w.Support.z);
            if(w.Stocked&&g.Stock>0)g.Stock=w.Stock;
        }

        // Once the right hand bone is on the visual weapon frame (H): the fitted frame H T(c) S(s) T(-c), for what draws there.
        private static void VisualWeaponFit(Transform hand,VisualWeapon w)
        {
            if(w==null||w.Scale==1)return;
            hand.position+=hand.TransformVector((1-w.Scale)*w.Centre);hand.localScale*=w.Scale;
        }

        // SourceCharacter, for a pilot drawn with its visual body: its weapon model at `frame` (where the Standard model would
        // draw: the fitted hand, or the held melee hand with the reach stretch), shown this frame. False: the game's model draws.
        private bool VisualWeaponDrawn(CapturedAnimation.Player rig,Matrix4x4 frame)
        {
            if(!visualBodies.TryGetValue(rig,out var body)||body.Held==null||body.PosedFrame!=Time.frameCount)return false;
            var w=body.Held;
            if(w.Scale!=1)frame=frame*Matrix4x4.Translate(w.Centre)*Matrix4x4.Scale(Vector3.one/w.Scale)*Matrix4x4.Translate(-w.Centre);
            Vector3 x=frame.GetColumn(0),y=frame.GetColumn(1),z=frame.GetColumn(2);
            if(!(y.sqrMagnitude>1e-10f&&z.sqrMagnitude>1e-10f))return false;
            w.Mount.SetPositionAndRotation(frame.GetColumn(3),Quaternion.LookRotation(z,y));
            var parent=w.Mount.parent.lossyScale;w.Mount.localScale=new Vector3(x.magnitude/parent.x,y.magnitude/parent.y,z.magnitude/parent.z);
            if(body.Shown!=w){if(body.Shown!=null)body.Shown.Model.SetActive(false);body.Shown=w;}
            if(!w.Model.activeSelf)w.Model.SetActive(true);
            body.ShownFrame=Time.frameCount;return true;
        }

        // VisualBodiesShown: a model not drawn this frame hides (the weapon put away, another skin, the body hidden).
        private static void VisualWeaponShown(VisualBody body)
        {
            if(body.Shown==null||body.ShownFrame==Time.frameCount&&body.Seen)return;
            if(body.Shown.Model!=null)body.Shown.Model.SetActive(false);body.Shown=null;
        }
    }
}
