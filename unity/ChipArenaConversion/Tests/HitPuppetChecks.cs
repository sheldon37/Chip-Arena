#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
namespace ChipArena.UnityConversion
{
    // Physical hit reactions on the drawn models (owner 2026-11-21: "Utilize Ragdoll Asset for hit reactions"; Core
    // NativeHitPuppetRules, Runtime NativeHitPuppets/NativeHitPuppetPresentation, docs/HIT-PUPPETS.md):
    //  - the push grows with the blow (a greatsword's heavy blow far past a pistol round), is bounded, and pushes nothing for a
    //    damage tick or a hazard; the cost is capped by tier (none on the low tier: Performance keeps today's look);
    //  - the licensed PuppetMaster never reaches this repository: game code names none of its types and reaches it only through
    //    the hook a git-ignored local bridge fills (as the cloth bridge does), every hook needed for it to run;
    //  - presentation only: the hook and the presentation write no rule, body, hit volume, timing or network state; each puppet
    //    steps right after its body is posed and before anything reads its bones; a killing hit pushes nothing; every body
    //    goes with its puppet;
    //  - where and from where (owner 2026-11-23: "I want the animation to actually display hit direction (Like reactively)"): a
    //    pilot's hit point is measured from its body centre (the rig root), not its feet; a blade's blow pushes along its own
    //    swing (opposite slashes throw the body opposite ways) and lands on the side the blade comes in from; a headshot on the
    //    head's side the shot came from; the reach grows with the blow (a pistol's flick local, a greatsword's whole body) and a
    //    shambler gives the most; the model's directional hit clip follows the blow, never the pilot's own facing; the local
    //    pilot in first person draws no body to react.
    internal static class HitPuppetChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Hit puppets: "+what);count++;}
        private static string R(string root,string path)=>File.ReadAllText(Path.Combine(root,path));
        private static string Body(string text,string head)
        {int i=text.IndexOf(head,StringComparison.Ordinal);if(i<0)return "";int b=text.IndexOf('{',i),depth=0;for(int k=b;k<text.Length;k++){if(text[k]=='{')depth++;else if(text[k]=='}'&&--depth==0)return text.Substring(i,k-i+1);}return "";}
        public static void Run(string root)
        {
            count=0;Rules();Direction();NoLicensedCode(root);Hook(root);Wiring(root);Directional(root);PresentationOnly(root);
            Console.WriteLine("PASS hit puppets ("+count+" checks: the push grows with the blow and is bounded, ticks and hazards push nothing; capped by tier, none on the low tier; no licensed code or type in the repository, the hook needs every call; each puppet steps right after its body's pose, a killing hit pushes nothing; nothing written to rules, bodies or the wire; points from the body centre, blades along their swing, headshots on the head's struck side, reach by weight, shamblers loosest, the hit clip by the blow's side, no reaction in first person)");
        }
        private static float Push(StaggerWeight w,float amount,bool melee,string spec,bool head=false)
        {
            if(!NativeHitPuppetRules.Push(w,NativeStaggerRules.Scale(w,amount),melee,NativeHitRules.Heft(spec,amount),head,out float impulse,out float unpin))return 0;
            if(!(unpin>0&&unpin<=1))throw new Exception("Hit puppets: unpin "+unpin+" out of (0, 1]");
            return impulse;
        }
        // ---- 1. the push and the caps ----
        private static void Rules()
        {
            float greatsword=Push(NativeStaggerRules.Classify("greatsword","direct",48),48,true,"greatsword");
            float sword=Push(NativeStaggerRules.Classify("sword","direct",30),30,true,"sword");
            float daggers=Push(NativeStaggerRules.Classify("daggers","direct",14),14,true,"daggers");
            float rifle=Push(NativeStaggerRules.Classify("rifle","direct",30),30,false,"rifle");
            float pistol=Push(NativeStaggerRules.Classify("pistol","direct",14),14,false,"pistol");
            Check(greatsword>sword&&sword>daggers&&sword>rifle&&rifle>pistol&&pistol>0,"the push grows with the blow: greatsword "+greatsword+" > sword "+sword+" > daggers "+daggers+", sword > rifle "+rifle+" > pistol "+pistol);
            Check(greatsword>=4*pistol,"a greatsword's heavy blow pushes at least four times a pistol round ("+greatsword+" vs "+pistol+")");
            Check(Push(StaggerWeight.Heavy,1000,true,"greatsword")<=NativeHitPuppetRules.MaxImpulse&&Push(StaggerWeight.Heavy,float.NaN,true,"greatsword")<=NativeHitPuppetRules.MaxImpulse,"the push is bounded (and survives a non-finite amount)");
            Check(Push(StaggerWeight.Step,30,false,"rifle",true)<rifle,"a headshot pushes the light head less than a body shot");
            Check(Push(StaggerWeight.None,30,true,"greatsword")==0&&!NativeHitPuppetRules.Push(StaggerWeight.None,1,true,1,false,out _,out _),"a hit that staggers nothing pushes nothing");
            NativeHitPuppetRules.Push(StaggerWeight.Heavy,1,true,1.4f,false,out _,out float heavyUnpin);NativeHitPuppetRules.Push(StaggerWeight.Flinch,1,false,.5f,false,out _,out float flinchUnpin);
            Check(heavyUnpin>flinchUnpin,"a heavy blow unpins the muscles further than a flinch");
            foreach(var detail in new[]{"secondary","field","thorns","hazard","stage-vent","collapse","burner"})Check(!NativeHitPuppetRules.Felt(detail,false),"a "+detail+" hit (a tick, a hazard: its own presentation) pushes nothing");
            foreach(var detail in new[]{"direct","ability","blast",null})Check(NativeHitPuppetRules.Felt(detail,false),"a "+(detail??"plain")+" hit pushes");
            Check(NativeHitPuppetRules.Felt("secondary",true),"a melee blow pushes whatever its detail (the Siren's claws)");
            Check(NativeHitPuppetRules.Cap(NativeGraphicsTier.Low,false)==0&&NativeHitPuppetRules.Reach(NativeGraphicsTier.Low)==0,"the low tier (Performance) reacts nothing: today's look");
            Check(NativeHitPuppetRules.Cap(NativeGraphicsTier.High,false)==8&&NativeHitPuppetRules.Cap(NativeGraphicsTier.Medium,false)==4&&NativeHitPuppetRules.Cap(NativeGraphicsTier.High,true)==4,"at most 8 bodies react at once on High, 4 on Balanced, half with Reduced effects");
            Check(NativeHitPuppetRules.Reach(NativeGraphicsTier.High)>NativeHitPuppetRules.Reach(NativeGraphicsTier.Medium)&&NativeHitPuppetRules.Reach(NativeGraphicsTier.High)<=30,"the reach shrinks with the tier and stays near the camera");
            Check(NativeHitPuppetRules.MakesPerFrame==1&&NativeHitPuppetRules.Kept>=NativeHitPuppetRules.Cap(NativeGraphicsTier.High,false)&&NativeHitPuppetRules.Kept<=24,"one puppet made a frame, a bounded number kept made");
        }
        // ---- 1b. where and from where ----
        private static System.Numerics.Vector3 V(float x,float y,float z)=>new System.Numerics.Vector3(x,y,z);
        private static void Direction()
        {
            // The reach grows with the blow: a pistol's flick stays local, a greatsword's heavy blow reaches the whole body.
            float pistol=NativeHitPuppetRules.Spread(StaggerWeight.Flinch,false,.5f),rifle=NativeHitPuppetRules.Spread(StaggerWeight.Step,false,.5f);
            float sword=NativeHitPuppetRules.Spread(StaggerWeight.Step,true,1),greatsword=NativeHitPuppetRules.Spread(StaggerWeight.Heavy,true,1.4f);
            Check(pistol<rifle&&rifle<sword&&sword<greatsword&&pistol<=.1f&&greatsword>=.85f&&greatsword<=1,"the reach grows with the blow: pistol "+pistol+" < rifle "+rifle+" < sword "+sword+" < greatsword "+greatsword+" (the whole body)");
            Check(NativeHitPuppetRules.Spread(StaggerWeight.None,true,1)==0&&NativeHitPuppetRules.Spread(StaggerWeight.Heavy,true,float.NaN)<=1,"no reach for a hit that staggers nothing; bounded for a non-finite heft");
            // A shambler gives the most; an unknown kind (a pilot) the least.
            Check(NativeHitPuppetRules.Loose(NativeHordeKinds.Shambler)==1&&NativeHitPuppetRules.Loose(NativeHordeKinds.Shambler)>NativeHitPuppetRules.Loose(NativeHordeKinds.Runner)
                &&NativeHitPuppetRules.Loose(NativeHordeKinds.Runner)>NativeHitPuppetRules.Loose(NativeHordeKinds.Brute)&&NativeHitPuppetRules.Loose(NativeHordeKinds.Brute)>NativeHitPuppetRules.Loose(NativeHordeKinds.Hulk)
                &&NativeHitPuppetRules.Loose(-1)==0,"shamblers are the loosest, then runners, brutes, the Hulk; a pilot (no kind) is firm");
            // A pilot's point is measured from its body centre: the rig root stands that far over its feet (Actor.Position).
            Check(NativeHitPuppetRules.CentreHeight==NativeMeleeSlice.RootHeight&&Math.Abs(NativeHitPuppetRules.CentreHeight+NativeVisualRig.Floor)<1e-6f,"a pilot's hit points from its body centre, the rig root .865 m over its feet");
            // A blade's push turns along its swing: a victim facing +z struck by an attacker in front of it (the blow along -z).
            var centre=V(0,1,0);var along=V(0,0,-1);
            bool sa=NativeHitPuppetRules.Swing(along,V(1,0,0),centre,1.2f,.865f,out var a,out var pa),sb=NativeHitPuppetRules.Swing(along,V(-1,0,0),centre,1.2f,.865f,out var b,out var pb);
            Check(sa&&sb,"a slash's push is read");
            Check(a.X>.6f&&b.X<-.6f&&a.Z<0&&b.Z<0,"opposite slashes throw the body opposite ways ("+a+" / "+b+"), both still away from the attacker");
            Check(pa.X<-.1f&&pb.X>.1f&&pa.Z>0&&pb.Z>0,"a slash lands on the side its blade comes in from, on the attacker's side ("+pa+" / "+pb+")");
            Check(Math.Abs(pa.Y-(centre.Y+1.2f-.865f))<1e-4f,"at the cut's height");
            Check(NativeHitPuppetRules.Swing(along,along,centre,1,.865f,out var thrust,out _)&&thrust.Z<-.99f&&Math.Abs(thrust.X)<1e-3f,"a thrust (travelling along the blow) stays a push away from the attacker");
            Check(NativeHitPuppetRules.Swing(along,V(0,-1,0),centre,1.3f,.865f,out var chop,out _)&&chop.Y<-.5f&&chop.Z<0,"a chop drives down and back ("+chop+")");
            Check(!NativeHitPuppetRules.Swing(along,V(0,0,0),centre,1,.865f,out _,out _)&&!NativeHitPuppetRules.Swing(along,V(float.NaN,0,0),centre,1,.865f,out _,out _),"no travel, no swing (the authority's push stays)");
            Check(Math.Abs(a.Length()-1)<1e-4f&&Math.Abs(chop.Length()-1)<1e-4f,"the swing's push is a direction (its size is the blow's)");
        }
        // ---- 2. no licensed code in the repository ----
        private static void NoLicensedCode(string root)
        {
            foreach(var dir in new[]{"Assets/ChipArena","Tests"})
                foreach(var f in Directory.GetFiles(Path.Combine(root,dir),"*.cs",SearchOption.AllDirectories))
                {
                    var text=File.ReadAllText(f);
                    if(f.EndsWith("HitPuppetChecks.cs",StringComparison.Ordinal))continue;
                    Check(!text.Contains("using RootMotion")&&!text.Contains("RootMotion.Dynamics")&&!text.Contains("PuppetMaster.")&&!text.Contains("BehaviourPuppet"),Path.GetFileName(f)+" names none of PuppetMaster's types (they stay in the git-ignored local install)");
                }
            Check(!Directory.Exists(Path.Combine(root,"Assets/ChipArena/PrivateAddons"))&&!Directory.Exists(Path.Combine(root,"Assets/RootMotion"))&&!Directory.Exists(Path.Combine(root,"Assets/Plugins/RootMotion")),"no PuppetMaster folder outside the git-ignored LocalAssets");
            var ignore=R(root,".gitignore");Check(ignore.Contains("/Assets/LocalAssets/"),"LocalAssets (where the local install lives) is git-ignored");
        }
        // ---- 3. the hook ----
        private static void Hook(string root)
        {
            var hook=R(root,"Assets/ChipArena/Runtime/NativeHitPuppets.cs");
            foreach(var decl in new[]{"public static Func<Transform,object> Attach;","public static Func<object,Vector3,Vector3,float,bool> Hit;","public static Func<object,float,bool> Step;","public static Func<object,bool> Awake;","public static Action<object> Fade;","public static Action<object> Sleep;","public static Action<object> Release;"})
                Check(hook.Contains(decl),"the hook declares `"+decl+"` (the local bridge fills it by name)");
            Check(hook.Contains("public static bool Registered=>Attach!=null&&Hit!=null&&Step!=null&&Awake!=null&&Fade!=null&&Sleep!=null&&Release!=null;"),"every hook is needed: a partial registration draws the bodies as posed");
            foreach(var decl in new[]{"public static Func<object,Vector3,Vector3,float,float,float,bool> Strike;","public static Action<object,float,float> Bounds;","public static Action<string> Trace;"})
                Check(hook.Contains(decl),"the hook declares the optional `"+decl+"` (a bridge without it reacts through Hit alone)");
            var pres=R(root,"Assets/ChipArena/Runtime/NativeHitPuppetPresentation.cs");
            Check(pres.Contains("private bool HitPuppetsOn=>!hitPuppetsFailed&&!Viewer&&NativeHitPuppets.Registered;"),"nothing runs without the registered bridge (and never in the viewer)");
            Check(pres.Contains("private int HitPuppetCap=>LowGraphics?0:NativeHitPuppetRules.Cap(GraphicsTier,EffectsQuality<.5f);"),"the cap follows the tier (none on the low graphics tier) and Reduced effects");
            // Every bridge call is guarded: an exception turns the reactions off for the session instead of breaking the frame.
            foreach(var call in new[]{"NativeHitPuppets.Awake(h)","NativeHitPuppets.Sleep(h)","NativeHitPuppets.Fade(h)","NativeHitPuppets.Step(h,dt)","NativeHitPuppets.Hit(h,point,push,unpin)","NativeHitPuppets.Strike(h,point,push,unpin,spread,loose)","NativeHitPuppets.Bounds(h,radius,height)","NativeHitPuppets.Attach(skeleton)","NativeHitPuppets.Release(p.Handle)"})
            {
                int at=pres.IndexOf(call,StringComparison.Ordinal);Check(at>0,"the presentation calls "+call);
                int line=pres.LastIndexOf('\n',at);string text=pres.Substring(line,pres.IndexOf('\n',at)-line);
                Check(text.Contains("try{")&&text.Contains("catch(Exception e){HitPuppetFailed(e);"),call+" is guarded (an exception turns the reactions off, the bodies draw as posed)");
            }
        }
        // ---- 4. the wiring: where hits come in and where the puppets step ----
        private static void Wiring(string root)
        {
            string run(string f)=>R(root,"Assets/ChipArena/Runtime/"+f);
            var hit=run("NativeHitPresentation.cs");Check(hit.Contains("public void ObserveHits(Snapshot s){hitLedger.Observe(s);HitPuppetsObserve(s);}"),"pilot hits come from the frame's events, beside the felt-hit ledger");
            var body=run("NativeVisualBodyPresentation.cs");var pose=Body(body,"private void VisualBodyPose(Actor a,float time)");
            int dog=pose.IndexOf("TopDogBody(body,a,time);",StringComparison.Ordinal),step=pose.IndexOf("HitPuppetPilot(body,rig,a,time);",StringComparison.Ordinal),moved=pose.IndexOf("body.Moved=true;",StringComparison.Ordinal);
            Check(dog>0&&step>dog&&moved>step,"a pilot's puppet steps after every part of its pose (stride, pack clips, hand fit, the Hot Dog's) and before anything reads its bones");
            Check(Body(body,"private void ClearVisualBodies()").Contains("HitPuppetsClear();"),"every puppet goes with the bodies");
            var zm=run("NativeZombieModelPresentation.cs");var bind=Body(zm,"private void ZombieModelBind(");
            int carry=bind.IndexOf("z.CarryKnown=true;}",StringComparison.Ordinal),zstep=bind.IndexOf("HitPuppetZombie(z);",StringComparison.Ordinal),gear=bind.IndexOf("ZombieCarry(z,t,flash);ZombieSuit(z,flash);\n            DiagnosticZombiePoseMs",StringComparison.Ordinal);
            Check(carry>0&&zstep>carry&&gear>zstep,"an add's puppet steps after its pose and before its gear and suit read the bones");
            Check(bind.Contains("||z.ChangedParts(lost,hidden)||HitPuppetAwake(z.Body);"),"a reacting add is posed every frame (the crowd's pose clock never holds a bent pose)");
            var kinds=run("NativeHordeKindPresentation.cs");var horde=Body(kinds,"private void HordeHit(");
            Check(horde.Contains("if(m!=null)HitPuppetAdd(m,e,w,melee,spec,"),"add hits come from HordeHit, the replicated hit every client reads, only for a living add");
            Check(run("NativeSourcePresentation.cs").Contains("public void FinishSourceFrame(){HitPuppetsSweep();"),"once a frame a puppet whose body was not drawn sleeps, and a gone body's puppet is released");
            var pres=run("NativeHitPuppetPresentation.cs");var observe=Body(pres,"private void HitPuppetsObserve(Snapshot s)");
            Check(observe.Contains("lethal=events[k].Kind==EventKind.Death&&events[k].Target==e.Target")&&observe.Contains("if(lethal)continue;")&&observe.Contains("if(target==null||!target.Alive)continue;"),"a killing hit pushes nothing (the death's own presentation takes the body)");
            Check(observe.Contains("e.Target>=10000")&&observe.Contains("if(newest>0&&newest<hitPuppetEvent){hitPuppetEvent=0;hitPuppetHitCount=0;}"),"pilot hits only (adds come from HordeHit), each once, starting over with a new arena or replay");
            Check(Body(pres,"private void HitPuppetAdd(").Contains("!(m.Hp>0)"),"an add's killing hit pushes nothing");
            Check(pres.Contains("NativeHitPuppets.Strike!=null?NativeHitPuppets.Strike(h,point,push,unpin,spread,loose):NativeHitPuppets.Hit(h,point,push,unpin)"),"a bridge with Strike takes the blow's reach and the body's give; the first bridge (an older licensed-assets zip) still reacts through Hit");
            Check(observe.Contains("melee|=NativeBladeCuts.Cuts(e,attacker);")&&horde.Contains("melee|=blade;"),"a blade class's cut (the Katana's, the Fangs', the Glaive's: its detail names the move) pushes as a blade's blow, on pilots and adds");
            var land=Body(pres,"private bool HitPuppetPose(");
            Check(land.Contains("time-h.At>NativeHitPuppetRules.Stale")&&land.Contains("h.At<time+NativeHitPuppetRules.Ahead"),"a hit lands on its body's own clock (it waits for its moment, a stale one is dropped)");
            Check(land.Contains("slot.PosedFrame<frame-1||slot.Owner!=owner")&&land.Contains("NativeHitPuppetRules.Jump"),"a gap, another owner or a jump puts the drawn pose back first");
            Check(land.Contains("if(dying||HitPuppetCap==0||!HitPuppetNear(")&&land.Contains("HitPuppetFade(slot.Handle);"),"a dying body, a lowered tier or one past the reach blends back");
            var make=Body(pres,"private void HitPuppetLand(");
            Check(make.Contains("if(cap<=0||!HitPuppetNear(anchor,NativeHitPuppetRules.Reach(GraphicsTier)))return;")&&make.Contains("if(awake>=cap)return;")&&make.Contains("if(hitPuppetMakes>=NativeHitPuppetRules.MakesPerFrame)return;")&&make.Contains("if(hitPuppets.Count>=NativeHitPuppetRules.Kept)"),"a new reaction waits within the cap, the reach, the frame's making and the puppets kept");
        }
        // ---- 4b. where and from where (owner 2026-11-23) ----
        private static void Directional(string root)
        {
            string run(string f)=>R(root,"Assets/ChipArena/Runtime/"+f);
            var pres=run("NativeHitPuppetPresentation.cs");var observe=Body(pres,"private void HitPuppetsObserve(Snapshot s)");
            Check(observe.Contains("Vector3 centre=Vec(target.Position)+Vector3.up*NativeHitPuppetRules.CentreHeight,point=Vec(e.Position)")&&observe.Contains("Offset=point-centre")&&!observe.Contains("Offset=Vec(e.Position)-at"),
                "a pilot's hit point is measured from its body centre (the rig root its hits land from), not its feet (every point rose .865 m onto the chest or head)");
            Check(observe.Contains("bool swung=melee&&!head&&hitLedger.Blow(e.Target,e.Time,out var blow)&&HitPuppetSwing(blow,e.Id,centre,target.Yaw,ref dir,ref point);"),"a pilot's blade blow pushes along the swing its attacker played (the hit ledger's blow)");
            var swing=Body(pres,"private bool HitPuppetSwing(");
            Check(swing.Contains("NativeMeleeSlice.Plan(blow,true,blow.Spec,null,NativeArena.Vec(centre),yaw,seed,hitPuppetPlan);")&&swing.Contains("NativeHitPuppetRules.Swing("),"the swing is read as a kill's cut reads it (Core NativeMeleeSlice.Plan: the attacker's cut turned by its committed yaw)");
            Check(observe.Contains("Spread=spread")&&Body(pres,"private void HitPuppetAdd(").Contains("loose=NativeHitPuppetRules.Loose(kind)"),"each hit carries its reach, an add's its kind's give");
            var land=Body(pres,"private void HitPuppetLand(");
            Check(land.Contains("point=bones[hd].position+up*HitHeadCentre-h.Dir*HitHeadSide;")&&!land.Contains("if(h.Head&&hd>=0)point=bones[hd].position;"),"a headshot lands on the head's side the shot came from, not its pivot on the neck");
            Check(land.Contains("HitPuppetBounds(slot.Handle,radius,height);")&&land.Contains("NativeLocomotionRules.Radius*scale"),"the bridge is told each body's hitbox (a pilot's capsule, an add's own) to keep the bent pose in");
            var horde=Body(run("NativeHordeKindPresentation.cs"),"private void HordeHit(");
            int blow=horde.IndexOf("else if(blade){s.Spec=spec;s.HasBlow=true;s.BlowAt=e.Time;s.Blow=NativeBladeCuts.Blow(e,actor);}",StringComparison.Ordinal),add=horde.IndexOf("if(m!=null)HitPuppetAdd(",StringComparison.Ordinal);
            Check(blow>0&&add>blow&&horde.Contains("kind,s.HasBlow&&Math.Abs(s.BlowAt-e.Time)<.004f,s.Blow);"),"an add's blade blow pushes along the swing just recorded for it, its kind's give with it");
            // The model's directional hit clip: the blow's side, never the pilot's own facing (ReactionYaw is the facing at the hit).
            var motion=R(root,"Assets/ChipArena/Core/NativeVisualMotion.cs");
            Check(!motion.Contains("Side(a.ReactionYaw)")&&motion.Contains("set.Reactions[HitSide(a)]")&&motion.Contains("return x*x+z*z<1e-8f?0:Side((float)Math.Atan2(-x,-z));"),"the model's hit clip is the blow's side (Actor.ReactionDir), never the pilot's own facing");
            // First person: the local pilot's own body is never posed, so no reaction moves its view.
            var pose=Body(run("NativeVisualBodyPresentation.cs"),"private void VisualBodyPose(Actor a,float time)");
            Check(pose.IndexOf("if(!ModelBody(a)||FirstPerson&&a.Id==LocalId||",StringComparison.Ordinal)>0&&pose.IndexOf("HitPuppetPilot(body,rig,a,time);",StringComparison.Ordinal)>pose.IndexOf("FirstPerson&&a.Id==LocalId",StringComparison.Ordinal),"the local pilot in first person draws no body, so its own reaction never moves its view");
        }
        // ---- 5. presentation only ----
        private static void PresentationOnly(string root)
        {
            var pres=R(root,"Assets/ChipArena/Runtime/NativeHitPuppetPresentation.cs");var hook=R(root,"Assets/ChipArena/Runtime/NativeHitPuppets.cs");
            foreach(var (name,text) in new[]{("presentation",pres),("hook",hook)})
                foreach(var bad in new[]{".Hp=",".Hp-=","Body.position=","Body.linearVelocity","Knockback","Emit(","Damage(","StunEnd","Send(","NativeNetwork","PhysicsWorld",".Reaction=","Collider."})
                    Check(!text.Contains(bad),name+" writes no rule, body, hit volume, timing or network state (`"+bad+"`)");
            // The rig's hands follow the drawn hands only within this frame: VisualBodyRestore puts them back before the next pose.
            var body=R(root,"Assets/ChipArena/Runtime/NativeVisualBodyPresentation.cs");
            Check(Body(body,"private void VisualBodyRestore(int id)").Contains("bone.localPosition=body.HandPosition[side];bone.localRotation=body.HandRotation[side];"),"the rig's hands are put back before every pose (the carried weapon follows the bent hands for one frame only)");
            Check(!pres.Contains("rig.Root.transform.position=")&&!pres.Contains("Root.transform.SetPosition")&&!pres.Contains(".Root.transform.rotation="),"the body's root stays where the game put it");
        }
    }
}
#endif
