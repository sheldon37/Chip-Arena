using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ChipArena.UnityConversion;

// Engine-free checks for the PvE chip selection look (Core/NativeChipSelect.cs; docs/PVE-CHIP-SELECT.md): the replicated
// state's mapping, the blend track, the gesture beat, the pose layer over every class's real Core route (bounds, planted legs,
// the weapon kept in its hand and off the floor, the taps landing on the panel, no pops at 30-240 Hz, back to the route's pose
// exactly), first person on every view base, the hologram's tiers and the tag's timing, and no allocation per frame. Unity's
// drawing (the shader, the IMGUI tag, the occlusion ray) is not exercised here.
internal static class ChipSelectChecks
{
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Chip select: "+what);}
    private const float D2R=(float)Math.PI/180;
    public static IEnumerable<string> Run(ConversionContent content)
    {
        var specs=content.weapons.Select(w=>w.id).Where(id=>id!="siren").ToArray();
        Check(specs.Length>=16&&specs.Contains("cyberlord")&&specs.Contains("bow"),"the class list ("+string.Join(",",specs)+")");
        yield return Mapping();
        yield return Track();
        yield return Gestures();
        foreach(var line in Poses(specs))yield return line;
        yield return FirstPerson(specs);
        yield return Hologram();
        yield return Tag();
    }

    // ---- the replicated state's mapping ----
    private static string Mapping()
    {
        int on=0;
        for(int m=0;m<128;m++)
        {
            bool member=(m&1)!=0,open=(m&2)!=0,alive=(m&4)!=0,connected=(m&8)!=0,holding=(m&16)!=0,cinematic=(m&32)!=0,ended=(m&64)!=0;
            bool want=member&&open&&alive&&connected&&!holding&&!cinematic&&!ended;bool got=NativeChipSelect.Selecting(member,open?17:0,alive,connected,holding,cinematic,ended);
            Check(got==want,"mapping "+m);if(got)on++;
        }
        Check(on==1&&!NativeChipSelect.Selecting(true,-3,true,true,false,false,false),"only an open pick id selects");
        return "Mapping: the look shows only for a party row with an open pick (replicated NativePveRowFrame.open > 0), alive, connected, outside holds, cinematics and the end (128 combinations)";
    }

    // ---- the blend track ----
    private static string Track()
    {
        foreach(int hz in new[]{30,60,144,240})
        {
            float dt=1f/hz;var k=new NativeChipSelectTrack();float last=0,full=-1,gone=-1;
            for(int f=0;f<=(int)(6*hz);f++)
            {
                float t=f*dt;bool sel=t>=1&&t<4;int pick=t<2.5f?5:6;float w=k.Step(sel,sel?pick:0,t);Check(MathUtil.Finite(w)&&w>=0&&w<=1,"weight "+w);
                if(t>=1&&t<4){Check(w>=last-1e-5f,hz+" Hz: the weight never drops while open");if(w>=.9999f&&full<0)full=t;}
                if(t>=4){Check(w<=last+1e-5f,hz+" Hz: the weight never rises once closed");if(w<=1e-5f&&gone<0)gone=t;}
                if(t>=1&&t<4)Check(Math.Abs(k.OpenedAt-1)<=dt+1e-4f,hz+" Hz: the open edge is latched at the first open frame ("+k.OpenedAt+")");
                if(t>=2.5f+dt&&t<4)Check(Math.Abs(k.ChangedAt-2.5f)<=dt+1e-4f&&w>=.999f,hz+" Hz: a queued pick re-deals without dropping the look");
                last=w;
            }
            Check(full>=1+NativeChipSelect.BlendIn-dt&&full<=1+NativeChipSelect.BlendIn+2*dt,hz+" Hz: up in "+(full-1)+" s");
            Check(gone>=4+NativeChipSelect.BlendOut-dt&&gone<=4+NativeChipSelect.BlendOut+2*dt,hz+" Hz: down in "+(gone-4)+" s");
        }
        var d=new NativeChipSelectTrack();for(float t=0;t<=.6f;t+=1/60f)d.Step(true,3,t);Check(d.Weight>.99f,"up");Check(d.Step(true,3,.62f,true)==0,"a death drops the look at once");
        var c=new NativeChipSelectTrack();for(float t=10;t<=10.6f;t+=1/60f)c.Step(true,3,t);Check(c.Weight>.99f,"up again");Check(c.Step(true,3,2)==0&&c.OpenedAt==2,"a clock that jumps back (killcam, new round) restarts the look from its edge");
        for(float t=2;t<=2.2f;t+=1/60f)c.Step(true,3,t);Check(c.Weight>0&&c.Weight<1,"and eases in again");
        // A hitch (a long frame) eases at most .1 s, so the raise never snaps; a pilot unposed for a while (dead, revived) starts over.
        var h=new NativeChipSelectTrack();h.Step(true,3,0);h.Step(true,3,.3f);Check(h.Ramp<=.1f/NativeChipSelect.BlendIn+1e-5f,"a hitch is not a snap");
        var r=new NativeChipSelectTrack();for(float t=0;t<=.6f;t+=1/60f)r.Step(true,3,t);Check(r.Step(false,0,.6f+NativeChipSelectTrack.Gap+.1f)==0,"no look left over after a gap (a revival)");
        return "Track at 30/60/144/240 Hz: eases in over "+NativeChipSelect.BlendIn+" s and out over "+NativeChipSelect.BlendOut+" s (monotonic), latches the open edge (no open time is replicated), re-deals a queued pick without a drop, a death drops it, a clock jump or a gap (revival) restarts it";
    }

