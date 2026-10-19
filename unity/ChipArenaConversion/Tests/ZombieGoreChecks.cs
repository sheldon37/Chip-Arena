#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The horde's gore and the end of the Siren treatment on the zombies (owner 2026-10-19: "Lets fix this animated lines thing on the
    // zombies ... I want them to be more "standard" zombies, that produce gore when shot / gibbed, not just black flames";
    // docs/ZOMBIE-GORE.md). Presentation only. Holds:
    //  - no line or flame treatment on any horde kind: no glow cell flickers or glows on the Siren's seven but the eyes and their
    //    own acid and boils (all in the kind's colour, steady), no kind grows a tongue, no lick, haze, char or burn-away death, ink
    //    spray or black-flame gore is drawn, the zombie shader has no rising flow and no lavender rim, the materials no flow;
    //  - gore lands where the shot hit and goes the way it went: the wound on the nearest part, on its surface facing the shooter,
    //    its spray out of the wound and along the shot, the bits and mist off it, a head shot harder;
    //  - caps and cleanup hold in a 60-add horde dying over half a minute on each tier, and everything is gone after its life;
    //  - the same hits and deaths make the same gore on the host, a guest (other frames, other arrival order within a frame) and
    //    the killcam's replay;
    //  - the tiers draw less (corpses, life, wounds, bits, chunks, mist, gibs, drops);
    //  - nothing allocates per frame;
    //  - presentation only: the gore files write no authority state, and no authority, network or rules file reads them.
    internal static class ZombieGoreChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Zombie gore: "+what);count++;}
        public static void Run(string root)
        {
            Treatment(root,out string models);Placement(out string place);Caps(out string caps);Determinism(out string same);Tiers(out string tiers);Allocation();Source(root);
            Console.WriteLine("PASS zombie gore ("+count+" checks: "+models+"; "+place+"; "+caps+"; "+same+"; "+tiers+"; no allocation; presentation only)");
        }

        // ---- a posed zombie's joints (System.Numerics, row vectors: what the runtime hands the gore from its rig) ----
        private static Matrix4x4[] Bones(CapturedRig.Pose p,Vector3 root,float yawDeg,float scale)
        {
            var world=new Matrix4x4[22];var rootM=Matrix4x4.CreateScale(scale)*Matrix4x4.CreateFromQuaternion(Quaternion.CreateFromAxisAngle(Vector3.UnitY,yawDeg*(float)Math.PI/180))*Matrix4x4.CreateTranslation(root);
            for(int i=0;i<22;i++)
            {
                int parent=CapturedRig.Bones[i].Parent;var local=Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(CapturedRig.Mirror(p.Rotation[i])));var lp=CapturedRig.Mirror(p.Position[i]);local.M41=lp.X;local.M42=lp.Y;local.M43=lp.Z;
                world[i]=local*(parent<0?rootM:world[parent]);
            }
            return world;
        }
        private static Matrix4x4[] Posed(int kind,int id,Vector3 at,float yawDeg,float t=1)
        {
            var pose=new CapturedRig.Pose();var gait=new NativeHordeGait();var st=NativeHordeKinds.ProposedRow(kind);float sc=NativeHordeKinds.Scale[kind];
            var root=new Vector3(at.X,at.Y+NativeHordeKinds.DrawRoot(kind,false),at.Z);
            var f=new HordeMotionInput{Kind=kind,Id=id,Seed=NativeHordeKinds.Seed(id),Time=t,Yaw=yawDeg,Ground=at.Y/sc,Root=root/sc,Health=1,AttackAge=float.NaN,Strike=NativeHordeKinds.Strike(st,false,0),Recover=NativeHordeKinds.Recover(st,false,0),Attack=NativeHordeKinds.Default[kind],Side=1};
            gait.Pose(f,pose,new NativeHordeReact());return Bones(pose,root,gait.Yaw,sc);
        }

        // ---- no line, flame or burn-away treatment left ----
        private static void Treatment(string root,out string summary)
        {
            int glowing=0,models=0;
            for(int k=0;k<NativeHordeKinds.Count;k++)for(int l=0;l<NativeHordeKinds.Looks[k];l++)for(int t=0;t<NativeHordeModels.Tiers;t++)
            {
                var m=NativeHordeModels.Build(k,l,t);models++;var seam=WeaponPaint.Linear(NativeHordeKinds.Seam[k]);
                foreach(var mesh in m.Jaw!=null?new[]{m.Mesh,m.Jaw}:new[]{m.Mesh})foreach(var s in mesh.Sections)for(int i=0;i<s.Emission.Count;i++)
                {
                    var e=s.Emission[i];Check(e.W<2.5f,NativeHordeKinds.Ids[k]+" look "+l+" "+NativeHordeModels.TierNames[t]+": no flickering glow cell (glow code "+e.W+") on joint "+s.Bone);
                    if(e.X+e.Y+e.Z<1e-4f)continue;glowing++;
                    // What still glows is the eyes, the spitter's acid and the bloater's boils: in the kind's colour, on the head, neck or gut.
                    var n=Vector3.Normalize(new Vector3(e.X,e.Y,e.Z));var c=Vector3.Normalize(seam);
                    Check(Vector3.Dot(n,c)>.995f,NativeHordeKinds.Ids[k]+": a glowing vertex on joint "+s.Bone+" is the kind's own colour (eyes, acid, boils), no Siren line");
                    bool where=s.Bone==CapturedRig.Head||k==NativeHordeKinds.Spitter&&s.Bone==CapturedRig.Neck||k==NativeHordeKinds.Bloater&&s.Bone==CapturedRig.Spine;
                    Check(where,NativeHordeKinds.Ids[k]+" look "+l+": glow only on the head (eyes), the spitter's sac and the bloater's gut (joint "+s.Bone+")");
                }
            }
            for(int k=0;k<NativeHordeKinds.All;k++)for(int l=0;l<NativeHordeKinds.Looks[k];l++)Check(NativeHordeModels.Tongues(k,l).Length==0&&!NativeHordeModels.TongueMatrix(k,l,0,1,1,0,new double[16],out _,out _),NativeHordeKinds.Ids[k]+" grows no black-flame tongue");
            Check(NativeHordeModels.MostTongues==0,"no kind grows tongues");
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
            var fx=R("Runtime/NativeMinionFxPresentation.cs");
            foreach(var bad in new[]{"NativeMinionFx.Lick(","TongueMatrix(","QueueHordeTongue","NativeMinionFx.Haze(","NativeMinionFx.Effects(","FlameCube(","LickRange"})Check(!fx.Contains(bad),"the minion effects draw no tongue, lick, haze or black-flame emergence or burn-away (`"+bad+"`)");
            Check(fx.Contains("partTint:burn?hordeDeathTint:null")&&fx.Contains("if(!hordeDeathBurn)return m;"),"a dying body chars and burns away only on the Cinderling's ember crumble");
            Check(fx.Contains("NativeMinionFx.Hint(")&&fx.Contains("HordeGrin(camera,")&&fx.Contains("HordeTrail(id,possessedAt"),"the Siren's possession hint stays (seam, halo, grin, trail)");
            var kinds=R("Runtime/NativeHordeKindModels.cs");
            Check(kinds.Contains("HordeInkFlow=0;")&&kinds.Contains("material.SetFloat(MinionGlowId,HordeFlickers(kind)?1:0)")&&kinds.Contains("public static bool HordeFlickers(int kind)=>kind>=NativeHordeKinds.Count;"),"no rising ink flow on any kind; the Siren's seven never flicker (the zone kinds keep their own heat)");
            var shader=R("Resources/NativeZombieLit.shader");int emitLine=shader.IndexOf("o.Emission+=_Ink*",StringComparison.Ordinal);
            Check(emitLine>0&&!shader.Contains("rise=sin(")&&!shader.Contains("float3(.62,.56,.82)")&&!shader.Substring(emitLine,200).Contains("_InkFlow"),"ZombieLit: no rising flow lines, a neutral rim instead of the Siren's lavender");
            var kp=R("Runtime/NativeHordeKindPresentation.cs");
            Check(kp.Contains("HordeGoreHit(arena,m,s,e,at,dir,melee,spec,")&&!kp.Contains(",kill,true);")&&kp.Contains("HordeDeathBody(rig,model,death.id,death.lost,fx,hordeQueueVisible,burn)"),"a hit bleeds red through the gore (no Siren ink), a death is a corpse (no char)");
            var gore=R("Runtime/NativeGorePresentation.cs");Check(!gore.Contains("NativeGoreFlames."),"the PvE gore throws no black flames (hit cubes, gib puffs, stump drips)");
            Check(!R("Runtime/NativeHordeGorePresentation.cs").Contains("ink:true"),"the horde's blood is red (no Siren ink)");
            summary=models+" models of the seven with "+glowing+" glowing vertices, all eyes, acid or boils in the kind's colour; no tongue, lick, haze, ink or burn-away";
        }

        // ---- gore lands at the hit and goes along the shot ----
        private static void Placement(out string summary)
        {
            var st=new NativeHordeGoreState();int checks=0,aimed=0,onAim=0;float worstGap=0;
            var aims=new[]{(CapturedRig.Head,"head"),(CapturedRig.Chest,"chest"),(CapturedRig.LElbow,"left forearm"),(CapturedRig.RKnee,"right shin"),(CapturedRig.Spine,"gut")};
            foreach(int kind in new[]{NativeHordeKinds.Shambler,NativeHordeKinds.Runner,NativeHordeKinds.Brute,NativeHordeKinds.Hulk})
            for(int yaw=0;yaw<360;yaw+=90)
            {
                st.Clear();int id=70000+kind*10+yaw/90;var at=new Vector3(3,0,-2);var bones=Posed(kind,id,at,yaw);float scale=NativeHordeKinds.Scale[kind];
                foreach(var (bone,name) in aims)
                {
                    // The shooter 6 m in front of the add's facing, a little high.
                    float r=yaw*(float)Math.PI/180;var fwd=new Vector3((float)Math.Sin(r),0,(float)Math.Cos(r));NativeHordeGore.Segment(bones,bone,out var a,out var b);var part=(a+b)*.5f;
                    // The shot's ray from the shooter through the aimed part: the authority's hit point is where it first meets the body.
                    var shooter=new Vector3(at.X,1.5f*scale,at.Z)+fwd*6;var dir=Vector3.Normalize(part-shooter);if(!First(bones,kind,shooter,dir,out var point,out int first))continue;
                    float time=1+bone*.1f;var h=new GoreHit{Id=id,Kind=kind,Time=time,Amount=20,MaxHp=60,Cause=NativeHordeGore.Cause.Bullet,Point=point,Dir=dir};
                    int sprays=st.SprayCount;int hit=st.Hit(h,bones,at.Y,out bool head);
                    Check(Inside(bones,kind,hit,point)<.025f*scale,NativeHordeKinds.Ids[kind]+" yaw "+yaw+": the "+name+" shot wounds the part it struck (joint "+hit+", first "+first+")");
                    if(first==bone||CapturedRig.Bones[first].Parent==bone||CapturedRig.Bones[bone].Parent==first)onAim++;aimed++;
                    if(first==CapturedRig.Head)Check(head,NativeHordeKinds.Ids[kind]+": a shot into the head is a head shot");else if(first!=CapturedRig.Neck)Check(!head,NativeHordeKinds.Ids[kind]+": a shot into the "+name+" is no head shot");
                    var g=st.Find(id,false);var w=g.W[g.Count-1];var wp=Vector3.Transform(w.P,bones[w.Bone]);var wn=Vector3.Normalize(Vector3.TransformNormal(w.N,bones[w.Bone]));
                    float gap=Vector3.Distance(wp,point);worstGap=Math.Max(worstGap,gap);
                    Check(gap<.06f*scale,NativeHordeKinds.Ids[kind]+": the "+name+" wound sits at the hit ("+gap.ToString("0.000")+" m)");
                    Check(Vector3.Dot(wn,-dir)>.15f,NativeHordeKinds.Ids[kind]+" yaw "+yaw+": the "+name+" wound faces the shooter ("+Vector3.Dot(wn,-dir).ToString("0.00")+")");
                    Check(st.SprayCount==sprays+1,"one spray per hit");var sp=st.Sprays[st.SprayCount-1];
                    Check(Vector3.Distance(sp.Origin,wp)<.03f&&Vector3.Dot(sp.Along,dir)>.99f&&Vector3.Dot(sp.Normal,wn)>.99f,"the spray leaves the wound, out of it and along the shot");
                    Check(sp.Kind==(head?"cavity":"puncture")&&sp.Target==id,"a head shot sprays a cavity, a body shot a puncture");
                    // The blood sim: the drops start at the wound; the exit cone carries on along the shot.
                    var blood=new NativeBloodSim();int n=blood.Spray(sp.Kind,sp.Severity,sp.Seed,sp.Key,sp.Origin,sp.Normal,sp.Along,sp.Time,1,false,false,sp.Extra);Vector3 mean=Vector3.Zero;int live=0;
                    foreach(var d in blood.Drops){if(!d.Live)continue;live++;Check(Vector3.Distance(d.Position,sp.Origin)<1e-4f,"a drop leaves the wound");mean+=d.Velocity;}
                    Check(live==n&&n>0&&Vector3.Dot(mean,dir)>0,"the blood flies on along the shot ("+n+" drops)");
                    // The bits and the mist come off the wound.
                    ref var burst=ref st.Bursts[Array.FindLastIndex(st.Bursts,x=>x.Live&&x.Born==time)];Check(Vector3.Distance(burst.At,wp)<1e-3f,"the burst is at the wound");
                    for(int k=0;k<burst.Bits;k++){NativeHordeGore.Bit(burst.Seed,k,0,burst.At,burst.Dir,burst.Power,at.Y,false,out var b0,out _,out _,out _);Check(Vector3.Distance(b0,burst.At)<1e-4f,"a bit leaves the wound");}
                    if(NativeHordeGore.MistPuff(burst.Seed,0,.1f,burst.At,burst.N,burst.Dir,burst.Power,out var mp,out _,out _,out _))Check(Vector3.Dot(mp-burst.At,-dir)>0,"the entry's mist blows back toward the shooter");
                    checks++;
                }
            }
            Check(onAim>=aimed*9/10,"nine in ten shots aimed at a part strike it first ("+onAim+"/"+aimed+")");
            // A head shot sprays harder than a body shot of the same damage.
            Check(NativeHordeGore.Extra(NativeHordeGore.Cause.Bullet,true,false,NativeHordeGore.Full)>NativeHordeGore.Extra(NativeHordeGore.Cause.Bullet,false,false,NativeHordeGore.Full)&&NativeHordeGore.Severity(NativeHordeGore.Cause.Bullet,true,20,60)>NativeHordeGore.Severity(NativeHordeGore.Cause.Bullet,false,20,60)&&NativeBloodRules.Count("cavity",1,1,false)>NativeBloodRules.Count("puncture",1,1,false),"a head shot sprays harder");
            // A blade's gash runs along its sweep; a blast's wound faces the blast.
            {
                st.Clear();var bones=Posed(NativeHordeKinds.Shambler,70900,Vector3.Zero,0);var sweep=Vector3.Normalize(new Vector3(1,.4f,0));
                st.Hit(new GoreHit{Id=70900,Kind=0,Time=2,Amount=30,MaxHp=60,Cause=NativeHordeGore.Cause.Melee,Point=new Vector3(0,1.2f,.5f),Dir=new Vector3(0,0,-1),Sweep=sweep},bones,0,out _);
                var w=st.Find(70900,false).W[0];var t=Vector3.Normalize(Vector3.TransformNormal(w.T,bones[w.Bone]));Check(Math.Abs(Vector3.Dot(t,Vector3.Normalize(sweep-Vector3.Dot(sweep,Vector3.Normalize(Vector3.TransformNormal(w.N,bones[w.Bone])))*Vector3.Normalize(Vector3.TransformNormal(w.N,bones[w.Bone])))))>.95f,"a blade's gash runs along its sweep");
                st.Hit(new GoreHit{Id=70900,Kind=0,Time=3,Amount=50,MaxHp=60,Cause=NativeHordeGore.Cause.Blast,Point=new Vector3(2,.1f,0),Dir=new Vector3(-1,0,0)},bones,0,out _);
                var bw=st.Find(70900,false).W[1];var bn=Vector3.TransformNormal(bw.N,bones[bw.Bone]);Check(bn.X>.2f,"a blast's wound is on the side facing the blast");
            }
            // The authority's head shot (EventKind.Headshot, when it sends one for adds) puts the wound on the head wherever the point is.
            {
                st.Clear();var bones=Posed(NativeHordeKinds.Shambler,71400,Vector3.Zero,0);
                int j=st.Hit(new GoreHit{Id=71400,Kind=0,Time=1,Amount=20,MaxHp=60,Cause=NativeHordeGore.Cause.Bullet,Head=true,Point=new Vector3(0,1.1f,.3f),Dir=-Vector3.UnitZ},bones,0,out bool hd);
                Check(j==CapturedRig.Head&&hd&&st.Sprays[st.SprayCount-1].Kind=="cavity","a replicated head shot wounds the head and sprays a cavity");
            }
            // A cut's raw stub stands out of the cut on the part it hung from (the head's on the neck, out along the neck, as wide as
            // the kind's neck), so it shows over the part's own end; a thrown head's cap is its neck end, facing down out of it.
            foreach(int kind in new[]{NativeHordeKinds.Shambler,NativeHordeKinds.Brute,NativeHordeKinds.Hulk})
            {
                st.Clear();var bones=Posed(kind,71500+kind,Vector3.Zero,0);float sc=NativeHordeKinds.Scale[kind];st.Stump(71500+kind,bones,CapturedRig.Head,1,Vector3.UnitZ,kind);
                var w=st.Find(71500+kind,false).W[0];Check(w.Stump&&w.Bone==CapturedRig.Neck&&Math.Abs(w.Size-NativeHordeGore.CutRadius(kind,CapturedRig.Neck))<1e-6f,NativeHordeKinds.Ids[kind]+": the head's stump is on the neck, the kind's neck wide");
                st.Begin();st.DrawWounds(71500+kind,bones,1.2f);var at=NativeHordeGore.Origin(bones[CapturedRig.Head]);var up=Vector3.Normalize(at-NativeHordeGore.Origin(bones[CapturedRig.Neck]));float z=w.Size*sc;
                Check(st.BlockCount>=5,"the stub is drawn ("+st.BlockCount+" voxels)");
                for(int i=0;i<st.BlockCount;i++){float d=Vector3.Dot(st.Blocks[i].At-at,up);Check(d>-.01f*sc&&d<z*1.2f,NativeHordeKinds.Ids[kind]+": the stub stands out of the cut along the neck ("+(d/z).ToString("0.00")+" of its radius)");}
            }
            Check(NativeHordeGore.CutOut(CapturedRig.Head).Y<0&&NativeHordeGore.CutOut(CapturedRig.LElbow).Y>0&&NativeHordeGore.PartCut(0,CapturedRig.Head)==NativeHordeGore.CutRadius(0,CapturedRig.Neck),"a thrown head's cap is its neck end, facing down; a limb's faces up out of its root");
            summary=checks+" shots on four kinds at four facings wound the part they struck ("+onAim+" the part aimed at; worst "+(worstGap*100).ToString("0.0")+" cm from the hit), face the shooter and spray along the shot";
        }
        // A part's signed distance from `p` (its capsule, the kind's girth), and the ray's first contact with the body.
        private static float Inside(Matrix4x4[] bones,int kind,int j,Vector3 p)
        {
            NativeHordeGore.Segment(bones,j,out var a,out var b);float s=NativeHordeGore.ScaleOf(bones[CapturedRig.Body]);
            return Vector3.Distance(NativeHordeGore.Closest(a,b,p),p)-NativeHordeGore.SegmentRadius[j]*NativeHordeGore.Girth(kind,j>=CapturedRig.Pelvis&&j<=CapturedRig.Chest)*s;
        }
        private static bool First(Matrix4x4[] bones,int kind,Vector3 from,Vector3 dir,out Vector3 point,out int joint)
        {
            for(float t=0;t<12;t+=.004f){var p=from+dir*t;for(int j=1;j<22;j++)if(Inside(bones,kind,j,p)<=0){point=p;joint=j;return true;}}
            point=from;joint=-1;return false;
        }

        // ---- caps and cleanup in a big horde ----
        private static void Caps(out string summary)
        {
            var notes=new List<string>();
            for(int tier=0;tier<3;tier++)
            {
                var st=new NativeHordeGoreState{Tier=tier};var bones=Posed(NativeHordeKinds.Shambler,71000,Vector3.Zero,0);int mostSteady=0,mostLive=0,mostBlocks=0,gibs=0;
                for(int i=0;i<60;i++)
                {
                    float at=i*.5f;int id=72000+i;st.Expire(at);
                    for(int h=0;h<12;h++)st.Hit(new GoreHit{Id=id,Kind=0,Index=h,Time=at-.3f+h*.01f,Amount=6,MaxHp=60,Cause=NativeHordeGore.Cause.Bullet,Point=new Vector3((h%3-1)*.2f,.6f+h*.08f,.5f),Dir=new Vector3(0,0,-1)},bones,0,out _);
                    Check(st.Find(id,false).Count<=NativeHordeGore.WoundMax,"an add keeps at most "+NativeHordeGore.WoundMax+" wounds");st.Sprayed();
                    var fate=st.Fate(0,at,i%4==0?NativeHordeGore.Cause.Blast:NativeHordeGore.Cause.Bullet,false,60,60,.5f,false);if(NativeHordeGore.Gibs(fate))gibs++;
                    var r=st.Die(id,0,at,NativeHordeGore.DeathSeed(id,i),fate);r.PoolMost=.6f;r.ChunkAt=new Vector3(0,1,0);
                    if(r.Body){for(int b=0;b<22;b++)r.Bones[b]=bones[b];st.Pose(r,i);}
                    mostSteady=Math.Max(mostSteady,st.SteadyRemains(at));mostLive=Math.Max(mostLive,st.LiveRemains);
                    st.Begin();st.Collect(at+.2f,Vector3.Zero);mostBlocks=Math.Max(mostBlocks,st.BlockCount);
                }
                Check(mostSteady<=NativeHordeGore.CorpseCap[tier],"tier "+tier+": at most "+NativeHordeGore.CorpseCap[tier]+" corpses lie at once ("+mostSteady+")");
                Check(mostLive<=NativeHordeGore.RemainsMax,"tier "+tier+": the remains stay in their fixed pool ("+mostLive+")");
                Check(mostBlocks<NativeHordeGore.BlockMax,"tier "+tier+": a frame's voxels stay under the pool's size ("+mostBlocks+")");
                Check(gibs<=NativeHordeGore.GibCap[tier]*((int)(30/NativeHordeGore.GibWindow)+1),"tier "+tier+": the gibs keep their cap ("+gibs+")");
                // A burst of blast kills inside a second gibs only the cap's count.
                var st2=new NativeHordeGoreState{Tier=tier};int burst=0;for(int i=0;i<20;i++)if(NativeHordeGore.Gibs(st2.Fate(0,100+i*.05f,NativeHordeGore.Cause.Blast,false,60,60,.5f,false)))burst++;
                Check(burst==NativeHordeGore.GibCap[tier],"tier "+tier+": twenty blast kills in a second make "+NativeHordeGore.GibCap[tier]+" gibs ("+burst+"), the rest corpses");
                // Everything is gone after its life and the sink.
                float end=30+NativeHordeGore.CorpseLife[tier]+NativeHordeGore.SinkSeconds+.1f;st.Expire(end);st.Begin();st.Collect(end,Vector3.Zero);
                Check(st.LiveRemains==0&&st.BlockCount==0&&st.PoolCount==0&&st.MistCount==0,"tier "+tier+": every corpse, chunk, pool and bit is gone after its life ("+st.LiveRemains+" left)");
                notes.Add(mostSteady+"/"+NativeHordeGore.CorpseCap[tier]);
            }
            // The sink: whole until the life ends, then down and dithered away.
            NativeHordeGore.Sink(5,10,out float s0,out float a0);NativeHordeGore.Sink(10+NativeHordeGore.SinkSeconds,10,out float s1,out float a1);
            Check(s0==0&&a0==1&&Math.Abs(s1-NativeHordeGore.SinkDepth)<1e-5f&&a1==0&&NativeHordeGore.Gone(10+NativeHordeGore.SinkSeconds,10),"a corpse lies whole, then sinks and fades over "+NativeHordeGore.SinkSeconds+" s");
            summary="60 kills over 30 s keep "+string.Join(", ",notes)+" corpses on the Full, Reduced and Low tiers, all gone after their life";
        }

        // ---- the same gore on every peer ----
        private sealed class Feed{public float At;public GoreHit Hit;public bool Death;public int Id;public NativeHordeGore.Cause Cause;}
        private static void Determinism(out string summary)
        {
            var bones=Posed(NativeHordeKinds.Brute,73000,new Vector3(1,0,1),30);var feed=new List<Feed>();uint s=12345;float R(){s=s*1664525+1013904223;return (s>>8)/16777216f;}
            for(int i=0;i<40;i++){int id=73000+i%6;float at=i*.23f;var c=i%7==0?NativeHordeGore.Cause.Melee:i%11==0?NativeHordeGore.Cause.Blast:NativeHordeGore.Cause.Bullet;
                feed.Add(new Feed{At=at,Hit=new GoreHit{Id=id,Kind=2,Time=at,Amount=10+30*R(),MaxHp=240,Cause=c,Point=new Vector3(1+R()-.5f,.4f+1.2f*R(),1.6f),Dir=Vector3.Normalize(new Vector3(R()-.5f,-.1f,-1)),Sweep=new Vector3(1,.3f,0)}});}
            for(int i=0;i<6;i++)feed.Add(new Feed{At=6+i*.4f,Death=true,Id=73000+i,Cause=i%2==0?NativeHordeGore.Cause.Blast:NativeHordeGore.Cause.Bullet});
            feed=feed.OrderBy(f=>f.At).ToList();
            // The host sees every event the frame it happens (60 fps); a guest in 20 Hz batches, each batch in arrival order; the
            // killcam rewinds (Clear) and replays the whole window.
            NativeHordeGoreState Run(float step,bool rewind)
            {
                var st=new NativeHordeGoreState();int next=0;
                for(float now=0;now<10;now+=step)
                {
                    if(rewind&&now>=4&&now<4+step){st.Clear();next=0;}
                    while(next<feed.Count&&feed[next].At<=now){var f=feed[next++];if(f.Death){var fate=st.Fate(2,f.At,f.Cause,false,120,240,.6f,false);var r=st.Die(f.Id,2,f.At,NativeHordeGore.DeathSeed(f.Id,next),fate);r.PoolMost=NativeHordeGore.PoolMost(2,fate,st.Wounds(f.Id),1);r.ChunkAt=new Vector3(1,1,1);r.Push=Vector3.UnitZ;if(r.Body){for(int b=0;b<22;b++)r.Bones[b]=bones[b];st.Pose(r,next);}}else st.Hit(f.Hit,bones,0,out _);}
                    st.Sprayed();
                }
                return st;
            }
            var host=Run(1/60f,false);var guest=Run(1/20f,false);var killcam=Run(1/30f,true);int frames=0;
            foreach(float now in new[]{2.5f,6.3f,7.1f,8.9f})
            {
                foreach(var st in new[]{host,guest,killcam}){st.Begin();st.Collect(now,Vector3.Zero);for(int i=0;i<6;i++)st.DrawWounds(73000+i,bones,now);}
                Check(host.BlockCount>0&&host.BlockCount==guest.BlockCount&&host.BlockCount==killcam.BlockCount,"the same voxels at "+now+" s on the host, a guest and the killcam ("+host.BlockCount+", "+guest.BlockCount+", "+killcam.BlockCount+")");
                for(int i=0;i<host.BlockCount;i++){var a=host.Blocks[i];foreach(var o in new[]{guest,killcam}){var b=o.Blocks[i];Check(a.At==b.At&&a.Size==b.Size&&a.Turn==b.Turn&&a.Albedo==b.Albedo,"voxel "+i+" is the same everywhere");}}
                Check(host.MistCount==guest.MistCount&&host.PoolCount==guest.PoolCount&&host.PoolCount==killcam.PoolCount,"the same mist and pools");
                for(int i=0;i<host.PoolCount;i++)Check(host.Pools[i].At==guest.Pools[i].At&&host.Pools[i].Radius==killcam.Pools[i].Radius,"pool "+i+" is the same everywhere");
                frames++;
            }
            summary="40 hits and 6 deaths draw identically on the host (60 fps), a guest (20 Hz batches) and the killcam (rewound) at "+frames+" moments";
        }

        // ---- the tiers draw less ----
        private static void Tiers(out string summary)
        {
            var counts=new int[3];var bones=Posed(NativeHordeKinds.Shambler,74000,Vector3.Zero,0);
            for(int tier=0;tier<3;tier++)
            {
                var st=new NativeHordeGoreState{Tier=tier};
                for(int i=0;i<8;i++){int id=74000+i;for(int h=0;h<6;h++)st.Hit(new GoreHit{Id=id,Kind=0,Index=h,Time=1+h*.05f,Amount=12,MaxHp=60,Cause=h==5?NativeHordeGore.Cause.Heavy:NativeHordeGore.Cause.Bullet,Point=new Vector3(0,.5f+h*.2f,.5f),Dir=new Vector3(0,0,-1)},bones,0,out _);}
                var r=st.Die(74999,0,1,7,NativeHordeGore.Fate.Gib);r.ChunkAt=new Vector3(0,1,0);r.Push=Vector3.UnitZ;
                st.Begin();st.Collect(1.4f,Vector3.Zero);for(int i=0;i<8;i++)st.DrawWounds(74000+i,bones,1.4f);counts[tier]=st.BlockCount+st.MistCount;
            }
            Check(counts[0]>counts[1]&&counts[1]>counts[2],"Reduced effects draw less than Full, the low tier less still ("+string.Join(", ",counts)+" voxels and puffs)");
            for(int t=1;t<3;t++)Check(NativeHordeGore.CorpseCap[t]<NativeHordeGore.CorpseCap[t-1]&&NativeHordeGore.CorpseLife[t]<NativeHordeGore.CorpseLife[t-1]&&NativeHordeGore.WoundsShown[t]<NativeHordeGore.WoundsShown[t-1]&&NativeHordeGore.Bits[t]<NativeHordeGore.Bits[t-1]&&NativeHordeGore.Chunks[t]<NativeHordeGore.Chunks[t-1]&&NativeHordeGore.GibCap[t]<NativeHordeGore.GibCap[t-1]&&NativeHordeGore.WoundAdds[t]<NativeHordeGore.WoundAdds[t-1],"tier "+t+" keeps fewer corpses for less time, fewer wounds, bits, chunks and gibs");
            Check(NativeBloodRules.Count("puncture",.6f,.3f,false)<NativeBloodRules.Count("puncture",.6f,1,false)&&NativeBloodRules.Count("puncture",.6f,1,true)<NativeBloodRules.Count("puncture",.6f,1,false),"the blood sim sprays fewer drops on Reduced effects and the low tier");
            // Gore off: no wound, no gib, corpses only.
            var off=new NativeHordeGoreState{Enabled=false};Check(off.Hit(new GoreHit{Id=1,Kind=0,Time=1,Amount=60,MaxHp=60,Cause=NativeHordeGore.Cause.Bullet,Point=new Vector3(0,1,.5f),Dir=-Vector3.UnitZ},bones,0,out _)<0&&off.Fate(0,1,NativeHordeGore.Cause.Blast,false,60,60,.2f,false)==NativeHordeGore.Fate.Corpse,"gore off: no wound, no gib");
            summary="Full "+counts[0]+", Reduced "+counts[1]+", Low "+counts[2]+" voxels and puffs for the same hits";
        }

        // ---- nothing allocates per frame ----
        private static void Allocation()
        {
            var st=new NativeHordeGoreState();var bones=Posed(NativeHordeKinds.Shambler,75000,Vector3.Zero,0);var blood=new NativeBloodSim();
            void Frame(int i)
            {
                float now=i/60f;int id=75000+(i/90)%20;st.Expire(now);
                if(i%9==0)st.Hit(new GoreHit{Id=id,Kind=0,Index=0,Time=now,Amount=10,MaxHp=60,Cause=i%27==0?NativeHordeGore.Cause.Melee:NativeHordeGore.Cause.Bullet,Point=new Vector3(0,.4f+(i%5)*.25f,.5f),Dir=-Vector3.UnitZ,Sweep=Vector3.UnitX},bones,0,out _);
                if(i%90==89){var r=st.Die(id,0,now,NativeHordeGore.DeathSeed(id,i),st.Fate(0,now,i%180==89?NativeHordeGore.Cause.Blast:NativeHordeGore.Cause.Bullet,false,60,60,.5f,false));r.PoolMost=.5f;if(r.Body){for(int b=0;b<22;b++)r.Bones[b]=bones[b];st.Pose(r,i);}}
                st.Begin();st.Collect(now,Vector3.Zero);st.DrawWounds(id,bones,now);st.Drip(id,bones,now);st.Cap(Vector3.UnitY,Vector3.UnitY,.05f,now%2,7);
                for(int k=0;k<st.SprayCount;k++){var s=st.Sprays[k];blood.Spray(s.Kind,s.Severity,s.Seed,s.Key,s.Origin,s.Normal,s.Along,s.Time,1,false,false,s.Extra,s.Target,s.Kill);}st.Sprayed();
                NativeHordeGore.VentDust(7,i%10,now%2,out _,out _,out _,out _);NativeHordeGore.FountainPower(i%20);
            }
            for(int i=0;i<600;i++)Frame(i);long before=GC.GetAllocatedBytesForCurrentThread();for(int i=600;i<3600;i++)Frame(i);long used=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(used==0,"hits, deaths, corpses, wounds, drips, chunks, pools and sprays allocate nothing per frame ("+used+" bytes over 3000 frames)");
        }

        /// <summary>A source's code without its comment lines.</summary>
        private static string Code(string s)=>string.Join("\n",s.Split('\n').Where(l=>!l.TrimStart().StartsWith("//",StringComparison.Ordinal)));
        // ---- presentation only ----
        private static void Source(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
            var core=R("Core/NativeHordeGore.cs");
            Check(!core.Contains("UnityEngine")&&!core.Contains("System.Linq")&&!core.Contains("new Random(")&&!core.Contains("DateTime"),"the gore rules are pure Core (no engine, no LINQ, no clock-free randomness)");
            foreach(var f in new[]{"Core/NativeHordeGore.cs","Runtime/NativeHordeGorePresentation.cs","Runtime/NativeHordeGoreGallery.cs"})
            {
                var s=R(f);foreach(var bad in new[]{".Hp=",".Hp-=","Damage(","Emit(","StaggerAt=","RecoverAt=","LastAttack=","Body.position=","linearVelocity=",".Lost=a","Collider.","isKinematic=","Pve.Lost|=","SendFrame","Network"})Check(!s.Contains(bad),f+" never writes authority or network state (`"+bad+"`)");
            }
            // No authority, rules or network file reads the gore; every file that does is presentation.
            var dir=Path.Combine(root,"Assets/ChipArena");var readers=new List<string>();
            foreach(var f in Directory.GetFiles(dir,"*.cs",SearchOption.AllDirectories)){var s=Code(File.ReadAllText(f));if(s.Contains("NativeHordeGore")||s.Contains("HordeGore"))readers.Add(Path.GetFileName(f));}
            var allowed=new HashSet<string>{"NativeHordeGore.cs","NativeHordeGorePresentation.cs","NativeHordeGoreGallery.cs","NativeHordeKindPresentation.cs","NativeMinionFxPresentation.cs","NativeIronworksKindsPresentation.cs","NativeBowPresentation.cs"};
            foreach(var f in readers)Check(allowed.Contains(f),"only presentation files use the gore ("+f+")");
            foreach(var f in new[]{"Runtime/NativePveHorde.cs","Runtime/NativeArena.cs","Runtime/NativeNetwork.cs","Runtime/NativeNetworkFrame.cs","Runtime/NativeDeaths.cs","Runtime/NativeSummons.cs","Core/NativeHordeDamage.cs","Core/NativeHordeKinds.cs","Core/ArenaModel.cs"})
                if(File.Exists(Path.Combine(dir,f)))Check(!Code(R(f)).Contains("HordeGore"),f+" (authority, rules or network) never reads the gore");
            // The runtime hands the gore only replicated facts and draws through the shared renderers.
            var p=R("Runtime/NativeHordeGorePresentation.cs");
            Check(p.Contains("NativeHordeGore.Classify(e.Detail,spec,melee,e.DamageKind)")&&p.Contains("Time=e.Time,Amount=e.Amount")&&p.Contains("vfx.Block(")&&p.Contains("blood.Spray(s.Kind,s.Severity,s.Seed,s.Key,s.Origin,s.Normal,s.Along,s.Time,BloodQuality,LowGraphics,stain,s.Extra,s.Target,s.Kill)"),"the gore reads the replicated hit and draws through the shared renderers and the blood sim");
            Check(p.Contains("var r=hordeGore.RemainsOf(id);if(r==null)return false;")&&p.Contains("&&!NativeHordeGoreState.Fading(r,hordeClock))return true;"),"a falling body whose remains the cap already sank never pops back; one the cap sinks mid-fall sinks from where it is");
            Check(R("Runtime/NativeHordeKindPresentation.cs").Contains("public const int HordePartMax=48;")&&R("Runtime/NativeHordeKindPresentation.cs").Contains("NativeHordeGore.Gone(age,life)"),"thrown parts are a fixed pool and go with the corpses' life");
        }
    }
}
#endif
