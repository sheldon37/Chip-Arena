using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The way to the boss room (the owner, 2026-11-07: "When clearing quota on instances as well; Can we make an animated guide to show where the
    /// boss room is?"). Pure rules, no UnityEngine; Runtime/NativeBossGuidePresentation.cs draws it.
    /// <list type="bullet">
    /// <item>Trigger (<see cref="Of"/>): the viewer's current instance has met its kill quota, its boss has arrived (the replicated boss arrival:
    /// where the combat authority spawned it) and isn't beaten, and the view isn't loading or frozen. The presentation also turns it off once
    /// the viewer is inside the boss's room (<see cref="Target.Near"/> of where it arrived).</item>
    /// <item>Route (<see cref="BossGuideRoute"/>): in the Backrooms and the Poolrooms the real way through the grid (each generator's own walk and
    /// doorways, from the viewer's cell to the reachable cell nearest the boss), its corners rounded; elsewhere none (the marker and the edge
    /// arrow show the way).</item>
    /// <item>Trail (<see cref="Draw"/>): chevrons on the floor every <see cref="Spacing"/> m along the route's first <see cref="Reach"/> m, fixed
    /// to the floor as the viewer walks, a light running along them toward the room every <see cref="Period"/> s (steady with Reduced effects),
    /// and a beacon on the floor where the route meets the room. In the boss's own colours (its relics' tier).</item>
    /// </list>
    /// Presentation only: it reads the replicated view and the generated grid every peer already holds; it decides nothing for the authority.
    /// </summary>
    public static class NativeBossGuide
    {
        public const float Spacing=1.7f,Reach=42,Period=2.6f,PulseWidth=3.2f,Hidden=1.3f,Lift=.035f;
        /// <summary>The guide this frame: on, where the boss arrived, its archetype, area and name, how near counts as in its room, its colours.</summary>
        public struct Target{public bool Active;public Vector3 At;public string Archetype,Area,Name;public float Near;public int Deep,Light;}
        public static Target Of(ExpeditionView v)
        {
            var t=default(Target);
            if(v==null||v.loading||v.migrating||v.phase!=ExpeditionPhase.Instance||v.instances==null||v.bossArrivals==null)return t;
            var b=v.CurrentBuilding();if(b==null||b.cleared||b.target<=0||b.progress<b.target)return t;
            if(!string.IsNullOrEmpty(v.area)&&!string.Equals(b.id,v.area,StringComparison.Ordinal))return t;
            ExpeditionBossArrivalView boss=null;
            foreach(var a in v.bossArrivals)if(a!=null&&string.Equals(a.area,b.id,StringComparison.Ordinal)&&a.hp>0&&(b.defeated==null||Array.IndexOf(b.defeated,a.id)<0)){boss=a;break;}
            if(boss==null||!Finite(boss.x)||!Finite(boss.y)||!Finite(boss.z))return t;
            t.Active=true;t.At=new Vector3(boss.x,boss.y,boss.z);t.Archetype=boss.archetype;t.Area=b.id;t.Near=Near(boss.archetype);Tone(boss.archetype,out t.Deep,out t.Light);t.Name=Name(boss.archetype);
            return t;
        }
        private static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
        /// <summary>Within this of where the boss arrived the viewer is in its room (THE HUM's room is 38.4 m across, THE DEEP END's 40; the others fight in the open).</summary>
        public static float Near(string archetype)=>archetype==NativeHumBoss.Archetype?NativeHumArena.Size/2-1:archetype==NativeDeepEndBoss.Archetype?19:9;
        /// <summary>The boss's colours: its relics' tier (deep, and the light label); THE 6:47's amber for anyone else.</summary>
        public static void Tone(string archetype,out int deep,out int light){foreach(var s in NativeBossChips.Sets)if(s.Boss==archetype){deep=s.Rarity;light=s.Label;return;}deep=0xffb02e;light=0xffd27a;}
        public static string Name(string archetype){foreach(var s in NativeBossChips.Sets)if(s.Boss==archetype)return s.Name;return "THE BOSS";}
        private static string tagName;private static int tagMetres=-1;private static string tagText="";
        /// <summary>The HUD line: "THE HUM · 42 m" (cached while it reads the same).</summary>
        public static string Tag(string name,float metres)
        {
            int m=float.IsNaN(metres)||metres<0?0:metres>999?999:(int)Math.Round(metres,MidpointRounding.AwayFromZero);
            if(m!=tagMetres||!string.Equals(name,tagName,StringComparison.Ordinal)){tagName=name;tagMetres=m;tagText=(name??"THE BOSS")+" · "+m.ToString(CultureInfo.InvariantCulture)+" m";}
            return tagText;
        }

        private static float H(long k,uint salt){unchecked{uint h=(uint)k*0x9e3779b1u^salt*0x85ebca6bu;h^=h>>15;h*=0x2c1b3c6du;h^=h>>12;h*=0x297a2d39u;h^=h>>15;return (h&0xffffff)/16777216f;}}
        private static float Smooth(float x){x=x<0?0:x>1?1:x;return x*x*(3-2*x);}
        /// <summary>The light running along the trail (0..1) at `s` m from the viewer and `time` s: a soft band moving toward the room; none with Reduced effects.</summary>
        public static float Pulse(float s,float time,bool reduced)
        {
            if(reduced)return 0;float span=Reach+PulseWidth*2,front=(float)(time%Period/Period)*span-PulseWidth,d=(s-front)/PulseWidth;return (float)Math.Exp(-d*d);
        }
        /// <summary>How bright the trail is `s` m from the viewer: hidden under their feet, faded in over 2 m, out over its last 8.</summary>
        public static float Fade(float s)=>Smooth((s-Hidden)/2)*(1-Smooth((s-(Reach-8))/8));
        /// <summary>
        /// The trail on the floor at `time` with the guide's fade `level`, its colours (hex), effects quality `q` (Reduced .3) and the low tier: a chevron
        /// every <see cref="Spacing"/> m (half as many on the low tier), counted from the route's end so they stay put as the viewer walks, the running
        /// light, and the beacon where the route meets the room.
        /// </summary>
        public static void Draw(IKitFxSink s,BossGuideRoute r,float time,float level,int deepHex,int lightHex,float q,bool low)
        {
            if(s==null||r==null||!r.Valid||!(level>.01f))return;bool reduced=q<.5f;var deep=NativeMeleeKitFx.Lin(deepHex,1);var light=NativeMeleeKitFx.Lin(lightHex,1);
            float spacing=low?Spacing*1.5f:Spacing,total=r.Length;int k0=Math.Max(0,(int)Math.Ceiling((total-(Reach+1))/spacing));
            for(int k=k0;;k++)
            {
                float at=total-k*spacing;if(at<Hidden)break;float fade=Fade(at);if(fade<=.002f)continue;
                r.Sample(at,out var p,out var tan);float pulse=Pulse(at,time,reduced),bright=level*fade*(.3f+.85f*pulse);if(bright<=.004f)continue;
                var side=new Vector3(tan.Z,0,-tan.X);var tip=p+tan*.32f+Vector3.UnitY*Lift;var color=Vector3.Lerp(deep,light,pulse)*1.7f;
                Arm(s,tip,tip-tan*.44f+side*.38f,bright,color);Arm(s,tip,tip-tan*.44f-side*.38f,bright,color);
                if(!low&&pulse>.25f)s.Glow(p+Vector3.UnitY*.08f,light*.9f*pulse*level*fade,0,.85f,NativeMeleeKitFx.Core,pulse,k);
            }
            // The beacon where the route meets the room: a slow ring of light on the floor and motes rising through it.
            var end=r.Points[r.Points.Count-1];if(total>Reach+2)return;float beat=reduced?1:.75f+.25f*(float)Math.Sin(time*2.4f),lv=level*(1-Smooth((total-(Reach-6))/8));
            NativeMeleeKitFx.GroundRing(s,end,1.05f+(reduced?0:.12f*(float)Math.Sin(time*2.4f)),.16f,light*1.8f,.85f*beat*lv,low?18:30);
            NativeMeleeKitFx.GroundRing(s,end,.55f,.55f,deep*1.1f,.35f*beat*lv,low?12:20);
            int motes=low?4:reduced?5:9;for(int i=0;i<motes;i++){float h=H(i,7),u=(float)((time*.45f+h)%1.0),a=h*6.283f,rr=.3f+.6f*H(i,8);
                s.Glow(end+new Vector3((float)Math.Cos(a)*rr,.1f+2.1f*u,(float)Math.Sin(a)*rr),light*1.6f*(1-u)*lv,0,.16f,NativeMeleeKitFx.Mote,u,i);}
        }
        private static void Arm(IKitFxSink s,Vector3 tip,Vector3 end,float bright,Vector3 color)
        {
            var u=end-tip;var w=Vector3.Normalize(new Vector3(-u.Z,0,u.X))*.055f;s.Band(false,tip+w,end+w,end-w,tip-w,bright,bright*.55f,color);
        }
    }

    /// <summary>
    /// The route to the boss through a generated grid (<see cref="NativeBossGuide"/>): rebuilt when the viewer changes cell or the goal moves, from
    /// the generator's own walk (Backrooms: <see cref="NativeBackroomsGenerator.Distances"/> and its doorways; Poolrooms: its decks and walls), to the
    /// reachable cell nearest the goal, straight runs kept straight, corners rounded; the first point follows the viewer every frame.
    /// </summary>
    public sealed class BossGuideRoute
    {
        public readonly List<Vector3> Points=new List<Vector3>();public float Length;public bool Valid=>Points.Count>=2&&Length>.5f;
        private readonly List<float> cum=new List<float>();private readonly List<Vector3> body=new List<Vector3>(),cells=new List<Vector3>();
        private object grid;private int from=-1;private Vector3 goal=new Vector3(float.NaN);private int[] dist;
        private static readonly (int x,int z)[] Directions={(1,0),(0,1),(-1,0),(0,-1)};
        public void Clear(){Points.Clear();cum.Clear();body.Clear();cells.Clear();Length=0;grid=null;from=-1;dist=null;goal=new Vector3(float.NaN);}
        /// <summary>The Backrooms route from `viewer` (feet) to `target`; false when there is none.</summary>
        public bool Update(BackroomsLayout l,Vector3 viewer,Vector3 target)
        {
            if(l==null){Clear();return false;}
            int x=Math.Max(0,Math.Min(l.Width-1,(int)Math.Floor(viewer.X/BackroomsLayout.Cell))),z=Math.Max(0,Math.Min(l.Depth-1,(int)Math.Floor(viewer.Z/BackroomsLayout.Cell)));int cell=l.Index(x,z);
            if(!ReferenceEquals(grid,l)||cell!=from||!(Vector3.DistanceSquared(target,goal)<.25f))
            {
                grid=l;from=cell;goal=target;dist=NativeBackroomsGenerator.Distances(l,cell);
                Walk(l.Width,l.Depth,BackroomsLayout.Cell,c=>{var p=NativeBackroomsGenerator.Position(l,c);return new Vector3(p.x,p.y,p.z);},(n,dx,dz)=>NativeBackroomsGenerator.Link(l,n,dx,dz));
            }
            return Follow(viewer);
        }
        /// <summary>The Poolrooms route (over its decks, the generator's own walk) from `viewer` (feet) to `target`; false when there is none.</summary>
        public bool Update(PoolroomsLayout l,Vector3 viewer,Vector3 target)
        {
            if(l==null){Clear();return false;}
            int x=Math.Max(0,Math.Min(l.Width-1,(int)Math.Floor(viewer.X/PoolroomsLayout.Cell))),z=Math.Max(0,Math.Min(l.Depth-1,(int)Math.Floor(viewer.Z/PoolroomsLayout.Cell)));int cell=l.Index(x,z);
            if(!ReferenceEquals(grid,l)||cell!=from||!(Vector3.DistanceSquared(target,goal)<.25f))
            {
                grid=l;from=cell;goal=target;dist=NativePoolroomsGenerator.Distances(l,cell);
                Walk(l.Width,l.Depth,PoolroomsLayout.Cell,c=>{var p=NativePoolroomsGenerator.Position(l,c);return new Vector3(p.x,p.y,p.z);},(n,dx,dz)=>l.Passable(n%l.Width,n/l.Width,dx,dz));
            }
            return Follow(viewer);
        }
        /// <summary>From the reachable cell nearest the goal back down the walk to the viewer's cell (each step through a link the walk could take), then rounded.</summary>
        private void Walk(int width,int depth,float cell,Func<int,Vector3> centre,Func<int,int,int,bool> link)
        {
            cells.Clear();body.Clear();if(dist==null)return;int end=-1;float best=float.MaxValue;
            for(int c=0;c<dist.Length;c++){if(dist[c]<0)continue;var p=centre(c);float d=(p.X-goal.X)*(p.X-goal.X)+(p.Z-goal.Z)*(p.Z-goal.Z);if(d<best-1e-4f||end>=0&&Math.Abs(d-best)<=1e-4f&&dist[c]<dist[end]){best=d;end=c;}}
            if(end<0||dist[end]==0)return;
            int cur=end,last=-1,guard=0;cells.Add(centre(cur));
            while(dist[cur]>0&&guard++<dist.Length)
            {
                int x=cur%width,z=cur/width,next=-1,used=-1;
                for(int j=0;j<4;j++)
                {
                    int k=last>=0?(last+j)%4:j;var d=Directions[k];int nx=x+d.x,nz=z+d.z;if(nx<0||nz<0||nx>=width||nz>=depth)continue;int n=nx+nz*width;
                    if(dist[n]!=dist[cur]-1||!link(n,-d.x,-d.z))continue;next=n;used=k;break;
                }
                if(next<0)break;cur=next;last=used;cells.Add(centre(cur));
            }
            cells.Reverse();
            // Straight runs as two points, every corner rounded inside its own cell (a curve of radius under half a cell, so it never cuts across a wall):
            // the trail bends through doorways instead of turning on the spot.
            var simple=new List<Vector3>();for(int i=0;i<cells.Count;i++){if(i>0&&i<cells.Count-1){var a=cells[i]-cells[i-1];var b=cells[i+1]-cells[i];if(Math.Abs(a.X*b.Z-a.Z*b.X)<1e-3f&&Math.Abs(a.Y-b.Y)<.05f)continue;}simple.Add(cells[i]);}
            float radius=cell*.42f;body.Add(simple[0]);
            for(int i=1;i<simple.Count-1;i++)
            {
                var b=simple[i];var u=b-simple[i-1];var w=simple[i+1]-b;float lu=u.Length(),lw=w.Length();if(lu<1e-4f||lw<1e-4f){body.Add(b);continue;}u/=lu;w/=lw;float r=Math.Min(radius,Math.Min(lu,lw)*.5f);
                var p0=b-u*r;var p2=b+w*r;for(int k=0;k<=4;k++){float t=k/4f,m=1-t;body.Add(p0*(m*m)+b*(2*m*t)+p2*(t*t));}
            }
            if(simple.Count>1)body.Add(simple[simple.Count-1]);
        }
        /// <summary>The route this frame: the viewer's feet, then the walk past their own cell's centre.</summary>
        private bool Follow(Vector3 viewer)
        {
            Points.Clear();cum.Clear();Length=0;if(body.Count==0)return false;
            // The viewer joins the route's first run where they stand beside it, so everything ahead lies on the cells' own line (fixed to the floor however they stand in their cell).
            var start=new Vector3(viewer.X,body[0].Y,viewer.Z);Points.Add(start);
            if(body.Count>1){var a=body[0];var ab=body[1]-a;float l2=ab.LengthSquared(),t=l2>1e-6f?Math.Max(0,Math.Min(1,Vector3.Dot(start-a,ab)/l2)):0;var join=a+ab*t;if(Vector3.Distance(start,join)>.02f)Points.Add(join);}
            for(int i=1;i<body.Count;i++)Points.Add(body[i]);if(body.Count==1)Points.Add(body[0]);
            cum.Add(0);for(int i=1;i<Points.Count;i++){Length+=Vector3.Distance(Points[i-1],Points[i]);cum.Add(Length);}
            return Valid;
        }
        /// <summary>The place and flat direction along the route at `s` m from its start.</summary>
        public void Sample(float s,out Vector3 at,out Vector3 tangent)
        {
            at=Points.Count>0?Points[0]:Vector3.Zero;tangent=Vector3.UnitZ;if(Points.Count<2)return;
            if(s<=0)s=0;if(s>=Length)s=Length;int lo=0,hi=cum.Count-1;while(hi-lo>1){int mid=(lo+hi)/2;if(cum[mid]<=s)lo=mid;else hi=mid;}
            float seg=cum[hi]-cum[lo],u=seg>1e-5f?(s-cum[lo])/seg:0;at=Vector3.Lerp(Points[lo],Points[hi],u);var d=Points[hi]-Points[lo];d.Y=0;tangent=d.LengthSquared()>1e-8f?Vector3.Normalize(d):Vector3.UnitZ;
        }
    }
}