    // ---- the gesture beat ----
    private static string Gestures()
    {
        int gestures=0,changes=0,slots=0;
        foreach(ChipSelectHands hands in Enum.GetValues(typeof(ChipSelectHands)))
        for(int s=0;s<40;s++)
        {
            int seed=NativeChipSelect.Seed(s+1,s*7+3);int lit=NativeChipSelect.Highlight(0,seed,hands,3);var prev=new ChipSelectGesture();float dt=.01f;bool inside=false;float start=0;
            for(float t=0;t<240;t+=dt)
            {
                var g=NativeChipSelect.GestureAt(t,seed,hands,3);var again=NativeChipSelect.GestureAt(t,seed,hands,3);Check(g.Kind==again.Kind&&g.Age==again.Age&&g.Card==again.Card,"deterministic");
                if(g.Kind!=ChipSelectGestureKind.None)
                {
                    var want=hands==ChipSelectHands.Taps?new[]{ChipSelectGestureKind.Tap,ChipSelectGestureKind.Swipe}:hands==ChipSelectHands.Swipes?new[]{ChipSelectGestureKind.Swipe}:new[]{ChipSelectGestureKind.Flick};
                    Check(want.Contains(g.Kind),hands+" makes "+g.Kind);Check(g.Reach>=0&&g.Reach<=1&&g.Contact>=0&&g.Contact<=1&&g.Age>=0&&g.Age<g.Length,"gesture ranges");
                    if(!inside){inside=true;start=t;gestures++;}
                    float slot=hands==ChipSelectHands.Flicks?NativeChipSelect.FlickSlot:NativeChipSelect.TapSlot;Check(Math.Floor((t-g.Age+1e-4f)/slot)==Math.Floor((t-g.Age+g.Length-1e-4f)/slot),"a gesture stays inside its slot");
                }
                else inside=false;
                int now=NativeChipSelect.Highlight(t,seed,hands,3);Check(now>=0&&now<3,"lit card range");
                if(now!=lit){changes++;Check(g.Kind!=ChipSelectGestureKind.None&&g.SinceContact>=-1e-3f&&g.SinceContact<=dt+1e-3f,hands+": the lit card changes only at a contact ("+g.Kind+" "+g.SinceContact+")");Check(g.Card==now,"the gesture lights the card it touches");lit=now;}
                if(g.Kind!=ChipSelectGestureKind.None&&g.SinceContact>=0)Check(g.Card==now,"after the contact the gesture's card is lit");
                prev=g;
            }
            slots+=(int)(240/(hands==ChipSelectHands.Flicks?NativeChipSelect.FlickSlot:NativeChipSelect.TapSlot));
        }
        int runs=3*40;Check(gestures>=slots-runs&&gestures<=slots+runs,"every slot acts ("+gestures+" in "+slots+" whole slots, "+runs+" runs)");Check(changes>=gestures-runs,"every contact moves the light ("+changes+" changes, "+gestures+" gestures)");
        for(int s=0;s<200;s++)for(int i=1;i<50;i++)Check(NativeChipSelect.SlotCard(s,i,3)!=NativeChipSelect.SlotCard(s,i-1,3),"two slots in a row light different cards");
        Check(NativeChipSelect.GestureAt(float.NaN,1,ChipSelectHands.Taps,3).Kind==ChipSelectGestureKind.None&&NativeChipSelect.GestureAt(-1,1,ChipSelectHands.Taps,3).Kind==ChipSelectGestureKind.None,"no gesture before the beat or on a bad clock");
        return "Gestures over 240 s, 40 seeds per hand kind: deterministic (every client on the pick's replicated beat), taps and swipes for empty hands, swipes for a pistol or dagger, flicks for long weapons, one per slot ("+gestures+" in "+slots+" slots), the lit card moves only at a contact ("+changes+" times) and never repeats the last";
    }

