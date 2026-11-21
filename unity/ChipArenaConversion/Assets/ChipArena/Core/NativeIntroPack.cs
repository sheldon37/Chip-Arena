using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Intros v2's pack clips (docs/INTROS-V2.md 9d). An animation pack's humanoid clips are baked by the local tool
    /// (bozo-intromotion.request) on the baked body, as bone rotations, into the intros' own banks (Resources/ChipArenaIntroMotion.bytes
    /// and one per batch-2 author, Banks: local, never committed, never the match's motion bank). Here they become keyed poses, so an intro keys a pack clip
    /// (NativeEntranceKeys.FromPack) without the match's animation system: no Clip(), no aim, turn or foot-lock layer.
    /// Each frame is the model binding (NativeVisualRig.Pose) run backwards on the bank's own skeleton: a keyed joint's turn is its chain's
    /// last bone's world turn off the aligned rest, less the chain's start (the arms' spread and the wrist's eased bend undone), so the keyed
    /// pose drawn on the model is the clip's pose on the model; the pelvis's offset from its rest goes on the keyed body over the legs'
    /// ratio. Frames are converted once, when the bank is read; sampling is allocation-free and pure in time.
    /// </summary>
    public static class NativeIntroPack
    {
        /// <summary>The bank format Write writes ("CAIM" 1); Read also reads Sourced (2: each clip with its bake source, I0e's bake).</summary>
        public const int Version=1,Sourced=2;
        /// <summary>The first bank's resource name (Resources/ChipArenaIntroMotion.bytes in the git-ignored local assets: Slipstream's).</summary>
        public const string Resource="ChipArenaIntroMotion";
        /// <summary>
        /// Every intro motion bank, in the order read (docs/INTROS-V2.md 9d): the first (Slipstream's and the generic intros') and one per
        /// batch-2 author (I3 melee, I4 ranged, I5 elemental), so nobody rebakes another's. Each is optional; a clip name is unique across
        /// them all (Merge lists a duplicate, and the first bank's clip plays).
        /// </summary>
        public static readonly string[] Banks={Resource,Resource+"Melee",Resource+"Ranged",Resource+"Elemental"};
        /// <summary>The banks the presentation read, merged (Runtime sets it; null without one: pack segments then leave the keys as they are).</summary>
        public static Bank Local;

        // ---- joint masks (bits of keyed rig joints) ----
        private const int Body=CapturedRig.Body,Pelvis=CapturedRig.Pelvis,LeftHip=CapturedRig.LHip,RightHip=CapturedRig.RHip;
        public const int Torso=1<<CapturedRig.Spine|1<<CapturedRig.Chest|1<<CapturedRig.Neck|1<<CapturedRig.Head;
        public const int LeftArm=1<<CapturedRig.LClav|1<<CapturedRig.LArm|1<<CapturedRig.LElbow|1<<CapturedRig.LHand;
        public const int RightArm=1<<CapturedRig.RClav|1<<CapturedRig.RArm|1<<CapturedRig.RElbow|1<<CapturedRig.RHand;
        public const int Arms=LeftArm|RightArm;
        /// <summary>The legs and the hips: the pelvis's turn and place, both legs to the toes.</summary>
        public const int Legs=1<<Pelvis|1<<CapturedRig.LHip|1<<CapturedRig.LKnee|1<<CapturedRig.LFoot|1<<CapturedRig.LToe|1<<CapturedRig.RHip|1<<CapturedRig.RKnee|1<<CapturedRig.RFoot|1<<CapturedRig.RToe;
        public const int All=Torso|Arms|Legs;

        /// <summary>Where the root goes while a segment plays: Path (the intro's: its keys or RootPath; travel at the clip's Speed for the
        /// drawn feet to hold) or Kept (the clip's own travel: from the root at the segment's start, along the root's facing, at Speed by
        /// the segment's weight; the root stays where it took it after the segment ends).</summary>
        public enum RootMode:byte{Path,Kept}
        /// <summary>
        /// What a segment that plays the pelvis (its mask has it: All, Legs) does with the keys' body turn under it (joint 0, the motion
        /// root's turn: a keyed lean): Keyed (the default, as before: it turns the whole clip, its legs and feet too), Clip (it eases out by
        /// the segment's weight, so the clip stands as baked: the clip's own lean is in its pelvis and spine), or Pelvis (it rides the
        /// pelvis: the clip's hips and everything above them lean by it, and the legs keep the clip's turns, so the feet stay where the clip
        /// puts them). I2b's note on Slipstream: a keyed 20 degree lean tilted the whole clip.
        /// </summary>
        public enum BodyTurn:byte{Keyed,Clip,Pelvis}

        private const int Joints=22,Points=4,Stride=Joints*4+3+Points*3; // per frame: the joints' turns, the keyed body's offset, the drawn ankles and balls

        public sealed class Clip
        {
            public string Name;public float Length,Rate;public int Frames;public bool Loop;
            /// <summary>Where the bake took it: "asset path|take|first-last frame" (a Sourced bank); a version 1 bank's is its Name.</summary>
            public string Source;
            /// <summary>The bank it was read from (its skeleton and converter), also inside a merged bank.</summary>
            public Bank Owner;
            /// <summary>The clip's own travel as baked (m/s, the model's, rig frame x and z): its pelvis's drift over the clip (an in-place
            /// clip's is about zero). The bank takes it out, so the clip samples in place.</summary>
            public Vector2 Travel;
            /// <summary>The clip's own average speed as Unity reports it (m/s, x and z; its root motion), for reference.</summary>
            public Vector2 Average;
            /// <summary>How fast (m/s, the model's) a foot on the floor moves back under the body as sampled (in place): the speed the root
            /// must travel along its facing for the drawn feet to hold. Zero when no foot is down.</summary>
            public float Speed;
            internal float[] Data,Raw;internal byte[] Down;internal int RawStride;
            /// <summary>The clip's time for `time` seconds in: looped, or held at its ends.</summary>
            public float Time(float time,bool loop)
            {
                if(loop&&Length>1e-4f){time%=Length;if(time<0)time+=Length;return time;}
                return time<0?0:time>Length?Length:time;
            }
            private void Frame(float time,bool loop,out int a,out int b,out float u)
            {
                float f=Time(time,loop)*Rate;int last=Frames-1;
                a=(int)Math.Floor(f);u=f-a;if(a>=last){a=last;b=last;u=0;return;}if(a<0){a=0;b=0;u=0;return;}b=a+1;
            }
            /// <summary>Clip time `time`: each keyed joint's local turn (22, CapturedRig's order; the motion root's identity) and the keyed
            /// body's offset from its rest (the drawn pelvis's over NativeVisualRig.Rig.LegRatio; rig frame).</summary>
            public void Sample(float time,bool loop,Span<Quaternion> joints,out Vector3 body)
            {
                Frame(time,loop,out int a,out int b,out float u);int oa=a*Stride,ob=b*Stride;
                for(int j=0;j<Joints;j++)
                {
                    int i=j*4;var qa=new Quaternion(Data[oa+i],Data[oa+i+1],Data[oa+i+2],Data[oa+i+3]);var qb=new Quaternion(Data[ob+i],Data[ob+i+1],Data[ob+i+2],Data[ob+i+3]);
                    joints[j]=u<=0?qa:Quaternion.Slerp(qa,qb,u);
                }
                int o=Joints*4;body=Vector3.Lerp(new Vector3(Data[oa+o],Data[oa+o+1],Data[oa+o+2]),new Vector3(Data[ob+o],Data[ob+o+1],Data[ob+o+2]),u);
            }
            /// <summary>Clip time `time`: the drawn ankle and ball of foot `left` (rig frame, the model's metres; the floor at
            /// NativeVisualRig.Floor), for planning the feet in the model's space.</summary>
            public void Foot(float time,bool loop,bool left,out Vector3 ankle,out Vector3 ball)
            {
                Frame(time,loop,out int a,out int b,out float u);int o=Joints*4+3+(left?0:6);
                ankle=Vector3.Lerp(new Vector3(Data[a*Stride+o],Data[a*Stride+o+1],Data[a*Stride+o+2]),new Vector3(Data[b*Stride+o],Data[b*Stride+o+1],Data[b*Stride+o+2]),u);o+=3;
                ball=Vector3.Lerp(new Vector3(Data[a*Stride+o],Data[a*Stride+o+1],Data[a*Stride+o+2]),new Vector3(Data[b*Stride+o],Data[b*Stride+o+1],Data[b*Stride+o+2]),u);
            }
            /// <summary>Whether foot `left` is on the floor at clip time `time` (its ankle or ball within a few centimetres of its rest height,
            /// not moving forward under the body).</summary>
            public bool OnFloor(float time,bool loop,bool left){Frame(time,loop,out int a,out _,out _);return (Down[a]&(left?1:2))!=0;}
            /// <summary>Foot `left`'s time on the floor around clip time `time`: false while it is up; else how long (clip seconds) since it came
            /// down and until it lifts (within this pass of the clip).</summary>
            public bool Contact(float time,bool loop,bool left,out float since,out float until)
            {
                Frame(time,loop,out int a,out _,out float u);since=until=0;int side=left?0:1;if((Down[a]&(1<<side))==0)return false;
                since=(a-From[a*2+side]+u)/Rate;until=Math.Max(0,(To[a*2+side]-a-u)/Rate);return true;
            }
            internal int[] From,To;
        }

        public sealed class Bank
        {
            public readonly Dictionary<string,Clip> Clips=new Dictionary<string,Clip>(StringComparer.Ordinal);
            /// <summary>The bank's skeleton as the model binding reads it (NativeVisualRigBinding); a merged bank's is its first bank's.</summary>
            public NativeVisualRig.Rig Skeleton;
            /// <summary>Its resource name or file, as its loader names it (for messages).</summary>
            public string Name;
            internal Converter Conv;
            public bool TryGet(string name,out Clip clip){clip=null;return name!=null&&Clips.TryGetValue(name,out clip);}
            /// <summary>Frame `frame` of `clip` as baked on the model: every bone's world turn and place (rig frame, the model's metres;
            /// arrays of the clip's own bank's Skeleton.Count), in place (the drift taken out). For checks and for planning feet on the model;
            /// main thread.</summary>
            public void Model(Clip clip,int frame,Quaternion[] world,Vector3[] at)
            {
                var conv=(clip.Owner??this).Conv;frame=Math.Max(0,Math.Min(clip.Frames-1,frame));conv.Pose(clip.Raw,frame*clip.RawStride);conv.Copy(world,at);
            }
        }
        /// <summary>
        /// The banks read, as one (Local): null without any, the bank itself when there is one, else a bank holding every clip, each clip
        /// keeping its own bank (Owner: its skeleton for its feet and its frames). A clip name already taken by an earlier bank is left out
        /// and listed in `duplicates` ("clip in A and B"): clip names must be unique across the banks (IntroPackChecks fails on one).
        /// </summary>
        public static Bank Merge(IReadOnlyList<Bank> banks,List<string> duplicates=null)
        {
            if(banks==null||banks.Count==0)return null;
            var all=banks.Count==1?banks[0]:new Bank{Skeleton=banks[0].Skeleton,Conv=banks[0].Conv,Name=string.Join("+",System.Linq.Enumerable.Select(banks,b=>b.Name))};
            for(int i=0;i<banks.Count;i++)
                foreach(var c in banks[i].Clips.Values)
                {
                    if(ReferenceEquals(all,banks[i]))continue;
                    if(all.Clips.TryGetValue(c.Name,out var had)){duplicates?.Add(c.Name+" in "+(had.Owner?.Name??"?")+" and "+banks[i].Name);continue;}
                    all.Clips[c.Name]=c;
                }
            // (a single bank's own clips cannot repeat a name; a duplicate needs two banks)
            return all;
        }

        /// <summary>A segment of keys played from a pack clip (NativeEntranceKeys.FromPack).</summary>
        public sealed class Segment
        {
            public readonly string Clip;public readonly float From,To,Start,Speed,BlendIn,BlendOut;public readonly bool Loop;public readonly RootMode Root;public readonly int Mask;
            private Bank resolvedIn;private Clip clip;
            /// <summary>Whether a foot the clip has on the floor is held where it came down against the intro's root (the legs' mask only).</summary>
            public readonly bool Hold;
            /// <summary>What it does with the keys' body turn (BodyTurn; with the pelvis in its mask).</summary>
            public readonly BodyTurn Body;
            public Segment(string clip,float from,float to,float start,float speed,bool loop,RootMode root,int mask,float blendIn,float blendOut,bool hold=true,BodyTurn body=BodyTurn.Keyed)
            {
                Clip=clip;From=from;To=to;Start=start;Speed=speed;Loop=loop;Root=root;Mask=mask;BlendIn=Math.Max(0,blendIn);BlendOut=Math.Max(0,blendOut);Hold=hold&&(mask&Legs)==Legs;Body=body;
            }
            /// <summary>The clip in the local bank (null without it).</summary>
            public Clip Resolve(){var bank=Local;if(bank!=resolvedIn){resolvedIn=bank;clip=null;bank?.TryGet(Clip,out clip);}return clip;}
            /// <summary>The segment's weight at intro time `t`: eased in from From, out to To.</summary>
            public float Weight(float t)
            {
                if(t<From||t>To)return 0;float w=1;
                if(BlendIn>0)w=Math.Min(w,NativeEntranceMath.Smooth((t-From)/BlendIn));if(BlendOut>0)w=Math.Min(w,NativeEntranceMath.Smooth((To-t)/BlendOut));
                return w;
            }
            /// <summary>The clip's time at intro time `t`.</summary>
            public float ClipTime(float t)=>Start+(t-From)*Speed;
            /// <summary>The segment's weight summed over intro time from From to `t` (seconds at full weight; it stops growing at To): how far
            /// a Kept segment has carried the root, over the clip's Speed.</summary>
            public float Travelled(float t)
            {
                float end=Math.Min(t,To),span=end-From;if(span<=0)return 0;const int steps=32;float sum=0,dt=span/steps;
                for(int i=0;i<steps;i++)sum+=Weight(From+(i+.5f)*dt);return sum*dt;
            }
        }

        [ThreadStatic] private static Quaternion[] scratch;
        /// <summary>The share of `mask`'s joints the keys' pack segments take at `t` (0 with no segment or no bank).</summary>
        public static float Weight(NativeEntranceKeys k,float t,int mask)
        {
            float w=0;foreach(var s in k.Packs)if((s.Mask&mask)!=0&&s.Resolve()!=null)w=Math.Max(w,s.Weight(t));return w;
        }
        /// <summary>
        /// Plays the keys' pack segments into `pose` at intro time `t` (NativeEntrancePose.Keyed calls it, after the keys, the living hold and
        /// the planting): each segment's joints (its mask) eased from what the keys put there to the clip's by the segment's weight, the keyed
        /// body put where the clip's pelvis is, and a Kept segment's root moved on along the facing at the clip's Speed. No lag (the keys'
        /// kinetic chain) and no idle on a segment: the clip is the clip.
        /// </summary>
        public static void Apply(NativeEntranceKeys k,float t,CapturedRig.Pose pose,ref Vector3 root,Quaternion rotation,Func<float,Vector3,Vector3> rootAt=null)
        {
            var joints=scratch??=new Quaternion[Joints];
            foreach(var s in k.Packs)
            {
                // (a Kept segment's travel: the root carried on along the facing at the clip's Speed by its weight, and left there after it)
                if(s.Root==RootMode.Kept&&t>s.From){var kc=s.Resolve();if(kc!=null)root+=Vector3.Transform(new Vector3(0,0,kc.Speed*s.Speed*s.Travelled(t)),rotation);}
                float w=s.Weight(t);if(w<=0)continue;var c=s.Resolve();if(c==null)continue;
                c.Sample(s.ClipTime(t),s.Loop,joints,out var body);
                for(int j=1;j<Joints;j++)
                {
                    if((s.Mask&1<<j)==0)continue;
                    pose.Rotation[j]=w>=.9999f?joints[j]:Quaternion.Normalize(Quaternion.Slerp(pose.Rotation[j],joints[j],w));
                }
                if((s.Mask&1<<Pelvis)!=0)pose.Position[Body]=Vector3.Lerp(pose.Position[Body],CapturedRig.Bones[Body].Position+body,w);
                if(s.Body!=BodyTurn.Keyed&&(s.Mask&1<<Pelvis)!=0)
                {
                    // (the keys' body turn eased out by the weight; with Pelvis what was taken off rides the pelvis, and the hips give it back)
                    var keyed=pose.Rotation[Body];var kept=w>=.9999f?Quaternion.Identity:Quaternion.Normalize(Quaternion.Slerp(keyed,Quaternion.Identity,w));pose.Rotation[Body]=kept;
                    if(s.Body==BodyTurn.Pelvis)
                    {
                        var was=pose.Rotation[Pelvis];pose.Rotation[Pelvis]=Quaternion.Normalize(Quaternion.Inverse(kept)*keyed*was);
                        var back=Quaternion.Normalize(Quaternion.Inverse(pose.Rotation[Pelvis])*was);
                        pose.Rotation[LeftHip]=Quaternion.Normalize(back*pose.Rotation[LeftHip]);pose.Rotation[RightHip]=Quaternion.Normalize(back*pose.Rotation[RightHip]);
                    }
                }
                if(s.Hold)HoldFeet(k,s,c,t,pose,root,rotation,rootAt,w);
            }
        }
        /// <summary>The root and its turn at intro time `t` as NativeEntrancePose.Keyed makes them (with every Kept segment's travel).</summary>
        private static void RootAt(NativeEntranceKeys k,Segment s,Clip c,float t,Func<float,Vector3,Vector3> rootAt,out Vector3 root,out Quaternion rotation)
        {
            root=new Vector3(k.At(NativeEntranceKeys.RootX,t),k.At(NativeEntranceKeys.RootY,t),k.At(NativeEntranceKeys.RootZ,t));if(rootAt!=null)root=rootAt(t,root);
            rotation=Quaternion.CreateFromYawPitchRoll(k.At(NativeEntranceKeys.Yaw,t),k.At(NativeEntranceKeys.Pitch,t),k.At(NativeEntranceKeys.Roll,t));
            foreach(var o in k.Packs)if(o.Root==RootMode.Kept&&t>o.From){var oc=o.Resolve();if(oc!=null)root+=Vector3.Transform(new Vector3(0,0,oc.Speed*o.Speed*o.Travelled(t)),rotation);}
        }
        /// <summary>
        /// The drawn feet held where they came down: a foot the clip has on the floor is kept where it landed, against the intro's root (the
        /// slip a path makes that does not travel at the clip's Speed), by the keyed leg as a hinge (NativeEntrancePose.HingedLeg; the drawn
        /// leg is the keyed one LegRatio times as long from the same turns), its foot turned as the clip turns it; eased out over the last 30 ms before it lifts.
        /// </summary>
        private static void HoldFeet(NativeEntranceKeys k,Segment s,Clip c,float t,CapturedRig.Pose pose,Vector3 root,Quaternion rotation,Func<float,Vector3,Vector3> rootAt,float w)
        {
            float tau=s.ClipTime(t),speed=Math.Max(1e-3f,Math.Abs(s.Speed));var inv=Quaternion.Inverse(rotation);bool posed=false;
            for(int side=0;side<2;side++)
            {
                bool left=side==0;if(!c.Contact(tau,s.Loop,left,out float since,out float until))continue;
                float t0=Math.Max(s.From,t-since/speed);float down=tau-(t-t0)*s.Speed;
                RootAt(k,s,c,t0,rootAt,out var r0,out var q0);c.Foot(down,s.Loop,left,out var a0,out _);c.Foot(tau,s.Loop,left,out var a1,out _);
                var slip=r0+Vector3.Transform(a0,q0)-(root+Vector3.Transform(a1,rotation));slip.Y=0;
                float ease=w*NativeEntranceMath.Smooth(until/.03f);if(slip.LengthSquared()*ease*ease<1e-6f)continue;
                if(!posed){pose.World();posed=true;}
                int hip=left?CapturedRig.LHip:CapturedRig.RHip,knee=hip+1,foot=hip+2;var keep=pose.WorldRotation[foot];
                var target=pose.WorldPosition[foot]+Vector3.Transform(slip*ease,inv)/(c.Owner??Local).Skeleton.LegRatio;
                var f=Vector3.Transform(Vector3.UnitZ,keep);NativeEntrancePose.HingedLeg(pose,left,target,(float)Math.Atan2(f.X,f.Z));
                pose.World();pose.Rotation[foot]=Quaternion.Normalize(Quaternion.Inverse(pose.WorldRotation[knee])*keep);pose.World();
            }
        }

        // ---- the bank ----
        /// <summary>Reads a bank ("CAIM" version 1 or Sourced, written by the local tool's bozo-intromotion.request; see Write) and converts
        /// every frame to keyed joints on the bank's skeleton. `name` names it in messages (Bank.Name).</summary>
        public static Bank Read(byte[] bytes,string name=null)
        {
            using var r=new BinaryReader(new MemoryStream(bytes),Encoding.UTF8);
            if(r.ReadByte()!='C'||r.ReadByte()!='A'||r.ReadByte()!='I'||r.ReadByte()!='M')throw new InvalidDataException("not an intro motion bank");
            int version=r.ReadInt32();if(version!=Version&&version!=Sourced)throw new InvalidDataException("intro motion bank version");
            int n=r.ReadInt32();if(n<2||n>1024)throw new InvalidDataException("intro motion bank skeleton");
            var names=new string[n];var parents=new int[n];var positions=new Vector3[n];var rotations=new Quaternion[n];
            for(int v=0;v<n;v++)
            {
                names[v]=Name(r);parents[v]=r.ReadInt32();if(parents[v]>=v)throw new InvalidDataException("intro motion bank bone order");
                var p=new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());var q=new Quaternion(r.ReadSingle(),r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
                // (as NativeVisualRigBinding reads a skeleton: the root against the body's floor point, into the source frame)
                if(parents[v]<0)p+=new Vector3(0,NativeVisualRig.Floor,0);
                positions[v]=CapturedRig.Mirror(p);rotations[v]=CapturedRig.Mirror(Quaternion.Normalize(q));
            }
            int tracks=r.ReadInt32();if(tracks<1||tracks>n)throw new InvalidDataException("intro motion bank tracks");
            var track=new int[tracks];for(int i=0;i<tracks;i++){track[i]=r.ReadInt32();if(track[i]<0||track[i]>=n)throw new InvalidDataException("intro motion bank track");}
            var bank=new Bank{Skeleton=new NativeVisualRig.Rig(NativeVisualRig.Mannequin(names,parents,positions,rotations)),Name=name};
            var conv=bank.Conv=new Converter(bank.Skeleton,positions,rotations,track);
            int count=r.ReadInt32();if(count<0||count>4096)throw new InvalidDataException("intro motion bank clips");
            int raw=3+4*tracks;
            for(int k=0;k<count;k++)
            {
                var c=new Clip{Name=Name(r),Owner=bank};c.Source=version>=Sourced?Name(r):c.Name;c.Length=r.ReadSingle();c.Rate=r.ReadSingle();
                c.Travel=new Vector2(-r.ReadSingle(),r.ReadSingle());c.Average=new Vector2(-r.ReadSingle(),r.ReadSingle());
                c.Loop=r.ReadByte()!=0;c.Frames=r.ReadInt32();
                if(c.Frames<2||c.Frames>100000||!(c.Rate>0)||!(c.Length>=0))throw new InvalidDataException("intro motion bank clip "+c.Name);
                c.Data=new float[c.Frames*Stride];c.Down=new byte[c.Frames];c.RawStride=raw;c.Raw=new float[c.Frames*raw];
                for(int i=0;i<c.Raw.Length;i++)c.Raw[i]=r.ReadSingle();
                for(int f=0;f<c.Frames;f++){conv.Pose(c.Raw,f*raw);conv.Frame(c.Data,f*Stride);}
                conv.Contacts(c);bank.Clips[c.Name]=c;
            }
            return bank;
        }
        private static string Name(BinaryReader r){int len=r.ReadInt32();if(len<0||len>4096)throw new InvalidDataException("intro motion bank name");return Encoding.UTF8.GetString(r.ReadBytes(len));}

        /// <summary>
        /// Writes a bank in the tool's format (the tests' synthetic banks; the local tool writes the same bytes): "CAIM", version 1; the bones
        /// (count; per bone its name, parent, rest position xyz and rotation xyzw in Unity's frame as NativeVisualRigBinding reads them: the
        /// skeleton root's against the body's root, the rest local); the tracks (count; bone indices, the pelvis first); the clips (count; per
        /// clip its name, length, rate, travel x and z, average speed x and z (m/s, Unity's frame), loop byte, frame count, then per frame the
        /// pelvis's local position xyz and every track's local rotation xyzw, Unity's frame). With `sources` (one per clip) it is version
        /// Sourced, each clip's source string ("asset path|take|first-last") right after its name, as the I0e bake writes it.
        /// </summary>
        public static byte[] Write(string[] names,int[] parents,Vector3[] positions,Quaternion[] rotations,int[] tracks,IEnumerable<(string name,float length,float rate,Vector2 travel,Vector2 average,bool loop,int frames,float[] data)> clips,IReadOnlyList<string> sources=null)
        {
            using var m=new MemoryStream();using var w=new BinaryWriter(m,new UTF8Encoding(false));
            void N(string s){var b=Encoding.UTF8.GetBytes(s);w.Write(b.Length);w.Write(b);}
            w.Write((byte)'C');w.Write((byte)'A');w.Write((byte)'I');w.Write((byte)'M');w.Write(sources!=null?Sourced:Version);w.Write(names.Length);
            for(int v=0;v<names.Length;v++){N(names[v]);w.Write(parents[v]);w.Write(positions[v].X);w.Write(positions[v].Y);w.Write(positions[v].Z);w.Write(rotations[v].X);w.Write(rotations[v].Y);w.Write(rotations[v].Z);w.Write(rotations[v].W);}
            w.Write(tracks.Length);foreach(int t in tracks)w.Write(t);
            var list=new List<(string name,float length,float rate,Vector2 travel,Vector2 average,bool loop,int frames,float[] data)>(clips);w.Write(list.Count);
            for(int i=0;i<list.Count;i++){var c=list[i];N(c.name);if(sources!=null)N(sources[i]??c.name);w.Write(c.length);w.Write(c.rate);w.Write(c.travel.X);w.Write(c.travel.Y);w.Write(c.average.X);w.Write(c.average.Y);w.Write((byte)(c.loop?1:0));w.Write(c.frames);foreach(float x in c.data)w.Write(x);}
            w.Flush();return m.ToArray();
        }

        /// <summary>The wrist's eased bend undone (NativeVisualRig.Wrist's inverse): the keyed wrist that draws the visual turn `drawn`.
        /// A drawn bend past what the ease reaches (WristKnee plus WristExcess of a half turn) is held there.</summary>
        public static Quaternion Unwrist(Quaternion drawn)
        {
            var twist=CapturedRig.Rot(CapturedRig.Y,NativeVisualRig.Twist(drawn));var swing=Quaternion.Normalize(drawn*Quaternion.Inverse(twist));
            if(swing.W<0)swing=new Quaternion(-swing.X,-swing.Y,-swing.Z,-swing.W);
            float angle=2*(float)Math.Acos(Math.Min(1f,swing.W));if(angle<=NativeVisualRig.WristKnee)return drawn;
            var axis=new Vector3(swing.X,swing.Y,swing.Z);if(axis.LengthSquared()<1e-12f)return drawn;
            float keyed=Math.Min((float)Math.PI-.01f,NativeVisualRig.WristKnee+(angle-NativeVisualRig.WristKnee)/NativeVisualRig.WristExcess);
            return Quaternion.Normalize(CapturedRig.Rot(Vector3.Normalize(axis),keyed)*twist);
        }
        /// <summary>The keyed Euler angles (CapturedRig.Euler's XYZ, as NativeEntranceKeys keys them) of a joint's local turn `q`, nearest
        /// `near` (the previous key's, so a run of keys stays continuous).</summary>
        public static Vector3 Euler(Quaternion q,Vector3 near=default)
        {
            q=Quaternion.Normalize(q);
            // R = Rx(x) Ry(y) Rz(z): m02 = sin y; m12 = -sin x cos y; m22 = cos x cos y; m01 = -cos y sin z; m00 = cos y cos z
            float xx=q.X*q.X,yy=q.Y*q.Y,zz=q.Z*q.Z,xy=q.X*q.Y,xz=q.X*q.Z,yz=q.Y*q.Z,wx=q.W*q.X,wy=q.W*q.Y,wz=q.W*q.Z;
            float m00=1-2*(yy+zz),m01=2*(xy-wz),m02=2*(xz+wy),m12=2*(yz-wx),m22=1-2*(xx+yy),m10=2*(xy+wz),m11=1-2*(xx+zz);
            float y=(float)Math.Asin(Math.Max(-1,Math.Min(1,m02)));float x,z;
            if(Math.Abs(m02)<.99999f){x=(float)Math.Atan2(-m12,m22);z=(float)Math.Atan2(-m01,m00);}else{x=(float)Math.Atan2(m10*Math.Sign(m02),m11);z=0;}
            var a=new Vector3(x,y,z);var b=new Vector3(x+(float)Math.PI,(float)Math.PI-y,z+(float)Math.PI);
            a=Near(a,near);b=Near(b,near);return (a-near).LengthSquared()<=(b-near).LengthSquared()?a:b;
        }
        private static Vector3 Near(Vector3 v,Vector3 near)=>new Vector3(Wrap(v.X,near.X),Wrap(v.Y,near.Y),Wrap(v.Z,near.Z));
        private static float Wrap(float a,float near){float tau=2*(float)Math.PI;while(a-near>Math.PI)a-=tau;while(a-near<-Math.PI)a+=tau;return a;}

        /// <summary>The model binding run backwards on one skeleton (fixed scratch: one per bank; main thread).</summary>
        internal sealed class Converter
        {
            private readonly NativeVisualRig.Rig skeleton;private readonly Vector3[] restPos;private readonly Quaternion[] restRot;private readonly int[] track;
            private readonly Quaternion[] local,world,turn,game=new Quaternion[Joints],keyed=new Quaternion[Joints];private readonly Vector3[] at;
            private readonly int[] start=new int[Joints];private readonly int footL,footR,ballL,ballR;private readonly float restAnkle,restBall;
            public Converter(NativeVisualRig.Rig skeleton,Vector3[] restPos,Quaternion[] restRot,int[] track)
            {
                this.skeleton=skeleton;this.restPos=restPos;this.restRot=restRot;this.track=track;int n=skeleton.Count;
                local=new Quaternion[n];world=new Quaternion[n];turn=new Quaternion[n];at=new Vector3[n];
                for(int j=0;j<Joints;j++){int v=skeleton.Of[j];while(v>=0&&!skeleton.ChainStart[v])v=skeleton.Parent[v];start[j]=v;}
                footL=skeleton.Of[CapturedRig.LFoot];footR=skeleton.Of[CapturedRig.RFoot];ballL=skeleton.Of[CapturedRig.LToe];ballR=skeleton.Of[CapturedRig.RToe];
                restAnkle=footL<0?0:skeleton.AlignedPosition[footL].Y;restBall=ballL<0?restAnkle:skeleton.AlignedPosition[ballL].Y;
            }
            /// <summary>One baked frame at `raw[o..]` (the pelvis's local position, every track's local rotation; Unity's frame) on the model:
            /// every bone's world turn and place (rig frame), and its turn off the aligned rest.</summary>
            public void Pose(float[] raw,int o)
            {
                int n=skeleton.Count,pelvis=track[0];
                for(int v=0;v<n;v++)local[v]=restRot[v];
                for(int i=0;i<track.Length;i++){int q=o+3+i*4;local[track[i]]=CapturedRig.Mirror(Quaternion.Normalize(new Quaternion(raw[q],raw[q+1],raw[q+2],raw[q+3])));}
                var pelvisAt=CapturedRig.Mirror(new Vector3(raw[o],raw[o+1],raw[o+2]));
                for(int v=0;v<n;v++)
                {
                    int p=skeleton.Parent[v];world[v]=p<0?local[v]:Quaternion.Normalize(world[p]*local[v]);
                    var lp=v==pelvis?pelvisAt:restPos[v];at[v]=p<0?lp:at[p]+Vector3.Transform(lp,world[p]);
                    turn[v]=Quaternion.Normalize(world[v]*Quaternion.Inverse(skeleton.AlignedWorld[v]));
                }
            }
            public void Copy(Quaternion[] w,Vector3[] p){Array.Copy(world,w,Math.Min(w.Length,world.Length));Array.Copy(at,p,Math.Min(p.Length,at.Length));}
            /// <summary>The frame Pose last read, as keyed joints, the keyed body's offset and the drawn feet at `into[o..]`.</summary>
            public void Frame(float[] into,int o)
            {
                int pelvis=track[0];
                game[Body]=keyed[Body]=Quaternion.Identity;
                for(int j=1;j<Joints;j++)
                {
                    int gp=CapturedRig.Bones[j].Parent;var above=gp<0?Quaternion.Identity:game[gp];int v=skeleton.Of[j],c=start[j];
                    if(v<0||c<0){keyed[j]=Quaternion.Identity;game[j]=above;continue;}
                    int p=skeleton.Parent[c];bool attached=p>=0&&gp>=0&&skeleton.Joint[p]==gp;var from=attached?turn[p]:above;
                    Quaternion q;
                    if(j==CapturedRig.LArm||j==CapturedRig.RArm)
                    {
                        // (the binding spreads a hanging arm out by ArmSpread, by how far the keyed upper arm hangs: solved to its fixed point)
                        var spread=Quaternion.Identity;q=Quaternion.Inverse(from)*turn[v];
                        for(int it=0;it<6;it++)
                        {
                            float hang=-Vector3.Transform(-CapturedRig.Y,above*q).Y,w=CapturedClip.Ease((hang-.5f)/.5f);
                            spread=CapturedRig.Rot(CapturedRig.Z,(j==CapturedRig.LArm?1:-1)*NativeVisualRig.ArmSpread*w);q=Quaternion.Inverse(from*spread)*turn[v];
                        }
                    }
                    else q=Quaternion.Inverse(from)*turn[v];
                    if(j==CapturedRig.LHand||j==CapturedRig.RHand)q=Unwrist(q);
                    keyed[j]=Quaternion.Normalize(q);game[j]=Quaternion.Normalize(above*keyed[j]);
                }
                for(int j=0;j<Joints;j++){var q=keyed[j];if(j>0&&o>=Stride){int prev=o-Stride+j*4;if(q.X*into[prev]+q.Y*into[prev+1]+q.Z*into[prev+2]+q.W*into[prev+3]<0)q=-q;}into[o+j*4]=q.X;into[o+j*4+1]=q.Y;into[o+j*4+2]=q.Z;into[o+j*4+3]=q.W;}
                var body=(at[pelvis]-skeleton.PelvisRest)/skeleton.LegRatio;int b=o+Joints*4;into[b]=body.X;into[b+1]=body.Y;into[b+2]=body.Z;
                Put(into,b+3,footL);Put(into,b+6,ballL);Put(into,b+9,footR);Put(into,b+12,ballR);
            }
            private void Put(float[] into,int o,int v){var p=v<0?Vector3.Zero:at[v];into[o]=p.X;into[o+1]=p.Y;into[o+2]=p.Z;}
            /// <summary>Each frame's feet on the floor (ankle within 3 cm, or ball within 2 cm, of its rest height) and the clip's Speed: the
            /// mean rate a foot on the floor moves back.</summary>
            public void Contacts(Clip c)
            {
                int b=Joints*4+3;double sum=0;int samples=0;
                for(int f=0;f<c.Frames;f++)
                {
                    int o=f*Stride+b;byte down=0;int pa=Math.Max(0,f-1)*Stride+b,na=Math.Min(c.Frames-1,f+1)*Stride+b;
                    for(int side=0;side<2;side++)
                    {
                        float ankle=c.Data[o+side*6+1],ball=c.Data[o+side*6+4];
                        // (on the floor and not swinging forward under the body: a low swing skims the floor going the other way)
                        float ahead=(c.Data[na+side*6+2]+c.Data[na+side*6+5])-(c.Data[pa+side*6+2]+c.Data[pa+side*6+5]);
                        if((ankle-restAnkle<.03f||ball-restBall<.02f)&&ahead<=.004f)down|=(byte)(1<<side);
                    }
                    c.Down[f]=down;
                }
                c.From=new int[c.Frames*2];c.To=new int[c.Frames*2];
                for(int side=0;side<2;side++)
                {
                    int bit=1<<side;
                    for(int f=0;f<c.Frames;f++){if((c.Down[f]&bit)==0)continue;c.From[f*2+side]=f>0&&(c.Down[f-1]&bit)!=0?c.From[(f-1)*2+side]:f;}
                    for(int f=c.Frames-1;f>=0;f--){if((c.Down[f]&bit)==0)continue;c.To[f*2+side]=f<c.Frames-1&&(c.Down[f+1]&bit)!=0?c.To[(f+1)*2+side]:f;}
                }
                for(int f=0;f+1<c.Frames;f++)
                    for(int side=0;side<2;side++)
                    {
                        if((c.Down[f]&c.Down[f+1]&(1<<side))==0)continue;int o=f*Stride+b+side*6,o2=o+Stride;
                        float back=-((c.Data[o2+2]+c.Data[o2+5])-(c.Data[o+2]+c.Data[o+5]))*.5f*c.Rate;sum+=back;samples++;
                    }
                c.Speed=samples>0?Math.Max(0,(float)(sum/samples)):0;
            }
        }
    }
}
