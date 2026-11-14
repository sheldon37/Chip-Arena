using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>One cell a wound carved out of a body, in its joint's bank frame (the frame the joint's meshes upload in): the
    /// cell's centre, its three half axes and its depth below the face (m). The Prototype's are its carve's own cells; a body
    /// skin's are those cells where its wound cut lays them on the skin (recorded by the cut itself).</summary>
    public struct WoundHole {public int Bone;public Vector3 Center,U,V,W;public float Depth;}

    /// <summary>A hardlight piece of the revive: its mesh (<see cref="NativeReviveHolo.Cube"/>, Ring or Column), its world matrix
    /// (System.Numerics row vectors, Unity frame) and its linear colour, opacity in w.</summary>
    public struct ReviveGlow {public int Kind;public Matrix4x4 Matrix;public Vector4 Color;}

    /// <summary>
    /// The holographic revive (owner 2026-10-13: "Improve revive effect as well (If user was gibbed / wounded, repair with cyber
    /// holographic lines and player levitates up for a moment, returns to normal after)"). Presentation only, engine free, and a
    /// pure function of replicated data: the pilot's RevivedAt, its death record (the wound list, the destroyed part, the
    /// style) and the presentation clock, so the host, guests and the killcam show the same repair.
    /// <list type="bullet">
    /// <item>Timeline: inside the C17 revive's own window (<see cref="NativeReviveRules"/>: the scan of the body .8 s, then the
    /// stand .6 s; neither changes). The body lifts off the floor as the scan starts (<see cref="Height"/> m by .48 s), holds
    /// while it is repaired and stands, and sets down as it finishes standing, exactly on the floor at
    /// <see cref="NativeReviveRules.Seconds"/>: from then on it is the live body where the authority holds it. The floor ring
    /// and the column linger <see cref="Linger"/> s more.</item>
    /// <item>The repair follows the C17 scan front (<see cref="Arrive"/>): a wounded part's clean skin solidifies over its
    /// craters as the front passes (<see cref="Heal"/>), each crater cell filling with a hardlight voxel first
    /// (<see cref="Crater"/>); a missing (gibbed) part re-forms as a wireframe, then a hologram, then its real skin
    /// (<see cref="Wire"/>, <see cref="Fill"/>, <see cref="Solid"/>); a hologram band sweeps the body with the front
    /// (<see cref="Band"/>); the blood and ink round it wash away (<see cref="WashAt"/>). An unwounded body gets the lift, the
    /// ring and column and the band only.</item>
    /// <item>First person: the view rises gently (<see cref="ViewLift"/>, none on the still setting) inside a scanline
    /// vignette (<see cref="Vignette"/>).</item>
    /// <item>Tiers: <see cref="Full"/>, <see cref="Reduced"/> (reduced effects) and <see cref="Low"/> (the low graphics tier) set
    /// the voxel and streak budgets. Nothing here allocates: every per-frame function writes into the caller's arrays.</item>
    /// </list>
    /// No hit volume, damage, timing, invulnerability, network or rule reads any of this.
    /// </summary>
    public static class NativeReviveHolo
    {
        public const int Cube=0,Ring=1,Column=2;
        public const int Full=0,Reduced=1,Low=2;
        /// <summary>How high the body floats (m).</summary>
        public const float Height=.5f;
        /// <summary>It lifts from <see cref="RiseAt"/> over <see cref="RiseFor"/> s, sets down over the last <see cref="SetFor"/> s
        /// of the revive window, and the ring and column fade over <see cref="Linger"/> s after it lands.</summary>
        public const float RiseAt=.06f,RiseFor=.42f,SetFor=.45f,Linger=.32f;
        /// <summary>The body is back on the floor (the C17 revive window's end).</summary>
        public const float End=NativeReviveRules.Seconds;
        public const float SetAt=End-SetFor,Total=End+Linger;
        /// <summary>The share of the lift a first-person view takes (a gentle rise).</summary>
        public const float ViewShare=.35f;
        /// <summary>The blood and ink within <see cref="WashRadius"/> m of the body start fading at <see cref="WashAt"/> s, over
        /// <see cref="WashFor"/> s.</summary>
        public const float WashAt=.22f,WashFor=.65f,WashRadius=1.5f;
        /// <summary>The column's height and its radius round a standing pilot (m).</summary>
        public const float ColumnHeight=2.35f,StandRadius=.62f;
        /// <summary>Hardlight colours (linear): the scan's cyan and the settling white (NativeInteractionPresentation's).</summary>
        public static readonly Vector3 Cyan=new Vector3(.1144f,.8070f,1f),White=new Vector3(.6867f,.9647f,1f);

        public static float Clamp01(float x)=>x<0?0:x>1?1:x;
        public static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}

        // ---- the lift ----
        /// <summary>The body's height above where the authority holds it (m): 0 before <see cref="RiseAt"/>, <see cref="Height"/>
        /// while it is repaired and stands, exactly 0 again from <see cref="End"/>.</summary>
        public static float Lift(float age)
        {
            if(!(age>RiseAt)||age>=End)return 0;
            return Height*Smooth((age-RiseAt)/RiseFor)*(1-Smooth((age-SetAt)/SetFor));
        }
        /// <summary>The first-person view's rise (m): a share of the lift, none when the viewer keeps the camera still (reduced
        /// effects or camera shake off).</summary>
        public static float ViewLift(float age,bool still)=>still?0:ViewShare*Lift(age);
        /// <summary>The first-person scanline vignette's strength, 0..1.</summary>
        public static float Vignette(float age)=>!(age>=0)||age>=Total?0:Smooth(age/.15f)*(1-Smooth((age-(End-.2f))/(Linger+.2f)));
        /// <summary>The ring and column's strength, 0..1.</summary>
        public static float Aura(float age)=>!(age>=0)||age>=Total?0:Smooth(age/.14f)*(1-Smooth((age-End)/Linger));

        // ---- the repair, along the C17 scan ----
        /// <summary>When the C17 scan front reaches scan coordinate `c` (0 the feet end, 1 the head end): its own switch,
        /// Front(age) x 1.2 >= c.</summary>
        public static float Arrive(float c)=>Clamp01(c)*NativeReviveRules.Scan*.8f/1.2f;
        /// <summary>The scan coordinate the front is at (the band's place); past 1 once it has left the body.</summary>
        public static float Band(float age)=>age<0?-1:age/Arrive(1);
        /// <summary>A wounded part's clean skin over its craters, 0..1 (drawn dithered in, over the wounded part).</summary>
        public static float Heal(float age,float c)=>Smooth((age-Arrive(c)+.03f)/.2f);
        /// <summary>A missing part's wireframe, 0..1: just before the front, gone as its skin solidifies.</summary>
        public static float Wire(float age,float c){float t=age-Arrive(c);return Smooth((t+.12f)/.1f)*(1-Smooth((t-.12f)/.12f));}
        /// <summary>A missing part's hologram, 0..1.</summary>
        public static float Fill(float age,float c){float t=age-Arrive(c);return Smooth((t+.02f)/.1f)*(1-Smooth((t-.15f)/.11f));}
        /// <summary>A missing part's real skin, 0..1 (drawn dithered in).</summary>
        public static float Solid(float age,float c)=>Smooth((age-Arrive(c)-.08f)/.18f);
        /// <summary>The scan coordinate of `p` between the axis ends `a` and `b` (NativeInteractionPresentation's).</summary>
        public static float Along(Vector3 p,Vector3 a,Vector3 b){var d=b-a;float l=d.LengthSquared();return l>1e-6f?Clamp01(Vector3.Dot(p-a,d)/l):0;}

        // ---- the hardlight pieces ----
        public static int CraterBudget(int tier)=>tier==Full?160:tier==Reduced?64:24;
        public static int StreakCount(int tier)=>tier==Full?12:tier==Reduced?6:0;
        public const int MaxAura=6+12;
        /// <summary>
        /// The hardlight voxels refilling the wound craters, into `into` from `start` (returns the next free index). An even pick of
        /// the holes within the tier's budget; each grows in just before the front reaches it (the deeper ones first), glows,
        /// then sinks into the clean skin as it solidifies. `coordinates` holds each hole's scan coordinate, `draw` each joint's
        /// draw matrix (mesh to world) and `lift` the body's lift (added after).
        /// </summary>
        public static int Crater(IReadOnlyList<WoundHole> holes,float[] coordinates,Matrix4x4[] draw,float lift,float age,int tier,ReviveGlow[] into,int start)
        {
            if(holes==null||coordinates==null||draw==null||into==null||age<0||age>NativeReviveRules.Scan+.1f)return start;
            int count=holes.Count,n=Math.Min(count,CraterBudget(tier));
            for(int k=0;k<n&&start<into.Length;k++)
            {
                int i=(int)((long)k*count/n);var h=holes[i];if(h.Bone<0||h.Bone>=draw.Length||i>=coordinates.Length)continue;
                float t=age-Arrive(coordinates[i])+.07f*Clamp01(h.Depth/.04f);
                float grow=Smooth((t+.1f)/.08f),sink=Smooth((t-.1f)/.16f);float a=grow*(1-sink);if(a<=.004f)continue;
                float s=.35f+.75f*grow-.45f*sink;
                var box=new Matrix4x4(h.U.X*2*s,h.U.Y*2*s,h.U.Z*2*s,0,h.V.X*2*s,h.V.Y*2*s,h.V.Z*2*s,0,h.W.X*2*s,h.W.Y*2*s,h.W.Z*2*s,0,h.Center.X,h.Center.Y,h.Center.Z,1);
                var m=box*draw[h.Bone];m.M42+=lift;
                var c=Vector3.Lerp(Cyan,White,Smooth((t-.02f)/.1f));into[start++]=new ReviveGlow{Kind=Cube,Matrix=m,Color=new Vector4(c,.85f*a)};
            }
            return start;
        }
        /// <summary>
        /// The ring and column round a reviving body, into `into` from `start` (returns the next free index): a floor ring and
        /// an inner ring under the body, a hardlight disc under it at its lift, the column (none for a first-person viewer
        /// inside it), rising data streaks on the column, and a pulse ring as it lands. `floor` is the floor point under the
        /// body, `radius` the column's radius, `lift` the body's.
        /// </summary>
        public static int Halo(float age,uint seed,Vector3 floor,float radius,float lift,int tier,bool viewer,ReviveGlow[] into,int start)
        {
            float aura=Aura(age);if(aura<=.002f||into==null)return start;
            float pulse=.82f+.18f*(float)Math.Sin(age*15);
            void Put(int kind,Vector3 at,Vector3 scale,Vector3 color,float alpha){if(start<into.Length&&alpha>.002f)into[start++]=new ReviveGlow{Kind=kind,Matrix=Matrix4x4.CreateScale(scale)*Matrix4x4.CreateTranslation(at),Color=new Vector4(color,alpha)};}
            Put(Ring,floor+new Vector3(0,.012f,0),new Vector3(radius,1,radius),Cyan,.6f*aura*pulse);
            Put(Ring,floor+new Vector3(0,.014f,0),new Vector3(radius*.62f,1,radius*.62f),White,.45f*aura);
            if(lift>.01f)Put(Ring,floor+new Vector3(0,lift-.03f,0),new Vector3(radius*.78f,1,radius*.78f),Cyan,.5f*aura*Clamp01(lift/(Height*.5f)));
            if(!viewer)Put(Column,floor,new Vector3(radius,ColumnHeight,radius),Cyan,.32f*aura);
            // Landing: one ring out from the feet as the body sets down.
            float land=(age-(End-.06f))/.34f;if(land>0&&land<1)Put(Ring,floor+new Vector3(0,.016f,0),new Vector3(radius*(.5f+1.4f*land),1,radius*(.5f+1.4f*land)),White,.8f*(1-land)*(1-land));
            int streaks=viewer?0:StreakCount(tier);
            for(int k=0;k<streaks;k++)
            {
                double h=NativeWoundCarve.Hash(seed,k,3),h2=NativeWoundCarve.Hash(seed,k,5);
                float angle=(float)(Math.PI*2*(k+.6*h)/streaks),phase=(float)((age*(1.1+.7*h2)+h)%1.0);
                float y=phase*(ColumnHeight-.3f),fade=Smooth(phase/.15f)*(1-Smooth((phase-.75f)/.25f));
                var at=floor+new Vector3((float)Math.Cos(angle)*radius,y+.15f,(float)Math.Sin(angle)*radius);
                Put(Cube,at,new Vector3(.014f,.22f+.16f*(float)h2,.014f),k%3==0?White:Cyan,.75f*aura*fade);
            }
            return start;
        }

        // ---- the wireframe ----
        /// <summary>
        /// A mesh's feature edges, as index pairs into its own vertices (into `into`, cleared first): every edge between two
        /// triangles more than `crease` radians apart and every edge only one triangle has. Vertices are welded by position
        /// (a bank mesh repeats a vertex per face). Built once per part, when a revive first needs it.
        /// </summary>
        public static void Edges(WeaponSection m,List<int> into,float crease=.5f)
        {
            into.Clear();if(m==null||m.Indices.Count<3)return;
            var weld=new Dictionary<(long,long,long),int>();var id=new int[m.Positions.Count];
            for(int i=0;i<m.Positions.Count;i++){var p=m.Positions[i];var key=((long)Math.Round(p.X*20000),(long)Math.Round(p.Y*20000),(long)Math.Round(p.Z*20000));if(!weld.TryGetValue(key,out int w)){w=weld.Count;weld[key]=w;}id[i]=w;}
            var edges=new Dictionary<(int,int),(int a,int b,Vector3 n,int count,bool sharp)>();float cos=(float)Math.Cos(crease);
            for(int k=0;k+2<m.Indices.Count;k+=3)
            {
                int i0=m.Indices[k],i1=m.Indices[k+1],i2=m.Indices[k+2];var n=Vector3.Cross(m.Positions[i1]-m.Positions[i0],m.Positions[i2]-m.Positions[i0]);
                if(n.LengthSquared()<1e-16f)continue;n=Vector3.Normalize(n);
                for(int e=0;e<3;e++)
                {
                    int a=e==0?i0:e==1?i1:i2,b=e==0?i1:e==1?i2:i0;int wa=id[a],wb=id[b];if(wa==wb)continue;var key=wa<wb?(wa,wb):(wb,wa);
                    if(!edges.TryGetValue(key,out var v))edges[key]=(a,b,n,1,false);
                    else edges[key]=(v.a,v.b,v.n,v.count+1,v.sharp||Vector3.Dot(v.n,n)<cos);
                }
            }
            foreach(var v in edges.Values)if(v.count==1||v.sharp){into.Add(v.a);into.Add(v.b);}
        }
    }
}