    // ---- the pose over every class's Core route ----
    private sealed class Pilot
    {
        public readonly CapturedRig.Pose Pose=new CapturedRig.Pose();public readonly Actor A;public readonly string Route;
        private MeleeMotion melee;private readonly MeleeView view=new MeleeView();private GunMotion gun;private MagicMotion magic;private NativeBowMotion bow;private CyberLordMotion cyber;
        private readonly NativeWaveDash wave=new NativeWaveDash();private readonly float[] channels=new float[13];private readonly NativeJuggleTrack jt=new NativeJuggleTrack();private float last=-100;
        public Pilot(string spec)
        {
            var w=spec=="pistol"?WeaponId.Pistol:spec=="rifle"?WeaponId.Rifle:spec=="machinegun"?WeaponId.MachineGun:WeaponId.Sword;int mv=spec=="fire"?1:spec=="water"?2:spec=="lightning"?3:spec=="darkness"?4:0;
            A=new Actor{Id=1,SourceSpec=spec,Weapon=w,MagicVisual=mv,Grounded=true,Hp=100,CastRate=1,VisualSet=spec=="daggers"?1:spec=="greatsword"?2:spec=="shield"?3:0,Ammo=w==WeaponId.Sword?0:GunRules.Get(w).Magazine,MeleeKit=new NativeMeleeKit(),LastShot=-100};
            Route=spec=="pistol"||spec=="rifle"||spec=="machinegun"?"gun":spec=="emperor"||spec=="ansatsuken"||spec==CyberLordSpec.Id?"fighter":spec=="bow"?"bow":MeleeStance.MagicSpec(spec)||spec=="leet"?"magic":"melee";
        }
        public void Base(float t)
        {
            float dt=last<-90||t<last||t-last>.35f?1/60f:Math.Min(.1f,t-last);last=t;var group=new Vector3(-A.Position.x,A.Position.y+.865f,A.Position.z);
            if(Route=="melee"||Route=="fighter")
            {
                melee??=new MeleeMotion{Impacts=new MeleeImpacts(),JumpLayer=new NativeJumpMotion(),Locomotion=new NativeLocomotionCarry(),SprintLayer=new NativeSprintMotion()};
                var v=view;v.Id="1";v.Spec=A.SourceSpec;v.Yaw=0;v.Pitch=0;v.Vel=new Vector3(-A.Velocity.x,A.Velocity.y,A.Velocity.z);v.Grounded=true;v.Hp=A.Hp;v.Sprinting=false;v.LastAttack=A.AttackAt;v.Windup=A.VisualWindup;v.AttackRate=1;v.AttackIndex=A.Combo+1;
                v.SwingAt=v.SwingUntil=float.NaN;v.Commit=false;v.Guarding=false;v.BlockTime=0;v.LastBlock=float.NaN;v.HitStunUntil=0;v.RollAt=v.RollUntil=-100;v.ActionKind=null;v.Reaction=false;v.Down=false;v.Juggle=false;v.WeaponAway=false;v.Reach=1;
                melee.Rig.GroupPosition=group;melee.Rig.GroupYaw=0;melee.Rig.WeaponReach=1;
                if(Route=="melee")melee.Sample(v,t,dt,r=>{});
                else
                {
                    NativeFighterPose.View(v,A,wave,t);melee.FighterBase(v,t,dt);
                    if(A.SourceSpec==CyberLordSpec.Id){cyber??=new CyberLordMotion();cyber.Style(melee,A,t,false);}
                    melee.FighterFinish(v,t,dt);
                }
                melee.Rig.WritePose(Pose);
            }
            else if(Route=="gun"){gun??=new GunMotion{JumpLayer=new NativeJumpMotion(),SprintLayer=new NativeSprintMotion(),Locomotion=new NativeRangedLocomotion()};gun.Rig.GroupPosition=group;gun.Rig.GroupYaw=0;gun.Pose(A,t,Pose,false,jt,out _);gun.Finish(Pose,t);}
            else if(Route=="bow"){bow??=new NativeBowMotion{JumpLayer=new NativeJumpMotion(),SprintLayer=new NativeSprintMotion(),Locomotion=new NativeRangedLocomotion()};bow.Rig.GroupPosition=group;bow.Rig.GroupYaw=0;bow.Pose(A,t,Pose,false,jt,out _);bow.Finish(Pose,t);}
            else{magic??=new MagicMotion{JumpLayer=new NativeJumpMotion(),SprintLayer=new NativeSprintMotion(),Locomotion=new NativeRangedLocomotion()};magic.Rig.GroupPosition=group;magic.Rig.GroupYaw=0;magic.Pose(A,t,Pose,channels,0,false,jt,out _);magic.Finish(Pose,t);}
            Pose.World();
        }
    }
    private static void Copy(CapturedRig.Pose from,CapturedRig.Pose to){for(int b=0;b<CapturedRig.Bones.Length;b++){to.Position[b]=from.Position[b];to.Rotation[b]=from.Rotation[b];}to.World();}
    private static float Angle(Quaternion a,Quaternion b){float d=Math.Abs(Quaternion.Dot(Quaternion.Normalize(a),Quaternion.Normalize(b)));return 2*(float)Math.Acos(Math.Min(1,d));}
    private static IEnumerable<string> Poses(string[] specs)
    {
        float fastest=0,worstWrist=0,worstFloor=99,worstTap=0,worstSlide=0,minPitch=99,maxPitch=0;long allocated=0;string fastestAt="";int frames=0,presses=0,slides=0;
        var layer=new NativeChipSelectPose();var shown=new CapturedRig.Pose();
        foreach(var spec in specs)foreach(int hz in new[]{30,60,144,240})
        {
            var pilot=new Pilot(spec);var track=new NativeChipSelectTrack();float dt=1f/hz;int seed=NativeChipSelect.Seed(4,9);var carry=NativeChipSelect.Carry(spec);bool left=NativeChipSelect.DisplayLeft(spec);
            int hand=left?CapturedRig.LHand:CapturedRig.RHand,other=left?CapturedRig.RHand:CapturedRig.LHand;var design=NativeWeaponModels.Get(spec);
            // Grip kept: the display never takes the weapon's own hand (a pair rides both: the dagger and gauntlet stay in it).
            if(design!=null&&!design.Dual)Check(design.Bone!=hand,spec+": the display wrist is the weapon's hand");
            var prev=new Vector3[CapturedRig.Bones.Length];bool first=true;
            for(int f=0;f<=(int)(8.6f*hz);f++)
            {
                float t=f*dt;bool sel=t>=1&&t<8;float w=track.Step(sel,sel?7:0,t);pilot.Base(t);Copy(pilot.Pose,shown);
                long before=GC.GetAllocatedBytesForCurrentThread();
                var input=new ChipSelectInput{Weight=w,Clock=t-.3f,Time=t,Reach=1,Spec=spec,Seed=seed,Cards=3};layer.Apply(shown,input);layer.Measure(shown,false);
                if(frames>200)allocated+=GC.GetAllocatedBytesForCurrentThread()-before;frames++;
                shown.World();var b=pilot.Pose;var ci=Quaternion.Inverse(shown.WorldRotation[CapturedRig.Chest]);var chest=shown.WorldPosition[CapturedRig.Chest];
                for(int k=0;k<CapturedRig.Bones.Length;k++)
                {
                    Check(MathUtil.Finite(shown.WorldPosition[k].X)&&MathUtil.Finite(shown.WorldPosition[k].Y)&&MathUtil.Finite(shown.WorldPosition[k].Z)&&MathUtil.Finite(shown.Rotation[k].W),spec+" "+hz+" Hz: a finite pose");
                    // Planted: legs, pelvis and the body root are the route's own, exactly.
                    if(!NativeChipSelectPose.Writes(k))Check(shown.Rotation[k]==b.Rotation[k]&&shown.Position[k]==b.Position[k],spec+": the layer writes "+CapturedRig.Bones[k].Name);
                    if(w<=0)Check(shown.Rotation[k]==b.Rotation[k],spec+": closed, the route's pose exactly ("+CapturedRig.Bones[k].Name+")");
                }
                // Pops: the layer's own offset from the route's pose never jumps between frames (the route may: the Cyber Lord's glitch).
                if(!first)foreach(int k in new[]{CapturedRig.LHand,CapturedRig.RHand,CapturedRig.Head})
                {float v=((shown.WorldPosition[k]-b.WorldPosition[k])-prev[k]).Length()/dt;if(v>fastest){fastest=v;fastestAt=spec+" "+hz+" Hz t "+t.ToString("0.000")+" "+CapturedRig.Bones[k].Name;}}
                for(int k=0;k<CapturedRig.Bones.Length;k++)prev[k]=shown.WorldPosition[k]-b.WorldPosition[k];first=false;
                // Hands and lowered weapons above the floor (the rig root is .865 m up).
                foreach(int k in new[]{CapturedRig.LHand,CapturedRig.RHand})worstFloor=Math.Min(worstFloor,shown.WorldPosition[k].Y+.865f);
                if(w>=.999f)
                {
                    var wrist=Vector3.Transform(shown.WorldPosition[hand]-chest,ci);var goal=NativeChipSelectPose.Raised;if(!left)goal.X=-goal.X;worstWrist=Math.Max(worstWrist,(wrist-goal).Length());
                    var panel=Vector3.Transform(layer.PanelCentre-chest,ci);var eye=Vector3.Transform(layer.Eye-chest,ci);
                    Check(panel.Z>.2f&&panel.Y>0&&panel.Y<eye.Y-.12f,spec+": the panel floats in front of the chest, under the eyes ("+panel+")");
                    Check(Vector3.Dot(layer.PanelNormal,Vector3.Normalize(layer.Eye-layer.PanelCentre))>.7f,spec+": the panel faces its wearer");
                    Check(Vector3.Dot(layer.PanelNormal,Vector3.Transform(Vector3.UnitY,shown.WorldRotation[CapturedRig.Chest]))<=NativeChipSelectPose.PanelSteep+1e-3f,spec+": the panel stands up for the pilots round it");
                    var face=Vector3.Transform(Vector3.Transform(Vector3.UnitZ,shown.WorldRotation[CapturedRig.Head]),ci);float pitch=(float)Math.Asin(Math.Clamp(-face.Y,-1,1));minPitch=Math.Min(minPitch,pitch);maxPitch=Math.Max(maxPitch,pitch);
                    // The weapon kept: a staff or bow hand keeps its idle hold exactly; a lowered weapon's tip stays off the floor.
                    if(NativeChipSelect.KeepsIdle(carry))for(int k=other-3;k<=other;k++)Check(shown.Rotation[k]==b.Rotation[k],spec+": the "+carry+" hand keeps its idle hold");
                    if(carry!=ChipSelectCarry.Free&&carry!=ChipSelectCarry.Fist&&!NativeChipSelect.KeepsIdle(carry))
                    {
                        float length=NativeChipSelectPose.CarryLength(carry,spec,1);var tip=shown.WorldPosition[other]+Vector3.Transform(Vector3.UnitZ,shown.WorldRotation[other])*length;worstFloor=Math.Min(worstFloor,tip.Y+.865f);
                        // An armed hand reaching the panel keeps its weapon pointing down, below the cards.
                        if(layer.Gesture.Kind==ChipSelectGestureKind.Swipe)Check(Vector3.Transform(Vector3.UnitZ,shown.WorldRotation[other]).Y<-.2f,spec+": the swiping hand keeps its weapon pointing down");
                    }
                    // The taps land: the press on the lit card, the swipe's slide along the panel.
                    var g=layer.Gesture;float card=NativeChipSelectPose.CardOffset(g.Card,3);
                    if(g.Kind==ChipSelectGestureKind.Tap&&g.Contact>=.999f&&g.Reach>=.999f){presses++;worstTap=Math.Max(worstTap,Math.Max(Math.Abs(layer.TipDepth)*100,Math.Abs(layer.TipU-card)*10));}
                    if(g.Kind==ChipSelectGestureKind.Swipe&&g.Contact>.3f&&g.Contact<.95f&&g.Reach>=.999f){slides++;worstSlide=Math.Max(worstSlide,Math.Abs(layer.TipDepth)*100);}
                }
            }
        }
        Check(fastest<7f,"the layer moves a hand or the head "+fastest.ToString("0.00")+" m/s off the route between frames (a pop) at "+fastestAt);
        Check(worstWrist<.03f,"the display wrist misses its place by "+worstWrist+" m");Check(worstFloor>.06f,"a hand or a lowered weapon reaches "+worstFloor+" m (the floor)");
        Check(presses>0&&worstTap<1.6f,"a press misses the panel or its card ("+worstTap+" cm / tenths, "+presses+" presses)");Check(slides>0&&worstSlide<2.2f,"a swipe leaves the panel ("+worstSlide+" cm)");
        Check(minPitch>.25f&&maxPitch<.95f,"the head looks down "+minPitch+".."+maxPitch+" rad");Check(allocated==0,"the layer allocated "+allocated+" bytes");
        yield return "Pose over the Core route of all "+specs.Length+" classes at 30/60/144/240 Hz ("+frames+" frames): legs, pelvis and body untouched (feet planted), the route's pose exactly when closed, the layer moves a hand or the head at most "+fastest.ToString("0.00")+" m/s off the route (no pops), the display wrist within "+(worstWrist*100).ToString("0.0")+" cm of its place, the head down "+(minPitch/D2R).ToString("0")+"-"+(maxPitch/D2R).ToString("0")+" deg, 0 bytes per frame";
        yield return "Grip kept: the display never takes the weapon's hand, the staff and bow hands keep their idle hold exactly, a swiping pistol or dagger points down, hands and lowered tips stay "+(worstFloor*100).ToString("0")+" cm or more off the floor; "+presses+" presses land within "+worstTap.ToString("0.0")+" (cm / tenths of a card) and "+slides+" swipe frames within "+worstSlide.ToString("0.0")+" cm of the panel";
    }

