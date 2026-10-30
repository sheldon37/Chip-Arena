using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ChipArena.UnityConversion;

/// <summary>
/// The weapon-signature entrances driven by the owner's Explosive pack (Core/NativeEntrancePack, NativePackIntros.Generated,
/// tools/export_pack_intros.mjs): every track is the exporter's output byte for byte (tools/pack-intros-provenance.json),
/// decodes deterministically into finite, bounded channels and spans the 4.8 s phase at 60 Hz with the sting's hit; the
/// planted feet stay on their spots on the floor (no slide, no sinking) and no body part dips under it; the weapon stays in
/// the hand (two-handed weapons with the support hand on the grip), its blade or staff above the floor; the sword's cut is
/// fast at the hit and the greatsword's slam bites the floor on it, the fire and darkness casts thrust the hand out on it,
/// the water slam reaches the floor and the lightning drop lands on it; the detail shots keep the muzzle or the casting hand
/// in frame and in view; a pose is a pure function of time and costs no allocation. The daggers keep their baked clip and
/// weapon-intro layer bit for bit (pinned fingerprint of pose, camera and effects), as does every signature without a pack
/// track. Presentation only (EntranceDirectorChecks covers timing, camera and effects).
/// </summary>
internal static class PackIntroChecks
{
    private static void Check(bool ok,string what){if(!ok)throw new InvalidOperationException("Pack intros: "+what);}
    // The daggers' Twin Phantom as it stood before the pack motion (512348d): pose, camera and effects fingerprints (0.1 mm / 1 mm).
    private const long DaggersPose=6741820660586021114,DaggersCamera=-7764488902950295394,DaggersFx=-3352056703259569675;
    public static IEnumerable<string> Run(string root)
    {
        var clips=Clips(root);
        // 1. The tracks are the exporter's: provenance, hashes, size, timing.
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"tools/pack-intros-provenance.json")));
        var prov=new Dictionary<string,JsonElement>();foreach(var e in doc.RootElement.GetProperty("entrances").EnumerateArray())prov[e.GetProperty("style").GetString()]=e;
        Check(doc.RootElement.GetProperty("channels").GetInt32()==CapturedClip.Channels&&prov.Count==NativePackIntros.All.Length,"the provenance lists every track");
        Check(NativePackIntro.For("weapon-daggers")==null,"the daggers keep their baked clip");
        int segments=0;
        foreach(var p in NativePackIntros.All)
        {
            var e=prov[p.Style];var c=p.Track;Check(NativeIntroRules.IntroWeapon(p.Style)==p.Style&&NativeEntranceDirector.Script(p.Style) is NativeWeaponEntrance w&&w.Pack==p,p.Style+": a weapon signature playing its track");
            string sha=Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(c.Data))).ToLowerInvariant();Check(sha==e.GetProperty("sha256").GetString(),p.Style+": track is the exporter's output ("+sha+")");
            Check(c.Fps==60&&c.Frames==289&&!c.Loop&&Math.Abs(c.Duration-NativeEntranceScript.Duration)<1e-4f&&e.GetProperty("frames").GetInt32()==c.Frames,p.Style+": 4.8 s at 60 Hz");
            Check(p.Hit==NativeIntroMotion.Hit(p.Style)&&Math.Abs(e.GetProperty("hit").GetSingle()-p.Hit)<1e-6f&&c.Marks.Length==1&&c.Marks[0]==p.Hit,p.Style+": hits on its sting's accent");
            Check(Same(p.PlantsLeft,e.GetProperty("plants").GetProperty("left"))&&Same(p.PlantsRight,e.GetProperty("plants").GetProperty("right"))&&p.TwoHand==(e.GetProperty("support").ValueKind==JsonValueKind.Array)&&p.Breathe==e.GetProperty("breathe").GetBoolean(),p.Style+": plants and grip match the provenance");
            foreach(var s in e.GetProperty("segments").EnumerateArray()){segments++;Check(s.GetProperty("input").GetString().Length==64&&c.Source.Contains(s.GetProperty("source").GetString()),p.Style+": segment "+s.GetProperty("source").GetString()+" recorded");}
            var a=c.Decode();var b=new CapturedClip(c.Name,c.Source,c.Fps,c.Frames,c.Loop,c.Marks,c.Pace,c.Data).Decode();
            for(int i=0;i<a.Length;i++){Check(BitConverter.SingleToInt32Bits(a[i])==BitConverter.SingleToInt32Bits(b[i]),p.Style+": deterministic decode");Check(float.IsFinite(a[i])&&Math.Abs(a[i])<8,p.Style+": finite, bounded channel "+(i%CapturedClip.Channels));}
            for(int f=0;f<c.Frames;f++){int o=f*CapturedClip.Channels;foreach(int u in new[]{MeleeCapture.Blade,MeleeCapture.Edge,MeleeCapture.LFwd,MeleeCapture.LUp})Check(Math.Abs(new Vector3(a[o+u],a[o+u+1],a[o+u+2]).Length()-1)<.05f,p.Style+": unit direction channels");}
            for(int i=0;i+1<p.PlantsLeft.Length;i+=2)Check(p.PlantsLeft[i]<p.PlantsLeft[i+1],p.Style+": plant windows ordered");
            for(int i=0;i+1<p.PlantsRight.Length;i+=2)Check(p.PlantsRight[i]<p.PlantsRight[i+1],p.Style+": plant windows ordered");
        }
        yield return $"pack intros: {NativePackIntros.All.Length} entrance tracks ({string.Join(", ",NativePackIntros.All.Select(p=>p.Style))}) of {segments} pack segments match tools/pack-intros-provenance.json, decode deterministically and span 4.8 s at 60 Hz with their sting's hit";
        // 2. The body: planted feet stay put on the floor, nothing sinks under it, the weapon stays gripped and off the floor.
        var pose=new CapturedRig.Pose();
        foreach(var p in NativePackIntros.All)
        {
            var sc=Bind(p.Style,clips);float slide=0,sole=0,lowest=9,soles=9,grip=0,tipFloor=9,staffFloor=9,clear=0,deep=0;string clearWhat="";int planted=0;bool staff=Staff(p.Style);
            for(int side=0;side<2;side++)
            {
                var plants=side==0?p.PlantsLeft:p.PlantsRight;int foot=side==0?CapturedRig.LFoot:CapturedRig.RFoot;
                for(int k=0;k+1<plants.Length;k+=2)
                {
                    Vector3? spot=null;
                    for(float t=plants[k];t<=plants[k+1]+1e-4f;t+=1/120f)
                    {
                        p.Pose(t,pose,out var r,out var q);pose.World();var at=r+Vector3.Transform(pose.WorldPosition[foot],q);spot??=at;planted++;
                        slide=Math.Max(slide,new Vector2(at.X-spot.Value.X,at.Z-spot.Value.Z).Length());sole=Math.Max(sole,Math.Abs(NativePackIntro.Sole(pose,r,side==0)-NativeEntranceStage.FloorY));
                    }
                }
            }
            for(float t=0;t<=NativeEntranceScript.Duration+1e-4f;t+=1/120f)
            {
                p.Pose(t,pose,out var r,out var q);Check(NativeIntroMotion.Finite(pose)&&float.IsFinite(r.X)&&float.IsFinite(r.Z),p.Style+": finite pose at "+t);
                pose.World();float low=NativePackIntro.LowestAboveFeet(pose,r);lowest=Math.Min(lowest,low);if(low<NativeEntranceStage.FloorY-.025f)deep+=1/120f;soles=Math.Min(soles,Math.Min(NativePackIntro.Sole(pose,r,true),NativePackIntro.Sole(pose,r,false)));
                var wrist=pose.WorldPosition[CapturedRig.RHand];var dir=Vector3.Transform(Vector3.UnitZ,pose.WorldRotation[CapturedRig.RHand]);if(p.Blade>0)tipFloor=Math.Min(tipFloor,r.Y+wrist.Y+dir.Y*p.Blade-NativeEntranceStage.FloorY);
                if(staff){var lw=pose.WorldPosition[CapturedRig.LHand];var ld=Vector3.Transform(Vector3.UnitZ,pose.WorldRotation[CapturedRig.LHand]);staffFloor=Math.Min(staffFloor,r.Y+Math.Min(lw.Y+ld.Y*StaffFrom,lw.Y+ld.Y*StaffTo)-NativeEntranceStage.FloorY);}
                if(p.TwoHand&&p.SupportAlways)grip=Math.Max(grip,(pose.WorldPosition[CapturedRig.LHand]-(wrist+Vector3.Transform(p.Support,pose.WorldRotation[CapturedRig.RHand]))).Length());
                float d=NativeIntroMotion.Clearance(p.Style,pose,out var what);if(d>clear){clear=d;clearWhat=what+" at "+t.ToString("0.00");}
            }
            Check(planted>0,p.Style+": the feet are planted");
            Check(slide<=.01f,p.Style+": a planted foot slides "+(slide*1000).ToString("0.0")+" mm");Check(sole<=.012f,p.Style+": a planted foot leaves the floor by "+(sole*1000).ToString("0.0")+" mm");
            // The soles never go through the floor. The voxel shin is a 20 cm block on a human's crouch: over a planted foot the
            // heel rises out of the floor, but through a quick step or a landing a shin corner may graze into the floor top for
            // a moment (at most 6 cm, under 2.5 cm except for at most .2 s).
            Check(soles>=NativeEntranceStage.FloorY-.012f,p.Style+": a foot digs "+((NativeEntranceStage.FloorY-soles)*1000).ToString("0")+" mm into the floor");
            Check(lowest>=NativeEntranceStage.FloorY-.06f&&deep<=.2f,p.Style+": the body sinks "+((NativeEntranceStage.FloorY-lowest)*1000).ToString("0")+" mm under the floor ("+deep.ToString("0.00")+" s past 2.5 cm)");
            Check(!p.TwoHand||grip<=.03f,p.Style+": the support hand leaves the grip by "+(grip*1000).ToString("0")+" mm");
            Check(p.Blade<=0||tipFloor>=-.005f,p.Style+": the blade sinks "+(-tipFloor*1000).ToString("0")+" mm into the floor");
            Check(!staff||staffFloor>=-.005f,p.Style+": the staff sinks "+(-staffFloor*1000).ToString("0")+" mm into the floor");
            Check(clear<=.06f,p.Style+": the "+clearWhat+" sinks "+(clear*1000).ToString("0")+" mm into the body");
            yield return $"pack intro {p.Style}: planted feet slide at most {slide*1000:0.0} mm and stay within {sole*1000:0.0} mm of the floor ({planted} planted samples); soles at least {(soles-NativeEntranceStage.FloorY)*1000:0} mm and every other body box at least {(lowest-NativeEntranceStage.FloorY)*1000:0} mm off the floor top ({deep:0.00} s past -2.5 cm){(p.Blade>0?$"; blade tip at least {tipFloor*1000:0} mm above it":"")}{(staff?$"; staff ends at least {staffFloor*1000:0} mm above it":"")}{(p.TwoHand&&p.SupportAlways?$"; support hand within {grip*1000:0} mm of the grip":"")}; deepest limb or weapon contact with the body {clear*1000:0} mm ({clearWhat})";
        }
        // 3. The hit lands on the sting: the sword's blade is cutting fast, the greatsword's tip bites the floor; the fire and
        // darkness casts thrust the casting hand out ahead of the chest into it, the water slam drives the hand down to the floor,
        // the lightning drop lands both feet on it out of the air.
        foreach(var p in NativePackIntros.All)
        {
            var sc=Bind(p.Style,clips);float floor=NativeEntranceStage.FloorY;
            if(Staff(p.Style))
            {
                var hand=sc.Anchor(NativeEntranceAnchor.RightHand,p.Hit);var v=(hand-sc.Anchor(NativeEntranceAnchor.RightHand,p.Hit-1/30f))*30;var fw=Vector3.Normalize(sc.Anchor(NativeEntranceAnchor.Forward,p.Hit));
                float ahead=Vector3.Dot(hand-sc.Anchor(NativeEntranceAnchor.Chest,p.Hit),fw),thrust=Vector3.Dot(v,fw);
                p.Pose(p.Hit,pose,out var r,out _);pose.World();float feet=Math.Max(Math.Abs(NativePackIntro.Sole(pose,r,true)-floor),Math.Abs(NativePackIntro.Sole(pose,r,false)-floor)),air=9;
                for(float t=p.Hit-.5f;t<p.Hit-.15f;t+=1/60f){p.Pose(t,pose,out var r2,out _);pose.World();air=Math.Min(air,Math.Min(NativePackIntro.Sole(pose,r2,true),NativePackIntro.Sole(pose,r2,false))-floor);}
                if(p.Style=="weapon-fire"||p.Style=="weapon-darkness")Check(thrust>=3&&ahead>=.4f,p.Style+": the cast reaches "+ahead.ToString("0.00")+" m ahead of the chest at "+thrust.ToString("0.0")+" m/s at its hit");
                if(p.Style=="weapon-water")Check(hand.Y-floor<=.2f&&v.Length()>=3,p.Style+": the slam's hand ends "+(hand.Y-floor).ToString("0.00")+" m over the floor at its hit");
                if(p.Style=="weapon-lightning")Check(feet<=.012f&&air>=1,p.Style+": the drop lands "+(feet*1000).ToString("0")+" mm off the floor from "+air.ToString("0.00")+" m up");
                yield return $"pack intro {p.Style}: at the {p.Hit} s hit the casting hand moves {v.Length():0.0} m/s ({thrust:0.0} forward), {ahead:0.00} m ahead of the chest and {(hand.Y-floor)*100:0} cm over the floor; feet {feet*1000:0} mm off the floor ({air:0.00} m up .15-.5 s before)";
                continue;
            }
            var tip=sc.Anchor(NativeEntranceAnchor.RightTip,p.Hit);float speed=(sc.Anchor(NativeEntranceAnchor.RightTip,p.Hit)-sc.Anchor(NativeEntranceAnchor.RightTip,p.Hit-1/30f)).Length()*30;
            if(p.Style=="weapon-sword")Check(speed>=6,"the sword's cut is moving "+speed.ToString("0.0")+" m/s at its hit");
            if(p.Style=="weapon-greatsword")Check(tip.Y-floor<.12f,"the greatsword's slam ends "+(tip.Y-floor).ToString("0.00")+" m over the floor at its hit");
            yield return $"pack intro {p.Style}: at the {p.Hit} s hit the weapon tip (blade, muzzle or shield face) moves {speed:0.0} m/s, {(tip.Y-floor)*100:0} cm over the floor";
        }
        // 4. Pure and allocation-free: any order gives the same pose; a 60 Hz playthrough allocates nothing.
        var rng=new Random(7);foreach(var p in NativePackIntros.All)
        {
            var times=Enumerable.Range(0,121).Select(i=>i*.04f).ToArray();var first=new Dictionary<float,(Vector3,Quaternion,Vector3)>();
            foreach(var order in new[]{times,times.Reverse().ToArray(),times.OrderBy(_=>rng.Next()).ToArray()})foreach(float t in order)
            {
                p.Pose(t,pose,out var r,out var q);pose.World();var v=(r,q,pose.WorldPosition[CapturedRig.LHand]+pose.WorldPosition[CapturedRig.RFoot]);
                if(first.TryGetValue(t,out var was))Check(was==v,p.Style+" differs at "+t+" when evaluated in another order");else first[t]=v;
            }
            p.Pose(0,pose,out _,out _);long before=GC.GetAllocatedBytesForCurrentThread();for(float t=0;t<=4.8f;t+=1/60f)p.Pose(t,pose,out _,out _);
            Check(GC.GetAllocatedBytesForCurrentThread()==before,p.Style+": a playthrough allocates");
        }
        yield return "pack intros: poses are pure functions of time (shuffled and reversed evaluation agree) and a 60 Hz playthrough allocates nothing";
        // 5. The detail shots read: the guns' muzzle (and the pistol's hand; down the aim, the target too) and the magic schools'
        // casting hand project inside the frame with a 15% margin, out from behind the pilot's own body (a hand sweeping past the
        // head may hide for at most .1 s of a shot), and the pilot's body covers at most 40% of the frame, so a new pose never
        // leaves a close-up on an empty frame, a hidden hand or a wall of armour.
        int detail=0;float worstCover=0,worstEdge=0,worstHidden=0;string coverAt="",edgeAt="",hiddenAt="";
        foreach(var (style,shotsToCheck,keep) in Detail)
        {
            if(NativePackIntro.For(style)==null)continue;var sc=Bind(style,clips);
            foreach(int k in shotsToCheck)
            {
                float from=AgeOf(sc,sc.Shots[k].At),to=AgeOf(sc,sc.Shots[k+1].At),hidden=0;
                for(float age=from+.04f;age<to-.04f;age+=1/60f)
                {
                    float t=sc.Clock(age);sc.Camera(age,out var view);sc.Pose(t,pose,out var r,out var q);pose.World();detail++;float edge=0;bool hid=false;
                    foreach(var a in keep){var at=sc.Anchor(a,t);edge=Math.Max(edge,Edge(view,at));hid|=Hidden(view,pose,r,q,at);}
                    // The view down the aim also holds the target.
                    if(k==2&&sc is GunEntrance gun)edge=Math.Max(edge,Edge(view,gun.TargetPoint));
                    if(hid)hidden+=1/60f;if(hidden>worstHidden){worstHidden=hidden;hiddenAt=style+" shot "+k;}
                    if(edge>worstEdge){worstEdge=edge;edgeAt=style+" shot "+k+" at "+age.ToString("0.00");}
                    float cover=Cover(view,pose,r,q);if(cover>worstCover){worstCover=cover;coverAt=style+" shot "+k+" at "+age.ToString("0.00");}
                    Check(edge<=.85f,style+" detail shot "+k+" loses its "+(Staff(style)?"casting hand":"muzzle")+" at "+age.ToString("0.00")+" (|x|,|y| "+edge.ToString("0.00")+")");
                    Check(cover<=.4f,style+" detail shot "+k+" is "+(cover*100).ToString("0")+"% pilot at "+age.ToString("0.00"));
                    Check(hidden<=.1f+1e-4f,style+" detail shot "+k+" hides its "+(Staff(style)?"casting hand":"muzzle")+" behind the pilot at "+age.ToString("0.00"));
                }
            }
        }
        yield return $"pack intros: detail shots keep the gun muzzle (the pistol's hand, and the target down the aim) and the casting hand inside 85% of the frame (worst {worstEdge:0.00}, {edgeAt}), behind the pilot at most {worstHidden:0.00} s of a shot ({(hiddenAt==""?"none":hiddenAt)}) and the pilot under 40% of the frame (worst {worstCover*100:0}%, {coverAt}) over {detail} samples";
        // 6. The daggers (and any signature without a track) are the baked clip and its weapon-intro layer, bit for bit.
        int baked=0;
        foreach(var style in NativeIntroRules.Entrances.Where(s=>NativeEntranceDirector.Script(s) is NativeWeaponEntrance))
        {
            if(NativePackIntro.For(style)!=null)continue;var sc=Bind(style,clips);var c=clips["intro-"+style];var clip=new NativeEntranceClip(c.values,c.roots,c.frames,c.duration);var reference=new CapturedRig.Pose();
            for(float t=0;t<=4.8f;t+=1/30f)
            {
                sc.Pose(t,pose,out var r,out var q);NativeEntrancePose.Baked(style,clip,t,reference,out var r2,out var q2);baked++;
                Check(r==r2&&q==q2&&Enumerable.Range(0,22).All(b=>pose.Rotation[b]==reference.Rotation[b]&&pose.Position[b]==reference.Position[b]),style+" leaves its baked clip at "+t);
            }
        }
        Fingerprint(Bind("weapon-daggers",clips),out long fp,out long fc,out long ff);
        Check(fp==DaggersPose&&fc==DaggersCamera&&ff==DaggersFx,$"Twin Phantom changed (pose {fp}, camera {fc}, effects {ff})");
        yield return $"pack intros: Twin Phantom (daggers) is unchanged (pose, camera and effects fingerprints over 145 frames match 512348d) and every signature without a pack track poses its baked clip bit for bit ({baked} frames)";
    }
    // The detail shots (indices into their Shots) and what each must keep in frame: the pistol's twirl and the over-the-shoulder
    // view down the aim; the rifle's drop into the crouched aim and the scope view; the machine gun's feed and hands; the fire
    // gathering in the casting hand and the arm rising into the strike; the water hand raised under the serpents and slammed
    // down; the darkness claw raised and drawn back for the cast.
    private static readonly (string style,int[] shots,NativeEntranceAnchor[] keep)[] Detail=
    {
        ("weapon-pistol",new[]{1,2},new[]{NativeEntranceAnchor.RightTip,NativeEntranceAnchor.RightHand}),("weapon-rifle",new[]{1,2},new[]{NativeEntranceAnchor.RightTip}),
        ("weapon-machinegun",new[]{1},new[]{NativeEntranceAnchor.RightTip}),("weapon-fire",new[]{1,2},new[]{NativeEntranceAnchor.RightHand}),
        ("weapon-water",new[]{2},new[]{NativeEntranceAnchor.RightHand}),("weapon-darkness",new[]{2},new[]{NativeEntranceAnchor.RightHand}),
    };
    // The magic schools: the staff rides the left hand (NativeIntroMotion's probes, -.34 to +.88 along the wrist's +z), the right hand casts.
    private static bool Staff(string style)=>style=="weapon-fire"||style=="weapon-water"||style=="weapon-lightning"||style=="weapon-darkness";
    private const float StaffFrom=-.34f,StaffTo=.88f;
    // Whether a body box other than the right arm's own lies between the eye and `p` (the casting hand, the muzzle).
    private static bool Hidden(NativeEntranceView v,CapturedRig.Pose pose,Vector3 root,Quaternion rotation,Vector3 p)
    {
        var d=p-v.Eye;float len=d.Length();if(len<1e-4f)return false;d/=len;
        foreach(var b in CapturedRig.Boxes)
        {
            if(b.Bone==CapturedRig.RArm||b.Bone==CapturedRig.RElbow||b.Bone==CapturedRig.RHand)continue;
            var q=Quaternion.Normalize(rotation*pose.WorldRotation[b.Bone]);var c=root+Vector3.Transform(pose.WorldPosition[b.Bone]+Vector3.Transform(b.Position,pose.WorldRotation[b.Bone]),rotation);
            var inv=Quaternion.Conjugate(q);var o=Vector3.Transform(v.Eye-c,inv);var dir=Vector3.Transform(d,inv);var h=b.Size*.5f;float t0=0,t1=len-.05f;bool hit=true;
            for(int a=0;a<3&&hit;a++)
            {
                float oa=a==0?o.X:a==1?o.Y:o.Z,da=a==0?dir.X:a==1?dir.Y:dir.Z,ha=a==0?h.X:a==1?h.Y:h.Z;
                if(Math.Abs(da)<1e-6f){if(Math.Abs(oa)>ha)hit=false;continue;}
                float u0=(-ha-oa)/da,u1=(ha-oa)/da;if(u0>u1)(u0,u1)=(u1,u0);t0=Math.Max(t0,u0);t1=Math.Min(t1,u1);if(t0>t1)hit=false;
            }
            if(hit)return true;
        }
        return false;
    }
    // The match age at which presentation time `t` is reached (the clock is monotone).
    private static float AgeOf(NativeEntranceScript sc,float t){float lo=0,hi=5;for(int i=0;i<40;i++){float m=(lo+hi)*.5f;if(sc.Clock(m)<t)lo=m;else hi=m;}return hi;}
    private const float Aspect=16/9f;
    // Where point `p` projects in view `v` (x, y in -1..1 across the frame; far outside, or behind, when off screen).
    private static Vector2 Project(NativeEntranceView v,Vector3 p,out bool front)
    {
        var f=Vector3.Normalize(v.Look-v.Eye);var right=Vector3.Normalize(Vector3.Cross(f,v.Up));var up=Vector3.Cross(right,f);var d=p-v.Eye;float z=Vector3.Dot(d,f);front=z>.05f;
        float tan=(float)Math.Tan(v.Fov*Math.PI/360);return front?new Vector2(Vector3.Dot(d,right)/(z*tan*Aspect),Vector3.Dot(d,up)/(z*tan)):new Vector2(9,9);
    }
    private static float Edge(NativeEntranceView v,Vector3 p){var s=Project(v,p,out bool front);return front?Math.Max(Math.Abs(s.X),Math.Abs(s.Y)):9;}
    // The share of the frame the pilot's body boxes cover (each box's projected bounds on a 64 x 36 grid; a box reaching
    // behind the lens covers the frame).
    private static readonly bool[] grid=new bool[64*36];
    private static float Cover(NativeEntranceView v,CapturedRig.Pose pose,Vector3 root,Quaternion rotation)
    {
        Array.Clear(grid,0,grid.Length);
        foreach(var b in CapturedRig.Boxes)
        {
            var q=Quaternion.Normalize(rotation*pose.WorldRotation[b.Bone]);var c=root+Vector3.Transform(pose.WorldPosition[b.Bone]+Vector3.Transform(b.Position,pose.WorldRotation[b.Bone]),rotation);
            float x0=9,x1=-9,y0=9,y1=-9;bool behind=false,any=false;
            for(int i=0;i<8;i++)
            {
                var corner=c+Vector3.Transform(new Vector3((i&1)==0?-.5f:.5f,(i&2)==0?-.5f:.5f,(i&4)==0?-.5f:.5f)*b.Size,q);var s=Project(v,corner,out bool front);
                if(!front){behind=true;continue;}any=true;x0=Math.Min(x0,s.X);x1=Math.Max(x1,s.X);y0=Math.Min(y0,s.Y);y1=Math.Max(y1,s.Y);
            }
            if(behind&&any){x0=-1;x1=1;y0=-1;y1=1;}else if(!any)continue;
            int gx0=Math.Max(0,(int)Math.Floor((x0+1)*32)),gx1=Math.Min(63,(int)Math.Ceiling((x1+1)*32)-1),gy0=Math.Max(0,(int)Math.Floor((y0+1)*18)),gy1=Math.Min(35,(int)Math.Ceiling((y1+1)*18)-1);
            for(int gy=gy0;gy<=gy1;gy++)for(int gx=gx0;gx<=gx1;gx++)grid[gy*64+gx]=true;
        }
        int n=0;foreach(bool g in grid)if(g)n++;return n/(float)grid.Length;
    }
    private static bool Same(float[] a,JsonElement e){var b=e.EnumerateArray().Select(x=>x.GetSingle()).ToArray();return a.Length==b.Length&&a.Zip(b).All(x=>Math.Abs(x.First-x.Second)<1e-4f);}
    private static NativeEntranceScript Bind(string style,Dictionary<string,Clip> clips)
    {
        var sc=NativeEntranceDirector.Script(style);var c=clips["intro-"+style];sc.Bind(new NativeEntranceClip(c.values,c.roots,c.frames,c.duration));return sc;
    }
    private sealed class Sink:INativeEntranceFx,INativeEntranceProps
    {
        public long H;private void Mix(Vector3 a,float k){H=H*31+(long)Math.Round(a.X*1000)*7+(long)Math.Round(a.Y*1000)*11+(long)Math.Round(a.Z*1000)*13+(long)Math.Round(k*1000);}
        public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed)=>Mix(at,size);
        public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed)=>Mix(head,width);
        public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor)=>Mix(at,size);
        public void Piece(WeaponShape shape,bool glow,Vector3 position,Quaternion rotation,Vector3 scale,Vector3 color)=>Mix(position,scale.X);
    }
    // Pose (every joint, 0.1 mm), camera (eye, look, field of view) and effects/set (1 mm) over 145 frames at 30 Hz.
    private static void Fingerprint(NativeEntranceScript sc,out long hp,out long hc,out long fx)
    {
        var pose=new CapturedRig.Pose();var sink=new Sink();var lights=new NativeEntranceLights();hp=0;hc=0;
        for(int i=0;i<=144;i++)
        {
            float age=i/30f,t=sc.Clock(age);sc.Pose(t,pose,out var r,out var q);pose.World();
            foreach(var p in pose.WorldPosition)hp=hp*31+(long)Math.Round(p.X*1e4)*3+(long)Math.Round(p.Y*1e4)*5+(long)Math.Round(p.Z*1e4)*7;hp=hp*31+(long)Math.Round(r.X*1e4)+(long)Math.Round(r.Z*1e4)*3+(long)Math.Round(q.Y*1e4)*5;
            sc.Camera(age,out var v);hc=hc*31+(long)Math.Round(v.Eye.X*1e4)+(long)Math.Round(v.Eye.Y*1e4)*3+(long)Math.Round(v.Look.Z*1e4)*5+(long)Math.Round(v.Fov*100)*7;
            sc.Effects(t,sink,1);sc.Props(t,sink,1);sc.Lights(t,lights);hc=hc*31+lights.Points;
        }
        fx=sink.H;
    }
    private sealed class Clip{public float[] values,roots;public int frames;public float duration;}
    private static Dictionary<string,Clip> Clips(string root)
    {
        using var envelope=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/SourceMotion.json")));
        using var bytes=new MemoryStream(Convert.FromBase64String(envelope.RootElement.GetProperty("data").GetString()));using var zip=new GZipStream(bytes,CompressionMode.Decompress);using var reader=new StreamReader(zip);
        using var doc=JsonDocument.Parse(reader.ReadToEnd());var map=new Dictionary<string,Clip>();
        foreach(var c in doc.RootElement.GetProperty("clips").EnumerateArray())
        {
            string key=c.GetProperty("key").GetString();if(!key.StartsWith("intro-weapon-"))continue;
            map[key]=new Clip{values=c.GetProperty("values").EnumerateArray().Select(v=>(float)v.GetDouble()).ToArray(),roots=c.GetProperty("roots").EnumerateArray().Select(v=>(float)v.GetDouble()).ToArray(),frames=c.GetProperty("frames").GetInt32(),duration=(float)c.GetProperty("duration").GetDouble()};
        }
        return map;
    }
}
