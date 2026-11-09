using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Hotdog Friends, a DEV kill effect (owner 2026-11-09: "Id also like to make a DEV kill effect called "Hotdog Friends" after players are
    /// gibbed/ragdolled like normal. Tiny little hotdogs appear from a tiny door (near whereever the body parts land/stop) and take all the body
    /// parts through the door. (Have them move accordingly so it fits the body parts seamlessly)"). The body falls as the stock impact ragdoll
    /// (its gibs, severs and slices too). Once it has come to rest a little arched door rises out of the floor beside the pieces and swings open,
    /// and a file of tiny hot dogs on stick legs scurries out to each piece (the pieces still joined together are one load), gathers round it
    /// (more round a bigger one: <see cref="Friends"/>), lifts it, carries it back and squeezes it through the door, then follows it in; the door
    /// shuts and sinks away. Every moment is a pure function of the death's seed, where the pieces came to rest and the effect clock, so every
    /// client and the killcam draw the same scene. Runtime NativeHotdogFriendsPresentation.cs gathers the loads, moves the body's pieces and draws
    /// the door and the friends (built here) in the Top Dog's toon props shader. Presentation only. Unity's axes.
    /// </summary>
    public static class NativeHotdogFriends
    {
        public const string Style="dev-hotdog-friends",Label="Hotdog Friends · DEV ONLY",Text="Tiny hot dogs carry the pieces off through a tiny door.";public const int Swatch=0xf4c430;
        /// <summary>The body rests this long (s after the death) before the door rises; the door's rise, opening, shutting and sinking (s);
        /// its height and width, and how far past the pieces it stands (m).</summary>
        public const float Settle=1.2f,DoorUp=.25f,DoorOpen=.18f,DoorShut=.2f,DoorDown=.25f,DoorHeight=.46f,DoorWidth=.3f,DoorAway=.55f,DoorSwing=105;
        /// <summary>The friends: their height (m); walking and carrying speeds (m/s) and the longest walk and carry (s: farther pieces are
        /// hurried, so the scene ends inside the body's five seconds); the lift (s, m); the stagger between loads and within a file (s); the
        /// squeeze through the door (s); a step (m).</summary>
        public const float Height=.27f,Walk=2.4f,CarrySpeed=1.7f,MostWalk=.5f,MostCarry=.8f,LiftTime=.22f,LiftHeight=.14f,Stagger=.08f,File=.05f,Squeeze=.25f,Stride=.1f;
        public const int MaxLoads=12,MostFriends=6,WalkFrames=8;
        /// <summary>The door is open from here (s after the death).</summary>
        public const float Opened=Settle+DoorUp+DoorOpen;
        /// <summary>How many carry a load `size` m across.</summary>
        public static int Friends(float size)=>size<.2f?2:size<.38f?3:size<.65f?4:MostFriends;

        private static float Clamp(float x)=>x<0?0:x>1?1:x;
        private static float Ease(float x){x=Clamp(x);return 1-(1-x)*(1-x);}
        private static float Smooth(float x){x=Clamp(x);return x*x*(3-2*x);}
        public static float Hash(uint seed,int k){uint h=seed*0x9E3779B1u^(uint)k*0x85EBCA77u;h^=h>>15;h*=0x2C1B3C6Du;h^=h>>12;return (h&0xffffff)/16777216f;}
        private static Vector3 Flat(Vector3 v){v.Y=0;float l=v.Length();return l>1e-5f?v/l:Vector3.UnitZ;}

        /// <summary>The door's candidate spot k (0..7) round the pieces' middle `centre` (`radius` their spread): eight directions from a seeded
        /// start; the Runtime keeps the first with floor under it and nothing in the way. `yaw` (degrees) turns the door's mouth to the middle.</summary>
        public static Vector3 DoorSpot(Vector3 centre,float radius,uint seed,int k,out float yaw)
        {
            float a=Hash(seed,1)*6.2831853f+k*.7853982f;var d=new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a));
            yaw=(float)(Math.Atan2(-d.X,-d.Z)*180/Math.PI);return centre+d*(radius+DoorAway);
        }
        /// <summary>How far the door has risen out of the floor (0..1) and how open it is (0..1) at effect age `t`, the last friend in at `last`;
        /// false once it has sunk away (or before it rises).</summary>
        public static bool Door(float t,float last,out float rise,out float open)
        {
            rise=open=0;if(t<Settle)return false;float shutAt=Math.Max(Opened,last+.05f),downAt=shutAt+DoorShut;if(t>=downAt+DoorDown)return false;
            rise=t<downAt?Ease((t-Settle)/DoorUp):1-Ease((t-downAt)/DoorDown);
            open=t<Settle+DoorUp?0:t<shutAt?Ease((t-Settle-DoorUp)/DoorOpen):1-Smooth((t-shutAt)/DoorShut);return true;
        }
        /// <summary>When the door is gone: the effect is over.</summary>
        public static float Over(float last)=>Math.Max(Opened,last+.05f)+DoorShut+DoorDown;

        /// <summary>One load's trip: when its file leaves the door, reaches it, has it up, is back at the door and has it through; `Floor` the
        /// floor under it.</summary>
        public struct Trip{public float Leave,Arrive,Lifted,Back,In,Floor;}
        /// <summary>The trip of the load `order`th nearest the door, `distance` m from it.</summary>
        public static Trip Plan(int order,float distance,float floor)
        {
            float leave=Opened+order*Stagger,walk=Math.Min(MostWalk,Math.Max(.05f,distance)/Walk),carry=Math.Min(MostCarry,Math.Max(.05f,distance)/CarrySpeed);
            return new Trip{Leave=leave,Arrive=leave+walk,Lifted=leave+walk+LiftTime,Back=leave+walk+LiftTime+carry,In=leave+walk+LiftTime+carry+Squeeze,Floor=floor};
        }
        /// <summary>When friend `j` of a trip is through the door (the last one closes it).</summary>
        public static float InAt(in Trip trip,int j)=>trip.In+.04f+j*File;

        /// <summary>
        /// The load at age `t`: from its rest middle `rest` to the door's mouth `mouth`; its middle `at`, its `scale` (1, to 0 as it squeezes
        /// through) and the sway the friends' steps give it, `tilt` (radians about the carry's side). Held up LiftHeight while carried.
        /// </summary>
        public static void Load(in Trip trip,Vector3 rest,Vector3 mouth,float t,out Vector3 at,out float scale,out float tilt)
        {
            at=rest;scale=1;tilt=0;if(t<trip.Arrive)return;
            if(t<trip.Lifted){float u=(t-trip.Arrive)/LiftTime;at=rest+Vector3.UnitY*(LiftHeight*Ease(u));tilt=.07f*(float)Math.Sin(u*9)*(1-u);return;}
            var flat=mouth-rest;flat.Y=0;float length=flat.Length();
            if(t<trip.Back)
            {
                float u=(t-trip.Lifted)/Math.Max(1e-3f,trip.Back-trip.Lifted),s=Smooth(u),steps=length*s/Stride;
                at=rest+flat*s+Vector3.UnitY*(LiftHeight+.012f*(float)Math.Abs(Math.Sin(steps*Math.PI)));tilt=.05f*(float)Math.Sin(steps*Math.PI);return;
            }
            // Through the door: pushed into its mouth, squashed small as it goes.
            float q=Clamp((t-trip.Back)/Squeeze);var inside=mouth+Flat(flat)*.12f+Vector3.UnitY*Math.Min(DoorHeight*.4f,Math.Max(.06f,rest.Y-trip.Floor));
            var from=rest+flat+Vector3.UnitY*LiftHeight;at=Vector3.Lerp(from,inside,Smooth(q));scale=1-Smooth(q);
        }
        /// <summary>Where friend `j` of `n` stands round a load `size` m across (an offset on the floor from its middle), the carry along
        /// `toDoor`: round the edge, a little under it.</summary>
        public static Vector3 Slot(int j,int n,float size,Vector3 toDoor)
        {
            float r=Math.Max(.1f,size*.4f),a=(j+.5f)/Math.Max(1,n)*6.2831853f;var f=Flat(toDoor);var side=new Vector3(f.Z,0,-f.X);
            return f*((float)Math.Cos(a)*r)+side*((float)Math.Sin(a)*r*.85f);
        }
        /// <summary>
        /// Friend `j` of `n` on a load's trip at age `t`: its `feet`, its `facing` (flat, unit), how far it has walked (`walked`, m: its stride's
        /// phase), how high its arms are (`hold`, 0 swinging .. 1 up under the load) and its `scale` (popping out of the door, shrinking into
        /// it). False before it comes out and after it has gone in.
        /// </summary>
        public static bool Friend(in Trip trip,int j,int n,Vector3 rest,Vector3 mouth,float size,float t,out Vector3 feet,out Vector3 facing,out float walked,out float hold,out float scale)
        {
            feet=mouth;facing=Vector3.UnitZ;walked=hold=0;scale=1;float leave=trip.Leave+j*File;if(t<leave||t>=InAt(trip,j))return false;
            var toDoor=mouth-rest;toDoor.Y=0;var slot=Slot(j,n,size,toDoor);var stand=new Vector3(rest.X,trip.Floor,rest.Z)+slot;float far=Vector3.Distance(mouth,stand);
            float arrive=Math.Max(leave+.05f,trip.Arrive-.02f+j*.01f);
            if(t<arrive)
            {
                float u=Clamp((t-leave)/(arrive-leave));feet=Vector3.Lerp(mouth,stand,u);facing=Flat(stand-mouth);walked=far*u;scale=.55f+.45f*Ease((t-leave)/.1f);return true;
            }
            walked=far;
            if(t<trip.Lifted){feet=stand;facing=Flat(rest-stand);hold=Ease((t-arrive)/Math.Max(.05f,trip.Lifted-arrive));walked+=(t-arrive)*.3f;return true;}
            Load(trip,rest,mouth,Math.Min(t,trip.Back),out var at,out _,out _);
            var under=new Vector3(at.X,trip.Floor,at.Z)+slot;
            if(t<trip.Back){feet=under;facing=Flat(toDoor);hold=1;walked+=Vector3.Distance(new Vector3(rest.X,0,rest.Z),new Vector3(at.X,0,at.Z));return true;}
            // In after the load, in a file, shrinking as they pass the mouth.
            float q=Clamp((t-trip.Back)/Math.Max(.05f,InAt(trip,j)-trip.Back));feet=Vector3.Lerp(under,mouth+Flat(toDoor)*.05f,Smooth(q));facing=Flat(toDoor);
            hold=1-q;walked+=toDoor.Length()+q*.3f;scale=1-Smooth((q-.55f)/.45f);return true;
        }
        /// <summary>The walk frame (0..WalkFrames-1) for a distance walked.</summary>
        public static int Frame(float walked)=>(int)((walked/(2*Stride)*WalkFrames%WalkFrames+WalkFrames)%WalkFrames);

        // ---- the models (NativeFallenKit, sRGB hex colours, the Top Dog's toon palette) ----
        /// <summary>A friend: a hot dog stood on end in its bun, googly eyes on the sausage, stick legs and arms in white gloves, its feet at the
        /// origin facing +z. `frame` is the stride (WalkFrames to a step pair), `hold` the arms (0 swinging, 1 up over its head).</summary>
        public static NativeFallenKit Friend(int frame,float hold,int tier=0)
        {
            var k=new NativeFallenKit();float s=.78f,hip=.068f,phase=frame*2*(float)Math.PI/WalkFrames;int seg=tier>0?5:7;
            var body=Matrix4x4.CreateScale(s)*Matrix4x4.CreateRotationX((float)Math.PI/2)*Matrix4x4.CreateTranslation(0,hip+NativeTopDogModels.DogLength*s*.5f,0);
            NativeTopDogModels.HotDog(k,body,phase*.5f,.25f,0,1,tier,true);
            // The eyes, high on the sausage's front, and their pupils looking ahead.
            float ey=hip+NativeTopDogModels.DogLength*s*.8f,ez=NativeTopDogModels.SausageRadius*s+.004f;
            foreach(float side in new[]{-1f,1})
            {
                var e=new Vector3(side*.012f,ey,ez);k.Ellipsoid(e,Vector3.UnitX*.0105f,Vector3.UnitY*.013f,Vector3.UnitZ*.008f,tier>0?6:9,tier>0?4:6,NativeTopDogModels.White);
                k.Ellipsoid(e+new Vector3(side*.001f,-.002f,.007f),Vector3.UnitX*.0048f,Vector3.UnitY*.0056f,Vector3.UnitZ*.003f,5,4,NativeTopDogModels.Ink,line:false);
            }
            // A grin under them.
            var grin=new List<Vector3>();for(int i=0;i<=6;i++){float a=(i/6f-.5f)*1.6f;grin.Add(new Vector3((float)Math.Sin(a)*.012f,ey-.022f-.004f*(float)Math.Cos(a),ez+.001f));}k.Tube(grin,.0022f,4,NativeTopDogModels.Ink,line:false);
            // The legs: hips to feet, swinging through the stride, a shoe on each.
            foreach(float side in new[]{-1f,1})
            {
                float swing=(float)Math.Sin(phase+(side>0?0:Math.PI)),lift=Math.Max(0,(float)Math.Cos(phase+(side>0?0:Math.PI)))*.014f;
                var hipAt=new Vector3(side*.02f,hip,0);var foot=new Vector3(side*.022f,.01f+lift,swing*.032f);var knee=(hipAt+foot)*.5f+new Vector3(0,0,.006f);
                k.Tube(new[]{hipAt,knee,foot},.0062f,seg,NativeTopDogModels.Ink);
                k.Ellipsoid(foot+new Vector3(0,-.002f,.008f),Vector3.UnitX*.011f,Vector3.UnitY*.008f,Vector3.UnitZ*.017f,tier>0?6:8,tier>0?4:5,NativeTopDogModels.Red);
            }
            // The arms: from the bun's sides, swinging when it walks, up over its head (palms up) when it carries.
            float shoulder=hip+NativeTopDogModels.DogLength*s*.62f;
            foreach(float side in new[]{-1f,1})
            {
                float swing=(float)Math.Sin(phase+(side>0?Math.PI:0));var sh=new Vector3(side*.036f,shoulder,0);
                var down=new Vector3(side*.05f,shoulder-.075f,swing*.03f);var up=new Vector3(side*.03f,shoulder+.1f,.012f);var hand=Vector3.Lerp(down,up,hold);
                var elbow=(sh+hand)*.5f+new Vector3(side*.016f*(1-hold*.5f),0,0);
                k.Tube(new[]{sh,elbow,hand},.0052f,seg,NativeTopDogModels.Ink);
                k.Ellipsoid(hand,Vector3.UnitX*.011f,Vector3.UnitY*.01f,Vector3.UnitZ*.011f,tier>0?6:8,tier>0?4:5,NativeTopDogModels.White);
            }
            k.Finish();return k;
        }
        private static List<Vector3> Arch(float w,float h,float z,float inset,int n)
        {
            var pts=new List<Vector3>();float r=w*.5f-inset,top=h-w*.5f;pts.Add(new Vector3(-r,0,z));pts.Add(new Vector3(-r,top,z));
            for(int i=1;i<n;i++){float a=(float)Math.PI*(1-i/(float)n);pts.Add(new Vector3((float)Math.Cos(a)*r,top+(float)Math.Sin(a)*r,z));}
            pts.Add(new Vector3(r,top,z));pts.Add(new Vector3(r,0,z));return pts;
        }
        /// <summary>The door's surround: an arched mustard frame round a dark doorway in a little wooden block, a step in front; its sill at the
        /// origin, its mouth facing +z.</summary>
        public static NativeFallenKit DoorFrame(int tier=0)
        {
            var k=new NativeFallenKit();int n=tier>0?10:16;float w=DoorWidth,h=DoorHeight;
            // The block behind: the arch's outline pushed back, its face round the doorway.
            var front=Arch(w+.08f,h+.05f,-.006f,0,n).ToArray();var back=Arch(w+.08f,h+.05f,-.07f,0,n).ToArray();var c0=new Vector3(0,h*.45f,-.006f);var c1=new Vector3(0,h*.45f,-.07f);
            k.Loft(new List<Vector3[]>{front,back},new[]{c0,c1},NativeTopDogModels.WoodDark,capLast:true);
            // The doorway: dark inside.
            k.Cap(Arch(w,h,-.004f,0,n).ToArray(),new Vector3(0,h*.4f,-.004f),Vector3.UnitZ,NativeTopDogModels.Ink,line:false);
            // The frame: a round mustard moulding up, over and down.
            k.Tube(NativeFallenKit.Curve(Arch(w,h,.004f,-.01f,n),2),.017f,tier>0?6:8,NativeTopDogModels.Mustard);
            // The step.
            k.Ellipsoid(new Vector3(0,.006f,.045f),Vector3.UnitX*(w*.6f),Vector3.UnitY*.012f,Vector3.UnitZ*.045f,tier>0?10:16,4,NativeTopDogModels.Wood);
            // A little hot dog sign over it.
            NativeTopDogModels.HotDog(k,Matrix4x4.CreateScale(.42f)*Matrix4x4.CreateRotationY((float)Math.PI/2)*Matrix4x4.CreateTranslation(0,h+.05f,.012f),0,0,0,1,tier,true);
            k.Finish();return k;
        }
        /// <summary>The door itself: a red arched leaf hinged at the origin (its frame's left side), shut along +x, a mustard knob.</summary>
        public static NativeFallenKit DoorLeaf(int tier=0)
        {
            var k=new NativeFallenKit();int n=tier>0?10:16;float w=DoorWidth,h=DoorHeight;
            var f=Arch(w,h,.002f,.012f,n);var b=Arch(w,h,-.012f,.012f,n);for(int i=0;i<f.Count;i++){f[i]+=new Vector3(w*.5f,0,0);b[i]+=new Vector3(w*.5f,0,0);}
            var cf=new Vector3(w*.5f,h*.45f,.002f);var cb=new Vector3(w*.5f,h*.45f,-.012f);
            k.Loft(new List<Vector3[]>{f.ToArray(),b.ToArray()},new[]{cf,cb},NativeTopDogModels.Red,capFirst:true,capLast:true);
            // Two panels pressed into it, and the knob.
            k.Tube(NativeFallenKit.Curve(Arch(w*.62f,h*.78f,.004f,0,n).ConvertAll(p=>p+new Vector3(w*.5f,h*.08f,0)),2),.004f,4,NativeTopDogModels.RedDark,line:false);
            k.Ellipsoid(new Vector3(w*.82f,h*.42f,.012f),Vector3.UnitX*.013f,Vector3.UnitY*.013f,Vector3.UnitZ*.01f,8,5,NativeTopDogModels.Mustard);
            k.Finish();return k;
        }
    }
}