    // ---- first person on every view base ----
    private static string FirstPerson(string[] specs)
    {
        float worst=0,lowest=99,widest=0;int n=0;string worstAt="";var layer=new NativeChipSelectPose();
        foreach(var spec in specs)
        {
            // The view rig's generic path (CapturedAnimation.ActorPose with firstPerson): the captured idle of its set, the gun hold, the
            // bow's view, the staff's view or the Cyber Lord's fists, the torso settled; placed at the class's view base and scale.
            var pilot=new Pilot(spec);var p=pilot.Pose;var a=pilot.A;int set=Math.Clamp(a.VisualSet,0,3);var idle=CapturedClips.Sets[set].First(c=>c.StateName=="idle");var row=new float[CapturedClip.Channels];
            idle.Sample(CapturedClip.Wrap(2,idle.Duration),row);CapturedRig.Sample(row,set,p);bool bow=spec=="bow",gun=GunRules.Ranged(a.Weapon)&&!bow,magic=MagicPose.School(a)>0;
            if(spec==CyberLordSpec.Id)new CyberLordMotion().FirstPerson(p,a,2);
            else{if(gun)RangedPose.Apply(p,a,2,1);if(bow)NativeBowPose.View(p,a,2);if(magic)MagicPose.Apply(p,a,2,true,new float[13],0);NativeFirstPersonPose.SettleTorso(p);}
            p.World();
            var offset=bow?NativeBowPose.ViewOffset:gun?NativeViewBase.Gun:magic?NativeFirstPersonPose.MagicViewOffset(0):NativeViewBase.IsFighter(spec)?NativeViewBase.Fighter:NativeViewBase.Melee;float scale=bow?NativeBowPose.ViewScale:NativeFirstPersonPose.Scale;
            // The camera at the origin looking +z (Unity frame); the view rig's root at the class's base, scaled. Rig frame = source axes.
            Vector3 Local(Vector3 cam){var l=(cam-offset)/scale;return new Vector3(-l.X,l.Y,l.Z);}Vector3 Cam(Vector3 rig){var u=new Vector3(-rig.X,rig.Y,rig.Z)*scale+offset;return u;}
            var input=new ChipSelectInput{Weight=1,Clock=1.5f,Time=2,Reach=1,Spec=spec,Seed=3,Cards=3,FirstPerson=true,ViewWrist=Local(NativeChipSelectPose.ViewWrist(spec)),ViewEye=Local(Vector3.Zero)};
            layer.Apply(p,input);p.World();
            int hand=NativeChipSelect.DisplayLeft(spec)?CapturedRig.LHand:CapturedRig.RHand,other=hand==CapturedRig.LHand?CapturedRig.RHand:CapturedRig.LHand;
            // The wrist goes to its place (as far as the arm reaches: the bow's view rig stands its eye at the camera, its shoulder behind).
            var wrist=Cam(p.WorldPosition[hand]);var shoulder=Cam(p.WorldPosition[hand-2]);var goal=NativeChipSelectPose.ViewWrist(spec);float reach=.515f*scale;
            float miss=(goal-shoulder).Length()<=reach?(wrist-goal).Length():Math.Abs((wrist-shoulder).Length()-reach)+Vector3.Distance(Vector3.Normalize(wrist-shoulder),Vector3.Normalize(goal-shoulder))*.1f;if(miss>worst){worst=miss;worstAt=spec;}
            var panel=Cam(layer.PanelCentre);float across=NativeChipSelectPose.ViewWidth*layer.PanelScale*scale/panel.Length();widest=Math.Max(widest,across);
            Check(panel.Z>.12f&&panel.Y/panel.Z>-.75f&&panel.Y/panel.Z<-.1f&&Math.Abs(panel.X/panel.Z)<.75f&&across<.42f,spec+": the view's panel sits low in view ("+panel+"; wrist "+wrist+", shoulder "+Cam(p.WorldPosition[hand-2])+")");
            Check(Vector3.Dot(layer.PanelNormal,Vector3.Normalize(layer.Eye-layer.PanelCentre))>.95f,spec+": the view's panel faces the camera");
            // A weapon hand goes down out of the view (75 degrees: below y/z = -.77), unless it is empty (it rests just under the view).
            var weapon=Cam(p.WorldPosition[other]);var carry=NativeChipSelect.Carry(spec);
            if(carry!=ChipSelectCarry.Free&&carry!=ChipSelectCarry.Fist){lowest=Math.Min(lowest,-weapon.Y/Math.Max(.05f,weapon.Z));Check(weapon.Z<.05f||weapon.Y/weapon.Z<-.55f,spec+": the weapon hand leaves the view ("+weapon+")");}
            Check(layer.Gesture.Kind!=ChipSelectGestureKind.Swipe||NativeChipSelect.Hands(carry)==ChipSelectHands.Taps,spec+": an armed hand does not reach into the view");n++;
        }
        Check(worst<.06f,"the view's display wrist misses its place by "+worst+" m ("+worstAt+")");
        return "First person on every view base ("+n+" classes): the display wrist within "+(worst*100).ToString("0.0")+" cm of its place low in view (left, right for a right display; as far as the arm reaches), the panel low in view facing the camera, at most "+(2*Math.Atan(widest/2)/D2R).ToString("0")+" deg wide, a weapon hand down out of view, an armed hand never reaching into it";
    }

