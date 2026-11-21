using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    public static partial class NativeMeridianStreetLife
    {
        /// <summary>
        /// The solid street props proposed for a city (NativeMeridianStreetProps; the contract for Astra): every candidate of the
        /// layout by the contract's rules, in a fixed order, each marked filled or not by the run's world seed (a stem's Fill share).
        /// Candidates are the layout's alone, so Astra may validate a layout once with every candidate filled: any subset keeps every
        /// rule (taking a solid away never narrows a lane or cuts a path). Pure and deterministic; nothing here is a collider: it is
        /// what the DEV preview draws and what the contract asks for.
        /// </summary>
        public static List<NativeMeridianStreetProps.Block> ProposeProps(Input input)
        {
            if(input?.Map?.blocks==null)throw new ArgumentException("No map",nameof(input));
            var w=WorldOf(input);w.Slots=Bays(w);
            var o=new PropPlan{W=w};
            Kerbside(o);
            BusStops(o);
            Plaza(o);
            LampPosts(o);
            Alleys(o);
            Yards(o);
            Horde(o);
            // the run's fill: a seeded share of each stem's candidates
            for(int i=0;i<o.Props.Count;i++)
            {
                var b=o.Props[i];var st=NativeMeridianStreetProps.StemOf(b.Stem);
                b.Filled=FillProp(w.RSeed,i,st.Fill);b.Slot=i;b.Id=NativeMeridianStreetProps.IdPrefix+b.Stem+"-"+i;o.Props[i]=b;
            }
            return o.Props;
        }
        internal static bool FillProp(uint seed, int slot, float fill) => Rand(seed, 301, slot) < fill;

        /// <summary>The horde's lattice (Core NativeMeridianNavigation: the Hulk's radius and height on every street) built with the
        /// candidates must keep every node it reached without them but those under them: a candidate that closes a pocket (a nook
        /// between parked cars and a wall, a hydrant and a bin) is dropped, the one nearest each cut node, until none is cut (four
        /// passes at most; any left are dropped whole). A prop's top is never a street of the lattice (as the fire escapes' and the
        /// camp's: here by its id, for the lattice's own rule).</summary>
        private static void Horde(PropPlan o)
        {
            var map=o.W.In.Map;var before=NativeMeridianNavigation.Build(map);
            for(int pass=0;pass<5&&o.Props.Count>0;pass++)
            {
                var blocks=new ContentBlock[map.blocks.Length+o.Props.Count];Array.Copy(map.blocks,blocks,map.blocks.Length);
                for(int i=0;i<o.Props.Count;i++){var c=NativeMeridianStreetProps.ToBlock(o.Props[i]);c.id=NativeMeridianStreetProps.IdPrefix+i;blocks[map.blocks.Length+i]=c;}
                var after=NativeMeridianNavigation.Build(new ContentMap{id=map.id,bounds=map.bounds,ramps=map.ramps,blocks=blocks});
                var cut=Cut(before,after,o.Props);if(cut.Count==0)return;
                var drop=new HashSet<int>();
                foreach(var n in cut)
                {
                    int best=-1;float bd=float.MaxValue;
                    for(int i=0;i<o.Props.Count;i++){var b=o.Props[i];float dx=Math.Max(0,Math.Max(b.Lo.x-n.x,n.x-b.Hi.x)),dz=Math.Max(0,Math.Max(b.Lo.z-n.z,n.z-b.Hi.z)),d=dx*dx+dz*dz+(Math.Abs(b.Lo.y-n.y)>3?1e6f:0);if(d<bd){bd=d;best=i;}}
                    if(best>=0)drop.Add(best);
                }
                if(pass==4){o.Props.Clear();return;}
                var keep=new List<NativeMeridianStreetProps.Block>();for(int i=0;i<o.Props.Count;i++)if(!drop.Contains(i))keep.Add(o.Props[i]);
                o.Props.Clear();o.Props.AddRange(keep);
            }
        }
        /// <summary>The lattice's nodes (their feet) reached without the props and lost with them, but those under a prop.</summary>
        public static List<V3> Cut(MapNav before,MapNav after,List<NativeMeridianStreetProps.Block> props)
        {
            var have=new HashSet<(int,int,int)>();foreach(var n in after.nodes)have.Add(((int)Math.Round(n.pos.x),(int)Math.Round(n.pos.y*4),(int)Math.Round(n.pos.z)));
            float r=NativeHordeKinds.HulkRadius+.25f;var cut=new List<V3>();
            foreach(var n in before.nodes)
            {
                if(have.Contains(((int)Math.Round(n.pos.x),(int)Math.Round(n.pos.y*4),(int)Math.Round(n.pos.z))))continue;var f=new V3(n.pos.x,n.pos.y-.9f,n.pos.z);bool under=false;
                foreach(var b in props){float dx=Math.Max(0,Math.Max(b.Lo.x-f.x,f.x-b.Hi.x)),dz=Math.Max(0,Math.Max(b.Lo.z-f.z,f.z-b.Hi.z));if(dx*dx+dz*dz<r*r&&b.Lo.y<f.y+NativeHordeKinds.HulkHeight&&b.Hi.y>f.y+.05f){under=true;break;}}
                if(!under)cut.Add(f);
            }
            return cut;
        }
        private sealed class PropPlan{public World W;public readonly List<NativeMeridianStreetProps.Block> Props=new List<NativeMeridianStreetProps.Block>();public readonly Boxes Taken=new Boxes();public int Refused;}

        /// <summary>A prop's candidate at a floor point (its block's foot centre) turned by a quarter-turn yaw: false where a rule refuses it.</summary>
        private static bool TryProp(PropPlan o,string stem,V3 foot,float yaw,string model)
        {
            var w=o.W;var st=NativeMeridianStreetProps.StemOf(stem);var size=NativeMeridianStreetProps.WorldSize(st,yaw);
            // each stem's own spacing (none of its kind nearer): the streets read furnished, not stocked
            float apart=Apart(stem);
            if(apart>0)foreach(var b in o.Props){if(b.Stem!=stem)continue;float dx=b.Pos.x-foot.x,dz=b.Pos.z-foot.z;if(dx*dx+dz*dz<apart*apart&&Math.Abs(b.Pos.y-b.Size.y*.5f-foot.y)<3f){o.Refused++;return false;}}
            var lo=new V3(foot.x-size.x*.5f,foot.y,foot.z-size.z*.5f);var hi=new V3(foot.x+size.x*.5f,foot.y+size.y,foot.z+size.z*.5f);
            if(!PropRules(w,stem,lo,hi,o.Taken)){o.Refused++;return false;}
            o.Taken.Add(lo,hi);
            o.Props.Add(new NativeMeridianStreetProps.Block{Stem=stem,Model=model,Pos=new V3(foot.x,foot.y+size.y*.5f,foot.z),Size=size,Yaw=yaw});
            return true;
        }
        /// <summary>The least distance (m) between two props of a stem (0: none; cordons and bollards stand in their own rows).</summary>
        public static float Apart(string stem)=>stem=="hydrant"?55f:stem=="bin"?28f:stem=="dumpster"?24f:stem=="bench"?16f:stem=="newsstand"?45f:stem=="kiosk"?60f:stem=="planter"?9f:stem=="wreck"?30f:stem=="bus"?60f:stem=="shelter"?60f:stem=="barricade"?30f:0f;
        /// <summary>Every rule of the contract on a prop's box (also the checks' measure): the camp and its gates, the caches, the doors,
        /// the landmarks, the routes' clear lanes and junctions, the flights, the floor under it, the blocks round it and the gap rule.</summary>
        private static bool PropRules(World w,string stem,V3 lo,V3 hi,Boxes taken)
        {
            float cx=(lo.x+hi.x)*.5f,cz=(lo.z+hi.z)*.5f,y=lo.y;
            // the camp, and each gate's approach
            if(lo.x<26f&&hi.x>-26f&&lo.z<26f&&hi.z>-26f)return false;
            foreach(var g in new[]{new V3(0,0,25),new V3(25,0,0),new V3(0,0,-25),new V3(-25,0,0)})
            {float dx=Math.Max(0,Math.Max(lo.x-g.x,g.x-hi.x)),dz=Math.Max(0,Math.Max(lo.z-g.z,g.z-hi.z));if(dx*dx+dz*dz<NativeMeridianStreetProps.Gate*NativeMeridianStreetProps.Gate&&Math.Abs(y)<1f)return false;}
            // the caches (their keep-outs and a metre more)
            var m=new V3(NativeMeridianStreetProps.CacheMargin,0,NativeMeridianStreetProps.CacheMargin);
            if(w.In.KeepOut!=null)foreach(var ko in w.In.KeepOut)if(ko.Hits(lo-m,hi+m))return false;
            // the doors (door front, join circle) and the final court's gateway: the routes' last points
            foreach(var r in w.In.Routes??Array.Empty<NativeMeridianDress.Route>())
            {
                if(r?.Points==null||r.Points.Length==0)continue;var d=r.Points[r.Points.Length-1];
                float dx=Math.Max(0,Math.Max(lo.x-d.x,d.x-hi.x)),dz=Math.Max(0,Math.Max(lo.z-d.z,d.z-hi.z));if(dx*dx+dz*dz<NativeMeridianStreetProps.Door*NativeMeridianStreetProps.Door&&Math.Abs(y-d.y)<8f)return false;
            }
            // the landmarks' features
            foreach(var (c,r) in w.Rounds){float dx=Math.Max(0,Math.Max(lo.x-c.x,c.x-hi.x)),dz=Math.Max(0,Math.Max(lo.z-c.z,c.z-hi.z));if(dx*dx+dz*dz<r*r&&Math.Abs(y-c.y)<10)return false;}
            // the routes: the clear lane, and the junctions (bollards and hydrants may stand on a junction's pavement)
            bool small=stem=="bollard"||stem=="hydrant";
            foreach(var lg in w.Legs)
            {
                var ab=new V3(lg.b.x-lg.a.x,0,lg.b.z-lg.a.z);float len=ab.Length;if(len<.01f)continue;var al=ab/len;var ac=new V3(al.z,0,-al.x);
                float half=Math.Max(NativeMeridianStreetProps.LaneHalf,lg.width*.5f-NativeMeridianStreetProps.KerbBand);
                if(BoxHitsRect(lo,hi,lg.a,al,ac,-1f,len+1f,half,Math.Min(lg.a.y,lg.b.y)-2f,Math.Max(lg.a.y,lg.b.y)+3.2f))return false;
                if(!small)foreach(var q in new[]{lg.a,lg.b}){float dx=Math.Max(0,Math.Max(lo.x-q.x,q.x-hi.x)),dz=Math.Max(0,Math.Max(lo.z-q.z,q.z-hi.z));if(dx*dx+dz*dz<NativeMeridianStreetProps.Junction*NativeMeridianStreetProps.Junction&&Math.Abs(y-q.y)<3f)return false;}
            }
            // the flights: no prop within 4 m of a stair's, a slope's or a fire escape's footprint
            if(NearFlight(w,new V3(lo.x,y,lo.z),new V3(hi.x,y,lo.z),y-.5f,hi.y,NativeMeridianStreetProps.Flight)||NearFlight(w,new V3(lo.x,y,hi.z),new V3(hi.x,y,hi.z),y-.5f,hi.y,NativeMeridianStreetProps.Flight))return false;
            // the floor: its whole footprint on a street's top (a terrace's; a bridge only for the small ones), nothing standing there
            for(int i=0;i<=4;i++)for(int j=0;j<=4;j++)
            {
                float x=lo.x+(hi.x-lo.x)*(.02f+.96f*i/4),z=lo.z+(hi.z-lo.z)*(.02f+.96f*j/4);if(!OnTop(w,x,z,y))return false;
                int u=BlockUnder(w,x,z,y);if(u<0||w.Left[u]||w.Tread[u])return false;var role=w.Roles[u];if(!(role==NativeMeridianDress.RGround||small&&role==NativeMeridianDress.RBridge))return false;
            }
            // nothing in its box (2 cm in), and the gap rule against the blocks and the props round it
            var ins=new V3(.02f,.02f,.02f);if(!BoxClear(w,lo+ins,hi-ins))return false;
            if(taken.Hits(lo,hi,.01f,.01f))return false;
            if(Trap(w,lo,hi,taken))return false;
            return true;
        }
        // Whether a box meets a rectangle along a leg: a from-to along `al`, half across `ac`, between heights y0 and y1.
        private static bool BoxHitsRect(V3 lo,V3 hi,V3 a,V3 al,V3 ac,float s0,float s1,float half,float y0,float y1)
        {
            if(hi.y<=y0||lo.y>=y1)return false;
            // the box's corners in the leg's frame (separating axes: the leg's two, the box's two)
            float minS=1e9f,maxS=-1e9f,minU=1e9f,maxU=-1e9f;
            foreach(float x in new[]{lo.x,hi.x})foreach(float z in new[]{lo.z,hi.z}){var d=new V3(x-a.x,0,z-a.z);float s=V3.Dot(d,al),u=V3.Dot(d,ac);minS=Math.Min(minS,s);maxS=Math.Max(maxS,s);minU=Math.Min(minU,u);maxU=Math.Max(maxU,u);}
            if(maxS<=s0||minS>=s1||maxU<=-half||minU>=half)return false;
            // the rectangle's corners against the box's axes
            float rx0=1e9f,rx1=-1e9f,rz0=1e9f,rz1=-1e9f;
            foreach(float s in new[]{s0,s1})foreach(float u in new[]{-half,half}){var p=a+al*s+ac*u;rx0=Math.Min(rx0,p.x);rx1=Math.Max(rx1,p.x);rz0=Math.Min(rz0,p.z);rz1=Math.Max(rz1,p.z);}
            return rx1>lo.x&&rx0<hi.x&&rz1>lo.z&&rz0<hi.z;
        }
        /// <summary>The gap rule: on each side, the nearest wall or prop is flush (5 cm or less) or a pilot's width away (1 m or more);
        /// a narrower gap would be a slot a pilot sticks in.</summary>
        private static bool Trap(World w,V3 lo,V3 hi,Boxes taken)
        {
            float y0=lo.y+.1f,y1=Math.Min(hi.y,lo.y+1.8f);
            for(int side=0;side<4;side++)
            {
                // the slab beside this side, from just past flush to a pilot's width out
                V3 a,b;
                switch(side)
                {
                    case 0:a=new V3(hi.x+NativeMeridianStreetProps.Flush+.01f,y0,lo.z+.05f);b=new V3(hi.x+NativeMeridianStreetProps.Pass,y1,hi.z-.05f);break;
                    case 1:a=new V3(lo.x-NativeMeridianStreetProps.Pass,y0,lo.z+.05f);b=new V3(lo.x-NativeMeridianStreetProps.Flush-.01f,y1,hi.z-.05f);break;
                    case 2:a=new V3(lo.x+.05f,y0,hi.z+NativeMeridianStreetProps.Flush+.01f);b=new V3(hi.x-.05f,y1,hi.z+NativeMeridianStreetProps.Pass);break;
                    default:a=new V3(lo.x+.05f,y0,lo.z-NativeMeridianStreetProps.Pass);b=new V3(hi.x-.05f,y1,lo.z-NativeMeridianStreetProps.Flush-.01f);break;
                }
                if(!BoxClear(w,a,b)||taken.Hits(a,b,0f,0f))
                {
                    // something stands in the slab: a trap, unless the side is flush against it (then it stands within 5 cm too)
                    V3 fa,fb;
                    switch(side)
                    {
                        case 0:fa=new V3(hi.x,y0,lo.z+.05f);fb=new V3(hi.x+NativeMeridianStreetProps.Flush,y1,hi.z-.05f);break;
                        case 1:fa=new V3(lo.x-NativeMeridianStreetProps.Flush,y0,lo.z+.05f);fb=new V3(lo.x,y1,hi.z-.05f);break;
                        case 2:fa=new V3(lo.x+.05f,y0,hi.z);fb=new V3(hi.x-.05f,y1,hi.z+NativeMeridianStreetProps.Flush);break;
                        default:fa=new V3(lo.x+.05f,y0,lo.z-NativeMeridianStreetProps.Flush);fb=new V3(hi.x-.05f,y1,lo.z);break;
                    }
                    if(BoxClear(w,fa,fb)&&!taken.Hits(fa,fb,0f,0f))return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------------------------------------------------------
        // The kerbs: parked cars in the gutters of the streets (10-40 m between their walls), and on the pavements bins, hydrants,
        // bollards at the corners, benches and newsstands against the shops, planters by the civic fronts, dumpsters behind the
        // tenements and the warehouses
        // ------------------------------------------------------------------------------------------------------------------
        private static readonly string[] Paints={"car.blue","car.cream","car.maroon","car.green","car.mustard","car.white"};
        private static void Kerbside(PropPlan o)
        {
            var w=o.W;uint ls=w.LSeed;var lastCar=new Dictionary<long,float>();var lastBin=new Dictionary<long,float>();var lastHyd=new Dictionary<long,float>();
            for(int i=0;i<w.Slots.Count;i++)
            {
                var s=w.Slots[i];if(s.Flat||!s.Open)continue;long face=((long)s.Block<<3)^s.Face;float along=V3.Dot(s.Foot,s.Along);
                uint h=Hash2(ls,(uint)(s.Block*16+s.Face)*1777u+(uint)s.Bay);float r=Rand(h,200,0);float yawOut=YawOf(s.N),yawAlong=YawOf(s.Along);
                // a car in the gutter, its near side 0.2 m off the kerb, nose either way along the street, one every 6.5 m
                if(s.Gap>=10f&&s.Gap<=40f&&(!lastCar.TryGetValue(face,out float pc)||Math.Abs(along-pc)>=9f)&&r<(s.Style=='r'||s.Style=='v'?.4f:s.Style=='s'||s.Style=='t'?.32f:s.Style=='c'?.2f:.25f))
                {
                    var st=NativeMeridianStreetProps.StemOf("car");float off=NativeMeridianStreetProps.Kerb+.2f+st.Size.x*.5f;
                    string model=Rand(h,201,0)<.12f?"car.smashed":Rand(h,202,0)<.1f?"car.rack":Paints[(int)(Rand(h,203,0)*Paints.Length)];
                    if(TryProp(o,"car",s.Foot+s.N*off,Rand(h,204,0)<.5f?yawAlong:yawAlong+(float)Math.PI,model))lastCar[face]=along;
                }
                // on the pavement: at the kerb (1.9 m out: a pilot's width and more from the wall) or flush against the wall
                float q=Rand(h,205,0);var kerb=s.Foot+s.N*1.95f;var wall=s.Foot;
                if((s.Style=='s'||s.Style=='m'||s.Style=='t'||s.Style=='c')&&q<.18f&&(!lastBin.TryGetValue(face,out float pb)||Math.Abs(along-pb)>=14f))
                {if(TryProp(o,"bin",kerb,yawOut,"bin"))lastBin[face]=along;continue;}
                if(q<.26f&&(!lastHyd.TryGetValue(face,out float ph)||Math.Abs(along-ph)>=40f)&&s.Style!='h')
                {if(TryProp(o,"hydrant",kerb+s.Along*.8f,yawOut,"hydrant"))lastHyd[face]=along;continue;}
                switch(s.Style)
                {
                    case 's':
                        if(q<.36f&&s.Bay>0&&s.Bay<s.Bays-1)TryProp(o,"newsstand",wall+s.N*(NativeMeridianStreetProps.StemOf("newsstand").Size.z*.5f+.01f),yawOut,"newsstand");
                        else if(q<.44f)TryProp(o,"bench",wall+s.N*(NativeMeridianStreetProps.StemOf("bench").Size.z*.5f+.01f),yawOut,Rand(h,206,0)<.3f?"bench.broken":"bench");
                        break;
                    case 'm':if(q<.4f)TryProp(o,"bench",wall+s.N*(NativeMeridianStreetProps.StemOf("bench").Size.z*.5f+.01f),yawOut,Rand(h,206,0)<.3f?"bench.blanket":"bench");break;
                    case 'c':
                        if(q<.42f)TryProp(o,"planter",kerb,yawOut,"planter");
                        else if(q<.5f)TryProp(o,"bench",wall+s.N*(NativeMeridianStreetProps.StemOf("bench").Size.z*.5f+.01f),yawOut,"bench");
                        break;
                    case 't':case 'w':case 'h':
                        if(q<(s.Style=='t'?.34f:.4f))TryProp(o,"dumpster",wall+s.N*(NativeMeridianStreetProps.StemOf("dumpster").Size.z*.5f+.01f),yawOut,Rand(h,207,0)<.4f?"dumpster.open":"dumpster");
                        break;
                    case 'r':case 'v':if(q<.3f&&s.Bay==0)TryProp(o,"kiosk",wall+s.N*.61f+s.Along*-.4f,yawOut,"kiosk.m");break;
                }
                // bollards at a face's ends facing a route: a pair at the kerb line, 1.5 m apart
                if(s.Route>=0&&(s.Bay==0||s.Bay==s.Bays-1)&&(s.Style=='s'||s.Style=='m'||s.Style=='c')&&Rand(h,208,0)<.5f)
                    foreach(float u in new[]{-.75f,.75f})TryProp(o,"bollard",kerb+s.Along*u,yawOut,Rand(h,209,0)<.3f?"bollard.sleeve":"bollard.cannon");
            }
        }

        // ------------------------------------------------------------------------------------------------------------------
        // Bus stops on the wide shopping and civic streets off the routes (14 m or more between the walls): the shelter's back to
        // the wall, a 647 stopped at the kerb a pilot's width beyond its front
        // ------------------------------------------------------------------------------------------------------------------
        private static void BusStops(PropPlan o)
        {
            var w=o.W;uint ls=w.LSeed;int stops=0;
            for(int i=0;i<w.Slots.Count&&stops<3;i++)
            {
                var s=w.Slots[i];if(s.Flat||!s.Open||s.Route>=0||s.Gap<14f||s.Gap>40f||s.Bays<5||s.Bay!=s.Bays/2||!(s.Style=='s'||s.Style=='c'||s.Style=='m'||s.Style=='t'))continue;
                uint h=Hash2(ls,(uint)(s.Block*16+s.Face)*3301u);if(Rand(h,210,0)>.45f)continue;
                var sh=NativeMeridianStreetProps.StemOf("shelter");
                if(!TryProp(o,"shelter",s.Foot+s.N*(sh.Size.z*.5f+.01f),YawOf(s.N),"shelter.cast"))continue;stops++;
                if(Rand(h,211,0)<.7f)
                {
                    var bus=NativeMeridianStreetProps.StemOf("bus");float off=sh.Size.z+.01f+NativeMeridianStreetProps.Pass+.05f+bus.Size.x*.5f;
                    TryProp(o,"bus",s.Foot+s.N*off+s.Along*(Rand(h,212,0)-.5f)*3f,YawOf(s.Along)+(Rand(h,213,0)<.5f?0:(float)Math.PI),"bus.647");
                }
            }
        }

        // ------------------------------------------------------------------------------------------------------------------
        // The camp's plaza: lamp posts on two rings, planters and benches by them, the Civil Defence's cordons funnelling two of the
        // gates' approaches (barriers end to end either side of the lane, a police car beside one), cars left in the outer ring
        // ------------------------------------------------------------------------------------------------------------------
        private static void Plaza(PropPlan o)
        {
            var w=o.W;uint ls=w.LSeed;
            V3[] gates={new V3(0,0,1),new V3(1,0,0),new V3(0,0,-1),new V3(-1,0,0)};
            for(int g=0;g<4;g++)
            {
                if(Rand(ls,220,g)>.5f)continue;var d=gates[g];var right=new V3(d.z,0,-d.x);var bar=NativeMeridianStreetProps.StemOf("barrier");
                foreach(float side in new[]{-1f,1f})
                {
                    // barriers end to end along the lane's side, 7.5 m off its axis, from 33 m out
                    for(int k=0;k<6;k++)TryProp(o,"barrier",d*(33f+bar.Size.x*(k+.5f))+right*(side*7.5f),YawOf(right),"barrier");
                }
                var pol=NativeMeridianStreetProps.StemOf("police");
                TryProp(o,"police",d*(38f+Rand(ls,221,g)*4f)+right*((Rand(ls,222,g)<.5f?-1:1)*(7.5f+bar.Size.z*.5f+NativeMeridianStreetProps.Pass+.2f+pol.Size.x*.5f)),YawOf(d),"police.car");
            }
            // lamp posts on the plaza's rings (31 and 50 m out), every 14 m, and a planter or a bench beside every other one
            int n=0;
            foreach(float ring in new[]{31f,50f})
                for(float t=-ring;t<=ring+.01f;t+=14f)
                    foreach(var (dx,dz) in new[]{(1f,0f),(-1f,0f),(0f,1f),(0f,-1f)})
                    {
                        var at=new V3(dx!=0?dx*ring:t,0,dz!=0?dz*ring:t);n++;
                        if(Math.Abs(t)<12f)continue;   // the gates' approaches
                        if(!TryProp(o,"lamp",at,0,Rand(ls,223,n)<.2f?"lamp.post.dead":"lamp.post"))continue;
                        var side=new V3(dz,0,-dx);
                        if(Rand(ls,224,n)<.5f){if(Rand(ls,225,n)<.6f)TryProp(o,"planter",at+side*2.4f,YawOf(new V3(-dx,0,-dz)),"planter");else TryProp(o,"bench",at+side*2.6f,YawOf(new V3(-dx,0,-dz)),"bench");}
                    }
            // cars left in the plaza's outer ring, in a line as if queued for the camp
            for(int k=0;k<6;k++)
            {
                var d=gates[(int)(Rand(ls,226,k)*4)];var right=new V3(d.z,0,-d.x);var at=d*(52f+6f*Rand(ls,227,k))+right*((Rand(ls,228,k)-.5f)*60f);
                TryProp(o,Rand(ls,229,k)<.25f?"wreck":"car",at,YawOf(right)+(Rand(ls,230,k)<.5f?0:(float)Math.PI),Rand(ls,231,k)<.25f?"wreck.crashed":Paints[(int)(Rand(ls,232,k)*Paints.Length)]);
            }
        }

        // ------------------------------------------------------------------------------------------------------------------
        // Lamp posts where the street plan's lamp points found no wall (the dress hangs a lamp on a wall, a span or a low wall
        // where it can: these are the points it could not)
        // ------------------------------------------------------------------------------------------------------------------
        private static void LampPosts(PropPlan o)
        {
            var w=o.W;var pts=NativeMeridianDress.SheetLamps(w.In.Layout)??Array.Empty<float>();uint ls=w.LSeed;
            for(int k=0;k+2<pts.Length;k+=3)
            {
                var p=new V3(pts[k],pts[k+1],pts[k+2]);bool hung=false;
                if(w.In.Dress!=null)foreach(var l in w.In.Dress.Lamps){var d=l.Mount-p;if(d.x*d.x+d.z*d.z<81f&&Math.Abs(l.Pool.y-p.y)<1f){hung=true;break;}}
                if(hung)continue;
                TryProp(o,"lamp",p,0,Rand(ls,240,k)<.2f?"lamp.post.dead":"lamp.post");
            }
        }

        // ------------------------------------------------------------------------------------------------------------------
        // The alleys (walls 5-9 m apart): dumpsters against a wall, and at a mouth now and then the survivors' barricade across
        // half its width, a Hulk's 3.2 m always left
        // ------------------------------------------------------------------------------------------------------------------
        private static void Alleys(PropPlan o)
        {
            var w=o.W;uint ls=w.LSeed;
            for(int i=0;i<w.Slots.Count;i++)
            {
                var s=w.Slots[i];if(s.Flat||s.Gap<5f||s.Gap>9f||s.Route>=0)continue;uint h=Hash2(ls,(uint)(s.Block*16+s.Face)*2203u+(uint)s.Bay);
                if((s.Bay==0||s.Bay==s.Bays-1)&&s.Gap>=3.4f+3.4f&&Rand(h,250,0)<.25f)
                {
                    var bc=NativeMeridianStreetProps.StemOf("barricade");TryProp(o,"barricade",s.Foot+s.N*(bc.Size.x*.5f+.01f),YawOf(s.Along),"barricade.furniture");continue;
                }
                if(s.Gap-1.25f>=3.4f&&Rand(h,251,0)<.12f){var dm=NativeMeridianStreetProps.StemOf("dumpster");TryProp(o,"dumpster",s.Foot+s.N*(dm.Size.z*.5f+.01f),YawOf(s.N),Rand(h,252,0)<.4f?"dumpster.open":"dumpster");}
            }
        }

        // ------------------------------------------------------------------------------------------------------------------
        // The yards and quays (open ground, no lot within 18 m): a few wrecks and dumpsters left where they stood
        // ------------------------------------------------------------------------------------------------------------------
        private static void Yards(PropPlan o)
        {
            var w=o.W;uint ls=w.LSeed;var mb=w.In.Map.bounds;int placed=0;
            for(int k=0;k<220&&placed<8;k++)
            {
                var at=new V3(mb.minX+20f+(mb.maxX-mb.minX-40f)*Rand(ls,260,k),0,mb.minZ+20f+(mb.maxZ-mb.minZ-40f)*Rand(ls,261,k));
                float y=FloorUnder(w,at.x,at.z,60f);if(y<-1e8f)continue;at.y=y;
                if(NearestSlot(w,at,18f)!=null)continue;
                bool wreck=Rand(ls,262,k)<.6f;
                if(TryProp(o,wreck?"wreck":"dumpster",at,(int)(Rand(ls,263,k)*4)*(float)(Math.PI/2),wreck?(Rand(ls,264,k)<.5f?"wreck.burnt":"wreck.crashed"):"dumpster"))placed++;
            }
        }
    }
}
