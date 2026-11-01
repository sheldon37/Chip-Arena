#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // Tests/cinematic-camera-parity.json is written by tools/export_cinematic_cameras.mjs from the pinned DemonCinematic
    // (client/demon-cinematic.ts) and SirenCutscene (client/siren/cutscene.ts) driven with synthetic snapshots; the C# port
    // (Core/NativeCinematicRules.cs) must place the same cameras, backdrops and letterbox from the same inputs.
    internal static class CinematicCameraChecks
    {
        public static IEnumerable<string> Run(string root)
        {
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/cinematic-camera-parity.json")));var json=document.RootElement;var passed=new List<string>();
            void Check(bool ok,string what){if(!ok)throw new Exception("Cinematic cameras: "+what);}
            bool Near(double a,double b,double tolerance)=>Math.Abs(a-b)<=tolerance;
            V3 Arr(JsonElement e)=>new V3(e[0].GetSingle(),e[1].GetSingle(),e[2].GetSingle());
            V3 Obj(JsonElement e)=>new V3(e.GetProperty("x").GetSingle(),e.GetProperty("y").GetSingle(),e.GetProperty("z").GetSingle());
            bool NearV(V3 a,JsonElement b,double tolerance)=>Near(a.x,b[0].GetDouble(),tolerance)&&Near(a.y,b[1].GetDouble(),tolerance)&&Near(a.z,b[2].GetDouble(),tolerance);
            V3 Cross(V3 a,V3 b)=>new V3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);

            // The clocks the ports hard-code.
            var raging=json.GetProperty("raging");
            Check(Near(NativeDemonScreen.Contact,raging.GetProperty("contact").GetDouble(),1e-7)&&Near(NativeDemonScreen.Combo,raging.GetProperty("combo").GetDouble(),1e-7)&&Near(NativeDemonScreen.Symbol,raging.GetProperty("symbol").GetDouble(),1e-7)&&Near(NativeDemonScreen.Reveal,raging.GetProperty("reveal").GetDouble(),1e-7)&&Near(NativeDemonScreen.End,raging.GetProperty("end").GetDouble(),1e-7),"RAGING_DEMON window");
            var hits=raging.GetProperty("hits");Check(hits.GetArrayLength()==NativeDemonScreen.HitCount,"RAGING_DEMON hit count");for(int i=0;i<NativeDemonScreen.HitCount;i++)Check(Near(NativeDemonScreen.Hit(i),hits[i].GetDouble(),1e-7),"RAGING_DEMON hit "+i);
            var laugh=json.GetProperty("laugh");Check(Near(NativeSirenCutscene.Crush,laugh.GetProperty("crush").GetDouble(),1e-7)&&Near(NativeSirenCutscene.Release,laugh.GetProperty("release").GetDouble(),1e-7)&&Near(NativeSirenCutscene.Tail,json.GetProperty("tail").GetDouble(),1e-7),"SIREN_TIMING.laugh crush/release and CUTSCENE_TAIL");
            passed.Add("cinematic cameras: RAGING_DEMON clock, SIREN_TIMING.laugh crush/release and CUTSCENE_TAIL match the source");

            // DemonCinematic: the private camera, its framing for narrow windows, the backdrop and the black opening cut.
            var deaths=new Dictionary<string,JsonElement>();foreach(var d in json.GetProperty("deaths").EnumerateArray())deaths[d.GetProperty("name").GetString()]=d;
            int frames=0,on=0;
            foreach(var c in json.GetProperty("demon").EnumerateArray())
            {
                var d=deaths[c.GetProperty("death").GetString()];bool attacker=d.TryGetProperty("attacker",out var a);float age=c.GetProperty("age").GetSingle(),aspect=c.GetProperty("aspect").GetSingle();
                string what=c.GetProperty("death").GetString()+" age "+age+" aspect "+aspect;
                bool active=NativeDemonScreen.Screen(age,Obj(d.GetProperty("pos")),d.GetProperty("yaw").GetSingle(),attacker,attacker?Obj(a):default,aspect,out var f);
                Check(active==c.GetProperty("on").GetBoolean(),"window "+what);frames++;if(!active)continue;on++;
                V3 look=(f.Look-f.Eye).Normalized;
                Check(NearV(f.Eye,c.GetProperty("eye"),2e-4),"eye "+what);Check(NearV(look,c.GetProperty("dir"),2e-4),"look "+what);Check(Near(f.Fov,c.GetProperty("fov").GetDouble(),1e-5),"fov "+what);
                var b=c.GetProperty("backdrop");Check(Near(f.R,b[0].GetDouble(),2e-6)&&Near(f.G,b[1].GetDouble(),2e-6)&&Near(f.B,b[2].GetDouble(),2e-6),"backdrop "+what);
                Check(!f.Black==c.GetProperty("visible").GetBoolean(),"scene visibility "+what);
            }
            Check(on>200&&on<frames,"demon coverage");
            passed.Add("cinematic cameras: Raging Demon victim screen matches DemonCinematic over "+on+" of "+frames+" frames (window, five shots, narrow-aspect pull-back, attacker/near/yaw facing, linear backdrop pulse, black cut)");

            // SirenCutscene: story start/end per role, interruption, phase change, eased facing, rig heads, the four shots,
            // wall pull-in, the shake (position and roll) and the letterbox slide.
            var grab=json.GetProperty("grab");float at=grab.GetProperty("at").GetSingle(),crushAt=grab.GetProperty("crush").GetSingle(),releaseAt=grab.GetProperty("release").GetSingle();
            int takes=0,shots=0,walls=0;
            foreach(var take in json.GetProperty("siren").EnumerateArray())
            {
                string name=take.GetProperty("name").GetString();bool victimRole=take.GetProperty("role").GetString()=="victim",rig=take.GetProperty("rig").GetBoolean(),victimRig=take.GetProperty("victimRig").GetBoolean(),wall=take.GetProperty("wall").GetBoolean();float wallX=take.GetProperty("wallX").GetSingle();
                var cut=new NativeSirenCutscene();bool seenActive=false,seenEnd=false;
                foreach(var fr in take.GetProperty("frames").EnumerateArray())
                {
                    float time=(float)fr.GetProperty("time").GetDouble();string what=name+" t="+time;string phase=fr.GetProperty("phase").GetString();
                    bool active=cut.Check(fr.GetProperty("grab").GetBoolean(),1,2,victimRole,at,crushAt,releaseAt,time,phase=="fight"||phase=="result");
                    Check(active==fr.GetProperty("active").GetBoolean(),"story "+what);if(!active){if(seenActive)seenEnd=true;continue;}seenActive=true;
                    Check(cut.VictimRole==(fr.GetProperty("role").GetString()=="victim"),"role "+what);
                    cut.Track(time,true,Arr(fr.GetProperty("owner")),(float)(rig?fr.GetProperty("rigYaw").GetDouble():fr.GetProperty("yaw").GetDouble()),fr.GetProperty("scale").GetSingle(),rig,Arr(fr.GetProperty("sirenHead")),victimRig&&fr.GetProperty("victimHp").GetDouble()>0,Arr(fr.GetProperty("victimHead")));
                    var shot=cut.ShotAt(time);V3 position=shot.Position;
                    if(wall){V3 dir=position-shot.Look;float d=dir.Length;if(d>.05f){dir=dir/d;if(dir.x<-1e-9){float hit=(wallX-shot.Look.x)/dir.x;if(hit>=0&&hit<=d){var moved=NativeSirenCutscene.Occlude(position,shot.Look,hit);if((moved-position).Length>1e-6)walls++;position=moved;}}}}
                    NativeSirenCutscene.Shake(time,shot.Shake,out float dx,out float dy,out float roll);
                    V3 z=(position-shot.Look).Normalized,x=Cross(new V3(0,1,0),z).Normalized,y=Cross(z,x),up=y*(float)Math.Cos(roll)-x*(float)Math.Sin(roll);
                    Check(NearV(new V3(position.x+dx,position.y+dy,position.z),fr.GetProperty("position"),3e-4),"position "+what);
                    Check(NearV(z*-1,fr.GetProperty("dir"),3e-4),"direction "+what);Check(NearV(up,fr.GetProperty("up"),3e-4),"roll "+what);
                    Check(Near(shot.Fov,fr.GetProperty("fov").GetDouble(),2e-4),"fov "+what);
                    float k=cut.Letterbox(time);Check(k>0==fr.GetProperty("bars").GetBoolean()&&(k<=0||Near(k,fr.GetProperty("k").GetDouble(),2e-4)),"letterbox "+what);
                    shots++;
                }
                Check(seenActive&&seenEnd,"take "+name+" starts and ends");takes++;
            }
            Check(walls>10,"wall pull-in coverage");
            passed.Add("cinematic cameras: Last Laugh cutscene matches SirenCutscene over "+takes+" takes and "+shots+" frames (both roles, interruption, release at the crush, phase end, eased facing, rig heads, grown Siren, "+walls+" wall pull-ins, shake and roll, letterbox)");
            return passed;
        }
    }
}
#endif