    // ---- the hologram ----
    private sealed class Sink:INativeChipHoloSink
    {
        public readonly Vector3[] P=new Vector3[1024];public readonly Vector4[] C=new Vector4[1024],U=new Vector4[1024],E=new Vector4[1024];public int N,T;
        public int Vertex(Vector3 at,Vector4 color,Vector4 uv,Vector4 extra){P[N]=at;C[N]=color;U[N]=uv;E[N]=extra;return N++;}
        public void Triangle(int a,int b,int c){T++;}
        public void Clear(){N=T=0;}
    }
    private static string Hologram()
    {
        var s=new Sink();var frame=new ChipHoloFrame{Emitter=new Vector3(0,1,0),Centre=new Vector3(0,1.1f,0),Right=Vector3.UnitX,Up=Vector3.UnitY,Normal=-Vector3.UnitZ,Width=NativeChipSelectPose.PanelWidth,Height=NativeChipSelectPose.PanelHeight};
        var look=new ChipHoloLook{Weight=1,Age=2,Redeal=float.PositiveInfinity,Time=3.3f,Count=3,Seed=12,C0=0x70b5ff,C1=0xce8aff,C2=0xffae55};
        int Count(ChipHoloLook l,out bool beam,out bool motes,out bool ring){s.Clear();int q=NativeChipSelectHolo.Build(s,frame,l);beam=motes=ring=false;for(int i=0;i<s.N;i++){beam|=s.U[i].Z==NativeChipSelectHolo.KindBeam;motes|=s.U[i].Z==NativeChipSelectHolo.KindMote;ring|=s.U[i].Z==NativeChipSelectHolo.KindRing;}Check(s.N==q*4&&s.T==q*2,"quads written as reported");Check(q==NativeChipSelectHolo.Quads(l),"Quads() counts what Build writes");return q;}
        int full=Count(look,out bool b1,out bool m1,out bool r1);var red=look;red.Reduced=true;int reduced=Count(red,out bool b2,out bool m2,out _);var low=look;low.Low=true;int lowQ=Count(low,out bool b3,out bool m3,out bool r3);
        Check(full>reduced&&reduced>lowQ&&b1&&m1&&r1&&b2&&!m2&&!b3&&!m3&&!r3,"tiers: Full "+full+", Reduced "+reduced+" (no motes), Low "+lowQ+" (no cone, ring or motes)");
        for(int i=0;i<s.N;i++)Check((s.P[i]-frame.Emitter).Length()<.4f&&MathUtil.Finite(s.P[i].X)&&s.C[i].W>=0&&s.C[i].W<=1&&s.E[i].X>0&&s.E[i].X<5,"vertex "+i+" in range");
        // The offered cards wear their rarity colours (sRGB), danger red under attack.
        s.Clear();NativeChipSelectHolo.Build(s,frame,look);var cards=new List<Vector4>();for(int i=0;i<s.N;i+=4)if(s.U[i].Z==NativeChipSelectHolo.KindCard)cards.Add(s.C[i]);
        Check(cards.Count==3,"three cards");bool Is(Vector4 c,int hex)=>Math.Abs(c.X-((hex>>16)&255)/255f)<1e-4f&&Math.Abs(c.Y-((hex>>8)&255)/255f)<1e-4f&&Math.Abs(c.Z-(hex&255)/255f)<1e-4f;
        Check(Is(cards[0],0x70b5ff)&&Is(cards[1],0xce8aff)&&Is(cards[2],0xffae55),"the cards' rarity colours");
        int redOn=0,redOff=0;for(float t=0;t<1;t+=.01f){if(NativeChipSelectHolo.Red(true,t,false))redOn++;else redOff++;Check(NativeChipSelectHolo.Red(true,t,true)&&!NativeChipSelectHolo.Red(false,t,false),"steady red with Reduced effects, none unhit");}
        Check(redOn>35&&redOff>35,"the 6 Hz red blink");
        var hit=look;hit.Attacked=true;hit.Time=0;s.Clear();NativeChipSelectHolo.Build(s,frame,hit);for(int i=0;i<s.N;i++)if(s.U[i].Z==NativeChipSelectHolo.KindCard)Check(Is(s.C[i],NativeChipSelectHolo.Danger),"red cards when hit");
        // Folded before the wrist is up, unfolding with the weight; a re-deal folds the cards and deals them again.
        var fold=look;fold.Weight=NativeChipSelect.HoloFrom-.01f;s.Clear();Check(NativeChipSelectHolo.Build(s,frame,fold)==0,"folded until the wrist is up");
        float u0=0;for(float w=0;w<=1.0001f;w+=.01f){float u=NativeChipSelectHolo.Unfold(w);Check(u>=u0-1e-5f&&u<=1,"unfold monotonic");u0=u;}Check(Math.Abs(u0-1)<1e-4f,"unfolds fully");
        Check(NativeChipSelectHolo.Deal(1,.4f*NativeChipSelect.Redeal,0)<.05f&&NativeChipSelectHolo.Deal(1,NativeChipSelect.Redeal+.01f,2)>.99f,"the re-deal");
        Check(Math.Abs(NativeChipSelectHolo.Flicker(1.234f,5,true)-.93f)<1e-6f,"no flicker with Reduced effects");
        float fmin=1,fmax=0;for(float t=0;t<10;t+=.003f){float f=NativeChipSelectHolo.Flicker(t,5,false);fmin=Math.Min(fmin,f);fmax=Math.Max(fmax,f);}Check(fmin<.4f&&fmax>.95f&&fmax<=1,"flicker and the glitch dip");
        // Crowd: four selecting pilots in one mesh, allocation-free.
        int most=NativeChipSelectHolo.Quads(new ChipHoloLook{Count=3,Gesture=new ChipSelectGesture{Kind=ChipSelectGestureKind.Tap,SinceContact=0}}),crowd=0;
        long before=GC.GetAllocatedBytesForCurrentThread();
        for(int f=0;f<200;f++){s.Clear();for(int k=0;k<4;k++){var l=look;l.Seed=k;l.Time=f*.016f;l.Gesture=NativeChipSelect.GestureAt(f*.016f+k,k,ChipSelectHands.Taps,3);NativeChipSelectHolo.Build(s,frame,l);}crowd=Math.Max(crowd,s.N);}
        long bytes=GC.GetAllocatedBytesForCurrentThread()-before;Check(bytes==0,"the hologram build allocated "+bytes+" bytes");
        Check(crowd<=4*most*4,"four pilots' quads ("+crowd+" vertices, at most "+4*most*4+")");
        return "Hologram: Full "+full+" quads (panel, header, three rarity cards, cone, edges, ring, "+NativeChipSelectHolo.Motes+" motes), Reduced "+reduced+" (still: no motes, flicker or scan travel), Low "+lowQ+" (panel and cards); red at 6 Hz under attack (steady with Reduced effects), folded until the wrist is up, re-dealt for a queued pick; four pilots at most "+crowd+" vertices in one mesh, 0 bytes";
    }

