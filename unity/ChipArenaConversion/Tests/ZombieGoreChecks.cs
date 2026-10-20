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
    //  - presentation only: the gore files write no authority state, and no authority, network or rules file reads them;
    //  - the authority's horde-hit data (Astra 2026-10-20: ArenaEvent HasHordeHit, Head, Raw, HasPart, Part) binds: its head flag
    //    wins, the wound goes on its part (a lost part's on the stump or the trunk), its raw damage drives the gib, the head burst
    //    and the spray; an old frame without it draws bit for bit as before.
    internal static class ZombieGoreChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Zombie gore: "+what);count++;}
        public static void Run(string root)
        {
            Treatment(root,out string models);Placement(out string place);Caps(out string caps);Determinism(out string same);Tiers(out string tiers);Allocation();Source(root);
            Fallback(out string old);Metadata(root,out string meta);OnBody(root,out string body);Conversion(out string joints);
            Console.WriteLine("PASS zombie gore ("+count+" checks: "+models+"; "+place+"; "+caps+"; "+same+"; "+tiers+"; no allocation; presentation only; "+old+"; "+meta+"; "+body+"; "+joints+")");
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
        private static Matrix4x4[] Posed(int kind,int id,Vector3 at,float yawDeg,float t=1,float speed=0)
        {
            var pose=new CapturedRig.Pose();var gait=new NativeHordeGait();var st=NativeHordeKinds.ProposedRow(kind);float sc=NativeHordeKinds.Scale[kind];
            var root=new Vector3(at.X,at.Y+NativeHordeKinds.DrawRoot(kind,false),at.Z);float yr=yawDeg*(float)Math.PI/180;var v=new Vector3((float)Math.Sin(yr),0,(float)Math.Cos(yr))*speed;
            var f=new HordeMotionInput{Kind=kind,Id=id,Seed=NativeHordeKinds.Seed(id),Time=t,Yaw=yawDeg,Ground=at.Y/sc,Root=root/sc,Velocity=v/sc,Health=1,AttackAge=float.NaN,Strike=NativeHordeKinds.Strike(st,false,0),Recover=NativeHordeKinds.Recover(st,false,0),Attack=NativeHordeKinds.Default[kind],Side=1};
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
            var st=new NativeHordeGoreState();int checks=0,aimed=0,onAim=0,beside=0;float worstGap=0,worstOn=0,leastFacing=1;
            var aims=new[]{(CapturedRig.Head,"head"),(CapturedRig.Chest,"chest"),(CapturedRig.LElbow,"left forearm"),(CapturedRig.RKnee,"right shin"),(CapturedRig.Spine,"gut")};
            foreach(int kind in new[]{NativeHordeKinds.Shambler,NativeHordeKinds.Runner,NativeHordeKinds.Brute,NativeHordeKinds.Hulk})
            for(int yaw=0;yaw<360;yaw+=90)
            {
                st.Clear();int id=70000+kind*10+yaw/90;var at=new Vector3(3,0,-2);var bones=Posed(kind,id,at,yaw);float scale=NativeHordeKinds.Scale[kind];var drawn=new Drawn(kind,0,id,bones);
                foreach(var (bone,name) in aims)
                {
                    // The shooter 6 m in front of the add's facing, a little high, aiming at the part's middle. The authority's hit point
                    // is where that ray meets its capsule (the hit volume); the shot's own travel rides the hit (ArenaEvent.Normal).
                    float r=yaw*(float)Math.PI/180;var fwd=new Vector3((float)Math.Sin(r),0,(float)Math.Cos(r));NativeHordeGore.Segment(bones,bone,out var a,out var b);var part=(a+b)*.5f;
                    var shooter=new Vector3(at.X,1.5f*scale,at.Z)+fwd*6;var dir=Vector3.Normalize(part-shooter);if(!Authority(kind,at,shooter,dir,out var point))continue;
                    bool entered=drawn.Ray(shooter,dir,14,-1,out float te,out int first,out _);var entry=shooter+dir*te;
                    float time=1+bone*.1f;var h=new GoreHit{Id=id,Kind=kind,Time=time,Amount=20,MaxHp=60,Cause=NativeHordeGore.Cause.Bullet,Point=point,Dir=dir,Shot=dir};
                    int sprays=st.SprayCount;int hit=st.Hit(h,bones,at.Y,out bool head);
                    var g=st.Find(id,false);var w=g.W[g.Count-1];var wp=Vector3.Transform(w.P,bones[w.Bone]);var wn=Vector3.Normalize(Vector3.TransformNormal(w.N,bones[w.Bone]));
                    // On the drawn part, where the shot enters it (the authority's capsule is not the body: wider than a thin kind,
                    // narrower than a reaching arm); a shot that only grazed the capsule goes on the nearest drawn part.
                    float on=drawn.Off(hit,wp);worstOn=Math.Max(worstOn,on);Check(on<.004f*scale,NativeHordeKinds.Ids[kind]+" yaw "+yaw+": the "+name+" wound is on the drawn part ("+on.ToString("0.000")+" m off joint "+hit+")");
                    if(entered)
                    {
                        Check(hit==first,NativeHordeKinds.Ids[kind]+" yaw "+yaw+": the "+name+" shot wounds the drawn part it enters first (joint "+hit+", first "+first+")");
                        float gap=Vector3.Distance(wp,entry);worstGap=Math.Max(worstGap,gap);Check(gap<.005f*scale,NativeHordeKinds.Ids[kind]+": the "+name+" wound sits where the shot enters the drawn body ("+gap.ToString("0.000")+" m)");
                        if(first==bone||CapturedRig.Bones[first].Parent==bone||CapturedRig.Bones[bone].Parent==first)onAim++;aimed++;
                        if(first==CapturedRig.Head)Check(head,NativeHordeKinds.Ids[kind]+": a shot into the head is a head shot");else if(first!=CapturedRig.Neck)Check(!head,NativeHordeKinds.Ids[kind]+": a shot into the "+name+" is no head shot");
                    }
                    else beside++;
                    // It lies on the surface the shot struck, the side facing the shooter.
                    float facing=Vector3.Dot(wn,-dir);leastFacing=Math.Min(leastFacing,facing);Check(facing>0,NativeHordeKinds.Ids[kind]+" yaw "+yaw+": the "+name+" wound faces the shooter ("+facing.ToString("0.00")+")");
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
            summary=checks+" shots on four kinds at four facings wound the drawn part they enter ("+onAim+" of "+aimed+" the part aimed at; worst "+(worstGap*100).ToString("0.0")+" cm from the entry, "+(worstOn*100).ToString("0.0")+" cm off the part; "+beside+" grazing the capsule on the nearest part), face the shooter (least "+leastFacing.ToString("0.00")+") and spray along the shot";
        }
        // ---- the drawn body: the kind's model on the posed joints as the runtime draws it ----
        // Each part on its joint's world matrix (sourceModels.Queue: bone(part.Bone)), the add's proportions and missing pieces as
        // ZombieLit's vert draws them (written out here from Resources/NativeZombieLit.shader ZombieVert, not from the gore's own
        // NativeHordeGoreBody), the hinged jaw closed. Every triangle faces the way its vertex normals do.
        private static readonly Dictionary<(int,int,int),HordeModel> built=new Dictionary<(int,int,int),HordeModel>();
        private static HordeModel Model(int kind,int look,int tier){lock(built){if(!built.TryGetValue((kind,look,tier),out var m))built[(kind,look,tier)]=m=NativeHordeModels.Build(kind,look,tier);return m;}}
        private sealed class Drawn
        {
            public readonly List<Vector3>[] Tri=new List<Vector3>[22];// a, b, c per triangle, world
            public Drawn(int kind,int look,int id,Matrix4x4[] bones,int tier=NativeHordeModels.Full)
            {
                var model=Model(kind,look,tier);var body=NativeHordeModels.Varies(NativeHordeModels.Id(kind,look,tier))?NativeHordeModels.Variation(id):Vector4.One;
                for(int j=0;j<22;j++)Tri[j]=new List<Vector3>();
                foreach(var mesh in model.Jaw!=null?new[]{model.Mesh,model.Jaw}:new[]{model.Mesh})foreach(var s in mesh.Sections)
                {
                    if(s.Bone<=0||s.Bone>=22)continue;var m=bones[s.Bone];
                    Vector3 V(int i,out bool gone){var v=Vert(s.Positions[i],i<s.Colors.Count?s.Colors[i].W:1,i<s.Data.Count?s.Data[i].Y:0,body,out gone);return Vector3.Transform(v,m);}
                    for(int i=0;i+2<s.Indices.Count;i+=3)
                    {
                        int ia=s.Indices[i],ib=s.Indices[i+1],ic=s.Indices[i+2];var a=V(ia,out bool gone);if(gone)continue;var b=V(ib,out _);var c=V(ic,out _);
                        var nn=Vector3.TransformNormal(s.Normals[ia]+s.Normals[ib]+s.Normals[ic],m);if(Vector3.Dot(Vector3.Cross(b-a,c-a),nn)<0){var x=b;b=c;c=x;}
                        Tri[s.Bone].Add(a);Tri[s.Bone].Add(b);Tri[s.Bone].Add(c);
                    }
                }
            }
            // ZombieVert: the proportions by region about the joint, a missing piece folded to the joint.
            private static Vector3 Vert(Vector3 p,float region,float data,Vector4 body,out bool gone)
            {
                var pr=NativeHordeModels.Proportions(body.Y-1);gone=false;
                if(region>.62f&&region<.88f)p*=pr.X;else if(region>.37f&&region<.63f)p.X*=pr.Y;else if(region>.12f&&region<.38f){p.X*=pr.Z;p.Z*=pr.Z;}
                float variant=(float)Math.Floor(data),mask=(float)Math.Floor((body.Z-1)*16+.5f);
                if(variant>.5f&&variant<4.5f){float bit=(float)Math.Pow(2,variant-1),half=(float)Math.Floor(mask/bit)*.5f;gone=half-(float)Math.Floor(half)>.25f;}
                return p;
            }
            /// <summary>The first outward face a ray enters (joints in `mask`): its parameter, joint and normal.</summary>
            public bool Ray(Vector3 o,Vector3 d,float most,int mask,out float t,out int joint,out Vector3 normal)
            {
                t=most;joint=-1;normal=Vector3.Zero;
                for(int j=1;j<22;j++)
                {
                    if((mask&1<<j)==0)continue;var l=Tri[j];
                    for(int i=0;i<l.Count;i+=3)
                    {
                        var a=l[i];var e1=l[i+1]-a;var e2=l[i+2]-a;var f=Vector3.Cross(e1,e2);if(!(Vector3.Dot(f,d)<0))continue;
                        var pv=Vector3.Cross(d,e2);float det=Vector3.Dot(e1,pv);if(Math.Abs(det)<1e-12f)continue;var tv=o-a;float u=Vector3.Dot(tv,pv)/det;if(u<0||u>1)continue;
                        var qv=Vector3.Cross(tv,e1);float v=Vector3.Dot(d,qv)/det;if(v<0||u+v>1)continue;float tt=Vector3.Dot(e2,qv)/det;if(tt>=0&&tt<t){t=tt;joint=j;normal=Vector3.Normalize(f);}
                    }
                }
                return joint>0;
            }
            /// <summary>A point's distance from joint `joint`'s drawn part (-1: the whole body).</summary>
            public float Off(int joint,Vector3 p)
            {
                float best=float.MaxValue;
                for(int j=joint<0?1:joint;j<=(joint<0?21:joint);j++)
                {
                    var l=Tri[j];
                    for(int i=0;i<l.Count;i+=3)
                    {
                        var a=l[i];var b=l[i+1];var c=l[i+2];
                        // A box round the triangle first.
                        float bx=Math.Max(0,Math.Max(Math.Min(a.X,Math.Min(b.X,c.X))-p.X,p.X-Math.Max(a.X,Math.Max(b.X,c.X)))),by=Math.Max(0,Math.Max(Math.Min(a.Y,Math.Min(b.Y,c.Y))-p.Y,p.Y-Math.Max(a.Y,Math.Max(b.Y,c.Y)))),bz=Math.Max(0,Math.Max(Math.Min(a.Z,Math.Min(b.Z,c.Z))-p.Z,p.Z-Math.Max(a.Z,Math.Max(b.Z,c.Z))));
                        if(bx*bx+by*by+bz*bz>=best)continue;float d=Vector3.DistanceSquared(NativeHordeGoreBody.ClosestOnTriangle(p,a,b,c),p);if(d<best)best=d;
                    }
                }
                return (float)Math.Sqrt(best);
            }
            /// <summary>How far a drawn voxel reaches off joint `joint`'s part: the farthest of its eight corners.</summary>
            public float Off(int joint,in GoreBlock b){float worst=0;for(int k=0;k<8;k++){var c=new Vector3((k&1)==0?-.5f:.5f,(k&2)==0?-.5f:.5f,(k&4)==0?-.5f:.5f)*b.Size;worst=Math.Max(worst,Off(joint,b.At+Vector3.Transform(c,b.Turn)));}return worst;}
        }
        /// <summary>The authority's hit point: where a ray meets the add's capsule (its hit volume, upright round the body).</summary>
        private static bool Authority(int kind,Vector3 ground,Vector3 from,Vector3 dir,out Vector3 point)
        {
            float r=NativeHordeKinds.WorldRadius(kind),h=NativeHordeKinds.WorldHeight(kind),half=Math.Max(0,h*.5f-r);var c=new Vector3(ground.X,ground.Y+NativeHordeKinds.RootHeight(kind,false),ground.Z);
            for(float t=0;t<14;t+=.002f){var p=from+dir*t;if(Vector3.Distance(NativeHordeGore.Closest(c-Vector3.UnitY*half,c+Vector3.UnitY*half,p),p)<=r){point=p;return true;}}
            point=from;return false;
        }
        /// <summary>A ray's first contact with the drawn body (add `id`'s look 0 of `kind`).</summary>
        private static bool First(Matrix4x4[] bones,int kind,int id,Vector3 from,Vector3 dir,out Vector3 point,out int joint)
        {
            bool hit=new Drawn(kind,0,id,bones).Ray(from,dir,14,-1,out float t,out joint,out _);point=from+dir*t;return hit;
        }

        // ---- every drawn wound piece on the drawn body (owner 2026-10-20: "Zombie wounds appear off their body (Happens to other
        //      zombies as well)") ----
        // The wounds had gone on a capsule round each joint (the Standard pilot's radii, a per-kind girth, the drawn scale), not on
        // the drawn model: on the big kinds' heads the capsule is near twice the head, and every wound on a part thinner or fatter
        // than its capsule floated or sank. Their blood run was one straight strip laid across the shot (the wound frame's b axis),
        // up to 5.6 times the wound's size, standing off a curved part, so neighbouring runs crossed as X's beside the body. Now
        // the wound is where the shot enters the drawn model, its footprint fitted to the part and its run traced down the part's
        // surface. Measured here against the drawn body (Drawn: the model posed as the runtime draws it, the add's own look), on
        // every kind and look, standing and walking (drawn on the pose 1.6 and 4.5 s after the hit), each gore tier, five causes.
        private static void OnBody(string root,out string summary)
        {
            var causes=new[]{NativeHordeGore.Cause.Bullet,NativeHordeGore.Cause.Heavy,NativeHordeGore.Cause.Melee,NativeHordeGore.Cause.Blast,NativeHordeGore.Cause.Arrow};
            var aims=new[]{CapturedRig.Head,CapturedRig.Neck,CapturedRig.Chest,CapturedRig.Spine,CapturedRig.Pelvis,CapturedRig.LArm,CapturedRig.RElbow,CapturedRig.LHip,CapturedRig.RKnee};
            var core=new List<float>();var run=new List<float>();var drop=new List<float>();var centre=new List<float>();var downs=new List<float>();var acrossOld=new List<float>();
            float worstSeven=0,worstZone=0,worstAll=0;int hits=0,blocks=0,moving=0;var tierWorst=new float[3];string worstWhat="";
            for(int kind=0;kind<NativeHordeKinds.All;kind++)for(int look=0;look<NativeHordeKinds.Looks[kind];look++)for(int yi=0;yi<2;yi++)for(int walk=0;walk<2;walk++)
            {
                int id=81000+kind*40+look*8+yi*2+walk;float sc=NativeHordeKinds.Scale[kind],yaw=(yi==0?20:200)+kind*7,speed=walk==1?1.3f:0;var at=new Vector3(1,0,2);
                float yr=yaw*(float)Math.PI/180;var fwd=new Vector3((float)Math.Sin(yr),0,(float)Math.Cos(yr));var side=new Vector3(fwd.Z,0,-fwd.X);
                var bones=Posed(kind,id,at,yaw,1,speed);var drawn=new Drawn(kind,look,id,bones);var body=new Vector3(at.X,at.Y+NativeHordeKinds.RootHeight(kind,false),at.Z);
                for(int ai=0;ai<aims.Length;ai++)
                {
                    var cause=causes[(ai+look+yi+walk+kind)%causes.Length];var st=new NativeHordeGoreState{Tier=(ai+kind+walk)%3};
                    NativeHordeGore.Segment(bones,aims[ai],out var a0,out var a1);var aim=(a0+a1)*.5f;Vector3 point,dir,shot=Vector3.Zero,sweep=Vector3.Zero;
                    if(cause==NativeHordeGore.Cause.Blast){point=body+fwd*1.3f*sc-Vector3.UnitY*.6f*sc;dir=-fwd;}
                    else
                    {
                        var from=cause==NativeHordeGore.Cause.Melee?body+fwd*1.4f*sc+side*.3f:new Vector3(at.X,1.55f*sc,at.Z)+fwd*6+side*(ai%3-1)*1.1f;
                        var ray=Vector3.Normalize(aim-from);if(!Authority(kind,at,from,ray,out point))continue;
                        dir=Vector3.Normalize(new Vector3(ray.X,0,ray.Z));if(cause==NativeHordeGore.Cause.Melee)sweep=Vector3.Normalize(Vector3.Cross(Vector3.UnitY,dir)+Vector3.UnitY*(ai%2==0?.35f:-.4f));else shot=ray;
                    }
                    int j=st.Hit(new GoreHit{Id=id,Kind=kind,Look=look,Time=1,Amount=25,MaxHp=80,Cause=cause,Point=point,Dir=dir,Shot=shot,Sweep=sweep},bones,at.Y,out _);
                    Check(j>0,"kind "+kind+": a hit wounds a drawn part");if(j<=0)continue;hits++;if(walk==1)moving++;
                    var w=st.Find(id,false).W[0];var wp=Vector3.Transform(w.P,bones[j]);float cd=drawn.Off(j,wp);centre.Add(cd);
                    Check(cd<.004f*sc,NativeHordeKinds.Ids[kind]+" look "+look+" joint "+j+" "+cause+": the wound's centre is on the drawn part ("+cd.ToString("0.000")+" m)");
                    // The run goes down the part's surface where the wound was struck (the strip it replaced lay across the shot).
                    for(int k=0;k+1<w.RunN;k++)
                    {
                        var ra=Vector3.Transform(w.Run(k),bones[j]);var rb=Vector3.Transform(w.Run(k+1),bones[j]);var rn=Vector3.Normalize(Vector3.TransformNormal(w.RunNormal(k),bones[j]));
                        var fall=-Vector3.UnitY-rn*Vector3.Dot(-Vector3.UnitY,rn);if(fall.Length()<.3f)continue;downs.Add(Vector3.Dot(Vector3.Normalize(rb-ra),Vector3.Normalize(fall)));
                        if(k==0){var wn=Vector3.Normalize(Vector3.TransformNormal(w.N,bones[j]));var wt=Vector3.TransformNormal(w.T,bones[j]);var across=NativeHordeGore.Unit(Vector3.Cross(wn,wt),Vector3.UnitY);var fw=-Vector3.UnitY-wn*Vector3.Dot(-Vector3.UnitY,wn);if(fw.Length()>.3f)acrossOld.Add(Math.Abs(Vector3.Dot(across,Vector3.Normalize(fw))));}
                    }
                    foreach(float age in new[]{1.6f,4.5f})
                    {
                        var pose=walk==1?Posed(kind,id,at+fwd*speed*age,yaw,1+age,speed):bones;var surface=walk==1?new Drawn(kind,look,id,pose):drawn;
                        st.Begin();st.DrawWounds(id,pose,1+age);bool hasRun=w.RunN>=2&&w.RunLength*MeleeMath.Smooth(age/1.6f+.15f)>w.Size*.4f;int runs=hasRun?w.RunN-1:0;
                        for(int i=0;i<st.BlockCount;i++)
                        {
                            float d=surface.Off(j,st.Blocks[i]);blocks++;(hasRun&&i==st.BlockCount-1?drop:hasRun&&i>=st.BlockCount-1-runs?run:core).Add(d);
                            if(kind<NativeHordeKinds.Count)worstSeven=Math.Max(worstSeven,d);else worstZone=Math.Max(worstZone,d);tierWorst[st.Tier]=Math.Max(tierWorst[st.Tier],d);
                            if(d>worstAll){worstAll=d;worstWhat=(kind<NativeHordeKinds.Count?NativeHordeKinds.Ids[kind]:"zone kind "+kind)+" joint "+j+" "+cause;}
                            Check(d<=.035f*Math.Max(1,sc),NativeHordeKinds.Ids[Math.Min(kind,NativeHordeKinds.Ids.Length-1)]+" kind "+kind+" look "+look+(walk==1?" walking":" standing")+" joint "+j+" "+cause+" at "+age+" s: wound piece "+i+" of "+st.BlockCount+" sits on the drawn part ("+d.ToString("0.000")+" m off)");
                        }
                    }
                }
            }
            float P(List<float> l,float q){if(l.Count==0)return 0;l.Sort();return l[Math.Min(l.Count-1,(int)(l.Count*q))];}
            Check(hits>=NativeHordeKinds.All*2*4*aims.Length*8/10,"the shots land ("+hits+")");
            Check(P(core,.95f)<.02f&&P(run,.95f)<.02f&&P(drop,.95f)<.025f,"nineteen in twenty wound pieces sit within 2 cm of the drawn part (wound "+P(core,.95f).ToString("0.000")+", run "+P(run,.95f).ToString("0.000")+", drop "+P(drop,.95f).ToString("0.000")+" m)");
            // The regression: the run runs down the surface (its stretch along the surface's own down), where the old strip lay across.
            string Deg(float c)=>(Math.Acos(Math.Max(-1,Math.Min(1,c)))*180/Math.PI).ToString("0");
            Check(downs.Count>hits/2&&P(downs,.5f)>.95f&&P(downs,.05f)>.6f&&P(downs,0)>0,"the blood run runs down the drawn surface (half its stretches within "+Deg(P(downs,.5f))+" degrees of the surface's down, 19 in 20 within "+Deg(P(downs,.05f))+", every one downward: worst "+Deg(P(downs,0))+"; "+downs.Count+" stretches)");
            Check(P(acrossOld,.5f)<.75f,"the old run's axis (the wound frame's b, across the shot) was not down the surface (median |cos| "+P(acrossOld,.5f).ToString("0.00")+")");
            // The regression: a head shot on the big kinds is on the drawn head; the capsule the wound went on is far wider.
            var notes=new List<string>();
            foreach(int kind in new[]{NativeHordeKinds.Brute,NativeHordeKinds.Hulk})
            {
                int id=82000+kind;var at=Vector3.Zero;var bones=Posed(kind,id,at,0);var drawn=new Drawn(kind,0,id,bones);float sc=NativeHordeKinds.Scale[kind];
                NativeHordeGore.Segment(bones,CapturedRig.Head,out var h0,out var h1);float capsule=NativeHordeGore.SegmentRadius[CapturedRig.Head]*NativeHordeGore.Girth(kind,false)*sc,widest=0;
                foreach(var v in drawn.Tri[CapturedRig.Head])widest=Math.Max(widest,Vector3.Distance(NativeHordeGore.Closest(h0,h1,v),v));
                var from=new Vector3(.2f,1.6f,7);var ray=Vector3.Normalize((h0+h1)*.5f-from);Check(Authority(kind,at,from,ray,out var point),"the head shot meets the hit volume");
                var st=new NativeHordeGoreState();int j=st.Hit(new GoreHit{Id=id,Kind=kind,Time=1,Amount=20,MaxHp=80,Cause=NativeHordeGore.Cause.Bullet,Point=point,Dir=Vector3.Normalize(new Vector3(ray.X,0,ray.Z)),Shot=ray},bones,0,out bool head);
                var w=st.Find(id,false).W[0];float now=drawn.Off(CapturedRig.Head,Vector3.Transform(w.P,bones[w.Bone]));
                NativeHordeGore.Place(bones,kind,point,ray,false,out var old,out _,out _);float was=drawn.Off(CapturedRig.Head,old);
                Check(capsule>widest*1.2f,NativeHordeKinds.Ids[kind]+": the head's capsule ("+capsule.ToString("0.000")+" m) is wider than the drawn head ("+widest.ToString("0.000")+" m)");
                Check(j==CapturedRig.Head&&head&&now<.004f*sc&&was>.04f*sc,NativeHordeKinds.Ids[kind]+": the head shot wounds the drawn head ("+now.ToString("0.000")+" m off it), not the capsule ("+was.ToString("0.000")+" m off)");
                notes.Add(NativeHordeKinds.Ids[kind]+" head capsule "+capsule.ToString("0.00")+" m vs drawn "+widest.ToString("0.00")+" m: a head shot "+(was*100).ToString("0")+" cm off on the capsule, "+(now*100).ToString("0.0")+" cm on the model");
            }
            // An old frame (no horde-hit data) never wounds a lost limb (it is not drawn); the replicated lost bits are on every frame.
            {
                var bones=Posed(NativeHordeKinds.Shambler,82100,Vector3.Zero,0);var st=new NativeHordeGoreState();
                var arm=First(bones,0,82100,new Vector3(-.4f,1.1f,6),Vector3.Normalize(NativeHordeGore.Origin(bones[CapturedRig.LElbow])-new Vector3(-.4f,1.1f,6)),out var ap,out int aj)?ap:Vector3.Zero;
                int j=st.Hit(new GoreHit{Id=82100,Kind=0,Time=1,Amount=10,MaxHp=60,Cause=NativeHordeGore.Cause.Bullet,Lost=2|8,Point=arm,Dir=-Vector3.UnitZ},bones,0,out _);
                Check((NativeHordeGore.ShownJoints(2|8)&1<<j)!=0&&j!=CapturedRig.LArm&&j!=CapturedRig.LElbow&&j!=CapturedRig.LHand,"an old frame's hit where a lost arm was wounds a drawn part (joint "+j+", the arm's "+aj+")");
                Check(NativeHordeGore.ShownJoints(0)==(((1<<22)-1)&~1)&&NativeHordeGore.ShownJoints(2|8)==((((1<<22)-1)&~1)&~(1<<CapturedRig.LArm|1<<CapturedRig.LElbow|1<<CapturedRig.LHand))&&NativeHordeGore.ShownJoints(1)==((((1<<22)-1)&~1)&~(1<<CapturedRig.Head)),"the drawn joints are the runtime's (a lost part's whole subtree hidden)");
            }
            // The runtime: wounds only on the Full model they were placed on; the Full models handed over as the bank loads.
            {
                var p=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeHordeGorePresentation.cs"));var k=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeHordeKindPresentation.cs"));var m=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeHordeKindModels.cs"));
                Check(p.Contains("if(rig==null||tier!=NativeHordeModels.Full||hordeGore.Wounds(id)==0)return;")&&k.Contains("GoreWoundsJob(m.Id,rig,s.Tier);"),"a live add's wounds draw only on the Full model (the surface they are on)");
                Check(m.Contains("if(tier==NativeHordeModels.Full)NativeHordeGoreBody.Keep(built);"),"the bank hands the gore its Full models as it loads (no hit builds one)");
                Check(p.Contains("Look=look,")&&p.Contains("Shot=GoreV(shot)")&&p.Contains("var shot=projectile&&travel.sqrMagnitude>.5f?travel.normalized:Vector3.zero;"),"a hit carries the drawn look and a projectile's own travel");
            }
            summary=hits+" hits on all "+NativeHordeKinds.All+" kinds and every look ("+moving+" walking, drawn on the later pose), five causes and three gore tiers draw "+blocks+" wound pieces on the drawn part: worst "+(worstSeven*100).ToString("0.0")+" cm on the seven, "+(worstZone*100).ToString("0.0")+" cm on the zone kinds ("+worstWhat+"), tiers "+string.Join("/",tierWorst.Select(x=>(x*100).ToString("0.0")))+" cm, 95% within "+(P(core,.95f)*100).ToString("0.0")+" (wound) / "+(P(run,.95f)*100).ToString("0.0")+" (run) / "+(P(drop,.95f)*100).ToString("0.0")+" (drop) cm, centres within "+(P(centre,1)*100).ToString("0.00")+" cm; runs down the surface (half within "+Deg(P(downs,.5f))+" degrees, 19 in 20 within "+Deg(P(downs,.05f))+"; the old strip across it, median "+Deg(P(acrossOld,.5f))+" degrees off); "+string.Join("; ",notes);
        }

        // ---- the joints the runtime hands the gore are the joints the checks pose ----
        // Unity's Transform.localToWorldMatrix (column vectors: the parent's times its own translation, rotation, scale), written out
        // here as column-major products, through NativeHordeGore.FromColumns (what NativeHordeGorePresentation.GoreN calls), equals
        // the System.Numerics row-vector chain the checks use (Posed/Bones); and Column takes it back (GoreU, the corpses' joints).
        private static void Conversion(out string summary)
        {
            float worst=0;int n=0;
            static float[] Mul(float[] a,float[] b){var r=new float[16];for(int i=0;i<4;i++)for(int j=0;j<4;j++){float s=0;for(int k=0;k<4;k++)s+=a[i+4*k]*b[k+4*j];r[i+4*j]=s;}return r;}
            static float[] Trs(Vector3 t,Quaternion q,float s)
            {
                float x=q.X,y=q.Y,z=q.Z,w=q.W;var m=new float[16];
                m[0]=(1-2*(y*y+z*z))*s;m[1]=2*(x*y+w*z)*s;m[2]=2*(x*z-w*y)*s;m[4]=2*(x*y-w*z)*s;m[5]=(1-2*(x*x+z*z))*s;m[6]=2*(y*z+w*x)*s;m[8]=2*(x*z+w*y)*s;m[9]=2*(y*z-w*x)*s;m[10]=(1-2*(x*x+y*y))*s;
                m[12]=t.X;m[13]=t.Y;m[14]=t.Z;m[15]=1;return m;
            }
            foreach(int kind in new[]{NativeHordeKinds.Shambler,NativeHordeKinds.Hulk})foreach(float yaw in new[]{0f,37,250})
            {
                var pose=new CapturedRig.Pose();var gait=new NativeHordeGait();var st=NativeHordeKinds.ProposedRow(kind);float sc=NativeHordeKinds.Scale[kind];var at=new Vector3(2,0,-1);
                var root=new Vector3(at.X,NativeHordeKinds.DrawRoot(kind,false),at.Z);
                gait.Pose(new HordeMotionInput{Kind=kind,Id=83000,Seed=NativeHordeKinds.Seed(83000),Time=1.3f,Yaw=yaw,Root=root/sc,Velocity=new Vector3(0,0,.8f),Health=1,AttackAge=float.NaN,Strike=NativeHordeKinds.Strike(st,false,0),Recover=NativeHordeKinds.Recover(st,false,0),Attack=NativeHordeKinds.Default[kind],Side=1},pose,new NativeHordeReact());
                var rows=Bones(pose,root,gait.Yaw,sc);
                // The rig root: SetPositionAndRotation(pos, Euler(0, yaw, 0)), localScale = the kind's scale; each joint its local pose.
                float half=gait.Yaw*(float)Math.PI/360;var unity=new float[22][];var rootM=Trs(root,new Quaternion(0,(float)Math.Sin(half),0,(float)Math.Cos(half)),sc);
                for(int i=0;i<22;i++)
                {
                    int parent=CapturedRig.Bones[i].Parent;var local=Trs(CapturedRig.Mirror(pose.Position[i]),Quaternion.Normalize(CapturedRig.Mirror(pose.Rotation[i])),1);
                    unity[i]=Mul(parent<0?rootM:unity[parent],local);
                    var c=unity[i];var g=NativeHordeGore.FromColumns(new Vector4(c[0],c[1],c[2],c[3]),new Vector4(c[4],c[5],c[6],c[7]),new Vector4(c[8],c[9],c[10],c[11]),new Vector4(c[12],c[13],c[14],c[15]));
                    var r=rows[i];float d=Math.Max(Math.Max(Math.Max(Math.Abs(g.M11-r.M11),Math.Abs(g.M12-r.M12)),Math.Max(Math.Abs(g.M13-r.M13),Math.Abs(g.M21-r.M21))),Math.Max(Math.Max(Math.Abs(g.M22-r.M22),Math.Abs(g.M23-r.M23)),Math.Max(Math.Max(Math.Abs(g.M31-r.M31),Math.Abs(g.M32-r.M32)),Math.Max(Math.Abs(g.M33-r.M33),(NativeHordeGore.Origin(g)-NativeHordeGore.Origin(r)).Length()))));
                    worst=Math.Max(worst,d);n++;
                    for(int col=0;col<4;col++){var back=NativeHordeGore.Column(g,col);Check(back==new Vector4(c[col*4],c[col*4+1],c[col*4+2],c[col*4+3]),"Column takes a joint back to Unity's column "+col);}
                }
            }
            Check(worst<2e-5f,"the Unity joints through FromColumns are the checks' joints (worst element "+worst.ToString("0.0e0")+")");
            summary=n+" Unity joint matrices (shambler and Hulk, three facings) convert to the checks' own within "+worst.ToString("0.0e0");
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
        private sealed class Feed{public float At;public GoreHit Hit;public bool Death;public int Id;public NativeHordeGore.Cause Cause;public bool Head;public float Strength=120;}
        private static void Determinism(out string summary)
        {
            var bones=Posed(NativeHordeKinds.Brute,73000,new Vector3(1,0,1),30);int frames=0;
            // Old frames, then frames with the authority's horde-hit data (head flags, parts with lost limbs, raw damage driving the
            // deaths' fates through the same strength the runtime keeps).
            for(int pass=0;pass<2;pass++)
            {
                bool meta=pass==1;var feed=new List<Feed>();uint s=12345;float R(){s=s*1664525+1013904223;return (s>>8)/16777216f;}
                for(int i=0;i<40;i++){int id=73000+i%6;float at=i*.23f;var c=i%7==0?NativeHordeGore.Cause.Melee:i%11==0?NativeHordeGore.Cause.Blast:NativeHordeGore.Cause.Bullet;
                    var hit=new GoreHit{Id=id,Kind=2,Time=at,Amount=10+30*R(),MaxHp=240,Cause=c,Point=new Vector3(1+R()-.5f,.4f+1.2f*R(),1.6f),Dir=Vector3.Normalize(new Vector3(R()-.5f,-.1f,-1)),Sweep=new Vector3(1,.3f,0)};
                    if(meta&&i%4!=3){hit.Known=true;hit.Head=i%5==0;hit.HasPart=i%6!=5;hit.Part=i%10==0?0:NativeHordeGore.PartBits[i%9];hit.Raw=hit.Amount*(1+R()*3);hit.Lost=i%8==0?8|32:0;}
                    feed.Add(new Feed{At=at,Hit=hit});}
                for(int i=0;i<6;i++)
                {
                    var c=i%2==0?NativeHordeGore.Cause.Blast:meta&&i%3==1?NativeHordeGore.Cause.Heavy:NativeHordeGore.Cause.Bullet;
                    feed.Add(new Feed{At=6+i*.4f,Death=true,Id=73000+i,Cause=c,Head=meta&&i%3!=0,Strength=meta?NativeHordeGore.Strength(c,true,60+i*60,20):120});
                }
                feed=feed.OrderBy(f=>f.At).ToList();
                // The host sees every event the frame it happens (60 fps); a guest in 20 Hz batches, each batch in arrival order; the
                // killcam rewinds (Clear) and replays the whole window.
                NativeHordeGoreState Run(float step,bool rewind)
                {
                    var st=new NativeHordeGoreState();int next=0;
                    for(float now=0;now<10;now+=step)
                    {
                        if(rewind&&now>=4&&now<4+step){st.Clear();next=0;}
                        while(next<feed.Count&&feed[next].At<=now){var f=feed[next++];if(f.Death){var fate=st.Fate(2,f.At,f.Cause,f.Head,f.Strength,240,.6f,false);var r=st.Die(f.Id,2,f.At,NativeHordeGore.DeathSeed(f.Id,next),fate);r.PoolMost=NativeHordeGore.PoolMost(2,fate,st.Wounds(f.Id),1);r.ChunkAt=new Vector3(1,1,1);r.Push=Vector3.UnitZ;if(r.Body){for(int b=0;b<22;b++)r.Bones[b]=bones[b];st.Pose(r,next);}}else st.Hit(f.Hit,bones,0,out _);}
                        st.Sprayed();
                    }
                    return st;
                }
                var host=Run(1/60f,false);var guest=Run(1/20f,false);var killcam=Run(1/30f,true);
                foreach(float now in new[]{2.5f,6.3f,7.1f,8.9f})
                {
                    foreach(var st in new[]{host,guest,killcam}){st.Begin();st.Collect(now,Vector3.Zero);for(int i=0;i<6;i++)st.DrawWounds(73000+i,bones,now);}
                    string tag=meta?" (horde-hit data)":"";
                    Check(host.BlockCount>0&&host.BlockCount==guest.BlockCount&&host.BlockCount==killcam.BlockCount,"the same voxels at "+now+" s on the host, a guest and the killcam"+tag+" ("+host.BlockCount+", "+guest.BlockCount+", "+killcam.BlockCount+")");
                    for(int i=0;i<host.BlockCount;i++){var a=host.Blocks[i];foreach(var o in new[]{guest,killcam}){var b=o.Blocks[i];Check(a.At==b.At&&a.Size==b.Size&&a.Turn==b.Turn&&a.Albedo==b.Albedo,"voxel "+i+" is the same everywhere"+tag);}}
                    Check(host.MistCount==guest.MistCount&&host.PoolCount==guest.PoolCount&&host.PoolCount==killcam.PoolCount,"the same mist and pools"+tag);
                    for(int i=0;i<host.PoolCount;i++)Check(host.Pools[i].At==guest.Pools[i].At&&host.Pools[i].Radius==killcam.Pools[i].Radius,"pool "+i+" is the same everywhere"+tag);
                    for(int i=0;i<6;i++){var a=host.RemainsOf(73000+i);foreach(var o in new[]{guest,killcam}){var b=o.RemainsOf(73000+i);Check(a==null==(b==null)&&(a==null||a.Fate==b.Fate),"add "+i+"'s fate is the same everywhere"+tag);}}
                    frames++;
                }
                if(meta)Check(host.RemainsOf(73001)?.Fate==NativeHordeGore.Fate.HeadGib&&host.RemainsOf(73005)?.Fate==NativeHordeGore.Fate.HeadGib&&host.RemainsOf(73003)?.Fate==NativeHordeGore.Fate.Corpse,"the raw damage's fates hold on every peer (a rifle's and a bullet's head kills burst the head, a bullet's body kill is a corpse)");
            }
            summary="40 hits and 6 deaths draw identically on the host (60 fps), a guest (20 Hz batches) and the killcam (rewound) at "+frames+" moments, old frames and with the horde-hit data";
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
                // Hits with the authority's horde-hit data: head flags, every part, lost limbs, raw damage.
                if(i%9==4)st.Hit(new GoreHit{Id=id,Kind=0,Index=1,Time=now,Amount=10,MaxHp=60,Cause=i%36==4?NativeHordeGore.Cause.Heavy:NativeHordeGore.Cause.Bullet,Known=true,Head=i%45==4,HasPart=i%63!=4,Part=i%10==4?0:NativeHordeGore.PartBits[(i/9)%9],Raw=10+i%70,Lost=i%54==4?8|128:0,Point=new Vector3(0,.4f+(i%5)*.25f,.5f),Dir=-Vector3.UnitZ},bones,0,out _);
                if(i%90==89){var r=st.Die(id,0,now,NativeHordeGore.DeathSeed(id,i),st.Fate(0,now,i%180==89?NativeHordeGore.Cause.Blast:NativeHordeGore.Cause.Bullet,false,60,60,.5f,false));r.PoolMost=.5f;if(r.Body){for(int b=0;b<22;b++)r.Bones[b]=bones[b];st.Pose(r,i);}}
                st.Begin();st.Collect(now,Vector3.Zero);st.DrawWounds(id,bones,now);st.Drip(id,bones,now);st.Cap(Vector3.UnitY,Vector3.UnitY,.05f,now%2,7);
                for(int k=0;k<st.SprayCount;k++){var s=st.Sprays[k];blood.Spray(s.Kind,s.Severity,s.Seed,s.Key,s.Origin,s.Normal,s.Along,s.Time,1,false,false,s.Extra,s.Target,s.Kill);}st.Sprayed();
                NativeHordeGore.VentDust(7,i%10,now%2,out _,out _,out _,out _);NativeHordeGore.FountainPower(i%20);
            }
            for(int i=0;i<600;i++)Frame(i);long before=GC.GetAllocatedBytesForCurrentThread();for(int i=600;i<3600;i++)Frame(i);long used=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(used==0,"hits, deaths, corpses, wounds, drips, chunks, pools and sprays allocate nothing per frame ("+used+" bytes over 3000 frames)");
        }

        // ---- an old frame (no horde-hit data) draws bit for bit as before the binding ----
        // A body in the rig's rest pose (arms down) at `at`, turned `quarter` quarter turns and scaled `scale`, built from exact
        // matrices only (no sine or cosine): its joints are the same bits on every machine.
        private static Matrix4x4[] Rest(Vector3 at,int quarter,float scale)
        {
            float c=quarter==0?1:quarter==2?-1:0,s=quarter==1?1:quarter==3?-1:0;
            var root=Matrix4x4.CreateScale(scale)*new Matrix4x4(c,0,-s,0,0,1,0,0,s,0,c,0,0,0,0,1)*Matrix4x4.CreateTranslation(at);var world=new Matrix4x4[22];
            for(int i=0;i<22;i++){int parent=CapturedRig.Bones[i].Parent;world[i]=Matrix4x4.CreateTranslation(CapturedRig.Mirror(CapturedRig.Bones[i].Position))*(parent<0?root:world[parent]);}
            return world;
        }
        /// <summary>
        /// A digest of everything an old frame's hits make (no horde-hit data): each hit's struck joint, head, wound, burst and
        /// sprays on four kinds at four facings, and every fate, severity and extra-drop rule over a grid. Only exact arithmetic
        /// and square roots, so the bits are the machine's IEEE results (x64 with hardware vector intrinsics, as CI runs).
        /// </summary>
        private static ulong FallbackDigest()
        {
            ulong h=14695981039346656037UL;
            void I(int v){unchecked{h=(h^(uint)v)*1099511628211UL;}}
            void F(float v)=>I(BitConverter.SingleToInt32Bits(v));
            void V(Vector3 v){F(v.X);F(v.Y);F(v.Z);}
            void T(string s){if(s!=null)foreach(char ch in s)I(ch);I(-1);}
            var causes=new[]{NativeHordeGore.Cause.Bullet,NativeHordeGore.Cause.Heavy,NativeHordeGore.Cause.Melee,NativeHordeGore.Cause.Arrow,NativeHordeGore.Cause.Blast,NativeHordeGore.Cause.Crush};
            foreach(int kind in new[]{NativeHordeKinds.Shambler,NativeHordeKinds.Runner,NativeHordeKinds.Brute,NativeHordeKinds.Hulk})
            for(int q=0;q<4;q++)
            {
                var st=new NativeHordeGoreState{Tier=q%3};float sc=NativeHordeKinds.Scale[kind];var at=new Vector3(1,1.1f*sc,-2);var bones=Rest(at,q,sc);
                for(int i=0;i<30;i++)
                {
                    int id=76000+kind*100+q*10+i%4;var cause=causes[i%6];
                    var point=at+new Vector3(((i%5)-2)*.12f,-.95f+(i%9)*.21f,((i/5)%3-1)*.14f)*sc;var dir=Vector3.Normalize(new Vector3((i%4)-1.5f,-.1f*(i%3),-1+(i%2)*2.2f));
                    var hit=new GoreHit{Id=id,Kind=kind,Index=i%3,Time=1+i*.07f,Amount=3+i*3.5f,MaxHp=40+kind*20,Cause=cause,Kill=i%4==3,Head=i%7==0,Point=point,Dir=dir,Sweep=i%2==0?new Vector3(1,.3f,0):Vector3.Zero};
                    int bone=st.Hit(hit,bones,0,out bool head);I(bone);I(head?1:0);
                    var g=st.Find(id,false);if(g!=null&&g.Count>0){var w=g.W[(g.Next-1+g.W.Length)%g.W.Length];I(w.Bone);V(w.P);V(w.N);V(w.T);V(w.Down);F(w.Size);F(w.At);I((int)w.Cause);I(w.Head?1:0);I(w.Stump?1:0);I((int)w.Seed);I(g.Count);}
                }
                foreach(var b in st.Bursts){I(b.Live?1:0);if(!b.Live)continue;V(b.At);V(b.N);V(b.Dir);F(b.Born);F(b.Floor);F(b.Power);I((int)b.Seed);I(b.Bits);I(b.Mist);}
                for(int k=0;k<st.SprayCount;k++){var s=st.Sprays[k];T(s.Kind);F(s.Severity);F(s.Time);I((int)s.Seed);I((int)s.Key);I((int)(s.Key>>32));V(s.Origin);V(s.Normal);V(s.Along);I(s.Extra);I(s.Target);I(s.Kill?1:0);}
                st.Sprayed();
            }
            foreach(int kind in new[]{NativeHordeKinds.Shambler,NativeHordeKinds.Bloater})foreach(var c in causes)for(int hd=0;hd<2;hd++)foreach(float amount in new[]{0f,10,19.8f,20,29.9f,30,47.9f,48,50,60,100})foreach(float d in new[]{.5f,1.4f,2})for(int hv=0;hv<2;hv++)
            {
                I((int)NativeHordeGore.Decide(kind,c,hd==1,amount,60,d,hv==1));F(NativeHordeGore.Severity(c,hd==1,amount,60));
            }
            foreach(var c in causes)for(int hd=0;hd<2;hd++)for(int kill=0;kill<2;kill++)for(int t=0;t<3;t++){I(NativeHordeGore.Extra(c,hd==1,kill==1,t));T(NativeHordeGore.SprayKind(c,hd==1));}
            for(int t=0;t<3;t++){var st=new NativeHordeGoreState{Tier=t};for(int i=0;i<24;i++)I((int)st.Fate(0,10+i*.3f,causes[i%6],i%3==0,10+i*4,60,i%2==0?1:2,i%5==0));}
            return h;
        }
        /// <summary>The digest of an old frame's hits, Debug and Release alike. Re-recorded on purpose on 2026-10-20 (the wound fix,
        /// "Zombie wounds appear off their body"): the wounds go where the shot enters the drawn model, fitted to it, with their
        /// run traced down it, instead of on a capsule round the joint, so every wound's place, normal, size and run changed (and
        /// with them the bursts and sprays that leave the wound). The binding's own digest on shared 8740ceb..f930684 was
        /// 0x1a7d959dbc8e8969; the binding still leaves old frames alone (the stray-data check below).</summary>
        private const ulong FallbackBefore=0xf8a1621b9ac8bba8UL;
        private static void Fallback(out string summary)
        {
            ulong d=FallbackDigest();
            Check(d==FallbackBefore,"an old frame's hits (no horde-hit data) make bit for bit the wounds, bursts, sprays and fates they made (digest 0x"+d.ToString("x16")+", recorded 0x"+FallbackBefore.ToString("x16")+")");
            // The data's fields are not read without HasHordeHit: the same hits with stray values in them make the same bits.
            var bones=Posed(NativeHordeKinds.Shambler,77000,Vector3.Zero,0);var a=new NativeHordeGoreState();var b=new NativeHordeGoreState();int same=0;
            for(int i=0;i<40;i++)
            {
                var h=new GoreHit{Id=77000+i%3,Kind=0,Index=i%2,Time=1+i*.11f,Amount=4+i*2,MaxHp=60,Cause=i%5==0?NativeHordeGore.Cause.Heavy:i%7==0?NativeHordeGore.Cause.Blast:NativeHordeGore.Cause.Bullet,Kill=i%6==5,Head=i%9==0,Point=new Vector3(((i%5)-2)*.12f,.3f+(i%8)*.2f,.4f),Dir=-Vector3.UnitZ};
                // (The lost limbs are the replicated Pve.Lost on every frame, not the data: a lost part is not drawn, so no frame
                // wounds it; OnBody holds that.)
                var stray=h;stray.Raw=999;stray.HasPart=true;stray.Part=NativeHordeGore.PartBits[i%9];
                int ja=a.Hit(h,bones,0,out bool ha),jb=b.Hit(stray,bones,0,out bool hb);
                var wa=a.Find(h.Id,false);var wb=b.Find(h.Id,false);var x=wa.W[(wa.Next-1+8)%8];var y=wb.W[(wb.Next-1+8)%8];var sa=a.Sprays[a.SprayCount-1];var sb=b.Sprays[b.SprayCount-1];
                Check(ja==jb&&ha==hb&&x.P==y.P&&x.N==y.N&&x.Size==y.Size&&x.Head==y.Head&&sa.Kind==sb.Kind&&sa.Severity==sb.Severity&&sa.Origin==sb.Origin,"hit "+i+": without HasHordeHit the data's fields are not read");same++;
            }
            summary="an old frame draws bit for bit as before (digest 0x"+d.ToString("x16")+"; "+same+" hits ignore stray data)";
        }

        // ---- the authority's horde-hit data ----
        private static void Metadata(string root,out string summary)
        {
            var st=new NativeHordeGoreState();int placed=0,sides=0,agree=0;
            // The part map is the severed-limb map (the same LIMB bit throws and wounds the same joint).
            var fx=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeMinionFxPresentation.cs"));
            Check(fx.Contains("HordeLimbBits={1,2,4,8,16,32,64,128,256},HordeLimbBones={5,7,11,8,12,14,18,15,19}")&&string.Join(",",NativeHordeGore.PartBits)=="1,2,4,8,16,32,64,128,256"&&string.Join(",",NativeHordeGore.PartRoots)=="5,7,11,8,12,14,18,15,19","the wound's part map is the severed limbs' map (LIMB bit to joint)");
            for(int i=0;i<9;i++)Check((NativeHordeGore.PartJoints(NativeHordeGore.PartBits[i])&1<<NativeHordeGore.PartRoots[i])!=0&&(NativeHordeGore.PartJoints(NativeHordeGore.PartBits[i])&NativeHordeGore.TorsoJoints)==0,"part "+NativeHordeGore.PartBits[i]+" holds its own joint and none of the trunk's");
            {int all=NativeHordeGore.TorsoJoints;for(int i=0;i<9;i++){Check((all&NativeHordeGore.PartJoints(NativeHordeGore.PartBits[i]))==0,"parts do not overlap");all|=NativeHordeGore.PartJoints(NativeHordeGore.PartBits[i]);}Check(all==(((1<<22)-1)&~1),"the torso and the nine parts cover every drawn joint but the root");}
            Check(NativeHordeGore.PartJoints(3)==0&&NativeHordeGore.PartJoints(512)==0&&NativeHordeGore.Struck(false,true,3,0)==0&&NativeHordeGore.Struck(false,false,8,0)==0,"no part, an unknown part: any joint (the nearest, as before)");
            foreach(int kind in new[]{NativeHordeKinds.Shambler,NativeHordeKinds.Runner,NativeHordeKinds.Brute,NativeHordeKinds.Hulk})
            for(int yaw=0;yaw<360;yaw+=90)
            {
                int id=78000+kind*10+yaw/90;var at=new Vector3(-2,0,3);var bones=Posed(kind,id,at,yaw);float sc=NativeHordeKinds.Scale[kind];
                float r=yaw*(float)Math.PI/180;var fwd=new Vector3((float)Math.Sin(r),0,(float)Math.Cos(r));var shooter=new Vector3(at.X,1.4f*sc,at.Z)+fwd*6;
                // A shot into the chest that the authority says struck part p: the wound is on p (the torso a known part).
                NativeHordeGore.Segment(bones,CapturedRig.Chest,out var c0,out var c1);var chestDir=Vector3.Normalize((c0+c1)*.5f-shooter);First(bones,kind,id,shooter,chestDir,out var chest,out _);
                foreach(int p in new[]{0,1,2,4,8,16,32,64,128,256})
                {
                    st.Clear();var h=new GoreHit{Id=id,Kind=kind,Time=1,Amount=12,MaxHp=60,Cause=NativeHordeGore.Cause.Bullet,Known=true,Head=p==1,HasPart=true,Part=p,Raw=12,Point=chest,Dir=chestDir};
                    int j=st.Hit(h,bones,at.Y,out bool head);Check(j>0&&(NativeHordeGore.PartJoints(p)&1<<j)!=0,NativeHordeKinds.Ids[kind]+" yaw "+yaw+": a chest hit the authority puts on part "+p+" wounds that part (joint "+j+")");
                    Check(head==(p==1)&&st.Sprays[st.SprayCount-1].Kind==(p==1?"cavity":"puncture"),"part "+p+": the head only when the authority says so");placed++;
                }
                // A shot aimed at each drawn part, its part taken from the authority's own anatomy (NativeHordeDamage.Part, in its
                // mirrored frame round the body's centre): the wound is on that part at the hit, and the sides agree.
                var body=new Vector3(at.X,at.Y+NativeHordeKinds.RootHeight(kind,false),at.Z);
                foreach(int aim in new[]{CapturedRig.Head,CapturedRig.Chest,CapturedRig.LArm,CapturedRig.RArm,CapturedRig.LElbow,CapturedRig.RElbow,CapturedRig.LHip,CapturedRig.RHip,CapturedRig.LKnee,CapturedRig.RKnee})
                {
                    NativeHordeGore.Segment(bones,aim,out var a0,out var a1);var dir=Vector3.Normalize((a0+a1)*.5f-shooter);if(!First(bones,kind,id,shooter,dir,out var point,out int first))continue;
                    int part=NativeHordeDamage.Part(new V3(-body.X,body.Y,body.Z),-yaw,false,new V3(-point.X,point.Y,point.Z),false);
                    st.Clear();int j=st.Hit(new GoreHit{Id=id,Kind=kind,Time=2,Amount=10,MaxHp=60,Cause=NativeHordeGore.Cause.Bullet,Known=true,Head=part==1,HasPart=true,Part=part,Raw=10,Point=point,Dir=dir},bones,at.Y,out _);
                    Check((NativeHordeGore.PartJoints(part)&1<<j)!=0,NativeHordeKinds.Ids[kind]+" yaw "+yaw+": the shot at joint "+aim+" (first "+first+") the authority calls part "+part+" wounds that part (joint "+j+")");
                    bool left(int x)=>x==CapturedRig.LArm||x==CapturedRig.LElbow||x==CapturedRig.LHand||x==CapturedRig.LHip||x==CapturedRig.LKnee||x==CapturedRig.LFoot||x==CapturedRig.LToe||x==CapturedRig.LClav;
                    bool right(int x)=>x==CapturedRig.RArm||x==CapturedRig.RElbow||x==CapturedRig.RHand||x==CapturedRig.RHip||x==CapturedRig.RKnee||x==CapturedRig.RFoot||x==CapturedRig.RToe||x==CapturedRig.RClav;
                    if(part>1&&(left(first)||right(first))){Check(left(first)==((part&(2|8|32|128))!=0),NativeHordeKinds.Ids[kind]+" yaw "+yaw+": the authority's side of a limb hit is the drawn side (joint "+first+", part "+part+")");sides++;}
                    if((NativeHordeGore.PartJoints(part)&1<<first)!=0){agree++;var g=st.Find(id,false);var w=g.W[g.Count-1];Check(Vector3.Distance(Vector3.Transform(w.P,bones[w.Bone]),point)<.06f*sc,"a known part's wound sits at the hit");}
                }
            }
            Check(sides>=110&&agree>=placed*7/10,"the authority's anatomy and the drawn body agree on the sides of "+sides+" limb hits ("+agree+" on the very part)");
            var bonesS=Posed(NativeHordeKinds.Shambler,79000,Vector3.Zero,0);
            Vector3 Surface(int joint,out Vector3 dir){NativeHordeGore.Segment(bonesS,joint,out var a0,out var a1);var sh=new Vector3(0,1.4f,6);dir=Vector3.Normalize((a0+a1)*.5f-sh);First(bonesS,0,79000,sh,dir,out var p,out _);return p;}
            int Wound(GoreHit h,out bool head,out GoreSpray spray){st.Clear();h.Id=79000;h.Kind=0;h.MaxHp=60;if(h.Amount==0)h.Amount=12;if(h.Cause==NativeHordeGore.Cause.None)h.Cause=NativeHordeGore.Cause.Bullet;int j=st.Hit(h,bonesS,0,out head);spray=st.Sprays[st.SprayCount-1];return j;}
            // Present against absent: a hit with no new fact draws as the inference; old frames infer the head and the part.
            {
                var arm=Surface(CapturedRig.LElbow,out var d);var old=new GoreHit{Time=1,Point=arm,Dir=d};var meta=old;meta.Known=true;meta.Raw=12;
                int jo=Wound(old,out bool ho,out var so),jm=Wound(meta,out bool hm,out var sm);
                Check(jo==jm&&ho==hm&&!ho&&so.Severity==sm.Severity&&so.Kind==sm.Kind,"the data with no part, no head and raw equal to the damage draws as the old frame's inference (joint "+jo+")");
                var headPoint=Surface(CapturedRig.Head,out var hd);old.Point=headPoint;old.Dir=hd;meta=old;meta.Known=true;meta.Raw=12;meta.HasPart=true;meta.Part=0;
                jo=Wound(old,out ho,out so);jm=Wound(meta,out hm,out sm);
                Check(jo==CapturedRig.Head&&ho&&so.Kind=="cavity","an old frame's shot into the head infers a head shot");
                Check((NativeHordeGore.TorsoJoints&1<<jm)!=0&&!hm&&sm.Kind=="puncture","the authority's torso and no head win over the inference: the wound goes on the trunk, no head shot (joint "+jm+")");
            }
            // The head flag wins over a larger torso share; the head is still the head on a torso-largest blast.
            {
                var chestP=Surface(CapturedRig.Chest,out var d);
                int j=Wound(new GoreHit{Time=1,Point=chestP,Dir=d,Known=true,Head=true,HasPart=true,Part=0,Raw=12},out bool hd,out var sp);
                Check(j==CapturedRig.Head&&hd&&sp.Kind=="cavity"&&st.Find(79000,false).W[0].Head,"the authority's head flag wins over a larger torso share: the head is wounded and sprays a cavity");
                j=Wound(new GoreHit{Time=1,Point=chestP,Dir=d,Known=true,Head=false,HasPart=true,Part=0,Raw=12},out hd,out sp);
                Check((NativeHordeGore.TorsoJoints&1<<j)!=0&&!hd&&sp.Kind=="puncture","the same hit with no head share stays on the torso");
                j=Wound(new GoreHit{Time=1,Point=new Vector3(2,.1f,0),Dir=-Vector3.UnitX,Cause=NativeHordeGore.Cause.Blast,Amount=20,Known=true,Head=true,HasPart=true,Part=0,Raw=60},out hd,out sp);
                Check(j==CapturedRig.Head&&hd&&sp.Kind=="crush","a blast whose shares took the head wounds the head (a crush spray)");
                Check(NativeHordeGore.Decide(0,NativeHordeGore.Cause.Heavy,true,10,60,99,false)==NativeHordeGore.Fate.HeadGib&&NativeHordeGore.Decide(0,NativeHordeGore.Cause.Heavy,false,10,60,99,false)==NativeHordeGore.Fate.Corpse,"a rifle kill the authority calls a head hit bursts the head; without it a corpse");
            }
            // Known torso against an unknown part: the forearm's hit point, the torso known; the same hit, its part unknown.
            {
                var arm=Surface(CapturedRig.LElbow,out var d);
                int jt=Wound(new GoreHit{Time=1,Point=arm,Dir=d,Known=true,HasPart=true,Part=0,Raw=12},out bool ht,out _);
                int ju=Wound(new GoreHit{Time=1,Point=arm,Dir=d,Known=true,HasPart=false,Part=0,Raw=12},out bool hu,out _);
                int jo=Wound(new GoreHit{Time=1,Point=arm,Dir=d},out _,out _);
                Check((NativeHordeGore.TorsoJoints&1<<jt)!=0&&!ht,"a known torso (part 0 with HasPart) wounds the trunk, not the forearm by it (joint "+jt+")");
                Check(ju==jo&&(NativeHordeGore.PartJoints(8)&1<<ju)!=0&&!hu,"an unknown part (HasPart false) keeps the nearest drawn part, the forearm (joint "+ju+")");
            }
            // A part the add already lost: the authority names it, then moved the damage to the torso. The wound goes on what is left
            // of the limb (the stump) or the trunk, never on the lost part's hidden joints; a part not lost is unaffected.
            {
                var fore=Surface(CapturedRig.LElbow,out var d);var shin=Surface(CapturedRig.LKnee,out var ds);var head=Surface(CapturedRig.Head,out var dh);
                int j=Wound(new GoreHit{Time=1,Point=fore,Dir=d,Known=true,HasPart=true,Part=8,Raw=12,Lost=8},out _,out _);
                Check(j==CapturedRig.LArm,"a hit on a lost forearm wounds its stump, the upper arm (joint "+j+")");
                j=Wound(new GoreHit{Time=1,Point=fore,Dir=d,Known=true,HasPart=true,Part=8,Raw=12,Lost=2|8},out _,out _);
                Check((NativeHordeGore.TorsoJoints&1<<j)!=0,"a hit on a forearm whose whole arm is lost wounds the trunk (joint "+j+")");
                j=Wound(new GoreHit{Time=1,Point=fore,Dir=d,Known=true,HasPart=true,Part=2,Raw=12,Lost=2|8},out _,out _);
                Check((NativeHordeGore.TorsoJoints&1<<j)!=0,"a hit on a lost upper arm wounds the trunk (joint "+j+")");
                j=Wound(new GoreHit{Time=1,Point=shin,Dir=ds,Known=true,HasPart=true,Part=128,Raw=12,Lost=128},out _,out _);
                Check(j==CapturedRig.LHip||(NativeHordeGore.TorsoJoints&1<<j)!=0,"a hit on a lost shin wounds its stump, the thigh, or the trunk (joint "+j+")");
                j=Wound(new GoreHit{Time=1,Point=head,Dir=dh,Known=true,Head=true,HasPart=true,Part=1,Raw=12,Lost=1},out bool hd,out _);
                Check((NativeHordeGore.TorsoJoints&1<<j)!=0&&hd,"a head hit on a lost head wounds the neck's stump and stays a head hit (joint "+j+")");
                j=Wound(new GoreHit{Time=1,Point=Surface(CapturedRig.RElbow,out var dr),Dir=dr,Known=true,HasPart=true,Part=16,Raw=12,Lost=8},out _,out _);
                Check((NativeHordeGore.PartJoints(16)&1<<j)!=0,"a lost left forearm does not move a right forearm hit (joint "+j+")");
                // Every set of lost limbs the authority can hold (each severed part takes its descendants) and every part.
                int sets=0;
                for(int set=0;set<512;set++)
                {
                    int lost=0;for(int i=0;i<9;i++)if((set&1<<i)!=0)lost|=NativeHordeDamage.Descendants(NativeHordeGore.PartBits[i]);if(lost!=set)continue;sets++;
                    int hidden=0;for(int i=0;i<9;i++)if((lost&NativeHordeGore.PartBits[i])!=0)hidden|=NativeHordeGore.PartJoints(NativeHordeGore.PartBits[i]);
                    foreach(int p in new[]{0,1,2,4,8,16,32,64,128,256})
                    {
                        int mask=NativeHordeGore.Struck(p==1,true,p,lost);
                        Check(mask!=0&&(mask&hidden)==0,"lost "+lost+", part "+p+": the wound may land only on joints still drawn");
                        if(p==0||(lost&p)==0)Check(mask==NativeHordeGore.PartJoints(p),"lost "+lost+": part "+p+", not lost, keeps its own joints");
                        else Check((mask&NativeHordeGore.TorsoJoints)==NativeHordeGore.TorsoJoints&&(mask&~(NativeHordeGore.TorsoJoints|NativeHordeGore.PartJoints(NativeHordeGore.PartParent(p))))==0,"lost "+lost+": lost part "+p+" goes to its stump and the trunk");
                    }
                }
                Check(sets==162,"every closed set of lost limbs: the head's two states, three for each arm and leg ("+sets+")");
            }
            // Raw damage drives the gib, the head burst and the spray; a blast's raw (its centre's, before the falloff) does not.
            {
                float S(NativeHordeGore.Cause c,bool known,float raw,float amount)=>NativeHordeGore.Strength(c,known,raw,amount);
                Check(S(NativeHordeGore.Cause.Heavy,true,300,10)==300&&S(NativeHordeGore.Cause.Heavy,false,300,10)==10&&S(NativeHordeGore.Cause.Bullet,true,10,15)==15&&S(NativeHordeGore.Cause.Bullet,true,float.NaN,10)==10,"the strength: raw when known and larger, else the replicated damage");
                Check(S(NativeHordeGore.Cause.Blast,true,300,10)==10&&S(NativeHordeGore.Cause.Crush,true,300,10)==10,"a blast's or a crusher's raw is not what reached the add: the replicated damage");
                NativeHordeGore.Fate F(NativeHordeGore.Cause c,bool head,bool known,float raw,float amount,float blast=99,bool heavy=false)=>NativeHordeGore.Decide(0,c,head,S(c,known,raw,amount),60,blast,heavy);
                // Gib: a heavy round's (or a heavy blow's) raw at four fifths of the add's health.
                Check(F(NativeHordeGore.Cause.Heavy,false,true,300,10)==NativeHordeGore.Fate.Gib&&F(NativeHordeGore.Cause.Heavy,false,false,300,10)==NativeHordeGore.Fate.Corpse,"a rifle kill's raw overkill gibs the body; the old frame's clamped damage left a corpse");
                Check(F(NativeHordeGore.Cause.Heavy,false,true,48,10)==NativeHordeGore.Fate.Gib&&F(NativeHordeGore.Cause.Heavy,false,true,47.9f,10)==NativeHordeGore.Fate.Corpse,"the gib's raw threshold is four fifths of max HP (48 of 60)");
                Check(F(NativeHordeGore.Cause.Melee,false,true,48,10,99,true)==NativeHordeGore.Fate.Gib&&F(NativeHordeGore.Cause.Melee,false,true,300,10,99,false)==NativeHordeGore.Fate.Corpse,"a heavy blow's raw gibs at the same share; a light blow never does");
                Check(F(NativeHordeGore.Cause.Bullet,false,true,300,10)==NativeHordeGore.Fate.Corpse,"a bullet's body kill is a corpse whatever its raw");
                // Head burst: a head kill whose raw is half the add's health (any cause but the arrow).
                Check(F(NativeHordeGore.Cause.Bullet,true,true,30,8)==NativeHordeGore.Fate.HeadGib&&F(NativeHordeGore.Cause.Bullet,true,true,29.9f,8)==NativeHordeGore.Fate.Corpse&&F(NativeHordeGore.Cause.Bullet,true,false,30,8)==NativeHordeGore.Fate.Corpse,"the head burst's raw threshold is half of max HP (30 of 60); the old frame's clamped 8 left a corpse");
                Check(F(NativeHordeGore.Cause.Arrow,true,true,300,8)==NativeHordeGore.Fate.Corpse,"an arrow's head kill never bursts the head");
                // Blasts keep their own rule: a third of the add's health reaching it, or the blast within 1.4 m.
                Check(F(NativeHordeGore.Cause.Blast,false,true,300,10,2)==NativeHordeGore.Fate.Corpse&&F(NativeHordeGore.Cause.Blast,false,true,300,20,2)==NativeHordeGore.Fate.Gib&&F(NativeHordeGore.Cause.Blast,false,true,300,10,1.2f)==NativeHordeGore.Fate.Gib,"a blast gibs on the damage that reached the add or its distance, not its centre's raw");
                // The spray and the burst: a killing hit's raw sprays harder than its clamped damage; a hit without overkill is unchanged.
                var chestP=Surface(CapturedRig.Chest,out var d);
                Wound(new GoreHit{Time=1,Point=chestP,Dir=d,Amount=10,Kill=true,Cause=NativeHordeGore.Cause.Heavy},out _,out var so);float po=st.Bursts.First(b=>b.Live).Power;
                Wound(new GoreHit{Time=1,Point=chestP,Dir=d,Amount=10,Kill=true,Cause=NativeHordeGore.Cause.Heavy,Known=true,HasPart=true,Raw=300},out _,out var sk);float pk=st.Bursts.First(b=>b.Live).Power;
                Check(sk.Severity>so.Severity&&sk.Severity==NativeHordeGore.Severity(NativeHordeGore.Cause.Heavy,false,300,60)&&pk>po,"a kill's raw overkill sprays and bursts harder ("+so.Severity.ToString("0.00")+" to "+sk.Severity.ToString("0.00")+", power "+po.ToString("0.00")+" to "+pk.ToString("0.00")+")");
                Wound(new GoreHit{Time=1,Point=chestP,Dir=d,Amount=10,Known=true,HasPart=true,Raw=10},out _,out var sn);
                Check(sn.Severity==NativeHordeGore.Severity(NativeHordeGore.Cause.Bullet,false,10,60),"raw equal to the damage sprays as before");
                // Under the gib cap, the state's fate with the strength.
                var cap=new NativeHordeGoreState();Check(cap.Fate(0,1,NativeHordeGore.Cause.Heavy,false,S(NativeHordeGore.Cause.Heavy,true,300,10),60,99,false)==NativeHordeGore.Fate.Gib&&cap.Fate(0,1.1f,NativeHordeGore.Cause.Bullet,true,S(NativeHordeGore.Cause.Bullet,true,40,8),60,99,false)==NativeHordeGore.Fate.HeadGib,"the state's capped fate reads the raw strength");
            }
            // The runtime binds only the replicated fields, keeps the fallback, and keeps the killing hit's strength for the fate.
            {
                var p=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeHordeGorePresentation.cs"));
                Check(p.Contains("bool known=e.HasHordeHit;float strength=NativeHordeGore.Strength(cause,known,e.Raw,e.Amount);")&&p.Contains("s.GoreAmount=strength;")&&p.Contains("s.GoreHead=known&&e.Head;"),"the killing hit's strength and head flag are kept for the death's fate (even with no rig drawn)");
                Check(p.Contains("Time=e.Time,Amount=e.Amount,MaxHp=maxHp,Cause=cause,Kill=m==null,Head=known?e.Head:e.Kind==EventKind.Headshot,Known=known,Raw=e.Raw,HasPart=e.HasPart,Part=e.Part,Lost=lost,")&&p.Contains("if(s!=null)s.GoreHead=known?e.Head:head;"),"the hit binds Head, Raw, HasPart and Part when HasHordeHit, else the Headshot kind and the inference");
                Check(p.Contains("int lost=m?.Pve!=null?m.Pve.Lost:s!=null&&s.Lost>0?s.Lost:0;"),"the lost limbs are the replicated ones");
                var k=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeHordeKindPresentation.cs"));
                Check(k.Contains("s.React.Add(e.Time,push,w,e.Amount,melee,e.HasHordeHit?e.Head:e.Kind==EventKind.Headshot,stagger,"),"the head's snap reads the authority's head flag too, else Headshot");
                var react=new NativeHordeReact();react.Bind(0);react.Add(1,Vector2.UnitX,StaggerWeight.Flinch,10,false,true,false,.4f);Check(react.NewestHit(out _,out _,out bool rh)&&rh,"a head hit snaps the head harder");
            }
            summary=placed+" known-part chest hits on four kinds at four facings wound the part named, "+sides+" limb hits agree on the side with the authority's anatomy ("+agree+" on the very part); the head flag, a known torso, lost parts, raw gib and head-burst thresholds and the spray bind";
        }

        /// <summary>A source's code without its comment lines.</summary>
        private static string Code(string s)=>string.Join("\n",s.Split('\n').Where(l=>!l.TrimStart().StartsWith("//",StringComparison.Ordinal)));
        // ---- presentation only ----
        private static void Source(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
            foreach(var f in new[]{"Core/NativeHordeGore.cs","Core/NativeHordeGoreBody.cs"}){var core=R(f);Check(!core.Contains("UnityEngine")&&!core.Contains("System.Linq")&&!core.Contains("new Random(")&&!core.Contains("DateTime"),f+": the gore rules are pure Core (no engine, no LINQ, no clock-free randomness)");}
            foreach(var f in new[]{"Core/NativeHordeGore.cs","Core/NativeHordeGoreBody.cs","Runtime/NativeHordeGorePresentation.cs","Runtime/NativeHordeGoreGallery.cs"})
            {
                var s=R(f);foreach(var bad in new[]{".Hp=",".Hp-=","Damage(","Emit(","StaggerAt=","RecoverAt=","LastAttack=","Body.position=","linearVelocity=",".Lost=a","Collider.","isKinematic=","Pve.Lost|=","SendFrame","Network"})Check(!s.Contains(bad),f+" never writes authority or network state (`"+bad+"`)");
            }
            // No authority, rules or network file reads the gore; every file that does is presentation.
            var dir=Path.Combine(root,"Assets/ChipArena");var readers=new List<string>();
            foreach(var f in Directory.GetFiles(dir,"*.cs",SearchOption.AllDirectories)){var s=Code(File.ReadAllText(f));if(s.Contains("NativeHordeGore")||s.Contains("HordeGore"))readers.Add(Path.GetFileName(f));}
            // (NativeHordeKindModels.cs: the bank's loader hands the gore the Full models it builds, NativeHordeGoreBody.Keep.)
            var allowed=new HashSet<string>{"NativeHordeGore.cs","NativeHordeGoreBody.cs","NativeHordeGorePresentation.cs","NativeHordeGoreGallery.cs","NativeHordeKindPresentation.cs","NativeHordeKindModels.cs","NativeMinionFxPresentation.cs","NativeIronworksKindsPresentation.cs","NativeBowPresentation.cs"};
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
