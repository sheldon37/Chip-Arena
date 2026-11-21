using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Intros v2's pack clips (Core NativeIntroPack; docs/INTROS-V2.md 9d). Always: the binding run backwards is checked by a round trip on
    /// the measured skeleton (keyed poses bound to the model, written as the local tool writes a bank, read back as keys and bound again:
    /// the same drawn body), the keys' Euler angles round-trip, and banks merge as the Runtime merges them (MultiBank). With the local
    /// banks (NativeIntroPack.Banks, each at its variable, CHIPARENA_INTRO_MOTION_DIR, the local assets' Resources copy or the
    /// framework's scratch copy; none on CI): no clip name in two banks, no clip the match plays (with the match's bank here), every
    /// clip's keys drawn on its bank's own skeleton match the clip as baked on the model, and each clip played through FromPack with its
    /// own travel stays inside the anatomical limits with its feet held and out of the floor (IntroPoseChecks).
    /// </summary>
    internal static class IntroPackChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Intro pack: "+what);count++;}
        private const float Deg=57.29578f;
        /// <summary>The drawn bones a clip is held to on the model: the limbs' directions and the body's, head's and hands' turns.</summary>
        private static readonly (string name,int joint,int child)[] Held=
        {
            ("pelvis",CapturedRig.Pelvis,-1),("chest",CapturedRig.Chest,-1),("head",CapturedRig.Head,-1),
            ("L upper arm",CapturedRig.LArm,CapturedRig.LElbow),("L forearm",CapturedRig.LElbow,CapturedRig.LHand),("L hand",CapturedRig.LHand,-1),
            ("R upper arm",CapturedRig.RArm,CapturedRig.RElbow),("R forearm",CapturedRig.RElbow,CapturedRig.RHand),("R hand",CapturedRig.RHand,-1),
            ("L thigh",CapturedRig.LHip,CapturedRig.LKnee),("L shin",CapturedRig.LKnee,CapturedRig.LFoot),("L foot",CapturedRig.LFoot,CapturedRig.LToe),
            ("R thigh",CapturedRig.RHip,CapturedRig.RKnee),("R shin",CapturedRig.RKnee,CapturedRig.RFoot),("R foot",CapturedRig.RFoot,CapturedRig.RToe),
        };
        private static readonly string[] Tracks={"pelvis","spine_01","spine_02","spine_03","spine_04","spine_05","neck_01","neck_02","head",
            "clavicle_l","upperarm_l","lowerarm_l","hand_l","clavicle_r","upperarm_r","lowerarm_r","hand_r","thigh_l","calf_l","foot_l","ball_l","thigh_r","calf_r","foot_r","ball_r"};

        /// <summary>The environment variable naming bank `name` (NativeIntroPack.Banks): CHIPARENA_INTRO_MOTION for the first, then
        /// CHIPARENA_INTRO_MOTION_MELEE, _RANGED and _ELEMENTAL.</summary>
        public static string EnvOf(string name)=>"CHIPARENA_INTRO_MOTION"+(name.Length>NativeIntroPack.Resource.Length?"_"+name.Substring(NativeIntroPack.Resource.Length).ToUpperInvariant():"");
        /// <summary>Bank `name`'s path here, or null: its variable (EnvOf), the folder CHIPARENA_INTRO_MOTION_DIR, the local assets'
        /// Resources copy, the framework's scratch copy (scratch/intro-framework/ up the tree).</summary>
        public static string LocalPath(string root,string name=NativeIntroPack.Resource)
        {
            var env=Environment.GetEnvironmentVariable(EnvOf(name));if(!string.IsNullOrEmpty(env)&&File.Exists(env))return env;
            var dir=Environment.GetEnvironmentVariable("CHIPARENA_INTRO_MOTION_DIR");if(!string.IsNullOrEmpty(dir)&&File.Exists(Path.Combine(dir,name+".bytes")))return Path.Combine(dir,name+".bytes");
            if(root!=null)
            {
                var res=Path.Combine(root,"Assets/LocalAssets/ChipArenaLocal/Resources/"+name+".bytes");if(File.Exists(res))return res;
                for(var d=new DirectoryInfo(root);d!=null;d=d.Parent){var s=Path.Combine(d.FullName,"scratch/intro-framework/"+name+".bytes");if(File.Exists(s))return s;}
            }
            return null;
        }
        /// <summary>The clip names repeated across the banks LoadLocal last read ("clip in A and B"; Run fails on any).</summary>
        public static readonly List<string> LocalDuplicates=new List<string>();
        /// <summary>Loads every local bank (NativeIntroPack.Banks), merged, into NativeIntroPack.Local, as the Runtime does (so the intros'
        /// pack segments play in the checks); false without any. `path` lists the files read.</summary>
        public static bool LoadLocal(string root,out string path)
        {
            var banks=new List<NativeIntroPack.Bank>();var paths=new List<string>();LocalDuplicates.Clear();
            foreach(var name in NativeIntroPack.Banks){var p=LocalPath(root,name);if(p==null)continue;banks.Add(NativeIntroPack.Read(File.ReadAllBytes(p),name));paths.Add(p);}
            NativeIntroPack.Local=NativeIntroPack.Merge(banks,LocalDuplicates);path=paths.Count==0?null:string.Join("; ",paths);return banks.Count>0;
        }

        // ---- the match's clips (owner: the intros' motion is their own, not the match's) ----
        /// <summary>The match's motion bank here (Resources/ChipArenaVisualMotion.bytes: CHIPARENA_VISUAL_MOTION, the local assets' copy,
        /// or the Editor checkout's, an ancestor's Chip-Arena-Unity), or null.</summary>
        public static string MatchBankPath(string root)
        {
            const string rel="Assets/LocalAssets/ChipArenaLocal/Resources/ChipArenaVisualMotion.bytes";
            var env=Environment.GetEnvironmentVariable("CHIPARENA_VISUAL_MOTION");if(!string.IsNullOrEmpty(env)&&File.Exists(env))return env;
            if(root==null)return null;if(File.Exists(Path.Combine(root,rel)))return Path.Combine(root,rel);
            for(var d=new DirectoryInfo(root);d!=null;d=d.Parent){var s=Path.Combine(d.FullName,"Chip-Arena-Unity/unity/ChipArenaConversion/"+rel);if(File.Exists(s))return s;}
            return null;
        }
        /// <summary>The source clip names the match plays: its motion bank's clip names, a mirrored clip's "~mirror" dropped.</summary>
        public static HashSet<string> MatchNames(IEnumerable<string> bankClips)
        {
            var set=new HashSet<string>(StringComparer.Ordinal);
            foreach(var n in bankClips){if(string.IsNullOrEmpty(n))continue;int m=n.IndexOf("~mirror",StringComparison.Ordinal);set.Add(m>=0?n.Substring(0,m):n);}
            return set;
        }
        /// <summary>The names a pack clip is known by: its bank name and that name before a '#' label, and from its bake source ("asset
        /// path|take|range") its take and its file's name.</summary>
        public static IEnumerable<string> SourceNames(NativeIntroPack.Clip c)
        {
            yield return c.Name;int hash=c.Name.IndexOf('#');if(hash>0)yield return c.Name.Substring(0,hash);
            if(string.IsNullOrEmpty(c.Source)||c.Source==c.Name)yield break;var parts=c.Source.Split('|');
            if(parts.Length>1&&parts[1].Length>0)yield return parts[1];if(parts[0].Length>0)yield return Path.GetFileNameWithoutExtension(parts[0]);
        }
        /// <summary>The clips of `bank` the match plays (matched by source clip name): "clip (as source name)".</summary>
        public static List<string> MatchPlayed(NativeIntroPack.Bank bank,HashSet<string> match)
        {
            var hits=new List<string>();if(bank==null||match==null)return hits;
            foreach(var c in bank.Clips.Values.OrderBy(c=>c.Name,StringComparer.Ordinal))
                foreach(var s in SourceNames(c))if(match.Contains(s)){hits.Add(c.Name+(s==c.Name?"":" (as "+s+")")+" in "+(c.Owner?.Name??bank.Name));break;}
            return hits;
        }
        public static string Root()
        {
            foreach(string start in new[]{Directory.GetCurrentDirectory(),AppContext.BaseDirectory})
                for(var dir=new DirectoryInfo(start);dir!=null;dir=dir.Parent)
                    if(File.Exists(Path.Combine(dir.FullName,"Assets/Conversion/Resources/GameContent.json")))return dir.FullName;
            return null;
        }

        public static void Run(string root)
        {
            count=0;var rig=ModelPoseChecks.Measured();
            float euler=Euler();float trip=RoundTrip(rig,out float tripAt,out float tripKeyed);
            string local="no local bank here";
            if(LoadLocal(root,out var path))
            {
                Check(LocalDuplicates.Count==0,"clip names are unique across the intro motion banks ("+string.Join("; ",LocalDuplicates)+")");local=Local(path);
                // (owner: the intros' motion is their own: no intro bank holds a clip the match's motion bank plays, by source clip name)
                var match=MatchBankPath(root);
                if(match==null)local+="; no match motion bank here, so the match's clips are not checked";
                else
                {
                    var played=MatchPlayed(NativeIntroPack.Local,MatchNames(NativeVisualMotionClips.Read(File.ReadAllBytes(match)).Clips.Keys));
                    foreach(var p in played)Console.WriteLine("  PACK FAIL the match plays "+p);
                    Check(played.Count==0,"no intro motion bank holds a clip the match plays ("+string.Join("; ",played)+")");local+="; none of them is a clip the match plays ("+match+")";
                }
            }
            Console.WriteLine("PASS intro pack: "+count+" checks (the keys' Euler angles round-trip, worst "+(euler*Deg).ToString("0.000")+" deg; the binding run backwards on the measured skeleton: the drawn body redrawn within "
                +trip.ToString("0.00")+" deg and "+(tripAt*1000).ToString("0.0")+" mm, the keyed joints within "+tripKeyed.ToString("0.00")+" deg; "+local+")");
        }

        // ---- the keys' angles ----
        private static float Euler()
        {
            var rnd=new Random(41);float worst=0;var near=Vector3.Zero;
            for(int i=0;i<4000;i++)
            {
                var axis=Vector3.Normalize(new Vector3((float)rnd.NextDouble()-.5f,(float)rnd.NextDouble()-.5f,(float)rnd.NextDouble()-.5f));var q=CapturedRig.Rot(axis,(float)(rnd.NextDouble()*2*Math.PI));
                if(i%7==0)q=CapturedRig.Euler((float)rnd.NextDouble()*2-1,(i%14==0?1:-1)*(float)Math.PI/2,(float)rnd.NextDouble()*2-1); // the gimbal
                var e=NativeIntroPack.Euler(q,near);var back=CapturedRig.Euler(e.X,e.Y,e.Z);worst=Math.Max(worst,Angle(q,back));near=e;
            }
            Check(worst<1e-3f,"Euler round trip within 0.06 deg (worst "+(worst*Deg).ToString("0.000")+")");
            // a run of keys stays continuous: a turn swept through the wrap keeps its angles within a step of each other
            var prev=NativeIntroPack.Euler(CapturedRig.Euler(0,0,3.0f));bool jump=false;
            for(int i=1;i<=60;i++){var e=NativeIntroPack.Euler(CapturedRig.Euler(0,0,3.0f+i*.01f),prev);jump|=(e-prev).Length()>.05f;prev=e;}
            Check(!jump,"Euler keys stay continuous through the wrap");
            return worst;
        }

        // ---- the round trip on the measured skeleton ----
        /// <summary>Keyed poses of a run (hips, knees, ankles, toes, clavicles, swinging arms, bent elbows, wrists past the ease's knee,
        /// the spine's bend and twist, the head, the body's bob), bound to the model, baked to a bank as the tool bakes, read back as keys,
        /// bound again: the worst drawn bone's turn (deg) and place (m), and the worst keyed joint (deg) against the keys.</summary>
        private static float RoundTrip(NativeVisualRig.Rig rig,out float worstAt,out float worstKeyed)
        {
            int n=rig.Count,frames=48;float rate=60;
            var names=rig.Names;var parents=rig.Parent;var pos=new Vector3[n];var rot=new Quaternion[n];
            for(int v=0;v<n;v++){var p=rig.RestPosition[v];if(parents[v]<0)p-=new Vector3(0,NativeVisualRig.Floor,0);pos[v]=CapturedRig.Mirror(p);rot[v]=CapturedRig.Mirror(rig.RestRotation[v]);}
            var tracks=Tracks.Select(t=>Array.IndexOf(names,t)).Where(i=>i>=0).ToArray();Check(tracks.Length==Tracks.Length&&names[tracks[0]]=="pelvis","the measured skeleton has every track");
            var st=new NativeVisualRig.State(rig);var keyedPoses=new List<CapturedRig.Pose>();var drawnWorld=new List<Quaternion[]>();var drawnAt=new List<Vector3[]>();
            int stride=3+4*tracks.Length;var data=new float[frames*stride];
            for(int f=0;f<frames;f++)
            {
                float ph=2*(float)Math.PI*f/frames,s=(float)Math.Sin(ph),c=(float)Math.Cos(ph);var p=new CapturedRig.Pose();
                p.Position[CapturedRig.Body]=new Vector3(.02f*s,-.06f+.03f*c,.01f*c);
                p.Rotation[CapturedRig.Pelvis]=CapturedRig.Euler(.05f,.12f*s,.04f*c);p.Rotation[CapturedRig.Spine]=CapturedRig.Euler(.12f,-.08f*s,0);p.Rotation[CapturedRig.Chest]=CapturedRig.Euler(.08f,-.15f*s,.03f*c);
                p.Rotation[CapturedRig.Neck]=CapturedRig.Euler(-.05f,.05f*s,0);p.Rotation[CapturedRig.Head]=CapturedRig.Euler(-.08f,.1f*c,.04f*s);
                p.Rotation[CapturedRig.LClav]=CapturedRig.Euler(0,.05f*s,.06f);p.Rotation[CapturedRig.RClav]=CapturedRig.Euler(0,-.05f*s,-.06f);
                p.Rotation[CapturedRig.LArm]=CapturedRig.Euler(.7f*s,.1f,.18f);p.Rotation[CapturedRig.RArm]=CapturedRig.Euler(-.7f*s,-.1f,-.18f);
                p.Rotation[CapturedRig.LElbow]=CapturedRig.Euler(-1.1f-.3f*c,.15f,0);p.Rotation[CapturedRig.RElbow]=CapturedRig.Euler(-1.1f+.3f*c,-.15f,0);
                p.Rotation[CapturedRig.LHand]=CapturedRig.Euler(.25f+.6f*Math.Max(0,s),.3f,-.2f);p.Rotation[CapturedRig.RHand]=CapturedRig.Euler(.25f+.6f*Math.Max(0,-s),-.3f,.2f);
                p.Rotation[CapturedRig.LHip]=CapturedRig.Euler(-.75f*s-.1f,.05f,.05f);p.Rotation[CapturedRig.RHip]=CapturedRig.Euler(.75f*s-.1f,-.05f,-.05f);
                p.Rotation[CapturedRig.LKnee]=CapturedRig.Euler(.4f+.7f*Math.Max(0,c),0,0);p.Rotation[CapturedRig.RKnee]=CapturedRig.Euler(.4f+.7f*Math.Max(0,-c),0,0);
                p.Rotation[CapturedRig.LFoot]=CapturedRig.Euler(-.2f*s,.03f,0);p.Rotation[CapturedRig.RFoot]=CapturedRig.Euler(.2f*s,-.03f,0);
                p.Rotation[CapturedRig.LToe]=CapturedRig.Euler(-.3f*Math.Max(0,s),0,0);p.Rotation[CapturedRig.RToe]=CapturedRig.Euler(-.3f*Math.Max(0,-s),0,0);
                p.World();NativeVisualRig.Pose(st,p,new NativeVisualRig.Grasp(),false,false);
                keyedPoses.Add(p);drawnWorld.Add((Quaternion[])st.World.Clone());drawnAt.Add((Vector3[])st.Position.Clone());
                int o=f*stride;var lp=CapturedRig.Mirror(st.LocalPosition[tracks[0]]);data[o]=lp.X;data[o+1]=lp.Y;data[o+2]=lp.Z;
                for(int i=0;i<tracks.Length;i++){var q=CapturedRig.Mirror(st.Local[tracks[i]]);int at=o+3+i*4;data[at]=q.X;data[at+1]=q.Y;data[at+2]=q.Z;data[at+3]=q.W;}
            }
            var bytes=NativeIntroPack.Write(names,parents,pos,rot,tracks,new[]{("round-trip",(frames-1)/rate,rate,Vector2.Zero,Vector2.Zero,true,frames,data)});
            var bank=NativeIntroPack.Read(bytes);Check(bank.TryGet("round-trip",out var clip)&&clip.Frames==frames,"the synthetic bank reads back");
            // the bank's skeleton is the measured one, as the binding reads it
            for(int v=0;v<n;v++)Check((bank.Skeleton.AlignedPosition[v]-rig.AlignedPosition[v]).Length()<1e-4f&&Angle(bank.Skeleton.AlignedWorld[v],rig.AlignedWorld[v])<1e-4f,"the bank's skeleton is the binding's ("+names[v]+")");
            float worst=0;worstAt=0;worstKeyed=0;var joints=new Quaternion[22];var again=new CapturedRig.Pose();var st2=new NativeVisualRig.State(bank.Skeleton);
            for(int f=0;f<frames;f++)
            {
                clip.Sample(f/rate,false,joints,out var body);again.Reset();for(int j=1;j<22;j++)again.Rotation[j]=joints[j];again.Position[CapturedRig.Body]=CapturedRig.Bones[CapturedRig.Body].Position+body;again.World();
                NativeVisualRig.Pose(st2,again,new NativeVisualRig.Grasp(),false,false);
                for(int v=0;v<n;v++){if(rig.Finger[v]!=0)continue;worst=Math.Max(worst,Angle(st2.World[v],drawnWorld[f][v])*Deg);worstAt=Math.Max(worstAt,(st2.Position[v]-drawnAt[f][v]).Length());}
                var k=keyedPoses[f];for(int j=1;j<22;j++)worstKeyed=Math.Max(worstKeyed,Angle(again.WorldRotation[j],k.WorldRotation[j])*Deg);
            }
            Check(worst<.5f,"the round trip redraws the body within 0.5 deg (worst "+worst.ToString("0.00")+")");
            Check(worstAt<.002f,"the round trip redraws the body within 2 mm (worst "+(worstAt*1000).ToString("0.0")+" mm)");
            Check(worstKeyed<.5f,"the round trip recovers the keyed joints within 0.5 deg (worst "+worstKeyed.ToString("0.00")+")");
            // and the sampler is allocation-free between frames
            long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<2000;i++)clip.Sample(i*.0013f,true,joints,out _);
            Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"sampling a pack clip allocates nothing");
            // a Kept segment carries the root by its weight's sum (never back, its blend-out included) and leaves it there after it ends
            var keep=NativeIntroPack.Local;var keepSpeed=clip.Speed;NativeIntroPack.Local=bank;clip.Speed=1;
            try
            {
                var k=new NativeEntranceKeys();k.Key(0,NativeEntranceKeys.Stand()).Root(0,.88f,0,0);k.Key(3,NativeEntranceKeys.Stand()).Root(0,.88f,0,0);
                k.FromPack("round-trip",1,2,0,1,true,NativeIntroPack.RootMode.Kept,NativeIntroPack.All,.2f,.2f);
                var kp=new CapturedRig.Pose();float last=-1,at2=0,at3=0;bool back=false;
                for(int i=0;i<=90;i++){NativeEntrancePose.Keyed(k,i/30f,kp,out var r,out _,ReadOnlySpan<float>.Empty);back|=r.Z<last-1e-4f;last=r.Z;if(i==60)at2=r.Z;if(i==90)at3=r.Z;}
                Check(!back,"a Kept segment never carries the root back (its blend-out included)");
                Check(Math.Abs(at3-.8f)<.02f&&Math.Abs(at3-at2)<1e-4f,"a Kept segment's travel is its weight's sum and stays after it ("+at3.ToString("0.000")+" m, want 0.8)");
                // and without the bank the keys play as keyed
                NativeIntroPack.Local=null;NativeEntrancePose.Keyed(k,1.5f,kp,out var none,out _,ReadOnlySpan<float>.Empty);Check(Math.Abs(none.Z)<1e-5f,"without the bank a segment does nothing");
            }
            finally{NativeIntroPack.Local=keep;clip.Speed=keepSpeed;}
            MultiBank(names,parents,pos,rot,tracks,frames,rate,data,bank,bytes);
            return worst;
        }

        // ---- several banks (the first and one per batch-2 author) ----
        /// <summary>Banks merged as the Runtime merges them: each clip keeps its own bank; a repeated name is listed and the first bank's
        /// plays; one bank is itself and none is none; a Sourced bank keeps each clip's bake source; a clip plays through FromPack the same
        /// from a merged bank as from its own, allocation-free; and a clip whose source the match plays is found by its take or its file.</summary>
        private static void MultiBank(string[] names,int[] parents,Vector3[] pos,Quaternion[] rot,int[] tracks,int frames,float rate,float[] data,NativeIntroPack.Bank bank,byte[] bytes)
        {
            var clips=new[]{("walk-b",(frames-1)/rate,rate,Vector2.Zero,Vector2.Zero,true,frames,data)};
            var second=NativeIntroPack.Read(NativeIntroPack.Write(names,parents,pos,rot,tracks,clips,new[]{"Assets/LocalAssets/Pack/Hero@Walk.FBX|Walk|0-47"}),"second");
            var again=NativeIntroPack.Read(bytes,"again");bank.Name??="first";
            Check(second.TryGet("walk-b",out var wb)&&wb.Source=="Assets/LocalAssets/Pack/Hero@Walk.FBX|Walk|0-47"&&bank.Clips["round-trip"].Source=="round-trip","a Sourced bank keeps each clip's bake source; a version 1 bank's is its name");
            Check(NativeIntroPack.Merge(new NativeIntroPack.Bank[0])==null&&NativeIntroPack.Merge(new[]{bank})==bank,"no bank merges to none, one bank to itself");
            var dups=new List<string>();var merged=NativeIntroPack.Merge(new[]{bank,second,again},dups);
            Check(merged.Clips.Count==2&&dups.Count==1&&dups[0].Contains("round-trip")&&merged.Clips["round-trip"].Owner==bank&&merged.Clips["walk-b"].Owner==second,
                "merged banks keep every clip with its own bank, and list a name two banks share (the first bank's plays): "+string.Join("; ",dups));
            // the same clip drawn through the merged bank as through its own, and allocation-free
            var keep=NativeIntroPack.Local;
            try
            {
                var k=new NativeEntranceKeys();k.Key(0,NativeEntranceKeys.Stand()).Root(0,.88f,0,0);k.Key(3,NativeEntranceKeys.Stand()).Root(.4f,.88f,1.2f,0);
                k.FromPack("round-trip",.5f,2.5f,0,1,true,NativeIntroPack.RootMode.Path,NativeIntroPack.All,.2f,.2f);
                var a=new CapturedRig.Pose();var b=new CapturedRig.Pose();bool same=true;
                for(int i=0;i<=90;i++)
                {
                    NativeIntroPack.Local=bank;NativeEntrancePose.Keyed(k,i/30f,a,out var ra,out _,ReadOnlySpan<float>.Empty);
                    NativeIntroPack.Local=merged;NativeEntrancePose.Keyed(k,i/30f,b,out var rb,out _,ReadOnlySpan<float>.Empty);
                    same&=ra==rb&&a.Position[CapturedRig.Body]==b.Position[CapturedRig.Body];for(int j=0;j<22;j++)same&=a.Rotation[j]==b.Rotation[j];
                }
                Check(same,"a clip plays through FromPack the same from the merged banks as from its own bank");
                long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<300;i++)NativeEntrancePose.Keyed(k,i/120f,b,out _,out _,ReadOnlySpan<float>.Empty);
                Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"a pack segment from the merged banks allocates nothing per frame");
            }
            finally{NativeIntroPack.Local=keep;}
            // the match's clips, by source clip name (its take or its file's name; "~mirror" dropped)
            var match=MatchNames(new[]{"Walk~mirror","Unarmed-Idle","M_Big_Sword@Idle"});
            var own=NativeIntroPack.Read(NativeIntroPack.Write(names,parents,pos,rot,tracks,new[]{clips[0],("idle-b",(frames-1)/rate,rate,Vector2.Zero,Vector2.Zero,true,frames,data),("Mvm_Dash#sprint",(frames-1)/rate,rate,Vector2.Zero,Vector2.Zero,true,frames,data)},
                new[]{"Assets/LocalAssets/Pack/Hero@Walk.FBX|Walk|0-47","Assets/LocalAssets/G/M_Big_Sword@Idle.fbx|Take 001|","Assets/LocalAssets/CLazy/CLazy@Mvm_Dash.FBX|Mvm_Dash|0-31"}),"own");
            var hits=MatchPlayed(own,match);
            Check(hits.Count==2&&hits[0].StartsWith("idle-b",StringComparison.Ordinal)&&hits[1].StartsWith("walk-b",StringComparison.Ordinal),"a clip the match plays is found by its take or its file's name, and only those ("+string.Join("; ",hits)+")");
            Check(MatchPlayed(bank,match).Count==0,"a clip the match does not play passes");
        }

        // ---- the local bank ----
        private static string Local(string path)
        {
            var bank=NativeIntroPack.Local;Check(bank.Clips.Count>0,"the local bank ("+path+") has clips");
            NativeIntroPack.Bank owner=null;NativeVisualRig.Rig sk=null;var world=new Quaternion[0];var at=new Vector3[0];NativeVisualRig.State st=null;var joints=new Quaternion[22];var pose=new CapturedRig.Pose();
            var lines=new List<string>();float worstLimb=0,worstTurn=0,worstAnkle=0;string worstWhere="";var fails=new List<(string clip,bool ok,string what)>();
            foreach(var c in bank.Clips.Values.OrderBy(c=>c.Name,StringComparer.Ordinal))
            {
                // (each clip on its own bank's skeleton: the banks may be baked apart)
                if((c.Owner??bank)!=owner){owner=c.Owner??bank;sk=owner.Skeleton;world=new Quaternion[sk.Count];at=new Vector3[sk.Count];st=new NativeVisualRig.State(sk);}
                float limb=0,turnErr=0,ankle=0;string where="",turnWhere="";
                for(int f=0;f<c.Frames;f++)
                {
                    bank.Model(c,f,world,at);c.Sample(f/c.Rate,false,joints,out var body);pose.Reset();for(int j=1;j<22;j++)pose.Rotation[j]=joints[j];
                    pose.Position[CapturedRig.Body]=CapturedRig.Bones[CapturedRig.Body].Position+body;pose.World();NativeVisualRig.Pose(st,pose,new NativeVisualRig.Grasp{Exact=1},false,false);
                    foreach(var (name,joint,child) in Held)
                    {
                        int v=sk.Of[joint];if(v<0)continue;
                        if(child>=0){int vc=sk.Of[child];if(vc<0)continue;float e=Angle(st.Position[vc]-st.Position[v],at[vc]-at[v])*Deg;if(e>limb){limb=e;where=name+" at "+(f/c.Rate).ToString("0.00")+" s (inside "+st.LeftInside.ToString("0.00")+"/"+st.RightInside.ToString("0.00")+", clear "+(st.LeftClear*100).ToString("0")+"/"+(st.RightClear*100).ToString("0")+" cm)";}}
                        else{float e=Angle(st.World[v],world[v])*Deg;if(e>turnErr){turnErr=e;turnWhere=name+" at "+(f/c.Rate).ToString("0.00")+" s";}}
                    }
                    foreach(int foot in new[]{CapturedRig.LFoot,CapturedRig.RFoot}){int v=sk.Of[foot];if(v>=0)ankle=Math.Max(ankle,(st.Position[v]-at[v]).Length());}
                }
                fails.Add((c.Name,limb<=2,c.Name+": its keys draw every limb along the clip on the model (worst "+limb.ToString("0.0")+" deg, "+where+")"));
                fails.Add((c.Name,turnErr<=5,c.Name+": its keys turn the pelvis, chest, head and hands as the clip does (worst "+turnErr.ToString("0.0")+" deg, "+turnWhere+")"));
                fails.Add((c.Name,ankle<=.015f,c.Name+": its keys put the drawn ankles where the clip's are (worst "+(ankle*100).ToString("0.0")+" cm)"));
                if(limb>worstLimb){worstLimb=limb;worstWhere=c.Name+" "+where;}worstTurn=Math.Max(worstTurn,turnErr);worstAnkle=Math.Max(worstAnkle,ankle);
                // each clip through FromPack with its own travel: inside the limits, the feet held where they are down and out of the floor
                var s=new PackPose(c);var limits=IntroPoseChecks.Limits("pack "+c.Name,s,s.Seconds,null,packed:false);var feet=IntroPoseChecks.Feet(new PackPose(c),s.Seconds,ModelPoseChecks.Measured());
                // (a clip that keeps a foot on the floor a sixth of the time or more is grounded locomotion: its feet must hold on its own
                // travel; one that does not, a fall or a hovering boost, has its feet listed only)
                int down=0;for(int f=0;f<c.Frames;f++){float tt=f/c.Rate;if(c.OnFloor(tt,false,true)||c.OnFloor(tt,false,false))down++;}bool grounded=down>=c.Frames/6f&&!c.Name.Contains("Slide");
                if(grounded){fails.Add((c.Name,feet.Slide<=IntroPoseChecks.SlideMost,c.Name+": its feet hold on the floor on its own travel (slide "+feet.Slide.ToString("0.00")+" m/s at "+feet.SlideAt.ToString("0.00")+" s)"));
                    fails.Add((c.Name,feet.Deepest<=IntroPoseChecks.Through,c.Name+": nothing of it goes through the floor ("+(feet.Deepest*100).ToString("0")+" cm, the "+feet.DeepBone+")"));}
                lines.Add(c.Name+" "+c.Length.ToString("0.00")+" s "+c.Frames+" frames, speed "+c.Speed.ToString("0.00")+" m/s: limbs "+limb.ToString("0.0")+" deg, turns "+turnErr.ToString("0.0")+" deg, ankles "+(ankle*100).ToString("0.0")+" cm; limits "
                    +(limits.Count==0?"clean":limits.Count+" (keyed heuristics, not held: "+string.Join("; ",limits.Take(3).Select(f=>f.Joint+" "+f.What+" "+f.Worst.ToString("0")+f.Unit))+")")+"; feet slide "+feet.Slide.ToString("0.00")+" m/s, deepest "+(feet.Deepest*100).ToString("0")+" cm"+(grounded?"":" (not grounded: listed)")+", a foot down "+(100*down/c.Frames)+"% of frames");
            }
            foreach(var l in lines)Console.WriteLine("  PACK "+l);
            if(Environment.GetEnvironmentVariable("CHIPARENA_PACK_DUMP") is string dump&&bank.TryGet(dump,out var dc))
                for(int f=0;f<dc.Frames;f++){float tt=f/dc.Rate;dc.Foot(tt,false,true,out var la,out var lb);dc.Foot(tt,false,false,out var ra,out var rb);Console.WriteLine("  DUMP "+f+" down "+(dc.OnFloor(tt,false,true)?"L":"-")+(dc.OnFloor(tt,false,false)?"R":"-")+" L ankle "+la.Y.ToString("0.000")+" "+la.Z.ToString("0.000")+" ball "+lb.Y.ToString("0.000")+" "+lb.Z.ToString("0.000")+" | R ankle "+ra.Y.ToString("0.000")+" "+ra.Z.ToString("0.000")+" ball "+rb.Y.ToString("0.000")+" "+rb.Z.ToString("0.000"));}
            foreach(var f in fails)if(!f.ok)Console.WriteLine("  PACK FAIL "+f.what);
            foreach(var f in fails)Check(f.ok,f.what);
            return "the local bank, "+bank.Clips.Count+" clips: limbs within "+worstLimb.ToString("0.0")+" deg ("+worstWhere+"), turns "+worstTurn.ToString("0.0")+" deg, ankles "+(worstAnkle*100).ToString("0.0")+" cm";
        }

        /// <summary>A clip on its own through FromPack: a stand key, the clip over it whole (looped twice if it loops), the root on the clip's
        /// own travel; a slide's or a fall's feet may move (FeetSliding).</summary>
        internal sealed class PackPose:NativeIntroV2Script
        {
            public readonly float Seconds;private readonly bool sliding;
            public PackPose(NativeIntroPack.Clip c):base("pack-"+c.Name,Math.Max(.2f,c.Length*(c.Loop?2:1)),Math.Max(.2f,c.Length),Math.Max(.1f,c.Length*.5f),0x9fd8ff)
            {
                Seconds=Math.Max(.2f,c.Length*(c.Loop?2:1));sliding=c.Name.Contains("Slide");
                var k=new NativeEntranceKeys();k.Key(0,NativeEntranceKeys.Stand()).Root(0,.88f,0,0);k.Key(Seconds,NativeEntranceKeys.Stand()).Root(0,.88f,0,0);
                k.FromPack(c.Name,0,Seconds+1,0,1,c.Loop,NativeIntroPack.RootMode.Kept,NativeIntroPack.All,0,0);Keys=k;
            }
            protected override float HeroAt=>Seconds+10;
            public override bool FeetSliding(float t)=>sliding;
        }

        // (the turn between two turns, precise when it is small: from the relative turn's axis part, not the dot's arc cosine)
        private static float Angle(Quaternion a,Quaternion b){var q=Quaternion.Normalize(Quaternion.Inverse(Quaternion.Normalize(a))*Quaternion.Normalize(b));float s=new Vector3(q.X,q.Y,q.Z).Length();return 2*(float)Math.Atan2(s,Math.Abs(q.W));}
        private static float Angle(Vector3 a,Vector3 b){float la=a.Length(),lb=b.Length();if(la<1e-6f||lb<1e-6f)return 0;return (float)Math.Acos(Math.Max(-1,Math.Min(1,Vector3.Dot(a,b)/(la*lb))));}
    }
}