    // ---- the tag ----
    private static string Tag()
    {
        string text=NativeChipSelectTag.Text;Check(text=="SELECTING CHIP"&&NativeChipSelectTag.Typed.Length==text.Length+1,"the text");
        for(int i=0;i<=text.Length;i++)Check(NativeChipSelectTag.Typed[i]==text.Substring(0,i),"prefix "+i);
        int last=0;float done=-1;for(float a=0;a<3;a+=.005f)
        {
            int n=NativeChipSelectTag.Shown(a,false);Check(n>=last&&n<=text.Length,"typing monotonic");if(n==text.Length&&done<0)done=a;last=n;
            Check(!NativeChipSelectTag.Cursor(a,false)||a<NativeChipSelectTag.TypedBy+NativeChipSelectTag.CursorHold,"the cursor stops after the typing");
            for(int k=0;k<3;k++){float d=NativeChipSelectTag.Dot(k,a,false);Check(d>=0&&d<=1&&(a>=NativeChipSelectTag.TypedBy||d==0),"dots after the text");}
            Check(NativeChipSelectTag.Shown(a,true)==text.Length&&!NativeChipSelectTag.Cursor(a,true)&&NativeChipSelectTag.Dot(1,a,true)==.7f,"Reduced: the whole text, no cursor, still dots");
        }
        Check(Math.Abs(done-NativeChipSelectTag.TypedBy)<.04f&&done<.75f,"typed in "+done+" s");
        Check(NativeChipSelectTag.DistanceFade(5)==1&&NativeChipSelectTag.DistanceFade(20)==1&&NativeChipSelectTag.DistanceFade(30)==0&&NativeChipSelectTag.DistanceFade(40)==0&&NativeChipSelectTag.DistanceFade(25)>.3f&&NativeChipSelectTag.DistanceFade(25)<.7f&&NativeChipSelectTag.DistanceFade(float.NaN)==0,"distance fade");
        Check(NativeChipSelectTag.FadeTo<32,"gone before the names (32 m)");
        Check(NativeChipSelectTag.Alpha(0,5,5,1)==0&&NativeChipSelectTag.Alpha(1,0,5,1)==0&&NativeChipSelectTag.Alpha(1,1,5,1)==1&&NativeChipSelectTag.Alpha(1,1,5,0)==0,"alpha: weight, fade-in, visibility");
        float v=0;int steps=0;while(v<1&&steps<100){v=NativeChipSelectTag.Visible(v,true,1/60f);steps++;}Check(steps>=8&&steps<=10,"the occlusion eases over .15 s ("+steps+" frames)");
        long before=GC.GetAllocatedBytesForCurrentThread();float sum=0;for(int i=0;i<2000;i++){float a=i*.002f;sum+=NativeChipSelectTag.Typed[NativeChipSelectTag.Shown(a,false)].Length+NativeChipSelectTag.Dot(i%3,a,false)+NativeChipSelectTag.Alpha(1,a,12,1)+NativeChipSelectTag.Glow(a,false);}
        long bytes=GC.GetAllocatedBytesForCurrentThread()-before;Check(bytes==0&&sum>0,"the tag's state allocated "+bytes+" bytes");
        return "Tag: SELECTING CHIP typed in by "+done.ToString("0.00")+" s (cursor while typing), then three dots in turn; Reduced effects shows it whole and still; fades 20-30 m (inside the names' 32 m), occlusion eased over .15 s; every prefix preallocated (0 bytes)";
    }
}
