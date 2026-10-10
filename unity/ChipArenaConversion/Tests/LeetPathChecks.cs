#if ARENA_TESTS
using System;
using System.Numerics;
using System.Reflection;
namespace ChipArena.UnityConversion
{
    // L33t H4x outside the pose route (owner playtest 2026-10-10: "Explosion effect seems to play with L33t H4x E button, and
    // they seem to still be using sword animations?"). Tests/LeetMotionChecks.cs covers the route and the magic gait; this
    // covers the other paths a pilot is presented on, and its E. The source poses L33t H4x as a magic staff pilot everywhere
    // (content.ts class 'magic'), and its E without Admin Pause or Scare Tactics is Root Command: leet.ts secondary calls
    // explode(p, pos, 6, 35, 1200, 'root'), whose 'explosion' event (simulation.ts explode, message 'root') event-vfx.ts
    // blastEvent draws as blastStyle('root') = the default 'fire' chip blast (quality 1, no tint), and audio.ts plays its
    // BLAST_CUE default (explosion .65). So the E explosion is the source's; Admin Pause and Scare Tactics have none.
    internal static class LeetPathChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("L33t H4x paths: "+what);count++;}
        private static float Angle(Quaternion a,Quaternion b)=>2*(float)Math.Acos(Math.Min(1,Math.Abs(Quaternion.Dot(Quaternion.Normalize(a),Quaternion.Normalize(b)))));
        // Component distance up to sign (acos near 1 is too coarse in float for "the same pose").
        private static float Apart(Quaternion a,Quaternion b)=>Math.Min((a-b).Length(),(a+b).Length());
        private static Actor Leet()=>new Actor{Id=12,SourceSpec="leet",Weapon=WeaponId.Sword,Grounded=true,Hp=100,CastRate=1,RollDirection=new V3(0,0,1)};
        public static void Run(ConversionContent data)
        {
            Content(data);Replicated();Lobby();Dressing();Corpse();RootCommand();
            Console.WriteLine("PASS L33t H4x paths ("+count+" checks: magic class with no Q/E ability kit, guest/killcam copies and the corpse keep the magic route, lobby and portrait poses hold the staff like the schools, no melee reach, sweep or blade tip, staff drops from the left hand; E Root Command is the source's fire blast and explosion cue, nuke as the source, Leet beams and bolt impacts draw no blast)");
        }
        // content.ts: class 'magic', DEV only, E 'root' (the Root Command secondary, not an ability kit), so lock-on, the melee
        // swing cue, the melee specials and the ability casts (all keyed on class or kit) never apply.
        private static void Content(ConversionContent data)
        {
            var w=data.Weapon("leet");
            Check(w.@class=="magic"&&w.devOnly,"content weapon leet is a DEV magic weapon");
            Check(w.secondary=="root","E is Root Command (secondary 'root')");
            Check(data.Ability("leet","e")==null&&data.Ability("leet","q")==null,"no ability kit, so no ability cast or ability blast on Q/E");
            Check(!NativeMeleeRules.Standard("leet")&&!NativeSpecialRules.CanBegin("leet",true,false,false,false,0,100,0),"no sword-family E special (sword-launch and the others)");
        }
        // Guests and the killcam present the replicated actor (JsonUtility: public fields only, an absent string arrives as "")
        // through ViewCopy (ReplicaSnapshot, the interpolated host snapshot, ApplyFrame): the magic route needs only fields
        // that cross the wire.
        private static Actor Wire(Actor a)
        {
            var o=new Actor();
            foreach(var f in typeof(Actor).GetFields(BindingFlags.Public|BindingFlags.Instance)){if(f.IsInitOnly)continue;var v=f.GetValue(a);if(f.FieldType==typeof(string)&&v==null)v="";f.SetValue(o,v);}
            return o;
        }
        private static void Replicated()
        {
            var a=Leet();a.Velocity=new V3(0,0,6.8f);a.AttackAt=4;a.BlockUntil=0;
            foreach(var (name,copy) in new[]{("guest",Wire(a).ViewCopy()),("killcam",Wire(Wire(a)).ViewCopy()),("interpolated host",a.ViewCopy())})
            {
                Check(copy.SourceSpec=="leet"&&copy.MagicVisual==0&&copy.Weapon==WeaponId.Sword,name+" copy keeps the replicated spec");
                Check(NativePoseRoute.For(copy,false)==PoseRoute.Magic&&MagicPose.School(copy)==MagicPose.Leet,name+" walks on the magic gait");
                Check(!NativePoseRoute.SwordLayers(copy)&&!MeleeMotion.Handles(copy.SourceSpec),name+" never takes the sword set");
            }
        }
        // C14 lobby poses and portraits (lobby-poses.ts heroStance: L33t H4x is class 'magic'): every pose is the schools'
        // (joint for joint the fire staff's), the staff rides the left wrist's hand socket, no off-hand; the sword pose braces
        // its blade in the right hand instead.
        private static void Lobby()
        {
            var leet=new MeleeRig();var fire=new MeleeRig();
            Check(NativeLobbyPoses.Magic("leet")&&!NativeLobbyPoses.Fighter("leet")&&!NativeLobbyPoses.OffHand("leet")&&!NativeLobbyPoses.Gun("leet"),"lobby class is magic");
            for(int pose=0;pose<NativeLobbyPoses.Keys.Length;pose++)foreach(float t in new[]{0f,.7f,2.35f})
            {
                string what=NativeLobbyPoses.Keys[pose]+"@"+t;
                Check(NativeLobbyPoses.Effective("leet",NativeLobbyPoses.Keys[pose])==pose&&!NativeLobbyPoses.UsesIdle("leet",pose),what+": own pose, no baked idle");
                NativeLobbyPoses.Sample(leet,"leet",pose,t,out var lw,out var lo);NativeLobbyPoses.Sample(fire,"fire",pose,t,out var fw,out var fo);
                float worst=0;for(int b=0;b<MeleeRig.N;b++){worst=Math.Max(worst,Apart(leet.Q[b],fire.Q[b]));Check(Vector3.Distance(leet.P[b],fire.P[b])<1e-5f,what+" joint "+b+" position as the fire staff");}
                Check(worst<1e-5f,what+": posed as the fire staff (worst quaternion difference "+worst+")");
                Check(lw.Visible==(pose==NativeLobbyPoses.Weapon)&&lw.Bone==CapturedRig.LHand&&lw.Position==NativeLobbyPoses.HandSocket&&!lo.Visible,what+": staff in the left hand only");
            }
            var sword=new MeleeRig();NativeLobbyPoses.Sample(sword,"sword",NativeLobbyPoses.Weapon,0,out var sw,out _);NativeLobbyPoses.Sample(leet,"leet",NativeLobbyPoses.Weapon,0,out _,out _);
            float apart=0;foreach(int b in new[]{CapturedRig.LArm,CapturedRig.LElbow,CapturedRig.RArm,CapturedRig.RElbow})apart=Math.Max(apart,Angle(sword.Q[b],leet.Q[b]));
            Check(sw.Bone==CapturedRig.RHand&&apart>.3f,"the standing pose is not the sword's ("+apart+" rad)");
        }
        // C11 weapon dressing (weapon-visuals.ts, melee-trails.ts): a staff, never a melee blade.
        private static void Dressing()
        {
            Check(!NativeWeaponDressing.Melee("leet")&&!NativeWeaponDressing.Fist("leet")&&!NativeWeaponDressing.Gun("leet"),"not a melee, fist or gun weapon");
            Check(NativeWeaponDressing.ReachScale("leet",1.7f,false)==1&&NativeWeaponDressing.ReachScale("leet",1.7f,true)==1,"no melee reach stretch");
            Check(!NativeWeaponDressing.BladeTip("leet",out _),"no blade tip, so no sweep ribbon");
            Check(Math.Abs(NativeWeaponDressing.MuzzleZ("leet")-1.52f)<1e-6f,"matrix staff muzzle at 1.52 m");
            var a=Leet();a.AttackAt=10;a.AttackUntil=10.6f;float t=a.AttackAt+a.VisualWindup;
            var s=Leet();s.SourceSpec="sword";s.AttackAt=10;s.AttackUntil=10.6f;
            Check(NativeWeaponDressing.Sweeping(s,t)&&!NativeWeaponDressing.Sweeping(a,t),"a cast sweeps no blade ribbon (a sword cut at the same time does)");
        }
        // The death corpse (NativeDeathPresentation poses a rig once at the death time with Hp clamped to 1, ragdoll.ts drops the
        // weapon from its hand) and the C18 kill boxes: the magic route and the left hand, like the schools.
        private static void Corpse()
        {
            var a=Leet();a.Hp=0;var shown=a.ViewCopy();shown.Hp=Math.Max(1,shown.Hp);
            Check(NativePoseRoute.For(a,false)==PoseRoute.Magic&&NativePoseRoute.For(shown,false)==PoseRoute.Magic,"the corpse rig is posed on the magic gait");
            int s=NativeKillBoxes.SpecIndex("leet");
            Check(s>=0&&NativeKillBoxes.Magic[s]&&NativeKillBoxes.Magic[NativeKillBoxes.SpecIndex("fire")]&&!NativeKillBoxes.Magic[NativeKillBoxes.SpecIndex("sword")],"kill boxes keep the staff in the left hand");
        }
        private static int Sounds(EventKind kind,string detail,string spec,NativeEventSounds.Sound[] o)=>NativeEventSounds.Route(new NativeEventSounds.Context{Kind=kind,Detail=detail,SourceSpec=spec},o);
        // E. Root Command (NativeLeet StepLeetPilot: ChipBlast at the pilot, radius 6, effect 'leet-block'; the effect record has
        // element 0 and no tag) draws exactly the source's 'root' blast; the nuke the source's 'leet-nuke' blast (fire, then
        // the matrix mushroom); the bolt lock, possession and Slenderman hop beams and the bolt impacts draw no blast.
        private static void RootCommand()
        {
            Check(NativeBlastSim.StyleOf("root")==NativeBlastStyle.Fire&&NativeBlastSim.TintOf("root")==null,"source: blastStyle('root') is the default fire, untinted");
            Check(NativeBlastCues.Classify(false,6,0,null)==NativeBlastCues.Cue.Blast,"Root Command record is a blast");
            Check(NativeBlastCues.Style(null,0)==NativeBlastSim.StyleOf("root"),"Root Command draws the source's 'root' style (fire)");
            Check(NativeBlastCues.Quality(null)==1&&NativeBlastSim.TintOf(null)==null,"at full quality, untinted, as a tagged source explosion");
            Check(NativeBlastCues.Classify(false,11,0,null)==NativeBlastCues.Cue.Blast&&NativeBlastCues.Style(null,0)==NativeBlastSim.StyleOf("leet-nuke"),"the nuke's own blast is the source's 'leet-nuke' fire");
            foreach(var (r,what) in new[]{(.25,"bolt lock"),(1.0,"possession"),(1.0,"Slenderman hop")})
                Check(NativeBlastCues.Classify(true,r,2,null)==NativeBlastCues.Cue.None,what+" beam draws no blast");
            var o=new NativeEventSounds.Sound[NativeEventSounds.MaxSounds];
            int n=Sounds(EventKind.Explosion,"leet-block","leet",o);
            Check(n==1&&o[0].Clip=="explosion"&&Math.Abs(o[0].Gain-.65f)<1e-6f&&o[0].Positional&&o[0].Gate=="explosion","Root Command plays the source's BLAST_CUE default (explosion .65), once");
            n=Sounds(EventKind.Explosion,"root","leet",o);Check(n==1&&o[0].Clip=="explosion"&&Math.Abs(o[0].Gain-.65f)<1e-6f,"the source tag routes the same");
            n=Sounds(EventKind.Explosion,"leet-nuke","leet",o);
            Check(n==2&&o[0].Clip=="explosion"&&Math.Abs(o[0].Gain-.65f)<1e-6f&&o[1].Clip=="explosion"&&o[1].Gain==1,"the nuke adds its leet-nuke play (1.0)");
            Check(Sounds(EventKind.Leet,"bolt","leet",o)==0,"bolt impacts are silent (no source event)");
            n=Sounds(EventKind.Shot,null,"leet",o);Check(n==1&&o[0].Clip=="lightning","the staff shot is the lightning cue, not a blade");
        }
    }
}
#endif
