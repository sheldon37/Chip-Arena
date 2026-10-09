using System;

namespace ChipArena.UnityConversion
{
    // C16 private cinematic cameras, ported from the pinned source: client/demon-cinematic.ts (the Raging Demon victim
    // screen, on shared/raging-demon.ts's clock) and client/siren/cutscene.ts (the Last Laugh grab cutscene). Both are seen
    // only by the pilots in them; everyone else keeps the arena camera. Everything here is in the source's right-handed
    // frame (yaw in radians, facing (sin,0,cos), the right hand (-cos,0,sin)); Runtime/NativeCinematicCamera.cs mirrors
    // Unity's X into it and back. Tests/CinematicCameraChecks.cs compares it with tools/export_cinematic_cameras.mjs.
    public static class NativeDemonScreen
    {
        // RAGING_DEMON (shared/raging-demon.ts).
        public const float Contact=.55f,Combo=.72f,Symbol=2.90f,Reveal=3.55f,End=4.25f;
        private static readonly float[] Hits={.80f,1.02f,1.24f,1.46f,1.69f,1.94f,2.21f,2.56f};
        private static readonly float[] Angles={1.35f,-1.25f,.75f,-.55f,0},Heights={.35f,.15f,-.2f,.4f,.25f};
        public struct Frame {public float Age,Fov,R,G,B;public bool Black;public int Shot;public V3 Eye,Look;}
        public static int HitCount=>Hits.Length;
        public static float Hit(int i)=>Hits[i];
        /// <summary>demonBeat: the last combo hit at or before age, and the seconds since it (0 before the first).</summary>
        public static int Beat(float age,out float since)
        {int index=-1;for(int i=0;i<Hits.Length;i++)if(age>=Hits[i])index=i;since=index<0?0:age-Hits[index];return index;}
        /// <summary>
        /// demonScreen + DemonCinematic.update: the victim's private camera at age seconds after the death, or false outside
        /// [contact, end). death is the body centre (the scene root, which settles to .88 m); attacker (when hasAttacker) the killer's body centre at the death; the
        /// colour is the backdrop in linear (three's working space), black for the opening cut, red until the symbol, then
        /// near black, lifted toward 0xb80b29 by each combo hit's pulse.
        /// </summary>
        public static bool Screen(float age,V3 death,float deathYaw,bool hasAttacker,V3 attacker,float aspect,out Frame frame)
        {
            frame=default;if(!(age>=Contact&&age<End))return false;
            int index=Beat(age,out float since);double pulse=index>=0?Math.Max(0,1-since/.17):0;bool black=age<Combo;
            int hex=black?0x000000:age<Symbol?0x8b001a:0x11020e;double r=Linear(hex>>16),g=Linear(hex>>8),b=Linear(hex);
            if(!black){double t=pulse*.2;r+=(Linear(0xb8)-r)*t;g+=(Linear(0x0b)-g)*t;b+=(Linear(0x29)-b)*t;}
            double sx=hasAttacker?attacker.x-death.x:-3,sz=hasAttacker?attacker.z-death.z:0;
            if(Math.Sqrt(sx*sx+sz*sz)<2){sx=-Math.Sin(deathYaw)*3;sz=-Math.Cos(deathYaw)*3;}
            double yaw=Math.Atan2(-sx,-sz);int shot=age<Symbol?(int)Math.Max(0,Math.Floor((index+1)/2.0))%4:4;double distance=shot==4?3.5:shot==2?2.8:2.5;
            double angle=yaw+Angles[shot],ex=Math.Sin(angle)*distance,ey=Heights[shot],ez=Math.Cos(angle)*distance;
            if(aspect<1.35f){double s=1.35/Math.Max(.65,aspect);ex*=s;ey*=s;ez*=s;}
            // The scene root settles from the body centre to .88 m over .6 s from contact (raging-demon.ts update).
            double settle=Math.Min(1,Math.Max(0,(age-Contact)/.6));settle=settle*settle*(3-2*settle);
            var center=new V3(death.x,(float)(death.y+(.88-death.y)*settle+.18),death.z);
            frame=new Frame{Age=age,Black=black,Shot=shot,Look=center,Eye=new V3((float)(center.x+ex),(float)(center.y+ey),(float)(center.z+ez)),Fov=shot==4?42:48,R=(float)r,G=(float)g,B=(float)b};
            return true;
        }
        /// <summary>three's SRGBToLinear for one 8-bit channel (Color.setHex converts into the linear working space).</summary>
        public static double Linear(int channel)
        {double c=(channel&255)/255.0;return c<.04045?c*.0773993808:Math.Pow(c*.9478672986+.0521327014,2.4);}
    }

