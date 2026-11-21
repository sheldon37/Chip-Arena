using System;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// A pilot on the model who falls into an open Ward Zero song vent drops down its shaft as its own ragdoll (Runtime
    /// NativeVentFallPresentation.cs; owner 2026-11-13: "Falling through vent still reverts back to voxel model, but when falling down
    /// the player should ragdoll (If they are far enough in vent)"). Far enough is the rules' own kill (NativeVentRules.Falls: the
    /// pilot's centre within .78 m of the vent's on both axes, the vent at least half open): until then the authority's floor holds the
    /// pilot up and it keeps its control and its pose; from that tick the model ragdolls from the pose it had.
    /// <list type="bullet">
    /// <item>The shaft is the one the stage draws (NativeWardPit: a 2.1 m opening, its walls 7 m deep). The ragdoll's own physics world
    /// has it: the floor laid round the opening, four walls and a bottom.</item>
    /// <item>The body hides once its pelvis is <see cref="HideBelow"/> under the vent's floor (the open pit's quad already covers what is
    /// under the lip; the hide keeps it out of sight once the grate closes over it).</item>
    /// <item>PvE's splat (the kept body landing on its spot, VENT_PIT.drop after its record turns 'vent-splat'): the ragdoll starts
    /// <see cref="SplatLift"/> over the spot, tipping forward, and lands face down at the drop, with the scripted splat's dust and
    /// SPLAT! pop (NativeVentDeath).</item>
    /// </list>
    /// The scripted vent-fall.ts victim (NativeVentDeath, drawn on the voxel body) stays for the voxel bodies: Settings · Player model
    /// Classic, the --ca-voxel-bodies launch, a client without the bake. Presentation only: the kill, its timing, credit, the splat's spot
    /// and time and the network frame are the rules'.
    /// </summary>
    public static class NativeVentFall
    {
        /// <summary>The opening's half width (NativeWardPit's 2.1 m quad), the shaft's depth (its 7 m) and its walls' thickness.</summary>
        public const float Half=1.05f,Depth=7,Wall=.4f;
        /// <summary>How far under the vent's floor the pelvis goes before the body hides.</summary>
        public const float HideBelow=2.4f;
        /// <summary>A floor whose top is this near the vent's floor is the floor the vent opens in.</summary>
        public const float FloorReach=.35f;
        /// <summary>The fall's start (in a body's ragdoll, its parts' share): straight down, the arms thrown up, a forward tip (rad/s
        /// about the body's right).</summary>
        public const float FallSpeed=1.5f,ArmsUp=3.4f,FallTip=1.2f;
        /// <summary>The corpse's gravity (NativeCorpse.Force: 18 m/s²) and how high over its spot the splat's ragdoll starts.</summary>
        public const float BodyGravity=18,SplatLift=10;
        /// <summary>The splat's start speed (m/s, down): from <see cref="SplatLift"/> it is back at its spot's height at
        /// VENT_PIT.drop (NativeVentDeath.Drop) under <see cref="BodyGravity"/>.</summary>
        public static float SplatSpeed=>(SplatLift-.5f*BodyGravity*NativeVentDeath.Drop*NativeVentDeath.Drop)/NativeVentDeath.Drop;
        /// <summary>The splat's forward tip (rad/s about the body's right): a quarter turn over the drop, so it lands face down.</summary>
        public static float SplatTip=>(float)(Math.PI/2)/NativeVentDeath.Drop;
        /// <summary>Whether a death is a fall into a vent's shaft (the record's 'vent-fall'; 'vent-splat' lands on the floor).</summary>
        public static bool Falls(string style)=>style=="vent-fall";
        /// <summary>The vent (index into <paramref name="vents"/>) whose opening holds a death at <paramref name="at"/>, or -1.</summary>
        public static int Vent(V3 at,V3[] vents)
        {
            if(vents==null)return -1;int best=-1;float near=float.MaxValue;
            for(int i=0;i<vents.Length;i++)
            {
                var v=vents[i];float dx=Math.Abs(at.x-v.x),dz=Math.Abs(at.z-v.z);if(dx>Half||dz>Half||at.y<v.y-1||at.y>v.y+2.5f)continue;
                float d=dx*dx+dz*dz;if(d<near){near=d;best=i;}
            }
            return best;
        }
        /// <summary>Whether an axis-aligned box (<paramref name="min"/>, <paramref name="max"/>) is a floor the opening at
        /// <paramref name="vent"/> cuts: its top at the vent's floor and its plan over some of the opening.</summary>
        public static bool Cuts(V3 min,V3 max,V3 vent)=>Math.Abs(max.y-vent.y)<=FloorReach&&min.x<vent.x+Half&&max.x>vent.x-Half&&min.z<vent.z+Half&&max.z>vent.z-Half;
        /// <summary>The box with the opening's column cut out of it: up to four boxes (west, east, then south and north between them)
        /// written to <paramref name="pieces"/> as min, max pairs (eight slots). Returns how many.</summary>
        public static int Around(V3 min,V3 max,V3 vent,V3[] pieces)
        {
            float x0=vent.x-Half,x1=vent.x+Half,z0=vent.z-Half,z1=vent.z+Half;int n=0;
            void Add(float ax,float az,float bx,float bz){if(bx-ax>1e-4f&&bz-az>1e-4f){pieces[n*2]=new V3(ax,min.y,az);pieces[n*2+1]=new V3(bx,max.y,bz);n++;}}
            Add(min.x,min.z,Math.Min(max.x,x0),max.z);Add(Math.Max(min.x,x1),min.z,max.x,max.z);
            float cx0=Math.Max(min.x,x0),cx1=Math.Min(max.x,x1);Add(cx0,min.z,cx1,Math.Min(max.z,z0));Add(cx0,Math.Max(min.z,z1),cx1,max.z);
            return n;
        }
        /// <summary>The shaft under the vent's floor: its four walls round the opening (west, east, south, north) and its bottom, as
        /// min, max pairs (ten slots).</summary>
        public static void Shaft(V3 vent,V3[] boxes)
        {
            float x0=vent.x-Half,x1=vent.x+Half,z0=vent.z-Half,z1=vent.z+Half,top=vent.y,bottom=vent.y-Depth;
            boxes[0]=new V3(x0-Wall,bottom,z0-Wall);boxes[1]=new V3(x0,top,z1+Wall);
            boxes[2]=new V3(x1,bottom,z0-Wall);boxes[3]=new V3(x1+Wall,top,z1+Wall);
            boxes[4]=new V3(x0,bottom,z0-Wall);boxes[5]=new V3(x1,top,z0);
            boxes[6]=new V3(x0,bottom,z1);boxes[7]=new V3(x1,top,z1+Wall);
            boxes[8]=new V3(x0-Wall,bottom-Wall,z0-Wall);boxes[9]=new V3(x1+Wall,bottom,z1+Wall);
        }
        /// <summary>Whether a body in a shaft is out of sight: its pelvis <see cref="HideBelow"/> under the vent's floor.</summary>
        public static bool Hidden(float pelvisY,float floorY)=>pelvisY<floorY-HideBelow;
    }
}
