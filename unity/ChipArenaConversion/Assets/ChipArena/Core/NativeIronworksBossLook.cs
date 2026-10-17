using System;
using System.Collections.Generic;
using System.Numerics;
using M=ChipArena.UnityConversion.NativeIronworksBossModel;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// THE CONVERTER as the game draws it (docs/EXPEDITION-BOSS-CITY-IRONWORKS.md "Presentation build"): the concept model's
    /// skeleton and geometry (Core/NativeIronworksBossModel.cs, its three tiers) with the presentation's own joints after the
    /// concept's 34 and a quality pass inside the concept's part volumes:
    /// <list type="bullet">
    /// <item>The slag crust re-sculpted as glassy plates: irregular chamfered slabs whose chamfer catches the light like obsidian,
    /// glints on the faces, a dim red seam under each plate's lower edge, glassy drips off the lowest row. Each group splits into
    /// three chunks (<see cref="Crust"/>: the top, middle and bottom rows) that fly, tumble and lie as rubble on their own.</item>
    /// <item>The mouth: the white metal on its own joint (<see cref="Metal"/>, so it sloshes against the vessel) and the hot lip and
    /// band seams re-sculpted without the concept's blocky spill boxes; the spill down the nose is a molten strip drawn by the
    /// runtime through the molten shader (<see cref="SpillPath"/>).</item>
    /// <item>The work lamps (its eyes) on their own joints (<see cref="LampL"/>, <see cref="LampR"/>): a bezel, a ribbed lens and
    /// a grille, driven per joint (they click on in the arrival, flicker out in the death).</item>
    /// <item>The foreman re-sculpted in his aluminised heat suit: quilted coat, belt and gauntlets, a square hood with a wide cyan
    /// visor on its own glow joint (<see cref="Visor"/>), a white hard hat, the respirator lamp; split into hips, chest and head so he
    /// breathes and looks, and two-bone arms (<see cref="ArmUL"/>..<see cref="ArmFR"/>) whose gauntlets stay on the tilt levers.</item>
    /// <item>The blast valve wheel on its own joint (<see cref="Valve"/>: it spins in the blow).</item>
    /// <item>A grime pass in the vertex colours (no triangles): soot from the mouth down the shell, heat scale low on the vessel, dirt
    /// rising up the legs from the feet, oil and wear on the girders.</item>
    /// </list>
    /// Deterministic: the same tier gives the same triangles; built once per tier, nothing per frame.
    /// </summary>
    public static class NativeIronworksBossLook
    {
        // ---------------- joints (the concept's 0..33, then the presentation's) ----------------
        public const int ChunkBase=M.Count,Chunks=3,LampL=42,LampR=43,Chest=44,Head=45,Visor=46,ArmUL=47,ArmFL=48,ArmUR=49,ArmFR=50,Valve=51,Metal=52,Count=53;
        /// <summary>Crust group g's (0 front, 1 left, 2 right, 3 back) chunk c (0 top, 1 middle, 2 bottom rows).</summary>
        public static int Crust(int group,int chunk)=>chunk==0?M.CrustFront+group:ChunkBase+group*2+chunk-1;
        public static bool IsCrust(int j)=>j>=M.CrustFront&&j<=M.CrustBack||j>=ChunkBase&&j<ChunkBase+8;
        public static int GroupOf(int j)=>j>=M.CrustFront&&j<=M.CrustBack?j-M.CrustFront:j>=ChunkBase&&j<ChunkBase+8?(j-ChunkBase)/2:-1;
        public static int ChunkOf(int j)=>j>=M.CrustFront&&j<=M.CrustBack?0:j>=ChunkBase&&j<ChunkBase+8?(j-ChunkBase)%2+1:-1;
        public static readonly string[] Names=new string[Count];public static readonly int[] Parent=new int[Count];
        /// <summary>Triangle budgets per tier (full, reduced, low), the chain links drawn instanced counted apart.</summary>
        public static readonly int[] Budget={47000,29000,10000};
        // ---------------- the presentation joints' frames ----------------
        /// <summary>The left lamp's lens centre (yoke frame); the right mirrors it.</summary>
        public static readonly Vector3 LampAt=new Vector3(-M.GirderX,.72f,2.09f);
        /// <summary>The foreman: his waist (foreman frame) and neck (chest frame), the shoulders (chest frame, left), the arms.</summary>
        public static readonly Vector3 Waist=new Vector3(0,.92f,0),Neck=new Vector3(0,.68f,.01f),Shoulder=new Vector3(-.31f,.54f,.02f),VisorAt=new Vector3(0,.18f,.197f);
        public const float UpperArm=.4f,Forearm=.36f;
        /// <summary>The valve wheel's centre (pulpit frame; it turns about its local z).</summary>
        public static readonly Vector3 ValveAt=new Vector3(-.78f,1.02f,.3f);
        /// <summary>The mouth's metal surface centre (vessel frame, scaled).</summary>
        public static readonly Vector3 MetalAt=new Vector3(0,MetalLevel*M.VesselScale,M.Nose(MetalLevel)*M.VesselScale);
        private const float MetalLevel=2.46f,MetalR=.76f;
        /// <summary>The spill down the nose (vessel frame, scaled): the strip's centreline from the lip down (the runtime draws
        /// it as molten flow, a box per segment along it; its width at each point).</summary>
        public static readonly Vector3[] SpillPath=MakeSpill(out SpillWidth);public static readonly float[] SpillWidth;
        /// <summary>The shift whistle's mouth (pulpit frame): its steam plume blows from here.</summary>
        public static readonly Vector3 Whistle=new Vector3(.9f,2.12f,-.5f);
        /// <summary>The tuyère grate's centre (vessel frame, scaled) and the trunnion seals (vessel frame, scaled): steam jets.</summary>
        public static readonly Vector3 Grate=new Vector3(0,-2.38f*M.VesselScale,0);
        /// <summary>The crust chunks' centres (vessel frame, scaled), the pivots they fly and tumble about.</summary>
        public static readonly Vector3[] ChunkCentre=new Vector3[12];public static readonly float[] ChunkRadius=new float[12];

        static NativeIronworksBossLook()
        {
            for(int i=0;i<M.Count;i++){Names[i]=M.Names[i];Parent[i]=M.Parent[i];}
            void J(int j,string n,int p){Names[j]=n;Parent[j]=p;}
            for(int g=0;g<4;g++)for(int c=1;c<Chunks;c++)J(Crust(g,c),M.Names[M.CrustFront+g]+"-"+(c==1?"mid":"low"),M.Vessel);
            J(LampL,"lamp-l",M.Yoke);J(LampR,"lamp-r",M.Yoke);J(Chest,"foreman-chest",M.Foreman);J(Head,"foreman-head",Chest);J(Visor,"foreman-visor",Head);
            J(ArmUL,"arm-upper-l",Chest);J(ArmFL,"arm-fore-l",ArmUL);J(ArmUR,"arm-upper-r",Chest);J(ArmFR,"arm-fore-r",ArmUR);J(Valve,"valve",M.Pulpit);J(Metal,"metal",M.Vessel);
            // The chunks' pivots from the plate layout (tier independent).
            var sum=new Vector3[12];var n=new int[12];var far=new float[12];
            foreach(var pl in Plates()){int k=pl.Zone*3+pl.Chunk;var c=PlateCentre(pl)*M.VesselScale;sum[k]+=c;n[k]++;}
            for(int k=0;k<12;k++)ChunkCentre[k]=n[k]>0?sum[k]/n[k]:Vector3.Zero;
            foreach(var pl in Plates()){int k=pl.Zone*3+pl.Chunk;far[k]=Math.Max(far[k],Vector3.Distance(PlateCentre(pl)*M.VesselScale,ChunkCentre[k])+pl.W*.6f*M.VesselScale);}
            Array.Copy(far,ChunkRadius,12);
        }
        /// <summary>The chunk index 0..11 (group x 3 + chunk) of a crust joint.</summary>
        public static int ChunkIndex(int j)=>GroupOf(j)*3+ChunkOf(j);

        // ================================================================================================================
        // build
        // ================================================================================================================
        public static IronworksBossMesh Build(int tier)
        {
            tier=Math.Max(0,Math.Min(2,tier));var m=M.Build(tier);var body=m.Body;
            // Re-sculpted joints: cleared, then drawn anew.
            foreach(int j in new[]{M.VesselHeat,M.CrustFront,M.CrustLeft,M.CrustRight,M.CrustBack,M.Foreman,M.ForemanArms})Clear(body.Section(j,WeaponStretch.None));
            SplitLamps(body);SplitValve(body);
            var s=new WeaponSculpt(body,tier);
            Mouth(s,tier);CrustPlates(s,tier);ForemanSuit(s,tier);Lamps(s,tier);ValveWheel(s,tier);
            Grime(body);
            // Empty sections (the concept's arms joint is rebuilt as four) are dropped.
            body.Sections.RemoveAll(x=>x.Indices.Count==0);
            return m;
        }
        /// <summary>The total triangles drawn for one boss at a tier with `links` chain links showing.</summary>
        public static int Drawn(IronworksBossMesh m,int links)=>m.Body.Triangles+m.Link.Triangles*links+m.Hook.Triangles*2+m.Shackle.Triangles*2;
        private static void Clear(WeaponSection s){s.Positions.Clear();s.Normals.Clear();s.Colors.Clear();s.Emission.Clear();s.Indices.Clear();}

        // Moves the yoke's two work lamps (their housing, lens, hood and grille strips) into the lamp joints, re-expressed about the
        // lens centre. The new lens and bezel go on top (Lamps).
        private static void SplitLamps(WeaponMesh mesh)
        {
            var yoke=mesh.Section(M.Yoke,WeaponStretch.None);var keep=new WeaponSection{Bone=M.Yoke};
            for(int t=0;t+2<yoke.Indices.Count;t+=3)
            {
                int a=yoke.Indices[t],b=yoke.Indices[t+1],c=yoke.Indices[t+2];var p=(yoke.Positions[a]+yoke.Positions[b]+yoke.Positions[c])/3;
                int to=-1;foreach(int side in new[]{-1,1}){float x=side*M.GirderX;if(Math.Abs(p.X-x)<.26f&&p.Y>M.GirderHalf+.25f&&p.Y<1.0f&&p.Z>1.58f&&p.Z<2.22f)to=side<0?LampL:LampR;}
                var dst=to<0?keep:mesh.Section(to,WeaponStretch.None);var off=to<0?Vector3.Zero:LampOrigin(to);int n=dst.Positions.Count;
                foreach(int v in new[]{a,b,c}){var e=yoke.Emission[v];bool lens=e.X+e.Y+e.Z>1e-4f;if(lens&&to>=0)continue;dst.Positions.Add(yoke.Positions[v]-off);dst.Normals.Add(yoke.Normals[v]);dst.Colors.Add(yoke.Colors[v]);dst.Emission.Add(e);}
                if(dst.Positions.Count==n+3){dst.Indices.Add(n);dst.Indices.Add(n+1);dst.Indices.Add(n+2);}
            }
            Replace(yoke,keep);
        }
        /// <summary>A lamp joint's origin in the yoke frame.</summary>
        public static Vector3 LampOrigin(int lamp)=>lamp==LampL?LampAt:new Vector3(-LampAt.X,LampAt.Y,LampAt.Z);
        // Moves the blast valve's wheel (rim and spokes) into its own joint about its hub.
        private static void SplitValve(WeaponMesh mesh)
        {
            var pul=mesh.Section(M.Pulpit,WeaponStretch.None);var keep=new WeaponSection{Bone=M.Pulpit};var dst=mesh.Section(Valve,WeaponStretch.None);
            for(int t=0;t+2<pul.Indices.Count;t+=3)
            {
                int a=pul.Indices[t],b=pul.Indices[t+1],c=pul.Indices[t+2];var p=(pul.Positions[a]+pul.Positions[b]+pul.Positions[c])/3;
                bool wheel=Vector3.Distance(p,ValveAt)<.31f&&Math.Abs(p.Z-ValveAt.Z)<.06f&&p.Y>ValveAt.Y-.3f;
                var to=wheel?dst:keep;var off=wheel?ValveAt:Vector3.Zero;int n=to.Positions.Count;
                foreach(int v in new[]{a,b,c}){to.Positions.Add(pul.Positions[v]-off);to.Normals.Add(pul.Normals[v]);to.Colors.Add(pul.Colors[v]);to.Emission.Add(pul.Emission[v]);}
                to.Indices.Add(n);to.Indices.Add(n+1);to.Indices.Add(n+2);
            }
            Replace(pul,keep);
        }
        private static void Replace(WeaponSection into,WeaponSection from)
        {
            Clear(into);into.Positions.AddRange(from.Positions);into.Normals.AddRange(from.Normals);into.Colors.AddRange(from.Colors);into.Emission.AddRange(from.Emission);into.Indices.AddRange(from.Indices);
        }

        // ---------------- palette ----------------
        private const int SlagHex=0x110e0f,SlagGlassHex=0x2b272e,SlagSideHex=0x1d191b,GlintHex=0x3d3a44,SilverHex=0xbab7ae,SilverDarkHex=0x6e6c66,QuiltHex=0x8e8b83,
            LeatherHex=0x3a2414,RubberHex=0x141414,HatHex=0xdcd6c4,GlassHex=0x0b0e12,IronHex=0x24211e,IronDarkHex=0x141210,EdgeHex=0x55504a,SteelHex=0x5e5b55,GreenHex=0x2f4038;
        private static WeaponPaint P(int hex,float sheen=0)=>WeaponPaint.Solid(hex,sheen);
        private static WeaponPaint G(int hex,float strength,float albedo=.3f)=>WeaponPaint.Glow(hex,strength,albedo);

        // ---------------- the mouth: the hot lip, the band seams' heat, the metal ----------------
        private static void Mouth(WeaponSculpt s,int tier)
        {
            int sides=s.Sides(28);var molten=G(NativeIronworksKinds.Molten,2.6f,.08f);
            s.Into(M.VesselHeat);s.Push(WeaponSculpt.S(M.VesselScale,M.VesselScale,M.VesselScale));
            // The hot lip: the inner lining glowing down to the metal, the rim's lip, the outer edge cooler.
            LatheY(s,_=>molten,new[]{.86f,.81f,.775f},new[]{2.72f,2.62f,2.5f},sides,true);
            LatheY(s,_=>molten.Scaled(.5f),new[]{1.05f,.99f,.88f},new[]{2.675f,2.765f,2.738f},sides,true);
            // The shell's heat in the seams under the hoop bands (dull red; mostly under the crust until it sheds).
            foreach(float hy in new[]{-1.5f,-.95f,.9f,1.42f}){float hr=M.ShellR(hy-.11f)+.012f;LatheY(s,_=>molten.Scaled(.3f),new[]{hr,hr},new[]{hy-.135f,hy-.1f},sides,true);}
            s.Pop();
            // The metal: a white-hot disc on its own joint (its frame at the surface's centre), a darker skin round its edge.
            s.Into(Metal);var metal=G(NativeIronworksKinds.WhiteHot,6.0f,.06f);var skin=G(NativeIronworksKinds.Molten,3.4f,.06f);
            float r=MetalR*M.VesselScale;int ms=s.Sides(26);
            var rings=new List<Vector3[]>{Disc(ms,.001f,0),Disc(ms,r*.62f,.004f),Disc(ms,r*.86f,0),Disc(ms,r,-.02f)};
            s.Push(WeaponSculpt.Rx(-(float)Math.PI/2));s.Loft(rings,(i,k)=>i==2?skin:metal,1,false,false);s.Pop();
            if(tier==0)for(int i=0;i<7;i++){double a=i*2.4;float rr=r*(.25f+.5f*(float)Hash(i,3,1));s.Box(metal.Scaled(1.25f),new Vector3((float)Math.Sin(a)*rr,.01f,(float)Math.Cos(a)*rr),new Vector3(.12f,.012f,.12f),0,0,(float)a,0);}
        }
        private static Vector3[] Disc(int sides,float r,float y)=>WeaponSculpt.Circle(sides,Math.Max(.001f,r),Math.Max(.001f,r),y);
        // A body of revolution about +y in the vessel's profile convention (the nose's lean when `nose`).
        private static void LatheY(WeaponSculpt s,Func<int,WeaponPaint> paint,float[] r,float[] y,int sides,bool nose)
        {
            var rings=new List<Vector3[]>(r.Length);for(int i=0;i<r.Length;i++){float n=nose?M.Nose(y[i]):0;rings.Add(WeaponSculpt.Circle(sides,r[i],r[i],y[i],(float)Math.PI/sides,0,-n));}
            s.Push(WeaponSculpt.Rx(-(float)Math.PI/2));s.Loft(rings,(i,k)=>paint(i),0,false,false);s.Pop();
        }
        private static Vector3[] MakeSpill(out float[] width)
        {
            // From the front lip down the nose's centreline (phi 0) 1.05 m, wandering a little, narrowing.
            const int n=9;var p=new Vector3[n];width=new float[n];
            for(int i=0;i<n;i++)
            {
                float u=i/(float)(n-1),y=2.72f-1.05f*u,phi=.06f*(float)Math.Sin(u*5.1f)+.02f*u;var f=OnShell(phi,y,.045f+.02f*u*(1-u));
                p[i]=new Vector3(f.M41,f.M42,f.M43)*M.VesselScale;width[i]=(.36f-.2f*u)*M.VesselScale;
            }
            return p;
        }

        // ---------------- the glassy slag crust ----------------
        private struct Plate{public int Zone,Row,Col,Chunk;public float Phi,Y,W,H,T,Turn;public bool Lowest;}
        private static readonly float[] Rows={-.95f,-.5f,.58f,1.0f,1.42f,1.82f};
        private static IEnumerable<Plate> Plates()
        {
            for(int zone=0;zone<4;zone++)
            {
                float centre=zone==0?0:zone==1?-(float)Math.PI/2:zone==2?(float)Math.PI/2:(float)Math.PI;bool side=zone==1||zone==2;const int cols=4;
                for(int row=0;row<Rows.Length;row++)for(int col=0;col<cols;col++)
                {
                    float y=Rows[row]+(float)(Hash(zone,row,col)-.5)*.08f;
                    if(side&&Math.Abs(y)<.6f)continue;if(zone==0&&y<-.45f)continue;if(y>1.9f)continue;
                    float off=row%2==0?0:.5f,phi=centre+((col+.5f+off*.6f)/cols-.5f)*1.5f+(float)(Hash(zone,col,row+7)-.5)*.08f;
                    float w=.82f+(float)Hash(zone,row,col+3)*.3f-.12f*Math.Max(0,y-1.2f),h=.6f+(float)Hash(zone,row+5,col)*.18f,t=.16f+(float)Hash(zone,row,col+9)*.05f;
                    bool lowest=row==0||zone==0&&row==2||side&&row==2;
                    yield return new Plate{Zone=zone,Row=row,Col=col,Chunk=row>=4?0:row>=2?1:2,Phi=phi,Y=y,W=w,H=h,T=t,Turn=(float)(Hash(zone,row+2,col+4)-.5)*.3f,Lowest=lowest};
                }
            }
        }
        private static Vector3 PlateCentre(Plate p){var f=OnShell(p.Phi,p.Y,p.T*.5f);return new Vector3(f.M41,f.M42,f.M43);}
        private static void CrustPlates(WeaponSculpt s,int tier)
        {
            var front=P(SlagHex,.1f);var glass=P(SlagGlassHex,.34f);var sideP=P(SlagSideHex,.12f);var glint=P(GlintHex,.55f);var crack=G(NativeIronworksKinds.SlagCrack,1.7f,.2f);var drip=P(SlagHex,.16f);
            foreach(var pl in Plates())
            {
                // The low tier keeps a third of the plates (it is drawn from 60 m out).
                if(tier==2&&((pl.Col+pl.Row)%3!=0))continue;
                s.Into(Crust(pl.Zone,pl.Chunk));s.Push(WeaponSculpt.S(M.VesselScale,M.VesselScale,M.VesselScale));
                var f=OnShell(pl.Phi,pl.Y,0);s.Push(WeaponSculpt.Rz(pl.Turn)*WeaponSculpt.Rx(-.12f)*f);
                // An irregular hexagon a little wider at the bottom, its corners jittered; the slab out from the shell (z up to T).
                int k=pl.Zone*97+pl.Row*13+pl.Col;float w=pl.W*.56f,h=pl.H*.5f;int corners=tier==2?5:tier==1?6:7;var outline=new Vector3[corners];
                // An irregular slab of slag: uneven corners (a broken, glassy fracture), wider than tall, its lower edge hanging lower.
                for(int i=0;i<corners;i++){double a=Math.PI/2+(i+.5*(Hash(k,i,20)-.5))*2*Math.PI/corners;float j=.72f+.5f*(float)Hash(k,i,22);float rx=w*j,ry=h*(.8f+.4f*(float)Hash(k,i,23));float y=(float)Math.Sin(a)*ry;if(y<0)y*=1.15f;outline[i]=new Vector3((float)Math.Cos(a)*rx,y,0);}
                float bev=tier==2?0:tier==1?.035f:.045f,t=pl.T;
                var rings=new List<Vector3[]>();
                if(bev>0){rings.Add(WeaponSculpt.Inset(outline,bev*.6f,-.02f));rings.Add(WeaponSculpt.Inset(outline,0,t*.45f));rings.Add(WeaponSculpt.Inset(outline,0,t-bev));rings.Add(WeaponSculpt.Inset(outline,bev*1.6f,t));}
                else{rings.Add(WeaponSculpt.Inset(outline,0,-.02f));rings.Add(WeaponSculpt.Inset(outline,0,t));}
                int last=rings.Count-2;s.Loft(rings,(i,e)=>i==last&&bev>0?glass:i==0?P(SlagSideHex,.04f):sideP,0,true,true,P(SlagHex),false,false,front);
                // A conchoidal chip on some plates (a facet catching the light at another angle), at full detail a second, smaller one.
                if(tier<=1&&Hash(k,1,34)<.55){float cx=(float)(Hash(k,2,35)-.5)*w*.8f,cy=(float)(Hash(k,3,36)-.5)*h*.6f;s.Box(glass,new Vector3(cx,cy,t-.01f),new Vector3(w*.4f,h*.32f,.03f),tier==0?.012f:0,(float)(Hash(k,5,41)-.5)*.5f,(float)(Hash(k,6,42)-.5)*.5f,(float)(Hash(k,4,37)-.5));}
                if(tier==0&&Hash(k,7,43)<.35){float cx=(float)(Hash(k,8,44)-.5)*w*.9f,cy=(float)(Hash(k,9,45)-.5)*h*.7f;s.Box(glint,new Vector3(cx,cy,t-.005f),new Vector3(w*.18f,h*.16f,.02f),.006f,(float)(Hash(k,10,46)-.5)*.7f,(float)(Hash(k,11,47)-.5)*.7f,(float)Hash(k,12,48)*3);}
                // The dim seam glowing under the plate's lower edge (the environment's warm colour, never a tell).
                if(tier<=1)s.Box(crack,new Vector3(0,-h*.92f,t*.3f),new Vector3(w*1.5f,.035f,t*.55f),0);
                // Glassy drips off the lowest row: a drop and its thinning tail.
                if(tier<=1&&pl.Lowest&&Hash(k,5,38)<.75)
                {
                    int drips=tier==0?2:1;
                    for(int d=0;d<drips;d++){float dx=(d-(drips-1)*.5f)*w*.8f+(float)(Hash(k,d,39)-.5)*.1f,len=.14f+(float)Hash(k,d,40)*.16f;
                        s.Box(drip,new Vector3(dx,-h-len*.35f,t*.45f),new Vector3(.09f,len*.75f,.08f),tier==0?.02f:0);s.Box(glass,new Vector3(dx,-h-len*.85f,t*.45f),new Vector3(.06f,len*.35f,.055f),tier==0?.015f:0);}
                }
                s.Pop();s.Pop();
            }
        }

        // ---------------- the foreman in his heat suit ----------------
        private static void ForemanSuit(WeaponSculpt s,int tier)
        {
            var silver=P(SilverHex,.24f);var dark=P(SilverDarkHex,.1f);var quilt=P(QuiltHex,.12f);var glove=P(LeatherHex,.04f);var boot=P(RubberHex,.02f);var belt=P(0x2a2622,.05f);var buckle=P(0x8a7a4a,.3f);
            int sd=s.Sides(10);float bv=tier==0?.04f:.03f;
            // Hips and legs (the foreman joint, feet at its origin): boots, quilted trousers, the coat's skirt.
            s.Into(M.Foreman);
            foreach(float x in new[]{-.12f,.12f})
            {
                s.Box(boot,new Vector3(x,.08f,.05f),new Vector3(.16f,.16f,.32f),bv);s.Box(silver,new Vector3(x,.5f,0),new Vector3(.17f,.72f,.19f),bv);
                if(tier<=1)for(int i=0;i<3;i++)s.Box(dark,new Vector3(x,.24f+i*.18f,0),new Vector3(.18f,.025f,.2f),0);
            }
            s.Box(silver,new Vector3(0,.82f,0),new Vector3(.52f,.3f,.34f),bv);if(tier<=1)s.Box(quilt,new Vector3(0,.72f,.172f),new Vector3(.46f,.014f,.01f),0);
            // The chest (waist up): the coat, belt and buckle, quilting, the cape's shoulders, the respirator canister and its lamp.
            s.Into(Chest);var w=Waist;
            s.Box(silver,new Vector3(0,.22f,0),new Vector3(.54f,.52f,.36f),bv);s.Box(belt,new Vector3(0,.0f,0),new Vector3(.56f,.07f,.38f),tier==0?.01f:0);s.Box(buckle,new Vector3(0,0,.19f),new Vector3(.09f,.06f,.02f),0);
            if(tier<=1){for(int i=0;i<3;i++)s.Box(quilt,new Vector3(0,.1f+i*.14f,.182f),new Vector3(.48f,.014f,.01f),0);s.Box(dark,new Vector3(0,.25f,-.182f),new Vector3(.42f,.4f,.008f),0);}
            s.Box(silver,new Vector3(0,.5f,0),new Vector3(.62f,.18f,.4f),bv);s.Box(silver,new Vector3(0,.6f,-.01f),new Vector3(.7f,.07f,.44f),tier==0?.025f:0);
            s.Box(P(0x1c1a18,.05f),new Vector3(.12f,.42f,.21f),new Vector3(.11f,.13f,.08f),tier==0?.02f:0);s.Box(G(NativeIronworksKinds.Cyan,2.4f,.25f),new Vector3(.12f,.5f,.252f),new Vector3(.035f,.02f,.01f),0);
            if(tier==0){s.Push(WeaponSculpt.T(-.15f,.42f,.2f));s.Cylinder(P(0x1c1a18,.05f),.045f,0,.06f,8,.008f);s.Pop();}
            // The head (neck up): the square hood, its cape skirt, the white hard hat and brim, the visor's glass.
            s.Into(Head);
            s.Box(silver,new Vector3(0,.16f,0),new Vector3(.37f,.38f,.37f),bv);s.Box(silver,new Vector3(0,-.02f,-.01f),new Vector3(.44f,.08f,.42f),tier==0?.02f:0);
            s.Box(P(GlassHex,.25f),new Vector3(0,.18f,.186f),new Vector3(.3f,.11f,.012f),0);
            s.Box(P(HatHex,.16f),new Vector3(0,.4f,.01f),new Vector3(.37f,.14f,.41f),bv);s.Box(P(HatHex,.12f),new Vector3(0,.34f,.04f),new Vector3(.47f,.025f,.52f),tier==0?.01f:0);
            if(tier<=1)s.Box(P(0x2a2622),new Vector3(0,.4f,.01f),new Vector3(.38f,.03f,.42f),0);
            if(tier==0){s.Box(P(HatHex,.14f),new Vector3(0,.47f,0),new Vector3(.06f,.03f,.36f),.01f);s.Box(P(0x1c1a18),new Vector3(.12f,.18f,.19f),new Vector3(.03f,.13f,.012f),0);s.Box(P(0x1c1a18),new Vector3(-.12f,.18f,.19f),new Vector3(.03f,.13f,.012f),0);}
            // The visor's cyan strip (its own glow joint: lit in the arrival's flare, out in the death).
            s.Into(Visor);s.Box(G(NativeIronworksKinds.Cyan,3.4f,.2f),new Vector3(0,0,0),new Vector3(.27f,.052f,.008f),0);
            if(tier==0)s.Box(G(NativeIronworksKinds.Cyan,1.6f,.2f),new Vector3(0,-.035f,-.002f),new Vector3(.23f,.012f,.006f),0);
            // The arms: an upper arm (shoulder to elbow along +z) and a forearm with its gauntlet; the shoulder pad on the upper.
            foreach(int side in new[]{-1,1})
            {
                s.Into(side<0?ArmUL:ArmUR);s.Push(WeaponSculpt.Rx(0));s.Cylinder(silver,.085f,-.02f,UpperArm,sd,tier==0?.02f:0);s.Pop();
                s.Box(silver,new Vector3(side*.04f,.04f,.04f),new Vector3(.2f,.15f,.22f),bv);if(tier<=1)s.Box(dark,new Vector3(0,0,UpperArm*.62f),new Vector3(.18f,.18f,.025f),0);
                s.Into(side<0?ArmFL:ArmFR);s.Cylinder(silver,.077f,0,Forearm-.06f,sd,tier==0?.015f:0);
                s.Push(WeaponSculpt.T(0,0,Forearm-.08f));s.Cylinder(glove,.09f,0,.07f,sd,tier==0?.012f:0);s.Pop();s.Box(glove,new Vector3(0,-.01f,Forearm+.02f),new Vector3(.11f,.1f,.12f),tier==0?.02f:0);
            }
        }

        // ---------------- the work lamps (its eyes) ----------------
        private static void Lamps(WeaponSculpt s,int tier)
        {
            var lens=G(NativeIronworksKinds.ColdWhite,5.0f,.4f);var bezel=P(EdgeHex,.08f);var grille=P(IronDarkHex,.04f);int sides=s.Sides(16);
            foreach(int lamp in new[]{LampL,LampR})
            {
                s.Into(lamp);
                // Facing +z: the lens a shallow dome in a bezel ring, the grille's bars across it, the hood over it.
                s.Push(WeaponSculpt.T(0,0,-.02f));s.Cylinder(bezel,.19f,0,.05f,sides,tier==0?.012f:0);s.Pop();
                s.Sphere(lens,new Vector3(0,0,.04f),new Vector3(.155f,.155f,.04f),tier==0?4:3,sides,tier!=0);
                if(tier<=1)for(int i=-1;i<=1;i++)s.Box(grille,new Vector3(0,i*.07f,.085f),new Vector3(.32f,.018f,.02f),0);
                s.Box(P(IronHex,.03f),new Vector3(0,.17f,.0f),new Vector3(.4f,.04f,.16f),tier==0?.01f:0);
            }
        }
        private static void ValveWheel(WeaponSculpt s,int tier)
        {
            if(s==null)return;s.Into(Valve);var green=P(GreenHex,.06f);
            // The concept's wheel moved here (SplitValve); at full detail a brass hub cap.
            if(tier==0){s.Cylinder(P(0x8a6a2a,.25f),.05f,-.03f,.04f,10,.01f);}
        }

        // ---------------- grime (vertex colours) ----------------
        private static void Grime(WeaponMesh mesh)
        {
            foreach(var sec in mesh.Sections)
            {
                int bone=sec.Bone;
                for(int v=0;v<sec.Positions.Count;v++)
                {
                    var e=sec.Emission[v];if(e.X+e.Y+e.Z>1e-4f)continue;var c=sec.Colors[v];var p=sec.Positions[v];float k=1;float warm=0;
                    if(bone==M.Vessel)
                    {
                        // Soot trailing down from the mouth on the front, heat scale low on the shell, darker under the trunnion ring.
                        float y=p.Y/M.VesselScale;k*=Lerp(.72f,1f,Smooth((y+2.2f)/1.4f));
                        if(y>1.6f){float front=Smooth((p.Z+.2f)/1.2f);k*=Lerp(1f,.7f,front*Smooth((y-1.6f)/.9f));}
                        if(Math.Abs(y)<.5f)k*=.9f;warm=.12f*Smooth((-y-.6f)/1.2f);
                        float streak=(float)Math.Abs(Math.Sin(Math.Atan2(p.X,p.Z)*7.3+1.1));if(y>.2f)k*=Lerp(.86f,1f,streak);
                    }
                    else if(bone==M.Yoke)
                    {
                        // Oil and wear: darker toward the girders' undersides, the flanges' top edges kept bright.
                        k*=Lerp(.8f,1f,Smooth((p.Y+.6f)/1.1f));if(p.Z<-2.4f)k*=.9f;
                    }
                    else if(bone>=M.LegBase&&bone<M.JawBase)
                    {
                        int part=(bone-M.LegBase)%M.LegJoints;
                        // Dirt rising up the tibia from the foot, the femur's underside darker, the feet scuffed.
                        if(part==M.Tibia){float u=Math.Max(0,Math.Min(1,p.Z/M.TibiaLength));k*=Lerp(1f,.62f,Smooth((u-.35f)/.65f));warm=.1f*Smooth((u-.6f)/.4f);}
                        else if(part==M.Femur)k*=p.Y<0?.84f:1;
                        else if(part==M.Foot)k*=Lerp(.6f,.9f,Smooth((p.Y+.6f)/.7f));
                    }
                    else if(bone>=M.JawBase)k*=Lerp(.7f,1f,Smooth((p.Y+.9f)/.7f));
                    else if(IsCrust(bone))k*=.94f+.06f*(float)Math.Sin(p.X*9.1f+p.Y*5.3f);
                    if(k!=1||warm>0)sec.Colors[v]=new Vector4(c.X*k*(1+warm),c.Y*k*(1+warm*.35f),c.Z*k*(1-warm*.3f),c.W);
                }
            }
        }

        // ---------------- helpers ----------------
        /// <summary>A frame on the vessel's outer shell (unscaled vessel frame) at azimuth phi (from +z toward +x) and height y,
        /// lifted off it: +z the outward normal, +y up the surface, +x round it (the concept's OnShell).</summary>
        public static Matrix4x4 OnShell(float phi,float y,float lift)
        {
            float s=(float)Math.Sin(phi),c=(float)Math.Cos(phi),r=M.ShellR(y),k=(M.ShellR(y+.04f)-M.ShellR(y-.04f))/.08f;
            var n=Vector3.Normalize(new Vector3(s,-k,c));var t=Vector3.Normalize(new Vector3(k*s,1,k*c));var x=Vector3.Cross(t,n);
            var p=new Vector3(s*r,y,c*r+M.Nose(y))+n*lift;return new Matrix4x4(x.X,x.Y,x.Z,0,t.X,t.Y,t.Z,0,n.X,n.Y,n.Z,0,p.X,p.Y,p.Z,1);
        }
        private static float Lerp(float a,float b,float t)=>a+(b-a)*Math.Max(0,Math.Min(1,t));
        private static float Smooth(float t){t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}
        private static double Hash(int a,int b,int c)=>NativeIronworksKit.Hash(a+0x71,b,c);
    }
}