    /// <summary>
    /// SirenCutscene: the Last Laugh as the held player and the Siren's player see it. Check decides once per frame whether
    /// the local pilot is in a hold; Track follows the two bodies (easing the Siren's facing); Shot is the authored shot list
    /// by seconds since the catch; Letterbox is the bar slide. One instance per viewer; no allocations after construction.
    /// </summary>
    public sealed class NativeSirenCutscene
    {
        // SIREN_TIMING.laugh (shared/siren-contract.ts) and cutscene.ts's own constants.
        public const float Crush=3.2f,Release=3.8f,Tail=1.75f,Bars=.12f,Slide=.3f,Follow=.3f;
        public struct Shot {public V3 Position,Look;public float Fov,Shake;}
        public bool Active{get;private set;}
        public bool VictimRole{get;private set;}
        public int Owner{get;private set;}
        public int Victim{get;private set;}
        public float At{get;private set;}
        public float CrushAt{get;private set;}
        public float End{get;private set;}
        private V3 anchor,sirenHead,victimHead;private float yaw,last,scale=1;private bool aimed,known;
        /// <summary>
        /// check(): grab is the hold involving the local pilot this frame (owner/victim seat ids, its catch, crush and release
        /// times), victimIsMe its role. A new hold starts a story; one that vanishes before the crush (the Siren died, the
        /// victim was freed) cuts back at once, one that ends at the crush plays out; time out of range or a phase other
        /// than fight/result ends it.
        /// </summary>
        public bool Check(bool grab,int owner,int victim,bool victimIsMe,float at,float crushAt,float releaseAt,float time,bool phaseOk)
        {
            if(grab&&(!Active||At!=at||Owner!=owner))
            {Active=true;Owner=owner;Victim=victim;VictimRole=victimIsMe;At=at;CrushAt=crushAt;End=victimIsMe?crushAt+Tail:Math.Max(crushAt+.3f,releaseAt);known=false;aimed=false;}
            else if(!grab&&Active&&time<CrushAt-.08f)Active=false;
            if(Active&&(time>=End||time<At-.5f||!phaseOk))Active=false;
            return Active;
        }
        /// <summary>stop(): end any cutscene immediately (killcam, menus, a new arena).</summary>
        public void Stop()=>Active=false;
        /// <summary>
        /// track(): owner (when present) is the Siren's body centre, targetYaw its facing (the rig's when drawn), visualScale its
        /// grown size; heads are rig head positions when known (the Siren's always, the victim's only while alive).
        /// </summary>
        public void Track(float time,bool owner,V3 ownerPos,float targetYaw,float visualScale,bool sirenHeadKnown,V3 sirenHeadAt,bool victimHeadKnown,V3 victimHeadAt)
        {
            if(owner)scale=visualScale;float k=scale;
            if(owner)
            {
                anchor=ownerPos;double dt=Math.Min(.1,Math.Max(0,(double)time-last));
                if(!aimed){yaw=targetYaw;aimed=true;}else yaw+=(float)(Math.Atan2(Math.Sin(targetYaw-yaw),Math.Cos(targetYaw-yaw))*(1-Math.Exp(-dt/Follow)));
            }
            last=time;V3 f=Facing();
            sirenHead=sirenHeadKnown?new V3(sirenHeadAt.x,sirenHeadAt.y+.12f*k,sirenHeadAt.z):new V3(anchor.x,anchor.y+.95f*k,anchor.z);
            if(victimHeadKnown){victimHead=new V3(victimHeadAt.x,victimHeadAt.y+.12f,victimHeadAt.z);known=true;}
            else if(!known)victimHead=new V3(anchor.x+f.x*.95f*k,anchor.y+.75f*k,anchor.z+f.z*.95f*k);
        }
        /// <summary>The shot for the current story at time (the shot list, by seconds since the catch).</summary>
        public Shot ShotAt(float time)
        {
            float t=time-At,crush=CrushAt-At,k=scale,ground=anchor.y-.85f*k;V3 f=Facing(),mid=Lerp(sirenHead,victimHead,.5f);
            // 1. The lift: low, in profile on the Siren's right, looking up as the victim is hauled off the floor.
            if(t<.9f){float u=Smooth(t/.9f);return new Shot{Position=Place(.8f,2.8f-.35f*u,ground+.55f*k),Look=Up(mid,-.15f*k),Fov=46,Shake=0};}
            // 2. The close-up: the victim sees the grin from below; the Siren's player gets the reverse from high front right.
            if(t<2.2f)
            {
                float u=Smooth((t-.9f)/1.3f);
                if(VictimRole)return new Shot{Position=new V3(sirenHead.x+f.x*(.6f-.06f*u)*k,sirenHead.y+(-.5f+.05f*u)*k,sirenHead.z+f.z*(.6f-.06f*u)*k),Look=Up(sirenHead,.1f*k),Fov=46-5*u,Shake=.002f};
                return new Shot{Position=Place(2.3f-.2f*u,1.7f-.2f*u,sirenHead.y+(1.4f-.15f*u)*k),Look=Up(sirenHead,-.05f*k),Fov=36-4*u,Shake=.002f};
            }
            // 3. The grip: an eye-level two-shot, creeping in while the frame starts to tremble.
            if(t<crush-.12f){float u=Smooth((t-2.2f)/Math.Max(.1f,crush-2.32f));return new Shot{Position=Place(1.6f-.15f*u,3.2f-.6f*u,mid.y+(-.2f+.05f*u)*k),Look=Up(mid,-.15f*k),Fov=40-6*u,Shake=.002f+.008f*u};}
            // 4. The crush and after: a hard cut high off the front right, a jolt, then a slow crane up and back while the body
            //    drops at the Siren's feet (the victim is player-sized, so where it lies does not scale).
            float c=t-crush,w=Smooth((c-.3f)/(Tail-.3f));var fallen=new V3(anchor.x+f.x*.85f*k,ground+.75f,anchor.z+f.z*.85f*k);
            var look=Lerp(new V3(victimHead.x,victimHead.y-.3f,victimHead.z),fallen,Smooth((c-.35f)/1.1f));float jolt=Clamp(1-c/.35f,0,1);
            return new Shot{Position=Place(2.7f+.7f*w,2.7f+.7f*w,ground+(3.3f+.8f*w)*k),Look=look,Fov=38+6*w,Shake=c>=0?.06f*jolt*jolt:0};
        }
        /// <summary>The bars' slide in at the catch and out at the end: 0 hidden, 1 fully in.</summary>
        public float Letterbox(float time)=>Math.Min(Smooth((time-At)/Slide),Smooth((End-time)/Slide));
        /// <summary>The camera shake after lookAt: world x/y offsets and a roll about the view axis (radians).</summary>
        public static void Shake(float time,float shake,out float dx,out float dy,out float roll)
        {double t=time*60.0;dx=(float)(Math.Sin(t*1.7)*shake);dy=(float)(Math.Sin(t*2.3+1)*shake);roll=(float)(Math.Sin(t*1.3+2)*shake*.6);}
        /// <summary>Keeps the shot out of walls: hit is the distance from look toward position to the first solid surface.</summary>
        public static V3 Occlude(V3 position,V3 look,float hit)
        {V3 dir=position-look;float d=dir.Length;if(d<=.05f||hit<0||hit>=d)return position;dir=dir/d;return look+dir*Math.Max(.35f,hit-.18f);}
        private V3 Facing()=>new V3((float)Math.Sin(yaw),0,(float)Math.Cos(yaw));
        // at(ahead,right,y) as Place: ahead along the facing and right toward the Siren's right hand, scaled; y absolute.
        private V3 Place(float ahead,float right,float y){V3 f=Facing();float k=scale;return new V3(anchor.x+f.x*ahead*k-f.z*right*k,y,anchor.z+f.z*ahead*k+f.x*right*k);}
        private static V3 Up(V3 v,float y)=>new V3(v.x,v.y+y,v.z);
        private static V3 Lerp(V3 a,V3 b,float t)=>new V3(a.x+(b.x-a.x)*t,a.y+(b.y-a.y)*t,a.z+(b.z-a.z)*t);
        private static float Clamp(float x,float a,float b)=>Math.Min(b,Math.Max(a,x));
        private static float Smooth(float x){x=Clamp(x,0,1);return x*x*(3-2*x);}
    }
}
