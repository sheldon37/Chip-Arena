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
    // A spec the bake carries none of (the daggers, shield, bow and caster staffs) draws the weapons pack bank's model on an
    // arena pilot's body instead (NativePackWeaponPresentation.cs): the daggers on both hands, the staffs on the left.
    // Presentation only: aim, rules, reach, hit volumes and the wire are the game's.
    public sealed partial class ArenaPresentation
    {
        private sealed class VisualWeapon
        {
            public Transform Mount; public GameObject Model; public Vector3 Centre; public float Scale=1; public bool Supported, Stocked; public Vector3 Support; public float Stock;
            // A pack weapon's (NativePackWeaponPresentation.cs): its off-hand copy (daggers), whether the left fist closes on it
            // (a staff's shaft) and the bow's limb bones.
            public bool Pack, Seat; public Transform LeftMount, Upper, Lower; public GameObject LeftModel;
            public void Hide(){if(Model!=null)Model.SetActive(false);if(LeftModel!=null)LeftModel.SetActive(false);}
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

        // The entry pilot `a` shows on its visual body this frame: a weapon in hand on the Standard skin that the bake carries,
        // else (an arena pilot's body) the pack bank's (PackWeaponOf); the main dagger thrown hides its copy (MainAway).
        private VisualWeapon VisualWeaponFor(VisualBody body,Actor a)
        {
            body.MainAway=a.MainWeaponAway;
            if(a.WeaponAway||string.IsNullOrEmpty(a.SourceSpec)||WeaponSkin(a)!=NativeWeaponSkins.StandardIndex)return null;
            if(body.Weapons!=null&&body.Weapons.TryGetValue(a.SourceSpec,out var w))return w;
            return body.Arena?PackWeaponOf(body,a.SourceSpec):null;
        }

        // The visual hands' grasp on it: the support fist cups its handguard and its butt shoulders (weapon frame; the source
        // frame mirrors x).
        // A pack staff's shaft: the left fist closes on it at the game's main grip (its index side toward the head).
        private static void VisualWeaponGrasp(ref NativeVisualRig.Grasp g,VisualWeapon w)
        {
            if(w.Supported&&g.SupportHold.Seated)g.SupportHold.Center=new System.Numerics.Vector3(-w.Support.x,w.Support.y,w.Support.z);
            if(w.Stocked&&g.Stock>0)g.Stock=w.Stock;
            if(w.Seat){g.LeftGrip=1;g.Left=NativeVisualRig.Blade(System.Numerics.Quaternion.Identity);}
        }

        // Once the right hand bone is on the visual weapon frame (H): the fitted frame H T(c) S(s) T(-c), for what draws there.
        private static void VisualWeaponFit(Transform hand,VisualWeapon w)
        {
            if(w==null||w.Scale==1)return;
            hand.position+=hand.TransformVector((1-w.Scale)*w.Centre);hand.localScale*=w.Scale;
        }

        // SourceCharacter, for a pilot drawn with its visual body: its weapon model at `frame` (where the Standard model would
        // draw: the fitted hand, or the held melee hand with the reach stretch; a staff's StaffMatrix), shown this frame, and a
        // pack weapon's off-hand copy at `left` (the daggers' second blade; none: the game's model draws). False: the game's
        // model draws.
        private bool VisualWeaponDrawn(CapturedAnimation.Player rig,Matrix4x4 frame,Matrix4x4? left=null)
        {
            if(!visualBodies.TryGetValue(rig,out var body)||body.Held==null||body.PosedFrame!=Time.frameCount)return false;
            var w=body.Held;
            if(w.LeftMount!=null&&(!left.HasValue||!VisualWeaponMount(w.LeftMount,left.Value)))return false;
            if(w.Scale!=1)frame=frame*Matrix4x4.Translate(w.Centre)*Matrix4x4.Scale(Vector3.one/w.Scale)*Matrix4x4.Translate(-w.Centre);
            if(!VisualWeaponMount(w.Mount,frame))return false;
            if(body.Shown!=w){if(body.Shown!=null)body.Shown.Hide();body.Shown=w;}
            // The main dagger thrown: its copy goes with it (the off-hand's stays).
            bool main=!(w.LeftMount!=null&&body.MainAway);
            if(w.Model.activeSelf!=main)w.Model.SetActive(main);
            if(w.LeftModel!=null&&!w.LeftModel.activeSelf)w.LeftModel.SetActive(true);
            body.ShownFrame=Time.frameCount;return true;
        }
        // A model's mount on `frame` (its axes' lengths as the mount's scale); false for a degenerate frame.
        private static bool VisualWeaponMount(Transform mount,Matrix4x4 frame)
        {
            Vector3 x=frame.GetColumn(0),y=frame.GetColumn(1),z=frame.GetColumn(2);
            if(!(y.sqrMagnitude>1e-10f&&z.sqrMagnitude>1e-10f))return false;
            mount.SetPositionAndRotation(frame.GetColumn(3),Quaternion.LookRotation(z,y));
            var parent=mount.parent.lossyScale;mount.localScale=new Vector3(x.magnitude/parent.x,y.magnitude/parent.y,z.magnitude/parent.z);
            return true;
        }

        // VisualBodiesShown: a model not drawn this frame hides (the weapon put away, another skin, the body hidden).
        private static void VisualWeaponShown(VisualBody body)
        {
            if(body.Shown==null||body.ShownFrame==Time.frameCount&&body.Seen)return;
            body.Shown.Hide();body.Shown=null;
        }
    }
}
