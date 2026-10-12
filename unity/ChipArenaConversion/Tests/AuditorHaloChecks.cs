#if ARENA_TESTS
using System;
using System.IO;
using System.Numerics;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // The Auditor halo (Core/NativeAuditorHalo.cs, Resources/NativeAuditorHalo.shader, Runtime/NativeAuditorHaloPresentation.cs).
    // Owner (2026-10-10): the DEV halo animated only in the lobby portrait; in game it came from the static model bank. Checked:
    // the group follows the source's bob, spin, tilt and scale on the clock (against auditor-halo.ts frames in
    // Tests/lobby-pose-parity.json); everything moves with the clock and nothing pops; the upgraded pieces keep the source's
    // layout (arcs on the .32 ring at i*2.1 spanning .57 pi, the middle one raised, flames on the .30 ring); the gaps never
    // close and crackle sometimes, not always; the Unity-frame uniforms match the shader's turn and flat-flame formulas;
    // flames order far to near; reduced effects keep only the arcs, inner glow and source flicker; a frame allocates nothing;
    // and every place a pilot wears the halo (arena host/guest/bot, killcam, intro rig, chip draft stage, corpse, loose hat,
    // death scenes, menu portraits) draws it through the one shared drawer, hidden on the first-person view model, with the
    // shader registered for the Unity build check.
    internal static class AuditorHaloChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Auditor halo: "+what);count++;}
        private static void Near(double a,double b,double tolerance,string what){Check(Math.Abs(a-b)<=tolerance&&!double.IsNaN(a),what+": "+a+" != "+b);}
        private static Vector3 M(Vector3 v)=>new Vector3(-v.X,v.Y,v.Z);
        private static Vector3 V3(JsonElement e)=>new Vector3((float)e[0].GetDouble(),(float)e[1].GetDouble(),(float)e[2].GetDouble());
        private static Quaternion Q4(JsonElement e)=>new Quaternion((float)e[0].GetDouble(),(float)e[1].GetDouble(),(float)e[2].GetDouble(),(float)e[3].GetDouble());
        private static float Apart(Quaternion a,Quaternion b)=>Math.Min((a-b).Length(),(a+b).Length());
        public static void Run(string root)
        {
            Clock(root);Layout();Motion();Gaps();Uniforms();Order();Reduced();Allocation();Paths(root);
            Console.WriteLine("PASS Auditor halo ("+count+" checks: source bob/spin/tilt/scale on the clock, animates without pops, source layout, gaps never close, crackle in bursts, Unity-frame uniforms match the shader, far-to-near flames, reduced fallback, zero allocation per frame, one shared drawer in the arena, killcam, intro, draft, corpses, death scenes and portraits, hidden in first person, shader in the build check)");
        }
        // auditor-halo.ts animateCosmetics: y .46 + sin(2.3t) * .012, rotation.y = .18t, tilt z -.12, scale .72, at the
        // source's own frames and at any clock.
        private static void Clock(string root)
        {
            var f=new AuditorHaloFrame();
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/lobby-pose-parity.json")));int frames=0;
            foreach(var fr in document.RootElement.GetProperty("halo").GetProperty("frames").EnumerateArray())
            {
                float t=(float)fr.GetProperty("t").GetDouble();var g=fr.GetProperty("group");NativeAuditorHalo.Evaluate(t,false,f);
                Near(Vector3.Distance(f.GroupPosition,V3(g.GetProperty("p"))),0,5e-6,"group position vs source @"+t);Near(Apart(f.GroupRotation,Q4(g.GetProperty("q"))),0,1e-5,"group rotation vs source @"+t);
                Near(f.GroupScale,g.GetProperty("s")[0].GetDouble(),1e-6,"group scale vs source");frames++;
            }
            Check(frames>=5,"source halo frames");
            foreach(float t in new[]{0f,.25f,3.7f,61.3f,1234.5f})
            {
                NativeAuditorHalo.Evaluate(t,false,f);Near(f.GroupPosition.Y,.46+Math.Sin(t*2.3)*.012,1e-5,"bob @"+t);Near(f.GroupPosition.X,0,1e-7,"bob is vertical");
                Near(Apart(f.GroupRotation,MeleeMath.Euler(0,t*.18f,-.12f)),0,1e-5,"spin and tilt @"+t);Check(f.Clock>=0&&f.Clock<NativeAuditorHalo.ClockWrap,"shader clock wraps @"+t);
            }
        }
        // The upgraded arcs on the source's centre lines (TorusGeometry(.32, .., .57 pi) at (pi/2, 0, i*2.1), the middle +.025)
        // and the flames on the source's ring points (HaloFlame positions).
        private static void Layout()
        {
            for(int i=0;i<NativeAuditorHalo.Arcs;i++)
            {
                NativePortraitRules.HaloArcLocal(i,out var ap,out var aq);
                foreach(float s in new[]{0f,.25f,.5f,1f})
                {
                    float u=s*NativePortraitRules.HaloArc;var source=ap+Vector3.Transform(new Vector3(NativePortraitRules.HaloArcRadius*(float)Math.Cos(u),NativePortraitRules.HaloArcRadius*(float)Math.Sin(u),0),aq);
                    Near(Vector3.Distance(NativeAuditorHalo.Centre(i,s),source),0,1e-5,"arc "+i+" centre line at "+s);
                }
            }
            Check(NativeAuditorHalo.Taper(0)<1e-3&&NativeAuditorHalo.Taper(1)<1e-3&&Math.Abs(NativeAuditorHalo.Taper(.5f)-1)<1e-6,"arcs taper to blade tips");
            var f=new AuditorHaloFrame();NativeAuditorHalo.Evaluate(2.2f,false,f);
            for(int n=0;n<NativeAuditorHalo.Flames;n++){NativePortraitRules.HaloFlame(n,2.2f,out var p,out _,out _);Near(Vector3.Distance(f.FlameBase[n],p),0,1e-5,"flame "+n+" on the source ring");}
            var m=new HaloMesh();NativeAuditorHalo.BuildArcs(m);Check(m.Positions.Count==NativeAuditorHalo.Arcs*(NativeAuditorHalo.ArcSegments+1)*(NativeAuditorHalo.TubeSides+1)&&m.Indices.Count%3==0,"arc mesh");
            float worst=0;for(int k=0;k<m.Positions.Count;k++){var d=m.Data[k];var c=NativeAuditorHalo.Centre((int)d.Z,d.Y);worst=Math.Max(worst,Math.Abs(Vector3.Distance(m.Positions[k],c)-NativeAuditorHalo.Tube*NativeAuditorHalo.Taper(d.Y)));}
            Near(worst,0,1e-5,"arc vertices sit on the tapered tube");
            m.Clear();NativeAuditorHalo.BuildFins(m);int outline=NativePortraitRules.FlameOutline().Count;Check(m.Positions.Count==NativeAuditorHalo.Flames*outline,"reduced flames use the source flame outline");
            m.Clear();NativeAuditorHalo.BuildFlames(m);Check(m.Positions.Count==NativeAuditorHalo.Flames*4&&m.Indices.Count==NativeAuditorHalo.Flames*6,"twelve flame slots");
            bool reach=true;foreach(var d in m.Data)reach&=Math.Abs(Math.Abs(d.Y)-NativeAuditorHalo.FlameQuad)<1e-6;Check(reach&&NativeAuditorHalo.FlameQuad>1.4f,"flame quads leave room for the hook and tongues");
            // Slim horns, not a wall of teardrops (review 2026-10-10): thin flames, the short ones about .55 of the tall ones.
            double tall=0,small=0;
            for(float t=0;t<20;t+=.37f){NativeAuditorHalo.Evaluate(t,false,f);for(int n=0;n<NativeAuditorHalo.Flames;n++){Check(f.FlameWidth[n]>.05f&&f.FlameWidth[n]<.105f,"slim flame "+n+" ("+f.FlameWidth[n]+")");if(n%2==0)tall+=f.FlameHeight[n];else small+=f.FlameHeight[n];}}
            Check(small/tall>.45&&small/tall<.65,"short flames about .55 of the tall ones ("+small/tall+")");
        }
        // Every animated quantity moves with the clock, smoothly (a 60 fps step changes it a little, never jumps).
        private static void Motion()
        {
            var a=new AuditorHaloFrame();var b=new AuditorHaloFrame();
            NativeAuditorHalo.Evaluate(4f,false,a);NativeAuditorHalo.Evaluate(4.5f,false,b);
            Check(Math.Abs(a.GroupPosition.Y-b.GroupPosition.Y)>1e-4&&Apart(a.GroupRotation,b.GroupRotation)>1e-3,"bob and spin move");
            Check(Math.Abs(a.Spin[0]-b.Spin[0])>1e-3&&Math.Abs(a.Spin[1]-b.Spin[1])>1e-3&&Math.Abs(a.Spin[2]-b.Spin[2])>1e-3,"each arc turns");
            Check(Math.Abs(a.Pulse[0]-b.Pulse[0])>1e-4,"arcs pulse");int flames=0,embers=0;
            for(int n=0;n<NativeAuditorHalo.Flames;n++)if(Math.Abs(a.FlameHeight[n]-b.FlameHeight[n])>1e-3)flames++;
            for(int e=0;e<NativeAuditorHalo.Embers;e++)if(Vector3.Distance(a.Ember[e],b.Ember[e])>1e-3)embers++;
            Check(flames==NativeAuditorHalo.Flames,"every flame flickers ("+flames+")");Check(embers>=NativeAuditorHalo.Embers/2,"embers rise ("+embers+")");
            // The arcs turn against the group: net drift backwards while the flames go round.
            Check(NativeAuditorHalo.Counter+.18f<0,"arcs counter-rotate against the flames");
            float worstBob=0,worstSpin=0,worstFlame=0;
            for(float t=0;t<30;t+=1f/60)
            {
                NativeAuditorHalo.Evaluate(t,false,a);NativeAuditorHalo.Evaluate(t+1f/60,false,b);
                worstBob=Math.Max(worstBob,Math.Abs(a.GroupPosition.Y-b.GroupPosition.Y));worstSpin=Math.Max(worstSpin,Math.Abs(a.Spin[0]-b.Spin[0]));
                for(int n=0;n<NativeAuditorHalo.Flames;n++)worstFlame=Math.Max(worstFlame,Math.Abs(a.FlameHeight[n]-b.FlameHeight[n]));
            }
            Check(worstBob<.001f&&worstSpin<.01f&&worstFlame<.02f,"no pops at 60 fps (bob "+worstBob+", spin "+worstSpin+", flame "+worstFlame+")");
            Check(NativeAuditorHalo.Hash(17)==NativeAuditorHalo.Hash(17)&&NativeAuditorHalo.Hash(17)!=NativeAuditorHalo.Hash(18),"deterministic hash");
        }
        // Over ten minutes: the gaps breathe but never close, narrow gaps always crackle, and crackle is occasional overall.
        private static void Gaps()
        {
            var f=new AuditorHaloFrame();float min=float.MaxValue,max=0;int frames=0,crackling=0,narrowQuiet=0;
            for(float t=0;t<600;t+=.05f)
            {
                NativeAuditorHalo.Evaluate(t,false,f);frames++;bool any=false;
                for(int i=0;i<NativeAuditorHalo.Arcs;i++)
                {
                    float g=NativeAuditorHalo.Gap(f,i);min=Math.Min(min,g);max=Math.Max(max,g);any|=f.Bolt[i]>0;if(g<NativeAuditorHalo.CrackleGap-.1f&&f.Bolt[i]<=0)narrowQuiet++;
                    Near(Vector3.Distance(f.BoltFrom[i],NativeAuditorHalo.ArcPoint(f,i,1)),0,1e-6,"crackle starts at the arc's tip");Near(Vector3.Distance(f.BoltTo[i],NativeAuditorHalo.ArcPoint(f,(i+1)%3,0)),0,1e-6,"crackle ends at the next arc");
                }
                if(any)crackling++;
            }
            Check(min>.08f,"gaps never close (min "+min+" rad)");Check(max-min>.2f,"gaps open and close ("+min+".."+max+")");Check(narrowQuiet==0,"a narrow gap always crackles");
            float share=crackling/(float)frames;Check(share>.1f&&share<.7f,"crackle is occasional ("+share+" of frames)");
        }
        // The Unity-frame uniforms against the shader's formulas: the turn of a mirrored arc vertex lands on the mirrored arc,
        // the crackle ends are the mirrored tips, a reduced flame's offset is the mirrored source flame transform.
        private static void Uniforms()
        {
            var f=new AuditorHaloFrame();var u=new AuditorHaloUniforms();NativeAuditorHalo.Evaluate(7.3f,false,f);NativeAuditorHalo.Pack(f,.4f,u);
            Near(u.A.Y,.4f,1e-6,"opacity");Near(u.A.X,f.Clock,1e-6,"clock");
            for(int i=0;i<NativeAuditorHalo.Arcs;i++)
            {
                float spin=i==0?u.Spin.X:i==1?u.Spin.Y:u.Spin.Z;
                foreach(float s in new[]{0f,.4f,1f})Near(Vector3.Distance(NativeAuditorHalo.ShaderTurn(M(NativeAuditorHalo.Centre(i,s)),spin),M(NativeAuditorHalo.ArcPoint(f,i,s))),0,1e-5,"shader turn of arc "+i);
                Near(Vector3.Distance(new Vector3(u.Bolts[i].X,u.Bolts[i].Y,u.Bolts[i].Z),M(f.BoltFrom[i])),0,1e-6,"packed crackle start");Near(u.Bolts[i].W,f.Bolt[i],1e-6,"packed crackle strength");
                Near(Vector3.Distance(new Vector3(u.Bolts[i+3].X,u.Bolts[i+3].Y,u.Bolts[i+3].Z),M(f.BoltTo[i])),0,1e-6,"packed crackle end");
            }
            for(int n=0;n<NativeAuditorHalo.Flames;n++)
            {
                NativePortraitRules.HaloFlame(n,7.3f,out var p,out var q,out var s);var fin=u.Fins[n];
                foreach(var pt in new[]{new Vector2(-.06f,0),new Vector2(0,.43f),new Vector2(.13f,.11f)})
                {var source=p+Vector3.Transform(new Vector3(pt.X*s.X,pt.Y*s.Y,0),q);Near(Vector3.Distance(M(p)+NativeAuditorHalo.FinOffset(M(p),pt.X,pt.Y,fin),M(source)),0,1e-5,"reduced flame "+n+" matches animateCosmetics");}
            }
            for(int e=0;e<NativeAuditorHalo.Embers;e++)Near(Vector3.Distance(new Vector3(u.Embers[e].X,u.Embers[e].Y,u.Embers[e].Z),M(f.Ember[e])),0,1e-6,"packed ember");
        }
        // Premultiplied flames blend in order: the farthest from the eye first.
        private static void Order()
        {
            var f=new AuditorHaloFrame();var u=new AuditorHaloUniforms();NativeAuditorHalo.Evaluate(1,false,f);var eye=new Vector3(2.5f,.8f,-3);NativeAuditorHalo.Sort(f,eye);NativeAuditorHalo.Pack(f,1,u);
            for(int k=1;k<NativeAuditorHalo.Flames;k++)Check(Vector3.DistanceSquared(f.FlameBase[f.Order[k-1]],eye)>=Vector3.DistanceSquared(f.FlameBase[f.Order[k]],eye),"flames far to near");
            Near(Vector3.Distance(new Vector3(u.Flames[0].X,u.Flames[0].Y,u.Flames[0].Z),M(f.FlameBase[f.Order[0]])),0,1e-6,"slot 0 is the farthest flame");Near(u.Flames[0].W,f.FlameHeight[f.Order[0]],1e-6,"slot carries its height");
            var seen=new bool[NativeAuditorHalo.Flames];foreach(int o in f.Order)seen[o]=true;Check(Array.TrueForAll(seen,x=>x),"every flame drawn once");
        }
        // prefs.reducedEffects: the arcs, the inner glow shells and the source's flat flames with animateCosmetics' flicker.
        private static void Reduced()
        {
            var f=new AuditorHaloFrame();NativeAuditorHalo.Evaluate(5.5f,true,f);
            Check(f.Simple&&f.Glow==0&&f.Light==0,"reduced: no haze, no light");
            for(int i=0;i<3;i++)Check(f.Bolt[i]==0,"reduced: no crackle");for(int e=0;e<NativeAuditorHalo.Embers;e++)Check(f.EmberAlpha[e]==0,"reduced: no embers");
            for(int n=0;n<NativeAuditorHalo.Flames;n++){NativePortraitRules.HaloFlame(n,5.5f,out _,out _,out var s);Near(f.FinScale[n].X,s.X,1e-6,"reduced flicker x");Near(f.FinScale[n].Y,s.Y,1e-6,"reduced flicker y");Near(f.FinTilt[n],Math.Sin(5.5*5+n)*.17,1e-5,"reduced roll");}
            var simple=new HaloMesh();NativeAuditorHalo.BuildGlow(simple,true);var full=new HaloMesh();NativeAuditorHalo.BuildGlow(full,false);
            bool innerOnly=true;foreach(var d in simple.Data)innerOnly&=d.X==NativeAuditorHalo.KindShell&&d.W==0;Check(innerOnly&&simple.Positions.Count>0,"reduced glow is the inner shells only");
            bool haze=false,outer=false;foreach(var d in full.Data){haze|=d.X==NativeAuditorHalo.KindHaze;outer|=d.X==NativeAuditorHalo.KindShell&&d.W==1;}Check(haze&&outer,"full glow has the outer shells and haze");
            var sparks=new HaloMesh();NativeAuditorHalo.BuildSparks(sparks);bool bolt=false,ember=false;foreach(var d in sparks.Data){bolt|=d.X==NativeAuditorHalo.KindBolt;ember|=d.X==NativeAuditorHalo.KindEmber;}Check(bolt&&ember,"sparks: crackle and embers");
        }
        // Evaluate, Sort and Pack for many frames allocate nothing (the Runtime drawer reuses one frame, uniforms and block).
        private static void Allocation()
        {
            var f=new AuditorHaloFrame();var u=new AuditorHaloUniforms();var eye=new Vector3(1,2,3);
            for(int i=0;i<200;i++){NativeAuditorHalo.Evaluate(i*.016f,i%2==0,f);NativeAuditorHalo.Sort(f,eye);NativeAuditorHalo.Pack(f,1,u);}
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<5000;i++){NativeAuditorHalo.Evaluate(10+i*.016f,i%7==0,f);NativeAuditorHalo.Sort(f,eye);NativeAuditorHalo.Pack(f,i%3==0?.5f:1,u);}
            long used=GC.GetAllocatedBytesForCurrentThread()-before;Check(used==0,"no allocation per frame ("+used+" bytes over 5000 frames)");
        }
        // The places a pilot wears the halo all use the shared drawer (read from the Runtime sources), the first-person view
        // model skips it, and the shader is in Resources with its meta, declares what the Runtime sets and is build-checked.
        private static void Paths(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
            string source=R("NativeSourcePresentation.cs"),intro=R("NativeIntros.cs"),draft=R("NativeDraftPresentation.cs"),death=R("NativeDeathPresentation.cs"),kill=R("NativeKillEffectPresentation.cs"),scene=R("NativeSceneMotion.cs"),portrait=R("NativePortraits.cs"),drawer=R("NativeAuditorHaloPresentation.cs"),arena=R("ArenaPresentation.cs"),replay=R("NativeReplay.cs"),view=R("NativeViewPresentation.cs");
            Check(source.Contains("if(!first&&!string.IsNullOrEmpty(a.Hat)&&a.SourceSpec!=\"siren\"&&!AuditorHalo(a.Hat,rig.Bones[CapturedRig.Head].localToWorldMatrix,float.IsNaN(halo)?time:halo,1,HaloReduced,layer,camera,color:a.HatColor)"),"arena third person (host, guests, bots) draws the animated halo on the presentation clock, not on the first-person view model");
            Check(arena.Contains("if(SourceCharacter(rig,a,time))return;")&&arena.Contains("if(!a.Alive||(FirstPerson&&a.Id==LocalId)) continue;"),"every third-person actor goes through SourceCharacter; your own pilot is not drawn in first person");
            Check(view.Contains("SourceCharacter(viewRig,shown,time,true)"),"the view model passes first=true (no hat)");
            Check(replay.Contains("presentation.WeaponEffectsQuality=prefs.reducedEffects?.3f:1;")&&replay.Contains("presentation.Render(replayArena.Snapshot(),time,false);"),"the killcam renders through the same path with the reduced setting");
            Check(intro.Contains("SourceCharacter(introRig,actor,arena.Time,weapon:false,halo:age);"),"intro rig animates the halo on the entrance age (player-intros.ts animateCosmetics(rig, age))");
            Check(draft.Contains("!AuditorHalo(seat.hat,draftRig.Bones[CapturedRig.Head].localToWorldMatrix,(float)time,1,reduced,0,null,camera,seat.hatColor)"),"draft stage animates the halo on its clock (draft-stage.ts animateCosmetics(r, this.time))");
            Check(death.Contains("!AuditorHalo(death.actor?.Hat,corpse.PresentedBone(CapturedRig.Head),clock,alpha,HaloReduced,0,null,color:death.actor?.HatColor)"),"corpse hat animates and fades with the body");
            Check(kill.Contains("!AuditorHalo(corpse.Death.actor?.Hat,killLooseHat,corpse.Death.at+age,alpha,HaloReduced,0,null,color:corpse.Death.actor?.HatColor)"),"a hat thrown loose animates");
            Check(scene.Contains("AuditorHalo(death.actor.Hat,Bone(CapturedRig.Head),death.at+age,alpha,HaloReduced,layer,only,color:death.actor.HatColor)"),"death scenes carry the animated halo on their camera route");
            Check(portrait.Contains("haloDraw=NativeAuditorHaloDraw.Create(false);")&&portrait.Contains("haloDraw.Draw(bones[CapturedRig.Head],time,1,Reduced,view,Layer,view,s.HatColor);")&&!portrait.Contains("Auditor halo arc")&&!portrait.Contains("private void Halo("),"portraits use the shared drawer, not a portrait-only halo");
            Check(drawer.Contains("NativeAuditorHalo.Evaluate(time,simple,frame);")&&drawer.Contains("NativeAuditorHalo.Pack(frame,opacity,uniforms);")&&drawer.Contains("Resources.Load<Shader>(\"NativeAuditorHalo\")"),"drawer evaluates Core on the given clock and loads the shader");
            int body=drawer.IndexOf("public void Draw(Matrix4x4 head"),end=drawer.IndexOf("private static Vector4 V(",body);string draw=drawer.Substring(body,end-body);
            Check(draw.Replace("new NVector(","").IndexOf("new ",StringComparison.Ordinal)<0&&!draw.Contains("=>"),"drawer allocates nothing per draw (no new reference objects or closures)");
            Check(arena.Contains("DisposeHalos();"),"the arena releases the drawer and lights");
            string shaderPath=Path.Combine(root,"Assets/ChipArena/Resources/NativeAuditorHalo.shader");Check(File.Exists(shaderPath)&&File.Exists(shaderPath+".meta"),"shader and meta under Resources");
            string shader=File.ReadAllText(shaderPath);
            foreach(var name in new[]{"_HaloA","_HaloSpin","_HaloPulse","_HaloBolts[6]","_HaloFlames[24]","_HaloFins[12]","_HaloEmbers[8]","_HaloDeep","_HaloRed","_HaloHot","_HaloWhite","_HaloGlow","_HaloBolt","_HaloEmber","_HaloSoot","_HaloInner","_HaloRim","_HaloFog","_Src","_Dst","_ZWrite"})Check(shader.Contains(name),"shader declares "+name);
            Check(shader.Contains("float3 Turn(float3 p,float a){float c=cos(a),s=sin(a);return float3(p.x*c+p.z*s,p.y,-p.x*s+p.z*c);}"),"shader turn is Core's ShaderTurn");
            Check(shader.Contains("#pragma multi_compile_fog")&&shader.Contains("// ---- HALO SHARED BEGIN")&&shader.Contains("// ---- HALO SHARED END"),"shader fog variants and shared block");
            Check(File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Editor/NativeBuildChecks.cs")).Contains("\"NativeAuditorHalo\""),"shader registered in NativeBuildChecks");
        }
    }
}
#endif
