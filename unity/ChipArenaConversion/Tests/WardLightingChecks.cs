#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // Ward Zero's relighting (owner, 2026-10-15: "too bright"; lower the brightness, keep it visible, light it from the ceiling
    // lights and the alarms). Against the imported map (UnityMaps.json "siren" and the Expedition's "expedition-ward-zero"):
    // the base fill (ambient, the key, fog, background, haze) is below the frozen values it had; the ceiling light field (Core
    // NativeWardZeroField) bakes deterministically, makes pools with darker gaps, stops at walls and decks, bounces through the
    // open space and keeps a floor; every walkable nav node on both floors keeps a readable light in every mood (ambient, the
    // key and the rim, the field and the practical slots the picker gives there, with their beams) while the ward as a whole
    // is darker than the old troffer slots made it; the emergency and the alarms throw red, turning beams (the ward's beacons,
    // the shutters' alarms, the hazard kit's beacons through the slot merge); the shader block matches the Core; the runtime
    // hooks bake off the main thread and switch the field off for every other map and stage; nothing allocates per frame.
    internal static class WardLightingChecks
    {
        private static int count;private static readonly List<string> facts=new List<string>();
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Ward lighting: "+what);count++;}
        // The quarantine look as it was before the relighting (7009887).
        private const float OldKey=.14f,OldSkyScale=.12f,OldEquatorScale=.1f,OldGroundScale=.12f,OldFogEnd=110;private const int OldFog=0x0B1418,OldBackground=0x05080B,OldHaze=0x1C2E30;
        public static void Run(string root)
        {
            count=0;facts.Clear();var o=new JsonSerializerOptions{IncludeFields=true};string U(string f)=>Path.Combine(root,"Assets/Conversion/Resources",f);
            var set=JsonSerializer.Deserialize<MapLookSet>(File.ReadAllText(U("MapLooks.json")),o);NativeMapLooks.Validate(set);var look=NativeMapLooks.ByLook(set,NativeWardZeroLooks.Look);
            var maps=JsonSerializer.Deserialize<UnityMapContent>(File.ReadAllText(U("UnityMaps.json")),o);var entries=JsonSerializer.Deserialize<NativeWardEntrySet>(File.ReadAllText(U("PveContent.json")),o).entries;
            Base(look);Synthetic();Moods();
            foreach(var id in new[]{"siren","expedition-ward-zero"})
            {
                var map=maps.maps.Single(m=>m.id==id);Check(NativeWardZeroLooks.Active(map),id+" carries the quarantine look");
                var kit=NativeWardZeroKit.Build(map.blocks,map.hazards,entries);var field=Bake(kit,map);
                Map(id,map,kit,field);Readable(id,map,kit,field,look);
            }
            Alarms(root,maps.maps.Single(m=>m.id=="siren"),entries);Shader(root);Hooks(root);
            Console.WriteLine("PASS ward lighting ("+count+" checks: the base fill below 7009887's; the ceiling light field deterministic, pools with darker gaps, walls and decks shade it, bounce and a floor; every walkable node on both floors readable in every mood, darker than the troffer slots overall; red emergency circuits and turning alarm beams from the ward and the hazard kit; shader block equals the Core; bake off the main thread, off on every other map and stage; no per-frame allocation; "+string.Join(", ",facts)+")");
        }
        private static double Lum(int hex)=>NativeSceneryBuilder.Channel(hex,16)*.2126+NativeSceneryBuilder.Channel(hex,8)*.7152+NativeSceneryBuilder.Channel(hex,0)*.0722;
        private static double Lum(MapLookColor c)=>NativeEntranceLight.ToLinear(c.r)*.2126+NativeEntranceLight.ToLinear(c.g)*.7152+NativeEntranceLight.ToLinear(c.b)*.0722;
        private static NativeWardZeroField Bake(NativeWardZeroKit kit,ContentMap map)=>NativeWardZeroField.Build(kit,map.blocks,map.bounds.minX,map.bounds.maxX,map.bounds.minZ,map.bounds.maxZ);

        // The flat fill is lower than it was; the rim the characters read by stays.
        private static void Base(MapLook q)
        {
            Check(q.key.intensity<OldKey&&q.key.shadows=="none","the moon key is lower ("+q.key.intensity+" < "+OldKey+")");
            var a=q.ambient;Check(a.skyScale<OldSkyScale&&a.equatorScale<OldEquatorScale&&a.groundScale<OldGroundScale,"every ambient band is lower");
            double before=Lum(a.sky)*OldSkyScale+Lum(a.equator)*OldEquatorScale+Lum(a.ground)*OldGroundScale,after=Lum(a.sky)*a.skyScale+Lum(a.equator)*a.equatorScale+Lum(a.ground)*a.groundScale;
            Check(after<=before*.8,"the ambient is at least a fifth lower ("+(after/before).ToString("F2")+" of before)");
            Check(Lum(q.fog.color)<Lum(OldFog)&&q.fog.end<=OldFogEnd&&q.fog.mode=="linear","the fog is darker and no further out");
            Check(Lum(q.background)<Lum(OldBackground)&&Lum(q.sky.horizon)<Lum(OldFog)&&Lum(q.sky.zenith)<Lum(OldBackground)&&q.sky.variant=="none","the background and sky are darker");
            Check(Lum(q.blocks.hazeColor)<Lum(OldHaze)&&q.blocks.haze>0,"the ground haze is darker (it never lifts the dark)");
            Check(q.rim.intensity>=.2f&&q.rim.shadows=="none","the rim the characters read by stays");
            var st=new MapLookState();NativeMapLooks.Applied(q,0,false,st);Check(Math.Abs(st.KeyIntensity-q.key.intensity)<1e-6&&Lum(st.AmbientSky)<Lum(MapLookColor.Of(q.ambient.sky).Scaled(OldSkyScale)),"Apply writes the lower values");
            facts.Add("ambient "+(after/before).ToString("F2")+"x, key "+q.key.intensity+" (was "+OldKey+")");
        }
        // A troffer and a wall, a deck, and a room with no light: the bake's geometry rules on a map of its own.
        private static void Synthetic()
        {
            NativeWardGlow Lamp(float x,float y,float z,float floor,byte kind=NativeWardZeroKit.GlowFluor,float seed=.5f)=>new NativeWardGlow{X=x,Y=y,Z=z,Floor=floor,Kind=kind,Param=seed,R=1,G=1,B=1,Size=1.5f,Hazard=-1,Entry=-1};
            ContentBlock Block(string id,float x,float y,float z,float sx,float sy,float sz)=>new ContentBlock{id=id,pos=new V3(x,y,z),size=new V3(sx,sy,sz)};
            // Two troffers 4.8 m apart under a 5.6 m ceiling, a wall 3 m east of the second, a closed room beyond it, a deck at
            // 5.6-6 m over the west half with an upper troffer above it, and a dead fixture.
            var kit=new NativeWardZeroKit{Glows=new[]{Lamp(-2.4f,5.5f,0,0),Lamp(2.4f,5.5f,0,0),Lamp(-6,12.9f,0,6),Lamp(4,5.5f,4,0,NativeWardZeroKit.GlowFluor,.01f)}};
            var blocks=new[]{Block("t-wall",5.6f,2.8f,0,.4f,5.6f,20),Block("t-wall-b",9,2.8f,0,.4f,5.6f,20),Block("t-deck",-6,5.8f,0,8,.4f,20),Block("t-tank-1",0,1.5f,3,1,3,1)};
            var f=NativeWardZeroField.Build(kit,blocks,-12,12,-8,8);var g=NativeWardZeroField.Build(kit,blocks,-12,12,-8,8);
            Check(f.Fixtures==3&&f.Data.SequenceEqual(g.Data)&&f.Data.Length==f.Nx*f.Nz*NativeWardZeroField.Slices*4,"a small bake: three live fixtures (the dead one out), the same twice");
            float At(float x,float y,float z,float ny=1){f.Sample(x,y,z,0,ny,0,out float a,out float b,out float c,out float d);return a+b+c+d;}
            float under=At(2.4f,0,0),gap=At(0,0,0),east=At(4.6f,0,0),room=At(7.3f,0,0);
            Check(under>2.5f*gap,"a pool under each troffer, darker between them ("+under.ToString("F2")+" vs "+gap.ToString("F2")+")");
            Check(room<=NativeWardZeroField.Floor*1.01f&&room>=NativeWardZeroField.Floor*.99f&&east>1.5f*room,"a wall shades the closed room behind it, which keeps only the floor's faint light ("+room.ToString("F3")+" vs "+east.ToString("F3")+")");
            Check(At(-6,0,0)<.25f*At(-6,6,0)&&At(-6,6,0)>.5f,"the deck shades the floor under it from the troffer over it");
            // Past the pools' edge (no pool there): more than the spill and the floor, the bounce.
            float spill=0;foreach(var x in new[]{-2.4f,2.4f})spill+=NativeWardZeroField.Falloff(x,5.46f,0,5.5f,5.5f,5.5f*NativeWardZeroField.TrofferReach,4.6f,.25f,0);
            Check(east>NativeWardZeroField.Floor+spill+.01f,"the pools' bounce reaches past their edge ("+east.ToString("F3")+" over "+(NativeWardZeroField.Floor+spill).ToString("F3")+")");
            // The shader's facing: a wall takes Side, a ceiling Down of what a floor takes at the same point.
            Check(Math.Abs(NativeWardZeroField.Facing(1)-1)<1e-6&&Math.Abs(NativeWardZeroField.Facing(0)-NativeWardZeroField.Side)<1e-6&&Math.Abs(NativeWardZeroField.Facing(-1)-NativeWardZeroField.Down)<1e-6,"facing: floor 1, wall Side, ceiling Down");
            // The tank (glass) and thin blocks leave the light through.
            Check(At(0,0,4.2f)>.5f*At(0,0,-4.2f),"glass tanks do not shade the field");
        }
        private static void Moods()
        {
            var lv=new float[4];var calm=NativeWardZeroMood.Preset("calm");var emer=NativeWardZeroMood.Preset("emergency");var horde=NativeWardZeroMood.Preset("horde");
            for(double t=0;t<30;t+=.25){NativeWardZeroField.Levels(calm,t,lv);Check(lv.All(v=>Math.Abs(v-lv[0])<1e-6&&v>.9f),"calm: every circuit steady at full ("+t+" s)");}
            int dips=0;for(double t=0;t<60;t+=.1){NativeWardZeroField.Levels(emer,t,lv);foreach(var v in lv){Check(v>=0&&v<=1.2f,"levels in range");if(v<.3f)dips++;}}
            Check(dips>10&&dips<2400*.6,"an emergency browns circuits out now and then ("+dips+" circuit-samples of 2400)");
            NativeWardZeroField.Colour(calm,out float cr,out float cg,out float cb);NativeWardZeroField.Colour(emer,out float er,out float eg,out float eb);
            Check(cb>=cr&&cg>=cr*.9f,"calm: clinical white");Check(er>2.5f*eg&&er>1.5f*eb,"emergency: the field turns red");
            var steady=emer;steady.Flicker=0;NativeWardZeroField.Levels(steady,0,lv);Check(lv[0]>=emer.Fluor+.3f,"the emergency circuits stay on ("+lv[0].ToString("F2")+")");
            // The fixtures' surfaces drop with their circuit (the pools and the tubes agree).
            int agree=0;for(double t=0;t<20;t+=.05)for(int c=0;c<4;c++){float b=NativeWardZeroField.Brownout(c,emer,t);if(b>=1)continue;float seed=SeedOf(c);if(NativeWardZeroKit.FluorLevel(seed,emer,t)<=emer.Fluor*1.2f*b+1e-4f)agree++;}
            Check(agree>0,"a fixture on a browned-out circuit dims with its pool");
            Check(NativeWardZeroField.Brownout(0,calm,5)==1&&NativeWardZeroField.Brownout(1,horde,5)<=1,"brownouts only when the mood fails");
            long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<200;i++){NativeWardZeroField.Levels(i%2==0?calm:emer,i*.1,lv);NativeWardZeroField.Colour(emer,out _,out _,out _);NativeWardZeroField.Brownout(i&3,emer,i);}
            Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"levels and colour allocate nothing");
        }
        // A seed on circuit c that is neither dead nor flickering on its own (for the brownout agreement).
        private static float SeedOf(int c){for(float s=.7f;s<1;s+=.001f)if(NativeWardZeroField.Circuit(s)==c)return s;throw new Exception("no seed on circuit "+c);}
        private static void Map(string id,ContentMap map,NativeWardZeroKit kit,NativeWardZeroField f)
        {
            var g=Bake(kit,map);int lamps=kit.Glows.Count(x=>(x.Kind==NativeWardZeroKit.GlowFluor||x.Kind==NativeWardZeroKit.GlowHighBay)&&x.Param>=NativeWardZeroKit.DeadShare);
            Check(f.Data.SequenceEqual(g.Data)&&f.Nx==g.Nx&&f.Nz==g.Nz,id+": the bake is deterministic (threads and all)");
            Check(f.Fixtures==lamps&&lamps>250&&f.Nx*f.Nz*NativeWardZeroField.Slices*4==f.Data.Length,id+": every live troffer and high-bay ("+lamps+")");
            Check(f.MinX<=map.bounds.minX&&f.MinZ<=map.bounds.minZ&&f.MinX+f.Nx*NativeWardZeroField.Cell>=map.bounds.maxX&&f.MinZ+f.Nz*NativeWardZeroField.Cell>=map.bounds.maxZ,id+": the box covers the map");
            Check(f.Data.All(v=>v>=0&&!float.IsNaN(v)&&!float.IsInfinity(v)),id+": finite and never negative");
            facts.Add(id+" field "+f.Nx+"x"+f.Nz+"x"+NativeWardZeroField.Slices+" from "+lamps+" fixtures, bake "+f.BakeMs.ToString("F0")+" ms");
            // The north hall: pools under its troffers, darker between them.
            var hall=kit.Glows.Where(x=>x.Kind==NativeWardZeroKit.GlowFluor&&x.Param>=.1f&&Math.Abs(x.Z-16.4f)<.3f&&x.Y<6&&x.X<-20).OrderBy(x=>x.X).ToArray();
            Check(hall.Length>=3,id+": troffers down the north hall");
            float At(float x,float z){f.Sample(x,0,z,0,1,0,out float a,out float b,out float c,out float d);return a+b+c+d;}
            float pool=0,gap=0;for(int i=0;i+1<hall.Length;i++){pool+=At(hall[i].X,hall[i].Z);gap+=At((hall[i].X+hall[i+1].X)/2,hall[i].Z);}
            Check(pool>1.8f*gap,id+": the north hall's floor has pools and darker gaps ("+(pool/gap).ToString("F1")+"x)");
            long before=GC.GetAllocatedBytesForCurrentThread();float s=0;for(int i=0;i<500;i++){f.Sample(i%60-30,i%2==0?0:6,i%40-20,0,1,0,out float a,out _,out _,out _);s+=a;}
            Check(GC.GetAllocatedBytesForCurrentThread()-before==0&&s>0,id+": sampling allocates nothing");
        }
        // Every walkable nav node, both floors, in every mood: a floor and an actor's light from the base, the field and the
        // slots the picker gives at that spot (with their beams); and the ward as a whole darker than the old troffer slots.
        private static void Readable(string id,ContentMap map,NativeWardZeroKit kit,NativeWardZeroField f,MapLook look)
        {
            var st=new MapLookState();NativeMapLooks.Applied(look,map.sky,false,st);double amb=Lum(st.AmbientSky),ambE=Lum(st.AmbientEquator);
            var states=map.hazards.Select(h=>NativeWardZeroHazards.Evaluate(h,1,10,12)).ToArray();var lights=NativeWardZeroLights.Build(kit,true);var old=NativeWardZeroLights.Build(kit);
            var nodes=map.nav.nodes;Check(nodes.Length>2000&&nodes.Any(n=>n.layer==0)&&nodes.Any(n=>n.layer==1),id+": walkable nodes on both floors");
            double Slots(NativeWardZeroLights l,double x,double y,double z,double nx,double ny,double nz,bool wrap)
            {
                double sum=0;
                for(int s=0;s<8;s++)
                {
                    double dx=l.Pos[s*4]-x,dy=l.Pos[s*4+1]-y,dz=l.Pos[s*4+2]-z,d=Math.Sqrt(dx*dx+dy*dy+dz*dz),q=d/l.Pos[s*4+3],at=q<1?(1-q*q)*(1-q*q):0;if(at<=0)continue;
                    double ndl=(nx*dx+ny*dy+nz*dz)/d;ndl=wrap?Math.Max(0,(ndl+.35)/1.35):Math.Max(0,ndl);
                    sum+=(l.Rgb[s*3]*.2126+l.Rgb[s*3+1]*.7152+l.Rgb[s*3+2]*.0722)*at*ndl*NativeWardZeroLights.BeamFactor(l.Beam[s],-dx,-dz);
                }
                return sum;
            }
            var floorMin=new Dictionary<string,double>{["calm"]=.03,["horde"]=.025,["boss"]=.015,["emergency"]=.008};var actorMin=new Dictionary<string,double>{["calm"]=.015,["horde"]=.012,["boss"]=.008,["emergency"]=.004};
            var lv=new float[4];double calmNew=0,calmOld=0;
            foreach(var mood in new[]{"calm","horde","boss","emergency"})
            {
                var m=NativeWardZeroMood.Preset(mood);var steady=m;steady.Flicker=0;NativeWardZeroField.Levels(steady,12,lv);NativeWardZeroField.Colour(m,out float fr,out float fg,out float fb);double fl=fr*.2126+fg*.7152+fb*.0722;
                var floor=new List<double>();var actor=new List<double>();var olds=new List<double>();string darkest="";double low=1e9;
                foreach(var n in nodes)
                {
                    double x=n.pos.x,y=n.pos.y-.9,z=n.pos.z;lights.Settle(x,y+1.7,z,m,states,12);
                    f.Sample((float)x,(float)y,(float)z,0,1,0,out float c0,out float c1,out float c2,out float c3);double e=amb+(c0*lv[0]+c1*lv[1]+c2*lv[2]+c3*lv[3])*fl+Slots(lights,x,y,z,0,1,0,false);
                    floor.Add(e);if(e<low){low=e;darkest="("+x.ToString("F1",CultureInfo.InvariantCulture)+", "+y.ToString("F1",CultureInfo.InvariantCulture)+", "+z.ToString("F1",CultureInfo.InvariantCulture)+")";}
                    double a=0;for(int k=0;k<4;k++){float nx=(float)Math.Sin(k*Math.PI/2),nz=(float)Math.Cos(k*Math.PI/2);f.Sample((float)x,(float)y+1.2f,(float)z,nx,0,nz,out c0,out c1,out c2,out c3);a+=((c0*lv[0]+c1*lv[1]+c2*lv[2]+c3*lv[3])*fl+Slots(lights,x,y+1.2,z,nx,0,nz,true))/4;}
                    actor.Add(ambE+a);
                    if(mood=="calm"){old.Settle(x,y+1.7,z,m,states,12);olds.Add(amb*OldSkyScale/look.ambient.skyScale+Slots(old,x,y,z,0,1,0,false));}
                }
                floor.Sort();actor.Sort();
                Check(floor[0]>=floorMin[mood],id+" "+mood+": every walkable floor keeps a readable light (darkest "+floor[0].ToString("F4")+" at "+darkest+")");
                Check(actor[0]>=actorMin[mood],id+" "+mood+": a body standing anywhere keeps a readable light, before its rim and trims ("+actor[0].ToString("F4")+")");
                if(mood=="calm"){olds.Sort();calmNew=floor[floor.Count/2];calmOld=olds[olds.Count/2];Check(floor[floor.Count*19/20]<olds[olds.Count*19/20]*.75,id+": the brightest floors are well below the old troffer slots'");}
                if(id=="siren")facts.Add(mood+" floor min "+floor[0].ToString("F3")+" median "+floor[floor.Count/2].ToString("F3"));
            }
            Check(calmNew<=calmOld*.7,id+": the calm ward's median floor is at least 30% darker than the old troffer slots made it ("+calmNew.ToString("F3")+" vs "+calmOld.ToString("F3")+")");
            if(id=="siren")facts.Add("calm median floor "+(calmNew/calmOld).ToString("F2")+"x the old slots'");
        }
        private static void Alarms(string root,ContentMap map,NativeWardEntry[] entries)
        {
            var kit=NativeWardZeroKit.Build(map.blocks,map.hazards,entries);var l=NativeWardZeroLights.Build(kit,true);var fallback=NativeWardZeroLights.Build(kit);
            Check(!l.Candidates.Any(c=>c.Kind==NativeWardZeroKit.GlowFluor||c.Kind==NativeWardZeroKit.GlowHighBay)&&fallback.Candidates.Any(c=>c.Kind==NativeWardZeroKit.GlowFluor),"with the field the slots leave the troffers to it (the fallback keeps them)");
            Check(l.Candidates.Count(c=>c.Kind==NativeWardZeroKit.GlowStrobe&&c.Hazard<0)>=8&&l.Candidates.Any(c=>c.Kind==NativeWardZeroKit.GlowStrobe&&c.Alarm==1)&&l.Candidates.Any(c=>c.Kind==NativeWardZeroKit.GlowTank)&&l.Candidates.Any(c=>c.Kind==NativeWardZeroKit.GlowFurnace),"beacons, shutter alarms, tanks and furnaces keep their slots");
            var calm=NativeWardZeroMood.Preset("calm");var emer=NativeWardZeroMood.Preset("emergency");var states=map.hazards.Select(h=>NativeWardZeroHazards.Evaluate(h,1,10,12)).ToArray();
            double Red(NativeWardZeroLights x){double r=0,g=0;for(int s=0;s<8;s++){r+=x.Rgb[s*3];g+=x.Rgb[s*3+1];}return r/(g+1e-6);}
            l.Settle(0,7.7,10,emer,states,12);double redE=Red(l);int beams=Enumerable.Range(0,8).Count(s=>l.Slot[s]>=0&&l.Beam[s]>=1&&l.Beam[s]<1+Math.PI*2+1e-3&&l.Candidates[l.Slot[s]].Kind==NativeWardZeroKit.GlowStrobe);
            l.Settle(0,7.7,10,calm,states,12);double redC=Red(l);
            Check(redE>2*redC&&beams>=2,"an emergency's beacons take slots and throw red beams ("+beams+" beams, red "+redE.ToString("F1")+" vs "+redC.ToString("F1")+")");
            var beacon=l.Candidates.First(c=>c.Kind==NativeWardZeroKit.GlowStrobe&&c.Hazard<0);float b0=NativeWardZeroLights.BeamOf(beacon,states,12),b1=NativeWardZeroLights.BeamOf(beacon,states,12.2);
            Check(b0>=1&&b0<1+Math.PI*2+1e-3&&Math.Abs(b1-b0)>.5f,"a beacon's beam turns ("+b0.ToString("F2")+" -> "+b1.ToString("F2")+")");
            double yaw=b0-1;double ahead=NativeWardZeroLights.BeamFactor(b0,Math.Sin(yaw),Math.Cos(yaw)),behind=NativeWardZeroLights.BeamFactor(b0,-Math.Sin(yaw),-Math.Cos(yaw)),side=NativeWardZeroLights.BeamFactor(b0,Math.Cos(yaw),-Math.Sin(yaw));
            Check(Math.Abs(ahead-1)<1e-4&&Math.Abs(behind-NativeWardZeroLights.BeamFloor)<1e-4&&Math.Abs(side-NativeWardZeroLights.BeamFloor)<1e-4,"one beam: full along it, the lamp's glow behind and beside");
            Check(Math.Abs(NativeWardZeroLights.BeamFactor(8+(float)yaw,-Math.Sin(yaw),-Math.Cos(yaw))-1)<1e-4&&NativeWardZeroLights.BeamFactor(0,1,0)==1,"two beams light both ways; a point light is plain");
            // A shutter's alarm turns two beams while it warns and closes, nothing at rest.
            int sh=Array.FindIndex(map.hazards,h=>h.kind==NativeWardZeroHazards.Shutter);var alarm=l.Candidates.First(c=>c.Kind==NativeWardZeroKit.GlowStrobe&&c.Alarm==1&&c.Hazard==sh);var st=(NativeHazardState[])states.Clone();
            double warnAt=-1;for(double t=10;t<120&&warnAt<0;t+=.05)if(NativeWardZeroHazards.Evaluate(map.hazards[sh],1,10,t).Phase==NativeHazardPhase.Warn)warnAt=t;Check(warnAt>0,"the shutter warns in round 1");
            st[sh]=NativeWardZeroHazards.Evaluate(map.hazards[sh],1,10,warnAt+.2);Check(NativeWardZeroLights.BeamOf(alarm,st,warnAt+.2)>=8,"a warning shutter's alarm turns two beams");
            double Sweeps(NativeWardLight c,NativeHazardState[] x,double t,int beams){double d=NativeWardZeroLights.BeamOf(c,x,t+.01)-NativeWardZeroLights.BeamOf(c,x,t);if(d<-Math.PI)d+=Math.PI*2;return beams*d/.01/(Math.PI*2);}
            double sw1=Sweeps(beacon,states,12.3,1),sw2=Sweeps(alarm,st,warnAt+.3,2);Check(sw1>.5&&sw1<=3&&sw2>.5&&sw2<=3,"a wall sees at most 3 sweeps a second ("+sw1.ToString("F1")+" from a beacon, "+sw2.ToString("F1")+" from a shutter's alarm)");
            st[sh]=default;Check(NativeWardZeroLights.BeamOf(alarm,st,warnAt)==0,"at rest it throws none");
            // The hazard kit's beacons: a lockdown turns their two beams through the slots.
            var kset=JsonSerializer.Deserialize<KitSet>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/HazardKit.json")),new JsonSerializerOptions{IncludeFields=true});
            var records=NativeHazardKit.For(kset,map.id,map.look);var scene=new NativeKitScene(records);int fired=scene.Fire("lockdown",30,false,default,"siren-shutter-n");
            scene.Step(31,NativeKitFlash.Full,NativeKitBeat.Off,false);scene.Build(0,1.7,8,1,0,0,0,1,0,1,false);
            var kl=Enumerable.Range(0,scene.LightCount).Select(i=>scene.Lights[i]).Where(x=>x.Beam>=8).ToArray();
            Check(fired>0&&kl.Length>0&&kl.All(x=>x.Beam<8+Math.PI*2+1e-3&&x.R>x.G&&x.R>x.B),"a lockdown's beacon throws two turning beams in its lamp's colour");
            var pool=new NativeKitLightPool();pool.Update(scene,0,1.7,8,0);Check(pool.Count>0&&Enumerable.Range(0,pool.Count).Any(i=>pool.Beam[i]>=8),"the kit's slots carry the beams");
            float[] pos=new float[32],rgb=new float[24],beam=new float[8];for(int i=0;i<8;i++){pos[i*4+1]=3;pos[i*4+3]=8;rgb[i*3]=1;beam[i]=2;}rgb[5*3]=.001f;
            int took=NativeKitLightPool.Merge(pos,rgb,8,pool.Pos,pool.Rgb,1,0,2,0,beam,pool.Beam);Check(took==1&&beam[5]==pool.Beam[0]&&beam[0]==2,"the merge hands a taken slot the kit light's beam and leaves the others'");
            long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<60;i++){l.Update(Math.Sin(i*.1)*30,i%2==0?1.7:7.7,Math.Cos(i*.1)*20,i%2==0?calm:emer,states,12+i/60.0,1f/60);NativeWardZeroLights.BeamOf(beacon,states,i);}
            Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"the picker with beams allocates nothing");
        }
        // The shader block (Resources/NativeWardLight.cginc) is the Core's, and both shaders use it as the Core says.
        private static void Shader(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Resources",f));var c=R("NativeWardLight.cginc");var block=R("NativeBlockLit.shader");var src=R("NativeSourceLit.shader");
            float D(string n){var m=Regex.Match(c,@"#define "+n+@" ([0-9.]+)\r?\n");Check(m.Success,n+" defined");return float.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture);}
            Check(D("WF_SPLIT")==NativeWardZeroField.Split&&D("WF_OFFSET")==NativeWardZeroField.Offset&&D("WF_FLAT")==NativeWardZeroField.FlatOffset&&D("WF_SIDE")==NativeWardZeroField.Side&&D("WF_DOWN")==NativeWardZeroField.Down,"the field's offsets and facing match the Core");
            var y=NativeWardZeroField.SliceY;Check(D("WF_Y0")==y[0]&&D("WF_Y1")==y[1]&&D("WF_Y3")==y[3]&&D("WF_Y4")==y[4]&&D("WF_Y5")==y[5]&&D("WF_Y7")==y[7]&&NativeWardZeroField.Slices==8&&c.Contains("(s+0.5)/8.0"),"the slices match the Core");
            Check(Math.Abs(y[2]-y[1]-(y[1]-y[0]))<1e-5&&Math.Abs(y[3]-y[2]-(y[1]-y[0]))<1e-5&&Math.Abs(y[6]-y[5]-(y[5]-y[4]))<1e-5&&Math.Abs(y[7]-y[6]-(y[5]-y[4]))<1e-5,"each floor's slices are evenly spaced (the texture interpolates them)");
            Check(D("WB_FLOOR")==NativeWardZeroLights.BeamFloor&&c.Contains("float c2=c*c;return WB_FLOOR+(1.0-WB_FLOOR)*c2*c2*c2;")&&c.Contains("float two=step(7.5,w);float yaw=w-1.0-7.0*two;"),"the beam matches NativeWardZeroLights.BeamFactor");
            Check(c.Contains("return tex3Dlod(_WardField,float4(uvw,0.0));")&&c.Contains("if(_WardFieldColor.w<0.5)return float3(0.0,0.0,0.0);")&&c.Contains("if(w<0.5)return 1.0;"),"one volume read without derivatives; unset globals (0) leave both off");
            Check(c.Contains("float3 p=world+n*(WF_OFFSET*(1.0-(1.0-WF_FLAT)*abs(n.y)));")&&c.Contains("float k=n.y>=0.0?WF_SIDE+(1.0-WF_SIDE)*n.y:WF_SIDE+(WF_DOWN-WF_SIDE)*(-n.y);")&&c.Contains("float s=world.y<WF_SPLIT?"),"the shader reads the field as NativeWardZeroField.Sample does");
            string shared=block.Substring(block.IndexOf("// SHARED BEGIN",StringComparison.Ordinal),block.IndexOf("// SHARED END",StringComparison.Ordinal)-block.IndexOf("// SHARED BEGIN",StringComparison.Ordinal));
            Check(shared.Contains("#include \"NativeWardLight.cginc\"")&&shared.Contains("+WardField(world,n);")&&shared.Contains("*(a*a*saturate(dot(n,d/len)))*WardBeam(d,_ArenaLightColor[k].w);"),"NativeBlockLit adds the field and the beams to its light block");
            Check(src.Contains("float _ArenaActorLit;float4 _ArenaLightPos[8];float4 _ArenaLightColor[8];")&&src.Contains("#include \"NativeWardLight.cginc\"")&&src.Contains("if(_ArenaActorLit>.5&&_StageLit<.5&&!Unlit(i.color.rgb,i.emission.rgb))o.Emission+=o.Albedo*ActorLights(i.worldPos,normalize(i.worldNormal));"),"SourceLit lights bodies with the slots and the field only when the Ward turns it on, never on a stage, an unlit part or the ink");
        }
        private static void Hooks(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));var lit=R("NativeLighting.cs");var p=R("NativeWardZeroPresentation.cs");var kit=R("NativeHazardKitPresentation.cs");
            Check(lit.Contains("currentMap=map;arenaPicked=false;WardFieldOff();")&&Regex.Matches(lit,@"WardFieldOff\(\);EnterStage\(\);").Count==3&&lit.Contains("WardFieldOff();camera.clearFlags=CameraClearFlags.Skybox;"),"a new map look, every entrance, intro and draft frame, and the rooftop turn the field and actor lighting off");
            Check(lit.Contains("if(introActive||volume==null){WardFieldOff();return;}")&&lit.Contains("Shader.SetGlobalFloat(ActorLitId,actors?1:0);"),"the field stays off while a stage owns the screen");
            Check(lit.Contains("arenaColor[i]=new Vector4(c.r,c.g,c.b,beam!=null&&i<beam.Length?beam[i]:0);")&&lit.Contains("NativeKitLightPool.Merge(mergePos,mergeRgb,slots,kitPos,rgb,kitCount,kitEye.x,kitEye.y,kitEye.z,mergeBeam,kitBeam);"),"the slots carry their beams through the kit merge");
            Check(p.Contains("wzBake=System.Threading.Tasks.Task.Run(()=>NativeWardZeroField.Build(kit,blocks,x0,x1,z0,z1));")&&p.Contains("TextureFormat.RGBAHalf")&&p.Contains("filterMode=FilterMode.Bilinear")&&p.Contains("wrapMode=TextureWrapMode.Clamp"),"the bake runs off the main thread into a bilinear, clamped half-float volume");
            Check(p.Contains("wzFieldTex=tex;f.Data=Array.Empty<float>();wzField=f;wzLights=NativeWardZeroLights.Build(wzKit,true);")&&p.Contains("wzLights=NativeWardZeroLights.Build(wzKit);"),"the troffers leave the slots only once the field is in");
            Check(p.Contains("NativeLighting.PickedLights(wzLights.Pos,wzLights.Rgb,wzLights.Beam)")&&p.Contains("WardZeroFieldSend(now,true);")&&p.Contains("WardZeroFieldSend(time,false);"),"live and killcam light the bodies; the menu's cut-away does not");
            Check(p.Contains("if(!ReferenceEquals(wzBakeMap,wzMap))return;")&&p.Contains("ReleaseWardField();NativeLighting.WardFieldOff();"),"a bake for an old map is dropped; Dispose releases the volume");
            Check(kit.Contains("NativeLighting.KitLights(kitLights.Pos,kitLights.Rgb,kitLights.Count,e,kitLights.Beam);"),"the hazard kit hands its beams to the slots");
            Check(!File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Core/NativeWardZeroField.cs")).Contains("UnityEngine"),"NativeWardZeroField is engine-free Core");
        }
    }
}
#endif
