using System;
using System.IO;
using System.Linq;

namespace ChipArena.UnityConversion
{
    // The debris style (owner 2026-11-13; Core/NativeDebrisShapes.cs, the realistic part of Core/NativeStructureFx.cs, Runtime
    // NativeSlamDebrisPresentation.cs): the default is the realistic style on every shipped map and the voxel style still
    // builds (a map's look, the map list or the preview hook turn it on); the broken-piece meshes are sound; a realistic
    // break follows its material and its prop's size, bounces, settles on the floor without sinking into it, leans along
    // the blow, thins at reduced effects and is gone after its life; a slam's debris scales with its weight, rings dust
    // from the heaviest, sparks on metal, and cracks the floor; a loose fragment is drawn inside its own box; frames are
    // pure and allocation-free; and the runtime hooks are in place with no collider, body or network field of their own.
    internal static class DebrisChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Debris: "+what);count++;}
        private static readonly V3 Eye=new V3(0,1.6f,-30);
        public static void Run(string root)
        {
            Style(root);Shapes();Voxel();Breaks();Physics();Slams();Loose();Pure();Hooks(root);
            Console.WriteLine("PASS debris ("+count+" checks: the realistic style is the default on every shipped map and the voxel style still builds; piece meshes; realistic breaks by material and prop size; bounce, settle and fade; the blow's lean; slam debris by weight, dust ring, metal sparks, floor cracks; loose fragments in their boxes; pure and allocation-free frames; runtime hooks without colliders)");
        }
        private static void Style(string root)
        {
            Check(NativeDebrisLook.Default==NativeDebrisStyle.Realistic&&new NativeStructureFx().Style==NativeDebrisStyle.Realistic,"the default debris style is realistic");
            Check(NativeDebrisLook.VoxelMaps.Count==0&&NativeDebrisLook.Preview==null,"no map is a voxel map by code and nothing forces a style");
            Check(NativeDebrisLook.For("factory",null)==NativeDebrisStyle.Realistic&&NativeDebrisLook.For("factory","")==NativeDebrisStyle.Realistic&&NativeDebrisLook.For(null,"realistic")==NativeDebrisStyle.Realistic,"a map without the switch is realistic");
            Check(NativeDebrisLook.For("factory","voxel")==NativeDebrisStyle.Voxel&&NativeDebrisLook.For("factory","Voxel")==NativeDebrisStyle.Voxel,"a look's debris \"voxel\" turns the voxel style on");
            NativeDebrisLook.VoxelMaps.Add("blockworld");
            try{Check(NativeDebrisLook.For("blockworld",null)==NativeDebrisStyle.Voxel&&NativeDebrisLook.For("factory",null)==NativeDebrisStyle.Realistic,"a listed map id is a voxel map, the others are not");}finally{NativeDebrisLook.VoxelMaps.Clear();}
            NativeDebrisLook.Preview=NativeDebrisStyle.Voxel;
            try{Check(NativeDebrisLook.For("factory",null)==NativeDebrisStyle.Voxel,"the preview hook forces a style");}finally{NativeDebrisLook.Preview=null;}
            string looks=File.ReadAllText(Path.Combine(root,"Assets","Conversion","Resources","MapLooks.json"));
            Check(!looks.Contains("\"debris\""),"no shipped look asks for voxel debris (the realistic style everywhere)");
            var set=System.Text.Json.JsonSerializer.Deserialize<MapLookSet>("{\"maps\":[{\"id\":\"blockworld\",\"debris\":\"voxel\"}]}",new System.Text.Json.JsonSerializerOptions{IncludeFields=true});
            Check(set?.maps!=null&&set.maps.Length==1&&set.maps[0].debris=="voxel"&&NativeDebrisLook.For(set.maps[0].id,set.maps[0].debris)==NativeDebrisStyle.Voxel,"a look entry carries the switch");
            // Every shipped look is held styled (NativeLighting.LookSet: NativeMapStyle.Styled copies the look field by field), so the
            // copy must carry the switch: a styled voxel look draws voxel debris (it dropped it once, and the voxel style never showed).
            var shipped=System.Text.Json.JsonSerializer.Deserialize<MapLookSet>(looks,new System.Text.Json.JsonSerializerOptions{IncludeFields=true});int styled=0;
            foreach(var m in shipped.maps)
            {
                if(!NativeMapStyle.On(m,false))continue;styled++;m.debris="voxel";
                try{var s=NativeMapStyle.Styled(m,false);Check(!ReferenceEquals(s,m)&&s.debris=="voxel"&&NativeDebrisLook.For(s.id,s.debris)==NativeDebrisStyle.Voxel,m.id+": the styled look keeps its debris switch");}finally{m.debris=null;}
                Check(NativeDebrisLook.For(m.id,NativeMapStyle.Styled(m,false).debris)==NativeDebrisStyle.Realistic,m.id+": styled without the switch, realistic");
            }
            Check(styled>=10,"the styled looks were checked ("+styled+")");
            Check(typeof(MapLook).GetFields().Count(f=>f.Name=="debris")==1,"the look has one debris field");
        }
        private static void Shapes()
        {
            Check(NativeDebrisShapes.Mesh(NativeDebrisShape.Block,0)==null,"the block is the cube (no mesh)");
            foreach(NativeDebrisShape shape in Enum.GetValues(typeof(NativeDebrisShape)))
            {
                if(shape==NativeDebrisShape.Block)continue;float[] first=null;bool varied=false;
                for(int v=0;v<NativeDebrisShapes.Variants;v++)
                {
                    var m=NativeDebrisShapes.Mesh(shape,v);Check(m!=null&&m.Triangles>=4&&m.Triangles<=NativeDebrisShapes.MaxTriangles&&m.P.Length==m.Triangles*9&&m.N.Length==m.Triangles*3,shape+" "+v+" is a small flat-shaded mesh");
                    float[] lo={9,9,9},hi={-9,-9,-9};for(int i=0;i<m.P.Length;i++){Check(!float.IsNaN(m.P[i]),shape+" has no NaN");lo[i%3]=Math.Min(lo[i%3],m.P[i]);hi[i%3]=Math.Max(hi[i%3],m.P[i]);}
                    for(int k=0;k<3;k++)Check(Math.Abs(lo[k]+.5f)<1e-3f&&Math.Abs(hi[k]-.5f)<1e-3f,shape+" "+v+" fills its unit box on axis "+k);
                    int outward=0;
                    for(int t=0;t<m.Triangles;t++)
                    {
                        float nx=m.N[t*3],ny=m.N[t*3+1],nz=m.N[t*3+2];Check(Math.Abs(nx*nx+ny*ny+nz*nz-1)<1e-3f,shape+" facet normals are unit");
                        float gx=(m.P[t*9]+m.P[t*9+3]+m.P[t*9+6])/3,gy=(m.P[t*9+1]+m.P[t*9+4]+m.P[t*9+7])/3,gz=(m.P[t*9+2]+m.P[t*9+5]+m.P[t*9+8])/3;if(nx*gx+ny*gy+nz*gz>=0)outward++;
                    }
                    if(shape!=NativeDebrisShape.Panel)Check(outward==m.Triangles,shape+" "+v+" faces outward");
                    Check(ReferenceEquals(m,NativeDebrisShapes.Mesh(shape,v))&&ReferenceEquals(m,NativeDebrisShapes.Mesh(shape,v+NativeDebrisShapes.Variants))&&ReferenceEquals(m,NativeDebrisShapes.Mesh(shape,v-NativeDebrisShapes.Variants)),shape+" variants are made once and wrap");
                    if(first==null)first=m.P;else if(!first.SequenceEqual(m.P))varied=true;
                }
                Check(varied,shape+" has variety");
            }
        }
        private static void Voxel()
        {
            foreach(NativeStructureMaterial m in Enum.GetValues(typeof(NativeStructureMaterial)))
            {
                var fx=new NativeStructureFx{Style=NativeDebrisStyle.Voxel};fx.Break(m,0,new V3(0,1,0),new V3(1,1,1),0,11,0,false);fx.Frame(.15f,Eye,1);
                Check(fx.CubeCount>0&&fx.Cubes.Take(fx.CubeCount).All(c=>c.Shape==NativeDebrisShape.Block),"voxel style: a "+m+" break is cubes");
                fx.Frame(NativeStructureFx.BurstLife,Eye,1);Check(fx.CubeCount==0&&fx.LiveBursts==0,"voxel style: gone after the cube burst's life");
            }
            var s=new NativeStructureFx{Style=NativeDebrisStyle.Voxel};s.Slam(NativeStructureMaterial.Concrete,0,new V3(0,0,0),0,1,new V3(0,0,1),5,0,true);s.Frame(.2f,Eye,1);
            Check(s.LiveSlams==0&&s.LiveBursts==1&&s.CubeCount>0&&s.Cubes.Take(s.CubeCount).All(c=>c.Shape==NativeDebrisShape.Block)&&s.LiveMarks>=3,"voxel style: a slam is the cube rubble burst, with its floor marks");
            s.Loose(NativeStructureMaterial.Timber,0,new V3(3,1,0),new V3(.7f,.4f,.7f),0,0,0,1,9);int before=s.CubeCount;s.Loose(NativeStructureMaterial.Concrete,0,new V3(3,1,0),new V3(.7f,.4f,.7f),0,0,0,1,9);
            Check(s.CubeCount==before,"voxel style: loose fragments keep their boxes (nothing drawn over them)");
            var land=new NativeStructureFx{Style=NativeDebrisStyle.Voxel};land.Land(new V3(0,1,0),5,.85f,NativeStructureMaterial.Timber,0,41,0);land.Frame(2,Eye,1);
            Check(land.CubeCount>0&&land.Cubes.Take(land.CubeCount).All(c=>c.Shape==NativeDebrisShape.Block),"voxel style: collapse rubble is cubes");
        }
        private static NativeStructureFx Broken(NativeStructureMaterial m,V3 size,float t,float quality=1,V3 push=default)
        {var fx=new NativeStructureFx();fx.Break(m,0,new V3(0,size.y*.5f,0),size,0,11,0,false,push);fx.Frame(t,Eye,quality);return fx;}
        private static void Breaks()
        {
            var crate=new V3(2,1.2f,2);
            foreach(NativeStructureMaterial m in Enum.GetValues(typeof(NativeStructureMaterial)))
            {
                var fx=Broken(m,crate,.15f);var cubes=fx.Cubes.Take(fx.CubeCount).ToArray();
                Check(cubes.Length>0&&cubes.All(c=>c.Shape!=NativeDebrisShape.Block),"a realistic "+m+" break throws no cube");
                bool dusty=m!=NativeStructureMaterial.Metal&&m!=NativeStructureMaterial.Glass;Check(dusty?fx.PuffCount>0:fx.PuffCount==0,m+" keeps its dust (and metal and glass none)");
                switch(m)
                {
                    case NativeStructureMaterial.Timber:
                        Check(cubes.Any(c=>c.Shape==NativeDebrisShape.Plank&&c.Size.z>=.9f&&c.Size.z<=1.5f&&c.Size.y<.05f)&&cubes.Count(c=>c.Shape==NativeDebrisShape.Plank)>=3&&cubes.Any(c=>c.Shape==NativeDebrisShape.Splinter),"a crate breaks into boards as long as its edges, and splinters");break;
                    case NativeStructureMaterial.Metal:
                        Check(cubes.Count(c=>c.Shape==NativeDebrisShape.Panel)>=3&&cubes.Where(c=>c.Shape==NativeDebrisShape.Panel).All(c=>c.Size.y<c.Size.x&&c.Size.y<=.13f)&&fx.LineCount>0,"a metal box breaks into bent panels, with sparks");break;
                    case NativeStructureMaterial.Concrete:
                        Check(cubes.Count(c=>c.Shape==NativeDebrisShape.Rock&&Math.Max(c.Size.x,c.Size.z)>=.2f)>=2&&cubes.Any(c=>c.Shape==NativeDebrisShape.Rock&&c.Size.x<.15f)&&cubes.Any(c=>c.Shape==NativeDebrisShape.Pebble),"concrete breaks into a few big rough lumps, many small ones and stones");break;
                    case NativeStructureMaterial.Masonry:Check(cubes.Count(c=>c.Shape==NativeDebrisShape.Brick)>=10,"masonry breaks into snapped bricks");break;
                    default:Check(cubes.All(c=>c.Shape==NativeDebrisShape.Shard),"glass breaks into shards");break;
                }
                Check(Broken(m,crate,.15f,.3f).CubeCount<cubes.Length,m+" reduced effects throw fewer pieces");
                fx.Frame(NativeStructureFx.BurstLife+.2f,Eye,1);Check(fx.CubeCount>0,m+" pieces outlast the cube burst (they lie where they settle)");
                fx.Frame(NativeStructureFx.PieceLife,Eye,1);Check(fx.CubeCount==0&&fx.LiveBursts==0,m+" gone after the piece life");
            }
            float small=Broken(NativeStructureMaterial.Timber,new V3(1,1,1),.15f).Cubes.Take(8).Where(c=>c.Shape==NativeDebrisShape.Plank).Max(c=>c.Size.z),large=Broken(NativeStructureMaterial.Timber,crate,.15f).Cubes.Take(8).Where(c=>c.Shape==NativeDebrisShape.Plank).Max(c=>c.Size.z);
            Check(large>small*1.5f,"a bigger crate throws longer boards ("+large.ToString("0.00")+" against "+small.ToString("0.00")+" m)");
            // The blow's direction leans the pieces.
            float Mean(NativeStructureFx fx){float x=0;for(int i=0;i<fx.CubeCount;i++)x+=fx.Cubes[i].Center.x;return x/fx.CubeCount;}
            float still=Mean(Broken(NativeStructureMaterial.Timber,crate,1)),pushed=Mean(Broken(NativeStructureMaterial.Timber,crate,1,1,new V3(3,5,0))),back=Mean(Broken(NativeStructureMaterial.Timber,crate,1,1,new V3(-1,0,0)));
            Check(pushed>still+.8f&&back<still-.8f,"the pieces lean away from the blow ("+back.ToString("0.00")+" < "+still.ToString("0.00")+" < "+pushed.ToString("0.00")+")");
        }
        private static void Physics()
        {
            // One thrown piece: pure in t, lands, bounces, slows and stops; metal rings longer than concrete.
            V3 p0=new V3(0,1,0),v0=new V3(3,2,0);
            float Height(NativeStructureMaterial m,float t){NativeStructureFx.Tumble(p0,v0,NativeStructureFx.Gravity,0,t,NativeStructureFx.Bounce(m),NativeStructureFx.Grip(m),out V3 p,out _,out _);return p.y;}
            float first=(v0.y+(float)Math.Sqrt(v0.y*v0.y+2*NativeStructureFx.Gravity*p0.y))/NativeStructureFx.Gravity;
            Check(Height(NativeStructureMaterial.Metal,first*.5f)>1&&Height(NativeStructureMaterial.Metal,first+.08f)>.05f&&Height(NativeStructureMaterial.Metal,first+.08f)>Height(NativeStructureMaterial.Concrete,first+.08f),"a piece flies, lands and bounces, metal higher than concrete");
            for(float t=0;t<5;t+=.01f)foreach(var m in new[]{NativeStructureMaterial.Metal,NativeStructureMaterial.Concrete,NativeStructureMaterial.Timber})Check(Height(m,t)>=-1e-4f,"a piece never falls through the floor");
            NativeStructureFx.Tumble(p0,v0,NativeStructureFx.Gravity,0,3,.3f,.6f,out V3 a,out float airA,out float restA);NativeStructureFx.Tumble(p0,v0,NativeStructureFx.Gravity,0,9,.3f,.6f,out V3 b,out float airB,out float restB);
            Check(restA==1&&restB==1&&Math.Abs(a.x-b.x)<.01f&&a.y==0&&b.y==0&&Math.Abs(airA-airB)<.01f,"a piece comes to rest and stays (no drift, no spin)");
            NativeStructureFx.Tumble(p0,v0,NativeStructureFx.Gravity,0,.2f,.3f,.6f,out _,out _,out float early);Check(early==0,"a piece in flight is not at rest");
            NativeStructureFx.Tumble(p0,v0,NativeStructureFx.Gravity,0,1.234f,.3f,.6f,out V3 c1,out float air1,out _);NativeStructureFx.Tumble(p0,v0,NativeStructureFx.Gravity,0,1.234f,.3f,.6f,out V3 c2,out float air2,out _);
            Check(c1.Equals(c2)&&air1==air2,"the tumble is pure in t");
            Check(Math.Abs(NativeStructureFx.Stand(new V3(1,.2f,.6f),0,0,0,1)-.1f)<1e-5f&&Math.Abs(NativeStructureFx.Stand(new V3(1,.2f,.6f),0,0,.70710678f,.70710678f)-.5f)<1e-4f,"a box stands half its height over the floor, on end half its length");
            // A whole break: nothing sinks into the floor before the first pieces start to fade (1.7 s), and the solid pieces have
            // settled flat by 2.3 s and stay put.
            foreach(NativeStructureMaterial m in Enum.GetValues(typeof(NativeStructureMaterial)))
            {
                var fx=new NativeStructureFx();fx.Break(m,0,new V3(0,.6f,0),new V3(2,1.2f,2),0,11,0,false);
                NativeStructureFx.BurstCounts(m,NativeStructureFx.BurstScale(new V3(2,1.2f,2)),out int solid,out _,out _,out _);
                for(float t=.05f;t<1.69f;t+=.05f){fx.Frame(t,Eye,1);for(int i=0;i<fx.CubeCount;i++){var c=fx.Cubes[i];Check(c.Center.y-NativeStructureFx.Stand(c.Size,c.Qx,c.Qy,c.Qz,c.Qw)>=-2e-3f,m+" piece "+i+" is not sunk into the floor at "+t.ToString("0.00")+" s");}}
                fx.Frame(2.3f,Eye,1);var at=fx.Cubes.Take(solid).ToArray();fx.Frame(2.45f,Eye,1);int moved=0,flat=0;
                for(int i=0;i<at.Length;i++){var c=fx.Cubes[i];if(Math.Abs(c.Center.x-at[i].Center.x)+Math.Abs(c.Center.z-at[i].Center.z)>.02f)moved++;if(Math.Abs(c.Center.y-c.Size.y*.5f)<.02f)flat++;}
                Check(fx.CubeCount>=at.Length&&moved<=at.Length/10&&flat>=at.Length*8/10,m+" pieces have settled flat on the floor ("+moved+" still moving, "+flat+" of "+at.Length+" lying flat)");
                Check(fx.FlatCount>0||m==NativeStructureMaterial.Glass||m==NativeStructureMaterial.Masonry,m+" big pieces sit on a soft shadow");
            }
        }
        private static NativeStructureFx Slammed(float weight,NativeStructureMaterial m,float t,float quality=1,bool marks=false)
        {var fx=new NativeStructureFx();fx.Slam(m,0,new V3(0,0,0),0,weight,new V3(0,0,1),7,0,marks);fx.Frame(t,Eye,quality);return fx;}
        private static void Slams()
        {
            Check(NativeSlamDebris.Weapon("greatsword")==1&&NativeSlamDebris.Weapon("dev-greatsword")==1&&NativeSlamDebris.Weapon("shield")<1&&NativeSlamDebris.Weapon("shield")>NativeSlamDebris.Weapon("sword")&&NativeSlamDebris.Weapon("katana")==NativeSlamDebris.Weapon("sword")&&NativeSlamDebris.Weapon("glaive")==NativeSlamDebris.Weapon("sword")&&NativeSlamDebris.Weapon("sword")>NativeSlamDebris.Weapon("fangs")&&NativeSlamDebris.Weapon("fangs")==NativeSlamDebris.Weapon("daggers")&&NativeSlamDebris.Weapon("daggers")>0,"weight follows the weapon: greatsword, shield, sword and katana and glaive, fangs and daggers");
            Check(NativeSlamDebris.Plunge("greatsword",1)==1&&NativeSlamDebris.Plunge("greatsword",0)>=NativeSlamDebris.RingFrom&&NativeSlamDebris.Plunge("fangs",0)<.2f&&NativeSlamDebris.Plunge("fangs",1)<NativeSlamDebris.RingFrom&&NativeSlamDebris.Plunge("sword",1)>NativeSlamDebris.Plunge("sword",0),"a plunge's weight rises with the fall; a greatsword's always rings dust, a Fangs landing never does");
            Check(NativeSlamDebris.Jump("greatsword",1)>NativeSlamDebris.Jump("greatsword",0)&&NativeSlamDebris.Jump("greatsword",0)>NativeSlamDebris.Jump("fangs",1),"a jump attack's weight follows the weapon and the impact");
            Check(NativeSlamDebris.Chunks(1)==14&&NativeSlamDebris.Chunks(.25f)<=6&&NativeSlamDebris.Chunks(0)==0&&NativeSlamDebris.Stones(1)<=18&&NativeSlamDebris.Edge(1)<=.34f&&NativeSlamDebris.Edge(.25f)<NativeSlamDebris.Edge(1),"counts stay modest: at most 14 chunks and 18 stones, .34 m the biggest");
            var heavy=Slammed(1,NativeStructureMaterial.Concrete,.2f);var light=Slammed(.25f,NativeStructureMaterial.Concrete,.2f);
            float Biggest(NativeStructureFx fx)=>fx.Cubes.Take(fx.CubeCount).Max(c=>Math.Max(c.Size.x,Math.Max(c.Size.y,c.Size.z)));
            float Far(NativeStructureFx fx)=>fx.Cubes.Take(fx.CubeCount).Max(c=>(float)Math.Sqrt(c.Center.x*c.Center.x+c.Center.z*c.Center.z));
            Check(heavy.LiveSlams==1&&light.LiveSlams==1&&heavy.CubeCount>light.CubeCount*2&&light.CubeCount>=5&&Biggest(heavy)>Biggest(light)*1.8f&&heavy.Cubes.Take(heavy.CubeCount).All(c=>c.Shape!=NativeDebrisShape.Block),"a heavy slam throws more and bigger chunks than a light one ("+heavy.CubeCount+" up to "+Biggest(heavy).ToString("0.00")+" m against "+light.CubeCount+" up to "+Biggest(light).ToString("0.00")+" m), none a cube");
            Check(heavy.Cubes.Take(heavy.CubeCount).Any(c=>c.Shape==NativeDebrisShape.Rock)&&heavy.Cubes.Take(heavy.CubeCount).Any(c=>c.Shape==NativeDebrisShape.Pebble),"a concrete floor throws rough chunks and stones");
            Check(Far(Slammed(1,NativeStructureMaterial.Concrete,1.2f))>Far(Slammed(.25f,NativeStructureMaterial.Concrete,1.2f))+.5f,"a heavy slam throws its debris farther");
            Check(heavy.PuffCount>light.PuffCount+6&&light.PuffCount>=2&&heavy.Flats.Take(heavy.FlatCount).Any(f=>f.Lit&&f.Soft),"dust from every slam; the heaviest roll a dust ring out over a pale shock ("+heavy.PuffCount+" puffs against "+light.PuffCount+")");
            Check(NativeStructureFx.SlamRing(1,.5f)>NativeStructureFx.SlamRing(.6f,.5f)&&NativeStructureFx.SlamRing(1,.5f)<=4.5f&&NativeStructureFx.SlamRing(1,0)<.5f&&Slammed(1,NativeStructureMaterial.Concrete,NativeStructureFx.RingLife+.6f).PuffCount<heavy.PuffCount,"the ring is short: out to at most 4.4 m in half a second, gone in "+NativeStructureFx.RingLife+" s");
            var metal=Slammed(1,NativeStructureMaterial.Metal,.1f);
            Check(metal.LineCount>=12&&metal.Lines.Take(metal.LineCount).All(l=>l.Additive)&&metal.GlowCount==1&&metal.PuffCount==0&&metal.Cubes.Take(metal.CubeCount).Any(c=>c.Shape==NativeDebrisShape.Panel),"a metal floor sparks and flashes and throws bent scraps, no dust");
            Check(Slammed(1,NativeStructureMaterial.Timber,.2f).Cubes.Take(8).All(c=>c.Shape==NativeDebrisShape.Plank||c.Shape==NativeDebrisShape.Splinter),"a wooden floor throws boards and splinters");
            Check(Slammed(1,NativeStructureMaterial.Concrete,.2f,.3f).CubeCount<heavy.CubeCount&&Slammed(1,NativeStructureMaterial.Concrete,.2f,.3f).PuffCount<heavy.PuffCount,"reduced effects and the low tier thin the chunks and the dust");
            Check(Slammed(1,NativeStructureMaterial.Concrete,.2f,1,true).LiveMarks>=5&&Slammed(.25f,NativeStructureMaterial.Concrete,.2f,1,true).LiveMarks>=3&&heavy.LiveMarks==0,"a marking slam chips and cracks the floor, more for a heavier one");
            var twice=new NativeStructureFx();for(int i=0;i<3;i++)twice.Slam(NativeStructureMaterial.Concrete,0,new V3(0,0,0),0,1,new V3(),7,0,true);int marks=twice.LiveMarks;twice.Slam(NativeStructureMaterial.Concrete,0,new V3(0,0,0),0,1,new V3(),8,0,false);
            Check(twice.LiveSlams==2&&twice.LiveMarks==marks,"a slam fed again (the killcam, a re-read) never doubles");
            var later=new NativeStructureFx();later.Slam(NativeStructureMaterial.Concrete,0,new V3(0,0,0),0,.5f,new V3(),9,2,true);later.Frame(1.9f,Eye,1);Check(later.CubeCount==0&&later.PuffCount==0&&later.FlatCount==0&&later.LiveSlams==1,"a slam born later waits (the fissure's bursts along its front)");
            later.Frame(2.2f,Eye,1);Check(later.CubeCount>0,"and bursts at its time");
            var none=new NativeStructureFx();none.Slam(NativeStructureMaterial.Concrete,0,new V3(0,0,0),0,0,new V3(),9,0,true);Check(none.LiveSlams==0&&none.LiveMarks==0,"no weight, no slam");
            var gone=Slammed(1,NativeStructureMaterial.Concrete,NativeStructureFx.PieceLife);Check(gone.CubeCount==0&&gone.LiveBursts==0,"a slam's debris fades and is freed");
            // Aimed: the debris leans along the blow.
            float z=0;var aimed=Slammed(1,NativeStructureMaterial.Concrete,1);for(int i=0;i<aimed.CubeCount;i++)z+=aimed.Cubes[i].Center.z;Check(z/aimed.CubeCount>.3f,"the debris leans along the blow");
        }
        private static void Loose()
        {
            var size=new V3(.7f,.42f,.7f);var at=new V3(4,1,2);
            var fx=new NativeStructureFx();fx.Frame(0,Eye,1);fx.Loose(NativeStructureMaterial.Concrete,0x808080,at,size,0,0,0,1,5);
            Check(fx.CubeCount==1&&fx.Cubes[0].Shape==NativeDebrisShape.Rock&&fx.Cubes[0].Center.Equals(at)&&fx.Cubes[0].Size.Equals(size)&&Math.Abs(fx.Cubes[0].R-128/255f)<1e-4f,"a stone fragment is one rough chunk filling its own box, in its colour");
            fx.Frame(0,Eye,1);fx.Loose(NativeStructureMaterial.Masonry,0,at,size,0,0,0,1,5);Check(fx.CubeCount==1&&fx.Cubes[0].Shape==NativeDebrisShape.Rock,"a brick fragment too");
            fx.Frame(0,Eye,1);fx.Loose(NativeStructureMaterial.Timber,0,at,size,0,0,0,1,5);
            Check(fx.CubeCount==4&&fx.Cubes.Take(4).All(c=>c.Shape==NativeDebrisShape.Plank&&c.Size.z>c.Size.x*1.5f&&c.Size.y<c.Size.x&&Math.Abs(c.Center.x-at.x)<=size.x*.5f&&Math.Abs(c.Center.y-at.y)<=size.y*.5f&&Math.Abs(c.Center.z-at.z)<=size.z*.5f&&c.Size.x<=size.x&&c.Size.y<=size.y&&c.Size.z<=size.z),"a timber fragment is a clump of four torn boards inside its box");
            fx.Frame(0,Eye,1);fx.Loose(NativeStructureMaterial.Metal,0,at,size,0,0,0,1,5);
            Check(fx.CubeCount==2&&fx.Cubes.Take(2).All(c=>c.Shape==NativeDebrisShape.Panel&&c.Size.x<=size.x&&c.Size.y<=size.y&&c.Size.z<=size.z),"a metal fragment is two bent panels inside its box");
            // Turned with the fragment.
            fx.Frame(0,Eye,1);fx.Loose(NativeStructureMaterial.Timber,0,at,size,0,.70710678f,0,.70710678f,5);var turned=fx.Cubes[0];fx.Frame(0,Eye,1);fx.Loose(NativeStructureMaterial.Timber,0,at,size,0,0,0,1,5);var plain=fx.Cubes[0];
            Check(Math.Abs((turned.Center.z-at.z)+(plain.Center.x-at.x))<1e-4f&&Math.Abs(turned.Qy)>.5f,"the pieces turn with the fragment");
        }
        private static void Pure()
        {
            NativeStructureFx Make(){var fx=new NativeStructureFx();fx.Break(NativeStructureMaterial.Timber,0,new V3(0,.6f,0),new V3(2,1.2f,2),0,11,0,false,new V3(1,0,0));fx.Slam(NativeStructureMaterial.Concrete,0,new V3(3,0,0),0,1,new V3(0,0,1),7,.1f,true);fx.Slam(NativeStructureMaterial.Metal,0,new V3(-3,0,0),0,.5f,new V3(),8,.2f,false);return fx;}
            var a=Make();var b=Make();
            foreach(float t in new[]{.3f,1.1f,2.7f,4f})
            {
                a.Frame(t,Eye,1);b.Frame(.05f,Eye,1);b.Frame(t,Eye,1);bool same=a.CubeCount==b.CubeCount&&a.PuffCount==b.PuffCount&&a.FlatCount==b.FlatCount&&a.LineCount==b.LineCount;
                for(int i=0;same&&i<a.CubeCount;i++)same=a.Cubes[i].Equals(b.Cubes[i]);Check(same&&(a.CubeCount>0||t>=4f),"a second store (the killcam, a guest) draws the same frame at "+t+" s");
            }
            var fx=Make();for(int i=0;i<4;i++)fx.Frame(.4f+i*.1f,Eye,1);fx.Loose(NativeStructureMaterial.Timber,0,new V3(1,1,1),new V3(.7f,.4f,.7f),0,0,0,1,3);
            long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<40;i++){fx.Frame(.8f+i*.05f,Eye,1);fx.Loose(NativeStructureMaterial.Timber,0,new V3(1,1,1),new V3(.7f,.4f,.7f),0,0,0,1,3);fx.Loose(NativeStructureMaterial.Concrete,0,new V3(2,1,1),new V3(.7f,.4f,.7f),0,0,0,1,4);}
            fx.Slam(NativeStructureMaterial.Concrete,0,new V3(5,0,0),0,1,new V3(0,0,1),17,2.5f,true);fx.Frame(2.8f,Eye,1);long used=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(used==0,"realistic frames, slams and loose fragments allocate nothing once warm ("+used+" B)");
            int most=0;var busy=new NativeStructureFx();for(int i=0;i<NativeStructureFx.BurstSlots+6;i++)busy.Slam(NativeStructureMaterial.Concrete,0,new V3(i,0,0),0,1,new V3(),(uint)(100+i),i*.01f,false);
            for(float t=.2f;t<1;t+=.2f){busy.Frame(t,Eye,1);most=Math.Max(most,busy.CubeCount);}
            Check(busy.LiveSlams==NativeStructureFx.BurstSlots&&most<=NativeStructureFx.CubeCap&&most<=NativeStructureFx.BurstSlots*32,"slams are pooled in the burst slots and stay inside the piece cap ("+most+" pieces from "+NativeStructureFx.BurstSlots+" slams)");
        }
        // The fissure's bursts stay modest: one about every 1.5 m, at most eight, light after the first.
        private static bool ArenaPresentation_FissureStep(string slam)=>slam.Contains("FissureStep=1.5f,FissureWeight=.38f;public const int FissureBursts=8;")&&slam.Contains("j==0?1:FissureWeight");
        private static void Hooks(string root)
        {
            string status=File.ReadAllText(Path.Combine(root,"Assets","ChipArena","Runtime","NativeStatusPresentation.cs"));
            Check(status.Contains("bool drops=(structureFx!=null?structureFx.Fx.Style:NativeDebrisLook.Default)==NativeDebrisStyle.Realistic;")&&status.Contains("Add(slowDrops?m*dropLocal:m);")&&status.Contains("var bead=drops?statusMeshes[2]:cube;"),"the slow marker is beads in the realistic style and the cubes on a voxel map");
            Check(File.ReadAllText(Path.Combine(root,"Assets","ChipArena","Runtime","NativeStructureFxPresentation.cs")).Contains("Color lt=PieceLight(Light(n))"),"pieces take the tamed light; the cubes keep theirs");

            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets","ChipArena",f)).Replace("\r","");
            string kit=R("Runtime/NativeStructureFxPresentation.cs"),slam=R("Runtime/NativeSlamDebrisPresentation.cs"),air=R("Runtime/NativeAirAttackFxPresentation.cs"),dress=R("Runtime/NativeArenaDressing.cs"),core=R("Core/NativeStructureFx.cs"),shapes=R("Core/NativeDebrisShapes.cs"),plunge=R("Core/NativePlungeFx.cs");
            Check(kit.Contains("fx.Style=NativeDebrisLook.For(arena.MapId,debris);")&&kit.Contains("debris=look?.debris;"),"the kit takes the map's style from its look every frame");
            Check(kit.Contains("if(fx.Cubes[i].Shape==NativeDebrisShape.Block)ShadedCube(solid,fx.Cubes[i]);else ShadedPiece(solid,fx.Cubes[i]);"),"cubes draw as cubes, pieces as their meshes");
            Check(kit.Contains("fx.Frame(now,V(camera.transform.position),quality);Fragments(arena);")&&kit.Contains("fx.Loose(NativeStructureFx.Classify(material),")&&kit.Contains("box.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;box.forceRenderingOff=false;")&&kit.Contains("r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;r.forceRenderingOff=dressedMap;")&&!kit.Contains("new List<"),"loose fragments draw as pieces over a shadow-only box, and the voxel style gives the boxes back");
            Check(dress.Contains("if(p.Fragment&&NativeStructureFxPresentation.ShapedFragments)continue;"),"the dressed blocks leave a shaped fragment's box out");
            Check(kit.Contains("decals,push);")&&kit.Contains("p.Seat.id==e.Source"),"a break leans away from the pilot who struck it");
            Check(air.Contains("NativePlungeFx.Structures(st,b,t,fm,fc);")&&air.Contains("NativeStructureFxPresentation.FloorKind(arena,")&&air.Contains("NativeSlamDebris.Jump(p.Seat.spec,b.Strength)")&&air.Contains("SlamDebris(arena);"),"the plunge and the jump attack throw the floor's own chunks, and the kit slams run");
            Check(plunge.Contains("st.Slam(material,color,at,b.Floor,NativeSlamDebris.Plunge(b.Spec,b.Heavy),")&&plunge.Contains("if(voxel){if(b.Heavy>=RubbleFrom)st.Break(NativeStructureMaterial.Concrete,RubbleColor,"),"the plunge keeps its cube rubble for the voxel style");
            Check(slam.Contains("k.FissureAt+along/k.FissureSpeed")&&slam.Contains("q.Kind==\"ground-slam\"&&q.Phase==\"slam\"")&&slam.Contains("q.Kind==\"sunder\"&&q.Phase==\"smash\"")&&slam.Contains("e.Kind==EventKind.Land")&&slam.Contains("e.WallSlam")&&ArenaPresentation_FissureStep(slam),"the fissure, the kit landings, the wall slam, Ground Slam and Sunder throw floor debris");
            // A host that sends no frame never sets the codec's HasTechnique, so Ground Slam and Sunder read the record itself
            // (they threw nothing offline and on the host once).
            Check(slam.Contains("var q=a.Technique??NativeTechniques.Of(a);")&&!slam.Contains("HasTechnique?"),"the technique slams read the record, not the codec's presence flag");
            // Cosmetic only: no collider, body, object, light or sound of its own, and nothing sent or simulated.
            foreach(var (name,text) in new[]{("slam presentation",slam),("piece shapes",shapes)})
                foreach(string banned in new[]{"Collider","Rigidbody","AddComponent","new GameObject","CreatePrimitive","AudioSource","new Light","Emit(","Damage(","NetworkFrame","Physics."})Check(!text.Contains(banned),"the "+name+" has no "+banned);
            foreach(string banned in new[]{"UnityEngine","Rigidbody","Collider"})Check(!core.Contains(banned),"the kit's rules stay pure (no "+banned+")");
            foreach(string f in new[]{"Runtime/NativeDestruction.cs","Runtime/NativeStructures.cs","Runtime/NativeNetworkFrame.cs","Runtime/NativeMelee.cs","Runtime/NativeMeleeAbilities.cs","Runtime/NativeMobilityTechniques.cs","Runtime/NativePriorityTechniques.cs"})
                Check(!R(f).Contains("NativeDebrisLook")&&!R(f).Contains("NativeDebrisShape")&&!R(f).Contains("NativeDebrisStyle")&&!R(f).Contains("NativeSlamDebris")&&!R(f).Contains("ShapedFragments"),"the authority ("+Path.GetFileName(f)+") knows nothing of the debris style");
        }
    }
}
