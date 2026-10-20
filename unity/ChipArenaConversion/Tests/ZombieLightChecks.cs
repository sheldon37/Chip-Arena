#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // The zombies under the station's lights (owner, 2026-10-20: "Seems the zombies are appearing almost completely black a lot
    // of the time?", Central Station in the Windows build; "The glowing eyes are still fine").
    // Root cause: a map's practical lights are not Unity lights. The zone presentations hand the eight that matter to the
    // camera to NativeBlockLit's own light block (NativeLighting.PickedLights), so they light the floor and the kit. The source
    // shaders (ZombieLit for the horde, SourceLit for the pilots) add the same slots only while the global _ArenaActorLit is
    // on (NativeWardLight.cginc ActorLights). Ward Zero and the Ironworks turn it on each frame; Central Station never did.
    // There a body had only Unity's lights: the look's moon key (.5, its shadow .9 under the roofs and the vault's glazing),
    // the rim (.14) and an ambient of about .001. Its skin drew at about 3/255 beside a floor its lamps light at about 30/255.
    // The 2026-10-19 gore pass made it show: it took off the glowing seams, cracks and accents and the Siren's flow and
    // lavender rim (about 60% of the body's emission) that had carried the zombies there, while their albedo moved only a
    // few percent (stain: skin -6%, suit -8%).
    // Checks (pure code and static data; no Unity scene or GPU):
    //  - the contract: ZombieLit's passes (a surface shader with Unity's ambient, an add pass for the pixel rim, shadows),
    //    its lighting maths and the actor light hook as this check emulates them, the light block's wrap, NativeLighting's
    //    switch, and each dark zone turning it on in its per-frame scenery (the station after its map gate, outside the eye
    //    test, so the killcam has it too);
    //  - every look with practical lights and a dark base for bodies (its ambient plus half its key, the key at its shadow
    //    strength) is one whose presentation turns actor lighting on, so a new dark zone cannot repeat this;
    //  - the zombies' albedo floor: skin and suit luminance, area-weighted over every look of the seven kinds, after the
    //    occlusion, mottling and stain the shader applies;
    //  - the station, emulated: the live map, area, look, kit and light picker; at its walkable nodes (calm, a camera 6 m
    //    off, the key in shadow), a body's lit skin and suit through ZombieLit's maths beside the floor through
    //    NativeBlockLit's, with actor lighting as the runtime sets it: readable skin, the floor's light, contrast and hue.
    internal static class ZombieLightChecks
    {
        private static int count;private static readonly List<string> facts=new List<string>();
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Zombie light: "+what);count++;}
        /// <summary>A body's base light below this (linear luminance) is too dark to read by Unity's lights alone.</summary>
        private const double DarkBase=.02;
        public static void Run(string root)
        {
            count=0;facts.Clear();
            Contract(root);var set=Looks(root);var albedo=Albedo();Station(root,set,albedo);
            Console.WriteLine("PASS zombie light ("+count+" checks: ZombieLit's passes, lighting and actor hook as emulated; the station, Ward Zero and the Ironworks light their bodies with their practical slots; every dark look with practicals does; the albedo floor; Central Station emulated at its walkable nodes with the key in shadow; "+string.Join("; ",facts)+")");
        }

        // ---------------- the contract ----------------
        private static string Between(string s,string a,string b){int i=s.IndexOf(a,StringComparison.Ordinal);if(i<0)return null;int j=s.IndexOf(b,i+a.Length,StringComparison.Ordinal);return j<0?null:s.Substring(i,j-i);}
        private static void Contract(string root)
        {
            string R(string p)=>File.ReadAllText(Path.Combine(root,p));
            var z=R("Assets/ChipArena/Resources/NativeZombieLit.shader");var cg=R("Assets/ChipArena/Resources/NativeWardLight.cginc");var block=R("Assets/ChipArena/Resources/NativeBlockLit.shader");
            // A surface shader on the built-in forward path: Unity adds the ambient (SH) and the vertex lights to a legacy lighting
            // function's base pass and makes an add pass for the per-pixel rim; the shadows are the key's.
            string pragma=z.Split('\n').FirstOrDefault(l=>l.TrimStart().StartsWith("#pragma surface",StringComparison.Ordinal))?.Trim();
            Check(pragma=="#pragma surface surfZombie Zombie fullforwardshadows addshadow vertex:vert finalcolor:ZombieTone","ZombieLit is the surface shader this check emulates ("+pragma+")");
            foreach(var off in new[]{"noambient","noforwardadd","novertexlights","nolighting","noshadow"})Check(!z.Contains(off),"ZombieLit keeps Unity's "+off.Substring(2)+" (no "+off+")");
            Check(z.Contains("c.rgb=ZombieLight(s.Albedo,s.Normal,lightDir,viewDir,_LightColor0.rgb,atten,s.Gloss,s.Specular);"),"each Unity light goes through ZombieLight with its colour and shadow");
            // The maths this check runs (the shader's own lines).
            foreach(var line in new[]{
                " float ZombieValue(float x){return clamp((x-floor(x)-0.02)/0.96,0.0,1.0);}",
                " float ZombieHash(float3 p){p=frac(p*0.3183099+float3(0.71,0.113,0.419));p=p*17.0;return frac(p.x*p.y*p.z*(p.x+p.y+p.z));}",
                "  float seed=body.g-1.0;float tone=body.r-1.0;","  float3 q=floor(local*12.5+float3(seed*31.0,seed*7.0,seed*17.0));","  float big=ZombieHash(q);float fine=ZombieHash(floor(local*28.0)+q.zxy);",
                "  float3 c=lerp(albedo,albedo*float3(0.72,0.78,0.6),step(0.8,big)*0.8);","  c=lerp(c,albedo*float3(0.84,0.66,0.86),step(0.88,1.0-big)*0.6);","  c=c*(0.96+0.08*step(0.5,fine));",
                "  float3 hue=tone>0.0?float3(1.06,1.0,1.12):float3(0.94,1.04,0.84);","  return c*lerp(float3(1.0,1.0,1.0),hue,min(1.0,abs(tone)*1.6));",
                "  float seed=body.g-1.0;float k=cls>3.5?1.0:cls>2.5?0.7:0.4;","  float3 q=floor(local*14.0+float3(seed*13.0,seed*29.0,seed*5.0));",
                "  float h=ZombieHash(q+float3(3.1,7.7,1.3));float g=ZombieHash(floor(local*30.0)+q.yzx);","  float3 c=lerp(albedo,albedo*float3(0.6,0.52,0.42),step(0.64,h)*0.7*k);",
                "  c=lerp(c,float3(0.15,0.02,0.022)*(0.8+0.4*g),step(0.9,h)*step(0.3,g)*0.85*k);",
                " if(cls<1.5)return float3(0.18,0.08,1.0);"," if(cls<4.5)return float3(0.12,0.0,0.6);",
                "  float ndl=dot(n,l);float lam=max(ndl,0.0);float w=clamp((ndl+wrap)/(1.0+wrap),0.0,1.0);","  float3 diff=albedo*w+albedo*float3(0.3,0.06,0.05)*max(w-lam,0.0)*step(0.35,wrap);",
                "  float power=lerp(14.0,90.0,spec);float3 h=normalize(l+v);float s=pow(max(dot(n,h),0.0),power)*spec*step(0.0,ndl)*(0.5+power*0.012);","  return (diff+float3(s,s,s))*lightColor*atten;",
                "float cls=floor(i.zdata.x);float ao=1-ZombieValue(i.zdata.x);","float3 albedo=i.color.rgb*tint;if(cls>.5&&cls<1.5)albedo=ZombieSkin(albedo,i.localPos,shell.rgb);",
                "if(cls>.5&&cls<4.5&&(cls<1.5||cls>2.5))albedo=ZombieStain(albedo,i.localPos,shell.rgb,cls);","o.Albedo=albedo*ao;o.Gloss=k.x;o.Specular=k.y*ao;"})
                Check(z.Contains(line),"ZombieLit's line as emulated: "+line.Trim());
            // The hook: the slots (and Ward Zero's field) light the body's albedo, never its glow (the eyes keep theirs, unlit).
            Check(z.Contains("float _ArenaActorLit;float4 _ArenaLightPos[8];float4 _ArenaLightColor[8];")&&z.Contains("#include \"NativeWardLight.cginc\"")&&
                z.Contains("if(_InkFill<.5&&_ArenaActorLit>.5&&_StageLit<.5&&!Unlit(i.color.rgb,i.emission.rgb))o.Emission+=o.Albedo*ActorLights(i.worldPos,normalize(i.worldNormal));"),
                "ZombieLit adds the practical slots to the body's albedo while _ArenaActorLit is on (never the eyes' glow, a stage or the ink)");
            Check(cg.Contains("float q=len/max(_ArenaLightPos[k].w,0.0001);float a=saturate(1.0-q*q);")&&cg.Contains("sum+=_ArenaLightColor[k].rgb*(a*a*saturate((dot(n,d/len)+0.35)/1.35))*WardBeam(d,_ArenaLightColor[k].w);"),
                "a body takes each slot wrapped (.35), with the blocks' fall-off and beam");
            var bl=Between(block,"float3 BlockLights(float3 world,float3 n)","// Ground haze");
            Check(bl!=null&&bl.Contains("float3 sum=_ArenaRimColor.rgb*saturate(dot(n,_ArenaRimDir.xyz))+WardField(world,n);")&&bl.Contains("sum+=_ArenaLightColor[k].rgb*(a*a*saturate(dot(n,d/len)))*WardBeam(d,_ArenaLightColor[k].w);")&&
                block.Contains("float3 light=ShadeSH9(float4(nW,1.0))+_LightColor0.rgb*(saturate(dot(nW,_WorldSpaceLightPos0.xyz))*atten)+BlockLights(i.world.xyz,nW);"),"the floor's light as emulated: SH, the shadowed key, the rim and the slots");
            Check(R("Assets/ChipArena/Resources/NativeSourceLit.shader").Contains("if(_ArenaActorLit>.5&&_StageLit<.5&&!Unlit(i.color.rgb,i.emission.rgb))o.Emission+=o.Albedo*ActorLights(i.worldPos,normalize(i.worldNormal));"),"the pilots (SourceLit) take the same slots by the same switch");
            // The switch, and who throws it.
            var lit=R("Assets/ChipArena/Runtime/NativeLighting.cs");
            Check(lit.Contains("public static void ActorLights(bool on){Shader.SetGlobalVector(WardFieldColorId,Vector4.zero);Shader.SetGlobalFloat(ActorLitId,on&&!introActive?1:0);}")&&lit.Contains("ActorLitId=Shader.PropertyToID(\"_ArenaActorLit\")"),
                "NativeLighting.ActorLights sets _ArenaActorLit by a cached id (no allocation), off while a stage owns the screen, with no Ward field");
            Check(lit.Contains("currentMap=map;arenaPicked=false;WardFieldOff();"),"every map's look starts with actor lighting off (Apply), so only a zone that throws it has it");
            Check(!Directory.GetFiles(Path.Combine(root,"Assets/ChipArena"),"*.cs",SearchOption.AllDirectories).Any(f=>File.ReadAllText(f).Contains("lightsUseLinearIntensity=")),"no code changes Unity's light intensity mode (the emulation uses the built-in default, gamma intensity)");
            var station=R("Assets/ChipArena/Runtime/NativeExpeditionStationPresentation.cs");var scenery=Between(station,"private void StationScenery(NativeArena arena)","private bool EnsureStation()");
            const string gate="if(!NativeExpeditionStation.Active(arena.MapId)||map==null||!EnsureStation())return;",picked="NativeLighting.PickedLights(stationLights.Pos,stationLights.Rgb,stationLights.Beam);}",call="\n            NativeLighting.ActorLights(true);\n";
            Check(scenery!=null&&scenery.IndexOf(gate,StringComparison.Ordinal)>0&&scenery.IndexOf(call,StringComparison.Ordinal)>scenery.IndexOf(picked,StringComparison.Ordinal)&&scenery.IndexOf(picked,StringComparison.Ordinal)>scenery.IndexOf(gate,StringComparison.Ordinal),
                "Central Station turns actor lighting on each frame after its map gate, beside its slots and outside the eye test (the arena and the killcam)");
            var iw=R("Assets/ChipArena/Runtime/NativeIronworksPresentation.cs");var send=Between(iw,"private void IronworksFieldSend(double now)","private void ReleaseIronField()");
            Check(Between(iw,"private void IronworksScenery(NativeArena arena)","private bool EnsureIronworks()")?.Contains("IronworksFieldSend(now);")==true&&send!=null&&send.Contains("NativeLighting.ActorLights(true);return;")&&send.Contains("new Color(r,g,b),true);"),
                "the Ironworks light their bodies every frame (the field's actor share, or the slots alone)");
            var wz=R("Assets/ChipArena/Runtime/NativeWardZeroPresentation.cs");
            Check(Between(wz,"private void WardZeroScenery(NativeArena arena)","public void SetWardBreach")?.Contains("WardZeroFieldSend(now,true);")==true&&wz.Contains("if(wzFieldTex==null||wzField==null){if(actors)NativeLighting.ActorLights(true);"),"Ward Zero lights its bodies in the live arena and the killcam");
        }

        // ---------------- the looks ----------------
        private static double Lum(Vector3 c)=>.2126*c.X+.7152*c.Y+.0722*c.Z;
        private static double Lin(double c)=>NativeEntranceLight.ToLinear(c);
        private static Vector3 Linear(MapLookColor c)=>new Vector3((float)Lin(c.r),(float)Lin(c.g),(float)Lin(c.b));
        // Unity's light colour times intensity in Linear space with the built-in default (gamma) intensity.
        private static Vector3 Applied(MapLookColor c,float intensity)=>new Vector3((float)NativeEntranceLight.Applied(c.r,intensity,true,false),(float)NativeEntranceLight.Applied(c.g,intensity,true,false),(float)NativeEntranceLight.Applied(c.b,intensity,true,false));
        private static MapLookSet Looks(string root)
        {
            var o=new JsonSerializerOptions{IncludeFields=true};var set=JsonSerializer.Deserialize<MapLookSet>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/MapLooks.json")),o);NativeMapLooks.Validate(set);
            // The looks whose zone presentation turns actor lighting on (Contract holds each call).
            var actors=new Dictionary<string,string>{[NativeExpeditionStation.Look]="Central Station",["ironworks"]="the Ironworks",[NativeWardZeroLooks.Look]="Ward Zero"};
            var dark=new List<string>();
            foreach(var m in set.maps)
            {
                var s=new MapLookState();NativeMapLooks.Applied(m,0,false,s);bool shadows=s.KeyShadows!="none";
                double body=Lum(Linear(s.AmbientEquator))+.5*Lum(Applied(s.KeyColor,s.KeyIntensity))*(shadows?1-s.KeyStrength:1);
                if((m.lights?.Length??0)==0||body>=DarkBase)continue;dark.Add(m.look+" "+body.ToString("F4",CultureInfo.InvariantCulture));
                Check(actors.ContainsKey(m.look),m.id+" ("+m.look+"): a dark base for bodies ("+body.ToString("F4",CultureInfo.InvariantCulture)+" < "+DarkBase+") under practical lights its bodies would never take; turn actor lighting on in its presentation");
            }
            foreach(var k in actors.Keys)Check(set.maps.Any(m=>m.look==k),"the look "+k+" exists");
            facts.Add("dark looks with practicals, all lighting their bodies: "+string.Join(", ",dark));
            // The town hub and the arenas keep their own fill (none of them is dark): untouched by the station's switch.
            foreach(var id in new[]{"expedition-city","siren","cybercity","factory","brickfall"})
            {
                var m=set.maps.Single(x=>x.id==id);var s=new MapLookState();NativeMapLooks.Applied(m,0,false,s);bool shadows=s.KeyShadows!="none";
                double body=Lum(Linear(s.AmbientEquator))+.5*Lum(Applied(s.KeyColor,s.KeyIntensity))*(shadows?1-s.KeyStrength:1);Check(body>=DarkBase,id+" reads by its own fill ("+body.ToString("F4",CultureInfo.InvariantCulture)+")");
            }
            return set;
        }

        // ---------------- the albedo floor ----------------
        private struct Paint{public Vector3 Skin,Suit;public double SkinArea,SuitArea;}
        private static float Fr(float x)=>x-MathF.Floor(x);
        private static Vector3 Fr(Vector3 v)=>new Vector3(Fr(v.X),Fr(v.Y),Fr(v.Z));
        private static Vector3 Fl(Vector3 v)=>new Vector3(MathF.Floor(v.X),MathF.Floor(v.Y),MathF.Floor(v.Z));
        private static float Step(float e,float x)=>x>=e?1:0;
        private static float Hash(Vector3 p){p=Fr(p*.3183099f+new Vector3(.71f,.113f,.419f));p*=17f;return Fr(p.X*p.Y*p.Z*(p.X+p.Y+p.Z));}
        internal static float Value(float x)=>Math.Clamp((x-MathF.Floor(x)-.02f)/.96f,0,1);
        internal static Vector3 Skin(Vector3 albedo,Vector3 local,Vector3 body)
        {
            float seed=body.Y-1,tone=body.X-1;var q=Fl(local*12.5f+new Vector3(seed*31,seed*7,seed*17));float big=Hash(q),fine=Hash(Fl(local*28)+new Vector3(q.Z,q.X,q.Y));
            var c=Vector3.Lerp(albedo,albedo*new Vector3(.72f,.78f,.6f),Step(.8f,big)*.8f);c=Vector3.Lerp(c,albedo*new Vector3(.84f,.66f,.86f),Step(.88f,1-big)*.6f);c*=.96f+.08f*Step(.5f,fine);
            var hue=tone>0?new Vector3(1.06f,1,1.12f):new Vector3(.94f,1.04f,.84f);return c*Vector3.Lerp(Vector3.One,hue,Math.Min(1,Math.Abs(tone)*1.6f));
        }
        internal static Vector3 Stain(Vector3 albedo,Vector3 local,Vector3 body,float cls)
        {
            float seed=body.Y-1,k=cls>3.5f?1:cls>2.5f?.7f:.4f;var q=Fl(local*14+new Vector3(seed*13,seed*29,seed*5));float h=Hash(q+new Vector3(3.1f,7.7f,1.3f)),g=Hash(Fl(local*30)+new Vector3(q.Y,q.Z,q.X));
            var c=Vector3.Lerp(albedo,albedo*new Vector3(.6f,.52f,.42f),Step(.64f,h)*.7f*k);return Vector3.Lerp(c,new Vector3(.15f,.02f,.022f)*(.8f+.4f*g),Step(.9f,h)*Step(.3f,g)*.85f*k);
        }
        /// <summary>A vertex's albedo as ZombieLit's surf makes it (white tints): the colour, the skin's mottling, the stain, the occlusion.</summary>
        internal static Vector3 SurfAlbedo(WeaponSection s,int i,Vector3 body)
        {
            var c4=s.Colors[i];var a=new Vector3(c4.X,c4.Y,c4.Z);float x=i<s.Data.Count?s.Data[i].X:0;float cls=MathF.Floor(x),ao=1-Value(x);var p=s.Positions[i];
            if(cls>.5f&&cls<1.5f)a=Skin(a,p,body);if(cls>.5f&&cls<4.5f&&(cls<1.5f||cls>2.5f))a=Stain(a,p,body,cls);return a*ao;
        }
        private static Paint Albedo()
        {
            var ids=new[]{11,222,3333,40000,51234};var bodies=ids.Select(id=>{var v=NativeHordeModels.Variation(id);return new Vector3(v.X,v.Y,v.Z);}).ToArray();
            Vector3 skin=Vector3.Zero,suit=Vector3.Zero;double sa=0,ca=0;
            for(int k=0;k<NativeHordeKinds.Count;k++)for(int l=0;l<NativeHordeKinds.Looks[k];l++)
            {
                var m=NativeHordeModels.Build(k,l,NativeHordeModels.Full);
                foreach(var s in m.Mesh.Sections)for(int t=0;t+2<s.Indices.Count;t+=3)
                {
                    int a=s.Indices[t],b=s.Indices[t+1],c=s.Indices[t+2];var e=s.Emission[a];if(e.X+e.Y+e.Z>.01f||s.Data.Count!=s.Positions.Count)continue;
                    int cls=(int)MathF.Floor(s.Data[a].X);if(cls!=NativeHordeModels.SurfSkin&&cls!=NativeHordeModels.SurfCloth)continue;
                    double area=Vector3.Cross(s.Positions[b]-s.Positions[a],s.Positions[c]-s.Positions[a]).Length()*.5;var sum=Vector3.Zero;
                    foreach(var body in bodies)sum+=(SurfAlbedo(s,a,body)+SurfAlbedo(s,b,body)+SurfAlbedo(s,c,body))/3;sum/=bodies.Length;
                    if(cls==NativeHordeModels.SurfSkin){skin+=sum*(float)area;sa+=area;}else{suit+=sum*(float)area;ca+=area;}
                }
            }
            var p=new Paint{Skin=skin/(float)sa,Suit=suit/(float)ca,SkinArea=sa,SuitArea=ca};
            // The floor: the rotted skin a grey-green, the suit a dark blue-grey (the palette's 0x97a08f and 0x343d48 after the occlusion,
            // the mottling and the stain); held so a later pass cannot quietly take them to black.
            Check(Lum(p.Skin)>=.14&&p.Skin.Y>=p.Skin.X&&p.Skin.Y>p.Skin.Z,"the skin's albedo stays a readable grey-green ("+Lum(p.Skin).ToString("F3",CultureInfo.InvariantCulture)+", rgb "+V(p.Skin)+")");
            Check(Lum(p.Suit)>=.022&&p.Suit.Z>p.Suit.X,"the suit's albedo stays a dark blue-grey, not black ("+Lum(p.Suit).ToString("F4",CultureInfo.InvariantCulture)+", rgb "+V(p.Suit)+")");
            facts.Add("albedo skin "+Lum(p.Skin).ToString("F3",CultureInfo.InvariantCulture)+" suit "+Lum(p.Suit).ToString("F4",CultureInfo.InvariantCulture));
            return p;
        }
        private static string V(Vector3 v)=>"("+v.X.ToString("F3",CultureInfo.InvariantCulture)+", "+v.Y.ToString("F3",CultureInfo.InvariantCulture)+", "+v.Z.ToString("F3",CultureInfo.InvariantCulture)+")";
        private static double S8(double lum)=>Math.Round(255*NativeEntranceLight.ToGamma(Math.Clamp(lum,0,1)));

        // ---------------- the station, emulated ----------------
        /// <summary>ZombieLit's ZombieLight for one Unity light (diffuse wrap and the warm flush, the class's sheen).</summary>
        internal static Vector3 ZombieLight(Vector3 albedo,Vector3 n,Vector3 l,Vector3 v,Vector3 color,float atten,float wrap,float spec)
        {
            float ndl=Vector3.Dot(n,l),lam=Math.Max(ndl,0),w=Math.Clamp((ndl+wrap)/(1+wrap),0,1);var diff=albedo*w+albedo*new Vector3(.3f,.06f,.05f)*Math.Max(w-lam,0)*Step(.35f,wrap);
            float power=14+(90-14)*spec;var h=Vector3.Normalize(l+v);float s=MathF.Pow(Math.Max(Vector3.Dot(n,h),0),power)*spec*Step(0,ndl)*(.5f+power*.012f);return (diff+new Vector3(s))*color*atten;
        }
        /// <summary>NativeWardLight.cginc ActorLights over the picked slots (no Ward field at the station); `wrap` false is the blocks' term.</summary>
        internal static Vector3 Slots(NativeExpeditionStationLights l,Vector3 p,Vector3 n,bool wrap)
        {
            var sum=Vector3.Zero;
            for(int s=0;s<NativeExpeditionStationLights.Slots;s++)
            {
                var d=new Vector3(l.Pos[s*4],l.Pos[s*4+1],l.Pos[s*4+2])-p;float len=Math.Max(d.Length(),1e-4f),q=len/Math.Max(l.Pos[s*4+3],1e-4f),a=Math.Clamp(1-q*q,0,1),ndl=Vector3.Dot(n,d/len);
                ndl=wrap?Math.Clamp((ndl+.35f)/1.35f,0,1):Math.Clamp(ndl,0,1);
                sum+=new Vector3(l.Rgb[s*3],l.Rgb[s*3+1],l.Rgb[s*3+2])*(a*a*ndl*(float)NativeWardZeroLights.BeamFactor(l.Beam[s],-d.X,-d.Z));
            }
            return sum;
        }
        private static double P(List<double> xs,double q){var s=xs.OrderBy(x=>x).ToArray();return s[Math.Min(s.Length-1,(int)(q*s.Length))];}
        private static void Station(string root,MapLookSet set,Paint paint)
        {
            var o=new JsonSerializerOptions{IncludeFields=true};string R(string p)=>File.ReadAllText(Path.Combine(root,p));
            var unity=JsonSerializer.Deserialize<UnityMapContent>(R("Assets/Conversion/Resources/UnityMaps.json"),o);var map=unity.maps.Single(m=>m.id==NativeExpeditionStation.MapId);
            var look=set.maps.Single(m=>m.id==map.id);var area=NativeExpeditionLayout.Parse(R("Assets/Conversion/Resources/ExpeditionLayout.json")).Area("exp-transit");
            var kit=NativeExpeditionStationKit.Build(map.blocks,area);var lights=NativeExpeditionStationLights.Build(kit,look);var st=new MapLookState();NativeMapLooks.Applied(look,map.sky,false,st);
            var calm=new NativeStationState();calm.Evaluate(NativeStationLive.Calm,30);
            // What the runtime sets: actor lighting on at the station when its scenery throws the switch (Contract holds where).
            bool actors=Between(R("Assets/ChipArena/Runtime/NativeExpeditionStationPresentation.cs"),"private void StationScenery(NativeArena arena)","private bool EnsureStation()")?.Contains("NativeLighting.ActorLights(true);")==true;
            // Unity's lights: the key (soft shadows at the look's strength: the worst case, under the roofs and the glazing), the
            // per-pixel rim, the trilight ambient (SH, linearised by Unity in Linear space).
            NativeMapLooks.Toward(look.key,out double kx,out double ky,out double kz);NativeMapLooks.Toward(look.rim,out double rx,out double ry,out double rz);
            var key=Applied(st.KeyColor,st.KeyIntensity);var rim=Applied(st.RimColor,st.RimIntensity);var kd=Vector3.Normalize(new Vector3((float)kx,(float)ky,(float)kz));var rd=Vector3.Normalize(new Vector3((float)rx,(float)ry,(float)rz));
            float shade=st.KeyShadows=="none"?1:1-st.KeyStrength;var sky=Linear(st.AmbientSky);var eq=Linear(st.AmbientEquator);var gr=Linear(st.AmbientGround);
            Vector3 SH(Vector3 n)=>n.Y>=0?Vector3.Lerp(eq,sky,n.Y):Vector3.Lerp(eq,gr,-n.Y);
            Check(st.KeyShadows!="none"&&shade<=.15f&&Lum(SH(Vector3.UnitX))<.005,"the station's base for bodies is dark: a shadowed key ("+shade.ToString("F2",CultureInfo.InvariantCulture)+" of "+Lum(key).ToString("F3",CultureInfo.InvariantCulture)+") and an ambient of "+Lum(SH(Vector3.UnitX)).ToString("F4",CultureInfo.InvariantCulture));
            // The floor's plain colour: the concourse terrazzo's listed colour (NativeBlockLit's base, linear).
            var tile=look.blockLooks.First(b=>b.block=="expedition-transit-floor-*").color;var floorAlb=new Vector3((float)Lin(((tile>>16)&255)/255.0),(float)Lin(((tile>>8)&255)/255.0),(float)Lin((tile&255)/255.0));
            var floorL=new List<double>();var skinOn=new List<double>();var skinOff=new List<double>();var suitOn=new List<double>();var ratio=new List<double>();var litRatio=new List<double>();var litSkin=new List<double>();var litSuit=new List<double>();
            Vector3 hueSkin=Vector3.Zero,hueSuit=Vector3.Zero;
            var nodes=map.nav.nodes.Where(n=>n.pos.y<9).ToArray();Check(nodes.Length>4000,"walkable nodes on the station's floors ("+nodes.Length+"; the roofs' nodes left out)");
            for(int i=0;i<nodes.Length;i+=2)
            {
                var nd=nodes[i];var p=new Vector3(nd.pos.x,nd.pos.y-.9f,nd.pos.z);double th=i*2.39996;
                var eye=p+new Vector3(6*(float)Math.Sin(th),1.7f,6*(float)Math.Cos(th));lights.Settle(eye.X,eye.Y,eye.Z,calm,30);
                var up=Vector3.UnitY;var floor=floorAlb*(SH(up)+key*Math.Max(0,Vector3.Dot(up,kd))*shade+rim*Math.Max(0,Vector3.Dot(up,rd))+Slots(lights,p,up,false));
                // The body's chest (1.2 m) facing the camera, its two front corners and its shoulders' top.
                var c=p+new Vector3(0,1.2f,0);var v=Vector3.Normalize(eye-c);var f=Vector3.Normalize(new Vector3(v.X,0,v.Z));var side=new Vector3(f.Z,0,-f.X);
                var ns=new[]{f,Vector3.Normalize(f+side),Vector3.Normalize(f-side),Vector3.Normalize(f+new Vector3(0,.6f,0))};
                Vector3 Body(Vector3 alb,float wrap,float spec,bool on){var sum=Vector3.Zero;foreach(var n in ns){var x=ZombieLight(alb,n,kd,v,key,shade,wrap,spec)+ZombieLight(alb,n,rd,v,rim,1,wrap,spec)+alb*SH(n);if(on)x+=alb*Slots(lights,c,n,true);sum+=x;}return sum/ns.Length;}
                var skin=Body(paint.Skin,.18f,.08f,actors);var suit=Body(paint.Suit,.12f,0,actors);double fl=Lum(floor),sk=Lum(skin);
                floorL.Add(fl);skinOn.Add(sk);suitOn.Add(Lum(suit));skinOff.Add(Lum(Body(paint.Skin,.18f,.08f,false)));ratio.Add(sk/Math.Max(fl,1e-6));
                // Where a lamp lights the floor (above the base fill), the body stands in its light too.
                if(fl>=.01){litRatio.Add(sk/fl);litSkin.Add(sk);litSuit.Add(Lum(suit));hueSkin+=skin;hueSuit+=suit;}
            }
            double skinMed=P(skinOn,.5),floorMed=P(floorL,.5),offMed=P(skinOff,.5);
            Check(actors,"the station's scenery throws the actor light switch");
            Check(skinMed>=.008,"a zombie's skin at the station reads (median lit luminance "+skinMed.ToString("F4",CultureInfo.InvariantCulture)+", about "+S8(skinMed)+"/255; Unity's lights alone gave "+offMed.ToString("F4",CultureInfo.InvariantCulture)+", "+S8(offMed)+"/255)");
            Check(P(ratio,.5)>=.5,"a body is lit like the floor it stands on (median skin/floor "+P(ratio,.5).ToString("F2",CultureInfo.InvariantCulture)+")");
            // Under a lamp the body takes it in the round: at most one spot in ten draws its camera side under a tenth of the floor's
            // light (a lamp behind the body leaves a silhouette, as it does a pilot).
            Check(litRatio.Count>skinOn.Count/3&&P(litRatio,.5)>=.4&&P(litRatio,.1)>=.1&&P(litSkin,.5)>=.015,"under a lamp that lights the floor ("+litRatio.Count+" of "+skinOn.Count+" spots) the body takes it (skin/floor median "+P(litRatio,.5).ToString("F2",CultureInfo.InvariantCulture)+", 10th percentile "+P(litRatio,.1).ToString("F2",CultureInfo.InvariantCulture)+"; median skin "+S8(P(litSkin,.5))+"/255)");
            Check(P(litSuit,.5)>=.002&&P(litSkin,.5)>=3*P(litSuit,.5),"the suit is a dark colour, not black, and the skin reads against it (median suit "+S8(P(litSuit,.5))+"/255, skin "+S8(P(litSkin,.5))+"/255)");
            // Hue under the same lights: the skin greener than the suit, the suit bluer than the skin (the light's own tint cancels).
            float gSkin=hueSkin.Y/hueSkin.Z,gSuit=hueSuit.Y/hueSuit.Z;Check(gSkin>=1.2f*gSuit,"the lit skin keeps its grey-green against the blue-grey suit (green/blue "+gSkin.ToString("F2",CultureInfo.InvariantCulture)+" vs "+gSuit.ToString("F2",CultureInfo.InvariantCulture)+")");
            facts.Add("station ("+skinOn.Count+" spots, key in shadow): floor median "+S8(floorMed)+"/255, skin median "+S8(skinMed)+"/255 with actor lights vs "+S8(offMed)+"/255 by Unity's lights alone, suit "+S8(P(suitOn,.5))+"/255; under lamps skin "+S8(P(litSkin,.5))+"/255, suit "+S8(P(litSuit,.5))+"/255");
            // The picker and the emulated light allocate nothing per frame (the runtime's switch is two cached-id global writes).
            long before=GC.GetAllocatedBytesForCurrentThread();float acc=0;for(int i=0;i<120;i++){lights.Update(i%40-20,1.7,i%30-15,calm,30+i/60.0,1f/60);acc+=Slots(lights,new Vector3(i%40-20,1.2f,i%30-15),Vector3.UnitZ,true).X;}
            Check(GC.GetAllocatedBytesForCurrentThread()-before==0&&acc>=0,"the station's picker and the slots' light allocate nothing per frame");
        }
    }
}
#endif
