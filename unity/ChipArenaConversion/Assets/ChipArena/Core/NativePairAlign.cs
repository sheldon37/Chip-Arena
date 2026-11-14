using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The paired moves' two bodies drawn together as the pack pairs them (owner 2026-11-14: "Fang assassinate animation doesn't actually
    /// connect to the enemy player model (Though it does function correctly, just needs animation adjustments)"; docs/FANGS.md "The
    /// paired moves"). The pack authors each pair's two takes on one floor, the victim set off the attacker, and both roots travel (the
    /// stealth kill's attacker rises and springs about 0.8 m in on the game's body to seize the foe, which is pulled about 0.3 m back).
    /// The motion bank draws every clip on its own root, and the rules hold the foe a set way off (NativeFangs.PairGap), so the drawn
    /// bodies stood apart: the hand that holds the foe's face and the fang at its throat closed on air. Per attacker clip: where the
    /// victim's drawn root stands in the attacker's as the pack pairs them (Place: x right, z ahead, m at the game body's scale, and the
    /// victim's turn against the attacker's, degrees, 30 a second over the clip span the two bodies touch in, held at its ends; measured
    /// on the bake body, each take's drawn joints, the Editor's clipframes, fitted onto the pack's, and the two takes laid together as
    /// the pair's contact puts them: the takedown's left hand on the face from 0.4 to 1.25 s, the critical's fang in the belly from its
    /// dash at 0.9 s), how it comes in (In) and goes (Out), on the attacker's clip time, and how the closing is shared (Victim: the
    /// victim's share, the attacker's the rest). Shift turns the rules' placement into each drawn body's move. Presentation only: the
    /// rules' places, facing, timing, hits and the wire are untouched; every client works it out from the replicated actors (the pair's
    /// clip and times, both pilots' places, yaws and scales).
    /// </summary>
    public static class NativePairAlign
    {
        /// <summary>The samples' rate (per clip second).</summary>
        public const float Rate=30;
        private sealed class Pair
        {
            public string Clip;public float From;public float[] Place;public float In0,In1,Out0,Out1,Victim;
        }
        // The stealth kill's throat cut (the takedown): the attacker kneels 1.45 m behind and springs in; from 0.4 s its left hand holds the
        // face, the victim's root 0.38 to 0.07 m before the attacker's (the bake draws the attacker on its root bone, 0.2 m ahead of its
        // hips as it stands behind the foe; the victim on its centre of mass, VisualMotionVictimSpot), the fang drawn across at 1.3 s.
        private static readonly float[] TakedownPlace={-.062f,1.452f,2.4f,-.052f,1.351f,2.8f,-.046f,1.235f,3.1f,-.041f,1.114f,3.4f,-.044f,.99f,3.3f,-.043f,.865f,3.4f,-.048f,.758f,3.2f,-.053f,.666f,3,-.054f,.6f,3,-.051f,.552f,3.2f,-.042f,.539f,3.9f,-.045f,.475f,3.2f,-.046f,.384f,2.3f,-.05f,.334f,1.5f,-.047f,.328f,-.2f,-.046f,.309f,0,-.048f,.295f,.2f,-.049f,.282f,.3f,-.05f,.267f,.4f,-.048f,.244f,1.2f,-.045f,.219f,1.7f,-.044f,.195f,2.1f,-.045f,.172f,1.8f,-.042f,.152f,2,-.036f,.134f,2.5f,-.033f,.121f,2.7f,-.041f,.117f,1.5f,-.048f,.115f,.3f,-.051f,.114f,.4f,-.051f,.115f,.4f,-.049f,.113f,.3f,-.047f,.112f,.1f,-.048f,.11f,-.3f,-.05f,.108f,-.3f,-.052f,.104f,-.3f,-.054f,.098f,-.1f,-.058f,.092f,.4f,-.064f,.084f,.8f,-.073f,.078f,1.2f,-.073f,.066f,-1.8f};
        // The assassin's frontal critical: a dash in at 0.83-0.9 s and the fang held in the foe's belly to 1.3 s (the blow at 1.1); the
        // bake draws the attacker on its centre of mass and keeps it facing its root (the crouch before the dash is authored turned 140
        // degrees off the foe, so only the contact is laid together; before it the placement is held at the dash's end).
        private static readonly float[] CriticalPlace={.245f,.486f,-159.7f,.268f,.515f,-159,.228f,.544f,-162.7f,.228f,.553f,-163.1f,.245f,.549f,-161.4f,.263f,.543f,-159.6f,.269f,.539f,-158.6f,.271f,.539f,-158.4f,.269f,.541f,-158.5f,.262f,.543f,-159.2f,.255f,.548f,-160.1f,.243f,.552f,-161.1f,.228f,.561f,-162.3f};
        private static readonly Pair[] Pairs=
        {
            new Pair{Clip=NativeBladeMotion.FangsTakedown,From=0,Place=TakedownPlace,In0=.02f,In1=.4f,Out0=1.3f,Out1=1.98f,Victim=.5f},
            new Pair{Clip=NativeBladeMotion.FangsCritical,From=.9f,Place=CriticalPlace,In0=.7f,In1=.92f,Out0=1.3f,Out1=1.65f,Victim=.5f},
        };
        private static Pair Find(string clip){if(clip==null)return null;foreach(var p in Pairs)if(p.Clip==clip)return p;return null;}
        /// <summary>Whether the attacker's clip `clip` is a paired move whose bodies are laid together here.</summary>
        public static bool Aligns(string clip)=>Find(clip)!=null;
        /// <summary>The attacker clip times the pairing comes in from and has gone by (`from`, `to`), false for a clip without one.</summary>
        public static bool Span(string clip,out float from,out float to){var p=Find(clip);from=p?.In0??0;to=p?.Out1??0;return p!=null;}
        /// <summary>How much of the pairing shows at attacker clip time `c` (0..1: eased in over In, held, eased out over Out).</summary>
        public static float Weight(string clip,float c)
        {
            var p=Find(clip);if(p==null||!(c>p.In0)||!(c<p.Out1))return 0;
            if(c<p.In1)return Smooth((c-p.In0)/Math.Max(1e-3f,p.In1-p.In0));
            if(c>p.Out0)return Smooth((p.Out1-c)/Math.Max(1e-3f,p.Out1-p.Out0));
            return 1;
        }
        /// <summary>Where the pack puts the victim's drawn root in the attacker's at attacker clip time `c` (x right, z ahead, m at scale 1;
        /// `yaw`: the victim's turn against the attacker's, degrees), held at the span's ends; false for a clip without a pairing.</summary>
        public static bool Authored(string clip,float c,out float x,out float z,out float yaw)
        {
            x=z=yaw=0;var p=Find(clip);if(p==null)return false;
            int n=p.Place.Length/3;float f=(c-p.From)*Rate;if(!(f>0))f=0;if(f>n-1)f=n-1;int i=Math.Min((int)f,n-2);float u=f-i;
            int a=i*3,b=a+3;x=p.Place[a]+(p.Place[b]-p.Place[a])*u;z=p.Place[a+1]+(p.Place[b+1]-p.Place[a+1])*u;
            yaw=p.Place[a+2]+DeltaAngle(p.Place[a+2],p.Place[b+2])*u;return true;
        }
        /// <summary>A flat move of a drawn body in its own root's frame: turned `Yaw` degrees about the root (+ toward its right), then moved
        /// `X` (m, right) and `Z` (m, ahead).</summary>
        public struct Move { public float X,Z,Yaw; public bool Any=>X!=0||Z!=0||Yaw!=0; }
        /// <summary>Each drawn body's move at attacker clip time `c`, from the rules' placement (the victim's root in the attacker's: `gx`
        /// right, `gz` ahead, m, and its yaw less the attacker's, `gyaw`, degrees) and the two pilots' mean scale. Toward the pack's
        /// placement by Weight, the closing shared by the pair's Victim share: the attacker's body takes the rest of it, turn and move (its
        /// part of what would carry it alone onto the pack's place against the victim's root), and the victim's whatever then still
        /// separates the two. False (no moves) outside the pairing.</summary>
        public static bool Shift(string clip,float c,float gx,float gz,float gyaw,float scale,out Move attacker,out Move victim)
        {
            attacker=victim=default;var p=Find(clip);float w=Weight(clip,c);
            if(p==null||w<=0||!Authored(clip,c,out float mx,out float mz,out float myaw))return false;
            if(!(scale>0))scale=1;mx*=scale;mz*=scale;
            // The placement shown: the rules' eased toward the pack's.
            float bx=gx+(mx-gx)*w,bz=gz+(mz-gz)*w,byaw=gyaw+DeltaAngle(gyaw,myaw)*w;
            // The attacker's part: of the move that would carry it alone (G o B^-1: turned gyaw-byaw, moved g - R(that) b), its share.
            float k=1-Clamp01(p.Victim),turn=DeltaAngle(byaw,gyaw);Turn(turn,bx,bz,out float rx,out float rz);
            attacker.Yaw=turn*k;attacker.X=(gx-rx)*k;attacker.Z=(gz-rz)*k;
            // The victim's: G^-1 o A o B (the attacker's move, then the placement shown, seen from the rules' victim root).
            Turn(attacker.Yaw,bx,bz,out float ax,out float az);ax+=attacker.X-gx;az+=attacker.Z-gz;
            Turn(-gyaw,ax,az,out victim.X,out victim.Z);victim.Yaw=DeltaAngle(gyaw,attacker.Yaw+byaw);
            return true;
        }
        /// <summary>Flat vector (x, z) turned `deg` degrees toward the right (a Unity yaw).</summary>
        public static void Turn(float deg,float x,float z,out float tx,out float tz)
        {
            double a=deg*Math.PI/180,c=Math.Cos(a),s=Math.Sin(a);tx=(float)(x*c+z*s);tz=(float)(-x*s+z*c);
        }
        private static float DeltaAngle(float from,float to){float d=(to-from)%360;if(d>180)d-=360;else if(d<-180)d+=360;return d;}
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}
    }
}
