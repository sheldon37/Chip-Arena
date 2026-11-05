using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>One shot of a Backrooms boss's intro (docs/EXPEDITION-BOSS-BACKROOMS.md): the eye and the look move from A to B between
    /// `From` and `To` (boss time, s), in the boss's own frame (x its right, y up, z the way it faces; the origin its root).</summary>
    public struct BossIntroShot
    {
        public float From,To,FovA,FovB,Shake;public Vector3 EyeA,EyeB,LookA,LookB;
        /// <summary>0 eases in and out; 1 is a constant drift, for the long slow shots; 2 a whip that lands hard.</summary>
        public byte Ease;
    }
    /// <summary>The intro's card, in the Siren's manner (Runtime NativeMatchOverlays BossTitle): a top line and progress bar from the start,
    /// and the title rising letter by letter at <see cref="NativeBackroomsBossIntro.NameAt"/>. Colours are hex.</summary>
    public sealed class BossIntroCard
    {
        public string Eyebrow,Title,Line;
        public int Ink=0x020103,TopLine=0xd9d0ff,Rule=0xb7a8ff,BarFrom=0x7d5cff,BarTo=0xf6f3ff,Letters=0xf6f3ff,Glow=0x9b87ff,SplitA=0xff5a5a,SplitB=0x5affff;
        /// <summary>The top and bottom shades before the name rises, as a share of the Siren's (1: hers; they're always hers from the name on).</summary>
        public float Shades=1;
    }
    /// <summary>
    /// The intro both Backrooms bosses share: the Siren's method (a directed entrance on the 9.0678 s intro hold, with her style of card,
    /// ending as the music drops into the fight), played in the live arena. Each boss writes its own shots
    /// (<see cref="NativeHumBoss.Shots"/>, <see cref="NativeDeepEndBoss.Shots"/>); Runtime NativeZoneBossIntroCamera asks here first.
    /// Pure: the same age gives the same camera on every peer. Presentation only.
    /// </summary>
    public static class NativeBackroomsBossIntro
    {
        /// <summary>When the card's title rises (the Siren's own name time). The hold is the simulation's.</summary>
        public const float NameAt=6.4261f,Hold=NativeZoneBossRules.IntroSeconds;
        public static BossIntroShot[] Shots(string kind)=>kind==NativeHumBoss.Archetype?NativeHumBoss.Shots:kind==NativeDeepEndBoss.Archetype?NativeDeepEndBoss.Shots:null;
        public static BossIntroCard Card(string kind)=>kind==NativeHumBoss.Archetype?NativeHumBoss.Card:kind==NativeDeepEndBoss.Archetype?NativeDeepEndBoss.Card:null;
        public static bool Mine(string kind)=>kind==NativeHumBoss.Archetype||kind==NativeDeepEndBoss.Archetype;
        /// <summary>The camera at boss time `age`, from the boss's `root` and `yaw` (degrees): false when there is no shot then.</summary>
        public static bool Sample(string kind,double age,Vector3 root,float yaw,out Vector3 eye,out Vector3 look,out float fov)
        {
            eye=look=default;fov=60;var shots=Shots(kind);if(shots==null||shots.Length==0||double.IsNaN(age)||double.IsInfinity(age)||age<0)return false;
            return Sample(shots,(float)age,root,yaw,out eye,out look,out fov);
        }
        /// <summary>The camera at `t` on these shots; the last shot holds its end pose until the hold's end.</summary>
        public static bool Sample(BossIntroShot[] shots,float t,Vector3 root,float yaw,out Vector3 eye,out Vector3 look,out float fov)
        {
            eye=look=default;fov=60;if(shots==null||shots.Length==0||t<0)return false;
            int at=shots.Length-1;for(int i=0;i<shots.Length;i++)if(t<shots[i].To){at=i;break;}
            var s=shots[at];float span=s.To-s.From,u=span>0?Clamp01((t-s.From)/span):1;float k=Shape(s.Ease,u);
            var e=Vector3.Lerp(s.EyeA,s.EyeB,k);var l=Vector3.Lerp(s.LookA,s.LookB,k);fov=s.FovA+(s.FovB-s.FovA)*k;
            if(s.Shake>0)e+=Jolt(t)*s.Shake;
            float r=yaw*(float)Math.PI/180,c=(float)Math.Cos(r),n=(float)Math.Sin(r);
            Vector3 World(Vector3 p)=>root+new Vector3(p.X*c+p.Z*n,p.Y,p.Z*c-p.X*n);
            eye=World(e);look=World(l);return true;
        }
        private static float Clamp01(float v)=>v<0?0:v>1?1:v;
        private static float Shape(byte ease,float u)
        {
            if(ease==1)return u;
            if(ease==2){float i=1-u;return 1-i*i*i*i;}
            return u*u*(3-2*u);
        }
        private static Vector3 Jolt(float t)=>new Vector3((float)(Math.Sin(t*37)+Math.Sin(t*61)*.5),(float)(Math.Sin(t*43+1)+Math.Sin(t*71)*.5),(float)Math.Sin(t*53+2))*.5f;
    }
}
