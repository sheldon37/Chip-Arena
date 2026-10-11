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
    // Per-map looks (Resources/MapLooks.json, Core/NativeMapLooks.cs). For the 15 old maps every value NativeLighting now
    // applies is computed by the very function NativeLighting.Apply calls (Core NativeMapLooks.Applied) and compared, field by
    // field and bit for bit, to Tests/map-looks-parity.json (Tests/MapLookOracle.cs run once at ce3c506, before the move) and
    // to the oracle itself. The entrance and draft stages own the fog and the shadows (on every old map those equal the map's).
    // The one intended difference is Cinder Crossing's sky: the volcanic horizon and zenith now follow the forge theme, as
    // src/client/night-world.ts does (`volcanic=m.theme==='forge'`), where ce3c506 keyed them on the id "forge". Also: the
    // fallback for maps without an entry equals each entry, the sky shader's property defaults are the old constants,
    // Brickfall's golden-demolition preset is ARENA_PROPOSAL.md 3.10 as written, flicker and fall-off, and validation.
    internal static class MapLookChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Map looks: "+what);count++;}
        private static void Same(float a,float b,string what)=>Check(BitConverter.SingleToInt32Bits(a)==BitConverter.SingleToInt32Bits(b),what+": "+a.ToString("R",CultureInfo.InvariantCulture)+" != "+b.ToString("R",CultureInfo.InvariantCulture));
        private static void Same(float[] a,float[] b,string what){Check(a!=null&&b!=null&&a.Length==b.Length,what+" length");for(int i=0;i<a.Length;i++)Same(a[i],b[i],what+"["+i+"]");}
        private static void Near(double a,double b,double tolerance,string what)=>Check(Math.Abs(a-b)<=tolerance,what+": "+a+" != "+b);
        // What NativeLighting.Create then Apply(map) now leave behind: Core NativeMapLooks.Applied, the function Apply calls, in
        // the oracle's terms (colours as Unity's Color holds them; a rim with no direction keeps Create's).
        private static OldMapLight Applied(MapLook look,ContentMap map)
        {
            var a=new MapLookState();NativeMapLooks.Applied(look,map.sky,false,a);float[] C(MapLookColor c)=>new[]{c.r,c.g,c.b,c.a};float[] T(MapLookTint t)=>t.Hex==0?new[]{t.R,t.G,t.B}:null;
            var s=new OldMapLight{id=map.id,ambientMode="trilight",ambientSky=C(a.AmbientSky),ambientEquator=C(a.AmbientEquator),ambientGround=C(a.AmbientGround)};
            s.fog=a.Fog.On;s.fogMode=!a.Fog.On?"none":a.Fog.Mode==MapFogMode.Linear?"linear":a.Fog.Mode==MapFogMode.Exponential?"exp":"exp2";s.shadowCascades=a.Cascades;s.shadowDistance=a.ShadowDistance;
            s.keyIntensity=a.KeyIntensity;s.keyShadows=a.KeyShadows;s.keyStrength=a.KeyStrength;s.rimIntensity=a.RimIntensity;s.rimShadows=a.RimShadows;s.rimFrom=a.RimAim.Has?null:NativeMapLooks.RigRim;
            s.farClip=a.FarClip;s.background=C(a.Background);s.clear=a.Skybox?"skybox":"solid";s.skybox=a.Skybox;s.fogColor=C(a.Fog.Color);s.fogStart=a.Fog.Start;s.fogEnd=a.Fog.End;
            s.horizon=C(a.Horizon);s.zenith=C(a.Zenith);s.moonSize=a.MoonSize;s.keyColor=C(a.KeyColor);s.keyFrom=a.KeyAim.From?new[]{a.KeyAim.X,a.KeyAim.Y,a.KeyAim.Z}:null;s.rimColor=C(a.RimColor);
            s.sunDir=new[]{a.SunX,a.SunY,a.SunZ};s.halo=T(a.Halo);s.haloFalloff=a.HaloFalloff;s.disc=T(a.Disc);s.discShade=new[]{a.DiscBase,a.Crater};s.stars=a.Stars;s.cloud=T(a.Cloud);s.cloudSpeed=a.CloudSpeed;
            return s;
        }
        private static void Compare(OldMapLight a,OldMapLight e,string what,bool electronSky)
        {
            Check(a.clear==e.clear&&a.fogMode==e.fogMode&&a.ambientMode==e.ambientMode&&a.keyShadows==e.keyShadows&&a.rimShadows==e.rimShadows&&a.fog==e.fog&&a.skybox==e.skybox&&a.shadowCascades==e.shadowCascades,what+" modes");
            Same(a.farClip,e.farClip,what+" far clip");Same(a.fogStart,e.fogStart,what+" fog start");Same(a.fogEnd,e.fogEnd,what+" fog end");Same(a.moonSize,e.moonSize,what+" moon size");
            Same(a.keyIntensity,e.keyIntensity,what+" key intensity");Same(a.keyStrength,e.keyStrength,what+" key shadow strength");Same(a.rimIntensity,e.rimIntensity,what+" rim intensity");Same(a.shadowDistance,e.shadowDistance,what+" shadow distance");
            Same(a.haloFalloff,e.haloFalloff,what+" halo falloff");Same(a.stars,e.stars,what+" stars");Same(a.cloudSpeed,e.cloudSpeed,what+" cloud speed");
            Same(a.background,e.background,what+" background");Same(a.fogColor,e.fogColor,what+" fog colour");Same(a.keyColor,e.keyColor,what+" key colour");Same(a.keyFrom,e.keyFrom,what+" key position");
            Same(a.rimColor,e.rimColor,what+" rim colour");Same(a.rimFrom,e.rimFrom,what+" rim position");Same(a.ambientSky,e.ambientSky,what+" ambient sky");Same(a.ambientEquator,e.ambientEquator,what+" ambient equator");Same(a.ambientGround,e.ambientGround,what+" ambient ground");
            Same(a.sunDir,e.sunDir,what+" sky direction");Same(a.halo,e.halo,what+" halo colour");Same(a.disc,e.disc,what+" disc colour");Same(a.discShade,e.discShade,what+" disc shade");Same(a.cloud,e.cloud,what+" cloud tint");
            if(electronSky){Same(a.horizon,OldMapLighting.ColorOf(0x633345),what+" volcanic horizon");Same(a.zenith,OldMapLighting.ColorOf(0x100b1c),what+" volcanic zenith");Check(!a.horizon.SequenceEqual(e.horizon)&&!a.zenith.SequenceEqual(e.zenith),what+" sky changed from ce3c506");}
            else{Same(a.horizon,e.horizon,what+" horizon");Same(a.zenith,e.zenith,what+" zenith");}
        }
        private static OldMapLight Fixture(JsonElement m)
        {
            float F(string n)=>m.GetProperty(n).GetSingle();float[] A(string n)=>m.GetProperty(n).EnumerateArray().Select(x=>x.GetSingle()).ToArray();string S(string n)=>m.GetProperty(n).GetString();bool B(string n)=>m.GetProperty(n).GetBoolean();
            return new OldMapLight{id=S("id"),clear=S("clear"),fogMode=S("fogMode"),ambientMode=S("ambientMode"),keyShadows=S("keyShadows"),rimShadows=S("rimShadows"),fog=B("fog"),skybox=B("skybox"),
                farClip=F("farClip"),fogStart=F("fogStart"),fogEnd=F("fogEnd"),moonSize=F("moonSize"),keyIntensity=F("keyIntensity"),keyStrength=F("keyStrength"),rimIntensity=F("rimIntensity"),haloFalloff=F("haloFalloff"),stars=F("stars"),cloudSpeed=F("cloudSpeed"),shadowDistance=F("shadowDistance"),shadowCascades=m.GetProperty("shadowCascades").GetInt32(),
                background=A("background"),fogColor=A("fogColor"),horizon=A("horizon"),zenith=A("zenith"),keyColor=A("keyColor"),keyFrom=A("keyFrom"),rimColor=A("rimColor"),rimFrom=A("rimFrom"),ambientSky=A("ambientSky"),ambientEquator=A("ambientEquator"),ambientGround=A("ambientGround"),
                sunDir=A("sunDir"),halo=A("halo"),disc=A("disc"),discShade=A("discShade"),cloud=A("cloud")};
        }
        internal static MapLookSet Load(string root)=>JsonSerializer.Deserialize<MapLookSet>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/MapLooks.json")),new JsonSerializerOptions{IncludeFields=true});
        public static IEnumerable<string> Run(string root,ConversionContent content)
        {
            var set=Load(root);NativeMapLooks.Validate(set);
            var fixture=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/map-looks-parity.json"))).RootElement;Check(fixture.GetProperty("commit").GetString()=="ce3c506","fixture commit");
            var fixtures=fixture.GetProperty("maps").EnumerateArray().Select(Fixture).ToDictionary(x=>x.id);Check(fixtures.Count==15&&content.maps.Length==15,"15 maps in the fixture and the content");
            int sameBefore=count;
            foreach(var map in content.maps)
            {
                Check(fixtures.TryGetValue(map.id,out var e),map.id+" in the fixture");
                Compare(OldMapLighting.Evaluate(map),e,map.id+" oracle vs fixture",false);
                var entry=NativeMapLooks.Find(set,map.id);Check(entry!=null,map.id+" has a MapLooks entry");
                Compare(Applied(entry,map),e,map.id+" data vs ce3c506",map.id=="lavaworks");
                Compare(Applied(NativeMapLooks.Fallback(map.id,map.theme,map.sky,map.accent),map),e,map.id+" fallback vs ce3c506",map.id=="lavaworks");
                Check(entry.lights.Length==0&&entry.blockLooks.Length==0&&entry.dressing==""&&entry.backdrop==""&&entry.atmosphere==""&&entry.ambience==""&&!NativeBlockLooks.Dressed(entry,false),map.id+" stubs empty, blocks not dressed");
                Check(entry.shadows.fallbackCascades==entry.shadows.cascades&&entry.shadows.fallbackDistance==entry.shadows.distance,map.id+" no shadow change on the low tier");
                Check(!NativeMapLooks.HasDirection(entry.rim)&&NativeMapLooks.HasFrom(entry.key),map.id+" key from today's position, rim on the rig's direction");
            }
            int values=count-sameBefore;
            Check(NativeMapLooks.RigRim.SequenceEqual(new[]{14f,7f,12f})&&NativeMapLooks.RigKey.SequenceEqual(new[]{-8f,20f,-12f}),"Create's rig positions");
            string lighting=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeLighting.cs"));
            string Between(string from,string to){int i=lighting.IndexOf(from,StringComparison.Ordinal),j=lighting.IndexOf(to,i+1,StringComparison.Ordinal);Check(i>=0&&j>i,"NativeLighting has "+from);return lighting.Substring(i,j-i);}
            string apply=Between("public static void Apply(","/// <summary>The graphics tier's shadows");
            Check(apply.Contains("NativeMapLooks.Applied(look,map.sky,NativeArenaDressing.Preview,s);")&&!apply.Contains("look.fog")&&!apply.Contains("look.key")&&!apply.Contains("look.rim")&&!apply.Contains("look.ambient")&&!apply.Contains("look.sky"),"Apply writes what NativeMapLooks.Applied computes, and nothing it computes itself");
            Check(lighting.Contains("Rig(NativeMapLooks.RigKey)")&&lighting.Contains("Rig(NativeMapLooks.RigRim)"),"Create aims from the Core rig positions");
            yield return "map looks: the 15 old maps' lighting, fog, shadows and sky from MapLooks.json, through the Core mapping Apply calls, equal ce3c506 bit for bit ("+values+" values; Cinder Crossing's sky follows Electron's forge theme)";
            // The entrance and draft stages own the fog and the shadows: on every old map the pinned values are the map's own, on
            // Brickfall they are not (so nothing of its look leaks into an entrance or the draft); EndIntro gives back the fog found.
            var draftFog=NativeMapLooks.DraftFog;var introFog=NativeMapLooks.StageFog(MapLookColor.Of(.1f,.2f,.3f),9,26);
            Check(draftFog.On&&draftFog.Mode==MapFogMode.Linear&&!draftFog.WritesDensity&&draftFog.Start==30&&draftFog.End==100&&draftFog.Color.r==.025f&&draftFog.Color.g==.032f&&draftFog.Color.b==.055f&&draftFog.Color.a==1,"the draft fogs into its own backdrop from 30 to 100 m (draft-stage.ts fogs to its background)");
            Check(introFog.On&&introFog.Mode==MapFogMode.Linear&&!introFog.WritesDensity&&introFog.Start==9&&introFog.End==26&&introFog.Color.g==.2f,"an entrance's fog is on and linear in the clip's colour and range");
            foreach(var map in content.maps)
            {
                var st=new MapLookState();NativeMapLooks.Applied(NativeMapLooks.Find(set,map.id),map.sky,false,st);
                Check(st.Fog.On==draftFog.On&&st.Fog.Mode==draftFog.Mode&&!st.Fog.WritesDensity,map.id+": its fog is on and linear, as every stage's (an entrance sees what it saw before)");
                Check(st.ShadowCascades(false)==NativeMapLooks.StageCascades&&st.ShadowCascades(true)==NativeMapLooks.StageCascades&&st.Shadows(false)==NativeMapLooks.StageShadowDistance&&st.Shadows(true)==NativeMapLooks.StageShadowDistance,map.id+": its shadows on both tiers are the stages' 2 cascades over 65 m");
                Check(st.KeyShadows==NativeMapLooks.StageKeyShadows&&st.KeyStrength==NativeMapLooks.StageShadowStrength,map.id+": its key is soft at full strength, as the draft pins it");
            }
            {
                var st=new MapLookState();NativeMapLooks.Applied(NativeMapLooks.Find(set,"brickfall"),0,false,st);
                Check(st.Cascades!=NativeMapLooks.StageCascades&&st.ShadowDistance!=NativeMapLooks.StageShadowDistance&&st.KeyStrength!=NativeMapLooks.StageShadowStrength&&(st.Fog.Start!=draftFog.Start||st.Fog.End!=draftFog.End)&&st.RimPixel,"Brickfall's own shadows, key strength and fog differ from the stages', so the pin is what keeps them out (its rim stays per-pixel for the dressed blocks)");
            }
            string intro=Between("public static void Intro(","public static void Clock("),draft=Between("public static void Draft(","private static void Ambient("),end=Between("public static void EndIntro(","// An entrance or the draft takes the screen");
            Check(intro.Contains("EnterStage();")&&intro.Contains("WriteFog(NativeMapLooks.StageFog(MapLookColor.Of(fog.r,fog.g,fog.b),env[e+6],env[e+7]));StageShadows();")&&!intro.Contains("RenderSettings.fog"),"an entrance writes its whole fog and pins the stage shadows every frame");
            Check(draft.Contains("EnterStage();")&&draft.Contains("WriteFog(NativeMapLooks.DraftFog);StageShadows();")&&draft.Contains("camera.backgroundColor=C(NativeMapLooks.DraftBackground);")&&draft.Contains("moon.shadows=LightShadowsOf(NativeMapLooks.StageKeyShadows);moon.shadowStrength=NativeMapLooks.StageShadowStrength;")&&!draft.Contains("RenderSettings.fog"),"the draft writes its whole fog, pins the stage shadows and the key's shadow strength");
            Check(end.Contains("WriteFog(stageFog);Apply(map,camera);")&&lighting.Contains("private static void EnterStage(){if(introActive)return;introActive=true;stageFog=ReadFog();}")&&apply.Contains("WriteFog(s.Fog);if(introActive)stageFog=ReadFog();"),"EndIntro gives back the fog the stage found (density included; an Apply during a stage updates it), then Apply sets the map's look");
            Check(lighting.Contains("Density=RenderSettings.fogDensity")&&lighting.Contains("if(f.WritesDensity)RenderSettings.fogDensity=f.Density;"),"the saved fog keeps the density and the restore writes it");
            yield return "map looks: entrances and the chip draft own the fog (on, linear, their own colour and range) and the shadows (2 cascades over 65 m; the draft's key soft at full strength), restored at EndIntro; every old map's own values are exactly these, Brickfall's are not";
            // The volcanic sky in Electron: both forge-themed maps.
            // The Electron expressions, checked against src/client when the repository is around them (the source package has only the Unity project).
            string world=Path.Combine(root,"../../src/client/night-world.ts"),sceneryFile=Path.Combine(root,"../../src/client/map-scenery.ts");bool electronFiles=File.Exists(world)&&File.Exists(sceneryFile);
            if(electronFiles){var electron=File.ReadAllText(world);Check(electron.Contains("volcanic=m.theme==='forge'")&&electron.Contains("horizon=new T.Color(volcanic?0x633345:cold?0x405677:0x333f64),zenith=new T.Color(volcanic?0x100b1c:0x080e22)"),"night-world.ts keys the volcanic sky on the forge theme");
                Check(File.ReadAllText(sceneryFile).Contains("moon?.color.set(id==='outpost'?0xffc5a5:id==='forge'?0xffac9c:0xb2ceff)"),"map-scenery.ts keys the moon colour on the id");}
            foreach(var map in content.maps.Where(m=>m.theme=="forge")){var s=NativeMapLooks.Find(set,map.id).sky;Check(s.horizon==0x633345&&s.zenith==0x100b1c,map.id+" volcanic sky");}
            Check(NativeMapLooks.Find(set,"lavaworks").key.color==0xb2ceff&&NativeMapLooks.Find(set,"forge").key.color==0xffac9c,"moon colours stay keyed on the id");
            yield return "map looks: Cinder Crossing (forge theme) takes the volcanic sky as night-world.ts does; Obsidian Forge alone keeps the ember moon, as map-scenery.ts"+(electronFiles?" (both read from src/client)":" (src/client not present)");
            // A map with no entry: today's look from the map itself, and resolution order.
            var unknown=new ContentMap{id="test-yard",theme="snow",sky=0x0c1c30,accent=0x80e4ff};var fb=NativeMapLooks.Resolve(set,unknown.id,unknown.theme,unknown.sky,unknown.accent);
            Check(fb.id=="test-yard"&&fb.sky.horizon==0x405677&&fb.rim.color==0x80e4ff&&fb.background==0x0c1c30&&fb.sky.variant=="night","fallback for a map without an entry");
            Check(ReferenceEquals(NativeMapLooks.Resolve(set,"brickfall","brickfall",0,0),NativeMapLooks.Find(set,"brickfall"))&&NativeMapLooks.ByLook(set,"golden-demolition").id=="brickfall","entries win over the fallback; lookup by look name");
            var check=new MapLookSet{schema=1,maps=new[]{fb}};NativeMapLooks.Validate(check);
            yield return "map looks: a map without an entry gets today's look (validated), an entry wins";
            // The sky shader's property defaults are the constants it hard-coded at ce3c506 and the Core's defaults.
            string shader=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Resources/NativeNightSky.shader"));
            float[] Prop(string name){var m=Regex.Match(shader,name+@"\(""[^""]*"",(?:Vector|Float|Color)\)=\(?([-0-9.,]+)\)?");Check(m.Success,"shader property "+name);return m.Groups[1].Value.Split(',').Select(x=>float.Parse(x,CultureInfo.InvariantCulture)).ToArray();}
            Same(Prop("_SunDir").Take(3).ToArray(),NativeMapLooks.SunDirDefault,"_SunDir default");Same(Prop("_DiscColor").Take(3).ToArray(),NativeMapLooks.DiscDefault,"_DiscColor default");Same(Prop("_HaloColor").Take(3).ToArray(),NativeMapLooks.HaloDefault,"_HaloColor default");Same(Prop("_CloudTint").Take(3).ToArray(),NativeMapLooks.CloudDefault,"_CloudTint default");
            Same(Prop("_DiscShade").Take(2).ToArray(),new[]{NativeMapLooks.DiscBaseDefault,NativeMapLooks.CraterDefault},"_DiscShade default");Same(Prop("_HaloFalloff")[0],NativeMapLooks.HaloFalloffDefault,"_HaloFalloff default");Same(Prop("_Stars")[0],NativeMapLooks.StarsDefault,"_Stars default");
            Same(Prop("_CloudSpeed")[0],NativeMapLooks.CloudSpeedDefault,"_CloudSpeed default");Same(Prop("_MoonSize")[0],NativeMapLooks.DiscSizeDefault,"_MoonSize default");
            var old=fixtures["foundry"];Same(NativeMapLooks.SunDirDefault,old.sunDir,"old moon direction");Same(NativeMapLooks.HaloDefault,old.halo,"old halo");Same(NativeMapLooks.DiscDefault,old.disc,"old disc");Same(NativeMapLooks.CloudDefault,old.cloud,"old cloud tint");
            Same(NativeMapLooks.HaloFalloff(new MapLookSky{halo=NativeMapLooks.NightHalo}),12,"night halo size 1/12 gives falloff 12");Same(NativeMapLooks.DiscBase(new MapLookSky{crater=.24f}),.76f,"crater .24 gives base .76");
            // The night maths the old shader did with constants now reads the properties in the same order (defaults change nothing).
            foreach(var expr in new[]{"halo=exp(-distance*_HaloFalloff)","col+=_HaloColor.rgb*halo","_DiscColor.rgb*(_DiscShade.x+_DiscShade.y*fbm(d*170))","smoothstep(.08,.3,d.y)*_Stars","float drift=_SceneTime*_CloudSpeed","drift*.008","drift*.004","lerp(_Horizon.rgb*.85,_CloudTint.rgb,halo)","float3 md=normalize(_SunDir.xyz)"})Check(shader.Contains(expr),"sky shader reads "+expr);
            yield return "map looks: NativeNightSky's new properties default to the ce3c506 constants (direction, disc, crater, halo, stars, clouds) and the night entries resolve to exactly them";
            // Brickfall's golden-demolition preset, ARENA_PROPOSAL.md 3.10 as written.
            var b=NativeMapLooks.Find(set,"brickfall");Check(b!=null&&b.look=="golden-demolition","brickfall preset");
            Check(b.key.color==0xFFC27A&&b.key.intensity==1.2f&&b.key.elevation==30&&b.key.bearing==322&&!NativeMapLooks.HasFrom(b.key)&&b.key.shadows=="soft"&&b.key.shadowStrength==.85f,"sun #FFC27A 1.20, 30 deg, bearing 322, soft .85");
            Check(b.rim.color==0x7FA8FF&&b.rim.intensity==.36f&&b.rim.shadows=="none"&&Math.Abs(b.rim.bearing-(b.key.bearing-180))<1e-4,"rim #7FA8FF .36 opposite the sun, no shadows");
            Check(b.ambient.sky==0x8DA6D6&&b.ambient.skyScale==.5f&&b.ambient.equator==0xC7906E&&b.ambient.equatorScale==.34f&&b.ambient.ground==0x3B2A22&&b.ambient.groundScale==.42f,"trilight");
            Check(b.shadows.cascades==4&&b.shadows.distance==75&&b.shadows.fallbackCascades==2&&b.shadows.fallbackDistance==65,"4 cascades over 75 m, 2 over 65 on the low tier");
            Check(b.fog.mode=="linear"&&b.fog.color==0xCF9A74&&b.fog.skyLerp==0&&b.fog.start==38&&b.fog.end==175,"linear fog #CF9A74 38-175 m");
            var sk=b.sky;Check(sk.variant=="dusk"&&sk.zenith==0x2E4470&&sk.horizon==0xF29A5E&&sk.glow==0xFFC38A&&sk.disc==0xFFE6B8&&sk.discSize==.05f&&sk.halo==.25f&&sk.stars==0&&sk.cloudSpeed==.3f&&sk.crater==0&&sk.cloud!=0,"dusk sky: zenith, horizon, glow, disc .05, halo .25, no stars, warm clouds at .3x");
            Check(sk.elevation==b.key.elevation&&sk.bearing==b.key.bearing,"the sun disc sits where the key light comes from");
            NativeMapLooks.Toward(b.key,out double kx,out double ky,out double kz);Near(kx,-Math.Cos(Math.PI/6)*Math.Sin(38*Math.PI/180),1e-12,"sun x (north-west)");Near(ky,.5,1e-12,"sun y");Near(kz,Math.Cos(Math.PI/6)*Math.Cos(38*Math.PI/180),1e-12,"sun z");Check(kx<0&&kz>0,"bearing 322 is north-west");
            NativeMapLooks.SunDirection(sk,out float sx,out float sy,out float sz);Near(sx,kx,1e-6,"sky sun x");Near(sy,ky,1e-6,"sky sun y");Near(sz,kz,1e-6,"sky sun z");
            Check(b.lights.Length==8,"8 practical lights");var L=b.lights.ToDictionary(l=>l.id);
            foreach(var (id,x,z) in new[]{("sodium-north-west",-18f,25.6f),("sodium-north",0f,25.6f),("sodium-south-east",18f,-25.6f),("sodium-south",0f,-25.6f)})Check(L[id].color==0xFF9A3C&&L[id].pos.x==x&&L[id].pos.y==4.6f&&L[id].pos.z==z&&L[id].flicker=="","sodium lamp "+id);
            var sign=L["mercer-sign"];Check(sign.color==0x4FA8FF&&sign.pos.x==8.6f&&sign.pos.y==7.5f&&sign.pos.z==6.6f&&sign.flicker=="stutter"&&sign.length==.15f&&sign.period==11,"MERCER sign #4FA8FF at (8.6, 7.5, 6.6), .15 s stutter every 11 s");
            var barrels=b.lights.Where(l=>l.id.StartsWith("barrel-")).ToArray();Check(barrels.Length==2&&barrels.All(l=>l.color==0xFF7A2E&&l.flicker=="fire"&&l.rate==6),"two fire barrels #FF7A2E");
            Check(barrels[0].pos.x==-barrels[1].pos.x&&barrels[0].pos.z==-barrels[1].pos.z&&barrels[0].pos.x>-30&&barrels[0].pos.x<-14&&barrels[0].pos.z>-26&&barrels[0].pos.z<-12,"barrels mirrored, the first in the Brick Yard (x -30..-14, z -26..-12)");
            var crane=L["crane-flood"];Check(crane.color==0xFFF1D6&&crane.pos.z>28&&crane.flicker=="","crane floodlight #FFF1D6 outside the north wall");
            Check(Math.Sqrt(Math.Pow(crane.pos.x-0,2)+Math.Pow(crane.pos.y-6,2)+Math.Pow(crane.pos.z-6,2))<crane.range,"the floodlight reaches the hotel's north face");
            Check(b.dressing==""&&b.backdrop==""&&b.atmosphere==""&&b.ambience=="","later passes' stubs empty (block looks: BlockLookChecks)");
            Check(!content.maps.Any(m=>m.id=="brickfall"),"dormant: no brickfall map yet");
            yield return "map looks: golden-demolition is ARENA_PROPOSAL.md 3.10 as written (sun, rim, trilight, shadows, fog, dusk sky, 8 practical lights) and dormant until a brickfall map exists";
            // Flicker and fall-off.
            double lo=1,hi=0;int crossings=0;double prev=0;for(int i=0;i<=20000;i++){double t=i*.0005,m=NativeMapLooks.Flicker(barrels[0],t);lo=Math.Min(lo,m);hi=Math.Max(hi,m);double centred=m-(1-barrels[0].depth*.5);if(i>0&&Math.Sign(centred)!=Math.Sign(prev)&&centred!=0)crossings++;prev=centred;}
            Check(lo>=1-barrels[0].depth-1e-9&&hi<=1+1e-9&&hi-lo>barrels[0].depth*.6,"fire flicker stays within 1 - depth .. 1 and uses most of it");Check(crossings>=2*6*10*.8&&crossings<=2*9*10*1.4,"fire flicker crossings in 10 s fit 6-9 Hz: "+crossings);
            Check(NativeMapLooks.Flicker(barrels[1],1)!=NativeMapLooks.Flicker(barrels[0],1),"the two barrels are out of phase");
            int dark=0,cycles=0;for(int i=0;i<44000;i++){double t=i*.001,m=NativeMapLooks.Flicker(sign,t);double u=t%11;if(m<1){dark++;Check(u<.15&&Math.Abs(m-(1-sign.depth))<1e-12,"sign dips only inside its .15 s window");}if(i%11000==200)cycles+=NativeMapLooks.Flicker(sign,t)<1?0:1;}
            Check(dark>=4*70&&dark<=4*80,"sign stutters about half of each .15 s window, every 11 s: "+dark+" ms");Check(cycles==4,"the sign is lit between dips");
            Check(NativeMapLooks.Flicker(L["sodium-north"],3.3)==1&&!NativeMapLooks.Flickers(L["sodium-north"])&&NativeMapLooks.Flickers(sign),"steady lamps");
            Check(NativeMapLooks.Attenuation(0,10)==1&&NativeMapLooks.Attenuation(10,10)==0&&NativeMapLooks.Attenuation(12,10)==0&&Math.Abs(NativeMapLooks.Attenuation(5,10)-.5625)<1e-12&&NativeMapLooks.Attenuation(1,0)==0,"fall-off (1 - (d/r)^2)^2");
            double last=2;for(int i=0;i<=100;i++){double a=NativeMapLooks.Attenuation(i*.1,10);Check(a<=last,"fall-off decreases");last=a;}
            yield return "map looks: practical lights flicker (fire 6-9 Hz within its depth, the sign's .15 s stutter every 11 s) and fall off smoothly to the range";
            // Validation rejects what the runtime could not apply.
            string text=File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/MapLooks.json"));
            void Reject(Func<MapLookSet,bool> mutate,string what){var bad=Load(root);if(!mutate(bad))throw new Exception("mutation did not apply: "+what);bool threw=false;try{NativeMapLooks.Validate(bad);}catch(InvalidOperationException){threw=true;}Check(threw,"rejects "+what);}
            MapLook Brick(MapLookSet s)=>NativeMapLooks.Find(s,"brickfall");
            Reject(s=>{s.schema=2;return true;},"another schema");Reject(s=>{s.maps[1].id=s.maps[0].id;return true;},"duplicate map ids");Reject(s=>{s.maps[1].look=s.maps[0].look;return true;},"duplicate look names");
            Reject(s=>{var l=Brick(s);l.lights=l.lights.Concat(new[]{l.lights[0]}).ToArray();return true;},"a ninth practical light");Reject(s=>{Brick(s).fog.mode="volumetric";return true;},"an unknown fog mode");
            Reject(s=>{Brick(s).fog.end=10;return true;},"linear fog ending before it starts");Reject(s=>{Brick(s).sky.variant="noon";return true;},"an unknown sky variant");Reject(s=>{Brick(s).shadows.cascades=3;return true;},"3 cascades");
            Reject(s=>{Brick(s).key.elevation=0;Brick(s).key.bearing=0;return true;},"a key light with no direction");Reject(s=>{Brick(s).lights[0].range=0;return true;},"a light with no range");
            Reject(s=>{Brick(s).lights[4].length=12;return true;},"a stutter longer than its period");Reject(s=>{Brick(s).lights[5].rate=0;return true;},"a fire flicker without a rate");Reject(s=>{Brick(s).sky.halo=0;return true;},"a zero halo");
            Reject(s=>{Brick(s).key.color=0x1000000;return true;},"a colour beyond 0xFFFFFF");Reject(s=>{Brick(s).key.shadows="blurry";return true;},"unknown shadows");Reject(s=>{s.maps[0].sky=null;return true;},"a missing section");
            Reject(s=>{Brick(s).sky.stars=float.NaN;return true;},"NaN");Reject(s=>{Brick(s).blockLooks=new[]{new MapLookBlock{block="hotel-",look=""}};return true;},"an empty block look");
            Check(text.Contains("\"look\":\"golden-demolition\"")&&!Regex.IsMatch(text,@"\t"),"MapLooks.json readable");
            yield return "map looks: validation rejects bad schema, ids, light counts, fog, sky, shadows, directions, flicker and colours ("+count+" checks in all)";
        }
    }
}
#endif
