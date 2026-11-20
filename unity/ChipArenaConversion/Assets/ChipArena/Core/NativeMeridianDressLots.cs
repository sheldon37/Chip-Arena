using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    public static partial class NativeMeridianDress
    {
        /// <summary>A facade bay where a side-street lamp may hang: the wall point at the floor, its normal, its floor and its block.</summary>
        private struct Mount{public V3 At,N;public float Floor;public int Block;}
        /// <summary>A seeded variant of a key: key + "." + (0..count-1), by a place's hash.</summary>
        private static string V(string key,int count,uint h)=>key+"."+(Mix(h)%(uint)count);

        // ------------------------------------------------------------------------------------------------------------------
        // A lot: its facades bay by bay and storey by storey (bible 2.3, 5.1-5.4), its cornice, its roof, its pavement
        // ------------------------------------------------------------------------------------------------------------------
        private static void DressLot(Builder b,int bi)
        {
            var blk=b.Blocks[bi];int o=bi*6;var B=b.S.B;float bottom=B[o+2],top=B[o+3];
            string sheet=b.Lots[blk.id];char style=sheet[0];uint lotSeed=Mix(b.Seed,Hash(blk.id));
            int storeys=0;while(StoreyFloor(bottom,storeys)<top-.01f)storeys++;
            bool brick=style=='w'||style=='h'||style=='t'||(style=='r'?Unit(lotSeed)<.35f:style=='s'?Unit(lotSeed)<.5f:style=='m'?Unit(lotSeed)<.3f:false);
            string wallVar=brick?"0":"1";
            string trade=Trades[Mix(lotSeed,7)%(uint)Trades.Length],trade2=Trades[Mix(lotSeed,11)%(uint)Trades.Length];
            int wallFin=style=='c'?NativeMeridianSurfaces.StoneCivic:style=='w'||style=='h'?NativeMeridianSurfaces.BrickBuff:brick?NativeMeridianSurfaces.BrickStock:NativeMeridianSurfaces.Render;
            string topKey=style=='t'?"t.ten":style=='w'||style=='h'?"t.ware":style=='c'?"t.civic":brick?"t.brick":"t.render";
            // the longest face is the lot's front; a row's short ends are its gables
            float sx=B[o+1]-B[o],sz=B[o+5]-B[o+4];
            for(int f=0;f<6;f++)
            {
                if(f==2||f==3||OutOfBounds(b,bi,f))continue;
                FaceFrame(B,o,f,out var fo,out var fu,out var fv);float L=fu.Length;if(L<.5f)continue;var uh=fu/L;var N=NormalOf(f);
                int n=Math.Max(1,(int)Math.Round(L/Bay));float w=L/n;uint faceSeed=Mix(lotSeed,(uint)f+101);
                bool gable=(style=='r'||style=='v')&&L<12.6f&&L<Math.Max(sx,sz)-1;bool blind=gable||(style=='w'||style=='h')&&L<Math.Max(sx,sz)-1;
                bool ghostDone=false;
                for(int i=0;i<n;i++)
                {
                    float ua=i*w,ub=ua+w;var col=fo+uh*(ua+w*.5f);uint baySeed=Mix(faceSeed,(uint)i);
                    // the column's seen samples every 0.5 m (a slab over a passage or an arcade's roof may hide a band of it)
                    int ns=Math.Max(1,(int)Math.Ceiling((top-bottom)/.5f));var seen=new bool[ns];bool any=false;int lowest=-1;
                    for(int k=0;k<ns;k++){float y=bottom+(k+.5f)*(top-bottom)/ns;seen[k]=!b.S.Inside(col.x+N.x*.08f,y,col.z+N.z*.08f,bi);if(seen[k]){any=true;if(lowest<0)lowest=k;}}
                    if(!any)continue;float baseY=lowest<=0?bottom:bottom+lowest*(top-bottom)/ns;
                    var front=col+N*.6f;float floorFront=b.S.Floor(front.x,front.z,baseY+.3f);if(floorFront<-1e5f)floorFront=baseY-20;
                    bool street=lowest==0&&Math.Abs(floorFront-bottom)<.05f;bool topSeen=seen[ns-1];
                    int door=style=='t'?(i%4==1?1:0):(style=='r'||style=='v')?(i%2==0?1:0):(style=='s'?(i%4==3?1:0):0);
                    bool oriel=(style=='r'||style=='v')&&!blind&&i%4==1&&Unit(Mix(faceSeed,77))<.6f;
                    for(int k=0;k<storeys;k++)
                    {
                        float fy=StoreyFloor(bottom,k),fh=StoreyHeight(k);if(fy+fh>top+.01f)fh=top-fy;
                        // the storey's share seen: half or more draws its module (a hidden part lies inside the block hiding it),
                        // less draws plain wall over just the seen samples
                        int s0=Math.Max(0,(int)Math.Floor((fy-bottom)/(top-bottom)*ns+.01f)),s1=Math.Min(ns,(int)Math.Ceiling((fy+fh-bottom)/(top-bottom)*ns-.01f));
                        int got=0;for(int q=s0;q<s1;q++)if(seen[q])got++;if(got==0)continue;
                        var at=new V3(col.x,fy,col.z);
                        float floorK=floorFront;{int lo=s0;while(lo<s1&&!seen[lo])lo++;float yl=bottom+lo*(top-bottom)/ns;var ff=b.S.Floor(front.x,front.z,yl+.3f);floorK=ff<-1e5f?yl-20:ff;}
                        if(got*2<s1-s0)
                        {
                            for(int q=s0;q<s1;q++)
                            {
                                if(!seen[q])continue;int r=q;while(r+1<s1&&seen[r+1])r++;float y0=bottom+q*(top-bottom)/ns,y1=bottom+(r+1)*(top-bottom)/ns;
                                b.Quad(fo+uh*ua+new V3(0,y0-fo.y,0),uh*w,new V3(0,y1-y0,0),wallFin,0,bi,floorK);q=r;
                            }
                            continue;
                        }
                        floorFront=floorK;
                        bool hug=k==0||fy<floorFront+Hug-.01f;string key;
                        if(k==0)
                        {
                            if(blind)key=V("g.blind",3,baySeed);
                            else switch(style)
                            {
                                case 't':key=door==1?"g.entry":V("g.flats",2,baySeed);break;
                                case 'w':case 'h':key=i%3==1?"g.load":"g.ware";break;
                                case 'c':key=V("g.civic",2,baySeed);break;
                                case 's':key=door==1?V("g.door."+wallVar,3,baySeed):Unit(Mix(baySeed,3))<.2f?"g.shutter":"g.shop."+(i<n/2?trade:trade2);break;
                                case 'm':key=Unit(Mix(baySeed,3))<.15f?"g.shutter":"g.shop."+MarketTrades[Mix(lotSeed,(uint)(i/3))%(uint)MarketTrades.Length];break;
                                default:key=door==1?V("g.door."+wallVar,3,baySeed):V("g.window."+wallVar,3,baySeed);break;
                            }
                            b.Add(key,at,uh,N,w/Bay,fh/GroundStorey,bi,f,ua,ub,fy,fy+fh,floorFront,true,false,false,0);
                            continue;
                        }
                        if(blind)key=Unit(Mix(baySeed,(uint)k))<.45f?V("u.blind",2,Mix(baySeed,(uint)k)):"u.plain";
                        else switch(style)
                        {
                            case 't':key=door==1?"u.stair":V("u.ten",3,Mix(baySeed,(uint)k));break;
                            case 'w':case 'h':key=V("u.ware",2,Mix(baySeed,(uint)k));break;
                            case 'c':key=V("u.civic",2,Mix(baySeed,(uint)k));break;
                            default:key=oriel&&fy-.8f>=floorFront+Hug&&k<=2?"u.oriel":V("u.win."+wallVar,3,Mix(baySeed,(uint)k));break;
                        }
                        if(hug&&key!="u.oriel")key+=".h";
                        b.Add(key,at,uh,N,w/Bay,fh/Storey,bi,f,ua,ub,fy,fy+fh,floorFront,hug,false,false,UpperLayers);
                    }
                    // the cornice under the roof line (overhead: only where it hangs 3.1 m or more over the floor in front)
                    // (and not where a block stands against the face at the roof line, a landing or a neighbour's roof: the cornice would
                    // lie in its top)
                    var lip=col+N*.3f;bool clear=!b.S.Inside(lip.x,top-.05f,lip.z,bi)&&!b.S.Inside(lip.x,top-.6f,lip.z,bi)&&Math.Abs(b.S.Floor(lip.x,lip.z,top+.05f)-top)>.02f;
                    if(top-floorFront>=Hug+1f&&topSeen&&clear)b.Add(topKey,new V3(col.x,top,col.z),uh,N,w/Bay,1,bi,f,ua,ub,top-1,top,floorFront,false,false,false,CorniceLayers+(f<2?1:0));
                    // a ghost sign on a blind face (board 3): one per face, over three bays of its first upper storeys
                    if(blind&&!ghostDone&&n>=3&&i==n/2&&storeys>=3&&baseY<StoreyFloor(bottom,1)+.1f)
                    {
                        ghostDone=true;float gy=StoreyFloor(bottom,1)+.4f;
                        b.Add(V("ghost",4,faceSeed),new V3(col.x,gy,col.z),uh,N,1,1,bi,f,ua-w,ub+w,gy,gy+4.5f,floorFront,gy<floorFront+Hug,true);
                    }
                    if(street)
                    {
                        // the pavement in front (2 m of slabs and a granite kerb, flat paint on the street) where the street is open
                        bool open=true;foreach(float d in new[]{.5f,1.5f,2.2f}){var q=col+N*d;if(b.S.Inside(q.x,bottom+.1f,q.z,-1)||Math.Abs(b.S.Floor(q.x,q.z,bottom+.1f)-bottom)>.05f){open=false;break;}}
                        if(open)
                        {
                            // each face's pavement its own layer (1..4) and kerb (5..8): where two streets' pavements meet or a narrow
                            // lane's two pavements overlap, neither fights the other
                            int layer=f==0?1:f==1?2:f==4?3:4;var po=fo+uh*ua;po.y=bottom;
                            b.QuadUp(po,uh*w,N*2.0f,NativeMeridianSurfaces.Paving,layer,-1,bottom);
                            b.QuadUp(po+N*2.0f,uh*w,N*.28f,NativeMeridianSurfaces.Kerb,layer+4,-1,bottom);
                            if(i%2==0&&L>=9)b.Mounts.Add(new Mount{At=new V3(col.x,bottom,col.z),N=N,Floor=bottom,Block=bi});
                        }
                    }
                }
            }
            // the roof: tar felt with its coping round every edge that stands over the street, and its hatch
            Surface(b,bi,2,NativeMeridianSurfaces.TarFelt,0,3f);
            for(int f=0;f<6;f++)
            {
                if(f==2||f==3)continue;var N=NormalOf(f);FaceFrame(B,o,f,out var fo,out var fu,out _);float L=fu.Length;if(L<.5f)continue;var uh=fu/L;
                int n=Math.Max(1,(int)Math.Round(L/Bay));float w=L/n;
                // the copings butt at the corners: the x-facing edges' run stops .35 m short of each end (the z-facing edges' own)
                float s0=f<2?CopingWidth:0,s1=f<2?CopingWidth:0;
                for(int i=0;i<n;i++)
                {
                    var c=fo+uh*(w*(i+.5f));if(b.S.Inside(c.x+N.x*.1f,top-.3f,c.z+N.z*.1f,bi)||b.S.Inside(c.x-N.x*.2f,top+.05f,c.z-N.z*.2f,bi))continue;
                    float a0=w*i+(i==0?s0:0),a1=w*(i+1)-(i==n-1?s1:0);if(a1-a0<.05f)continue;
                    // on the roof: from the edge inward .35 m, along the edge (u), wound to face up
                    b.QuadUp(new V3(fo.x,top,fo.z)+uh*a0,uh*(a1-a0),N*-CopingWidth,NativeMeridianSurfaces.Coping,1,bi,top);
                }
            }
            foreach(var h in b.In.Hatches??Array.Empty<V3>())
                if(h.x>B[o]&&h.x<B[o+1]&&h.z>B[o+4]&&h.z<B[o+5]&&Math.Abs(h.y-top)<.2f)
                    b.Add("roof.hatch",new V3(h.x,top,h.z),new V3(1,0,0),new V3(0,0,1),1,1,bi,2,0,0,0,0,top,true,true);
        }

        // ------------------------------------------------------------------------------------------------------------------
        // Lamps (R4, bible 6.2): the routes' chain from the plan's lamp points on the nearest wall or a span, then the side streets
        // ------------------------------------------------------------------------------------------------------------------
        /// <summary>A roof coping's width on the roof (m).</summary>
        public const float CopingWidth=.35f;
        private static readonly V3[] Dirs={new V3(1,0,0),new V3(-1,0,0),new V3(0,0,1),new V3(0,0,-1)};
        /// <summary>The nearest wall from a floor point in a direction at a height over it (within reach): distance, the wall's block.</summary>
        private static bool Wall(Builder b,V3 p,V3 d,float rise,float reach,out float t,out int block)
        {
            t=0;block=-1;
            for(float s=.25f;s<=reach;s+=.25f)
            {
                var q=p+d*s;if(!b.S.Inside(q.x,p.y+rise,q.z,-1))continue;
                // the block it struck, and the face of it toward p
                for(int i=0;i<b.Blocks.Length;i++){int o=i*6;var B=b.S.B;if(q.x>B[o]&&q.x<B[o+1]&&p.y+rise>B[o+2]&&p.y+rise<B[o+3]&&q.z>B[o+4]&&q.z<B[o+5]){block=i;
                    t=d.x>0?B[o]-p.x:d.x<0?p.x-B[o+1]:d.z>0?B[o+4]-p.z:p.z-B[o+5];break;}}
                // a landmark's block is the landmark's to light: nothing of the dress hangs on it; and the wall stands 0.4 m either side
                // of the mount in the face (a mount on a corner would hang half off it, or into the block round the corner)
                if(block<0||t<0||b.Left[block])return false;
                var side=new V3(d.z,0,-d.x)*.4f;
                foreach(var e in new[]{q+side,q-side}){int k=BlockAt(b,e.x,p.y+rise,e.z);if(k<0||b.Left[k])return false;}
                return true;
            }
            return false;
        }
        private static int BlockAt(Builder b,float x,float y,float z)
        {
            var B=b.S.B;for(int i=0;i<b.Blocks.Length;i++){int o=i*6;if(x>B[o]&&x<B[o+1]&&y>B[o+2]&&y<B[o+3]&&z>B[o+4]&&z<B[o+5])return i;}
            return -1;
        }
        private static void Lamps(Builder b)
        {
            var P=b.P;var pts=SheetLamps(b.In.Layout)??Array.Empty<float>();uint runSeed=Mix(b.Seed,b.In.Run);
            for(int k=0;k+2<pts.Length;k+=3)
            {
                var p=new V3(pts[k],pts[k+1],pts[k+2]);int route=NearestRoute(b,p);uint h=Mix(runSeed,(uint)k);
                int state=Unit(Mix(h,5))<FlickerShare?Flicker:Working;
                if(!SwanAt(b,p,state,true,route,h)&&!SpanAt(b,p,state,true,route,h)&&!FlushAt(b,p,state,true,route,h))P.Unmounted++;
            }
            // the side streets: a lamp every 18 m along the street faces, clear of the route chain by 9 m, by the owner's rule
            var list=b.Mounts;
            V3 last=new V3(1e6f,0,0);
            foreach(var m in list)
            {
                if((m.At-last).Length<SideSpacing*.5f)continue;
                bool near=false;foreach(var l in P.Lamps){var d=l.Mount-m.At;if(d.x*d.x+d.z*d.z<81&&Math.Abs(d.y-SwanPlate)<6){near=true;break;}}
                if(near)continue;
                var q=m.At+m.N*3f;if(b.S.Inside(q.x,m.Floor+1,q.z,-1))continue;
                // where the lamps hang is the layout's (fixed); which are dead or flicker is the run's (bible 11.2)
                uint place=Mix(b.Seed,Hash(m.At.x.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)+","+m.At.z.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)));
                if(Unit(Mix(place,9))>.62f){last=m.At;continue;}
                uint h=Mix(runSeed,place);float r=Unit(Mix(h,5));int state=r<DeadShare?Dead:r<DeadShare+FlickerShare?Flicker:Working;
                PlaceSwan(b,m.At+new V3(0,SwanPlate,0),m.N,m.Floor,state,false,-1,h);last=m.At;
            }
        }
        private static int NearestRoute(Builder b,V3 p)
        {
            int best=-1;float bd=float.MaxValue;var routes=b.In.Routes??Array.Empty<Route>();
            for(int r=0;r<routes.Length;r++){var pts=routes[r].Points;for(int i=0;i+1<pts.Length;i++){float d=SegDist(p,pts[i],pts[i+1]);if(d<bd){bd=d;best=r;}}}
            return best;
        }
        private static float SegDist(V3 p,V3 a,V3 c){float dx=c.x-a.x,dz=c.z-a.z,l=dx*dx+dz*dz;float t=l<1e-6f?0:Math.Max(0,Math.Min(1,((p.x-a.x)*dx+(p.z-a.z)*dz)/l));float ex=a.x+dx*t-p.x,ez=a.z+dz*t-p.z;return (float)Math.Sqrt(ex*ex+ez*ez);}
        private static bool SwanAt(Builder b,V3 p,int state,bool prot,int route,uint h)
        {
            float best=float.MaxValue;int bestDir=-1;
            for(int d=0;d<4;d++)if(Wall(b,p,Dirs[d],SwanPlate,9f,out float t,out int blk)&&t<best)
            {
                // the wall must stand a metre over the plate, and the floor at its foot be the lamp's floor
                var w=p+Dirs[d]*(t-.1f);if(!b.S.Inside(w.x+Dirs[d].x*.2f,p.y+SwanPlate+1,w.z+Dirs[d].z*.2f,-1))continue;
                best=t;bestDir=d;
            }
            if(bestDir<0)return false;var dir=Dirs[bestDir];var at=p+dir*best+new V3(0,SwanPlate,0);
            return PlaceSwan(b,at,dir*-1,p.y,state,prot,route,h);
        }
        private static bool PlaceSwan(Builder b,V3 mount,V3 n,float floor,int state,bool prot,int route,uint h)
        {
            // the head hangs 3.1 m or more over whatever floor lies under it (a step, a raised pavement): the bracket rises with it
            // when its wall stands tall enough, else no lamp hangs here
            var bowl=mount+n*SwanReach-new V3(0,SwanDrop,0);float under=float.MinValue;
            for(int tries=0;;tries++)
            {
                under=b.S.Floor(bowl.x,bowl.z,bowl.y-.1f);if(under<-1e5f||bowl.y-under>=Hug+.2f)break;if(tries==3)return false;
                float lift=Hug+.2f-(bowl.y-under);var back=mount-n*.2f;if(!b.S.Inside(back.x,mount.y+lift+.6f,back.z,-1))return false;
                mount=mount+new V3(0,lift,0);bowl=bowl+new V3(0,lift,0);
            }
            if(b.S.Inside(bowl.x,bowl.y,bowl.z,-1))return false;
            var along=Cross(new V3(0,1,0),n);string key=state==Dead?"lamp.swan.dead":prot?"lamp.swan.route":"lamp.swan";
            var l=new Lamp{Mount=mount,Bowl=bowl,Kind=Swan,State=state,Protected=prot,Route=route,Seed=Unit(Mix(h,13)),Radius=SwanPool};
            l.Pool=new V3(l.Bowl.x,under<-1e5f?floor:under,l.Bowl.z);
            l.Placement=b.P.Placements.Count;b.Add(key,mount,along,n,1,1,-1,-1,0,0,mount.y-.5f,mount.y+.5f,Math.Max(floor,under),false,false,state==Flicker);
            b.P.Lamps.Add(l);return true;
        }
        /// <summary>A flush lamp set in a low wall or a parapet (bible 6.2's flush mount: its lens flush with the face, the body within
        /// 0.25 m inside it), where no wall stands tall enough for a bracket: a stair's cheek, a terrace's step, a bridge's parapet.</summary>
        private static bool FlushAt(Builder b,V3 p,int state,bool prot,int route,uint h)
        {
            float best=float.MaxValue;int bestDir=-1;float rise=0;
            for(int d=0;d<4;d++)
            {
                if(!Wall(b,p,Dirs[d],.5f,6f,out float t,out _)||t>=best)continue;
                // the face's height over the lamp's floor: a lens at most 2.4 m up, 0.4 m under its top
                var w=p+Dirs[d]*(t+.1f);float top=.5f;while(top<6&&b.S.Inside(w.x,p.y+top+.25f,w.z,-1))top+=.25f;
                if(top<.9f)continue;best=t;bestDir=d;rise=Math.Min(2.4f,top-.4f);
            }
            if(bestDir<0)return false;var n=Dirs[bestDir]*-1;var at=p+Dirs[bestDir]*best+new V3(0,rise,0);var along=Cross(new V3(0,1,0),n);
            string key=state==Dead?"lamp.flush.dead":prot?"lamp.flush.route":"lamp.flush";
            var l=new Lamp{Mount=at,Kind=Flush,State=state,Protected=prot,Route=route,Seed=Unit(Mix(h,13)),Radius=FlushPool};
            l.Bowl=at+n*.12f;var c=at+n*1.6f;float fl=b.S.Floor(c.x,c.z,at.y);l.Pool=new V3(c.x,fl<-1e5f?p.y:fl,c.z);
            l.Placement=b.P.Placements.Count;b.Add(key,at,along,n,1,1,-1,-1,0,0,at.y-.2f,at.y+.2f,p.y,true,false,state==Flicker);b.P.Lamps.Add(l);
            return true;
        }
        private static bool SpanAt(Builder b,V3 p,int state,bool prot,int route,uint h)
        {
            // across the route: walls both sides within 10 m of the point at the wire's height, at most 20 m apart
            for(int axis=0;axis<2;axis++)
            {
                var d=axis==0?new V3(1,0,0):new V3(0,0,1);
                if(!Wall(b,p,d,SpanHeight,16,out float ta,out _)||!Wall(b,p,d*-1,SpanHeight,16,out float tb,out _))continue;
                if(ta+tb>28||ta+tb<4)continue;
                var a=p+d*ta+new V3(0,SpanHeight,0);var c=p-d*tb+new V3(0,SpanHeight,0);var mid=(a+c)*.5f;float span=ta+tb;
                // the pendant hangs 3.1 m or more over the floor under it
                float under=b.S.Floor(mid.x,mid.z,mid.y-1);if(under>-1e5f&&mid.y-PendantDrop-under<Hug+.2f)continue;
                var n=Cross(d,new V3(0,1,0));
                // the wire (a unit span along its x) and the pendant hung at its middle
                b.Add("lamp.wire",c,d,n,span,1,-1,-1,0,span,mid.y-.1f,mid.y,p.y,false);
                string key=state==Dead?"lamp.pendant.dead":prot?"lamp.pendant.route":"lamp.pendant";
                var l=new Lamp{Mount=mid,Kind=Pendant,State=state,Protected=prot,Route=route,Seed=Unit(Mix(h,13)),Radius=PendantPool};
                l.Bowl=mid-new V3(0,PendantDrop,0);float fl=b.S.Floor(mid.x,mid.z,mid.y-1);l.Pool=new V3(mid.x,fl<-1e5f?p.y:fl,mid.z);
                l.Placement=b.P.Placements.Count;b.Add(key,mid,d,n,1,1,-1,-1,0,0,mid.y-1,mid.y,p.y,false,false,state==Flicker);b.P.Lamps.Add(l);
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------------------------------------------------------
        // The routes' marks: SANCTUARY and the walked metres home on the road, the street-name plates, the 6:47 clocks, the
        // sirens and the puddles (bible 6.7, R11 wayfinding)
        // ------------------------------------------------------------------------------------------------------------------
        /// <summary>The road paint's advance per glyph (m) and the line gap.</summary>
        public const float GlyphAdvance=.62f,GlyphLine=1.25f;
        private static void Marks(Builder b)
        {
            var routes=b.In.Routes??Array.Empty<Route>();var streets=SheetStreets(b.In.Layout);
            for(int r=0;r<routes.Length;r++)
            {
                var R=routes[r];if(R.Points==null||R.Points.Length<2)continue;float station=0;float nextMark=18,nextPuddle=9;bool plate=false,clock=false;
                for(int i=0;i+1<R.Points.Length;i++)
                {
                    var a=R.Points[i];var c=R.Points[i+1];float dx=c.x-a.x,dz=c.z-a.z,len=(float)Math.Sqrt(dx*dx+dz*dz);if(len<.01f)continue;
                    var dir=new V3(dx/len,0,dz/len);string kind=R.Kinds!=null&&i<R.Kinds.Length?R.Kinds[i]:"flat";float width=R.Widths!=null&&i<R.Widths.Length?R.Widths[i]:10;
                    bool level=Math.Abs(c.y-a.y)<.01f&&kind!="stair"&&kind!="ramp";
                    while(nextMark<station+len)
                    {
                        float t=nextMark-station;var p=a+dir*t;nextMark+=60;if(!level||t<6||len-t<6)continue;
                        if(!OnFloor(b,p))continue;Sanctuary(b,p,dir*-1,(int)Math.Round(nextMark-60+25));
                    }
                    while(nextPuddle<station+len)
                    {
                        float t=nextPuddle-station;uint h=Mix(Mix(b.Seed,(uint)r),(uint)(nextPuddle*10));nextPuddle+=11+Unit(h)*9;if(!level)continue;
                        var across=new V3(dir.z,0,-dir.x);var p=a+dir*t+across*((Unit(Mix(h,3))-.5f)*Math.Max(0,width-3));
                        if(!OnFloor(b,p))continue;
                        b.Add(V("puddle",3,h),p+new V3(0,.004f,0),across,dir,1,1,-1,-1,0,0,p.y,p.y,p.y,true,true);
                    }
                    // the street's name on its first wall, the 6:47 clock near its start
                    if(!plate&&streets!=null&&r<streets.Length&&level&&station+len>12)
                    {
                        var p=a+dir*Math.Min(len*.5f,Math.Max(2,12-station));if(PlateAt(b,p,dir,"plate."+b.In.Layout+"."+r,3.4f,.6f))plate=true;
                    }
                    if(!clock&&level&&station+len>30)
                    {
                        var p=a+dir*Math.Min(len*.5f,Math.Max(2,30-station));if(PlateAt(b,p,dir,"clock.wall",4.6f,1.5f))clock=true;
                    }
                    station+=len;
                }
                // a Civil Defence siren on a tall wall half way home
                float total=0;for(int i=0;i+1<R.Points.Length;i++){var e=R.Points[i+1]-R.Points[i];total+=(float)Math.Sqrt(e.x*e.x+e.z*e.z);}
                float half=total*.5f,s=0;
                for(int i=0;i+1<R.Points.Length;i++)
                {
                    var a=R.Points[i];var e=R.Points[i+1]-a;float len=(float)Math.Sqrt(e.x*e.x+e.z*e.z);if(len<.01f)continue;
                    if(s+len>=half){var p=a+e*((half-s)/len);if(Math.Abs(e.y)<.01f)PlateAt(b,p,new V3(e.x/len,0,e.z/len),"cd.siren",7.5f,5f);break;}
                    s+=len;
                }
            }
        }
        private static bool OnFloor(Builder b,V3 p)=>Math.Abs(b.S.Floor(p.x,p.z,p.y+.3f)-p.y)<.06f&&!b.S.Inside(p.x,p.y+.5f,p.z,-1)&&!LeftUnder(b,p);
        /// <summary>Whether the floor under a point is a landmark's block's top (no paint or puddle of the dress lies on it).</summary>
        private static bool LeftUnder(Builder b,V3 p)
        {
            var B=b.S.B;
            for(int i=0;i<b.Left.Length;i++){if(!b.Left[i])continue;int o=i*6;if(p.x>B[o]-.05f&&p.x<B[o+1]+.05f&&p.z>B[o+4]-.05f&&p.z<B[o+5]+.05f&&p.y-.1f<B[o+3]&&p.y-.1f>B[o+2])return true;}
            return false;
        }
        /// <summary>The SANCTUARY mark: the word with its arrow pointing home, the metres under it, laid on the road (flat paint).</summary>
        private static void Sanctuary(Builder b,V3 p,V3 home,int metres)
        {
            var right=new V3(home.z,0,-home.x);var y=new V3(0,.006f,0);
            b.Add("paint.sanctuary",p+home*.6f+y,right,home,1,1,-1,-1,0,0,p.y,p.y,p.y,true,true);
            string digits=metres.ToString(System.Globalization.CultureInfo.InvariantCulture)+"M";float x0=-(digits.Length-1)*GlyphAdvance*.5f;
            for(int k=0;k<digits.Length;k++)
            {
                string key=digits[k]=='M'?"paint.M":"paint."+digits[k];
                b.Add(key,p+home*(.6f-GlyphLine)+right*(x0+k*GlyphAdvance)+y,right,home,1,1,-1,-1,0,0,p.y,p.y,p.y,true,true);
            }
        }
        /// <summary>A wall-mounted piece (a name plate, a clock, a siren) on the nearest wall either side of a route point, `rise` over
        /// the floor; the wall must stand `above` m over its mount.</summary>
        private static bool PlateAt(Builder b,V3 p,V3 dir,string key,float rise,float above)
        {
            var across=new V3(dir.z,0,-dir.x);float best=float.MaxValue;V3 bd=default;
            foreach(var d in new[]{across,across*-1})
                if(Wall(b,p,d,rise,8f,out float t,out _)&&t<best){var w=p+d*(t+.2f);if(b.S.Inside(w.x,p.y+rise+above,w.z,-1)&&b.S.Inside(w.x,p.y+.5f,w.z,-1)){best=t;bd=d;}}
            if(best==float.MaxValue)return false;var n=bd*-1;var at=p+bd*best+new V3(0,rise,0);
            b.Add(key,at,Cross(new V3(0,1,0),n),n,1,1,-1,-1,0,0,at.y-.5f,at.y+.5f,p.y,rise<Hug,false);return true;
        }
    }
}
