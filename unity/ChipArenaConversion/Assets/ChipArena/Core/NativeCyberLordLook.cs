using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>One hardlight piece of the Cyber Lord's look for a frame: a primitive on a rig bone (source frame, the bone's space).</summary>
    public struct CyberPiece
    {
        public int Bone;public WeaponShape Shape;public Vector3 Position,Scale;public Quaternion Rotation;
        /// <summary>Linear colour (above 1: overbright for the bloom).</summary>
        public Vector3 Color;
    }

    /// <summary>
    /// The Cyber Lord's DEV look layered over any pilot body or skin (owner brief: "hardlight gauntlets and fists that form
    /// from light on equip and flare on hits; glowing seams; a floating hardlight halo or crest; a subtle data shimmer"),
    /// as glowing voxel primitives on the rig's joints (NativeSourceModels.QueuePiece: the same unlit glow pieces as weapon
    /// modules), so a new pilot body only needs the joints. Hardlight cyan with white cores and a magenta counter-accent
    /// (NativeCyberTheme), apart from L33T H4x's green matrix glyphs:
    /// - gauntlets: a hardlight cage round each forearm (edges, a wrist and an elbow band, a raised fin) and a hardlight shell
    ///   round each fist with a knuckle bar; they form edge by edge from the knuckles up over FormSeconds when the pilot is
    ///   first seen alive (FormAt), and the striking fist flares on a combo hit;
    /// - seams: thin lines down the chest, the arms and the legs, a pulse running outward along them;
    /// - the crest: a broken hexagon of light floating behind the head, turning slowly, a diamond at its heart;
    /// - shimmer: a few voxel motes blinking on and off the body on hashed time slots.
    /// Effects quality (Reduced) slows the shimmer and the crest and never strobes; the low tier drops the cage edges and the
    /// shimmer to the essentials. Pure functions of the time, the form time and the flare times; no allocation per frame.
    /// </summary>
    public static class NativeCyberLordLook
    {
        public const int Cyan=0x22e6ff,Core=0xc8fbff,Magenta=0xff3dcf,Deep=0x0a8fb3;
        public const float FormSeconds=.55f,FlareSeconds=.22f;
        public static Vector3 Lin(int hex,float gain=1){NativeBlastSim.Linear(hex,out double r,out double g,out double b);return new Vector3((float)r,(float)g,(float)b)*gain;}
        private struct Spec{public int Bone;public WeaponShape Shape;public Vector3 P,S;public Quaternion R;public int Color;public float Gain;public int Part;public float Order;public bool Low;}
        // Parts: 0 left gauntlet, 1 right gauntlet, 2 left fist, 3 right fist, 4 seams, 5 crest.
        private const int LGauntlet=0,RGauntlet=1,LFist=2,RFist=3,Seams=4,Crest=5;
        private static readonly Spec[] specs=Build();
        public static int Count=>specs.Length;
        private static Spec[] Build()
        {
            var l=new System.Collections.Generic.List<Spec>();
            void Add(int bone,WeaponShape shape,Vector3 p,Vector3 s,Quaternion r,int color,float gain,int part,float order,bool low=true)=>l.Add(new Spec{Bone=bone,Shape=shape,P=p,S=s,R=r,Color=color,Gain=gain,Part=part,Order=order,Low=low});
            const float e=.014f;
            foreach(bool left in new[]{true,false})
            {
                float sx=left?1:-1;int elbow=left?CapturedRig.LElbow:CapturedRig.RElbow,hand=left?CapturedRig.LHand:CapturedRig.RHand,g=left?LGauntlet:RGauntlet,f=left?LFist:RFist;
                // The forearm cage round the forearm shell (box (0,-.13,0) .16 x .20 x .17), a touch outside it.
                float hx=.094f,hz=.1f,top=-.035f,bottom=-.235f,mid=(top+bottom)/2,len=top-bottom;
                for(int i=0;i<4;i++){float x=(i&1)==0?-hx:hx,z=(i&2)==0?-hz:hz;Add(elbow,WeaponShape.Cube,new Vector3(x,mid,z),new Vector3(e,len,e),Quaternion.Identity,Cyan,1.5f,g,.35f+.1f*(i&1),i<2);}
                foreach(float y in new[]{bottom+.012f,top})
                {
                    Add(elbow,WeaponShape.Cube,new Vector3(0,y,hz),new Vector3(2*hx+e,e*1.6f,e),Quaternion.Identity,Core,1.8f,g,y<mid?.1f:.75f);
                    Add(elbow,WeaponShape.Cube,new Vector3(0,y,-hz),new Vector3(2*hx+e,e*1.6f,e),Quaternion.Identity,Cyan,1.4f,g,y<mid?.12f:.78f,false);
                    Add(elbow,WeaponShape.Cube,new Vector3(hx,y,0),new Vector3(e,e*1.6f,2*hz+e),Quaternion.Identity,Cyan,1.4f,g,y<mid?.14f:.8f,false);
                    Add(elbow,WeaponShape.Cube,new Vector3(-hx,y,0),new Vector3(e,e*1.6f,2*hz+e),Quaternion.Identity,Cyan,1.4f,g,y<mid?.16f:.82f,false);
                }
                // A raised hardlight fin along the outside of the forearm, and a plate on its back.
                Add(elbow,WeaponShape.Cube,new Vector3(sx*(hx+.03f),mid-.01f,-.01f),new Vector3(.012f,len*.78f,.06f),CapturedRig.Euler(.12f),Magenta,1.6f,g,.55f);
                Add(elbow,WeaponShape.Cube,new Vector3(0,mid,hz+.006f),new Vector3(.07f,len*.55f,.008f),Quaternion.Identity,Deep,2.4f,g,.45f);
                // The fist shell round the hand (boxes about (0,-.045,.03), .15 x .10 x .17) and the knuckle bar on its striking face.
                float fx=.088f,fy0=-.005f,fy1=-.115f,fz0=-.06f,fz1=.125f,fym=(fy0+fy1)/2,fzm=(fz0+fz1)/2;
                for(int i=0;i<4;i++){float x=(i&1)==0?-fx:fx,y=(i&2)==0?fy0:fy1;Add(hand,WeaponShape.Cube,new Vector3(x,y,fzm),new Vector3(e,e,fz1-fz0),Quaternion.Identity,Cyan,1.6f,f,.05f+.02f*i,i>=2);}
                for(int i=0;i<4;i++){float x=(i&1)==0?-fx:fx,z=(i&2)==0?fz0:fz1;Add(hand,WeaponShape.Cube,new Vector3(x,fym,z),new Vector3(e,fy0-fy1,e),Quaternion.Identity,Cyan,1.6f,f,.06f+.02f*i,false);}
                Add(hand,WeaponShape.Cube,new Vector3(0,fy1+.012f,fz1-.025f),new Vector3(2*fx,.026f,.032f),Quaternion.Identity,Core,2.4f,f,0);
                Add(hand,WeaponShape.Cube,new Vector3(0,fym,fz1+.004f),new Vector3(2*fx-.02f,.02f,.008f),Quaternion.Identity,Magenta,1.5f,f,.02f,false);
            }
            // Seams: down the chest front, over the shoulders, the outside of the upper arms, thighs and shins.
            Add(CapturedRig.Chest,WeaponShape.Cube,new Vector3(.07f,.1f,.164f),new Vector3(.01f,.24f,.008f),CapturedRig.Euler(0,0,.18f),Cyan,1.3f,Seams,0);
            Add(CapturedRig.Chest,WeaponShape.Cube,new Vector3(-.07f,.1f,.164f),new Vector3(.01f,.24f,.008f),CapturedRig.Euler(0,0,-.18f),Cyan,1.3f,Seams,0);
            Add(CapturedRig.Chest,WeaponShape.Cube,new Vector3(0,.232f,.152f),new Vector3(.3f,.01f,.008f),Quaternion.Identity,Core,1.4f,Seams,.1f,false);
            Add(CapturedRig.Spine,WeaponShape.Cube,new Vector3(0,.07f,.14f),new Vector3(.24f,.01f,.008f),Quaternion.Identity,Cyan,1.1f,Seams,.2f,false);
            foreach(bool left in new[]{true,false})
            {
                float sx=left?1:-1;
                Add(left?CapturedRig.LArm:CapturedRig.RArm,WeaponShape.Cube,new Vector3(sx*.093f,-.15f,0),new Vector3(.008f,.2f,.012f),Quaternion.Identity,Cyan,1.2f,Seams,.35f);
                Add(left?CapturedRig.LClav:CapturedRig.RClav,WeaponShape.Cube,new Vector3(sx*.065f,.052f,.04f),new Vector3(.15f,.008f,.012f),Quaternion.Identity,Cyan,1.2f,Seams,.25f,false);
                Add(left?CapturedRig.LHip:CapturedRig.RHip,WeaponShape.Cube,new Vector3(sx*.1f,-.13f,0),new Vector3(.008f,.2f,.012f),Quaternion.Identity,Cyan,1.1f,Seams,.5f);
                Add(left?CapturedRig.LKnee:CapturedRig.RKnee,WeaponShape.Cube,new Vector3(sx*.09f,-.14f,.02f),new Vector3(.008f,.18f,.012f),Quaternion.Identity,Cyan,1.1f,Seams,.7f,false);
            }
            // The crest: a broken hexagon (the top bar left out) behind the head, a diamond at its heart.
            for(int i=0;i<6;i++)
            {
                if(i==0)continue;float a=i*(float)Math.PI/3,r=.25f;var p=new Vector3((float)Math.Sin(a)*r,(float)Math.Cos(a)*r,0);
                Add(CapturedRig.Head,WeaponShape.Cube,p,new Vector3(.235f,.018f,.018f),CapturedRig.Euler(0,0,-a),i%2==0?Core:Cyan,1.7f,Crest,i/6f);
            }
            Add(CapturedRig.Head,WeaponShape.Cube,Vector3.Zero,new Vector3(.045f,.045f,.012f),CapturedRig.Euler(0,0,(float)Math.PI/4),Magenta,1.8f,Crest,1);
            return l.ToArray();
        }
        /// <summary>The crest's centre behind the head (head joint space) and its tilt.</summary>
        public static readonly Vector3 CrestAt=new Vector3(0,.19f,-.24f);
        /// <summary>The bones the first-person view model keeps (NativeViewRules.ViewModelBone): the arms only show there.</summary>
        public static bool ViewBone(int bone)=>NativeViewRules.ViewModelBone(bone);
        private static float H(int n,int salt){unchecked{uint h=(uint)n*0x9e3779b1u^(uint)salt*0x85ebca6bu;h^=h>>15;h*=0x2c1b3c6du;h^=h>>12;h*=0x297a2d39u;h^=h>>15;return (h&0xffffff)/16777216f;}}
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}

        /// <summary>The flare state of each fist at a moment (0..1), from the pilot's last combo hit (CyberLordFx.LastStrike).</summary>
        public struct Flare{public float Left,Right,Charge;}
        public static Flare FlareAt(float time,float strikeAt,int step,float chargeAt)
        {
            var f=new Flare();float s=time-strikeAt;if(s>=0&&s<FlareSeconds){float k=1-s/FlareSeconds;k*=k;if(step!=1)f.Left=k;if(step!=0)f.Right=k;}
            float c=time-chargeAt;if(c>=0&&c<.5f)f.Charge=(1-c/.5f);return f;
        }
        /// <summary>
        /// The look's pieces at `time` into `into` (returns the count): formed from `formAt`, the fists flared by `flare`,
        /// `first` keeps the arms only (the first-person view model), `solid` dims the whole look with the body (the Data Dash's
        /// translucency). `quality` .3 is Reduced; `low` the low graphics tier.
        /// </summary>
        public static int Pieces(float time,float formAt,Flare flare,bool first,float solid,float quality,bool low,CyberPiece[] into)
        {
            int n=0;bool reduced=quality<.5f;float form=time-formAt;
            float crestTurn=time*(reduced?.25f:.6f);
            for(int i=0;i<specs.Length&&n<into.Length;i++)
            {
                var s=specs[i];if(low&&!s.Low)continue;if(first&&!ViewBone(s.Bone))continue;if(first&&s.Part==Crest)continue;
                // Formation: each piece flickers in at its order along the forearm (knuckles first), the crest and seams after.
                float delay=s.Order*FormSeconds*.7f+(s.Part>=Seams?FormSeconds*.35f:0),u=Clamp01((form-delay)/(FormSeconds*.3f));
                if(u<=0)continue;
                float flick=u<1&&!reduced?(H((int)(time*40)+i*13,7)>.35f?1:.25f):1;
                float gain=s.Gain*flick*(.35f+.65f*Smooth(u));
                // Flare on a hit: the striking fist and its gauntlet brighten and swell; the charge (mana gained) lights the gauntlets.
                float fl=s.Part==LFist||s.Part==LGauntlet?flare.Left:s.Part==RFist||s.Part==RGauntlet?flare.Right:0;
                float charge=s.Part<=RFist?flare.Charge:0;
                gain*=1+1.6f*fl+.8f*charge;float swell=1+(s.Part>=LFist&&s.Part<=RFist?.18f:.06f)*fl;
                // Seams carry a pulse running outward from the chest (period 2.4 s; slower with reduced effects).
                if(s.Part==Seams){float wave=(float)Math.Sin((time*(reduced?1.2f:2.6f)-s.Order*4)*Math.PI);gain*=.7f+.3f*wave*wave;}
                var c=new CyberPiece{Bone=s.Bone,Shape=s.Shape,Position=s.P,Scale=s.S*swell*(.6f+.4f*Smooth(u)),Rotation=s.R,Color=Lin(s.Color,gain*Clamp01(solid))};
                if(s.Part==Crest)
                {
                    // The crest floats behind the head with a slow bob, the hexagon turning; the diamond counter-turns.
                    var spin=Quaternion.CreateFromAxisAngle(Vector3.UnitZ,s.Order>=1?-crestTurn*1.7f:crestTurn);var tilt=CapturedRig.Euler(-.25f);
                    float bob=.012f*(float)Math.Sin(time*(reduced?1.1f:1.9f));
                    c.Position=CrestAt+new Vector3(0,bob,0)+Vector3.Transform(Vector3.Transform(s.P,spin),tilt);c.Rotation=tilt*spin*s.R;
                }
                if(solid<.999f&&s.Part!=Crest)c.Scale*=.85f+.15f*solid;
                into[n++]=c;
            }
            // Data shimmer: motes blinking on the body's boxes on hashed slots (8 Hz; 2.5 Hz and fewer with reduced effects).
            int motes=first?2:low?2:reduced?3:6;float rate=reduced?2.5f:8;int slot=(int)Math.Floor(time*rate);
            for(int k=0;k<motes&&n<into.Length;k++)
            {
                if(form<FormSeconds)break;int seed=slot*31+k*7;if(H(seed,3)>.55f)continue;
                var box=CapturedRig.Boxes[(int)(H(seed,5)*CapturedRig.Boxes.Length)%CapturedRig.Boxes.Length];if(first&&!ViewBone(box.Bone))continue;
                var face=new Vector3((H(seed,11)-.5f)*box.Size.X,(H(seed,13)-.5f)*box.Size.Y,box.Size.Z*.5f+.006f);if(H(seed,17)>.5f)face.Z=-face.Z;
                float size=.014f+.012f*H(seed,19);
                into[n++]=new CyberPiece{Bone=box.Bone,Shape=WeaponShape.Cube,Position=box.Position+face,Scale=new Vector3(size,size,.006f),Rotation=Quaternion.Identity,Color=Lin(H(seed,23)>.8f?Magenta:Cyan,1.8f*Clamp01(solid))};
            }
            return n;
        }
        /// <summary>The most pieces Pieces writes (the buffer size).</summary>
        public static int Capacity=>specs.Length+8;

        /// <summary>
        /// How solid the pilot's body is drawn (NativeSourceModels part opacity: below 1 the pilot's own shader dithers it away,
        /// a pixelated look): the Data Dash's dash goes translucent and pixelated, back over its recovery; Kernel Panic's
        /// fork bomb glitches the caster out and back (KernelPanicPath.CasterSolid). 1 otherwise.
        /// </summary>
        public static float BodySolid(CyberLordView v,float time)
        {
            float solid=1;var q=v.Phase(CyberLordSpec.Q,time);
            if(q.Live)solid=q.Part==0?1-.78f*Smooth(q.U*1.4f-.4f):q.Part==1?.22f+.08f*(float)Math.Sin(q.U*Math.PI*6):.22f+.78f*Smooth(q.U);
            float age=v.PanicAge(time);if(!float.IsNaN(age)&&v.PanicLive(time))solid=Math.Min(solid,Math.Max(.0f,KernelPanicPath.CasterSolid(age)));
            return solid;
        }
    }
}
