using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// NOCTIEL's intro body from the owner's reference clips (2026-11-06: "reproduce the reference videos as accurately as technically
    /// possible"; docs/EXPEDITION-BOSS-FINAL.md section 19; tools/motion/README.md). The motion is extracted, not keyed: MediaPipe's
    /// pose landmarks from each frame of clips 02 (the kneel and the rise), 03 (the pull, the wheel to the salute, out to the side,
    /// lowered) and 04 (the raise, the cocked guard, the windup, the chop into the lunge), the blade tracked in the picture, solved
    /// onto NOCTIEL's own rest skeleton with each limb's hinge from its bend (no flipped or twisted arms), the planted foot held still,
    /// the wrist inside a human range. Resources/FallenIntroMocap.json holds, per clip and frame (24 a second), the root and each
    /// bone's turn from its rest in the boss's frame (x its right, y up, z its front), the fist's grip, the plant and the two-handed
    /// grip. This class lays the clips on the cutscene's clock (Map, at the reference's own speed; Speed and each segment's Rate
    /// change the speed without touching the frames) and samples them. Pure: the same clock, the same pose on every client.
    /// Presentation only; no rule reads it.
    /// </summary>
    public sealed class NativeFallenIntroMocap
    {
        public sealed class Clip
        {
            public string Name,Video;public int Frames;
            /// <summary>Per frame: each bone's turn (x,y,z,w per bone), the root (the pelvis, rest metres), the fist's grip, the left
            /// hand, the plant and two-handed weights, the tracked blade, its error left past the wrist's range (degrees), and the
            /// frames held (no body seen) or filled (the blade not readable: blur, behind the body).</summary>
            public float[] Q,Root,Grip,Left,Plant,Two,Blade,BladeErr;public int[] BodyHeld,SwordFilled;
        }
        public readonly string[] Bones;public readonly float Fps;public readonly Clip[] Clips;
        /// <summary>NOCTIEL's rest skeleton (tools/gear/fallen_boss.py REST, mesh space): names, parents, origins, turns (x,y,z,w).</summary>
        public readonly string[] RestBones;public readonly int[] RestParent;public readonly float[] RestPos,RestRot;
        /// <summary>The claymore in hand_r's own axes (Runtime NativeFallenBossBody FbBladeWay, FbGrip).</summary>
        public readonly Vector3 BladeInHand,GripInHand;
        private readonly int[] restOfBone,turnOfRest;private readonly Vector3[] clipOffset;

        public NativeFallenIntroMocap(string[] bones,float fps,Clip[] clips,string[] restBones,int[] restParent,float[] restPos,float[] restRot,float[] bladeInHand,float[] gripInHand)
        {
            Bones=bones??Array.Empty<string>();Fps=fps;Clips=clips??Array.Empty<Clip>();RestBones=restBones??Array.Empty<string>();RestParent=restParent??Array.Empty<int>();RestPos=restPos??Array.Empty<float>();RestRot=restRot??Array.Empty<float>();
            BladeInHand=bladeInHand!=null&&bladeInHand.Length==3?new Vector3(bladeInHand[0],bladeInHand[1],bladeInHand[2]):Vector3.UnitZ;GripInHand=gripInHand!=null&&gripInHand.Length==3?new Vector3(gripInHand[0],gripInHand[1],gripInHand[2]):Vector3.Zero;
            restOfBone=new int[Bones.Length];for(int i=0;i<Bones.Length;i++)restOfBone[i]=Array.IndexOf(RestBones,Bones[i]);
            turnOfRest=new int[RestBones.Length];for(int r=0;r<RestBones.Length;r++){int k=-1;for(int a=r;a>=0&&k<0;a=RestParent[a])k=Array.IndexOf(Bones,RestBones[a]);turnOfRest[r]=k;}
            Valid=Check();
            // Each clip's root picks up where the one before it in the map left off (on the floor's plane), so the joins don't jump.
            clipOffset=new Vector3[Clips.Length];
            if(Valid)
            {
                int prevClip=-1;float prevFrame=0;
                foreach(var s in Map)
                {
                    if(prevClip>=0&&s.Clip!=prevClip){var a=RootOf(prevClip,prevFrame)+clipOffset[prevClip];var b=RootOf(s.Clip,s.From);clipOffset[s.Clip]=new Vector3(a.X-b.X,0,a.Z-b.Z);}
                    prevClip=s.Clip;prevFrame=s.To;
                }
            }
        }
        /// <summary>The file is whole: every clip, track and rest bone the map and the rig need.</summary>
        public readonly bool Valid;
        private bool Check()
        {
            {
                if(Bones.Length==0||Clips.Length<3||!(Fps>0)||RestBones.Length==0||RestPos.Length!=RestBones.Length*3||RestRot.Length!=RestBones.Length*4||RestParent.Length!=RestBones.Length)return false;
                foreach(var c in Clips)if(c==null||c.Frames<2||c.Q==null||c.Q.Length!=c.Frames*Bones.Length*4||c.Root==null||c.Root.Length!=c.Frames*3||c.Grip==null||c.Grip.Length!=c.Frames*3||c.Left==null||c.Left.Length!=c.Frames*3||c.Plant==null||c.Plant.Length!=c.Frames||c.Two==null||c.Two.Length!=c.Frames)return false;
                foreach(var r in restOfBone)if(r<0)return false;
                return true;
            }
        }

        /// <summary>The file the game loads (Runtime NativeFallenIntroMocapBody.cs); null until then, or when there is none. Enabled
        /// off plays the keyed intro (NativeFallenIntroMotion) instead, which stays as it was.</summary>
        public static NativeFallenIntroMocap Current;public static bool Enabled=true;
        public static NativeFallenIntroMocap Active=>Enabled&&Current!=null&&Current.Valid?Current:null;

        // ---------------- the time map ----------------
        /// <summary>A stretch of one clip, frame From to To, at Rate times the reference's own speed (times Speed). Rate 0 holds From.</summary>
        public struct Segment{public int Clip;public float From,To,Rate;public string What;}
        public const int Kneel=0,Pull=1,Cut=2;
        /// <summary>The reference's frames the cutscene's beats are measured from: the rise begins (clip 02), the fist takes the sword
        /// from the water (03), the top of the windup and the chop's contact (04).</summary>
        public const float RiseFrame=98,PullFrame=56,TopFrame=128,ContactFrame=134,HoldFrame=60;
        /// <summary>The chop, top to contact: 0.25 s in the reference; the owner asked for it faster (0.20 s, 2026-11-06).</summary>
        public const float ChopRate=1.25f;
        /// <summary>The clips in order, at the reference's speed except the chop. The contact is anchored on the cutscene's impact (the
        /// drop); the kneel is held from the first frame the boss is seen until the rest of the map begins.</summary>
        public static readonly Segment[] Map=
        {
            new Segment{Clip=Kneel,From=HoldFrame,To=HoldFrame,Rate=0,What="kneel (held for the cutscene's length)"},
            new Segment{Clip=Kneel,From=HoldFrame,To=168,Rate=1,What="the kneel's last breath, the rise beside the planted sword, the stand"},
            new Segment{Clip=Pull,From=0,To=168,Rate=1,What="stand, the pull, the wheel to the salute, out to the side, lowered"},
            new Segment{Clip=Cut,From=0,To=TopFrame,Rate=1,What="low at the side, the raise, the cocked guard, the windup"},
            new Segment{Clip=Cut,From=TopFrame,To=ContactFrame,Rate=ChopRate,What="the chop"},
            new Segment{Clip=Cut,From=ContactFrame,To=168,Rate=1,What="the lunge settles (after the impact, while it hands back to the fight)"},
        };
        public const int ChopSegment=4;
        /// <summary>Every segment's speed together (1 the reference's): the choreography and its frames stay the same.</summary>
        public static float Speed=1;
        /// <summary>Seconds the change from one clip to the next blends over. The owner's clips are separate takes: each ends and the
        /// next begins standing, but the stance differs by 10 to 20 cm (rest), so the blend is long enough to read as the weight
        /// shifting, not the feet sliding. The second join falls in the mask's close-up, the body out of the shot.</summary>
        public const float JoinBlend=1f;
        public static float ImpactAt=>NativeFallenPilots.CutsceneEnd;

        public float Seconds(int segment){var s=Map[segment];float r=Math.Max(.05f,s.Rate*Math.Max(.05f,Speed));return s.Rate<=0?0:(s.To-s.From)/(Fps*r);}
        /// <summary>When each segment starts on the cutscene's clock, laid back from the chop's contact on the impact.</summary>
        public float StartOf(int segment)
        {
            if(segment>ChopSegment){float t=ImpactAt;for(int i=ChopSegment+1;i<segment;i++)t+=Seconds(i);return t;}
            float at=ImpactAt;for(int i=ChopSegment;i>=segment;i--)at-=Seconds(i);return segment==0?0:at;
        }
        public float EndOf(int segment)=>segment==0?StartOf(1):StartOf(segment)+Seconds(segment);
        /// <summary>When a clip's frame plays (the first segment that holds it).</summary>
        public float TimeOf(int clip,float frame)
        {
            for(int i=1;i<Map.Length;i++){var s=Map[i];if(s.Clip==clip&&frame>=s.From&&frame<=s.To)return StartOf(i)+(frame-s.From)/(Fps*Math.Max(.05f,s.Rate*Math.Max(.05f,Speed)));}
            return -1;
        }
        public float RiseAt=>TimeOf(Kneel,RiseFrame);public float PullAt=>TimeOf(Pull,PullFrame);public float TopAt=>TimeOf(Cut,TopFrame);public float ContactAt=>TimeOf(Cut,ContactFrame);
        public float HoldEnd=>StartOf(1);public float End=>EndOf(Map.Length-1);

        // ---------------- sampling ----------------
        public sealed class Pose
        {
            public bool On;public Quaternion[] Turn;public Vector3 Root,Grip,Left;public float Plant,Two;
            /// <summary>Where it is in the reference: the segment, the clip and its frame; held (the kneel's hold, no motion of its own).</summary>
            public int Segment,Clip;public float Frame;public bool Held;
            public Pose(int bones){Turn=new Quaternion[bones];}
        }
        public Pose NewPose()=>new Pose(Bones.Length);
        /// <summary>The body at p (the cutscene's seconds; past the impact it plays the lunge's settle on to the map's end).</summary>
        public bool Sample(float p,Pose into)
        {
            into.On=false;if(!Valid||into.Turn.Length!=Bones.Length||!(p>=0))return false;
            int seg=Map.Length-1;for(int i=0;i<Map.Length;i++)if(p<EndOf(i)){seg=i;break;}
            float frame=FrameIn(seg,p);var s=Map[seg];Read(s.Clip,frame,into);into.Segment=seg;into.Clip=s.Clip;into.Frame=frame;into.Held=s.Rate<=0;
            // Across a change of clip: from the last clip's end to the next one's start, smoothly.
            for(int j=1;j<Map.Length;j++)
            {
                if(Map[j].Clip==Map[j-1].Clip)continue;float tj=StartOf(j),u=(p-(tj-JoinBlend*.5f))/JoinBlend;if(u<=0||u>=1)continue;u=u*u*(3-2*u);
                var a=scratchA??(scratchA=NewPose());var b=scratchB??(scratchB=NewPose());Read(Map[j-1].Clip,Map[j-1].To,a);Read(Map[j].Clip,Map[j].From,b);
                if(p<tj)Read(Map[j-1].Clip,FrameIn(j-1,p),a);else Read(Map[j].Clip,FrameIn(j,p),b);
                for(int k=0;k<Bones.Length;k++)into.Turn[k]=Quaternion.Normalize(Quaternion.Slerp(a.Turn[k],b.Turn[k],u));
                into.Root=Vector3.Lerp(a.Root,b.Root,u);into.Grip=Vector3.Lerp(a.Grip,b.Grip,u);into.Left=Vector3.Lerp(a.Left,b.Left,u);into.Plant=a.Plant+(b.Plant-a.Plant)*u;into.Two=a.Two+(b.Two-a.Two)*u;
            }
            into.On=true;return true;
        }
        /// <summary>The fist's weight on the planted sword at p (1 until the pull takes it from the water).</summary>
        public float PlantAt(float p)=>Track(p,true);
        /// <summary>The left hand's weight on the grip at p (the two-handed lunge).</summary>
        public float TwoAt(float p)=>Track(p,false);
        private float Track(float p,bool plant)
        {
            if(!Valid||!(p>=0))return plant?1:0;int seg=Map.Length-1;for(int i=0;i<Map.Length;i++)if(p<EndOf(i)){seg=i;break;}
            var c=Clips[Map[seg].Clip];var x=plant?c.Plant:c.Two;float f=FrameIn(seg,p);int i0=(int)Math.Floor(f),i1=Math.Min(c.Frames-1,i0+1);float u=f-i0;return x[i0]+(x[i1]-x[i0])*u;
        }
        private Pose scratchA,scratchB;
        private float FrameIn(int seg,float p){var s=Map[seg];if(s.Rate<=0)return s.From;float f=s.From+(p-StartOf(seg))*Fps*s.Rate*Math.Max(.05f,Speed);return f<s.From?s.From:f>s.To?s.To:f;}
        private void Read(int clip,float frame,Pose into)
        {
            var c=Clips[clip];float f=frame<0?0:frame>c.Frames-1?c.Frames-1:frame;int i0=(int)Math.Floor(f),i1=Math.Min(c.Frames-1,i0+1);float u=f-i0;int nb=Bones.Length;
            for(int k=0;k<nb;k++)
            {
                int a=(i0*nb+k)*4,b=(i1*nb+k)*4;var qa=new Quaternion(c.Q[a],c.Q[a+1],c.Q[a+2],c.Q[a+3]);var qb=new Quaternion(c.Q[b],c.Q[b+1],c.Q[b+2],c.Q[b+3]);
                into.Turn[k]=Quaternion.Normalize(Quaternion.Slerp(qa,qb,u));
            }
            Vector3 V(float[] x,int i)=>new Vector3(x[i*3],x[i*3+1],x[i*3+2]);var off=clipOffset[clip];
            into.Root=Vector3.Lerp(V(c.Root,i0),V(c.Root,i1),u)+off;into.Grip=Vector3.Lerp(V(c.Grip,i0),V(c.Grip,i1),u)+off;into.Left=Vector3.Lerp(V(c.Left,i0),V(c.Left,i1),u)+off;
            into.Plant=c.Plant[i0]+(c.Plant[i1]-c.Plant[i0])*u;into.Two=c.Two[i0]+(c.Two[i1]-c.Two[i0])*u;
        }
        private Vector3 RootOf(int clip,float frame){var c=Clips[clip];int i=(int)Math.Max(0,Math.Min(c.Frames-1,Math.Round(frame)));return new Vector3(c.Root[i*3],c.Root[i*3+1],c.Root[i*3+2]);}
        /// <summary>Where the planted sword's grip stands (the boss's frame, rest metres): where the reference's fist rests on it in the
        /// kneel. The plant's fist is held there until the pull.</summary>
        public Vector3 PlantGrip{get{var c=Clips[Kneel];int i=(int)HoldFrame;return new Vector3(c.Grip[i*3],c.Grip[i*3+1],c.Grip[i*3+2])+clipOffset[Kneel];}}

        // ---------------- the rig, for the checks ----------------
        /// <summary>The rest skeleton posed by a sample (forward kinematics, rest metres, the boss's frame): each joint's origin.</summary>
        public void Joints(Pose pose,Vector3[] into,Quaternion[] turnInto=null)
        {
            int n=RestBones.Length;var p0=new Vector3[n];for(int r=0;r<n;r++)p0[r]=new Vector3(RestPos[r*3],RestPos[r*3+1],RestPos[r*3+2]);
            Quaternion T(int r)=>turnOfRest[r]>=0?pose.Turn[turnOfRest[r]]:Quaternion.Identity;
            for(int r=0;r<n;r++)
            {
                int par=RestParent[r];into[r]=par<0?pose.Root:into[par]+Vector3.Transform(p0[r]-p0[par],T(par));
                if(turnInto!=null)turnInto[r]=Quaternion.Normalize(T(r)*RestTurn(r));
            }
        }
        public Quaternion RestTurn(int r)=>new Quaternion(RestRot[r*4],RestRot[r*4+1],RestRot[r*4+2],RestRot[r*4+3]);
        public int RestIndex(string bone)=>Array.IndexOf(RestBones,bone);
        public int BoneIndex(string bone)=>Array.IndexOf(Bones,bone);
    }
}
