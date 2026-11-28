using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// One vertex of a ROUNDS field surface (Resources/NativeRoundsField.shader and NativeRoundsWarp.shader read it as is):
    /// position, the look's colours in the normal slot (hot, cool and dark as sRGB hex, exact in a float: the normal itself is up on
    /// the ground and radial on the sphere, which the shaders rebuild from A) and three float4s, TEXCOORD0..2:
    /// A (local x, local y, local z from the field's centre in m, seed), B (style, variant, age s, life s),
    /// C (layer: 0 ground, 1 dome, 2 warp; the drawn ground radius m, or on the sphere the floor's offset from the centre; the
    /// authority radius m; strength 0..1).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RoundsFieldVertex
    {
        public float Px,Py,Pz,Nx,Ny,Nz,Ax,Ay,Az,Aw,Bx,By,Bz,Bw,Cx,Cy,Cz,Cw;
    }

    /// <summary>A dynamic field surface mesh for one frame (fixed buffers; a field that would pass them is refused whole).</summary>
    public sealed class RoundsSurface
    {
        public readonly RoundsFieldVertex[] V;public readonly int[] T;public int VertexCount,IndexCount,Refused;
        public RoundsSurface(int vertices,int indices){V=new RoundsFieldVertex[vertices];T=new int[indices];}
        public void Clear(){VertexCount=IndexCount=Refused=0;}
        public bool Room(int v,int i){if(VertexCount+v>V.Length||IndexCount+i>T.Length){Refused++;return false;}return true;}
    }

    /// <summary>A field the presenter sees this frame: its replicated id, look (NativeRoundsLooks.Looks), centre, the floor under it,
    /// the authority radius, birth and expiry (authority seconds).</summary>
    public struct RoundsFieldInput{public int Id,Look;public Vector3 Centre;public float Floor,Radius,Born,Until;}

    /// <summary>What a field drew this frame (checks): its ground radius (the outermost ring), the dome's radius, its age, life and
    /// whether the hazard look (full strength, boundary drawn) or its dissipation was drawn.</summary>
    public struct RoundsFieldShape{public int Id,Look;public Vector3 Centre;public float Floor,Radius,Ground,GroundDrawn,DomeDrawn,Age,Life,Strength;public bool Hazard,Dissipating;public int Particles,Pieces;}

    /// <summary>
    /// The ROUNDS pack fields' bespoke look (owner 2026-10-19: the fire, frost and slow-time fields had no visuals of their own;
    /// then the batch-2 kinds): Ember Footprint and Thornwake (fire), Cryo Snare and Binding Lattice (frost), Suppression Cloud,
    /// Temporal Sacrifice and the three block interdicts' time fields (slow time), Repulsor Well and Emergency Vent (repulsion),
    /// Attractor Well (gravity), Storm Relay (storm).
    /// <list type="bullet">
    /// <item>The hazard read: the authority applies a field to a pilot whose body centre is within its radius R of the field's
    /// centre (NativeChips.StepChipFields; frost and time slow there too: ChipFieldSpeed). So the ground surface is the disc where a
    /// standing pilot (body centre <see cref="PilotCentre"/> above the floor) is inside, radius sqrt(R^2 - dy^2), its edge drawn
    /// exactly there; that is R for a field born at a pilot's centre (every dodge and block field, a hit on a standing pilot).
    /// The time and gravity fields also act on shots anywhere in the sphere, so they draw that sphere too (clipped at the floor).
    /// The hazard look is drawn from the field's birth to its expiry exactly, then a short dissipation (embers, a shattering,
    /// a collapse) that carries no boundary.</item>
    /// <item>Every particle and surface is a pure function of the field's replicated id (its seed), birth, expiry and the
    /// presentation clock, so host, guests, the killcam and the DEV preview draw the same frame. A field is remembered after it
    /// leaves the replicated list (a guest renders ~0.1 s behind, the killcam rewinds) so its hazard look still ends at its expiry
    /// and its dissipation plays. Counts and mesh resolution scale with the effects quality and the low tier.</item>
    /// </list>
    /// Allocation free.
    /// </summary>
    public sealed class NativeRoundsFields
    {
        /// <summary>A standing pilot's body centre above its feet (NativeArena's snapshot: Position = body - .865 up).</summary>
        public const float PilotCentre=.865f;
        /// <summary>The surface lift over the floor (no z-fighting).</summary>
        public const float Lift=.02f;
        public const int Capacity=48;
        private struct Mem{public bool Used;public RoundsFieldInput F;public float Seen;}
        private readonly Mem[] mem=new Mem[Capacity];
        public readonly RoundsFieldShape[] Shapes=new RoundsFieldShape[Capacity];public int ShapeCount;
        /// <summary>Seconds a style's dissipation lasts after the field's expiry.</summary>
        public static float Fade(RoundsStyle s)=>s==RoundsStyle.Fire?1.4f:s==RoundsStyle.Frost?.7f:s==RoundsStyle.Pull?.55f:.5f;
        /// <summary>The ground radius a standing pilot is inside (see the class summary).</summary>
        public static float GroundRadius(float radius,float centreY,float floor){float dy=centreY-(floor+PilotCentre);float g=radius*radius-dy*dy;return g>0?(float)Math.Sqrt(g):0;}
        public void Clear(){for(int i=0;i<mem.Length;i++)mem[i].Used=false;ShapeCount=0;}
        /// <summary>No field remembered.</summary>
        public bool Idle{get{for(int i=0;i<mem.Length;i++)if(mem[i].Used)return false;return true;}}
        /// <summary>Forget fields newer than a rewound clock (the killcam's start).</summary>
        public void Rewind(float clock){for(int i=0;i<mem.Length;i++)if(mem[i].Used&&mem[i].F.Born>clock+.05f)mem[i].Used=false;}
        /// <summary>A field the presenter sees this frame (replicated or the DEV preview's).</summary>
        public void Observe(in RoundsFieldInput f,float clock)
        {
            if(f.Look<0||f.Look>=NativeRoundsLooks.Looks.Length||!(f.Radius>0)||!(f.Until>f.Born))return;
            int free=-1;
            for(int i=0;i<mem.Length;i++){if(mem[i].Used&&mem[i].F.Id==f.Id){mem[i].F=f;mem[i].Seen=clock;return;}if(!mem[i].Used&&free<0)free=i;}
            if(free<0){float worst=float.MaxValue;for(int i=0;i<mem.Length;i++)if(mem[i].F.Until<worst){worst=mem[i].F.Until;free=i;}}
            mem[free]=new Mem{Used=true,F=f,Seen=clock};
        }
        /// <summary>Draws every remembered field at `clock` into the surfaces, particles and lit pieces; forgets finished ones.</summary>
        public void Draw(float clock,NativeBossVfx fx,RoundsSurface ground,RoundsSurface warp,RoundsLit lit,double quality,bool low,in RoundsShots shots)
        {
            ShapeCount=0;
            for(int i=0;i<mem.Length;i++)
            {
                if(!mem[i].Used)continue;var f=mem[i].F;var look=NativeRoundsLooks.Looks[f.Look];float age=clock-f.Born,life=f.Until-f.Born;
                if(age>life+Fade(look.Style)+.05f){mem[i].Used=false;continue;}
                if(age<-.05f)continue;
                int p0=fx.Count,l0=lit.Pieces;var s=new RoundsFieldShape{Id=f.Id,Look=f.Look,Centre=f.Centre,Floor=f.Floor,Radius=f.Radius,Ground=GroundRadius(f.Radius,f.Centre.Y,f.Floor),Age=age,Life=life};
                s.Hazard=age>=0&&age<=life;s.Dissipating=age>life;s.Strength=Strength(look.Style,age,life);
                Field(f,look,age,life,clock,fx,ground,warp,lit,quality,low,ref s,shots);
                s.Particles=fx.Count-p0;s.Pieces=lit.Pieces-l0;if(ShapeCount<Shapes.Length)Shapes[ShapeCount++]=s;
            }
        }
        /// <summary>A field's overall strength at `age`: 1 through its life (a brief settle in its first .12 s), then down to 0 over
        /// its dissipation.</summary>
        public static float Strength(RoundsStyle st,float age,float life)
        {
            if(age<0)return 0;if(age<=life)return .82f+.18f*BossMath.Smooth(age/.12f);
            return 1-BossMath.Smooth((age-life)/Fade(st));
        }
        private static uint Seed(int id,int look)=>(uint)id*2654435761u^(uint)(look*40503+17);
        private static float H(uint s,int i)=>BossMath.Hash(s,i);

        // ============================================================================================================
        // surfaces
        // ============================================================================================================
        private static void Put(RoundsSurface m,Vector3 p,Vector3 n,Vector3 local,float seed,in RoundsFieldLook look,float age,float life,float layer,float g,float R,float strength)
        {
            m.V[m.VertexCount++]=new RoundsFieldVertex{Px=p.X,Py=p.Y,Pz=p.Z,Nx=look.HotHex,Ny=look.CoolHex,Nz=look.DarkHex,Ax=local.X,Ay=local.Y,Az=local.Z,Aw=seed,Bx=(float)look.Style,By=look.Variant,Bz=age,Bw=life,Cx=layer,Cy=g,Cz=R,Cw=strength};
        }
        /// <summary>The ground disc: rings out to exactly `g` (its outermost ring is the boundary), `seg` segments.</summary>
        public static void Disc(RoundsSurface m,Vector3 centre,float floor,float g,int rings,int seg,float seed,in RoundsFieldLook look,float age,float life,float R,float strength)
        {
            if(!(g>.05f))return;int nv=1+rings*seg,ni=seg*3+(rings-1)*seg*6;if(!m.Room(nv,ni))return;
            int first=m.VertexCount;float y=floor+Lift;var up=Vector3.UnitY;
            Put(m,new Vector3(centre.X,y,centre.Z),up,Vector3.Zero,seed,look,age,life,0,g,R,strength);
            for(int r=1;r<=rings;r++)
            {
                // Rings packed toward the edge (where the rim's detail is), the last exactly on g.
                float u=(float)r/rings,rad=g*(1-(1-u)*(1-u)*.35f-(1-u)*.65f);if(r==rings)rad=g;
                for(int k=0;k<seg;k++){float a=k*6.2831853f/seg;var l=new Vector3((float)Math.Cos(a)*rad,0,(float)Math.Sin(a)*rad);Put(m,new Vector3(centre.X+l.X,y,centre.Z+l.Z),up,new Vector3(l.X,y-centre.Y,l.Z),seed,look,age,life,0,g,R,strength);}
            }
            var t=m.T;int ix=m.IndexCount;
            for(int k=0;k<seg;k++){t[ix++]=first;t[ix++]=first+1+(k+1)%seg;t[ix++]=first+1+k;}
            for(int r=1;r<rings;r++)for(int k=0;k<seg;k++)
            {
                int a=first+1+(r-1)*seg+k,b=first+1+(r-1)*seg+(k+1)%seg,c=first+1+r*seg+k,d=first+1+r*seg+(k+1)%seg;
                t[ix++]=a;t[ix++]=b;t[ix++]=c;t[ix++]=b;t[ix++]=d;t[ix++]=c;
            }
            m.IndexCount=ix;
        }
        /// <summary>The sphere of radius R about the centre, only the part above the floor (rows wholly below it are skipped; the
        /// shaders clip the rest at the floor). layer 1 the dome, 2 the warp.</summary>
        public static void Sphere(RoundsSurface m,Vector3 centre,float floor,float R,int lat,int lon,float seed,in RoundsFieldLook look,float age,float life,float strength,float layer)
        {
            if(!(R>.05f))return;
            // The first latitude row whose lower edge is still above the floor.
            int last=lat;for(int i=0;i<=lat;i++){float y=centre.Y+R*(float)Math.Cos(Math.PI*i/lat);if(y<floor){last=Math.Min(lat,i);break;}}
            int rows=last;if(rows<1)return;int nv=(rows+1)*(lon+1),ni=rows*lon*6;if(!m.Room(nv,ni))return;int first=m.VertexCount;
            for(int i=0;i<=rows;i++)for(int j=0;j<=lon;j++)
            {
                float th=(float)Math.PI*i/lat,ph=6.2831853f*j/lon;var d=new Vector3((float)(Math.Sin(th)*Math.Cos(ph)),(float)Math.Cos(th),(float)(Math.Sin(th)*Math.Sin(ph)));
                // C.y on the sphere: the floor's offset from the centre (the shaders clip below it).
                Put(m,centre+d*R,d,d*R,seed,look,age,life,layer,floor-centre.Y,R,strength);
            }
            var t=m.T;int ix=m.IndexCount;
            for(int i=0;i<rows;i++)for(int j=0;j<lon;j++){int a=first+i*(lon+1)+j,b=a+lon+1;t[ix++]=a;t[ix++]=b;t[ix++]=a+1;t[ix++]=b;t[ix++]=b+1;t[ix++]=a+1;}
            m.IndexCount=ix;
        }

        // ============================================================================================================
        // one field
        // ============================================================================================================
        private void Field(in RoundsFieldInput f,in RoundsFieldLook look,float age,float life,float clock,NativeBossVfx fx,RoundsSurface ground,RoundsSurface warp,RoundsLit lit,double quality,bool low,ref RoundsFieldShape s,in RoundsShots shots)
        {
            uint seed=Seed(f.Id,f.Look);float sd=(seed&0xffff)/65536f*97;float g=s.Ground,R=f.Radius,st=s.Strength;float q=(float)Math.Max(.3,Math.Min(1,quality));
            int rings=low?8:q<.6f?12:18,seg=low?40:q<.6f?56:80;
            // The ground's boundary is drawn while the field lives; the dissipation keeps the surface (embers, ice, the last rings)
            // but the shader drops the rim once age passes life.
            if(g>.05f){Disc(ground,f.Centre,f.Floor,g,rings,seg,sd,look,age,life,R,st);s.GroundDrawn=g;}
            bool sphere=look.Style==RoundsStyle.Time||look.Style==RoundsStyle.Pull||g<R*.55f;
            if(sphere){Sphere(ground,f.Centre,f.Floor,R,low?12:18,low?32:56,sd,look,age,life,st,1);s.DomeDrawn=R;}
            if(!low&&(look.Style==RoundsStyle.Time||look.Style==RoundsStyle.Pull||look.Style==RoundsStyle.Push))Sphere(warp,f.Centre,f.Floor,R,14,40,sd,look,age,life,st,2);
            var c=new Vector3(f.Centre.X,f.Floor,f.Centre.Z);
            switch(look.Style)
            {
                case RoundsStyle.Fire:Fire(c,g,age,life,seed,look,fx,lit);break;
                case RoundsStyle.Frost:Frost(c,g,age,life,seed,look,fx,lit);break;
                case RoundsStyle.Time:Time(f,g,age,life,clock,seed,look,fx,shots);break;
                case RoundsStyle.Push:Push(c,g,age,life,seed,look,fx);break;
                case RoundsStyle.Pull:Pull(f,g,age,life,seed,look,fx,lit,shots);break;
                case RoundsStyle.Storm:Storm(c,g,age,life,clock,seed,look,fx,lit);break;
            }
        }
        // A point on the ground disc of radius `g` round `c`, uniform in area, from two hashes (`edge` pushes it to the rim).
        private static Vector3 Spot(Vector3 c,float g,float h0,float h1,float edge=0)
        {float a=h0*6.2831853f,r=g*(float)Math.Sqrt(edge>0?1-(1-h1)*edge:h1);return new Vector3(c.X+(float)Math.Cos(a)*r,c.Y,c.Z+(float)Math.Sin(a)*r);}
        // A continuous emitter's live slots: slot i is born at born + (i + jitter) / rate, lives `span`; calls back nothing, returns
        // the first and last slot (the caller loops), so every particle's birth is fixed by its slot.
        private static void Slots(float age,float rate,float span,float end,out int first,out int last)
        {last=(int)Math.Floor(Math.Min(age,end)*rate);first=Math.Max(0,(int)Math.Floor((age-span)*rate));if(last-first>600)first=last-600;}

        // ---- fire: Ember Footprint, Thornwake ----
        // A burning source's flame: licks of light (tapering luminous streaks rising off the root and thinning to the tip, the
        // flame's bright body) wrapped in soft fire puffs (its volume) that leave the root, rise faster as they climb (buoyancy),
        // drift in toward the tongue's axis and narrow, sway on a slow turbulence, yellow at the root, orange, a dull red at the tip;
        // with a flicker of its own. Every lick and puff is a pure function of its slot (born at k / rate).
        private static void Flame(NativeBossVfx fx,Vector3 at,float height,float width,float rate,float start,float end,float age,uint seed,int key,in RoundsFieldLook look,float gain)
        {
            float r=rate*fx.RateScale;if(r<=0)return;const float span=.95f;
            float flick=.72f+.28f*(float)Math.Sin(age*(9+3*H(seed,key))+key*1.7f)*(float)Math.Sin(age*(5.3f+2*H(seed,key+1))+key);
            int last=(int)Math.Floor((Math.Min(age,end)-start)*r),first=Math.Max(0,(int)Math.Floor((age-span-start)*r));
            for(int k=first;k<=last;k++)
            {
                int q=key*911+k*5;float born=start+(k+H(seed,q)*.85f)/r,a=age-born,lf=(.45f+.35f*H(seed,q+1))*(.75f+.25f*height);if(a<0||a>lf||born>end)continue;
                float u=a/lf,h=height*flick*(.8f+.4f*H(seed,q+2));
                float rise=h*(u*.55f+u*u*.65f);float ph=H(seed,q+3)*6.28f;
                float j=(1-u*.8f)*width*.45f;var off=new Vector3((float)Math.Cos(ph),0,(float)Math.Sin(ph))*(j*(H(seed,q+4)-.2f));
                var sway=new Vector3((float)Math.Sin(age*2.3f+key*.7f+u*2.4f),0,(float)Math.Cos(age*1.9f+key*.4f+u*2.1f))*(.09f*u*h);
                var p=at+off+sway+new Vector3(0,.05f+rise,0);float size=width*(1.35f-.85f*u)*(.8f+.4f*H(seed,q+5));
                var tint=Vector3.Lerp(look.Core*2.0f,look.Hot*2.0f,BossMath.Smooth(u*3.5f));tint=Vector3.Lerp(tint,look.Cool*1.5f,BossMath.Smooth((u-.3f)*1.8f));
                float op=gain*.68f*(float)Math.Pow(1-u,1.2);fx.Fire(p,tint*gain,op,size,u*.26f,q%8191,at.Y);
            }
            // Licks: two or three tall streaks at a time, each rising off the root for .3-.5 s as it stretches and thins.
            float lr=4.5f*fx.RateScale;int l1=(int)Math.Floor((Math.Min(age,end)-start)*lr),l0=Math.Max(0,(int)Math.Floor((age-.55f-start)*lr));
            for(int k=l0;k<=l1;k++)
            {
                int q=key*577+k*3+90000;float born=start+(k+H(seed,q)*.8f)/lr,a=age-born,lf=.3f+.22f*H(seed,q+1);if(a<0||a>lf||born>end)continue;
                float u=a/lf,h=height*flick*(.75f+.5f*H(seed,q+2)),len=h*(.22f+.38f*(float)Math.Sin(Math.PI*Math.Min(1,u*1.2f)));float ph=H(seed,q+3)*6.28f;
                var b=at+new Vector3((float)Math.Cos(ph)*width*.22f+(float)Math.Sin(age*3+k)*.05f,.04f+h*.18f*u,(float)Math.Sin(ph)*width*.22f);
                var col=Vector3.Lerp(look.Hot*1.05f,look.Cool*.8f,u)*(gain*(1-u*u));fx.Spark(b,new Vector3(0,-len,0),col,width*(.62f-.25f*u),len,1,.95f,u,q);
            }
            // The root's glow: the bright base of the tongue lighting the floor.
            fx.Glow(at+new Vector3(0,.12f,0),look.Hot*(.8f*flick*gain),width*1.7f,NativeBossVfx.Core);
        }
        private void Fire(Vector3 c,float g,float age,float life,uint seed,in RoundsFieldLook look,NativeBossVfx fx,RoundsLit lit)
        {
            bool thorn=look.Variant==1;float area=(float)Math.PI*g*g,perim=6.2831853f*g;
            // Burning sources: a ring of tall tongues on the boundary (the fire's leading edge burns hottest), spaced a little
            // irregularly, and shorter fires scattered over the bed; all burn from the birth to the expiry (the hazard's life).
            int rim=Math.Max(10,(int)(perim/.42f)),bed=Math.Max(3,(int)(area/1.25f));float cut=life+.1f;
            // The fire collapses at the expiry: every live tongue dims and shrinks out over .15 s either side of it.
            float burn=age<=life-.15f?1:Math.Max(0,1-(age-(life-.15f))/.3f);
            for(int i=0;i<rim;i++)
            {
                float a=(i+(H(seed,100+i)-.5f)*.7f)*6.2831853f/rim,r=g*(.935f+.06f*H(seed,140+i));var at=new Vector3(c.X+(float)Math.Cos(a)*r,c.Y,c.Z+(float)Math.Sin(a)*r);
                float tall=.6f+.85f*H(seed,180+i)*H(seed,181+i);if(burn>0)Flame(fx,at,tall,.42f+.12f*H(seed,220+i),11,0,cut,age,seed,i,look,burn);
            }
            for(int i=0;i<bed;i++)
            {
                var at=Spot(c,g*.84f,H(seed,300+i),H(seed,340+i));if(burn>0)Flame(fx,at,.35f+.5f*H(seed,380+i),.32f+.12f*H(seed,420+i),7,0,cut,age,seed,500+i,look,.85f*burn);
            }
            // Embers: sparks rising on the plume with a curling drift, cooling; they outlive the flames a little.
            float er=(perim*2.6f+area*1.1f)*fx.RateScale;int first,last;Slots(age,er,1.8f,life+.8f,out first,out last);
            for(int i=first;i<=last;i++)
            {
                int k=50000+i*5;float born=(i+H(seed,k))/er,a=age-born,lf=1.0f+.8f*H(seed,k+1);if(a<0||a>lf||born>life+.8f)continue;
                var at=Spot(c,g*.95f,H(seed,k+2),H(seed,k+3),.35f);float vy=1.4f+1.8f*H(seed,k+4),ph=H(seed,k+5)*6.28f;
                var p=new Vector3(at.X+(float)Math.Sin(ph+a*3.1f)*.3f*a,c.Y+.35f+vy*a-.3f*a*a,at.Z+(float)Math.Cos(ph+a*2.7f)*.3f*a);var v=new Vector3((float)Math.Cos(ph+a*3.1f)*.9f,vy-.6f*a,-(float)Math.Sin(ph+a*2.7f)*.8f);
                float u=a/lf;fx.Spark(p,v,NativeBossVfx.SparkHeat(.1f+u*.9f,.9f),.016f,.03f,.03f,.85f,u,i);
            }
            // Soot: heavy smoke rolling up off the tips, lit orange from below, thickest over the wall of fire on the boundary.
            fx.SmokeFlow(age,new Vector3(c.X,c.Y+1.5f,c.Z),new Vector3(0,.5f,0),g*.6f,1.0f,4+g*2.2f,2.6f,.6f,1.7f+g*.2f,new Vector3(.05f,.047f,.045f),.42f,.15f,life+.1f,seed,3,glow:.18f,heat:new Vector3(.16f,.07f,.03f));
            // Ignition: a whoosh round the boundary and a flash at the centre.
            if(age<.35f)
            {
                int n=fx.Scale(30);for(int i=0;i<n;i++){float a=i*6.2831853f/n+H(seed,700+i)*.2f,u=age/.35f;var p=new Vector3(c.X+(float)Math.Cos(a)*g*.9f,c.Y+.15f+u*.8f,c.Z+(float)Math.Sin(a)*g*.9f);fx.Fire(p,look.Core*3.2f,.8f*(1-u),.6f+.5f*u,u*.4f,700+i,c.Y);}
                fx.Glow(new Vector3(c.X,c.Y+.5f,c.Z),look.Core*(3.5f*(1-age/.35f)),g*1.2f,NativeBossVfx.Core);
            }
            // After the expiry: the flames are out; smouldering wisps.
            if(age>life)fx.SmokeBurst(age,new Vector3(c.X,c.Y+.25f,c.Z),new Vector3(0,.35f,0),g*.7f,.45f,12,1.4f,.4f,1.3f,new Vector3(.1f,.095f,.09f),.3f,life,.4f,seed,9);
            // Thornwake's burning brambles: thorns ring the boundary and dot the bed, sprouting at the birth, their tips glowing,
            // crumbling to ash after the expiry.
            if(thorn)
            {
                int n=Math.Max(6,(int)(fx.Scale((int)(perim*2.4f))));
                for(int i=0;i<n;i++)
                {
                    bool onRim=i%3!=0;float a=(i+H(seed,1200+i)*.6f)*6.2831853f/n,r=onRim?g*(.84f+.13f*H(seed,1230+i)):g*.7f*(float)Math.Sqrt(H(seed,1260+i));
                    float grow=BossMath.Smooth((age-H(seed,1290+i)*.12f)/.22f),crumble=age>life?BossMath.Smooth((age-life)/1.1f):0;float sc=(.7f+.7f*H(seed,1320+i))*grow*(1-crumble*.8f);if(sc<.02f)continue;
                    float yaw=a+(float)Math.PI*.5f+(H(seed,1350+i)-.5f)*1.2f,lean=.25f+.3f*H(seed,1380+i);
                    var m=Matrix4x4.CreateScale(sc)*Matrix4x4.CreateRotationX(lean)*Matrix4x4.CreateRotationY(-yaw)*Matrix4x4.CreateTranslation(c.X+(float)Math.Cos(a)*r,c.Y-.03f-crumble*.2f,c.Z+(float)Math.Sin(a)*r);
                    float fl=.6f+.4f*(float)Math.Sin(age*11+i*1.7f);lit.Add(NativeRoundsMeshes.ThornSpike,m,new Vector3(.05f,.035f,.025f),.25f,look.Hot*(1.4f*fl*(1-crumble))+look.Cool*.25f,.3f,look.Hot*.6f);
                }
            }
        }

        // ---- frost: Cryo Snare, Binding Lattice ----
        private void Frost(Vector3 c,float g,float age,float life,uint seed,in RoundsFieldLook look,NativeBossVfx fx,RoundsLit lit)
        {
            bool lattice=look.Variant==1;float perim=6.2831853f*g;
            // The ice front sweeps out to the boundary in .18 s; crystals rise where it passes; at the expiry they shatter.
            float front=g*BossMath.Smooth(age/.18f);
            int n=Math.Max(8,fx.Scale((int)(perim*3.4f)+(int)(g*g*1.2f)));
            for(int i=0;i<n;i++)
            {
                bool rim=i%4!=3;float a=(i+H(seed,2000+i)*.7f)*6.2831853f/n,r=rim?g*(.9f+.09f*H(seed,2040+i)):g*.85f*(float)Math.Sqrt(H(seed,2080+i));
                float grow=BossMath.Smooth((front-r)/.35f+.0f)*BossMath.Smooth(age/.05f);float sh=age>life?BossMath.Smooth((age-life)/.12f):0;
                var at=new Vector3(c.X+(float)Math.Cos(a)*r,c.Y,c.Z+(float)Math.Sin(a)*r);
                if(sh<1&&grow>.02f)
                {
                    float sc=(rim?.75f+.6f*H(seed,2120+i):.4f+.35f*H(seed,2120+i))*grow*(1-sh);float tilt=(rim?.35f:.15f)+.25f*H(seed,2160+i);
                    var m=Matrix4x4.CreateScale(sc,sc*(.8f+.5f*H(seed,2200+i)),sc)*Matrix4x4.CreateRotationZ(tilt)*Matrix4x4.CreateRotationY(-a+(H(seed,2240+i)-.5f)*.8f)*Matrix4x4.CreateTranslation(at.X,c.Y-.02f,at.Z);
                    var ice=lattice?new Vector3(.62f,.66f,.72f):new Vector3(.45f,.62f,.78f);lit.Add(NativeRoundsMeshes.IceCrystal,m,ice,1.1f,look.Hot*(.35f+.25f*(float)Math.Sin(age*2+i)),.94f,look.Hot*1.6f);
                }
                // The shattering: each crystal bursts into shards that fly and fall.
                if(age>life&&i%2==0)fx.RubbleBurst(age,at+new Vector3(0,.18f,0),new Vector3(0,1,0),.9f,2.6f,3,.9f,life,.05f,new Vector3(.62f,.8f,.95f),seed,3000+i,0,c.Y);
            }
            // The forming front's bright crystalline edge.
            if(age<.3f){int m=fx.Scale(40);for(int i=0;i<m;i++){float a=i*6.2831853f/m;var p=new Vector3(c.X+(float)Math.Cos(a)*front,c.Y+.05f,c.Z+(float)Math.Sin(a)*front);fx.Glow(p,look.Core*(2.4f*(1-age/.3f)),.22f,NativeBossVfx.Mote);}}
            // Cold mist hugging the ice, drifting slowly out, never past the boundary by much.
            float mr=(4+g*g*.9f);fx.SmokeFlow(age,new Vector3(c.X,c.Y+.1f,c.Z),Vector3.Zero,g*.6f,.025f,mr,2.6f,.7f,1.8f,new Vector3(.66f,.76f,.86f),.3f,.05f,life,seed,4,lit:.28f);
            // Glints: frost sparkling as the light catches it.
            int gl=fx.Scale(28);
            for(int i=0;i<gl;i++){var at=Spot(c,front*.97f,H(seed,2400+i),H(seed,2440+i));float tw=(float)Math.Max(0,Math.Sin(age*(3+4*H(seed,2480+i))+i*2.1f));tw*=tw*tw;if(age>life)tw*=1-BossMath.Smooth((age-life)/.4f);if(tw<.05f)continue;fx.Glow(new Vector3(at.X,c.Y+.04f,at.Z),look.Core*(2.2f*tw),.09f,NativeBossVfx.Mote);}
            // Shatter: a burst of mist and sparkle at the expiry.
            if(age>life)fx.SmokeBurst(age,new Vector3(c.X,c.Y+.2f,c.Z),new Vector3(0,.4f,0),g*.6f,.2f,14,1.1f,.5f,1.4f,new Vector3(.75f,.82f,.9f),.28f,life,.15f,seed,5,lit:.15f);
            // Binding Lattice: six pillars on the boundary bound by glowing chains round it and across it.
            if(lattice)
            {
                float up=BossMath.Smooth((age-.08f)/.2f)*(age>life?1-BossMath.Smooth((age-life)/.3f):1);if(up<.02f)return;
                for(int i=0;i<6;i++)
                {
                    float a=i*1.0471976f+H(seed,2600)*1.04f;var at=new Vector3(c.X+(float)Math.Cos(a)*g*.97f,c.Y,c.Z+(float)Math.Sin(a)*g*.97f);
                    var m=Matrix4x4.CreateScale(1,up,1)*Matrix4x4.CreateTranslation(at.X,c.Y+.5f*up,at.Z);lit.Add(NativeRoundsMeshes.Pillar,m,new Vector3(.5f,.52f,.58f),.9f,look.Hot*1.2f,.8f,look.Hot*1.4f);
                    float b=i*1.0471976f+H(seed,2600)*1.04f+1.0471976f;var to=new Vector3(c.X+(float)Math.Cos(b)*g*.97f,c.Y,c.Z+(float)Math.Sin(b)*g*.97f);
                    Chain(fx,at+new Vector3(0,.82f*up,0),to+new Vector3(0,.82f*up,0),look.Hot*(1.6f*up),.03f,age,i);
                    if(i<3){var op=new Vector3(c.X*2-at.X,c.Y,c.Z*2-at.Z);Chain(fx,at+new Vector3(0,.1f,0),op+new Vector3(0,.1f,0),look.Hot*(.9f*up),.02f,age,10+i);}
                }
            }
        }
        // A glowing chain between a and b: short links as streaks, sagging a little, shimmering along.
        private static void Chain(NativeBossVfx fx,Vector3 a,Vector3 b,Vector3 rgb,float width,float age,int key)
        {
            float len=Vector3.Distance(a,b);int links=Math.Max(3,Math.Min(28,(int)(len/.18f)));
            for(int j=0;j<links;j++){float u0=(float)j/links,u1=(j+.8f)/links;Vector3 p0=Vector3.Lerp(a,b,u0)-new Vector3(0,(float)Math.Sin(Math.PI*u0)*len*.04f,0),p1=Vector3.Lerp(a,b,u1)-new Vector3(0,(float)Math.Sin(Math.PI*u1)*len*.04f,0);
                float sh=.7f+.3f*(float)Math.Sin(age*6-j*.8f+key);fx.Spark(p1,p1-p0,rgb*sh,width,(p1-p0).Length(),1,.9f);}
        }

        // ---- slow time: Suppression Cloud, Temporal Sacrifice, the interdicts' time fields ----
        /// <summary>The time field's motes drift at this share of a normal drift (enemy shots inside move at 20%).</summary>
        public const float SlowShare=.2f;
        private void Time(in RoundsFieldInput f,float g,float age,float life,float clock,uint seed,in RoundsFieldLook look,NativeBossVfx fx,in RoundsShots shots)
        {
            float R=f.Radius,st=Strength(RoundsStyle.Time,age,life);var c=f.Centre;float floor=f.Floor;
            // Motes suspended in the sphere, each drifting on a slow curl at SlowShare of its own pace, trailing three afterimages a
            // fixed time apart (the slow motion reads as the echoes bunching up), all of them inside the radius.
            int n=fx.Scale(64);float slow=age<=life?SlowShare:SlowShare+(1-SlowShare)*BossMath.Smooth((age-life)/.3f);
            for(int i=0;i<n;i++)
            {
                int k=4000+i*9;var d=new Vector3(H(seed,k)-.5f,H(seed,k+1)-.5f,H(seed,k+2)-.5f);float l=d.Length();if(l<1e-4f)continue;d/=l;float rr=R*.92f*(float)Math.Pow(H(seed,k+3),1/3.0);
                var home=c+d*rr;if(home.Y<floor+.1f)home.Y=floor+.1f+H(seed,k+4)*.4f;float ph=H(seed,k+5)*6.28f,sp=(.9f+.8f*H(seed,k+6));
                for(int e=0;e<4;e++)
                {
                    float t=(age-e*.16f)*slow*sp;var p=home+new Vector3((float)Math.Sin(ph+t*1.3f),(float)Math.Sin(ph*1.7f+t)*.6f,(float)Math.Cos(ph+t*1.1f))*.35f;
                    var off=p-c;float ol=off.Length();if(ol>R*.97f)p=c+off*(R*.97f/ol);if(p.Y<floor+.05f)p.Y=floor+.05f;
                    float a=st*(e==0?1:.42f/e);fx.Glow(p,(e==0?look.Core*1.5f:look.Hot*.9f)*a,e==0?.07f:.06f,NativeBossVfx.Mote);
                }
            }
            // A shot inside: its slowed echoes (four ghosts behind it at fixed time steps of its own motion) and a ripple round it.
            for(int i=0;i<shots.Count;i++)
            {
                var sp=shots.P[i];var sv=shots.V[i];if(Vector3.DistanceSquared(sp,c)>R*R)continue;
                for(int e=1;e<=4;e++){var p=sp-sv*(e*.035f);fx.Glow(p,look.Hot*(st*1.2f/e),.12f,NativeBossVfx.Mote);}
                fx.Glow(sp,look.Core*(st*1.4f),.32f+.06f*(float)Math.Sin(clock*9+i),NativeBossVfx.RingGlow);
            }
            // The birth's time-stop: a ring of motes thrown out that brake hard to a stop, and a flash.
            if(age<.6f)
            {
                int m=fx.Scale(36);for(int i=0;i<m;i++){float a=i*6.2831853f/m,v0=6+3*H(seed,4900+i),dist=v0*(1-(float)Math.Exp(-8*age))/8;float rr=Math.Min(R*.95f,dist);var p=new Vector3(c.X+(float)Math.Cos(a)*rr,Math.Max(floor+.1f,c.Y+(H(seed,4950+i)-.5f)*R*.6f),c.Z+(float)Math.Sin(a)*rr);fx.Glow(p,look.Core*(2*(1-age/.6f)),.12f,NativeBossVfx.Mote);}
                if(age<.2f)fx.Glow(c,look.Core*(3*(1-age/.2f)),R*.9f,NativeBossVfx.Core);
            }
            // Shadow Interdict: shadow wisps curling low inside.
            if(look.Variant==4)fx.SmokeFlow(age,new Vector3(c.X,floor+.15f,c.Z),Vector3.Zero,Math.Min(g,R)*.6f,.06f,6,2,.6f,1.3f,new Vector3(.02f,.012f,.03f),.38f,0,life,seed,6,glow:.12f);
        }

        // ---- repulsion: Repulsor Well, Emergency Vent ----
        private void Push(Vector3 c,float g,float age,float life,uint seed,in RoundsFieldLook look,NativeBossVfx fx)
        {
            bool vent=look.Variant==1;float on=age<=life?1:1-BossMath.Smooth((age-life)/.4f);
            // Dust and grit swept out along the floor from the centre to the boundary (each grain starts where its slot puts it and
            // slides out at a speed that keeps it inside), in the pressure rings' rhythm.
            float rate=(10+g*g*2.2f)*fx.RateScale;int first,last;Slots(age,rate,.8f,life,out first,out last);
            for(int s=first;s<=last;s++)
            {
                int k=6000+s*5;float born=(s+H(seed,k))/rate,a=age-born,lf=.45f+.35f*H(seed,k+1);if(a<0||a>lf||born>life)continue;
                float ang=H(seed,k+2)*6.2831853f,r0=g*.2f*H(seed,k+3),r=r0+(g*.98f-r0)*BossMath.Smooth(a/lf);var dir=new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang));
                var p=new Vector3(c.X,c.Y+.08f+.15f*H(seed,k+4),c.Z)+dir*r;float u=a/lf;fx.Spark(p,dir*(g*1.4f),look.Hot*(1.3f*(1-u)*on),.025f,.12f,.06f,.9f,u,s);
            }
            // Pressure puffs pushed out from the centre (pale dust, or the vent's steam).
            fx.SmokeFlow(age,new Vector3(c.X,c.Y+.25f,c.Z),Vector3.Zero,.2f,.05f,vent?9:4,1.1f,.5f,1.3f,vent?new Vector3(.86f,.84f,.82f):new Vector3(.5f,.42f,.48f),vent?.4f:.16f,0,life,seed,7,lit:vent?.55f:.12f);
            // The Emergency Vent's blast: steam jets thrown out round the centre at the birth, red alarm glows on the boundary.
            if(vent)
            {
                if(age<1.2f){int n=fx.Scale(24);for(int i=0;i<n;i++){float a=i*6.2831853f/n;var dir=new Vector3((float)Math.Cos(a),.15f,(float)Math.Sin(a));fx.SmokeBurst(age,new Vector3(c.X,c.Y+.45f,c.Z),dir*(g*1.7f),.45f,.35f,3,1.1f,.5f,1.6f,new Vector3(.9f,.88f,.86f),.6f,0,.1f,seed,8000+i,lit:.7f);}}
                int al=fx.Scale(12);for(int i=0;i<al;i++){float a=i*6.2831853f/al;float bl=(float)Math.Max(0,Math.Sin(age*9+i*.5f));fx.Glow(new Vector3(c.X+(float)Math.Cos(a)*g*.96f,c.Y+.12f,c.Z+(float)Math.Sin(a)*g*.96f),look.Hot*(1.8f*bl*on),.4f,NativeBossVfx.Mote);}
            }
        }

        // ---- gravity: Attractor Well ----
        private void Pull(in RoundsFieldInput f,float g,float age,float life,uint seed,in RoundsFieldLook look,NativeBossVfx fx,RoundsLit lit,in RoundsShots shots)
        {
            var c=f.Centre;float R=f.Radius,floor=f.Floor;float on=age<=life?1:1-BossMath.Smooth((age-life)/.55f);
            // Streams of dust and light spiralling in from the boundary sphere to the core, faster as they fall (a pure function of
            // each grain's slot: its radius shrinks on an accelerating curve while it turns).
            float rate=(18+R*R*2.5f)*fx.RateScale;int first,last;Slots(age,rate,1.3f,life,out first,out last);
            for(int s=first;s<=last;s++)
            {
                int k=7000+s*6;float born=(s+H(seed,k))/rate,a=age-born,lf=.8f+.5f*H(seed,k+1);if(a<0||a>lf||born>life)continue;
                float u=a/lf,fall=u*u*(3-2*u),r=R*.97f*(1-fall*.93f),turn=H(seed,k+2)*6.2831853f+(fall*2.6f+u)*(float)Math.PI;float yy=(H(seed,k+3)-.5f)*1.2f*(1-fall);
                var p=new Vector3(c.X+(float)Math.Cos(turn)*r,Math.Max(floor+.06f,c.Y+yy*R*.4f),c.Z+(float)Math.Sin(turn)*r);var v=new Vector3(-(float)Math.Sin(turn),0,(float)Math.Cos(turn))*(r*3)-new Vector3((float)Math.Cos(turn),0,(float)Math.Sin(turn))*(R*2*fall);
                fx.Spark(p,v,Vector3.Lerp(look.Hot,look.Core,fall)*(1.6f*on),.02f,.05f,.035f,.9f,u,s);
            }
            // The core: a dark lit orb with an accretion ring, collapsing at the expiry with a flash.
            float core=(.22f+.04f*(float)Math.Sin(age*5))*(age<=life?BossMath.Smooth(age/.2f):1-BossMath.Smooth((age-life)/.25f));
            if(core>.01f)
            {
                lit.Add(NativeRoundsMeshes.DroneLens,Matrix4x4.CreateScale(core/.04f)*Matrix4x4.CreateTranslation(c),new Vector3(.004f,.002f,.008f),.05f,Vector3.Zero,.9f,look.Hot*3);
                var ring=Matrix4x4.CreateScale(core*8)*Matrix4x4.CreateRotationX(.35f)*Matrix4x4.CreateRotationY(age*3)*Matrix4x4.CreateTranslation(c);lit.Add(NativeRoundsMeshes.SatRing,ring,look.Cool*.3f,.4f,look.Core*(2.6f*on),.6f,look.Hot*2);
                fx.Glow(c,look.Hot*(1.2f*on),core*7,NativeBossVfx.RingGlow);
            }
            if(age>life&&age<life+.3f)fx.Glow(c,look.Core*(5*(1-(age-life)/.3f)),R*.7f,NativeBossVfx.Core);
            // Shots inside bend toward the core: a faint streak along their pull.
            for(int i=0;i<shots.Count;i++){var sp=shots.P[i];if(Vector3.DistanceSquared(sp,c)>R*R)continue;var to=c-sp;fx.Spark(sp,to*(.6f/Math.Max(.3f,to.Length())),look.Hot*(1.1f*on),.03f,.2f,.5f,.95f);}
        }

        // ---- storm: Storm Relay ----
        private void Storm(Vector3 c,float g,float age,float life,float clock,uint seed,in RoundsFieldLook look,NativeBossVfx fx,RoundsLit lit)
        {
            float on=age<=life?BossMath.Smooth(age/.15f):1-BossMath.Smooth((age-life)/.4f);if(on<.01f)return;
            // Six relay pylons on the boundary, rising at the birth, arcs crawling between neighbours along the rim (violet and white:
            // the owner cut blue and cyan lightning from areas of effect, 2026-10-10).
            Vector3 Top(int i){float a=i*1.0471976f+H(seed,8800)*1.04f;return new Vector3(c.X+(float)Math.Cos(a)*g*.97f,c.Y+.62f*on,c.Z+(float)Math.Sin(a)*g*.97f);}
            for(int i=0;i<6;i++)
            {
                var t=Top(i);var basePos=new Vector3(t.X,c.Y,t.Z);
                lit.Add(NativeRoundsMeshes.PylonFoot,Matrix4x4.CreateTranslation(basePos+new Vector3(0,.025f,0)),new Vector3(.08f,.08f,.1f),.6f,Vector3.Zero,.6f,look.Hot*.4f);
                lit.Add(NativeRoundsMeshes.PylonMast,Matrix4x4.CreateScale(1,on,1)*Matrix4x4.CreateTranslation(basePos+new Vector3(0,.3f*on,0)),new Vector3(.12f,.12f,.15f),.8f,look.Hot*.25f,.7f,look.Hot*.5f);
                float fl=.6f+.4f*(float)Math.Sin(clock*23+i*1.9f);lit.Add(NativeRoundsMeshes.PylonHead,Matrix4x4.CreateTranslation(t),new Vector3(.2f,.15f,.25f),.5f,look.Hot*(2.4f*fl*on),.5f,look.Core*2);
                // Arc to the next pylon: re-struck every 1/12 s along a fresh jagged path.
                int strike=(int)Math.Floor(age*12);if(H(seed,8900+strike*7+i)<.75f)Bolt(fx,t,Top((i+1)%6),look,on,(uint)(seed+strike*131+i),.03f,6);
            }
            // Strikes from the storm cell into the area: one every .22 s at a hashed spot inside, a bolt and a ground flash.
            float cell=c.Y+3.4f;int s0=(int)Math.Floor((age-.18f)/.22f),s1=(int)Math.Floor(Math.Min(age,life)/.22f);
            for(int s=Math.Max(0,s0);s<=s1;s++)
            {
                float at=s*.22f,a=age-at;if(a<0||a>.18f)continue;var spot=Spot(c,g*.9f,H(seed,9000+s),H(seed,9040+s));float fade=1-a/.18f;
                Bolt(fx,new Vector3(spot.X+(H(seed,9080+s)-.5f)*.8f,cell,spot.Z+(H(seed,9120+s)-.5f)*.8f),new Vector3(spot.X,c.Y+.02f,spot.Z),look,fade*on,(uint)(seed+s*977),.05f,12);
                fx.Glow(new Vector3(spot.X,c.Y+.1f,spot.Z),look.Core*(3f*fade*on),1.1f,NativeBossVfx.Core);
                fx.SparkBurst(age,new Vector3(spot.X,c.Y+.05f,spot.Z),Vector3.UnitY,1.2f,3.5f,10,.35f,at,.02f,seed,9200+s,.9f,.016f,1.2f,true,c.Y);
            }
            // The storm cell: dark violet cloud over the area lit from within by the strikes.
            fx.SmokeFlow(age,new Vector3(c.X,cell,c.Z),Vector3.Zero,g*.6f,.02f,5,2.6f,1.2f,2.4f,new Vector3(.09f,.07f,.13f),.45f*on,0,life,seed,10,glow:.25f,lit:.08f);
        }
        // A jagged bolt from a to b: segments as streaks with a hot core, the path re-hashed by `s`.
        private static void Bolt(NativeBossVfx fx,Vector3 a,Vector3 b,in RoundsFieldLook look,float gain,uint s,float width,int steps)
        {
            var prev=a;var d=b-a;float len=d.Length();if(len<1e-3f)return;var side=Vector3.Normalize(Vector3.Cross(d,Math.Abs(d.Y)>.9f*len?Vector3.UnitX:Vector3.UnitY));var side2=Vector3.Normalize(Vector3.Cross(d,side));
            for(int i=1;i<=steps;i++)
            {
                float u=(float)i/steps,j=i<steps?(float)Math.Sin(Math.PI*u):0;var p=a+d*u+(side*(BossMath.Hash(s,i)-.5f)+side2*(BossMath.Hash(s,i+40)-.5f))*(len*.16f*j);
                fx.Spark(p,p-prev,look.Core*(3.2f*gain),width,(p-prev).Length(),1,.95f);fx.Spark(p,p-prev,look.Hot*(1.6f*gain),width*2.6f,(p-prev).Length(),1,.95f);prev=p;
            }
        }
    }

    /// <summary>The shots the slow-time and gravity fields react to this frame: drawn positions and velocities (fixed buffers).</summary>
    public struct RoundsShots
    {
        public Vector3[] P,V;public int Count;
        public static RoundsShots Make(int capacity)=>new RoundsShots{P=new Vector3[capacity],V=new Vector3[capacity]};
        public void Add(Vector3 p,Vector3 v){if(P!=null&&Count<P.Length){P[Count]=p;V[Count]=v;Count++;}}
    }
}
