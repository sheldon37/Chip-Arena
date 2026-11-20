using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using ChipArena.UnityConversion;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Intros v2 (docs/INTROS-V2.md "The managed checks"): the framework (the archive and its fallback, the registry, the rig, the ramps,
    /// the void, the runtime wiring read as text) and, for EVERY registered intro automatically, its registry entry, determinism under
    /// a scrub, no per-frame allocation, the frame budgets at peak, Reduced effects (no shake, nothing faster than 3 Hz), the streak
    /// rule and its cue timeline. Authors never add a line here: registering runs them.
    /// </summary>
    internal static class IntroV2Checks
    {
        private static int count;private static void Check(bool ok,string what){if(!ok)throw new Exception("Intros v2: "+what);count++;}
        // ---- recorders (no allocation per call) ----
        private sealed class Fx:INativeEntranceFx,INativeEntranceFire,INativeEntranceVenom
        {
            public int Calls,Glows;public double Hash,Emit;public bool Broken;public int LongStreaks,OffPlane;public Vector3 Eye,Forward;public float WorstDeg;
            private void V(Vector3 v){if(!MathUtil.Finite(v.X)||!MathUtil.Finite(v.Y)||!MathUtil.Finite(v.Z))Broken=true;Hash=Hash*1.0000001+v.X*.13+v.Y*.17+v.Z*.19;}
            public void Reset(){Calls=Glows=LongStreaks=OffPlane=0;Hash=Emit=0;}
            public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){V(at);V(color);Calls++;Glows++;Emit+=(color.X+color.Y+color.Z)*size*size;}
            public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed)
            {
                V(head);V(velocity);V(color);Calls++;float len=Math.Max(velocity.Length()*blur,minLength);
                if(len>NativeCacheOpenFx.ExactStreak&&velocity.LengthSquared()>1e-10f&&Forward.LengthSquared()>.5f)
                {LongStreaks++;float c=Math.Abs(Vector3.Dot(Vector3.Normalize(velocity),Forward));float deg=(float)(Math.Asin(Math.Min(1,c))*180/Math.PI);if(deg>WorstDeg)WorstDeg=deg;if(deg>2)OffPlane++;}
            }
            public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){V(at);V(albedo);Calls++;}
            public void Fire(Vector3 at,Vector3 tint,float opacity,float size,float u,float seed,float floor){V(at);V(tint);Calls++;}
            public void Bead(Vector3 head,Vector3 segment,Vector3 color,float coverage,float width,float u,float seed){V(head);V(segment);Calls++;}
        }
        private sealed class Props:INativeEntranceProps,INativeIntroSet
        {
            public int Primitives,Pieces,Models,Batched;public double Hash;public bool Ready{get;set;}
            private void V(Vector3 v){Hash=Hash*1.0000001+v.X*.11+v.Y*.23+v.Z*.29;}
            public void Reset(){Primitives=Pieces=Models=Batched=0;Hash=0;}
            public void Batch(string prop,ReadOnlySpan<Vector3> at,ReadOnlySpan<Quaternion> rotation,float scale,Vector3 tint){int n=Math.Min(at.Length,rotation.Length);for(int i=0;i<n;i++)V(at[i]);Batched+=n;}
            public void Piece(WeaponShape shape,bool glow,Vector3 position,Quaternion rotation,Vector3 scale,Vector3 color){V(position);V(scale);V(color);Primitives++;}
            public bool Model(string id,Vector3 position,Quaternion rotation,float scale,Vector3 light){V(position);Models++;return false;}
            public bool Bank;public bool Has(string prop)=>Bank;
            public void Piece(string prop,Vector3 at,Quaternion rotation,float scale,Vector3 tint,Vector3 glow){V(at);Pieces++;}
            public void Primitive(WeaponShape shape,bool glow,Vector3 at,Quaternion rotation,Vector3 scale,Vector3 color){V(at);V(scale);V(color);Primitives++;}
        }
        private static double Hash(CapturedRig.Pose p,Vector3 root){double h=root.X*.3+root.Y*.5+root.Z*.7;for(int b=0;b<p.Position.Length;b++){var q=p.Rotation[b];var v=p.Position[b];h=h*1.0000001+v.X+v.Y*2+v.Z*3+q.X*5+q.Y*7+q.Z*11+q.W*13;}return h;}
        private static double Hash(NativeEntranceLights l){double h=l.Background.X+l.Fog.Y*2+l.Sky.Z*3+l.Directionals*5+l.Points*7;for(int i=0;i<l.Points;i++){var p=l.Point[i];h=h*1.0000001+p.X+p.Y*2+p.Z*3+p.R*5+p.G*7+p.B*11;}return h;}
        private static double Power(NativeEntranceLights l){double s=0;for(int i=0;i<l.Points;i++){var p=l.Point[i];s+=p.R+p.G+p.B;}for(int i=0;i<l.Directionals;i++)s+=l.DirectionColor[i].X+l.DirectionColor[i].Y+l.DirectionColor[i].Z;return s;}
        /// <summary>One frame of every call at age `age`; returns a hash of all of it.</summary>
        private static double Frame(NativeEntranceScript s,INativeIntroV2 v2,float age,CapturedRig.Pose pose,NativeEntranceLights lights,Props props,Fx fx,bool reduced,bool low)
        {
            v2.Context=new NativeIntroContext{Reduced=reduced,Low=low,Quality=reduced?.3f:1,Name="Pilot Name"};
            float t=s.Clock(age);s.Pose(t,pose,out var root,out var rot);s.Camera(age,out var view);fx.Eye=view.Eye;var f=view.Look-view.Eye;fx.Forward=f.LengthSquared()>1e-8f?Vector3.Normalize(f):Vector3.Zero;
            lights.Clear();s.Lights(t,lights);props.Reset();s.Props(t,props,v2.Context.Quality);v2.Set(t,props);fx.Reset();s.Effects(t,fx,v2.Context.Quality);
            var card=default(NativeIntroCardState);v2.Card(t,ref card);v2.Lens(t,out var lens);
            return Hash(pose,root)+view.Eye.X*3+view.Eye.Y*5+view.Eye.Z*7+view.Fov*.01+Hash(lights)+props.Hash+fx.Hash+(card.Shown?card.Age*31+card.Exit*37:0)+lens.Letterbox*41+lens.Black*43+lens.Focus*.1;
        }
        public static void Run(string root)
        {
            count=0;worstFacing=0;worstFacingAt="";
            Framework(root);
            Anchors(root);
            int intros=0;foreach(var e in NativeIntroCatalog.Entries){Intro(e);Faithful(e);intros++;}
            Console.WriteLine("PASS intros v2: "+count+" checks (the archive and its fallback, the registry, the rig and the ramps, the void, the wiring, the model-aware anchors, the weapon sockets, the intro's own light; "+intros+" registered intro"+(intros==1?"":"s")+": entry, determinism, allocation, budgets, Reduced effects, streaks, cues, the keyed pose drawn faithfully on the model (worst head, chest or pelvis facing "+worstFacing.ToString("0.00")+" deg"+worstFacingAt+"))");
        }

        // ---- the keyed pose drawn faithfully on the model (owner 2026-11-20: Whiteout's close-up "head turned about 90°") ----
        // Every registered intro's keyed pose, every 1/20 s, bound onto the model's skeleton as VisualIntroPose binds it (Core NativeVisualRig,
        // the free arms relaxed) on the measured skeleton (the first outfit's rest joints) and on the synthetic one: the drawn head's, chest's and
        // pelvis's facing (their forward and up) stay within FacingTolerance of the keyed joints'. The binding gives each bone its game joint's
        // turn since its rest, so a deep pose (a kneel and a 56° bow) turns nothing aside; what differs is where things are (the model's
        // shorter torso and longer legs), which the model-aware anchors carry (Anchors).
        public const float FacingTolerance=3;
        private static float worstFacing;private static string worstFacingAt;
        private static NativeVisualRig.Rig measured,synthetic;
        private static void Faithful(NativeIntroEntry e)
        {
            measured??=ModelPoseChecks.Measured();synthetic??=VisualRigChecks.Synthetic(out _);
            var s=e.Make();var pose=new CapturedRig.Pose();int[] joints={CapturedRig.Head,CapturedRig.Chest,CapturedRig.Pelvis};
            foreach(var (rigName,rig) in new[]{("measured",measured),("synthetic",synthetic)})
            {
                var state=new NativeVisualRig.State(rig);float worst=0;string where="";
                for(int i=0;i<=e.Duration*20;i++)
                {
                    float t=s.Clock(i/20f);s.Pose(t,pose,out _,out _);pose.World();
                    NativeVisualRig.Pose(state,pose,new NativeVisualRig.Grasp{Relax=1},false,false);
                    foreach(int j in joints)
                    {
                        int v=rig.Of[j];if(v<0)continue;var drawn=Quaternion.Normalize(state.World[v]*Quaternion.Inverse(rig.AlignedWorld[v]));var keyed=pose.WorldRotation[j];
                        float a=Math.Max(Degrees(Vector3.Transform(Vector3.UnitZ,drawn),Vector3.Transform(Vector3.UnitZ,keyed)),Degrees(Vector3.Transform(Vector3.UnitY,drawn),Vector3.Transform(Vector3.UnitY,keyed)));
                        if(a>worst){worst=a;where=CapturedRig.Bones[j].Name+" at "+t.ToString("0.00")+" s";}
                    }
                }
                Check(worst<=FacingTolerance,e.Id+": the drawn head, chest and pelvis face as the keyed pose's on the "+rigName+" skeleton (worst "+worst.ToString("0.00")+" deg, "+where+")");
                if(worst>worstFacing){worstFacing=worst;worstFacingAt=", "+e.Id+" "+where+" ("+rigName+")";}
            }
        }
        private static float Degrees(Vector3 a,Vector3 b)=>(float)(Math.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(a),Vector3.Normalize(b)),-1,1))*180/Math.PI);

        // ---- the model-aware anchors (docs/INTROS-V2.md "Anchors"), the weapon sockets and the intro's own light ----
        private static void Anchors(string root)
        {
            // A script with a weapon (the pistol intro if one is registered, else the first registered intro): its anchors keyed until a body
            // is handed in; then each handed anchor exactly where it was drawn at that frame's time, carried rigidly by its keyed joint at other
            // times (a trail keeps its shape), the rest keyed; ClearDrawn gives the keyed ones back. No allocation.
            var entry=NativeIntroCatalog.Find(NativeIntroCatalog.WeaponIntro("pistol")??"")??NativeIntroCatalog.Entries.FirstOrDefault();
            if(entry!=null)
            {
                var s=entry.Make();int n=(int)NativeEntranceAnchor.Count;var at=new Vector3[n];
                for(int a=0;a<n;a++)for(float t=0;t<=entry.Duration;t+=.37f){var k=s.Anchor((NativeEntranceAnchor)a,t);Check(k==s.KeyedAnchor((NativeEntranceAnchor)a,t)&&MathUtil.Finite(k.X)&&MathUtil.Finite(k.Y)&&MathUtil.Finite(k.Z)&&!s.IsDrawn((NativeEntranceAnchor)a),entry.Id+": anchor "+(NativeEntranceAnchor)a+" is keyed and finite before a body is drawn at "+t);}
                float t0=entry.Duration*.45f;ulong mask=0;
                for(int a=0;a<n;a++)
                {
                    var w=(NativeEntranceAnchor)a;if(w==NativeEntranceAnchor.Root||w==NativeEntranceAnchor.Forward||w==NativeEntranceAnchor.Focus||w==NativeEntranceAnchor.LeftElbow)continue;
                    at[a]=s.KeyedAnchor(w,t0)+new Vector3(.03f+.01f*a,-.02f+.004f*a,.05f-.003f*a);mask|=1UL<<a;
                }
                s.SetDrawn(t0,at,mask);
                for(int a=0;a<n;a++)
                {
                    var w=(NativeEntranceAnchor)a;bool handed=(mask>>a&1)!=0;Check(s.IsDrawn(w)==handed,entry.Id+": "+w+" is drawn only when handed in");
                    if(!handed){Check(s.Anchor(w,t0)==s.KeyedAnchor(w,t0),entry.Id+": "+w+" stays keyed when not handed in");continue;}
                    Check((s.Anchor(w,t0)-at[a]).Length()<1e-4f,entry.Id+": "+w+" is where the body is drawn at the frame's time");
                    float off=(at[a]-s.KeyedAnchor(w,t0)).Length();
                    foreach(float dt in new[]{-.3f,-.1f,-1f/120,1f/120,.1f})
                    {
                        var p=s.Anchor(w,t0+dt);float o=(p-s.KeyedAnchor(w,t0+dt)).Length();
                        Check(Math.Abs(o-off)<1e-3f&&MathUtil.Finite(p.X),entry.Id+": "+w+" keeps its drawn offset ("+off.ToString("0.000")+" m) on its joint at "+dt+" s from the frame (a trail keeps its shape)");
                    }
                }
                long b0=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<200;i++){s.SetDrawn(t0+i*.001f,at,mask);for(int a=0;a<n;a++)s.Anchor((NativeEntranceAnchor)a,t0-i*.001f);}
                Check(GC.GetAllocatedBytesForCurrentThread()-b0<=64,entry.Id+": handing in a drawn body and reading its anchors allocates nothing");
                s.ClearDrawn();for(int a=0;a<n;a++)Check(s.Anchor((NativeEntranceAnchor)a,t0)==s.KeyedAnchor((NativeEntranceAnchor)a,t0),entry.Id+": ClearDrawn gives the keyed "+(NativeEntranceAnchor)a+" back");
                // The keyed fallbacks of the newer anchors sit on their joints: the eyes and the visor on the head, the toes ahead of the ankles,
                // the wrists and the grips at the wrist joints, the muzzles out along the hands.
                for(float t=0;t<=entry.Duration;t+=.25f)
                {
                    var head=s.KeyedAnchor(NativeEntranceAnchor.Head,t);
                    Check((s.KeyedAnchor(NativeEntranceAnchor.Eyes,t)-head).Length()<.15f&&(s.KeyedAnchor(NativeEntranceAnchor.Visor,t)-head).Length()<.2f,entry.Id+": the keyed eyes and visor are on the head at "+t);
                    Check((s.KeyedAnchor(NativeEntranceAnchor.LeftToe,t)-s.KeyedAnchor(NativeEntranceAnchor.LeftFoot,t)).Length()<.12f&&(s.KeyedAnchor(NativeEntranceAnchor.RightToe,t)-s.KeyedAnchor(NativeEntranceAnchor.RightFoot,t)).Length()<.12f,entry.Id+": the keyed toes are by the ankles at "+t);
                    Check(s.KeyedAnchor(NativeEntranceAnchor.RightGrip,t)==s.KeyedAnchor(NativeEntranceAnchor.RightWrist,t)&&(s.KeyedAnchor(NativeEntranceAnchor.RightHand,t)-s.KeyedAnchor(NativeEntranceAnchor.RightWrist,t)).Length()<.06f,entry.Id+": the keyed grip is the wrist, the hand beside it, at "+t);
                }
                if(entry.Spec=="pistol")Check(Math.Abs((s.KeyedAnchor(NativeEntranceAnchor.RightMuzzle,1)-s.KeyedAnchor(NativeEntranceAnchor.RightWrist,1)).Length()-NativeWeaponDressing.MuzzleZ("pistol"))<1e-3f,entry.Id+": the keyed muzzle is the pistol's MuzzleZ out along the wrist");
            }
            // The weapon sockets for every v2 weapon: the hand(s) that hold it, a tip and a muzzle in reach.
            foreach(var spec in NativeIntroCatalog.WeaponSpecs)
            {
                bool left=NativeIntroWeapon.Holds(spec,true),right=NativeIntroWeapon.Holds(spec,false);
                Check(left||right,spec+": a hand holds it");Check((spec=="daggers"||spec==NativeFangs.Spec)==(left&&right),spec+": two blades only for the daggers and the Fangs");
                if(NativeWeaponDressing.Staff(spec)||spec=="bow")Check(left&&!right,spec+": rides the left hand, as the match holds it");
                foreach(bool side in new[]{true,false})
                {
                    if(!NativeIntroWeapon.Holds(spec,side)){Check(!NativeIntroWeapon.Tip(spec,side,out _)&&!NativeIntroWeapon.Muzzle(spec,side,out _),spec+": no tip in the empty hand");continue;}
                    bool tipped=NativeIntroWeapon.Tip(spec,side,out var tip),muzzled=NativeIntroWeapon.Muzzle(spec,side,out var muzzle);
                    Check(tipped&&muzzled&&tip.Length()>.1f&&tip.Length()<2f&&muzzle.Length()>.1f&&muzzle.Length()<2f,spec+": a tip and a muzzle within reach ("+(side?"left":"right")+")");
                    if(NativeWeaponDressing.Gun(spec))Check(muzzle==new Vector3(0,0,NativeWeaponDressing.MuzzleZ(spec)),spec+": the muzzle at its MuzzleZ socket");
                }
            }
            // The intro's own light (docs/INTROS-V2.md "Lighting"): the reflection defaults to its ambient and fog; set colours win; Clear resets.
            var l=new NativeEntranceLights();l.Clear();l.Sky=new Vector3(.3f,.4f,.5f);l.Ground=new Vector3(.1f,.05f,.02f);l.Fog=new Vector3(.2f,.2f,.25f);
            l.Reflection(out var rs,out var rh,out var rg);Check(rs==l.Sky&&rh==l.Fog&&rg==l.Ground&&l.ReflectIntensity==1,"the reflection defaults to the intro's sky, fog and ground");
            l.ReflectSky=new Vector3(1,0,0);l.ReflectIntensity=.4f;l.Reflection(out rs,out rh,out rg);Check(rs==new Vector3(1,0,0)&&rh==l.Fog&&rg==l.Ground,"a set reflection colour wins");
            l.Clear();Check(l.ReflectSky==Vector3.Zero&&l.ReflectHorizon==Vector3.Zero&&l.ReflectGround==Vector3.Zero&&l.ReflectIntensity==1,"Clear resets the reflection");
            // The runtime's wiring (read as text): the camera stays keyed; the body as drawn is handed in after the model and its weapon are posed
            // and before the lights, the set and the effects, and taken back after; a v2 intro owns its light and gives it back.
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f)).Replace("\r\n","\n");
            string intros=R("Runtime/NativeIntros.cs"),v2=R("Runtime/NativeIntroV2Presentation.cs"),light=R("Runtime/NativeLighting.cs"),cam=R("Core/NativeIntroCamera.cs");
            int undrawn=intros.IndexOf("script.ClearDrawn();\n            script.Camera(age,out var view);",StringComparison.Ordinal),hands=intros.IndexOf("IntroV2Hands(script,t);\n            if(v2)EntranceLighting(script,t,camera,true);",StringComparison.Ordinal);
            Check(undrawn>0&&hands>undrawn&&intros.IndexOf("IntroWeapon(style,age);",StringComparison.Ordinal)<hands&&hands<intros.IndexOf("script.Props(t,entranceProps,quality);",StringComparison.Ordinal)
                &&intros.IndexOf("introBodyActor=-1;introBody=null;introV2Actor=-1;script.ClearDrawn();",StringComparison.Ordinal)>hands&&intros.Contains("bool v2=script is INativeIntroV2;if(!v2)EntranceLighting(script,t,camera,false);"),
                "DirectedIntro: the camera on the keyed rig; the drawn body handed in after the model and its weapon, before the lights, set and effects; taken back after");
            Check(v2.Contains("if(mask!=0)script.SetDrawn(t,introDrawn,mask);")&&v2.Contains("IntroV2Drawn(script,t);")&&cam.Contains("script.KeyedVelocity((NativeEntranceAnchor)s.Follow,t)")&&cam.Contains("script.KeyedAnchor(NativeEntranceAnchor.Chest,t)"),"the drawn anchors are handed in from the model; the camera leads and blends on the keyed ones");
            Check(intros.Contains("if(asModel&&script is INativeIntroV2){IntroV2GhostHair(2L<<40|(uint)i);if(!g.Bare)IntroV2GhostGear(2L<<40|(uint)i,tint,scan,g.Opacity);}")&&intros.Contains("Bare=script is NativeIntroV2Script holoScript&&holoScript.HoloBare}")
                &&v2.Contains("g.Body.Helm=live.Helm;VisualHatHair(g.Body,entranceGhostActor?.Hat);")&&v2.Contains("Part(helm?NativeSourceModels.Helm:NativeSourceModels.HalfMask,head.localToWorldMatrix);")&&v2.Contains("sourceModels.Queue(suit,Matrix4x4.identity,suitBone,suitVisible,0,null,tint,scan,opacity);"),"a v2 ghost on the model is the whole dressed pilot (its armour, helmet or mask, hat, core, emblem and device in the ghost's look; the hair hidden under the helmet), unless Bare");
            Check(intros.Contains("if(v2)NativeLighting.IntroOwn(entranceLights);else NativeLighting.IntroRelease();")&&light.Contains("public static void EndIntro(ContentMap map,Camera camera){IntroRelease();")&&light.Contains("RenderSettings.defaultReflectionMode=keptReflectMode;"),"a v2 intro owns its light each frame and gives back what it replaced (an older entrance next, the intro's end)");
        }
        // ---- the framework ----
        private static void Framework(string root)
        {
            double now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            // The archive: every older intro but the Top Dog's and the Bombadier's.
            var old=NativeIntroRules.Entrances.Concat(new[]{NativeCosmeticGate.SheldonIntro,CyberLordSpec.Entrance,NativeGlaiveIntro.Id,NativeKatana.Entrance,NativeFangs.Entrance,NativeDevMotions.GreatswordEntrance,NativeDevMotions.KatanaEntrance}).ToArray();
            Check(old.Length==30,"thirty archived intros listed ("+old.Length+")");
            foreach(var id in old)
            {
                Check(NativeIntroCatalog.Archived(id),id+" is archived");Check(NativeIntroRules.Pickable(id)&&NativeIntroRules.Entrance(id,"rifle")==id,id+" still stages when replicated (its code and data are kept)");
                string intro=id;bool pick=true;NativeIntroRules.Migrate(ref intro,ref pick);Check(intro==NativeIntroRules.Default&&!pick,id+" migrates to no pick");
                var dev=Dev(now);Check(NativeIntroRules.Style(new NativeSeat{spec="pistol",intro=id,overrideIntro=true,access=dev})==NativeIntroRules.Signature("pistol"),id+" picked (even with DEV access) plays the weapon's intro");
            }
            foreach(var keep in new[]{NativeTopDog.Entrance,NativeBombs.Entrance})
            {
                Check(!NativeIntroCatalog.Archived(keep),keep+" is kept");string intro=keep;bool pick=true;NativeIntroRules.Migrate(ref intro,ref pick);Check(intro==keep&&pick,keep+" survives migration");
                Check(NativeIntroRules.Style(new NativeSeat{spec="pistol",intro=keep,overrideIntro=true,access=Dev(now)})==keep,keep+" plays when picked with DEV access");
            }
            Check(NativeIntroRules.Style(new NativeSeat{spec=NativeTopDog.Spec})==NativeTopDog.Entrance&&NativeIntroRules.Style(new NativeSeat{spec=NativeBombs.Spec})==NativeBombs.Entrance,"the Top Dog and the Bombadier keep their own entrances");
            Check(NativeIntroRules.Style(new NativeSeat{spec="siren",AuthorityBoss=true})=="dev-siren","the Siren boss keeps its entrance");
            // The fallback: a weapon plays its v2 intro once registered, else its old signature; a weapon without either the new default once registered.
            foreach(var spec in NativeIntroCatalog.WeaponSpecs)
            {
                var v2=NativeIntroCatalog.WeaponIntro(spec);var style=NativeIntroRules.Style(new NativeSeat{spec=spec});
                if(v2!=null)Check(style==v2&&NativeIntroRules.Signature(spec)==v2,spec+" plays its registered v2 intro");
                else Check(style!=null&&!NativeIntroCatalog.Registered(style)&&NativeIntroRules.Pickable(style),spec+" keeps its old entrance ("+style+") until its v2 intro registers");
            }
            foreach(var spec in NativeIntroCatalog.ArchivedClasses)
            {var style=NativeIntroRules.Style(new NativeSeat{spec=spec});Check(NativeIntroCatalog.DefaultReady?style==NativeIntroCatalog.DefaultId:!NativeIntroCatalog.Registered(style),spec+" takes the new default once registered ("+style+")");}
            Check(NativeIntroRules.Fallback==(NativeIntroCatalog.DefaultReady?NativeIntroCatalog.DefaultId:NativeIntroRules.Default)&&NativeIntroRules.Entrance("missing")==NativeIntroRules.Fallback,"an unknown style stages the fallback");
            // The registry: kinds, prefixes, DEV gating.
            var guest=NativeAccess.Guest;var dev2=Dev(now);var granted=Access(false,now+60000,"intro:"+NativeIntroCatalog.ShowcaseId);
            foreach(var e in NativeIntroCatalog.Entries)
            {
                string want=e.Kind==NativeIntroKind.Weapon?NativeIntroCatalog.WeaponPrefix+e.Spec:e.Kind==NativeIntroKind.Default?NativeIntroCatalog.DefaultId:null;
                Check(want!=null?e.Id==want:e.Kind==NativeIntroKind.Dev?e.Id.StartsWith("dev-",StringComparison.Ordinal):e.Id.StartsWith(NativeIntroCatalog.Prefix,StringComparison.Ordinal)&&!e.Id.StartsWith(NativeIntroCatalog.WeaponPrefix,StringComparison.Ordinal),e.Id+" has its kind's id");
                if(e.Kind==NativeIntroKind.Weapon)Check(Array.IndexOf(NativeIntroCatalog.WeaponSpecs,e.Spec)>=0&&NativeIntroRules.IntroWeapon(e.Id)=="weapon-"+e.Spec,e.Id+" is one of the fifteen weapons and holds its weapon");
                Check(!NativeIntroCatalog.Archived(e.Id)&&NativeIntroRules.Pickable(e.Id)&&NativeEntranceDirector.Directed(e.Id)&&NativeIntroRules.StyleName(e.Id)==e.Name&&NativeLocker.IntroText(e.Id).Length>0,e.Id+" is pickable, staged, named and has its Locker text");
                if(e.Kind==NativeIntroKind.Dev)
                {
                    Check(NativeIntroCatalog.Allowed(e.Id,dev2,now)&&!NativeIntroCatalog.Allowed(e.Id,guest,now)&&!NativeIntroCatalog.Allowed(e.Id,granted,now)&&!NativeIntroCatalog.Allowed(e.Id,Access(true,now-1000),now),e.Id+": only live DEV access");
                    Check(NativeIntroRules.Style(new NativeSeat{spec="rifle",intro=e.Id,overrideIntro=true,access=dev2})==e.Id&&NativeIntroRules.Style(new NativeSeat{spec="rifle",intro=e.Id,overrideIntro=true,access=guest})==NativeIntroRules.Signature("rifle"),e.Id+": staged for DEV, never for a guest");
                    var listed=new List<string>();NativeIntroCatalog.Listed(listed,guest,now);Check(!listed.Contains(e.Id),e.Id+" is not listed for a guest");
                }
                else{Check(NativeIntroCatalog.Allowed(e.Id,guest,now),e.Id+" is open to everyone");var listed=new List<string>();NativeIntroCatalog.Listed(listed,guest,now);Check(listed.Contains(e.Id),e.Id+" is listed");}
                string pickIt=e.Id;bool on=true;NativeIntroRules.Migrate(ref pickIt,ref on);Check(pickIt==e.Id&&on,e.Id+" survives migration");
            }
            // The rig and the ramps.
            var ramps=new NativeIntroTime().Ramp(2,.3f,.4f,.8f);float last=-1;bool monotone=true;for(int i=0;i<=600;i++){float a=i/60f,m=ramps.Map(a,10);if(m<last-1e-5f)monotone=false;last=m;}
            Check(monotone&&Math.Abs(ramps.Map(1.9f,10)-1.9f)<.02f&&ramps.Map(2.3f,10)<2.3f-.05f&&Math.Abs(ramps.Map(4f,10)-4f)<.02f&&Math.Abs(ramps.Map(9f,10)-9f)<.02f,"a speed ramp slows the clock and repays it ("+ramps.Map(2.3f,10)+", "+ramps.Map(4f,10)+")");
            // The void: its primitives, no allocation.
            var vp=new Props();NativeIntroVoid.Draw(vp,1,NativeIntroVoidLook.Of(new Vector3(.5f,.6f,1)));Check(vp.Primitives==NativeIntroVoid.Pieces,"the void draws its "+NativeIntroVoid.Pieces+" pieces");
            // The runtime wiring (read as text).
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f)).Replace("\r\n","\n");
            Check(R("Runtime/NativeIntros.cs").Contains("IntroV2Begin(script,pilot,quality);")&&R("Runtime/NativeIntros.cs").Contains("IntroV2Set(script,t,camera);"),"DirectedIntro sets the context and draws the set");
            Check(R("Runtime/NativeAudio.cs").Contains("if(IntroV2Foley(drawnAge))return;")&&R("Runtime/NativeIntroAudio.cs").Contains("entranceCues.Crossed(cues[i].At)"),"the cues play through the entrance cue clock");
            Check(R("Runtime/NativeMatchOverlays.cs").Contains("if(!boss&&IntroCardV2(pilot,style,rawAge,index,total,team))return;"),"the match intro card hands a v2 intro to its own card");
            Check(R("Runtime/NativeLockerUI.cs").Contains("LockerIntrosV2(lockerIntros,access);")&&R("Runtime/NativeIntroLockerList.cs").Contains("if(NativeIntroCatalog.Archived(intros[i]))intros.RemoveAt(i);"),"the Locker hides the archive and lists v2");
            Check(R("Runtime/NativeProfileUI.cs").Contains("public static bool PickableIntro(string value,NativeAccess a)=>NativeIntroCatalog.Registered(value)?"),"saved and profile picks keep v2 ids and drop archived ones");
            Check(!R("Core/NativeIntroV2.cs").Contains("NativeEntranceStage.Arena(")&&!R("Core/NativeIntroVoid.cs").Contains("WeaponShape.Ring"),"the v2 base and its void never draw the block arena or a ring");
        }
        private static readonly float[] SavGol={-21,14,39,54,59,54,39,14,-21};
        private static NativeAccess Dev(double now)=>Access(true,now+60000);
        private static NativeAccess Access(bool developer,double expires,params string[] grants)
        {
            var a=(NativeAccess)Activator.CreateInstance(typeof(NativeAccess),true);var t=typeof(NativeAccess);
            t.GetProperty("Developer").SetValue(a,developer);t.GetProperty("Expires").SetValue(a,expires);
            var set=(HashSet<string>)t.GetField("grants",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(a);foreach(var g in grants)set.Add(g);return a;
        }
        // ---- one registered intro ----
        private static void Intro(NativeIntroEntry e)
        {
            string n=e.Id;var made=e.Make();Check(made!=null&&made.Style==e.Id&&made is INativeIntroV2,n+": its maker builds it (as itself, an INativeIntroV2)");
            var s=made;var v2=(INativeIntroV2)s;var again=e.Make();var v2b=(INativeIntroV2)again;
            float max=e.Kind==NativeIntroKind.Dev?NativeIntroSetLimits.DevLongest:NativeIntroSetLimits.Longest;bool devKind=e.Kind==NativeIntroKind.Dev;
            Check(e.Duration>=6-1e-4f&&e.Duration<=max+1e-4f,n+": 6 to "+max+" s ("+e.Duration+")");Check(e.NameAt>0&&e.NameAt<e.Duration-.8f,n+": the name beat lands with time to read it ("+e.NameAt+")");
            Check(Math.Abs(s.Length-e.Duration)<1e-4f&&(!(s is NativeIntroV2Script b)||Math.Abs(b.IntroNameAt-e.NameAt)<1e-4f),n+": the registry's Duration and NameAt are the script's");
            Check(Math.Abs(NativeIntroRules.Duration(n)-e.Duration)<1e-4&&Math.Abs(NativeIntroRules.NameAt(n)-e.NameAt)<1e-4,n+": the match reads them");
            Check(s.Ready,n+": always Ready (a procedural fallback where a clip may be missing)");
            var pose=new CapturedRig.Pose();var lights=new NativeEntranceLights();var props=new Props();var fx=new Fx();
            // Determinism: two scripts, one played forward, the other scrubbed backward, agree at every sampled age.
            int samples=(int)(e.Duration*20);var fwd=new double[samples+1];
            for(int i=0;i<=samples;i++)fwd[i]=Frame(s,v2,i/20f,pose,lights,props,fx,false,false);
            for(int i=samples;i>=0;i--){double h=Frame(again,v2b,i/20f,pose,lights,props,fx,false,false);if(Math.Abs(h-fwd[i])>1e-6*Math.Max(1,Math.Abs(h)))Check(false,n+": not deterministic at age "+(i/20f));}
            count++;
            // Allocation: none per frame after the first pass (all calls).
            for(int i=0;i<=samples;i++)Frame(s,v2,i/20f+.013f,pose,lights,props,fx,true,true);
            long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<=samples;i++){Frame(s,v2,i/20f+.007f,pose,lights,props,fx,false,false);Frame(s,v2,i/20f+.011f,pose,lights,props,fx,true,true);}
            long used=GC.GetAllocatedBytesForCurrentThread()-before;Check(used<=512,n+": no per-frame allocation ("+used+" bytes over "+(2*samples+2)+" frames)");
            // Budgets at peak, the streak rule, finite values; on High and on Low.
            int peakFx=0,peakLowFx=0,peakPrims=0,peakPieces=0,peakLowPieces=0,peakBatch=0,long_=0,off=0;float worst=0;
            for(int i=0;i<=e.Duration*60;i++)
            {
                float age=i/60f;Frame(s,v2,age,pose,lights,props,fx,false,false);peakFx=Math.Max(peakFx,fx.Calls);peakPrims=Math.Max(peakPrims,props.Primitives);peakPieces=Math.Max(peakPieces,props.Pieces);long_+=fx.LongStreaks;off+=fx.OffPlane;worst=Math.Max(worst,fx.WorstDeg);
                Check(!fx.Broken&&lights.Points<=8,n+": finite effects and at most 8 lights at "+age);
                peakBatch=Math.Max(peakBatch,props.Batched);Frame(s,v2,age,pose,lights,props,fx,false,true);peakLowFx=Math.Max(peakLowFx,fx.Calls);peakLowPieces=Math.Max(peakLowPieces,props.Pieces);peakBatch=Math.Max(peakBatch,props.Batched);
            }
            int fxHigh=devKind?NativeIntroSetLimits.DevFx:NativeIntroSetLimits.Fx,fxLow=devKind?NativeIntroSetLimits.DevFxLow:NativeIntroSetLimits.Fx,pcHigh=devKind?NativeIntroSetLimits.DevPieces:NativeIntroSetLimits.Pieces,pcLow=devKind?NativeIntroSetLimits.DevPiecesLow:NativeIntroSetLimits.Pieces;
            Check(peakFx<=fxHigh&&peakLowFx<=fxLow,n+": at most "+fxHigh+"/"+fxLow+" effect calls a frame ("+peakFx+" High, "+peakLowFx+" Low)");Check(peakPrims<=120&&peakPieces<=pcHigh&&peakLowPieces<=pcLow&&peakBatch<=NativeIntroSetLimits.Batch,n+": at most 120 primitives, "+pcHigh+"/"+pcLow+" set pieces and "+NativeIntroSetLimits.Batch+" batched copies a frame ("+peakPrims+", "+peakPieces+"/"+peakLowPieces+", "+peakBatch+")");
            Check(off==0,n+": every streak over 1 m lies on its head's camera plane ("+off+" of "+long_+" off, worst "+worst.ToString("0.0")+" deg; NativeCacheOpenFx.OntoHeadPlane)");
            // Reduced effects: no shake (the eye's acceleration stays smooth outside cuts) and nothing flashing faster than 3 Hz.
            var eye=new Vector3[(int)(e.Duration*60)+3];var power=new double[eye.Length];var glow=new double[eye.Length];
            for(int i=0;i<eye.Length;i++){float age=i/60f;Frame(s,v2,age,pose,lights,props,fx,true,false);s.Camera(age,out var view);eye[i]=view.Eye;power[i]=Power(lights);glow[i]=fx.Emit;}
            // (shake: what a quadratic fit over +-4 frames cannot follow, Savitzky-Golay; a cut's window is skipped)
            float worstShake=0,shakeAt=0;
            for(int i=4;i+4<eye.Length;i++)
            {
                bool cut=false;for(int k=-3;k<=4;k++)if((eye[i+k]-eye[i+k-1]).Length()>.25f)cut=true;if(cut)continue;
                var fit=Vector3.Zero;for(int k=-4;k<=4;k++)fit+=eye[i+k]*SavGol[k+4];fit/=231f;float r=(eye[i]-fit).Length();if(r>worstShake){worstShake=r;shakeAt=i/60f;}
            }
            Check(worstShake<.006f,n+": the Reduced camera never shakes (worst wobble "+(worstShake*1000).ToString("0.0")+" mm at "+shakeAt.ToString("0.00")+" s outside cuts)");
            Check(Flashes(power)<=3&&Flashes(glow)<=3,n+": in Reduced effects nothing flashes faster than 3 Hz (lights "+Flashes(power)+", glows "+Flashes(glow)+" in the worst second)");
            // Cues: sorted, inside the intro, each with something to play, each heard once at 60 Hz and at a guest's 20 Hz steps.
            var cues=v2.Cues??Array.Empty<NativeIntroCue>();
            for(int i=0;i<cues.Length;i++){var c=cues[i];Check(c.At>=0&&c.At<=e.Duration&&(i==0||c.At>=cues[i-1].At)&&(c.Key!=null||c.Alt!=null||c.Clip!=null)&&c.Gain>0&&c.Gain<=1.5f&&(c.Key==null||c.Key.StartsWith("intro2.",StringComparison.Ordinal)),n+": cue "+i+" at "+c.At+" is in order, inside the intro and playable");}
            foreach(int hz in new[]{60,20})
            {
                var clock=new NativeEntranceCueClock();var heard=new int[cues.Length];
                for(int f=0;f<=e.Duration*hz+2;f++){clock.Step("check",n,s.Clock(f/(float)hz));for(int i=0;i<cues.Length;i++)if(clock.Crossed(cues[i].At))heard[i]++;}
                Check(heard.All(h=>h==1),n+": every cue heard exactly once at "+hz+" Hz");
            }
            // The card: shown from the name beat, gone by the end; the fallback set without a bank.
            var card=default(NativeIntroCardState);v2.Card(s.Clock(e.NameAt-.05f),ref card);bool early=card.Shown&&card.Age>.06f;card=default;v2.Card(s.Clock(e.NameAt+.5f),ref card);
            Check(!early&&card.Shown,n+": the name card arrives on the name beat");
            props.Reset();props.Ready=false;props.Bank=false;v2.Set(s.Clock(e.NameAt),props);Check(props.Primitives>0||props.Pieces>0,n+": without its bank it still draws a set (the void or its own primitives)");
            // With a bank that has every prop: its Scene's pieces and batches inside the budgets, finite, deterministic, allocation-free.
            if(v2.SetBank!=null)
            {
                var bank=new Props{Ready=true,Bank=true};int peak=0,peakBatch2=0;
                for(int i=0;i<=e.Duration*30;i++){bank.Reset();v2.Context=new NativeIntroContext{Quality=1,Name="P"};v2.Set(s.Clock(i/30f),bank);peak=Math.Max(peak,bank.Pieces);peakBatch2=Math.Max(peakBatch2,bank.Batched);}
                Check(peak<=(devKind?NativeIntroSetLimits.DevPieces:NativeIntroSetLimits.Pieces)&&peakBatch2<=NativeIntroSetLimits.Batch,n+": its bank set stays inside the budgets ("+peak+" pieces, "+peakBatch2+" batched)");
                long b0=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<=e.Duration*30;i++){bank.Reset();v2.Set(s.Clock(i/30f+.01f),bank);}Check(GC.GetAllocatedBytesForCurrentThread()-b0<=256,n+": its bank set allocates nothing per frame");
            }
        }
        /// <summary>The most upward crossings of a level's flash threshold (its mean plus half its peak swing) in any one second at 60 Hz.</summary>
        private static int Flashes(double[] v)
        {
            double mean=v.Average(),peak=v.Max();if(peak-mean<1e-3||peak<=1e-6)return 0;double th=mean+(peak-mean)*.5,hyst=(peak-mean)*.15;var ups=new List<int>();bool high=v[0]>th;
            for(int i=1;i<v.Length;i++){if(!high&&v[i]>th+hyst){high=true;ups.Add(i);}else if(high&&v[i]<th-hyst)high=false;}
            int most=0;for(int a=0;a<ups.Count;a++){int k=0;for(int b=a;b<ups.Count&&ups[b]-ups[a]<60;b++)k++;most=Math.Max(most,k);}return most;
        }
    }
}
