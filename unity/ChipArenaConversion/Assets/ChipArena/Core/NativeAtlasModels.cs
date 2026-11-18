using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // ATLAS's cabinet and flaps (Core NativeAtlasBoard), sculpted with the station's kit (NativeWeaponSculpt, NativeDeployModels'
    // conventions). Presentation only and engine-free: the checks build what the player builds. Authored in the board's own
    // frame (x right, y up from the face's bottom, z back from the face; it faces -z, the room) and placed at NativeAtlasBoard.At.
    // Section bones, as the station's: 0 the lit body; 1 the header's and labels' lettering; 2 the in-service lamp (tinted to the
    // selected tileset's accent); 3 the other lines' lamps; -3 the flaps' screen faces (Runtime NativeAtlasScene animates them).
    // A graphite railway cabinet in the station's language: a brass-lipped bezel round a black face, the ATLAS · DEPARTURES
    // header, the column labels and brass rules, a round lamp at each line's start, slatted vents in its cheeks (the bell's
    // sparks come out of them), a riveted steel lip, and two drop rods clamped to the ceiling beam.
    public static class NativeAtlasModels
    {
        public const int Body=0,Lights=1,Accent=2,Dim=3,Screen=-3;
        public const string Id="atlas";
        /// <summary>Triangle budgets (the station stays under 130,000 with the board).</summary>
        public const int CabinetBudget=7500,FlapsBudget=2400;
        private static readonly WeaponPaint Graphite=WeaponPaint.Solid(0x2a2d35,.03f),GraphiteLight=WeaponPaint.Solid(0x3d414c,.04f),GraphiteDark=WeaponPaint.Solid(0x17191e,.02f),
            Brass=WeaponPaint.Solid(0xc0913f,.1f),BrassDark=WeaponPaint.Solid(0x7d5b27,.06f),Steel=WeaponPaint.Solid(0xa3aab6,.06f),SteelDark=WeaponPaint.Solid(0x5c6270,.04f),
            Black=WeaponPaint.Solid(0x07080b),Chrome=WeaponPaint.Solid(0xc6ccd6,.12f);
        private static WeaponPaint Lamp(int hex,float k=1){var c=WeaponPaint.Linear(hex)*k;return new WeaponPaint(c,c,0);}
        private const int Gold=0xffc65a,Ivory=0xffe9c4,Amber=0xff9a2e;
        private static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
        private const float Up=-(float)(Math.PI/2);
        // A vertical cylinder of radius r from y0 to y1 at (x, z).
        private static void Column(WeaponSculpt s,in WeaponPaint c,float r,float x,float z,float y0,float y1,int sides,float bevel=0)
        {s.Push(WeaponSculpt.Rx(Up)*WeaponSculpt.T(x,0,z));s.Cylinder(c,r,y0,y1,sides,bevel);s.Pop();}
        // The station's lettering (NativeDeployArt's strokes, each a bar) on the face plane at depth z (proud toward -z by `depth`),
        // `height` tall, its left edge at x0 (or centred on it), its middle at cy.
        private static void Letters(WeaponSculpt s,in WeaponPaint p,string text,float x0,float cy,float z,float height,float depth,bool centred=false)
        {
            float k=height/NativeDeployArt.GlyphHeight,x=centred?x0-NativeDeployArt.TextWidth(text)*k/2:x0,top=cy+height/2,w=height*.1f;var paint=p;
            foreach(char c in text)
            {
                foreach(var (a0,b0,a1,b1) in NativeDeployArt.Strokes(c))
                {
                    float ax=x+a0*k,ay=top-b0*k,bx=x+a1*k,by=top-b1*k,dx=bx-ax,dy=by-ay,len=(float)Math.Sqrt(dx*dx+dy*dy);
                    s.Box(paint,V((ax+bx)/2,(ay+by)/2,z-depth/2),V(len+w,w,depth),0,0,0,(float)Math.Atan2(dy,dx));
                }
                x+=NativeDeployArt.Advance(c)*k;
            }
        }
        /// <summary>The lettering's width at `height`.</summary>
        public static float LettersWidth(string text,float height)=>NativeDeployArt.TextWidth(text)*height/NativeDeployArt.GlyphHeight;
        /// <summary>A line's vertical middle (board space).</summary>
        public static float LineY(int line)=>line==0?NativeAtlasBoard.LargeTop-NativeAtlasBoard.LargeFlap/2:NativeAtlasBoard.SmallTop-(line-1)*NativeAtlasBoard.SmallPitchY-NativeAtlasBoard.SmallFlap/2;
        public const float LampRadius=.03f;

        /// <summary>The cabinet (the flaps are drawn on their own: Flaps).</summary>
        public static WeaponMesh Cabinet()
        {
            var mesh=new WeaponMesh{Spec="deploy-atlas",Tier=0};var s=new WeaponSculpt(mesh,0);s.Into(Body);
            const float W=NativeAtlasBoard.Width,H=NativeAtlasBoard.Height,D=NativeAtlasBoard.Depth,b=NativeAtlasBoard.Bezel,hw=W/2;
            // the body behind the face, the black field the flaps sit on, the bezel round it with a brass lip, corner plates
            s.Box(Graphite,V(0,H/2,.02f+(D-.02f)/2),V(W,H,D-.02f),.025f);
            s.Box(Black,V(0,H/2,.015f),V(W-2*b+.004f,H-2*b+.004f,.012f));
            foreach(int side in new[]{-1,1})
            {
                s.Box(GraphiteLight,V(0,side<0?b/2:H-b/2,0),V(W,b,.05f),.008f);s.Box(GraphiteLight,V(side*(hw-b/2),H/2,0),V(b,H-2*b,.05f),.008f);
                s.Box(Brass,V(0,side<0?b-.004f:H-b+.004f,-.022f),V(W-2*b+.008f,.008f,.012f));s.Box(Brass,V(side*(hw-b+.004f),H/2,-.022f),V(.008f,H-2*b+.008f,.012f));
                foreach(int v in new[]{-1,1})s.Box(BrassDark,V(side*(hw-.07f),v<0?.07f:H-.07f,-.028f),V(.1f,.1f,.012f),0,0,0,(float)(Math.PI/4));
            }
            // the header plate and its lettering: ATLAS in gold, then · DEPARTURES in ivory
            float hy=(NativeAtlasBoard.HeaderTop+NativeAtlasBoard.HeaderBottom)/2,hx=NativeAtlasBoard.EmblemX;
            s.Box(GraphiteDark,V(0,hy,.002f),V(W-2*b,NativeAtlasBoard.HeaderTop-NativeAtlasBoard.HeaderBottom,.016f));
            s.Box(BrassDark,V(0,NativeAtlasBoard.HeaderBottom+.003f,-.008f),V(W-2*b-.06f,.006f,.008f));
            s.Into(Lights);Letters(s,Lamp(Gold),"ATLAS",hx,hy,-.006f,.096f,.008f);
            float after=hx+LettersWidth("ATLAS",.096f)+.07f;Letters(s,Lamp(Ivory,.9f),"· DEPARTURES",after,hy-.006f,-.006f,.068f,.007f);
            // the column labels over the large line's columns, and the brass rule under them
            var L=NativeAtlasBoard.Board;float ly=(NativeAtlasBoard.LabelBottom+NativeAtlasBoard.HeaderBottom)/2;
            for(int c=0;c<NativeTilesets.Columns.Length;c++)Letters(s,Lamp(Ivory,.55f),NativeTilesets.Columns[c],L.Left[c],ly,.009f,.042f,.005f);
            s.Into(Body);s.Box(Brass,V((NativeAtlasBoard.EmblemX+NativeAtlasBoard.ColumnsEnd)/2,NativeAtlasBoard.LabelBottom-.004f,.005f),V(NativeAtlasBoard.ColumnsEnd-NativeAtlasBoard.EmblemX,.006f,.008f),.002f);
            // the divider under the large line, hairlines between the small ones, the columns' separators
            s.Box(Brass,V(0,(NativeAtlasBoard.LargeTop-NativeAtlasBoard.LargeFlap+NativeAtlasBoard.SmallTop)/2,-.004f),V(W-2*b-.04f,.01f,.008f),.003f);
            for(int line=2;line<NativeTilesets.Lines;line++){float y=NativeAtlasBoard.SmallTop-(line-1)*NativeAtlasBoard.SmallPitchY+.0115f;s.Box(GraphiteDark,V((NativeAtlasBoard.EmblemX+NativeAtlasBoard.ColumnsEnd)/2,y,.004f),V(NativeAtlasBoard.ColumnsEnd-NativeAtlasBoard.EmblemX,.004f,.006f));}
            for(int c=1;c<NativeTilesets.Columns.Length;c++)
            {
                float x=(L.Right[c-1]+L.Left[c])/2,top=NativeAtlasBoard.LargeTop+.006f,bottom=NativeAtlasBoard.SmallTop-(NativeTilesets.Lines-2)*NativeAtlasBoard.SmallPitchY-NativeAtlasBoard.SmallFlap-.006f;
                s.Box(GraphiteDark,V(x,(top+bottom)/2,.0f),V(.022f,top-bottom,.02f));
            }
            // each line's lamp: a chrome bezel and the lamp (line 1 in service, the others dim), and the emblem flap's frame
            for(int line=0;line<NativeTilesets.Lines;line++)
            {
                float y=LineY(line),f=line==0?NativeAtlasBoard.LargeFlap:NativeAtlasBoard.SmallFlap;s.Into(Body);
                s.Cylinder(Chrome,LampRadius+.012f,-.012f,.012f,24,.004f,NativeAtlasBoard.LampX,y);
                s.Into(line==0?Accent:Dim);s.Cylinder(Lamp(line==0?0xffffff:Amber),LampRadius,-.018f,-.012f,24,.003f,NativeAtlasBoard.LampX,y);
                s.Into(Body);float ex=NativeAtlasBoard.EmblemX+f/2;
                foreach(int v in new[]{-1,1}){s.Box(GraphiteDark,V(ex,y+v*(f/2+.007f),-.004f),V(f+.026f,.008f,.01f));s.Box(GraphiteDark,V(ex+v*(f/2+.007f),y,-.004f),V(.008f,f+.022f,.01f));}
            }
            // the cheeks: slatted vents on each end (the bell's sparks come out of line 1's), side plates
            foreach(int side in new[]{-1,1})
            {
                float x=side*(hw+.004f);s.Box(GraphiteDark,V(x,H/2,D/2),V(.008f,H-.12f,D-.08f),.002f);
                for(int k=0;k<9;k++)s.Box(GraphiteLight,V(x+side*.008f,.18f+k*.095f,D/2),V(.012f,.022f,D-.14f));
            }
            // the steel lip along the bottom with its rivets, and the maker's band along the top
            s.Box(SteelDark,V(0,-.008f,.02f),V(W-.06f,.02f,.06f),.006f);
            for(int k=0;k<14;k++){float x=-hw+.2f+k*(W-.4f)/13;s.Cylinder(Steel,.011f,-.012f,-.004f,8,0,x,b/2);}
            s.Box(SteelDark,V(0,H+.008f,D/2),V(W-.2f,.016f,D-.06f),.004f);
            // two drop rods to the ceiling beam's bottom flange: a mounting bracket on the cabinet, the rod, a clamp on the flange
            float rodTop=NativeAtlasBoard.BeamBottom-NativeAtlasBoard.At.Y,rodZ=NativeAtlasBoard.BeamZ-NativeAtlasBoard.At.Z-.1f;
            foreach(int side in new[]{-1,1})
            {
                float x=side*NativeAtlasBoard.RodX;
                s.Box(SteelDark,V(x,H+.03f,rodZ),V(.16f,.04f,.12f),.006f);Column(s,Steel,.022f,x,rodZ,H+.04f,rodTop-.02f,12,.003f);
                Column(s,SteelDark,.035f,x,rodZ,H+.05f,H+.09f,12,.004f);Column(s,SteelDark,.035f,x,rodZ,rodTop-.07f,rodTop-.03f,12,.004f);
                s.Box(SteelDark,V(x,rodTop-.012f,rodZ+.05f),V(.18f,.024f,.2f),.005f);
            }
            return mesh;
        }

        // ---- the flaps ---------------------------------------------------------------------------------------------------------
        /// <summary>The flaps' plane (a little proud of the black field) and the gap each flap half keeps from its hinge.</summary>
        public const float FlapZ=-.008f,FlapLead=.003f;
        public const int CellVertices=12,CellIndices=18;
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        /// <summary>
        /// One cell's three faces for a pose, written at `at` (CellVertices entries): its upper half (Next's top), its lower half
        /// (Prev's bottom) and the flap falling between them about the middle hinge toward the room: Prev's top on its face until
        /// it passes the horizontal, then Next's bottom on its back; at rest the flap folds flat on the hinge (no area). `shade`
        /// darkens the flap as it turns away from the front.
        /// </summary>
        public static void Cell(in NativeAtlasBoard.Cell c,in NativeAtlasBoard.Pose p,Vector3[] pos,Vector2[] uv,float[] shade,int at)
        {
            bool emblem=c.Kind==NativeAtlasBoard.Kind.Emblem;float g=c.Large?.0016f:.001f,mid=(c.Y0+c.Y1)/2,z=FlapZ;
            NativeAtlasArt.Rect(p.Next,emblem,out float nu0,out float nv0,out float nu1,out float nv1);NativeAtlasArt.Rect(p.Prev,emblem,out float pu0,out float pv0,out float pu1,out float pv1);
            float nm=(nv0+nv1)/2,pm=(pv0+pv1)/2;
            // the upper half: Next's top
            pos[at]=V(c.X0,mid+g,z);pos[at+1]=V(c.X1,mid+g,z);pos[at+2]=V(c.X1,c.Y1,z);pos[at+3]=V(c.X0,c.Y1,z);
            uv[at]=new Vector2(nu0,nm);uv[at+1]=new Vector2(nu1,nm);uv[at+2]=new Vector2(nu1,nv1);uv[at+3]=new Vector2(nu0,nv1);
            // the lower half: Prev's bottom (Next's when at rest)
            pos[at+4]=V(c.X0,c.Y0,z);pos[at+5]=V(c.X1,c.Y0,z);pos[at+6]=V(c.X1,mid-g,z);pos[at+7]=V(c.X0,mid-g,z);
            uv[at+4]=new Vector2(pu0,pv0);uv[at+5]=new Vector2(pu1,pv0);uv[at+6]=new Vector2(pu1,pm);uv[at+7]=new Vector2(pu0,pm);
            for(int i=0;i<8;i++)shade[at+i]=1;
            // the falling flap
            if(p.Phase<0)
            {
                for(int i=8;i<12;i++){pos[at+i]=V(i==9||i==10?c.X1:c.X0,mid,z-FlapLead);uv[at+i]=new Vector2(nu0,nm);shade[at+i]=1;}
                return;
            }
            float th=NativeAtlasBoard.FlapAngle(p.Phase),h=c.Y1-mid-g,cs=(float)Math.Cos(th),sn=(float)Math.Sin(th);float zz=z-FlapLead;
            var tip=new Vector2(mid+g*Math.Sign(cs)+h*cs,zz-h*sn);
            pos[at+8]=V(c.X0,mid+g*Math.Sign(cs),zz);pos[at+9]=V(c.X1,mid+g*Math.Sign(cs),zz);pos[at+10]=V(c.X1,tip.X,tip.Y);pos[at+11]=V(c.X0,tip.X,tip.Y);
            if(cs>=0){uv[at+8]=new Vector2(pu0,pm);uv[at+9]=new Vector2(pu1,pm);uv[at+10]=new Vector2(pu1,pv1);uv[at+11]=new Vector2(pu0,pv1);}
            else{uv[at+8]=new Vector2(nu0,nm);uv[at+9]=new Vector2(nu1,nm);uv[at+10]=new Vector2(nu1,nv0);uv[at+11]=new Vector2(nu0,nv0);}
            float k=.42f+.58f*Math.Abs(cs);for(int i=8;i<12;i++)shade[at+i]=k;
            // the upper half darkens a little under the flap as it starts to fall
            float under=.78f+.22f*Clamp01(th/(float)(Math.PI/2));for(int i=0;i<4;i++)shade[at+i]=under;
        }
        /// <summary>A cell's triangles (three quads, front facing -z; the flap is drawn two-sided), offset to its vertices.</summary>
        public static void Indices(int[] into,int at,int vertex)
        {
            for(int q=0;q<3;q++){int v=vertex+q*4,i=at+q*6;into[i]=v;into[i+1]=v+2;into[i+2]=v+1;into[i+3]=v;into[i+4]=v+3;into[i+5]=v+2;}
        }
        /// <summary>The flaps of every line at rest on a selection's text (the checks' budget and the bounds; the Runtime builds
        /// its meshes from Cell and animates them).</summary>
        public static WeaponMesh Flaps(NativeTilesets.Tileset selected=null)
        {
            var mesh=new WeaponMesh{Spec="deploy-atlas-flaps",Tier=0};var sec=mesh.Section(Screen,WeaponStretch.None);var L=NativeAtlasBoard.Board;
            var lines=NativeTilesets.Board(selected??NativeTilesets.Selected(null),null);
            var pos=new Vector3[CellVertices];var uv=new Vector2[CellVertices];var shade=new float[CellVertices];var idx=new int[CellIndices];
            for(int line=0;line<NativeTilesets.Lines;line++)
            {
                int k=NativeAtlasBoard.Kinds(lines[line]);var targets=line==0?L.Large[k]:L.Small[k];
                for(int i=0;i<targets.Length;i++)
                {
                    var c=L.Cells[L.LineStart[line]+i];Cell(c,new NativeAtlasBoard.Pose{Prev=targets[i],Next=targets[i],Phase=-1},pos,uv,shade,0);
                    int v0=sec.Positions.Count;
                    for(int j=0;j<CellVertices;j++){sec.Positions.Add(pos[j]);sec.Normals.Add(-Vector3.UnitZ);sec.Colors.Add(new Vector4(shade[j],shade[j],shade[j],1));sec.Emission.Add(Vector4.Zero);sec.Data.Add(uv[j]);}
                    Indices(idx,0,v0);sec.Indices.AddRange(idx);
                }
            }
            return mesh;
        }
    }
}
