#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The Hulk, its jump-in and the wall breach (owner 2026-10-13: "Add a Hulk zombie too, similar to brute but much bigger model
    // size, huge knockback, very slow, very high hp. 1/2 max spawned. The jump in too, shaking the ground and a even bigger stage
    // effect when landing. They can also break through the walls to enter (Stage will eventually holographically recover broken
    // pieces)"), and the spitter's reworked spit ("The spitter should shoot do a windup animation and shoot acidic spit from their
    // mouth, creating a small puddle of poison (Can corrode destructibles). Can interupt their spit by hitting them."). Presentation
    // only: the kind's tables and proposed numbers, its timing pinned to the authority's formula, every living pose inside its
    // proposed capsule, the drop's Hulk profile, the breach's hole, pieces and holographic restore, the jaws and the mouth socket,
    // the interrupt, determinism and no allocation, and a runtime that only draws.
    internal static class HulkSpitterChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Hulk / spitter: "+what);count++;}
        private static string R(string root,string path)=>File.ReadAllText(Path.Combine(root,path));
        public static void Run(string root)
        {
            Kind();Timing();Inside();Drop();Breach();Jaws();Interrupt();Effects();Determinism();Allocation();Runtime(root);
            Console.WriteLine("PASS hulk and spitter ("+count+" checks: the Hulk's tables and proposed capsule, its smash on the authority's strike, every living pose inside the capsule, the heavy jump-in profile, the breach's hole, pieces and hologram restore from map data, hinged jaws and the mouth socket, the spit's wind-up and release, the interrupt, deterministic, no allocation, presentation-only runtime)");
        }
        // ---- the kind ----
        private static void Kind()
        {
            int k=NativeHordeKinds.Hulk,n=NativeHordeKinds.Count;
            Check(n==7&&NativeHordeKinds.Ids[k]=="hulk"&&NativeHordeKinds.Resolve("hulk")==k&&NativeHordeKinds.Shipped[k],"the Hulk is the seventh shipped kind");
            foreach(var len in new[]{NativeHordeKinds.Ids.Length,NativeHordeKinds.Names.Length,NativeHordeKinds.Shipped.Length,NativeHordeKinds.Looks.Length,NativeHordeKinds.Radius.Length,NativeHordeKinds.Height.Length,NativeHordeKinds.Axis.Length,NativeHordeKinds.Root.Length,NativeHordeKinds.CenterAhead.Length,NativeHordeKinds.Scale.Length,NativeHordeKinds.Mass.Length,NativeHordeKinds.Default.Length,NativeHordeKinds.Seam.Length,NativeHordeKinds.SeamClass.Length,NativeHordeKinds.Proposed.Length})
                Check(len==NativeHordeKinds.All,"every kind table has the Hulk, the zone kinds after it ("+len+")");
            foreach(var len in new[]{NativeZombieMotion.Tempo.Length,NativeHordeModels.HeadWidth.Length,NativeHordeModels.HeadDrop.Length})Check(len==n,"the Siren's own tables have the Hulk ("+len+")");
            float scale=NativeHordeKinds.Scale[k];Check(scale>=1.6f&&scale<=2&&NativeHordeKinds.Scale.Take(k).All(x=>x==1),"the Hulk is drawn 1.6-2 times the brute's size ("+scale+"), every other kind at its own");
            Check(Math.Abs(NativeHordeKinds.WorldRadius(k)-.954f)<1e-3f&&Math.Abs(NativeHordeKinds.WorldHeight(k)-3.06f)<1e-3f&&Math.Abs(NativeHordeKinds.RootHeight(k,false)-1.53f)<1e-3f,"its proposed capsule: radius .95, height 3.06, the root 1.53 up");
            Check(NativeHordeKinds.RootHeight(NativeHordeKinds.Brute,false)==.85f&&NativeHordeKinds.RootHeight(NativeHordeKinds.Brute,true)==.44f&&NativeHordeKinds.DrawRoot(NativeHordeKinds.Shambler,false)==.85f,"every other kind's root as before");
            var h=NativeHordeKinds.ProposedRow(k);var b=NativeHordeKinds.ProposedRow(NativeHordeKinds.Brute);
            Check(h.id=="hulk"&&h.attack=="smash"&&h.hp>=5*b.hp&&h.walk<b.walk&&h.run<b.run&&h.impulse==900&&h.impulse>2*b.impulse&&h.hit>b.hit&&h.cycle>b.cycle&&h.smashRadius==NativeHordeKinds.HulkSmashRadius,"proposed: very high hp, very slow, a huge shove, a slow wind-up, a wide smash");
            Check(NativeHordeKinds.MixCount==k&&NativeHordeKinds.Mass[k]>NativeHordeKinds.Mass[NativeHordeKinds.Brute]*3,"the preview's mix leaves the Hulk out; it barely rocks to a hit");
            Check(NativeHordeKinds.SmashAheadOf(k)==2&&NativeHordeKinds.SmashRadiusOf(k)==2.8f&&NativeHordeKinds.SmashAheadOf(NativeHordeKinds.Brute)==NativeHordeKinds.SmashAhead&&NativeHordeKinds.SmashRadiusOf(NativeHordeKinds.Brute)==NativeHordeKinds.SmashRadius,"the brute's disc is the authority's, the Hulk's the proposal");
            // Three tiers within budget, the full tier detailed enough to be seen across the arena.
            for(int l=0;l<NativeHordeKinds.Looks[k];l++)for(int t=0;t<NativeHordeModels.Tiers;t++){var m=NativeHordeModels.Build(k,l,t);Check(m.Triangles<=NativeHordeModels.Budget[t],"hulk tier "+t+" within budget ("+m.Triangles+")");if(t<2)Check(m.Jaw!=null&&m.Jaw.Triangles>0,"its jaw is its own model on the full and reduced tiers");}
        }
        // ---- the blow on the authority's strike ----
        private static void Timing()
        {
            var pose=new CapturedRig.Pose();int k=NativeHordeKinds.Hulk;var st=NativeHordeKinds.ProposedRow(k);float H=NativeHordeKinds.Strike(st.hit,false,0),Rc=NativeHordeKinds.Recover(st.hit,st.cycle,false,0);
            Check(H==st.hit&&Rc==Math.Max(st.cycle,st.hit+.05f),"the Hulk's strike and recovery are the authority's formula");
            var g=new NativeHordeGait();var f=new HordeMotionInput{Kind=k,Id=81000,Seed=NativeHordeKinds.Seed(81000),Health=1,AttackAge=float.NaN,Root=new Vector3(0,.85f,0),Attack=NativeHordeKinds.Attack.Smash,Strike=H,Recover=Rc,Side=1};
            float prev=-1;for(int i=0;i<=(int)((1+Rc)*120);i++){float t=i/120f;f.Time=t;f.AttackAge=t>=1?t-1:float.NaN;g.Pose(f,pose);if(t<1)continue;Check(g.AttackClip>=0&&g.AttackClipTime>=prev-1e-5f,"the hammer plays its take forward");prev=g.AttackClipTime;}
            f.Time=1+H;f.AttackAge=H;g.Pose(f,pose);Check(Math.Abs(g.AttackClipTime-NativeZombieClips.Clip(g.AttackClip).Marks[2])<1e-5f,"its impact lands exactly at LastAttack + hit");
            // The spitter's proposed wind-up and its release on the strike.
            var sp=NativeHordeKinds.ProposedRow(NativeHordeKinds.Spitter);Check(sp.hit==NativeHordeKinds.SpitHit&&sp.hit>=.7f,"the spit's wind-up is long enough to read and to hit ("+sp.hit+" s)");
            float S=sp.hit;Check(Math.Abs(NativeHordeFx.Swell(NativeHordeKinds.Attack.Spit,S-1e-3f,S)-1)<.12f&&NativeHordeFx.Swell(NativeHordeKinds.Attack.Spit,S+.2f,S)<.05f,"the sac is full at the release and empties as the glob leaves");
            float peak=NativeHordeJaw.Open(NativeHordeKinds.Spitter,NativeHordeKinds.Attack.Spit,S,S,float.NaN,float.NaN,0,1);Check(peak>.85f,"the jaw is unhinged at the release ("+peak+")");
            Check(NativeHordeJaw.Open(NativeHordeKinds.Spitter,NativeHordeKinds.Attack.Spit,.05f,S,float.NaN,float.NaN,0,1)<.3f&&NativeHordeJaw.Open(NativeHordeKinds.Spitter,NativeHordeKinds.Attack.Spit,S+.8f,S,float.NaN,float.NaN,0,1)<.1f,"it opens through the wind-up and shuts after");
            int drool=0;for(int i=0;i<NativeHordeFx.DroolDrops;i++)for(float a=0;a<S+.4f;a+=.01f)if(NativeHordeFx.Drool(5,i,a,S,out var o,out _,out _)){drool++;Check(a<=S+.1f&&o.Y<0,"drool runs off the jaw only in the wind-up");}
            Check(drool>0,"it drools");
            // Its rear-back: the knock-back reel's start, held to the release (the take is the pack's).
            Check(NativeZombieMotion.SpitRear>=0&&NativeZombieClips.Key(NativeZombieMotion.SpitRear).StartsWith("knock")&&NativeZombieMotion.Roar>=0&&NativeZombieClips.Key(NativeZombieMotion.Roar).StartsWith("knock"),"the rear-back and the roar are on the capture");
        }
        // ---- every living pose inside the proposed capsule ----
        private static void Inside()
        {
            var pose=new CapturedRig.Pose();int k=NativeHordeKinds.Hulk;var st=NativeHordeKinds.ProposedRow(k);float sc=NativeHordeKinds.Scale[k];
            for(int l=0;l<NativeHordeKinds.Looks[k];l++)
            {
                var model=NativeHordeModels.Build(k,l,0);
                foreach(var (name,speed,attack,roar) in new[]{("stands",0f,false,false),("walks",st.walk,false,false),("runs",st.run,false,false),("smashes",0f,true,false),("roars",0f,false,true),("roars walking",st.walk,false,true)})
                {
                    var g=new NativeHordeGait();var f=new HordeMotionInput{Kind=k,Look=l,Id=82000+l,Seed=NativeHordeKinds.Seed(82000+l),Health=1,AttackAge=float.NaN,Attack=NativeHordeKinds.Attack.Smash,Strike=st.hit,Recover=st.cycle,Side=1};
                    float trunk=0,arms=0,legs=0,low=9;
                    for(int i=0;i<480;i++)
                    {
                        float t=i/60f,v=speed/sc;f.Time=t;f.Root=new Vector3(0,NativeHordeKinds.Root[k],v*t);f.Velocity=new Vector3(0,0,v);f.Ground=0;
                        f.AttackAge=attack&&t>1?(t-1)%(st.cycle+.3f):float.NaN;f.Roaring=roar&&t>1;f.RoarAge=roar?(t-1)%NativeHordeRoar.Seconds:float.NaN;g.Pose(f,pose);if(i<90||i%9!=0)continue;
                        Measure(k,model.Mesh,pose,ref trunk,ref arms,ref legs,ref low);Measure(k,model.Jaw,pose,ref trunk,ref arms,ref legs,ref low);
                    }
                    string what="hulk "+l+" "+name;
                    Check(trunk<=.16f,what+": trunk and head inside the proposed capsule (worst "+trunk.ToString("0.000")+" m out, rig)");
                    Check(arms<=.42f,what+": fists within reach (worst "+arms.ToString("0.000")+")");Check(legs<=(speed==0?.09f:.45f),what+": legs inside, a stride at most ("+legs.ToString("0.000")+")");Check(low>=-.03f,what+": nothing under the floor ("+low+")");
                }
            }
            // The landing crouch and the walk through a breach stay inside it too.
            var model0=NativeHordeModels.Build(k,0,0);float tr=0,ar=0,lg=0,lo=9;var p=NativeHordeDrop.Hulk;float span=NativeHordeDrop.Duration(p.DefaultHeight,p);
            for(float a=NativeHordeDrop.LandAt(span,p);a<span;a+=.05f){NativeHordeGait.Drop(pose,3,a,span,p.DefaultHeight,p,out _,out _);Measure(k,model0.Mesh,pose,ref tr,ref ar,ref lg,ref lo);}
            Check(tr<=.16f&&lo>=-.03f,"the Hulk's landing crouch is inside its capsule and on the floor ("+tr.ToString("0.000")+", "+lo+")");
        }
        private static void Measure(int k,WeaponMesh mesh,CapturedRig.Pose pose,ref float trunk,ref float arms,ref float legs,ref float low)
        {
            if(mesh==null)return;
            foreach(var s in mesh.Sections)
            {
                int region=s.Bone<=CapturedRig.Head?0:s.Bone==CapturedRig.LClav||s.Bone==CapturedRig.RClav?1:s.Bone<=CapturedRig.RHand?2:3;var q=pose.WorldRotation[s.Bone];var o=pose.WorldPosition[s.Bone];
                foreach(var v in s.Positions)
                {
                    var w=o+Vector3.Transform(new Vector3(-v.X,v.Y,v.Z),q);w.Y+=NativeHordeKinds.Root[k];float r=NativeHordeKinds.Radius[k],h=NativeHordeKinds.Height[k];float y=Math.Max(r,Math.Min(h-r,w.Y));float d=Math.Max(0,new Vector3(w.X,w.Y-y,w.Z).Length()-r);low=Math.Min(low,w.Y);
                    if(region==0)trunk=Math.Max(trunk,d);else if(region==1)trunk=Math.Max(trunk,d-.15f);else if(region==2)arms=Math.Max(arms,d);else legs=Math.Max(legs,d);
                }
            }
        }
        // ---- the Hulk's jump-in ----
        private static void Drop()
        {
            var p=NativeHordeDrop.Profile(NativeHordeKinds.Hulk);var b=NativeHordeDrop.Profile(NativeHordeKinds.Brute);
            Check(p.Heavy&&!b.Heavy&&p.Warn>b.Warn&&p.SlamRadius>=2*b.SlamRadius&&p.ShakeRange>=2*b.ShakeRange&&p.ShakePeak>b.ShakePeak&&p.DefaultHeight>b.DefaultHeight,"the Hulk's drop: a longer warning, a far bigger slam and shake, a higher fall");
            Check(b.Warn==NativeHordeDrop.Warn&&b.Recover==NativeHordeDrop.Recover&&b.SlamRadius==NativeHordeDrop.SlamRadius&&NativeHordeDrop.Duration(6.5f)==NativeHordeDrop.Duration(6.5f,b),"the brute's drop is the original contract");
            float h=p.DefaultHeight,span=NativeHordeDrop.Duration(h,p),land=NativeHordeDrop.LandAt(span,p);var pose=new CapturedRig.Pose();
            Check(Math.Abs(span-(p.Warn+p.Leap+NativeHordeDrop.Fall(h,p)+p.Recover))<1e-5f&&Math.Abs(land-(span-p.Recover))<1e-6f,"its timeline: warn, leap, fall, recover");
            var prev=NativeHordeDrop.Phase.Warn;float rumble=0;
            for(float a=0;a<=span+.01f;a+=1/120f)
            {
                bool shown=NativeHordeGait.Drop(pose,4,a,span,h,p,out float lift,out var ph);Check(ph>=prev,"the phases in order");prev=ph;Check(shown==(ph!=NativeHordeDrop.Phase.Warn),"hidden only in the warning");
                if(a<land-1e-3f)Check(lift>0,"in the air until it lands");
                if(ph==NativeHordeDrop.Phase.Land)for(int j=0;j<2;j++)Check(Math.Abs(pose.WorldPosition[j==0?CapturedRig.LFoot:CapturedRig.RFoot].Y-(NativeHordeGait.Floor+NativeHordeGait.AnkleHeight))<.012f,"landed on its feet");
                float r=NativeHordeDrop.Rumble(a,span,h,p);Check(r>=0&&r<=1,"the rumble is bounded");if(ph==NativeHordeDrop.Phase.Warn)Check(r>=rumble-1e-5f,"the rumble builds to the leap");rumble=r;
                Check(NativeHordeDrop.Rumble(a,span,h,b)==0,"a brute's drop does not rumble");
            }
        }
        // ---- the breach ----
        private static NativeBreach.Wall TestWall(float yaw)
        {
            var rot=Quaternion.CreateFromAxisAngle(Vector3.UnitY,yaw);var normal=Vector3.Transform(Vector3.UnitZ,rot);var from=new Vector3(1,0,2)-normal*2;var exit=new Vector3(1,0,2)+normal*3;
            return NativeBreach.WallOf(new Vector3(1,2.1f,2),rot,new Vector3(6,4.2f,.5f),from,exit);
        }
        private static void Breach()
        {
            Check(NativeBreach.Kind=="breach"&&NativeBreach.ObjectName("north-1")=="breach:north-1"&&NativeBreach.Is(new PveEntry{kind="breach"})&&!NativeBreach.Is(new PveEntry{kind="drop"})&&!NativeBreach.Is(null),"the entry kind and the map's segment name");
            foreach(float yaw in new[]{0f,.7f,(float)Math.PI*.5f,2.6f})
            {
                var w=TestWall(yaw);var rot=Quaternion.CreateFromAxisAngle(Vector3.UnitY,yaw);
                Check(Math.Abs(w.Width-6)<1e-4f&&Math.Abs(w.Height-4.2f)<1e-4f&&Math.Abs(w.Thickness-.5f)<1e-4f,"the wall's width, height and thickness from the segment");
                Check(Vector3.Dot(w.Normal,Vector3.Transform(Vector3.UnitZ,rot))>.99f&&Math.Abs(Vector3.Dot(w.Right,w.Normal))<1e-5f&&Math.Abs(Vector3.Cross(w.Right,Vector3.UnitY).Length()-1)<1e-4f,"its normal points from behind it toward the entry's exit");
                var hole=NativeBreach.Build(w,w.Center,NativeHordeKinds.WorldRadius(NativeHordeKinds.Hulk),NativeHordeKinds.WorldHeight(NativeHordeKinds.Hulk),11);
                float hw=w.Width*.5f,hh=w.Height*.5f;
                Check(hole.HalfWidth*2>=NativeHordeKinds.WorldRadius(NativeHordeKinds.Hulk)*2&&hole.HalfHeight*2>=NativeHordeKinds.WorldHeight(NativeHordeKinds.Hulk)*.98f,"the hole is the Hulk's size");
                Check(hole.Center.Y-hole.HalfHeight<-hh+.4f&&NativeBreach.InHole(hole,hole.Center.X,-hh+.05f),"it opens onto the floor");
                for(int i=0;i<64;i++){float a=(float)(Math.PI*2*i/64);float r=hole.RadiusAlong(a);var pnt=hole.Center+new Vector2((float)Math.Cos(a),(float)Math.Sin(a))*r;Check(pnt.X>-hw+.1f&&pnt.X<hw-.1f&&pnt.Y>=-hh-1e-4f&&pnt.Y<hh-.1f,"the rag stays inside the panel");}
                Check(hole.Chunks>=10&&hole.Chunks<=NativeBreach.MaxChunks,"its pieces ("+hole.Chunks+")");
                for(int c=0;c<hole.Chunks;c++)
                {
                    var s=hole.Slot[c];Check(s.X>-hw&&s.X<hw&&s.Y>-hh&&s.Y<hh&&hole.Size[c].Z<w.Thickness,"piece "+c+"'s slot is in the panel, thinner than it");
                    NativeBreach.Piece(hole,c,0,out var at0,out _);Check(Vector3.Distance(at0,s)<1e-5f,"a piece starts in its slot");
                    NativeBreach.Piece(hole,c,2.6f,out var rest,out _);NativeBreach.Piece(hole,c,6,out var rest2,out _);
                    Check(rest.Z>.5f&&Math.Abs(rest.Y-(-hh+hole.Size[c].Y*.42f))<1e-3f&&Vector3.Distance(rest,rest2)<1e-4f,"thrown into the arena, it comes to rest on the floor");
                    for(float t=0;t<3;t+=.02f){NativeBreach.Piece(hole,c,t,out var at,out _);Check(at.Y>=-hh+hole.Size[c].Y*.42f-1e-4f,"never through the floor");}
                    float lastBack=0;for(float t=0;t<=NativeBreach.Rebuild;t+=.02f){NativeBreach.Restore(hole,c,t,out float back,out float solid);Check(back>=lastBack-1e-5f&&solid<=back+1e-5f,"the restore brings it home, then sets it");lastBack=back;}
                    NativeBreach.Restore(hole,c,NativeBreach.Rebuild,out float b1,out float s1);Check(b1>.999f&&s1>.999f,"home and solid by the rebuild's end");
                    NativeBreach.PieceAt(hole,c,NativeBreach.Phase.Rebuild,NativeBreach.Rebuild,10,out var home,out var spin,out float holo,out _);Check(Vector3.Distance(home,s)<1e-3f&&spin.Length()<1e-3f&&holo<1e-3f,"back in its place, square, no longer a hologram");
                }
                // The broken panel: finite, its triangles valid, all inside the wall, none across the hole.
                Check(hole.Positions.Length>0&&hole.Triangles.Length%3==0&&hole.Triangles.All(i=>i>=0&&i<hole.Positions.Length)&&hole.Positions.Length==hole.Normals.Length&&hole.Positions.Length==hole.Uvs.Length,"the panel mesh is well formed");
                Check(hole.Positions.All(v=>MathUtil.Finite(v.X)&&MathUtil.Finite(v.Y)&&MathUtil.Finite(v.Z)&&Math.Abs(v.X)<=hw+1e-4f&&Math.Abs(v.Y)<=hh+1e-4f&&Math.Abs(v.Z)<=w.Thickness*.5f+1e-4f),"the panel stays inside the wall's box");
                for(int t=0;t<hole.Triangles.Length;t+=3)
                {
                    var a=hole.Positions[hole.Triangles[t]];var bb=hole.Positions[hole.Triangles[t+1]];var c=hole.Positions[hole.Triangles[t+2]];var n=hole.Normals[hole.Triangles[t]];var m=(a+bb+c)/3;
                    // (The mesh's rag is straight between its points; the radius is read by angle: a face reaches no deeper than its chord.)
                    if(Math.Abs(n.Z)>.99f&&Vector3.Cross(bb-a,c-a).Length()>1e-6f){var d=new Vector2(m.X,m.Y)-hole.Center;Check(d.Length()>hole.RadiusAlong((float)Math.Atan2(d.Y,d.X))*.93f,"no face across the hole");}
                    var e=Vector3.Cross(bb-a,c-a);if(e.Length()>1e-6f)Check(Vector3.Dot(e,n)>0,"faces wound to their normal (front facing out)");
                }
            }
            // The timeline: the knocks, the break, the restore, whole again.
            Check(NativeBreach.BreakAt(10)==10-NativeBreach.Through&&NativeBreach.SolidAt(NativeBreach.DefaultRestoreAt(5))==5+NativeBreach.RestoreDelay+NativeBreach.Rebuild,"break, restore and solid times");
            float spawn=1,brk=spawn+NativeBreach.Knock,res=NativeBreach.DefaultRestoreAt(brk);var prev=NativeBreach.Phase.Intact;
            for(float t=0;t<res+NativeBreach.Rebuild+1;t+=.01f){var ph=NativeBreach.At(t,spawn,brk,res,out float into);Check(ph>=prev&&into>=0,"phases in order");prev=ph;}
            Check(NativeBreach.Blows.Last()==NativeBreach.Knock&&NativeBreach.Blows.Zip(NativeBreach.Blows.Skip(1),(a,b)=>a<b).All(x=>x),"the blows lead up to the break");
            foreach(float blow in NativeBreach.Blows){float a=NativeBreach.KnockAge(blow,.42f);Check(Math.Abs(a-.42f)<1e-5f,"each knock's impact lands on its blow");}
            Check(float.IsNaN(NativeBreach.KnockAge(.05f,.42f)),"no swing before the first knock");
            for(float t=0;t<NativeBreach.Knock;t+=.01f){Check(Math.Abs(NativeBreach.Shudder(t))<.05f,"the shudder is small");Check(NativeBreach.Cracked(t)>=NativeBreach.Cracked(Math.Max(0,t-.01f))-1e-5f,"cracks only spread");}
            var hl=NativeBreach.Build(TestWall(0),TestWall(0).Center,.95f,3.06f,3);float top=-9;for(float t=0;t<1.7f;t+=.01f)if(NativeBreach.Scan(hl,t,out float sy,out _))top=Math.Max(top,sy);Check(top>=hl.Center.Y+hl.HalfHeight,"the scan sweeps the whole hole");
            // No wall named in the map: the entry alone implies one across its line.
            var implied=NativeBreach.WallOf(new Vector3(0,0,-2),new Vector3(0,0,3),0,4.2f,4.2f,.5f);Check(Vector3.Dot(implied.Normal,Vector3.UnitZ)>.99f&&Math.Abs(implied.Bottom)<1e-5f,"the implied wall stands on the floor across the entry");
        }
        // ---- the jaws and the mouth ----
        private static void Jaws()
        {
            Check(NativeHordeJaw.Hinged(NativeHordeKinds.Spitter)&&NativeHordeJaw.Hinged(NativeHordeKinds.Hulk)&&!NativeHordeJaw.Hinged(NativeHordeKinds.Brute),"the spitter's and the Hulk's jaws are hinged");
            foreach(int k in new[]{NativeHordeKinds.Spitter,NativeHordeKinds.Hulk})
            {
                var m=NativeHordeModels.Build(k,0,0);m.Mesh.Bounds(CapturedRig.Head,out var hmin,out var hmax);m.Jaw.Bounds(CapturedRig.Head,out var jmin,out var jmax);
                var mouth=NativeHordeModels.MouthSocket(k);var hinge=NativeHordeModels.JawHinge(k);
                Check(mouth.Z>=hmax.Z-.05f&&mouth.Z<=hmax.Z+.06f&&mouth.Y>hmin.Y&&mouth.Y<hmin.Y+.2f&&Math.Abs(mouth.X)<1e-5f,NativeHordeKinds.Ids[k]+": the mouth socket is at the front of the face, at the mouth's height");
                Check(mouth.Z>hinge.Z&&jmax.Z>hinge.Z&&jmin.Y<hinge.Y+.02f,NativeHordeKinds.Ids[k]+": the jaw hangs in front of its hinge");
                // Opened, its front drops.
                float Front(float open){var mm=NativeHordeModels.JawMatrix(k,open);float y=9;foreach(var s in m.Jaw.Sections)foreach(var v in s.Positions){var p=Vector3.Transform(v,mm);if(v.Z>jmax.Z-.03f)y=Math.Min(y,p.Y);}return y;}
                Check(Front(.9f)<Front(0)-.02f,NativeHordeKinds.Ids[k]+": opening the jaw drops its front ("+(Front(0)-Front(.9f)).ToString("0.000")+" m)");
                Check(Vector3.Distance(Vector3.Transform(hinge,NativeHordeModels.JawMatrix(k,.7f)),hinge)<1e-5f,NativeHordeKinds.Ids[k]+": it turns about the hinge");
            }
            // The posed spitter's socket rides the head through the wind-up (the glob leaves from it, never the chest).
            var pose=new CapturedRig.Pose();var g=new NativeHordeGait();var st=NativeHordeKinds.ProposedRow(NativeHordeKinds.Spitter);
            var f=new HordeMotionInput{Kind=NativeHordeKinds.Spitter,Id=83000,Seed=NativeHordeKinds.Seed(83000),Health=1,AttackAge=float.NaN,Root=new Vector3(0,.85f,0),Attack=NativeHordeKinds.Attack.Spit,Strike=st.hit,Recover=st.cycle,Side=1};
            float rest=float.NaN,back=9;
            for(int i=0;i<=200;i++)
            {
                float t=i/60f;f.Time=t;f.AttackAge=t>=1?t-1:float.NaN;g.Pose(f,pose);if(t<1)continue;pose.World();
                var head=pose.WorldPosition[CapturedRig.Head];var local=NativeHordeModels.MouthSocket(NativeHordeKinds.Spitter);var mouth=head+Vector3.Transform(new Vector3(-local.X,local.Y,local.Z),pose.WorldRotation[CapturedRig.Head]);
                Check(mouth.Y>pose.WorldPosition[CapturedRig.Chest].Y+.2f,"the mouth is above the chest");
                float a=t-1;if(Math.Abs(a)<1e-4f)rest=head.Z;if(a<st.hit&&a>st.hit*.6f)back=Math.Min(back,head.Z);
            }
            Check(back<rest-.05f,"it rears back in the wind-up (the head from "+rest.ToString("0.000")+" back to "+back.ToString("0.000")+" m)");
        }
        // ---- the interrupt ----
        private static void Interrupt()
        {
            int sp=NativeHordeKinds.Spitter,br=NativeHordeKinds.Brute,hk=NativeHordeKinds.Hulk;
            Check(NativeHordeKinds.Interrupts(sp,NativeHordeKinds.Attack.Spit,1,45,false),"any damaging hit interrupts the spit");
            Check(!NativeHordeKinds.Interrupts(sp,NativeHordeKinds.Attack.Spit,0,45,false),"a hit for nothing does not");
            Check(!NativeHordeKinds.Interrupts(br,NativeHordeKinds.Attack.Smash,10,240,false)&&NativeHordeKinds.Interrupts(br,NativeHordeKinds.Attack.Smash,60,240,false)&&NativeHordeKinds.Interrupts(br,NativeHordeKinds.Attack.Smash,10,240,true),"other blows only by a stagger, as the authority's");
            Check(!NativeHordeKinds.Interrupts(hk,NativeHordeKinds.Attack.Smash,48,1600,true)&&NativeHordeKinds.Interrupts(hk,NativeHordeKinds.Attack.Smash,400,1600,false)&&!NativeHordeKinds.Staggers(hk,200,1600,true),"the Hulk staggers from big hits only");
            float S=NativeHordeKinds.SpitHit;float open=NativeHordeJaw.Open(sp,NativeHordeKinds.Attack.Spit,S*.6f,S,float.NaN,float.NaN,0,1),cut=NativeHordeJaw.Open(sp,NativeHordeKinds.Attack.Spit,S*.6f+.2f,S,.2f,float.NaN,0,1);
            Check(cut<open*.55f,"interrupted, the jaw snaps half shut");
            // The acid dribbles to the floor and fizzles there, small and short.
            float drop=1.4f;int landed=0;
            for(int i=0;i<NativeHordeFx.FizzleDrops;i++){bool was=false;for(float t=0;t<NativeHordeFx.FizzleSeconds;t+=.01f){bool now=NativeHordeFx.Fizzle(7,i,t,drop,out var o,out _,out _);if(now)Check(o.Y<=0&&o.Y>=-drop,"a drop falls from the mouth to the floor");if(was&&!now)landed++;was=now;}}
            Check(landed==NativeHordeFx.FizzleDrops,"every drop reaches the floor");
            Check(NativeHordeFx.FizzlePool(.6f,out float r,out _)&&r<=NativeHordeFx.FizzleRadius&&r<.4f&&!NativeHordeFx.FizzlePool(NativeHordeFx.FizzleSeconds+.01f,out _,out _),"the fizzle at its feet is small and gone in moments");
        }
        // ---- the effects: bounded, timed ----
        private static void Effects()
        {
            for(float t=-.1f;t<12;t+=.05f)
            {
                if(NativeHordeFx.Wave(t,5.5f,.8f,out float r,out float inner,out float a))Check(r<=6.01f&&a<=.95f&&inner<1,"the Hulk's shockwave is bounded");
                if(NativeHordeFx.Crater(t,1.8f,out float cr,out float ca))Check(cr<=1.8f&&ca<=.85f&&t<=NativeHordeFx.CraterSeconds,"the crater stays a while, then fades");
                for(int i=0;i<NativeHordeFx.DustWallCount;i++)if(NativeHordeFx.DustWall(3,i,t,5.5f,out var o,out float s,out float da))Check(t<=2.4f&&da<=.5f&&new Vector2(o.X,o.Z).Length()<=5.5f*1.1f+.31f,"the dust wall rolls out and clears");
                for(int i=0;i<NativeHordeFx.BigCracks;i++)if(NativeHordeFx.BigCrack(3,i,t,7,9,out _,out float len,out _))Check(len<=7*1.1f+1e-4f,"cracks run out to the radius");
                for(int i=0;i<NativeHordeFx.StompCracks;i++)if(NativeHordeFx.StompCrack(3,i,t,out _,out float sl,out _))Check(sl<.81f&&t<=NativeHordeFx.StompSeconds,"a footfall's cracks are short and fade");
            }
            // The puddle: a clear edge at the authority's radius, never past it; fumes and bubbles inside it.
            for(float t=0;t<4;t+=.05f)if(NativeHordeFx.Puddle(t,4,1.3f,out float pr,out _))Check(pr<=1.3f+1e-4f,"the puddle never spreads past its radius");
            for(int i=0;i<NativeHordeFx.Fumes;i++)for(float t=0;t<4;t+=.1f)if(NativeHordeFx.Fume(9,i,t,4,1.3f,out var o,out _,out _))Check(new Vector2(o.X,o.Z).Length()<=1.3f,"fumes rise from inside the puddle");
            // The trail follows the glob, drips fall from it.
            var from=new Vector3(0,1.4f,0);var to=new Vector3(0,0,6);float flight=NativeHordeFx.Flight(6);
            for(float t=0;t<flight;t+=.02f)for(int i=0;i<NativeHordeFx.TrailDots;i++)if(NativeHordeFx.GlobTrail(from,to,flight,t,i,out var p,out _,out _)){NativeHordeFx.Glob(from,to,flight,t,out var gp);Check(p.Z<=gp.Z+1e-4f,"the trail is behind the glob");}
            // The heavy shake: longer, rolled, and still nothing past its range.
            var slots=new NativeHordeFx.Shake[NativeHordeFx.ShakeSlots];NativeHordeFx.AddShake(slots,Vector3.Zero,1,.2f,34,4.5f,9,1.3f,3);
            float most=0,roll=0;for(int i=0;i<140;i++){var off=NativeHordeFx.ShakeAt(slots,new Vector3(5,1.6f,0),1+i*.01f,out _,out float rr);most=Math.Max(most,off.Length());roll=Math.Max(roll,Math.Abs(rr));}
            Check(most>0&&most<=.2f&&roll>0&&roll<=3,"the landing shake: bounded, rolled");Check(NativeHordeFx.ShakeAt(slots,new Vector3(40,1.6f,0),1.2f,out _,out _).Length()==0,"none past its 34 m");
            Check(NativeHordeFx.ShakeAt(slots,new Vector3(5,1.6f,0),1+1.31f,out _,out _).Length()==0,"over after 1.3 s");
            // The roar: the same seeded moments for every peer, never through a blow.
            int roars=0;for(float t=0;t<60;t+=.05f){float a=NativeHordeRoar.Age(NativeHordeKinds.Hulk,77,t,0,float.NaN,3);if(!float.IsNaN(a)&&a<.05f)roars++;Check(float.IsNaN(NativeHordeRoar.Age(NativeHordeKinds.Hulk,77,t,0,.5f,3)),"no roar through a blow");Check(float.IsNaN(NativeHordeRoar.Age(NativeHordeKinds.Brute,77,t,0,float.NaN,3)),"only the Hulk roars");}
            Check(roars>=3&&roars<=6,"it roars every 12-16 s ("+roars+" in a minute)");
        }
        // ---- determinism ----
        private static void Determinism()
        {
            byte[] Run()
            {
                var pose=new CapturedRig.Pose();var g=new NativeHordeGait();var ms=new MemoryStream();var bw=new BinaryWriter(ms);int k=NativeHordeKinds.Hulk;var st=NativeHordeKinds.ProposedRow(k);
                var f=new HordeMotionInput{Kind=k,Id=84000,Seed=NativeHordeKinds.Seed(84000),Health=1,AttackAge=float.NaN,Attack=NativeHordeKinds.Attack.Smash,Strike=st.hit,Recover=st.cycle,Side=1};
                for(int i=0;i<600;i++){float t=i/60f;f.Time=t;f.Root=new Vector3(0,.85f,t*.4f);f.Velocity=new Vector3(0,0,.4f);f.AttackAge=t>5&&t<8?t-5:float.NaN;f.RoarAge=NativeHordeRoar.Age(k,f.Seed,t,0,f.AttackAge,st.cycle);f.Roaring=!float.IsNaN(f.RoarAge);g.Pose(f,pose);
                    for(int b=0;b<22;b++){bw.Write(pose.WorldPosition[b].X);bw.Write(pose.WorldPosition[b].Y);bw.Write(pose.WorldPosition[b].Z);}}
                var h=NativeBreach.Build(TestWall(.3f),TestWall(.3f).Center,.95f,3.06f,21);foreach(var v in h.Positions){bw.Write(v.X);bw.Write(v.Y);bw.Write(v.Z);}foreach(var i in h.Triangles)bw.Write(i);
                for(int c=0;c<h.Chunks;c++){NativeBreach.Piece(h,c,1.1f,out var at,out var spin);bw.Write(at.X);bw.Write(at.Y);bw.Write(at.Z);bw.Write(spin.X);}
                return ms.ToArray();
            }
            var a=Run();var b=Run();Check(a.Length>0&&a.SequenceEqual(b),"the Hulk's poses and a breach's hole and pieces are the same on every run (every peer)");
        }
        // ---- no allocation ----
        private static void Allocation()
        {
            var pose=new CapturedRig.Pose();var g=new NativeHordeGait();int k=NativeHordeKinds.Hulk;var st=NativeHordeKinds.ProposedRow(k);var hole=NativeBreach.Build(TestWall(0),TestWall(0).Center,.95f,3.06f,5);var p=NativeHordeDrop.Hulk;
            var slots=new NativeHordeFx.Shake[NativeHordeFx.ShakeSlots];var sg=new NativeHordeGait();var sst=NativeHordeKinds.ProposedRow(NativeHordeKinds.Spitter);
            void Frame(int i)
            {
                float t=i/60f;var f=new HordeMotionInput{Kind=k,Id=85000,Seed=NativeHordeKinds.Seed(85000),Time=t,Root=new Vector3(0,.85f,t*.4f),Velocity=new Vector3(0,0,.4f),Health=1,AttackAge=(i%240)<200?(i%240)/60f:float.NaN,Attack=NativeHordeKinds.Attack.Smash,Strike=st.hit,Recover=st.cycle,Side=1};
                f.RoarAge=NativeHordeRoar.Age(k,f.Seed,t,0,f.AttackAge,st.cycle);f.Roaring=!float.IsNaN(f.RoarAge);g.Pose(f,pose);
                var s=new HordeMotionInput{Kind=NativeHordeKinds.Spitter,Id=85001,Seed=NativeHordeKinds.Seed(85001),Time=t,Root=new Vector3(2,.85f,0),Health=1,AttackAge=(i%180)/60f,Attack=NativeHordeKinds.Attack.Spit,Strike=sst.hit,Recover=sst.cycle,Side=1};sg.Pose(s,pose);
                NativeHordeGait.Drop(pose,3,t%5,NativeHordeDrop.Duration(9,p),9,p,out _,out _);NativeHordeDrop.Rumble(t%5,4.5f,9,p);
                var ph=NativeBreach.At(t%20,1,3,15,out float into);for(int c=0;c<hole.Chunks;c++)NativeBreach.PieceAt(hole,c,ph,into,t%20-3,out _,out _,out _,out _);NativeBreach.Scan(hole,t%3,out _,out _);NativeBreach.Shudder(t%2);NativeBreach.KnockAge(t%3,.42f);
                for(int c=0;c<NativeBreach.CrackLines;c++)NativeBreach.Crack(hole,c,NativeBreach.Cracked(t%2),out _,out _,out _);NativeBreach.InHole(hole,t%1,0);
                NativeHordeJaw.Open(NativeHordeKinds.Spitter,NativeHordeKinds.Attack.Spit,t%1,.8f,float.NaN,float.NaN,t,3);NativeHordeJaw.Open(k,NativeHordeKinds.Attack.Smash,t%3,1.1f,float.NaN,t%2,t,3);
                NativeHordeFx.AddShake(slots,Vector3.Zero,t,.03f,14,0,(uint)(i/20),.3f,0);NativeHordeFx.ShakeAt(slots,Vector3.One,t,out _,out _);NativeHordeFx.Wave(t%1,5,.8f,out _,out _,out _);NativeHordeFx.Crater(t%9,2,out _,out _);
                for(int c=0;c<NativeHordeFx.DustWallCount;c++)NativeHordeFx.DustWall(3,c,t%2.4f,5.5f,out _,out _,out _);for(int c=0;c<NativeHordeFx.DroolDrops;c++)NativeHordeFx.Drool(3,c,t%1,.8f,out _,out _,out _);
                for(int c=0;c<NativeHordeFx.FizzleDrops;c++)NativeHordeFx.Fizzle(3,c,t%1.4f,1.2f,out _,out _,out _);for(int c=0;c<NativeHordeFx.Pits;c++)NativeHordeFx.Pit(3,c,t%5,4,.3f,out _,out _,out _);
                for(int c=0;c<NativeHordeFx.TrailDots;c++)NativeHordeFx.GlobTrail(Vector3.UnitY,Vector3.UnitZ*6,.8f,t%.8f,c,out _,out _,out _);
            }
            for(int i=0;i<300;i++)Frame(i);long before=GC.GetAllocatedBytesForCurrentThread();for(int i=300;i<1500;i++)Frame(i);long used=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(used==0,"no allocation over 1200 frames of the Hulk's gait, roar and drop, the spit, a breach's pieces and hologram, the jaws and the effects ("+used+" bytes)");
        }
        // ---- the runtime: draws only, data driven ----
        private static void Runtime(string root)
        {
            var kinds=R(root,"Assets/ChipArena/Runtime/NativeHordeKindPresentation.cs");var breach=R(root,"Assets/ChipArena/Runtime/NativeHordeBreachPresentation.cs");var acid=R(root,"Assets/ChipArena/Runtime/NativeHordeAcidPresentation.cs");var zombie=R(root,"Assets/ChipArena/Runtime/NativeHordeZombiePresentation.cs");var gallery=R(root,"Assets/ChipArena/Runtime/NativeHordeHulkGallery.cs");
            Check(kinds.Contains("NativeBreach.Is(a.Entry)")&&kinds.Contains("NativeArena.Vec(a.Entry.from.Position(0)),NativeArena.Vec(a.Entry.@exit.Position(0))")&&breach.Contains("foreach(var prop in arena.AllBlocks)if(prop!=null&&prop.gameObject.name==NativeBreach.ObjectName(entry.id))"),"a breach reads its entry and the map's named segment");
            Check(kinds.Contains("rig.Root.transform.localScale=Vector3.one*sc;")&&kinds.Contains("Root=new NVector(pos.x,ground+NativeHordeKinds.DrawRoot(kind,a.Crawl),pos.z)*inv"),"a big kind is its rig at its scale, the gait in the rig's metres");
            Check(kinds.Contains("NativeHordeKinds.Interrupts(kind,attack,e.Amount,m.MaxHp,heavy)")&&kinds.Contains("if(attack==NativeHordeKinds.Attack.Spit)HordeFizzle(")&&kinds.Contains("if(!float.IsNaN(age)&&s.CancelFor==attackAt&&now>=s.CancelAt){age=float.NaN;cancelled=true;}"),"a hit in the spit's wind-up cancels it (no glob), fizzling");
            Check(kinds.Contains("HordeLaunchGlob(mouth,to,")&&kinds.Contains("Vector3 mouth=j.Mouth")&&kinds.Contains("Mouth=s.Mouth"),"the glob leaves from the mouth socket");
            Check(gallery.Contains("HordeGalleryHulk")&&gallery.Contains("HordeGalleryBreach")&&kinds.Contains("HordeGalleryHulk(me,now,across,yawToMe,ground)"),"the Dev Sandbox gallery previews the Hulk, its jump-in and a breach");
            foreach(var (name,text) in new[]{("breach",breach),("acid",acid),("zombie",zombie),("gallery",gallery)})
            {
                foreach(var bad in new[]{".Hp=",".Health=","Damage(","Emit(","StaggerAt=","LastAttack=","linearVelocity=","AddComponent<","Collider>().enabled","isTrigger","Body.position="})Check(!text.Contains(bad),name+": never writes `"+bad+"`");
            }
            Check(!Regex.IsMatch(zombie,@"\.transform\.(position|rotation|localPosition|localScale)\s*=")&&!zombie.Contains("SetPositionAndRotation"),"a jolted prop is only drawn jolted (it never moves)");
            Check(!Regex.IsMatch(breach,@"Destroy\("),"the breach never destroys the map's wall");
            // Integration reads complete entry points, not individual numeric literals (a map coordinate may
            // legitimately equal gravity or an effect duration).
            Check(breach.Contains("entry.from.Position()")&&breach.Contains("entry.@exit.Position()")&&breach.Contains("frame.restoreAt"),"breach frame uses authored endpoints and replicated restoration");
            Check(R(root,"Assets/ChipArena/Core/NativeHordeDrop.cs").Contains("e.height"),"drop uses authored height");
            foreach(var f in new[]{"NativeBreach.cs","NativeHordeRoar.cs"}){var t=R(root,"Assets/ChipArena/Core/"+f);Check(!t.Contains("UnityEngine")&&!t.Contains("NativeArena")&&!t.Contains("new Random("),f+" is engine free and clock free");}
        }
    }
}
#endif
