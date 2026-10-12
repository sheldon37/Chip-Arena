using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using ChipArena.UnityConversion;

/// <summary>
/// The Bow's presentation (Claude; the rules are proposed to Astra in docs/NEW-KITS.md section 2 and read only through
/// Core NativeBowWire): the models (budgets, tiers, determinism, the Prototype Series pinned), the bow frame's sockets, the
/// string and arrow following the draw, the aim converging on the crosshair in third and first person, the poses reaching,
/// keying on the replicated data only, the stuck arrows' lifecycle, the effects and HUD, the Arrowfall entrance's beats,
/// the pack-derived numbers against their provenance, the runtime hooks, and no allocation per frame. Engine-free; Unity's
/// rendering, the Windows build and play are not exercised (the offline renders show the pixels).
/// </summary>
internal static class BowChecks
{
    private const float D=(float)Math.PI/180;
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Bow: "+what);count++;}
    private static Actor Pilot(int id=41,float yaw=0)=>new Actor{Id=id,SourceSpec=NativeBowWire.Spec,Weapon=WeaponId.Sword,Grounded=true,Hp=100,CastRate=1,Yaw=yaw,AimYaw=yaw,MeleeKit=new NativeMeleeKit(),LastShot=-100,RollDirection=new V3(0,0,1)};
    private static float Angle(Vector3 a,Vector3 b)=>(float)Math.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(a),Vector3.Normalize(b)),-1,1))/D;
    // The arms' reach to their targets: the bow arm always, the string arm while it holds the string (its hold weight).
    private static float Reach(in NativeBowPose.Frame f)=>Math.Max(f.BowReach,f.DrawWeight>.5f?f.DrawReach:0);
    private static float LineDistance(Vector3 p,Vector3 o,Vector3 dir){dir=Vector3.Normalize(dir);var v=p-o;return (v-dir*Vector3.Dot(v,dir)).Length();}
    // The Prototype Series is pinned: its meshes may only change on purpose (update these with the reason in the commit).
    private const ulong ProtoBow=0x6921910c38750e96UL,ProtoArrow=0xa6ea543d1452d3c0UL,ProtoQuiver=0x47160f2966502e94UL;
    public static void Run(string root)
    {
        var log=new List<string>();count=0;
        Models(log);Rig(log);Keying(log);Poses(log);View(log);Stuck(log);Effects(log);Hud(log);Entrance(root,log);Pack(root,log);Wiring(root,log);Allocation(log);
        foreach(var l in log)Console.WriteLine("PASS bow: "+l);
        Console.WriteLine("PASS bow: "+count+" checks");
    }

    // ---- 1. models ----
    private static void Models(List<string> log)
    {
        var tris=new int[2,3];
        foreach(bool std in new[]{true,false})for(int t=0;t<(std?3:1);t++)
        {
            var a=NativeBowModels.Part(std,NativeBowModels.Held,t);var b=NativeBowModels.Part(std,NativeBowModels.Held,t);tris[std?0:1,t]=a.Triangles;
            string what=(std?"Standard":"Prototype")+" tier "+t;
            Check(NativeBowModels.Hash(a)==NativeBowModels.Hash(b),what+" builds deterministically");
            Check(a.Triangles>0&&a.Triangles<=NativeWeaponModels.Budget[t],what+" within the weapon budget ("+a.Triangles+" / "+NativeWeaponModels.Budget[t]+")");
            int parts=0;for(int p=0;p<3;p++)parts+=NativeBowModels.Part(std,NativeBowModels.PartMask(p),t).Triangles;parts+=NativeBowModels.Part(std,NativeBowModels.StringPart,t).Triangles;
            Check(parts==a.Triangles,what+": the riser, the two limbs and the rest string make the whole bow ("+parts+" / "+a.Triangles+")");
            foreach(var m in new[]{a,NativeBowModels.ArrowMesh(std,t),NativeBowModels.QuiverMesh(std,t)})foreach(var s in m.Sections)
            {
                Check(s.Vertices<65535&&s.Indices.All(i=>i>=0&&i<s.Vertices)&&s.Colors.Count==s.Vertices&&s.Emission.Count==s.Vertices&&s.Normals.Count==s.Vertices,what+" section arrays");
                Check(s.Positions.All(p=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z))&&s.Normals.All(n=>Math.Abs(n.Length()-1)<1e-3f),what+" finite positions and unit normals");
                Check(s.Emission.All(e=>e.W==0||e.W==2),what+" emission tags untinted (0) or accent (2)");
                Check(s.Stretch==WeaponStretch.None,what+" never stretches");
            }
            Check(a.Sections.All(s=>s.Bone==NativeWeaponModels.LeftHand),what+" rides the bow hand");
        }
        Check(tris[0,1]<=tris[0,0]*.85f&&tris[0,2]<=tris[0,0]*.45f&&tris[0,2]<tris[0,1],"Standard tiers thin out ("+tris[0,0]+"/"+tris[0,1]+"/"+tris[0,2]+")");
        Check(tris[0,0]>=3*tris[1,0],"the Standard is far more detailed than the Prototype ("+tris[0,0]+" vs "+tris[1,0]+")");
        int[] arrow={NativeBowModels.ArrowMesh(true,0).Triangles,NativeBowModels.ArrowMesh(true,1).Triangles,NativeBowModels.ArrowMesh(true,2).Triangles},quiver={NativeBowModels.QuiverMesh(true,0).Triangles,NativeBowModels.QuiverMesh(true,1).Triangles,NativeBowModels.QuiverMesh(true,2).Triangles};
        Check(arrow[0]<=240&&arrow[1]<arrow[0]&&arrow[2]<arrow[1],"the arrow (drawn many times: stuck arrows, the volley) stays light and thins out ("+string.Join("/",arrow)+")");
        Check(quiver[0]<=1000&&quiver[1]<quiver[0]&&quiver[2]<quiver[1],"the quiver within 1000 triangles and thinning ("+string.Join("/",quiver)+")");
        ulong pb=NativeBowModels.Hash(NativeBowModels.Part(false,NativeBowModels.Held,0)),pa=NativeBowModels.Hash(NativeBowModels.ArrowMesh(false,0)),pq=NativeBowModels.Hash(NativeBowModels.QuiverMesh(false,0));
        Check(pb==ProtoBow&&pa==ProtoArrow&&pq==ProtoQuiver,"the Prototype Series is pinned (bow 0x"+pb.ToString("x16")+", arrow 0x"+pa.ToString("x16")+", quiver 0x"+pq.ToString("x16")+")");
        // The ids drawn (the Prototype Series is drawn at its one tier: BowHeld and the bank use tier 0 for it).
        var ids=new HashSet<string>();foreach(bool std in new[]{true,false})for(int t=0;t<(std?3:1);t++){for(int p=0;p<3;p++)ids.Add(NativeBowModels.PartId(std,p,t));ids.Add(NativeBowModels.ArrowId(std,t));ids.Add(NativeBowModels.QuiverId(std,t));}
        Check(ids.Count==9+3+3+3+1+1&&NativeBowModels.ArrowId(false,2)==NativeBowModels.ArrowId(false,0)&&NativeBowModels.QuiverId(false,2)==NativeBowModels.QuiverId(false,0),"distinct model ids per skin and tier (the Prototype has one tier)");
        log.Add($"models: Standard bow {tris[0,0]}/{tris[0,1]}/{tris[0,2]} triangles (Full/Reduced/Low), Prototype {tris[1,0]}; arrow {string.Join("/",arrow)}, quiver {string.Join("/",quiver)}, Prototype arrow {NativeBowModels.ArrowMesh(false,0).Triangles} and quiver {NativeBowModels.QuiverMesh(false,0).Triangles}; deterministic, in budget, the Prototype pinned");
    }

    // ---- 2. the bow frame: sockets, the string and the arrow following the pull ----
    private static void Rig(List<string> log)
    {
        Check(NativeBowModels.Rest.X==NativeBow.ArrowX&&NativeBowModels.Rest.Y==NativeBow.ArrowY&&NativeBowModels.Rest.Z>NativeBow.RiserZ,"the arrow rest on the arrow line, ahead of the riser's axis");
        Check(NativeBowModels.ModuleOffset==new Vector3(NativeBow.ArrowX,NativeBow.ArrowY,0)&&NativeWeaponDressing.MuzzleZ(NativeBowWire.Spec)==NativeBowModels.ModuleTip,"the chip modules laid out about the arrow line, their muzzle ahead of the riser");
        var grip=NativeBowModels.Part(true,NativeBowModels.Riser,0).Sections.SelectMany(s=>s.Positions).ToArray();
        Check(grip.Min(p=>Vector3.Distance(p,NativeBow.Grip))<.05f,"riser geometry round the grip in the fist");
        float lastZ=1,lastFlex=-1;int samples=0;
        for(float pull=0;pull<=1.0001f;pull+=.05f,samples++)
        {
            var nock=NativeBow.NockAt(pull);Check(nock.X==NativeBow.ArrowX&&nock.Y==NativeBow.ArrowY&&nock.Z<lastZ,"the nock runs back along the arrow line ("+pull.ToString("0.00")+")");lastZ=nock.Z;
            Check(NativeBow.Flex(pull)>lastFlex,"the limbs flex further with the pull");lastFlex=NativeBow.Flex(pull);
            foreach(bool top in new[]{true,false})
            {
                var s=NativeBowRig.String(top,pull,0,0);Check(Vector3.Distance(s.Apply(Vector3.Zero),NativeBow.Anchor(top,pull))<1e-5f&&Vector3.Distance(s.Apply(Vector3.UnitZ),nock)<1e-4f,"string "+(top?"upper":"lower")+" half from the limb tip to the nock at "+pull.ToString("0.00"));
                var limb=NativeBowRig.Limb(top,pull);var tip=new Vector3(NativeBow.ArrowX,top?NativeBow.TipY:-NativeBow.TipY,NativeBow.TipZ);
                Check(Vector3.Distance(limb.Apply(tip),NativeBow.Anchor(top,pull))<1e-5f,"the drawn limb's tip carries the string's anchor");
                var pivot=NativeBowModels.Pivot(top);Check(Vector3.Distance(limb.Apply(pivot),pivot)<1e-5f,"the limb turns about its pocket");
            }
            var arrow=NativeBowRig.Arrow(pull);Check(Vector3.Distance(arrow.Apply(Vector3.Zero),nock)<1e-6f&&Angle(arrow.Apply(Vector3.UnitZ)-arrow.Position,Vector3.UnitZ)<1e-3f,"the nocked arrow sits on the nock along the arrow line");
            var head=nock+Vector3.UnitZ*NativeBow.ArrowLength;Check(head.Z>NativeBowModels.Rest.Z,"the arrowhead stays ahead of the rest at full draw");
        }
        Check(Math.Abs(NativeBow.NockAt(0).Z-NativeBow.BraceZ)<1e-6f&&Math.Abs(NativeBow.NockAt(1).Z-NativeBow.FullZ)<1e-6f,"brace and full-draw depths");
        Check(NativeBow.PullOf(0)==0&&Math.Abs(NativeBow.PullOf(1)-1)<1e-6f&&NativeBow.PullOf(.5f)>.5f,"the pull eases out of the draw fraction (the meter stays linear)");
        // The Volley's fan: five arrows spread 14 degrees in all about the canted bow's world up, all on the nock.
        float spread=Angle(NativeBowRig.Arrow(1,0,5,NativeBow.VolleyFan,0).Apply(Vector3.UnitZ)-NativeBow.NockAt(1),NativeBowRig.Arrow(1,4,5,NativeBow.VolleyFan,0).Apply(Vector3.UnitZ)-NativeBow.NockAt(1));
        Check(Math.Abs(spread-NativeBow.VolleyFan)<.05f,"the Volley fan spans "+spread.ToString("0.0")+" degrees");
        log.Add($"bow frame: the string from both limb tips to the nock, the limbs turning about their pockets and the arrow on the nock at {samples} pulls from brace ({NativeBow.BraceZ}) to full draw ({NativeBow.FullZ}); the Volley fan {spread:0.0} degrees");
    }

    // ---- 3. keyed on the replicated data only ----
    private static void Keying(List<string> log)
    {
        var a=Pilot(41);var b=Pilot(42);a.Bow.DrawAt=b.Bow.DrawAt=3;a.LastShot=b.LastShot=2;a.Bow.ReleaseDraw=b.Bow.ReleaseDraw=.6f;
        b.Hp=17;b.Position=new V3(5,2,-3);b.Velocity=new V3(4,0,2);b.Ammo=3;b.Yaw=80;b.Pitch=-20;b.StunUntil=9;b.AccentColor="#123456";
        var ra=NativeBowWire.Of(a);var rb=NativeBowWire.Of(b);rb.Seed=ra.Seed;
        Check(ra.Equals(rb),"the read depends only on the bow's replicated fields (and the id's seed)");
        var none=NativeBowWire.Of(new Actor{SourceSpec="rifle",LastShot=5});Check(!none.Bow&&NativeBow.At(none,6).Phase==NativeBow.Phase.Carry,"another weapon reads as an idle bow");
        var bad=Pilot();bad.Bow.DrawAt=float.NaN;bad.LastShot=float.PositiveInfinity;bad.Bow.ReleaseDraw=7;bad.Bow.RainRadius=-3;bad.CastRate=0;var rbad=NativeBowWire.Of(bad);
        Check(rbad.DrawAt<0&&rbad.ReleaseAt<0&&rbad.ReleaseDraw==1&&rbad.RainRadius==NativeBow.RainRadius&&Math.Abs(rbad.DrawTime-NativeBow.FullDraw)<1e-6f,"malformed fields read as absent");
        // The existing cast stamp drives Volley and Rain when the bow's own fields are not set.
        var c=Pilot();c.AbilityId=NativeBowWire.VolleyId;c.AbilityAt=4;c.AbilityRelease=4.06f;var rc=NativeBowWire.Of(c);Check(rc.VolleyAt==4&&rc.VolleyRelease==4.06f,"Volley from AbilityId/AbilityAt/AbilityRelease");
        c.AbilityId=NativeBowWire.RainId;rc=NativeBowWire.Of(c);Check(rc.RainAt==4&&rc.VolleyAt<0,"Rain of Arrows from the cast stamp");
        // A frame is a pure function of the read and the time: any order of evaluation gives the same frames.
        var r=ra;var times=Enumerable.Range(0,240).Select(i=>i/60f+1.5f).ToArray();var forward=times.Select(t=>NativeBow.At(r,t)).ToArray();var rng=new Random(5);
        foreach(int i in Enumerable.Range(0,times.Length).OrderBy(_=>rng.Next())){var v=NativeBow.At(r,times[i]);Check(v.Equals(forward[i]),"frame "+times[i]+" depends on the evaluation order");}
        // Shot events carry the arrow's kind and draw fraction.
        foreach(var (detail,kind,amount) in new[]{(NativeBowWire.DetailQuick,NativeBow.Arrow.Quick,.2f),(NativeBowWire.DetailFull,NativeBow.Arrow.Full,1f),(NativeBowWire.DetailVolley,NativeBow.Arrow.Volley,.7f),(NativeBowWire.DetailRain,NativeBow.Arrow.Rain,1f)})
        {var e=new ArenaEvent{Kind=EventKind.Shot,Detail=detail,Amount=amount};Check(NativeBowWire.Shot(e,out var k,out float d)&&k==kind&&Math.Abs(d-amount)<1e-6f,detail+" shot event");}
        Check(!NativeBowWire.Shot(new ArenaEvent{Kind=EventKind.Shot,Detail="pistol"},out _,out _)&&!NativeBowWire.Shot(new ArenaEvent{Kind=EventKind.Hit,Detail="bow"},out _,out _),"other events are not bow shots");
        Check(NativeBowWire.Sticks(new ArenaEvent{Kind=EventKind.Headshot},"bow")&&!NativeBowWire.Sticks(new ArenaEvent{Kind=EventKind.Hit,Detail="field"},"bow")&&!NativeBowWire.Sticks(new ArenaEvent{Kind=EventKind.Hit},"rifle"),"only a bow pilot's direct hits stick an arrow");
        Check(Math.Abs(NativeBowWire.SpeedDraw(NativeBow.SpeedQuick))<1e-6f&&Math.Abs(NativeBowWire.SpeedDraw(NativeBow.SpeedFull)-1)<1e-6f&&NativeBowWire.SpeedDraw(float.NaN)==0,"an unseen shot's draw from its launch speed");
        log.Add("keying: the bow read depends only on the replicated bow fields (malformed ones read as absent, the cast stamp as fallback); 240 frames are identical in any evaluation order; shot events carry the kind and draw; only a bow pilot's direct hits stick");
    }

    // ---- 4. third-person poses: reach, the string hand on the nock, the aim on the crosshair ----
    private sealed class Driver
    {
        public readonly NativeBowMotion Motion=new NativeBowMotion{JumpLayer=new NativeJumpMotion(),SprintLayer=new NativeSprintMotion(),Locomotion=new NativeRangedLocomotion()};
        public readonly CapturedRig.Pose Pose=new CapturedRig.Pose();private readonly NativeJuggleTrack juggle=new NativeJuggleTrack();
        public string Step(Actor a,float t){Motion.Rig.GroupPosition=new Vector3(0,.865f,0);Motion.Rig.GroupYaw=-a.Yaw*D;string s=Motion.Pose(a,t,Pose,false,juggle,out _);Motion.Finish(Pose,t);return s;}
    }
    private static void Poses(List<string> log)
    {
        float worstAim=0,worstReach=0,worstHook=0,worstAnchor=0;int frames=0;var states=new HashSet<string>();string worstAt="",handAt="";float worstHand=0;
        foreach(float pitch in new[]{-30f,-10f,0f,15f,35f})foreach(float yaw in new[]{0f,-12f,12f})
        {
            var a=Pilot(43);a.Pitch=pitch;a.AimYaw=a.Yaw+yaw;a.Bow.DrawAt=1;var d=new Driver();
            for(float t=0;t<=2.2f;t+=1/60f)
            {
                states.Add(d.Step(a,t));var f=d.Motion.Shot;var v=d.Motion.Bow;frames++;
                Check(v.Equals(NativeBow.At(NativeBowWire.Of(a),t,d.Motion.Watch.CancelAt,d.Motion.Watch.CancelPull)),"the posed bow is the replicated frame");
                if(Reach(f)>worstReach){worstReach=Reach(f);worstAt=$"pitch {pitch} yaw {yaw} t {t:0.00} {v.Phase} raise {v.Raised:0.00} bow {f.BowReach:0.000} draw {f.DrawReach:0.000}";}
                if(v.Raised>.999f&&t>1.1f)
                {
                    var aim=NativeBowPose.PhaseAim(a,v,false);Check(Angle(aim,NativeBowPose.AimOf(yaw,pitch))<1e-3f,"the pose aims along the replicated look");
                    // The bow as drawn: the posed bow hand after the IK and the ground pass (the frame's bow is what the IK aimed for).
                    float err=Math.Max(Angle(Vector3.Transform(Vector3.UnitZ,f.BowRotation),aim),Angle(Vector3.Transform(Vector3.UnitZ,d.Pose.WorldRotation[CapturedRig.LHand]),aim));worstAim=Math.Max(worstAim,err);
                    // The ground pass may settle the body; the two hands keep their spacing, so the string stays on the nock as drawn.
                    float gap=Vector3.Distance(d.Pose.WorldPosition[CapturedRig.RHand]-d.Pose.WorldPosition[CapturedRig.LHand],f.DrawWrist-f.BowWrist);
                    if(gap>worstHand){worstHand=gap;handAt=$"pitch {pitch} yaw {yaw} t {t:0.00} {v.Phase} L {Vector3.Distance(d.Pose.WorldPosition[CapturedRig.LHand],f.BowWrist):0.000} R {Vector3.Distance(d.Pose.WorldPosition[CapturedRig.RHand],f.DrawWrist):0.000}";}
                    var hook=f.DrawWrist+Vector3.Transform(NativeBowModels.Hook,f.DrawRotation);worstHook=Math.Max(worstHook,Vector3.Distance(hook,f.Nock));
                    var nock=f.BowWrist+Vector3.Transform(NativeBow.NockAt(f.Pull),f.BowRotation);Check(Vector3.Distance(nock,f.Nock)<1e-4f,"the frame's nock is the bow's");
                    // From mid-draw on the bow arm is out on the line (early on, aiming low, the bow arm folds no tighter than it can).
                    if(v.Pull>.6f)worstAnchor=Math.Max(worstAnchor,LineDistance(f.Anchor,f.Nock,aim));
                    if(v.Full)Check(Vector3.Distance(f.Nock,f.Anchor)<.02f,"at full draw the string hand reaches the cheek anchor ("+Vector3.Distance(f.Nock,f.Anchor).ToString("0.000")+" m)");
                }
            }
        }
        Check(worstAim<1,"the arrow converges on the crosshair aim within 1 degree (worst "+worstAim.ToString("0.00")+")");
        Check(worstReach<=.522f,"both arms reach their targets (worst "+worstReach.ToString("0.000")+" m of .515: "+worstAt+")");
        Check(worstHand<.02f,"the posed hands keep the frame's bow-to-string spacing (worst "+worstHand.ToString("0.000")+" m: "+handAt+")");
        Check(worstHook<.012f,"the string hand holds the nock (worst "+worstHook.ToString("0.000")+" m)");
        Check(worstAnchor<.01f,"from mid-draw the arrow line passes the cheek anchor (worst "+worstAnchor.ToString("0.000")+" m)");
        Check(states.Contains(NativeBowMotion.Draw)&&states.Contains(NativeBowMotion.Hold)&&states.Contains(NativeBowMotion.Carry),"carry, draw and hold states");
        // The phases: tap, full release, the nock from the quiver, a let-down, the fatigue tremor, Volley and the sky shot.
        var p=Pilot(44);var dr=new Driver();float tremor=0,follow=0,fan=0,sky=0;bool handArrow=false,letDown=false;
        // Continuity: the string hand and the bow hand never jump between 60 Hz frames (the nock, the follow-through, the casts).
        // A jump is a one-frame spike over both neighbouring frames' travel (fast casts move far, but smoothly).
        float jump=0;string jumpAt="";Vector3 lastDraw=default,lastBow=default;bool held=false;float d1=-1,d2=-1;string at1="";
        void Watch(float t)
        {
            var f=dr.Motion.Shot;bool h=f.DrawWeight>.5f;float d=-1;if(held&&h)d=Math.Max(Vector3.Distance(f.DrawWrist,lastDraw),Vector3.Distance(f.BowWrist,lastBow));
            if(d1>=0&&d2>=0&&d>=0){float spike=d1-Math.Max(d2,d);if(spike>jump){jump=spike;jumpAt=at1;}}
            d2=d1;d1=d;at1=$"{t-1/60f:0.000} {dr.Motion.Bow.Phase} nocking {dr.Motion.Bow.Nocking:0.00}";held=h;lastDraw=f.DrawWrist;lastBow=f.BowWrist;
        }
        p.Bow.DrawAt=.5f;for(float t=0;t<5.5f;t+=1/60f){dr.Step(p,t);Watch(t);tremor=Math.Max(tremor,dr.Motion.Bow.Tremor);}
        p.LastShot=5.5f;p.Bow.ReleaseDraw=1;for(float t=5.5f;t<6.1f;t+=1/60f){dr.Step(p,t);Watch(t);follow=Math.Max(follow,dr.Motion.Bow.Follow);handArrow|=dr.Motion.Shot.HandArrow;}
        p.Bow.DrawAt=6.5f;for(float t=6.1f;t<6.9f;t+=1/60f)dr.Step(p,t);p.Bow.DrawAt=0;for(float t=6.9f;t<7.5f;t+=1/60f){dr.Step(p,t);letDown|=dr.Motion.Bow.Phase==NativeBow.Phase.LetDown;}
        p.Bow.VolleyAt=7.7f;p.Bow.VolleyRelease=7.76f;for(float t=7.5f;t<8.4f;t+=1/60f){dr.Step(p,t);Watch(t);fan=Math.Max(fan,dr.Motion.Bow.Fan);}
        p.Bow.RainAt=9f;p.Bow.RainRelease=9.16f;for(float t=8.4f;t<10f;t+=1/60f){dr.Step(p,t);Watch(t);sky=Math.Max(sky,dr.Motion.Bow.Sky);Check(Reach(dr.Motion.Shot)<=.522f,"the sky shot reaches ("+Reach(dr.Motion.Shot).ToString("0.000")+" at "+t.ToString("0.000")+" "+dr.Motion.Bow.Phase+" nocking "+dr.Motion.Bow.Nocking.ToString("0.00")+" bow "+dr.Motion.Shot.BowReach.ToString("0.000")+")");}
        Check(jump<.08f,"the hands never jump between frames (worst spike "+jump.ToString("0.000")+" m at "+jumpAt+")");
        Check(tremor>.5f&&follow>.9f&&handArrow&&letDown&&fan>.99f&&sky>.99f,"hold tremor "+tremor.ToString("0.00")+", follow-through "+follow.ToString("0.00")+", an arrow drawn from the quiver, the let-down watched, the Volley fan and the sky shot");
        log.Add($"third person: {frames} frames over 15 aims (pitch -30..35, yaw +-12): the posed arrow on the crosshair aim within {worstAim:0.00} degrees (the posed hands within {worstHand*1000:0.0} mm of the frame's spacing), the line through the cheek anchor within {worstAnchor*1000:0.0} mm, the string hand on the nock within {worstHook*1000:0.0} mm, the arms reaching (worst {worstReach:0.000} m); tremor, follow-through, nock, let-down, Volley and sky shot, no hand jump (worst one-frame spike {jump*1000:0} mm over its neighbours)");
    }

    // ---- 5. first person: the arrow straight down the view at every phase ----
    private static void View(List<string> log)
    {
        float worst=0,reach=0;var pose=new CapturedRig.Pose();var a=Pilot(45);a.Bow.DrawAt=1;
        foreach(float pitch in new[]{-40f,0f,40f})
        {
            a.Pitch=pitch;
            for(float t=1;t<2.5f;t+=1/30f)
            {
                pose.Reset();var f=NativeBowPose.View(pose,a,t);reach=Math.Max(reach,Reach(f));
                if(t>1.25f){float err=Angle(Vector3.Transform(Vector3.UnitZ,f.BowRotation),Vector3.UnitZ);worst=Math.Max(worst,err);}
            }
        }
        Check(worst<1,"first person: the drawn arrow points down the view at the crosshair within 1 degree (worst "+worst.ToString("0.00")+")");
        Check(reach<=.522f,"first person: the arms reach (worst "+reach.ToString("0.000")+")");
        Check(NativeBowPose.ViewEye.Y>NativeBowPose.ViewAnchor.Y&&NativeBowPose.ViewEye.X<NativeBowPose.ViewAnchor.X,"first person: the nock sits under and right of the eye, so the arrow comes in from the lower right");
        log.Add($"first person: the drawn arrow down the view within {worst:0.00} degrees at every pitch, the arms reaching (worst {reach:0.000} m)");
    }

    // ---- 6. stuck arrows: walls 6 s, pilots until death or the round's end ----
    private sealed class ArrowSink:IBowFxSink
    {
        public int Glows,Streaks,Smokes,Sprites,Lines,Bands,Arrows;public double Hash;public Vector3 LastNock,LastDir;public float LastOpacity;
        private void Mix(Vector3 v,float k){Hash=Hash*1.000001+v.X*.37+v.Y*.61+v.Z*.89+k;if(!(float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z)))throw new Exception("Bow: non-finite effect");}
        public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){Glows++;Mix(at,size);}
        public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){Streaks++;Mix(head,width);}
        public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){Smokes++;Mix(at,size);}
        public void Sprite(bool view,Vector3 at,Vector3 color,float size,int profile){Sprites++;Mix(at,size);}
        public void Line(bool view,Vector3 tail,Vector3 head,Vector3 color,float width,bool shard){Lines++;Mix(head,width);}
        public void Band(bool view,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float ua,float ub,Vector3 color){Bands++;Mix(a,ua);}
        public void Arrow(Vector3 nock,Vector3 dir,float opacity,bool view,bool standard){Arrows++;LastNock=nock;LastDir=dir;LastOpacity=opacity;Mix(nock,opacity);}
        public int Total=>Glows+Streaks+Smokes+Sprites+Lines+Bands+Arrows;
        public void Reset(){Glows=Streaks=Smokes=Sprites=Lines=Bands=Arrows=0;Hash=0;}
    }
    private static bool Bone(int target,int bone,out Matrix4x4 m){m=Matrix4x4.CreateTranslation(target,bone*.1f,0);return target!=99;}
    private static void Stuck(List<string> log)
    {
        var s=new NativeBowStuck();var sink=new ArrowSink();
        s.AddWall(1,new Vector3(1,2,3),new Vector3(0,0,2),10,1,true);s.AddWall(1,new Vector3(9,9,9),Vector3.UnitX,10,1,true);Check(s.Count==1,"one stuck arrow per hit event");
        s.Update(15.9f);Check(s.Count==1&&Math.Abs(NativeBowStuck.Opacity(s[0],15.9f)-.25f)<1e-4f,"a wall arrow fades over its last .4 s");
        s.Update(16.01f);Check(s.Count==0,"a wall arrow is gone after "+NativeBowStuck.WallLife+" s");
        for(int i=0;i<15;i++)s.AddPilot(100+i,7,CapturedRig.Chest,new Vector3(.1f,.2f,0),Vector3.UnitZ,20+i*.01f,1,true);
        Check(s.Pilot(7)==NativeBowStuck.PerPilot,"at most "+NativeBowStuck.PerPilot+" arrows in one pilot (the oldest give way)");
        s.Update(500);Check(s.Pilot(7)==NativeBowStuck.PerPilot,"a pilot's arrows stay until death or the round's end");
        sink.Reset();s.Draw(sink,500,Bone);var m=Matrix4x4.CreateTranslation(7,CapturedRig.Chest*.1f,0);var hit=Vector3.Transform(new Vector3(.1f,.2f,0),m);
        Check(sink.Arrows==NativeBowStuck.PerPilot&&Vector3.Distance(sink.LastNock,hit+Vector3.UnitZ*(NativeBowStuck.Bury-NativeBow.ArrowLength))<1e-4f&&sink.LastOpacity==1,"a pilot's arrow rides its joint (the corpse's or the rig's), buried along the flight");
        s.AddPilot(300,99,CapturedRig.Head,Vector3.Zero,Vector3.UnitZ,30,1,true);sink.Reset();s.Draw(sink,30,Bone);Check(sink.Arrows==NativeBowStuck.PerPilot,"an arrow whose carrier is gone is hidden, not drawn at the origin");
        s.ClearPilot(7);Check(s.Pilot(7)==0&&s.Count==1,"a respawn clears that pilot's arrows only");
        s.Update(29.6f);Check(s.Count==1,"a small rewind keeps the arrows");s.Update(29.4f);Check(s.Count==0,"a rewind past an arrow drops it");
        for(int i=0;i<400;i++)s.AddWall(1000+i,Vector3.Zero,Vector3.UnitZ,40,1,true);Check(s.Count==NativeBowStuck.Capacity,"the pool stays at its capacity");
        s.Clear();for(int i=0;i<400;i++)s.AddWall(2000+i,Vector3.Zero,Vector3.UnitZ,40,1,true,low:true);Check(s.Count==NativeBowStuck.LowCapacity,"the low graphics tier keeps "+NativeBowStuck.LowCapacity);
        s.Clear();Check(s.Count==0,"the round's end clears every arrow");
        log.Add($"stuck arrows: walls {NativeBowStuck.WallLife} s with a {NativeBowStuck.FadeOut} s fade, pilots until death or the round's end riding their joint (at most {NativeBowStuck.PerPilot} each), cleared on respawn and rewind; the pool {NativeBowStuck.Capacity} ({NativeBowStuck.LowCapacity} on low)");
    }

    // ---- 7. effects ----
    private sealed class Scene:IBowFxScene
    {
        public readonly List<Actor> List=new List<Actor>();public readonly List<NativeBowArrowView> Flying=new List<NativeBowArrowView>();
        public IReadOnlyList<Actor> Actors=>List;public int Viewer=>0;public int Arrows=>Flying.Count;public NativeBowArrowView ArrowAt(int i)=>Flying[i];
        public bool Bow(Actor a,bool view,out GunFrame frame){frame=new GunFrame{O=new Vector3(0,1.4f,0),X=Vector3.UnitX,Y=Vector3.UnitY,Z=Vector3.UnitZ,Scale=1};return true;}
        public bool Standard(Actor a)=>true;public bool RainShots(Actor a,float rainAt)=>false;
    }
    private static int bands;
    private static int Draw(ArrowSink sink,Scene scene,float t,float q=1,bool low=false){sink.Reset();NativeBowFx.Draw(sink,scene,t,q,low);bands+=sink.Bands;return sink.Total;}
    private static void Effects(List<string> log)
    {
        var scene=new Scene();var a=Pilot(46);scene.List.Add(a);var sink=new ArrowSink();
        Check(Draw(sink,scene,1)==0,"an idle bow draws no effects");
        a.Bow.DrawAt=1;Draw(sink,scene,1.85f);int glint=sink.Sprites;Check(glint>0,"the full-draw glint on the arrowhead");
        a.LastShot=2;a.Bow.ReleaseDraw=1;a.Bow.DrawAt=0;Draw(sink,scene,2.02f);Check(sink.Lines>0,"the string snaps at the release");
        scene.Flying.Add(new NativeBowArrowView{Id=1,Owner=46,Position=new Vector3(0,1.5f,4),Velocity=new Vector3(0,0,110),Born=2,Draw=1,Kind=NativeBow.Arrow.Full,Standard=true});
        scene.Flying.Add(new NativeBowArrowView{Id=2,Owner=46,Position=new Vector3(0,1.5f,4),Velocity=new Vector3(0,-2,45),Born=2,Draw=.1f,Kind=NativeBow.Arrow.Quick,Standard=true});
        Draw(sink,scene,2.05f);Check(sink.Arrows>=2,"every arrow in flight draws its model");
        NativeBowFx.TrailLook(1,NativeBow.Arrow.Full,out float fl,out float fw,out float fg,out _);NativeBowFx.TrailLook(.1f,NativeBow.Arrow.Quick,out float ql,out float qw,out float qg,out _);
        Check(fl>ql&&fg>qg&&fw>=qw,"a fuller draw's trail is longer and brighter");
        scene.Flying.Clear();a.Bow.VolleyAt=3;a.Bow.VolleyRelease=3.06f;Draw(sink,scene,3.1f);Check(sink.Lines>=NativeBow.VolleyArrows,"the Volley's five-arrow fan streak");
        a.Bow.RainAt=5;a.Bow.RainRelease=5.16f;a.Bow.HasRainTarget=true;a.Bow.RainTarget=new V3(6,0,10);
        int marker=Draw(sink,scene,5.3f),markerLines=sink.Lines+sink.Bands,fall=Draw(sink,scene,5.9f),falling=sink.Arrows,hits=Draw(sink,scene,6.9f);
        Check(marker>0&&markerLines>0&&fall>0&&falling>0&&hits>0&&sink.Arrows>0,"Rain of Arrows: the marker, the sky volley, the arrows falling and standing ("+marker+"/"+fall+"/"+hits+")");
        double h1=sink.Hash;Draw(sink,scene,6.9f);Check(sink.Hash==h1,"the effects are a pure function of the frame");
        int full=Draw(sink,scene,5.9f,1),reduced=Draw(sink,scene,5.9f,.3f),low=Draw(sink,scene,5.9f,1,true);
        Check(reduced<full&&low<full,"reduced effects and the low tier thin the rain ("+full+" / "+reduced+" / "+low+")");
        // The owner's rule: no glowing ring or disc lies on the floor (the marker is upright posts and chevrons; the slow is motes).
        for(float t=4.9f;t<9;t+=.05f){Draw(sink,scene,t);Draw(sink,scene,t,.3f,true);}
        var tint=new ArrowSink();NativeBowFx.SlowTint(tint,new Vector3(0,0,0),1,3,9,1,false);bands+=tint.Bands;
        Check(bands==0&&tint.Glows>0,"no flat band (ring or disc) on the floor in any bow effect");
        Check(NativeBowFx.SlowWeight(10,10.5f)>0&&NativeBowFx.SlowWeight(10,10+NativeBow.RainSlowSeconds+NativeBowFx.SlowFade+.01f)==0,"the slow tint lasts the slow");
        log.Add($"effects: the full-draw glint, the string snap, arrows in flight with draw-scaled trails, the Volley fan, Rain's marker/fall/ground ({marker}/{fall}/{hits} particles); reduced effects {reduced} and low {low} of {full}; deterministic");
    }

    // ---- 8. HUD and sounds ----
    private static void Hud(List<string> log)
    {
        var a=Pilot(47);a.Bow.DrawAt=1;var r=NativeBowWire.Of(a);
        var m=NativeBowHud.MeterOf(r,1.1f);Check(m.Shown&&m.Quick&&!m.Full&&Math.Abs(m.Fill-.125f)<1e-4f&&Math.Abs(m.QuickMark-NativeBow.QuickBelow/NativeBow.FullDraw)<1e-5f,"the meter fills with the draw fraction, the quick-shot mark at .25 s");
        m=NativeBowHud.MeterOf(r,1.4f);Check(m.Shown&&!m.Quick&&!m.Full,"past the quick mark");m=NativeBowHud.MeterOf(r,1.85f);Check(m.Full&&m.Pulse>0&&!m.Fatigue,"full draw locks with a flare");
        m=NativeBowHud.MeterOf(r,1.8f+NativeBow.SteadyHold+.5f);Check(m.Fatigue&&m.Tremor>0,"the meter warns as the arm tires");
        a.LastShot=5;a.Bow.DrawAt=0;a.Bow.ReleaseDraw=.6f;r=NativeBowWire.Of(a);m=NativeBowHud.MeterOf(r,5.1f);Check(m.Shown&&m.Release&&Math.Abs(m.Fill-.6f)<1e-6f&&m.Alpha<1,"after a release the meter holds the shot's fill and fades");
        Check(!NativeBowHud.MeterOf(r,5.5f).Shown&&!NativeBowHud.MeterOf(NativeBowWire.Of(new Actor{SourceSpec="pistol"}),1).Shown,"hidden otherwise and for other weapons");
        var c=NativeBowHud.Of(NativeBowWire.VolleyId,r,6,0,8);Check(c.Ready&&c.State=="READY"&&c.Icon==NativeBowHud.VolleyIcon,"the Q card ready with its icon");
        c=NativeBowHud.Of(NativeBowWire.RainId,r,6,4,12);Check(!c.Ready&&c.State=="4.0s"&&Math.Abs(c.Dial-(1-4/12f))<1e-5f&&c.Icon==NativeBowHud.RainIcon,"the E card's cooldown dial");
        a.Bow.RainAt=7;r=NativeBowWire.Of(a);Check(NativeBowHud.Of(NativeBowWire.RainId,r,7.2f,12,12).State.StartsWith("MARKED")&&NativeBowHud.Of(NativeBowWire.RainId,r,7.9f,12,12).State=="RAINING","Rain's card marks then rains");
        Check(NativeLocker.AbilityIcon(NativeBowWire.VolleyId,null)==NativeBowHud.VolleyIcon&&NativeLocker.AbilityIcon(NativeBowWire.RainId,null)==NativeBowHud.RainIcon,"the Locker shows the same ability icons");
        Check(NativeLoadoutText.Role(new ContentWeapon{id="bow"}).Length>0,"the loadout tile's role line");
        foreach(var s in new[]{NativeBowSounds.Creak,NativeBowSounds.Thwip,NativeBowSounds.Thunk})Check(NativeSoundKeys.Known(s.Key),"the "+s.Key+" sound is an existing bank key");
        Check(NativeBowSounds.ThwipRate(1)<NativeBowSounds.ThwipRate(0),"a full draw's thwip is heavier");
        log.Add("HUD and sounds: the draw meter (fill, quick mark, full-draw lock, fatigue, the released fill fading), the Q and E cards and Locker icons, the loadout role, the creak/thwip/thunk on existing bank keys");
    }

    // ---- 9. the Arrowfall entrance ----
    private static float AgeOf(NativeEntranceScript sc,float t){float lo=0,hi=5;for(int i=0;i<40;i++){float mid=(lo+hi)*.5f;if(sc.Clock(mid)<t)lo=mid;else hi=mid;}return hi;}
    private static void Entrance(string root,List<string> log)
    {
        var sc=(NativeBowEntrance)NativeEntranceDirector.Script(NativeBowEntrance.Id);sc.Prepare();
        Check(NativeIntroRules.Pickable(NativeBowEntrance.Id)&&NativeIntroRules.Signature(NativeBowWire.Spec)==NativeBowEntrance.Id&&NativeIntroRules.IntroWeapon(NativeBowEntrance.Id)==NativeBowEntrance.Id,"Arrowfall is the Bow's signature and holds the bow");
        Check(NativeIntroRules.StyleName(NativeBowEntrance.Id)=="Arrowfall"&&NativeSoundKeys.Intro(NativeBowEntrance.Id)=="intro.weapon-bow","its name and music key");
        string sting=NativeIntroRules.IntroSting(NativeBowEntrance.Id);Check(sting=="audio/intros/neon-drop"&&File.Exists(Path.Combine(root,"Assets/Conversion/Resources/Audio",sting+".mp3")),"its built-in sting fallback ("+sting+")");
        Check(NativeIntroRules.Duration(NativeBowEntrance.Id)==4.8&&NativeIntroRules.NameAt(NativeBowEntrance.Id)==3.2,"the standard 4.8 s entrance and 3.2 s name card");
        // The beats (match age = song time).
        var v=NativeBowEntrance.ViewAt(.2f);Check(!v.Nocked&&v.Raised==0,"arrives with the bow carried, nothing nocked");
        v=NativeBowEntrance.ViewAt(.6f);Check(v.Phase==NativeBow.Phase.Nock,"reaching to the quiver at .4-.8");
        v=NativeBowEntrance.ViewAt(.8f);Check(v.Nocked,"the arrow on the string by the .8 beat");
        v=NativeBowEntrance.ViewAt(1.3f);Check(v.Phase==NativeBow.Phase.Draw&&v.Pull>0&&v.Pull<1,"drawing from 1.0");
        v=NativeBowEntrance.ViewAt(1.6f);Check(v.Full&&Math.Abs(v.Pull-1)<1e-6f,"full draw on the 1.6 beat");
        v=NativeBowEntrance.ViewAt(2.05f);Check(!v.Nocked&&v.Follow>0&&v.Buzz>0,"the release on the 2.0 beat (the hit)");
        Check(Math.Abs(sc.Hit-2)<1e-6f&&Math.Abs(AgeOf(sc,sc.Split)-NativeBowEntrance.SplitAge)<2e-3f,"the split overhead on the 2.4 beat");
        int cluster=0;for(int i=0;i<NativeBowEntrance.Volley;i++){float age=AgeOf(sc,sc.Land(i));Check(Math.Abs(age-NativeBowEntrance.VolleyAges[i])<2e-3f,"volley arrow "+i+" lands at "+age.ToString("0.000"));if(Math.Abs(age-2.8f)<.031f)cluster++;}
        Check(cluster>=7,"the volley lands together on the 2.8 beat ("+cluster+" of "+NativeBowEntrance.Volley+")");
        // The pose: the arms reach, the full draw on the cheek, the arrow pitched to the sky, the sky arrow leaving along it.
        float reach=0;var pose=new CapturedRig.Pose();
        string reachAt="";for(float t=0;t<=4.8f;t+=1/60f){sc.BowAt(t,out var bv,out var f);if(Reach(f)>reach){reach=Reach(f);reachAt=$"t {t:0.00} {bv.Phase} raise {bv.Raised:0.00} nocking {bv.Nocking:0.00} bow {f.BowReach:0.000} draw {f.DrawReach:0.000} hold {f.DrawWeight:0.00}";}}
        sc.BowAt(1.8f,out var hv,out var hf);float pitch=(float)Math.Asin(Math.Clamp(Vector3.Transform(Vector3.UnitZ,hf.BowRotation).Y,-1,1))/D;
        Check(reach<=.522f,"the entrance's arms reach (worst "+reach.ToString("0.000")+": "+reachAt+")");
        Check(hv.Full&&Vector3.Distance(hf.Nock,hf.Anchor)<.02f&&Math.Abs(pitch-NativeBowEntrance.SkyPitch)<2,"full draw at the cheek, the arrow pitched "+pitch.ToString("0.0")+" degrees");
        Check(sc.SkyDirection.Y>.6f&&Vector3.Distance(sc.SkyArrow(2),sc.Nock(1.99f))<.03f,"the sky arrow leaves the nock upward");
        Span<NativeBowPiece> arrows=stackalloc NativeBowPiece[NativeBowEntrance.MaxArrows];
        Check(sc.Arrows(1.9f,arrows)==0&&sc.Arrows(2.05f,arrows)==1,"one arrow in the air after the release");
        int standing=sc.Arrows(4.79f,arrows);Check(standing==NativeBowEntrance.Volley,"the whole volley stands in the floor under the name card");
        for(int i=0;i<standing;i++)
        {
            var p=arrows[i];var tip=p.Apply(new Vector3(0,0,NativeBow.ArrowLength));
            Check(tip.Y<NativeEntranceStage.FloorY&&p.Position.Y>NativeEntranceStage.FloorY+.5f,"arrow "+i+" buried head first, its nock up");
            float r=new Vector2(tip.X,tip.Z).Length();Check(r>1&&r<2.6f,"arrow "+i+" round the pilot on the dais ("+r.ToString("0.00")+" m)");
        }
        log.Add($"Arrowfall: the Bow's signature (name, intro.weapon-bow, neon-drop sting fallback); beats .4 quiver, .8 nock, 1.0 draw, 1.6 full draw, 2.0 release, 2.4 split, 2.8 volley ({cluster} of {NativeBowEntrance.Volley} together, the rest to 3.0), 3.2 hero; arms reach (worst {reach:0.000} m), the full draw at the cheek pitched {pitch:0.0} degrees");
    }

    // ---- 10. the pack-derived numbers against their provenance (derived numbers only are committed) ----
    private static void Pack(string root,List<string> log)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"tools/pack-bow-provenance.json")));var d=doc.RootElement.GetProperty("derived");
        float F(params string[] path){var e=d;foreach(var p in path)e=e.GetProperty(p);return (float)e.GetDouble();}
        var pairs=new (float code,float json,string name)[]
        {
            (NativeBowPose.PackBlade,F("blade","total"),"blade"),(NativeBowPose.PackHips,F("blade","hips"),"hips"),(NativeBowPose.PackSpine,F("blade","spine"),"spine"),(NativeBowPose.PackChest,F("blade","chest"),"chest"),
            (NativeBowPose.PackLeanSpine,F("lean","spine"),"lean spine"),(NativeBowPose.PackLeanChest,F("lean","chest"),"lean chest"),(NativeBowPose.PackPitchUp,F("lean","pitchUp"),"pitch up"),
            (NativeBowPose.PackFollowBack,F("release","back"),"follow back"),(NativeBowPose.PackFollowDown,F("release","down"),"follow down"),(NativeBowPose.PackFollowOut,F("release","out"),"follow out"),
            (NativeBowPose.PackFollowPeak,F("release","peak"),"follow peak"),(NativeBowPose.PackFollowSettle,F("release","settle"),"follow settle"),(NativeBowPose.PackDrawToAnchor,F("drawToAnchor"),"draw to anchor"),
        };
        foreach(var (code,json,name) in pairs)Check(Math.Abs(code-json)<1e-4f,"pack "+name+" "+code+" matches its provenance "+json);
        Check(NativePackBow.Loco.Length==17&&NativePackBow.Loco.All(c=>c.Frames>0&&c.Loop),"17 pack bow travel loops (walk, strafe, run)");
        log.Add($"pack: {pairs.Length} derived numbers match tools/pack-bow-provenance.json; {NativePackBow.Loco.Length} travel loops");
    }

    // ---- 11. the runtime hooks (source checks; Unity is not run here) ----
    private static void Wiring(string root,List<string> log)
    {
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));string C(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Core",f));
        Check(R("NativeSourcePresentation.cs").Contains("if(armed&&BowHeld(rig,a,time,first,layer,camera,tint,flash))armed=false;"),"the posed bow replaces the static weapon model");
        Check(R("NativeWeaponFxPresentation.cs").Contains("BowFx(arena,now);"),"the bow effects run with the gun kit's");
        Check(R("NativeFightHud.cs").Contains("BowMeter(p);"),"the HUD draws the draw meter");
        Check(R("NativeIntros.cs").Contains("if(style==NativeBowEntrance.Id&&entranceScript is NativeBowEntrance bow){BowIntro(bow,entranceTime);return;}"),"the Arrowfall entrance draws the live bow");
        var audio=R("NativeAudio.cs");Check(audio.Contains("BowCues(")&&audio.Contains("BowFlightCues(")&&audio.Contains("BowEventCue("),"the bow's sounds are cued");
        Check(R("NativeSourceModels.cs").Contains("SheldonModels();PilotBodies();BowModels();"),"the bow models are banked at load");
        Check(C("ArenaModel.cs").Contains("[NonSerialized] public NativeBowFeed Bow;"),"the provisional feed is never on the wire");
        // Nothing in play writes the provisional feed, and the bow presentation reads the actor only through the wire map.
        foreach(var f in Directory.GetFiles(Path.Combine(root,"Assets/ChipArena"),"*.cs",SearchOption.AllDirectories))
        {
            string text=File.ReadAllText(f);Check(!System.Text.RegularExpressions.Regex.IsMatch(text,@"\.Bow\.\w+\s*=[^=]"),Path.GetFileName(f)+" writes the provisional bow feed");
            if(Path.GetFileName(f).StartsWith("NativeBow",StringComparison.Ordinal)&&Path.GetFileName(f)!="NativeBow.cs")Check(!text.Contains(".Bow.DrawAt")&&!text.Contains(".Bow.RainAt"),Path.GetFileName(f)+" reads the feed past NativeBowWire");
        }
        foreach(var f in new[]{"NativeBow.cs","NativeBowMotion.cs","NativeBowFx.cs","NativeBowEntrance.cs","NativeWeaponModels.Bow.cs"}){var text=C(f);Check(!text.Contains("UnityEngine")&&!text.Contains("new Random(")&&!text.Contains("DateTime"),f+" stays engine-free and deterministic");}
        log.Add("wiring: the posed bow, its effects, HUD meter, intro bow, sounds and model bank hooked into the runtime; the provisional feed never on the wire nor written in play, read only through NativeBowWire");
    }

    // ---- 12. no allocation per frame ----
    private static void Allocation(List<string> log)
    {
        var a=Pilot(48);a.Bow.DrawAt=1;var d=new Driver();var pose=new CapturedRig.Pose();var scene=new Scene();scene.List.Add(a);var sink=new ArrowSink();var stuck=new NativeBowStuck();
        var sc=(NativeBowEntrance)NativeEntranceDirector.Script(NativeBowEntrance.Id);var arrows=new NativeBowPiece[NativeBowEntrance.MaxArrows];
        for(int i=0;i<12;i++)stuck.AddPilot(i,7,CapturedRig.Chest,Vector3.Zero,Vector3.UnitZ,0,1,true);
        void Frame(float t)
        {
            d.Step(a,t);pose.Reset();NativeBowPose.View(pose,a,t);var r=NativeBowWire.Of(a);NativeBow.At(r,t);NativeBowHud.MeterOf(r,t);
            NativeBowFx.Draw(sink,scene,t,1,false);stuck.Update(t);stuck.Draw(sink,t,Bone);sc.BowAt(t,out _,out _);sc.Arrows(t,arrows);
        }
        a.Bow.RainAt=2;a.Bow.VolleyAt=1.5f;for(float t=0;t<4;t+=1/60f)Frame(t);
        long before=GC.GetAllocatedBytesForCurrentThread();for(float t=0;t<4;t+=1/60f)Frame(t);long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
        Check(bytes==0,"a frame of the bow (pose, view, read, meter, effects, stuck arrows, the entrance's bow) allocates "+bytes+" bytes");
        log.Add("allocation: 240 frames of the third- and first-person poses, the read, the meter, the effects, the stuck arrows and the entrance's bow allocate 0 bytes");
    }
}
