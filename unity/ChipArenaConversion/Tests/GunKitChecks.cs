#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The pistol and machine-gun Q kit's presentation (Claude; Astra's simulation, PR #134: Core/NativeRangedRules,
    // Runtime/NativeRangedAbilities): Core/NativeGunKit (the kit read off the replicated actor), Core/NativeGunKitMotion (poses),
    // Core/NativeGunKitFx (effects, tracer look), Core/NativeGunKitHud (Q cards) and Runtime/NativeGunKitPresentation (hooks).
    // The fan is replayed on the host's real fixed-tick schedule (NativeRangedRules.Fan / NativeFanBurst.Due) for every magazine
    // and a cancelled burst; Overdrive on its replicated clocks. Every phase owns a pose (third person and the first-person view
    // model), poses and effects key off replicated actor fields only (a public-field copy draws the same frame), the rifle keeps
    // ADS while pistol and machine gun never zoom, the HUD cards follow the kit, and the pose, effect and HUD paths allocate
    // nothing per frame. Rendering, Unity and Windows are not run here.
    internal static class GunKitChecks
    {
        private static int count;private static string measured="";
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Gun kit presentation: "+what);count++;}
        private static string Code(string text)=>string.Join("\n",text.Split('\n').Select(l=>{int c=l.IndexOf("//",StringComparison.Ordinal);return c<0?l:l.Substring(0,c);}));
        private const float Dt=1/60f;

        // ---- the host's fan on its fixed tick (NativeRangedAbilities.StepFan), as the snapshot carries it at `time` ----
        private static Actor Fan(int ammo,float at,float time,int cancelAfter=-1,float dt=Dt)
        {
            var a=new Actor{Id=3,SourceSpec="pistol",Weapon=WeaponId.Pistol,Hp=100,Grounded=true,CastRate=1,Ammo=ammo,AbilityId=NativeGunKit.Fan,AbilityAt=at,AbilityRelease=at};
            var burst=NativeRangedRules.Fan(at,ammo);
            for(int k=0;;k++)
            {
                float t=at+k*dt;if(t>time+1e-5f)break;
                if(a.ReloadUntil>0&&t>=a.ReloadUntil){a.Ammo=7;a.ReloadUntil=0;}
                while((cancelAfter<0||burst.Shots<cancelAfter)&&burst.Due(t)&&a.Ammo>0){a.Ammo--;a.LastShot=t;burst.Shots++;if(a.Ammo<=0){a.ReloadAt=t;a.ReloadUntil=t+1.1f;}}
            }
            return a;
        }
        private static Actor Drive(float at,float until,float vent)=>new Actor{Id=4,SourceSpec="machinegun",Weapon=WeaponId.MachineGun,Hp=100,Grounded=true,CastRate=1,Ammo=48,AbilityId=NativeGunKit.Overdrive,AbilityAt=at,AbilityRelease=at,OverdriveAt=at,OverdriveUntil=until,VentUntil=vent};
        private static Actor DriveAt(float time,float at=10,float until=14,float vent=14.8f)
        {var a=Drive(at,until,vent);if(time>=at){float gap=NativeRangedRules.Interval(.085,1,true);a.LastShot=time<until?at+(float)Math.Floor((time-at)/gap)*gap:until-.01f;}return a;}

        public static void Run(string root,ConversionContent data)
        {
            Contract();Poses();Effects();Tracers();Hud(data);Ads(root,data);Derived(root);Sounds(root);Allocation();Sources(root);
            Console.WriteLine("PASS Gun kit presentation ("+count+" checks: Fan the Hammer read from the replicated cast, LastShot and magazine on the host's tick schedule for 1-7 rounds, cancelled and reloading bursts; Overdrive phases on NativeRangedRules' clocks; every phase owns a third- and first-person pose with continuous hands and the slap on each round; effects and tracers a pure function of replicated actors with quality tiers; rifle-only ADS; HUD cards; derived pack curve; bank audio; no per-frame allocation; hooks and metas; "+measured+")");
        }
        private static void Contract()
        {
            // Every magazine and tick rate: the volley's length is the cast's rounds (at most four), the fired count follows the
            // host, the presentation's newest round matches it, and the kit ends after its smoke.
            foreach(int ammo in new[]{1,2,3,4,5,7})foreach(float dt in new[]{1/30f,1/60f,1/120f})
            {
                int expect=Math.Min(4,ammo);
                for(float t=10;t<10+NativeGunKit.FanSmoke+.4f;t+=1/120f)
                {
                    var a=Fan(ammo,10,t,-1,dt);var s=NativeGunKit.Of(a,t);
                    int host=0;var burst=NativeRangedRules.Fan(10,ammo);for(int k=0;10+k*dt<=t+1e-5f;k++){float tk=10+k*dt;while(burst.Due(tk)&&burst.Shots<ammo)burst.Shots++;}host=burst.Shots;
                    if(t<10+NativeGunKit.FanSmoke-.05f)Check(s.HasFan&&s.FanFired==host,$"fired count follows the host ({ammo} rounds, {1/dt:0} Hz, t {t-10:0.000}: {s.FanFired} vs {host})");
                    if(s.HasFan&&host==expect)Check(s.FanShots==expect,$"volley length {expect} of {ammo} rounds ({1/dt:0} Hz)");
                    if(s.HasFan){var ph=NativeGunKit.FanAt(s,t,out int shot,out _);if(ph==NativeGunKit.FanPhase.Fan&&host>0)Check(Math.Abs(shot+1-host)<=1,$"newest round on screen matches the host ({shot+1} vs {host})");}
                }
                Check(!NativeGunKit.Of(Fan(ammo,10,10+NativeGunKit.FanSmoke+1.1f,-1,dt),10+NativeGunKit.FanSmoke+1.1f).HasFan,"the kit ends after the smoke");
            }
            // A burst cut after two rounds (roll, block, stun) ends on screen once the third is overdue; R ends it at once.
            var cut=Fan(7,10,10.3f,2);var cs=NativeGunKit.Of(cut,10.3f);Check(cs.HasFan&&cs.FanShots==2,"a cancelled burst ends at its last round");
            var early=NativeGunKit.Of(Fan(7,10,10.16f,2),10.16f);Check(early.FanShots==4,"a round inside its arrival window is still expected");
            var reload=Fan(7,10,10.1f,2);reload.ReloadAt=10.09f;reload.ReloadUntil=11.2f;Check(NativeGunKit.Of(reload,10.1f).FanShots==2,"a reload ends the volley");
            var empty=Fan(2,10,10.2f);var es=NativeGunKit.Of(empty,10.2f);Check(es.FanShots==2&&empty.ReloadUntil>10.2f,"an emptied magazine ends the volley (and reloads)");
            // Not this pilot's kit: another ability, another gun, malformed numbers, a later normal round.
            var other=Fan(7,10,10.5f);other.AbilityId="frag-grenade";Check(!NativeGunKit.Of(other,10.5f).HasFan,"other abilities are not the fan");
            var rifle=Fan(7,10,10.5f);rifle.Weapon=WeaponId.Rifle;Check(!NativeGunKit.Of(rifle,10.5f).HasFan,"pistol only");
            var nan=Fan(7,10,10.5f);nan.AbilityAt=float.NaN;Check(!NativeGunKit.Of(nan,10.5f).HasFan,"malformed cast time");
            var later=Fan(7,10,11f);later.LastShot=10.8f;Check(!NativeGunKit.Of(later,11f).HasFan,"a later normal round ends the fan's presentation");
            // Overdrive on NativeRangedRules' half-open clocks; zeros (old snapshots, never cast) are no kit.
            Check(!NativeGunKit.Of(new Actor{Weapon=WeaponId.MachineGun},1).HasOverdrive,"zero clocks are no kit");
            var d=Drive(10,14,14.8f);float lastHeat=-1;
            for(float t=9.9f;t<17;t+=1/240f)
            {
                var s=NativeGunKit.Of(d,t);var ph=NativeGunKit.DriveAt(s,t);bool active=ph==NativeGunKit.DrivePhase.SpinUp||ph==NativeGunKit.DrivePhase.Sustain;
                Check(active==NativeRangedRules.Overdrive(t,10,14),"active exactly while the simulation's Overdrive is @"+t);
                Check((ph==NativeGunKit.DrivePhase.Vent)==NativeRangedRules.Vent(t,14,14.8f),"venting exactly while the simulation vents @"+t);
                float h=NativeGunKit.Heat(s,t);if(lastHeat>=0)Check(Math.Abs(h-lastHeat)<.03f,"heat is continuous @"+t);if(active&&lastHeat>=0&&t>10.01f)Check(h>=lastHeat-1e-6f,"heat climbs while firing");lastHeat=h;
            }
            Check(NativeGunKit.Heat(NativeGunKit.Of(d,13.99f),13.99f)>.98f&&NativeGunKit.Redden(NativeGunKit.Of(d,14.7f),14.7f)>.9f,"white-hot at the end, red through the vent");
            var bad=Drive(10,14,14.8f);bad.VentUntil=float.PositiveInfinity;Check(!NativeGunKit.Of(bad,12).HasOverdrive,"malformed clocks are no kit");
            var pistolDrive=Drive(10,14,14.8f);pistolDrive.Weapon=WeaponId.Pistol;Check(!NativeGunKit.Of(pistolDrive,12).HasOverdrive,"machine gun only");
            float a0=NativeGunKit.SpinAngle(NativeGunKit.Of(d,10),10);float prev=a0,prevRate=0;
            for(float t=10;t<15;t+=1/240f){float an=NativeGunKit.SpinAngle(NativeGunKit.Of(d,t),t),rate=(an-prev)*240;Check(an>=prev-1e-4f&&Math.Abs(rate-prevRate)<3,"barrel spin is smooth @"+t);prev=an;prevRate=rate;}
        }

        // ---- poses: the third-person GunMotion route and the first-person view model ----
        private static CapturedRig.Pose Third(Actor a,float t,GunMotion m,CapturedRig.Pose pose)
        {m.Rig.GroupPosition=new Vector3(0,.865f,0);m.Rig.GroupYaw=0;m.Pose(a,t,pose,false,new NativeJuggleTrack(),out _);m.Finish(pose,t);return pose;}
        private static readonly float[] row=new float[CapturedClip.Channels];private static CapturedClip idleClip;
        private static CapturedRig.Pose First(Actor a,float t,CapturedRig.Pose pose,bool kit=true)
        {
            var idle=idleClip??=CapturedClips.Sets[0].First(c=>c.StateName=="idle");idle.Sample(CapturedClip.Wrap(t,idle.Duration),row);CapturedRig.Sample(row,0,pose);
            RangedPose.Apply(pose,a,t,1);if(kit)RangedAbilityPose.Apply(pose,a,t,true);pose.World();return pose;
        }
        private static Vector3 InGun(CapturedRig.Pose p,int bone){var q=p.WorldRotation[CapturedRig.RHand];return Vector3.Transform(p.WorldPosition[bone]-p.WorldPosition[CapturedRig.RHand],Quaternion.Inverse(q));}
        private static float Apart(Quaternion a,Quaternion b)=>2*(float)Math.Acos(Math.Min(1,Math.Abs(Quaternion.Dot(Quaternion.Normalize(a),Quaternion.Normalize(b)))));
        private static void Poses()
        {
            // Every replicated phase owns a pose in both routes; none outside the kit.
            foreach(bool first in new[]{false,true})
            {
                var m=new GunMotion();
                foreach(float t in new[]{10.03f,10.11f,10.2f,10.36f,10.45f})
                {
                    var a=Fan(7,10,t);var bare=Fan(7,10,t);bare.AbilityId=null;
                    var withKit=first?First(a,t,new CapturedRig.Pose()):Third(a,t,new GunMotion(),new CapturedRig.Pose());var without=first?First(bare,t,new CapturedRig.Pose()):Third(bare,t,new GunMotion(),new CapturedRig.Pose());
                    float moved=Vector3.Distance(withKit.WorldPosition[CapturedRig.LHand],without.WorldPosition[CapturedRig.LHand]);
                    Check(NativeGunKit.FanWeight(NativeGunKit.Of(a,t),t)>.05f&&moved>.03f,$"fan pose owns the off hand ({(first?"first":"third")} person, t {t-10:0.00}: {moved:0.000} m)");
                }
                foreach(float t in new[]{10.1f,12f,13.9f,14.3f,14.85f})
                {
                    var a=DriveAt(t);var bare=DriveAt(t);bare.OverdriveAt=bare.OverdriveUntil=bare.VentUntil=0;
                    var withKit=first?First(a,t,new CapturedRig.Pose()):Third(a,t,new GunMotion(),new CapturedRig.Pose());var without=first?First(bare,t,new CapturedRig.Pose()):Third(bare,t,new GunMotion(),new CapturedRig.Pose());
                    float moved=Vector3.Distance(withKit.WorldPosition[CapturedRig.RHand],without.WorldPosition[CapturedRig.RHand])+Apart(withKit.WorldRotation[CapturedRig.RHand],without.WorldRotation[CapturedRig.RHand])*.1f;
                    Check(moved>.004f,$"Overdrive pose owns the gun ({(first?"first":"third")} person, t {t-10:0.00}: {moved:0.0000})");
                }
                var none=Fan(7,10,13);var ref0=first?First(none,13,new CapturedRig.Pose(),false):null;
                if(first){var after=First(none,13,new CapturedRig.Pose());for(int b=0;b<22;b++)Check(Vector3.Distance(after.WorldPosition[b],ref0.WorldPosition[b])<1e-6f,"no layer once the kit is over");}
                var rifle=new Actor{Weapon=WeaponId.Rifle,SourceSpec="rifle",Hp=100,Grounded=true,AbilityId=NativeGunKit.Fan,AbilityAt=10,LastShot=10,OverdriveAt=10,OverdriveUntil=14,VentUntil=14.8f};
                var r0=First(rifle,10.1f,new CapturedRig.Pose(),false);var r1=First(rifle,10.1f,new CapturedRig.Pose());for(int b=0;b<22;b++)Check(Vector3.Distance(r0.WorldPosition[b],r1.WorldPosition[b])<1e-6f,"the rifle has no kit layer");
            }
            // The slap: the palm reaches the hammer on each round's time (first person, where the view model holds still), strikes at
            // the derived speed and rises between rounds; the hands stay continuous at 240 Hz (no pops) through the volley and recovery.
            {
                // The rounds' own recoil (RangedPose, by LastShot) restarts its spring on every round; the layer adds no pop of its own.
                Vector3 lastL=default,lastR=default,lastB=default;bool has=false;float worstL=0,worstR=0,worstB=0;
                for(float t=9.95f;t<10.8f;t+=1/240f)
                {
                    var a=Fan(7,10,t);var p=First(a,t,new CapturedRig.Pose());var bare=Fan(7,10,t);bare.AbilityId=null;var pb=First(bare,t,new CapturedRig.Pose());
                    var l=p.WorldPosition[CapturedRig.LHand];var r=p.WorldPosition[CapturedRig.RHand];var rb=pb.WorldPosition[CapturedRig.RHand];
                    if(has){worstL=Math.Max(worstL,Vector3.Distance(l,lastL));worstR=Math.Max(worstR,Vector3.Distance(r,lastR));worstB=Math.Max(worstB,Vector3.Distance(rb,lastB));}lastL=l;lastR=r;lastB=rb;has=true;
                }
                measured+=$"off hand at most {worstL*240:0.0} m/s, gun step {worstR*1000:0} mm vs the rounds' own recoil {worstB*1000:0} mm per 1/240 s; ";
                Check(worstL<.085f&&worstR<worstB+.012f,$"hands continuous through the fan (off hand at most {worstL*240:0.0} m/s, gun step {worstR*1000:0} mm vs the rounds' own recoil {worstB*1000:0} mm per 1/240 s)");
                var s=NativeGunKit.Of(Fan(7,10,10.4f),10.4f);
                for(int i=0;i<4;i++){Check(Vector3.Distance(NativeGunKitMotion.SlapPoint(s,s.FanShot(i)+1e-4f),NativeGunKitMotion.Hammer)<.005f,"palm on the hammer at round "+(i+1));}
                Check(NativeGunKitMotion.SlapPoint(s,s.FanShot(1)-NativeGunKit.FanGap*.4f).Y>NativeGunKitMotion.Hammer.Y+.05f,"the hand is raised between rounds");
                float strike=NativeGunKitMotion.StrikeShare*NativeGunKit.FanGap;Check(Math.Abs(Vector3.Distance(NativeGunKitMotion.Raised,NativeGunKitMotion.Hammer)/strike-NativeGunKitMotion.StrikeSpeed)<.05f,"strike at the derived support-hand speed");
                var a2=Fan(7,10,10.06f);var p2=First(a2,10.06f,new CapturedRig.Pose());var g=InGun(p2,CapturedRig.LHand);Check(g.Y>.1f,"first person: the off hand rises over the slide ("+g+")");
            }
            // Recoil stacks with the rounds and settles; Overdrive shakes harder as it heats; the vent tilts the gun up.
            {
                var s=NativeGunKit.Of(Fan(7,10,10.4f),10.4f);Check(NativeGunKitMotion.FanRecoil(s,10.24f)>NativeGunKitMotion.FanRecoil(s,10.02f)&&NativeGunKitMotion.FanRecoil(s,10.9f)<.01f,"recoil climbs through the volley and settles");
                var cold=DriveAt(10.5f);var hot=DriveAt(13.9f);NativeGunKitMotion.CameraKick(cold,cold.LastShot+.01f,out float pc,out _);NativeGunKitMotion.CameraKick(hot,hot.LastShot+.01f,out float ph,out _);Check(ph>pc&&pc>0,"Overdrive's camera buzz grows with heat");
                NativeGunKitMotion.CameraKick(Fan(7,10,10.08f),10.08f,out float pf,out _);NativeGunKitMotion.CameraKick(Fan(7,10,11),11,out float pq,out _);Check(pf>.5f&&pq<.01f,"each fanned round kicks the view, then it settles");
                var v0=First(DriveAt(13.9f),13.9f,new CapturedRig.Pose());var v1=First(DriveAt(14.4f),14.4f,new CapturedRig.Pose());
                Check(Vector3.Transform(Vector3.UnitZ,v1.WorldRotation[CapturedRig.RHand]).Y>Vector3.Transform(Vector3.UnitZ,v0.WorldRotation[CapturedRig.RHand]).Y+.1f,"the vent tilts the muzzle up");
            }
        }

        // ---- effects ----
        private sealed class Recorder:IGunKitSink
        {
            public readonly List<string> Calls=new List<string>();public int Glows,Streaks,Smokes,Sprites,Lines,Bands,Wisps,View;
            static string F(Vector3 v)=>v.X.ToString("0.0000")+","+v.Y.ToString("0.0000")+","+v.Z.ToString("0.0000");
            public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){Glows++;Calls.Add("g"+F(at));}
            public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){Streaks++;Calls.Add("s"+F(head));}
            public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){Smokes++;Calls.Add("k"+F(at));}
            public void Sprite(bool view,Vector3 at,Vector3 color,float size,int profile){Sprites++;if(view)View++;Calls.Add("p"+view+F(at)+F(color));}
            public void Line(bool view,Vector3 tail,Vector3 head,Vector3 color,float width,bool shard){Lines++;if(view)View++;Calls.Add("l"+view+F(tail)+F(head));}
            public void Band(bool view,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float ua,float ub,Vector3 color){Bands++;if(view)View++;}
            public void Wisp(bool view,Vector3 at,Vector3 color,float coverage,float size,float seed,float roll){Wisps++;if(view)View++;Calls.Add("w"+view+F(at)+coverage.ToString("0.000"));}
            public int Total=>Glows+Streaks+Smokes+Sprites+Lines+Bands+Wisps;
        }
        private sealed class Scene:IGunKitScene
        {
            public List<Actor> List=new List<Actor>();public int ViewerId;
            public IReadOnlyList<Actor> Actors=>List;public int Viewer=>ViewerId;
            public bool Gun(Actor a,bool view,out GunFrame f){f=new GunFrame{O=new Vector3(a.Position.x,a.Position.y+1.1f,a.Position.z),X=Vector3.UnitX,Y=Vector3.UnitY,Z=Vector3.UnitZ,Scale=view?.5f:1};return view==(a.Id==ViewerId);}
            public bool Eye(out Vector3 at,out Vector3 forward,out Vector3 up,out Vector3 right,out float tan,out float aspect){at=new Vector3(0,1.6f,0);forward=Vector3.UnitZ;up=Vector3.UnitY;right=Vector3.UnitX;tan=.6f;aspect=16/9f;return ViewerId!=0;}
        }
        private static Recorder Draw(Scene scene,float t,float q=1,bool low=false){var r=new Recorder();NativeGunKitFx.Draw(r,scene,t,q,low);return r;}
        private static object Fields(object o)
        {
            if(o==null)return null;var t=o.GetType();if(t.IsPrimitive||t.IsEnum||o is string)return o;
            if(o is Array arr){var c=Array.CreateInstance(t.GetElementType(),arr.Length);for(int i=0;i<arr.Length;i++)c.SetValue(Fields(arr.GetValue(i)),i);return c;}
            var copy=t.IsValueType?Activator.CreateInstance(t):System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(t);
            foreach(var f in t.GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance))if(!f.IsInitOnly)f.SetValue(copy,Fields(f.GetValue(o)));
            return copy;
        }
        private static void Effects()
        {
            Scene S(Actor a,int viewer=0){var s=new Scene{ViewerId=viewer};s.List.Add(a);return s;}
            // Fan: the hammer glint at each slap, brass per round, the smoking barrel after; nothing before the cast.
            Check(Draw(S(Fan(7,10,9.9f)),9.9f).Total==0,"no fan effects before the cast");
            var slap=Draw(S(Fan(7,10,10.08f)),10.08f);Check(slap.Sprites>=1&&slap.Lines>=2,"fan: slap glint and brass in flight");
            var smoke=Draw(S(Fan(7,10,10.9f)),10.9f);Check(smoke.Wisps>=5,"fan: the barrel smokes after the volley");
            var fp=Draw(S(Fan(7,10,10.08f),3),10.08f);Check(fp.View>0&&fp.View==fp.Total,"first person: the fan's effects ride the view weapon");
            // Overdrive phases.
            var spin=Draw(S(DriveAt(10.2f)),10.2f);Check(spin.Lines>=18,"spin-up: the barrel cluster turns (six barrels with motion blur)");
            var sustain=Draw(S(DriveAt(13.5f)),13.5f);Check(sustain.Sprites>=7&&sustain.Wisps>=3,"sustained fire: barrel glow, shimmer and steam");
            var vent=Draw(S(DriveAt(14.3f)),14.3f);Check(vent.Wisps>=12,"vent: steam bursts from the side vents");
            var cool=Draw(S(DriveAt(15.4f)),15.4f);Check(cool.Sprites>0&&cool.Wisps<vent.Wisps,"cooling: the red glow fades out");
            Check(Draw(S(DriveAt(17f)),17f).Total==0,"nothing once cooled");
            var haze=Draw(S(DriveAt(13.5f),4),13.5f);Check(haze.View==haze.Total&&haze.Wisps>Draw(S(DriveAt(13.5f)),13.5f).Wisps,"first person: the view weapon's effects plus the screen-bottom haze");
            // Quality tiers thin the particles; the dead draw nothing.
            Check(Draw(S(DriveAt(14.3f)),14.3f,.3f).Total<vent.Total&&Draw(S(DriveAt(14.3f)),14.3f,1,true).Total<vent.Total,"Reduced effects and low graphics thin the vent");
            Check(Draw(S(Fan(7,10,10.9f)),10.9f,.3f).Wisps<smoke.Wisps,"Reduced effects thin the smoke");
            var dead=DriveAt(13.5f);dead.Hp=0;Check(Draw(S(dead),13.5f).Total==0,"a dead pilot draws no kit");
            // A public-field copy (the wire, the killcam) draws the same frame.
            foreach(var (a,t) in new[]{(Fan(7,10,10.12f),10.12f),(Fan(3,10,10.9f),10.9f),(DriveAt(12.3f),12.3f),(DriveAt(14.4f),14.4f)})
            {var x=Draw(S(a),t);var y=Draw(S((Actor)Fields(a)),t);Check(x.Calls.SequenceEqual(y.Calls)&&x.Total>0,"replicated fields alone draw the frame @"+t);}
        }
        private static void Tracers()
        {
            var d=DriveAt(12);var l=NativeTracerLook.Of(d,11.9f,"machinegun",12);var n=NativeTracerLook.Of(d,9.5f,"machinegun",12);
            Check(l.Kit&&!n.Kit&&l.Gain>n.Gain&&l.Width>n.Width&&l.Length>n.Length,"Overdrive rounds burn hotter, brighter, wider and longer");
            var f=Fan(7,10,10.3f);var fl=NativeTracerLook.Of(f,10.075f,"pistol",10.3f);Check(fl.Kit&&fl.Width>NativeTracerFx.Width("pistol"),"fanned rounds are warmer");
            Check(!NativeTracerLook.Of(null,1,"pistol",1).Kit&&!NativeTracerLook.Of(f,10.075f,"rifle",10.3f).Kit,"other rounds keep the milestone tracer");
        }
        private static void Hud(ConversionContent data)
        {
            var none=NativeGunKit.Of(null,0);
            var ready=NativeGunKitHud.Of(NativeGunKit.Fan,none,5,0,7,7,false);Check(ready.Ready&&ready.State=="READY"&&ready.Icon=="hammer","Fan ready, hammer icon");
            Check(NativeGunKitHud.Of(NativeGunKit.Fan,none,5,0,7,0,false).State=="NO AMMO"&&NativeGunKitHud.Of(NativeGunKit.Fan,none,5,0,7,3,true).State=="RELOADING","Fan needs a round and no reload");
            var fanning=NativeGunKitHud.Of(NativeGunKit.Fan,NativeGunKit.Of(Fan(7,10,10.1f),10.1f),10.1f,6.9f,7,5,false);Check(fanning.Active&&fanning.State=="FANNING","Fan fanning");
            var cool=NativeGunKitHud.Of(NativeGunKit.Fan,NativeGunKit.Of(Fan(7,10,13.5f),13.5f),13.5f,3.5f,7,3,false);Check(!cool.Ready&&cool.State=="3.5s"&&Math.Abs(cool.Dial-.5f)<1e-3f,"Fan cooldown with its dial");
            var active=NativeGunKitHud.Of(NativeGunKit.Overdrive,NativeGunKit.Of(DriveAt(12),12),12,8,10,48,false);Check(active.Active&&active.State=="ACTIVE 2.0s"&&Math.Abs(active.Dial-.5f)<1e-3f&&active.Icon=="gauge","Overdrive active time draining, gauge icon");
            var venting=NativeGunKitHud.Of(NativeGunKit.Overdrive,NativeGunKit.Of(DriveAt(14.4f),14.4f),14.4f,5.6f,10,20,false);Check(venting.Venting&&venting.State=="VENTING 0.4s","Overdrive venting");
            var after=NativeGunKitHud.Of(NativeGunKit.Overdrive,NativeGunKit.Of(DriveAt(15f),15f),15f,5,10,20,false);Check(!after.Active&&!after.Venting&&after.State=="5.0s","Overdrive cooldown after the vent");
            Check(NativeGunKitHud.Status("Overdrive",venting)=="Q  Overdrive · venting 0.4s","AbilityStatus line");
            // The content's Q kinds: only the rifle's is ADS, so AbilityStatus' "Q  Aim (hold)" never prints for pistol or machine gun.
            foreach(var spec in new[]{"pistol","machinegun"}){var q=data.Ability(spec,"q");Check(q!=null&&q.kind!="ads"&&NativeGunKit.Covers(q.id)&&NativeGunKit.Icon(q.id)!=null,spec+" Q is the kit, not ADS");}
            Check(data.Ability("rifle","q").kind=="ads"&&!NativeGunKit.Covers("ads"),"the rifle keeps its scope");
            Check(NativeLocker.AbilityIcon("fan-hammer",data.Weapon("pistol"))=="hammer"&&NativeLocker.AbilityIcon("overdrive",data.Weapon("machinegun"))=="gauge","locker icons match the HUD cards");
        }
        private static void Ads(string root,ConversionContent data)
        {
            Check(data.Ads("pistol")==null&&data.Ads("machinegun")==null&&data.Ads("rifle")!=null,"only the rifle has an ADS entry");
            Check(!NativeRangedRules.Scope("pistol")&&!NativeRangedRules.Scope("machinegun")&&NativeRangedRules.Scope("rifle"),"rifle-only scope rule");
            var game=Code(File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/ConversionGame.cs")));
            Check(game.Contains("bool aiming=NativeRangedRules.Scope(p.Seat.spec)&&ads!=null&&Input.GetKey(KeyCode.Q);"),"Q aims only with the rifle (look sensitivity, Aim input)");
            Check(game.Contains("bool aim=NativeRangedRules.Scope(p.Seat.spec)&&ads!=null&&pending.Aim&&alive&&!menu;"),"the camera zooms (and forces first person) only for the rifle");
            Check(game.Contains("if(slot==\"q\"&&NativeGunKit.Covers(d.id))return NativeGunKitHud.Status(")&&game.IndexOf("NativeGunKitHud.Status(",StringComparison.Ordinal)<game.IndexOf("return \"Q  Aim (hold)\"",StringComparison.Ordinal),"AbilityStatus routes the kit before the ADS line");
            Check(game.Contains("presentation.GunKitKick(")&&Regex.IsMatch(game,@"prefs\.cameraShake\)presentation\.GunKitKick\("),"the first-person kick is under the camera shake setting");
            var hud=Code(File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeFightHud.cs")));
            Check(hud.Contains("NativeGunKitHud.Of(")&&hud.Contains("NativeIcons.Draw(new Rect(lx,ly,kh,kh),c.Icon"),"Q cards: the kit card with its icon");
            Check(hud.Contains("value=p.Ammo+\" / \"+cap"),"the magazine readout still reads the replicated ammo");
        }
        private static void Derived(string root)
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"tools/fan-hammer-provenance.json")));var d=doc.RootElement.GetProperty("derived");
            var times=d.GetProperty("kickTimes").EnumerateArray().Select(x=>(float)x.GetDouble()).ToArray();var values=d.GetProperty("kickValues").EnumerateArray().Select(x=>(float)x.GetDouble()).ToArray();
            Check(times.Length==NativeGunKitMotion.KickTimes.Length&&times.Zip(NativeGunKitMotion.KickTimes,(a,b)=>Math.Abs(a-b)<1e-4f).All(x=>x)&&values.Zip(NativeGunKitMotion.KickValues,(a,b)=>Math.Abs(a-b)<1e-4f).All(x=>x),"the recoil kick is the derived pack curve");
            Check(Math.Abs((float)d.GetProperty("supportStrikeSpeed").GetDouble()-NativeGunKitMotion.StrikeSpeed)<1e-3f,"the strike speed is the derived pack value");
            var text=File.ReadAllText(Path.Combine(root,"tools/fan-hammer-provenance.json"));Check(!text.Contains("\"rows\"")&&!text.Contains("positions")&&doc.RootElement.GetProperty("sources").GetArrayLength()==6,"derived numbers only (no pose track), six sources");
        }
        private static void Sounds(string root)
        {
            var clips=new HashSet<string>(Directory.GetFiles(Path.Combine(root,"Assets/Conversion/Resources/Audio/audio"),"*.wav").Select(Path.GetFileNameWithoutExtension));
            foreach(var s in new[]{NativeGunKit.Slap,NativeGunKit.Whine,NativeGunKit.Hiss}){Check(NativeSoundKeys.Known(s.Key),"bank key "+s.Key);Check(s.Clip==null||clips.Contains(s.Clip),"fallback clip ships: "+s.Clip);}
            var audio=Code(File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeAudio.cs")));
            Check(audio.Contains("NativeGunKit.Whine.Key")&&audio.Contains("NativeGunKit.Hiss.Key")&&audio.Contains("NativeGunKit.Slap.Key"),"audio hooks");
            Check(audio.Contains("if(ability!=null&&!NativeRangedRules.Q(ability)&&t>=release"),"the kit's casts take its own cues, not a doubled gunshot");
        }
        private sealed class Silent:IGunKitSink
        {
            public int N;
            public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed)=>N++;
            public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed)=>N++;
            public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor)=>N++;
            public void Sprite(bool view,Vector3 at,Vector3 color,float size,int profile)=>N++;
            public void Line(bool view,Vector3 tail,Vector3 head,Vector3 color,float width,bool shard)=>N++;
            public void Band(bool view,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float ua,float ub,Vector3 color)=>N++;
            public void Wisp(bool view,Vector3 at,Vector3 color,float coverage,float size,float seed,float roll)=>N++;
        }
        private static void Allocation()
        {
            var fan=Fan(7,10,10.12f);var drive=DriveAt(14.3f);var hot=DriveAt(13.5f);var scene=new Scene{ViewerId=4};scene.List.Add(fan);scene.List.Add(hot);var sink=new Silent();
            var pose=First(fan,10.12f,new CapturedRig.Pose(),false);var pose2=First(drive,14.3f,new CapturedRig.Pose(),false);
            void Frame(float t)
            {
                RangedAbilityPose.Apply(pose,fan,10.12f,true);RangedAbilityPose.Apply(pose2,drive,14.3f,false);NativeGunKitMotion.CameraKick(hot,13.5f,out _,out _);
                NativeGunKitFx.Draw(sink,scene,13.5f,1,false);NativeGunKitFx.Draw(sink,scene,10.12f,1,false);var tl=NativeTracerLook.Of(hot,13.4f,"machinegun",13.5f);
                var c=NativeGunKitHud.Of(NativeGunKit.Overdrive,NativeGunKit.Of(hot,13.5f),13.5f,0,10,48,false);
            }
            for(int i=0;i<20;i++)Frame(i);long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<200;i++)Frame(i);long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            // The HUD card's state string is formatted per frame like every other card; the pose, effects and tracer paths allocate nothing.
            long hudBytes;{long b0=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<200;i++)NativeGunKitHud.Of(NativeGunKit.Overdrive,NativeGunKit.Of(hot,13.5f),13.5f,0,10,48,false);hudBytes=GC.GetAllocatedBytesForCurrentThread()-b0;}
            measured+=$"{bytes-hudBytes} bytes allocated by the pose/effects/tracer paths over 200 frames";
            Check(bytes-hudBytes<=0&&sink.N>0,$"pose, effects and tracer paths allocate nothing per frame ({bytes-hudBytes} bytes over 200 frames)");
        }
        private static void Sources(string root)
        {
            string R(string p)=>Code(File.ReadAllText(Path.Combine(root,p)));
            foreach(string file in new[]{"Core/NativeGunKit.cs","Core/NativeGunKitMotion.cs","Core/NativeGunKitFx.cs"})
                foreach(string banned in new[]{"NativePilot","NativeArena","NativeFanBurst",".Fan.","UnityEngine","System.Random","DateTime"})Check(!R("Assets/ChipArena/"+file).Contains(banned),file+" reads no authority-only state ("+banned+")");
            var runtime=R("Assets/ChipArena/Runtime/NativeGunKitPresentation.cs");
            foreach(string banned in new[]{".Fan","QReady","OverdriveAt=","arena.Pilots","Time.timeScale",".Body.position"})Check(!runtime.Contains(banned),"runtime reads replicated data only ("+banned+")");
            Check(R("Assets/ChipArena/Core/RangedAbilityPose.cs").Contains("NativeGunKitMotion.Apply(pose,a,time,firstPerson);"),"both gun rig routes reach the layer through RangedAbilityPose");
            var wfx=R("Assets/ChipArena/Runtime/NativeWeaponFxPresentation.cs");Check(wfx.Contains("GunKitFx();")&&wfx.Contains("GunKitTracer(b,v)")&&wfx.Contains("if(look.Kit)KitTracer("),"WeaponFx hooks: effects before the submit, the tracer look");
            foreach(string file in new[]{"Core/NativeGunKit.cs","Core/NativeGunKitMotion.cs","Core/NativeGunKitFx.cs","Runtime/NativeGunKitPresentation.cs"})
            {
                string meta=Path.Combine(root,"Assets/ChipArena",file+".meta");Check(File.Exists(meta),"meta: "+file);
                var guid=Regex.Match(File.ReadAllText(meta),@"^guid: ([0-9a-f]{32})$",RegexOptions.Multiline);Check(guid.Success,"meta guid: "+file);
                int owners=Directory.GetFiles(Path.Combine(root,"Assets"),"*.meta",SearchOption.AllDirectories).Count(m=>File.ReadAllText(m).Contains("guid: "+guid.Groups[1].Value));Check(owners==1,"unique guid: "+file);
            }
        }
    }
}
#endif
