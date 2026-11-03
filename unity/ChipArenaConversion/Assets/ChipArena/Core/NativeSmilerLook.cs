using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The Smiler as an apparition, not a person (owner 2026-11-03, with a reference image: "I think the smiler shouldn't be a
    // player model. Keep in mind enemies dont have to always use the player model. The smiler is usually an black ghostly
    // apparition like this": a billowing cloud of black smoke, two glowing white eyes, a wide grin of yellowed teeth glowing red
    // at the gums). What its body (Runtime NativeSmilerPresentation: a raymarched smoke volume with its face in it,
    // Resources/NativeSmiler.shader) shows at each moment, from the authority's zone frame (Core NativeBackroomsKinds and
    // docs/EXPEDITION-ENEMY-REWORK.md section 6: the blink's .5 s wind-up and the move at its end, the lunge's .6 s, each busy .6 s
    // after), its spawn, the hits on it and its death:
    // - at rest it hangs over the floor, its smoke boiling, bobbing slowly, its grin breathing, its eyes stuttering now and then
    //   like the dying fixtures round it;
    // - the blink: the eyes shut, the grin goes, the smoke thins and spreads to nothing by the move; it gathers again where the
    //   authority put it (or where it was, when the move was refused), the eyes opening and the grin last;
    // - the lunge: the wind-up draws the cloud back and swells it, the grin widening and parting, the eyes narrowing and flaring;
    //   at the strike a lobe of smoke is thrown out ahead with the face riding it, then hauled back;
    // - a hit roils the smoke and makes the face stutter; its death thins and spreads the smoke as it rises, the eyes going out
    //   first and the grin last, widening as it fades.
    // Its face always turns to whoever looks at it (the shader places it on the side of the cloud toward the camera).
    // Presentation only: nothing here is read by a rule, a hit volume or the network.
    public static class NativeSmilerLook
    {
        public static readonly int Kind=NativeHordeKinds.Resolve("smiler");
        /// <summary>The cloud's core over the floor (m; the kind is 2.2 m tall) and the proxy box round the cloud in its own space
        /// (x right, y up, z ahead, the core at the origin), before the lunge's reach.</summary>
        public const float Core=1.42f;
        public static readonly Vector3 BoxMin=new Vector3(-1.3f,-1.42f,-1.3f),BoxMax=new Vector3(1.3f,1.05f,1.3f);
        /// <summary>How far ahead the lunge throws its lobe of smoke at the peak (m; the authority's reach is 2.5 m from the body).</summary>
        public const float Reach=1.55f;
        /// <summary>The authority's wind-ups (the zone frame's), the reform after a blink, the spawn's and the death's lengths (s).</summary>
        public const float BlinkWind=.5f,LungeWind=.6f,Reform=.5f,Appear=.9f,Dissipate=2.2f;

        /// <summary>The apparition at a moment (the shader's inputs). Presence: how much smoke there is (0..1); Spread: how far it
        /// has thinned out (0 whole); Rise: how far it has drifted up (m); Gather: the lunge's wind-up (drawn back, swollen); Surge:
        /// the lunge's lobe thrown ahead (0..1 of Reach); Churn: how fast it boils (1 at rest); Bob: its float (m); Eyes: how open
        /// and lit its eyes are; Squint: the lids' angry cut; Grin: its width (1 at rest); Open: how far the teeth part; Face: the
        /// grin's presence; Glow: the face's light.</summary>
        public struct Pose{public float Presence,Spread,Rise,Gather,Surge,Churn,Bob,Eyes,Squint,Grin,Open,Face,Glow;}

        private static float S(float t)=>t<=0?0:t>=1?1:t*t*(3-2*t);
        private static float Hash(uint seed,int i){uint h=seed*0x9E3779B1u^unchecked((uint)i*0x85EBCA77u);h^=h>>15;h*=0x2C1B3C6Du;h^=h>>12;h*=0x297A2D39u;h^=h>>15;return (h&0xFFFFFF)/16777216f;}

        /// <summary>The living Smiler at `time` (the horde clock): `attack` the zone frame's ("blink", "lunge", else none) begun
        /// `age` s ago (NaN none), `spawn` s since it appeared (negative: long ago), `hit` s since the last hit on it (NaN none).</summary>
        public static Pose Living(uint seed,float time,string attack,float age,float spawn,float hit)
        {
            float ph=Hash(seed,1)*6.2831853f;
            var p=new Pose{Presence=1,Churn=1,Eyes=1,Grin=1,Face=1,Glow=1};
            p.Bob=.045f*(float)Math.Sin(time*1.25f+ph)+.015f*(float)Math.Sin(time*2.9f+ph*2);
            p.Grin=1+.035f*(float)Math.Sin(time*.8f+ph);
            // The eyes stutter now and then like a dying fixture: in some 2.7 s windows (seeded), three quick drops.
            float local=time+Hash(seed,3)*2.7f;int k=(int)Math.Floor(local/2.7f);float at=local-k*2.7f,start=.2f+Hash(seed,k*7+5)*2.1f;
            if(Hash(seed,k*7+4)<.3f&&at>=start&&at<start+.26f&&((int)((at-start)/.065f)&1)==0){p.Eyes*=.12f;p.Glow*=.6f;}
            if(spawn>=0&&spawn<Appear+.4f)
            {float u=S(spawn/Appear);p.Presence*=u;p.Spread+=.55f*(1-u);p.Eyes*=S((spawn-.55f)/.25f);p.Face*=S((spawn-.6f)/.35f);}
            if(attack=="blink"&&age>=0&&age<BlinkWind+Reform+.5f)
            {
                // Gone by the move, gathered again after it; the eyes shut first and open before the grin returns.
                float k2=S(age/BlinkWind)*(1-S((age-BlinkWind-.05f)/Reform));
                p.Presence*=1-k2;p.Spread+=.7f*k2;
                p.Eyes*=Math.Min(1,1-S((age-.04f)/.14f)+S((age-BlinkWind-.32f)/.15f));
                p.Face*=Math.Min(1,1-S((age-.12f)/.3f)+S((age-BlinkWind-.4f)/.2f));
            }
            else if(attack=="lunge"&&age>=0&&age<LungeWind+.9f)
            {
                float wind=S(age/LungeWind),strike=S((age-LungeWind)/.1f),back=S((age-LungeWind-.15f)/.6f),bare=S((age-.12f)/.4f)*(1-back);
                p.Gather=wind*(1-strike);p.Surge=strike*(1-back);
                p.Grin*=1+.16f*bare;p.Open=bare*(.55f+.45f*strike);p.Squint=.7f*S(age/.3f)*(1-back);p.Glow*=1+.7f*bare;p.Churn+=1.5f*strike*(1-back);
            }
            if(hit>=0&&hit<.6f){p.Churn+=2.2f*(float)Math.Exp(-hit*7);if(hit<.1f&&((int)(hit/.033f)&1)==0){p.Eyes*=.2f;p.Glow*=.5f;}}
            return p;
        }
        /// <summary>The dying Smiler `age` s after its death: the smoke thins, spreads and rises, the eyes go out first, the grin
        /// lingers and widens as it fades.</summary>
        public static Pose Dying(uint seed,float time,float age)
        {
            var p=Living(seed,time,null,float.NaN,-1,float.NaN);float u=S(age/Dissipate);
            p.Presence*=1-S(age/(Dissipate*.85f));p.Spread+=1.3f*u;p.Rise=.55f*u;p.Churn+=.5f+1.2f*(1-u);
            p.Eyes*=1-S((age-.08f)/.22f);
            p.Face*=1-S((age-.5f)/1.1f);p.Grin*=1+.22f*S(age/1.2f);p.Open=.25f*S(age/.3f)*(1-S((age-.9f)/.6f));p.Glow*=1+.4f*(1-S(age/.6f));
            return p;
        }
        /// <summary>Whether a death has nothing left to draw.</summary>
        public static bool Gone(float age)=>age>=Dissipate;

        // ---- the billows: a tileable 3D noise, built once off the main thread ----
        /// <summary>The noise texture's edge (texels) and its seed.</summary>
        public const int NoiseSize=48;public const uint NoiseSeed=0x5317;
        /// <summary>The raw noise at (x, y, z), periodic over 1 on every axis: x the big billows (inverted Worley over 4, 8 and 16
        /// cells with value noise under it), y the fine curls (inverted Worley over 8, 16 and 32 cells), z a value noise.</summary>
        public static Vector3 Sample(float x,float y,float z,uint seed=NoiseSeed)
        {
            float v=Value(x,y,z,4,seed+7)*.5f+Value(x,y,z,8,seed+8)*.3f+Value(x,y,z,16,seed+9)*.2f;
            float w4=Worley(x,y,z,4,seed),w8=Worley(x,y,z,8,seed+1),w16=Worley(x,y,z,16,seed+2),w32=Worley(x,y,z,32,seed+3);
            float billows=(1-w4)*.55f+(1-w8)*.3f+(1-w16)*.15f;
            return new Vector3(billows*.7f+v*.3f,(1-w8)*.55f+(1-w16)*.3f+(1-w32)*.15f,v);
        }
        /// <summary>The texture's bytes (`size`³ texels, RGBA, x fastest), each channel stretched to the full range over the volume;
        /// alpha 255.</summary>
        public static byte[] Noise(int size=NoiseSize,uint seed=NoiseSeed)
        {
            int n=size*size*size;var raw=new Vector3[n];var lo=new Vector3(float.MaxValue);var hi=new Vector3(float.MinValue);
            for(int z=0,i=0;z<size;z++)for(int y=0;y<size;y++)for(int x=0;x<size;x++,i++)
            {var s=Sample((x+.5f)/size,(y+.5f)/size,(z+.5f)/size,seed);raw[i]=s;lo=Vector3.Min(lo,s);hi=Vector3.Max(hi,s);}
            var span=Vector3.Max(hi-lo,new Vector3(1e-5f));var o=new byte[n*4];
            for(int i=0;i<n;i++){var u=(raw[i]-lo)/span;o[i*4]=B(u.X);o[i*4+1]=B(u.Y);o[i*4+2]=B(u.Z);o[i*4+3]=255;}
            return o;
        }
        private static byte B(float u)=>(byte)Math.Max(0,Math.Min(255,(int)Math.Round(u*255)));
        private static int Wrap(int i,int n)=>((i%n)+n)%n;
        private static float Cell(int x,int y,int z,int n,uint seed,int axis)=>Hash(seed,((Wrap(x,n)*n+Wrap(y,n))*n+Wrap(z,n))*3+axis);
        // The distance to the nearest feature point (one per cell, wrapped), in cells, clamped to 1.
        private static float Worley(float x,float y,float z,int n,uint seed)
        {
            float px=x*n,py=y*n,pz=z*n;int cx=(int)Math.Floor(px),cy=(int)Math.Floor(py),cz=(int)Math.Floor(pz);float best=9;
            for(int dz=-1;dz<=1;dz++)for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
            {
                int ix=cx+dx,iy=cy+dy,iz=cz+dz;
                float fx=ix+Cell(ix,iy,iz,n,seed,0)-px,fy=iy+Cell(ix,iy,iz,n,seed,1)-py,fz=iz+Cell(ix,iy,iz,n,seed,2)-pz;
                float d=fx*fx+fy*fy+fz*fz;if(d<best)best=d;
            }
            return Math.Min(1,(float)Math.Sqrt(best));
        }
        // Value noise on an n-cell periodic lattice, quintic.
        private static float Value(float x,float y,float z,int n,uint seed)
        {
            float px=x*n,py=y*n,pz=z*n;int ix=(int)Math.Floor(px),iy=(int)Math.Floor(py),iz=(int)Math.Floor(pz);
            float fx=Q(px-ix),fy=Q(py-iy),fz=Q(pz-iz);
            float L(int a,int b,int c)=>Cell(ix+a,iy+b,iz+c,n,seed,0);
            float x00=Lerp(L(0,0,0),L(1,0,0),fx),x10=Lerp(L(0,1,0),L(1,1,0),fx),x01=Lerp(L(0,0,1),L(1,0,1),fx),x11=Lerp(L(0,1,1),L(1,1,1),fx);
            return Lerp(Lerp(x00,x10,fy),Lerp(x01,x11,fy),fz);
        }
        private static float Q(float t)=>t*t*t*(t*(t*6-15)+10);
        private static float Lerp(float a,float b,float t)=>a+(b-a)*t;
    }
}
