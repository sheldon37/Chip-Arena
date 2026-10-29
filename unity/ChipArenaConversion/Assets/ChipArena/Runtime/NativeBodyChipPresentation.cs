using System;
using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    // Pilot rig dressing on the source models (renderer.ts:83,128-129,155 at 05950ea): the body chips (Core/NativeBodyChips.cs),
    // the right-hand spell charge and the element hand motes (Core/NativeSpellCharge.cs), and the block-cooldown dimming with the
    // Ansatsuken parry flash (Core/NativeBlockTint.cs). Arena rigs (host, guests and the killcam replay alike) and the first-person
    // rig wear them; the entrance and sandbox viewer rigs stay bare. Presentation only: everything reads the replicated actor
    // (block clocks, cast stamps, fighter action, roll and wavedash), the seat chips and the stats every client recomputes, and
    // the Block events every client already feeds the captured-melee impacts; nothing reaches the authority or the network frame.
    public sealed partial class ArenaPresentation
    {
        private readonly BodyChip[] bodyChips=new BodyChip[NativeBodyChips.MaxPieces];private readonly SpellMote[] handMotes=new SpellMote[8];
        private readonly NativeBodyChipGear.Part[] bodyChipParts=new NativeBodyChipGear.Part[NativeBodyChipGear.MaxParts];
        private readonly Dictionary<int,NativeWaveDash> chipWaves=new Dictionary<int,NativeWaveDash>();private readonly Dictionary<int,NativeSpellMotes> moteClocks=new Dictionary<int,NativeSpellMotes>();
        private readonly Dictionary<int,string> impactIds=new Dictionary<int,string>();
        // This rig's block-cooldown factor and parry flash, set by RigTint before the rig's parts are queued.
        private float rigFactor=1,rigFlash;private Color rigTintColor=Color.white;private Func<int,Color> rigTint;
        private Func<int,Matrix4x4> flashSource,flashShell;
        // The flash overlay sits a hair outside the rig's own surfaces so it never depth-fights them.
        private static readonly Matrix4x4 FlashShell=Matrix4x4.Scale(Vector3.one*1.003f);
        private string ImpactId(int id){if(!impactIds.TryGetValue(id,out var s)){s=id.ToString();impactIds.Add(id,s);}return s;}
        // The Ansatsuken's parry (arcade parryAt): its Block event, which NativeChips emits from the parrying seat.
        private float ParryAt(Actor a,float time){if(a.SourceSpec!="ansatsuken")return -100;float at=meleeImpacts.BlockAt(ImpactId(a.Id),time,NativeBlockTint.FlashSeconds);return float.IsNaN(at)?-100:at;}
        /// <summary>
        /// block-cooldown-tint.ts: the per-part scene tint every rig part (body, hat, weapon) is drawn with this frame, combining a
        /// burning Last Laugh victim's char (SirenCharTint) with the block-cooldown dimming; null when neither applies. Sets
        /// rigFactor (chip modules and the muzzle flash dim with the rig) and rigFlash. Allocation-free.
        /// </summary>
        private Func<int,Color> RigTint(Actor a,float time,bool first)
        {
            rigFlash=NativeBlockTint.Flash(a.Alive,ParryAt(a,time),time);
            rigFactor=NativeBlockTint.Factor(a.Alive,NativeBlockTint.Cooldown(a,time),NativeBlockTint.Blocking(a,time),rigFlash,time);
            var siren=first?null:SirenCharTint(a.Id);if(rigFactor>=1&&siren==null)return null;
            var c=siren!=null?siren(0):Color.white;rigTintColor=new Color(c.r*rigFactor,c.g*rigFactor,c.b*rigFactor,1);return rigTint??(rigTint=_=>rigTintColor);
        }
        private void ClearRigTint(){rigFactor=1;rigFlash=0;}
        /// <summary>
        /// The parry flash: the source pulls every rig colour toward 0x9fe9ff and adds a 0x63cfff glow over .22 s; the native rig
        /// draws its own silhouette over itself in NativeBlockTint.OverlayColor at FlashLerp opacity (the Demon Step ghost shader),
        /// which gives the same colour on a part under unit light. Body, gloves and hat, on the rig's layer and camera.
        /// </summary>
        private void ParryFlash(CapturedAnimation.Player rig,Actor a,int layer,Camera camera,Func<int,Matrix4x4> bone,Func<int,bool> visible,Func<int,Matrix4x4> weaponBone,bool first)
        {
            float k=NativeBlockTint.FlashLerp(rigFlash);if(!(k>0)||sourceModels==null)return;
            if(!ghostsLoaded){ghostsLoaded=true;ghostPilot=sourceModels.Ghost("pilot");ghostGloves=sourceModels.Ghost("weapon-ansatsuken");}if(ghostPilot==null)return;
            var c=NativeBlockTint.OverlayColor(rigFlash);var color=new Color(c.X,c.Y,c.Z,1);var root=rig.Root.transform.localToWorldMatrix;
            if(flashShell==null)flashShell=b=>flashSource(b)*FlashShell;
            // The parried pilot's own body silhouette (its skin at its tier, NativeBodySkinPresentation.cs).
            string shell=BodyGhost(sheldonScripted?NativeBodySkins.SheldonIndex:BodySkin(a.Id),rig==introRig?BodyCloseTier:BodyTier(rig.Root.transform.position,first))??ghostPilot;
            flashSource=bone;sourceModels.Queue(shell,root,flashShell,visible,layer,camera,color,null,k);
            if(weaponBone!=null&&ghostGloves!=null){flashSource=weaponBone;sourceModels.Queue(ghostGloves,root,flashShell,null,layer,camera,color,null,k);}
            if(!first&&!string.IsNullOrEmpty(a.Hat)&&a.Hat!="auditor-halo")
            {
                if(!ghostHats.TryGetValue(a.Hat,out var hat)){hat=sourceModels.Ghost("hat-"+a.Hat);ghostHats[a.Hat]=hat;}
                if(hat!=null){flashSource=bone;sourceModels.Queue(hat,root,flashShell,null,layer,camera,color,null,k);}
            }
            flashSource=null;
        }
        /// <summary>
        /// bodyModifiers for one rig (after its model is queued): the spell charge in the right hand, the hand motes (third-person
        /// rigs; the first-person view takes them from the hidden body, HiddenSweep), and the body chips on their joints (the
        /// first-person rig keeps only the chest's). On the player's model (owner 2026-10-29: "NO VOXEL appearances") the chips
        /// are its own sculpted parts on its chest, shoulders and feet (Core/NativeBodyChipGear, BodyChipModel); the first-person
        /// view body (forearms and hands) wears none of them. The Classic voxel body keeps the source's pieces.
        /// </summary>
        private void BodyDressing(CapturedAnimation.Player rig,Actor a,float time,bool first,int layer,Camera camera)
        {
            var pilot=LoadoutPilot(a.Id);int color=pilot?.Weapon!=null?(int)pilot.Weapon.color:-1;
            bool charging=NativeSpellCharge.Charging(a,time,out float charge);var pose=NativeSpellCharge.Sample(a,time,charging,charge);
            if(pose.Visible&&color>=0)
            {
                var socket=rig.Bones[CapturedRig.RHand].localToWorldMatrix*Matrix4x4.TRS(CapturedAnimation.Vec(CapturedRig.Mirror(NativeSpellCharge.Socket)),CapturedAnimation.Quat(CapturedRig.Mirror(pose.Rotation)),Vector3.one*pose.Scale);
                // The darkness cast is the void orb's dark gather (NativeMagicFx, owner 2026-10-12): no glowing ball or white streaks,
                // its two halos ring the forming black core.
                bool voidCast=MagicLooks&&a.SourceSpec=="darkness";
                if(!voidCast)QueuePiece(new WeaponPiece{Shape=WeaponShape.Sphere,Glow=true,Color=color,Scale=new System.Numerics.Vector3(NativeSpellCharge.OrbScale),Rotation=System.Numerics.Quaternion.Identity},socket,layer,camera);
                QueuePiece(new WeaponPiece{Shape=WeaponShape.Ring,Glow=true,Color=color,Scale=NativeSpellCharge.HaloScale,Rotation=pose.Halo0},socket,layer,camera);
                QueuePiece(new WeaponPiece{Shape=WeaponShape.Ring,Glow=true,Color=color,Scale=NativeSpellCharge.HaloScale,Rotation=pose.Halo1},socket,layer,camera);
                // Converging streaks and shrinking rings round the orb (NativeChargeFx, weapons milestone).
                if(!voidCast)ChargeFx(socket,a,color,time,first);
            }
            if(!first)HandMotes(rig,a,time,color);
            if(pilot?.Stats==null||pilot.Seat?.chips==null)return;int n=NativeBodyChips.Build(pilot.Seat.chips,pilot.Stats,bodyChips);if(n==0)return;
            if(first&&VisualViewDrawn(rig))return;var model=first?null:VisualPosedBody(rig);int parts=0;
            if(!chipWaves.TryGetValue(a.Id,out var wave)){wave=new NativeWaveDash();chipWaves.Add(a.Id,wave);}float dash=NativeBodyChips.DashTime(a,wave,time);
            for(int i=0;i<n;i++)
            {
                var c=bodyChips[i];if(first&&!NativeBodyChips.FirstPerson(c.Bone))continue;NativeBodyChips.Animate(ref c,time,dash);
                if(model!=null){parts=NativeBodyChipGear.Parts(c,time,bodyChipParts,parts);continue;}
                if(c.Visible)QueuePiece(c.Piece,first?rig.Bones[c.Bone].localToWorldMatrix:VisualJointFrame(rig,c.Bone),layer,camera);
            }
            if(model!=null)BodyChipModel(model,rig,parts,layer,camera);
        }
        // The model's parts on their anchors: the chest core's frame, the shoulders' and the ankles' (the model's joints, the game
        // joints' axes), each in its Chip's colour.
        private readonly Matrix4x4[] bodyChipAnchors=new Matrix4x4[5];
        private void BodyChipModel(VisualBody body,CapturedAnimation.Player rig,int count,int layer,Camera camera)
        {
            if(count==0)return;int done=0;
            for(int i=0;i<count;i++)
            {
                var p=bodyChipParts[i];int k=(int)p.Anchor;
                if((done&(1<<k))==0){bodyChipAnchors[k]=p.Anchor==NativeBodyChipGear.Anchor.Core?VisualCoreFrame(body,rig):VisualLimbFrame(body,rig,LimbOf(p.Anchor));done|=1<<k;}
                var matrix=bodyChipAnchors[k]*Matrix4x4.TRS(CapturedAnimation.Vec(CapturedRig.Mirror(p.Position)),CapturedAnimation.Quat(CapturedRig.Mirror(p.Rotation)),Vector3.one);
                sourceModels.Queue(p.Id,matrix,null,null,layer,camera,body:NativeArena.ColorOf(p.Color).linear);
            }
        }
        private static int LimbOf(NativeBodyChipGear.Anchor a)=>a==NativeBodyChipGear.Anchor.LeftArm?CapturedRig.LArm:a==NativeBodyChipGear.Anchor.RightArm?CapturedRig.RArm:a==NativeBodyChipGear.Anchor.LeftFoot?CapturedRig.LFoot:CapturedRig.RFoot;
        // The visual body `rig` draws this frame (an arena pilot's, an entrance's, the draft's), or null.
        private VisualBody VisualPosedBody(CapturedAnimation.Player rig)=>rig!=null&&visualBodies.TryGetValue(rig,out var b)&&b.Root!=null&&b.Root.activeInHierarchy&&b.PosedFrame==Time.frameCount?b:null;
        // A joint of the model: at its bone (the shoulder's, the ankle's), turned as the game joint rides it (VisualJointFrame), at
        // the body's scale.
        private Matrix4x4 VisualLimbFrame(VisualBody body,CapturedAnimation.Player rig,int joint)
        {
            var frame=VisualJointFrame(rig,joint);int v=joint<body.Binding.Rig.Of.Length?body.Binding.Rig.Of[joint]:-1;
            Vector3 at=v>=0&&v<body.Binding.Bones.Length&&body.Binding.Bones[v]!=null?body.Binding.Bones[v].position:(Vector3)frame.GetColumn(3);
            Vector3 z=frame.GetColumn(2),y=frame.GetColumn(1);var turn=z.sqrMagnitude>1e-10f&&y.sqrMagnitude>1e-10f?Quaternion.LookRotation(z,y):Quaternion.identity;
            return Matrix4x4.TRS(at,turn,Vector3.one*body.Root.transform.lossyScale.x);
        }
        // The chest's front where the pilot gear's core sits (VisualGearCore, the bake's per top); without it the model's chest
        // bone, a hand forward and down.
        private Matrix4x4 VisualCoreFrame(VisualBody body,CapturedAnimation.Player rig)
        {
            if(body.GearCore!=null)return body.GearCore.localToWorldMatrix;
            var chest=VisualLimbFrame(body,rig,CapturedRig.Chest);return chest*Matrix4x4.Translate(new Vector3(0,-.06f,.12f));
        }
        /// <summary>spellMotes: element sparks from the casting hand (renderer.ts:128 spellBurst), at the source effects quality.</summary>
        private void HandMotes(CapturedAnimation.Player rig,Actor a,float time,int color)
        {
            if(MagicPose.School(a)==0||color<0)return;
            if(!moteClocks.TryGetValue(a.Id,out var clock)){clock=new NativeSpellMotes();moteClocks.Add(a.Id,clock);}
            int n=clock.Step(a,time,Time.unscaledDeltaTime*clockRate,BlastQuality,color,handMotes);if(n==0)return;var hand=rig.Bones[CapturedRig.RHand].localToWorldMatrix;
            for(int i=0;i<n;i++){var m=handMotes[i];SourceBurst(hand.MultiplyPoint3x4(CapturedAnimation.Vec(CapturedRig.Mirror(m.Local)))+Vector3.one*m.Jitter,m.Color,m.Count,m.Power);}
        }
        private void ClearRigDressing(){chipWaves.Clear();foreach(var clock in moteClocks.Values)clock.Reset();ClearRigTint();}
    }
}
