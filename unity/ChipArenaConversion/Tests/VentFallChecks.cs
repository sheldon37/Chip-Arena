#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // A pilot on the model who falls into an open Ward Zero vent drops down its shaft as its ragdoll (Core NativeVentFall, Runtime
    // NativeVentFallPresentation.cs; owner 2026-11-13: "Falling through vent still reverts back to voxel model, but when falling down the
    // player should ragdoll"): the vent each song vent's death finds, the floor laid round the opening, the shaft's walls, the hide
    // depth against the drawn pit, the splat's drop, and the Runtime hooks (the voxel victim kept for the voxel bodies only).
    internal static class VentFallChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Vent fall: "+what);count++;}
        private static void Near(double a,double b,string what,double tolerance=1e-4){Check(Math.Abs(a-b)<=tolerance&&!double.IsNaN(a),what+": "+a+" != "+b);}
        private static bool Inside(V3 p,V3 min,V3 max)=>p.x>min.x+1e-4f&&p.x<max.x-1e-4f&&p.y>min.y+1e-4f&&p.y<max.y-1e-4f&&p.z>min.z+1e-4f&&p.z<max.z-1e-4f;
        public static void Run(string root)
        {
            count=0;
            // The song vents (PveContent's entries with a song side: StageVents) and Ward Zero's floor (its slab), from the shipped data.
            using var pve=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/PveContent.json")));
            var vents=pve.RootElement.GetProperty("entries").EnumerateArray().Where(e=>e.TryGetProperty("song",out var s)&&s.GetInt32()>=0).OrderBy(e=>e.GetProperty("e").GetInt32())
                .Select(e=>{var f=e.GetProperty("from");return new V3((float)f.GetProperty("x").GetDouble(),(float)f.GetProperty("y").GetDouble(),(float)f.GetProperty("z").GetDouble());}).ToArray();
            Check(vents.Length==8,"eight song vents");
            using var maps=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json")));
            foreach(string id in new[]{"siren","expedition-ward-zero"})
            {
                var map=maps.RootElement.GetProperty("maps").EnumerateArray().First(m=>m.GetProperty("id").GetString()==id);
                var floors=map.GetProperty("blocks").EnumerateArray().Select(b=>{var p=b.GetProperty("pos");var s=b.GetProperty("size");float px=(float)p.GetProperty("x").GetDouble(),py=(float)p.GetProperty("y").GetDouble(),pz=(float)p.GetProperty("z").GetDouble(),sx=(float)s.GetProperty("x").GetDouble()/2,sy=(float)s.GetProperty("y").GetDouble()/2,sz=(float)s.GetProperty("z").GetDouble()/2;return (id:b.GetProperty("id").GetString(),min:new V3(px-sx,py-sy,pz-sz),max:new V3(px+sx,py+sy,pz+sz));}).ToArray();
                for(int e=0;e<vents.Length;e++)
                {
                    var v=vents[e];string at=" "+id+" vent "+e;
                    // The rules' fall (NativeVentRules.Falls) is always inside the opening the shaft has.
                    foreach(var d in new[]{new V3(0,.9f,0),new V3(.77f,.9f,.77f),new V3(-.77f,.9f,.3f),new V3(.5f,1.2f,-.77f),new V3(0,0,0)})
                    {var p=v+d;Check(NativeVentRules.Falls(p,v,1),"a fall by the rules"+at);Check(NativeVentFall.Vent(p,vents)==e,"the death finds its vent"+at+" at "+d.x+","+d.y+","+d.z);}
                    Check(NativeVentFall.Vent(v+new V3(NativeVentFall.Half+.05f,.9f,0),vents)<0&&NativeVentFall.Vent(v+new V3(0,5,0),vents)<0&&NativeVentFall.Vent(v+new V3(0,-3,0),vents)<0,"no vent off the opening"+at);
                    // The floors the opening cuts: Ward Zero's slab, and nothing above or beside it.
                    var cut=floors.Where(f=>NativeVentFall.Cuts(f.min,f.max,v)).ToArray();Check(cut.Length==1&&cut[0].id=="siren-slab","the slab alone is cut"+at+": "+string.Join(",",cut.Select(c=>c.id)));
                    var slab=cut[0];var pieces=new V3[8];int n=NativeVentFall.Around(slab.min,slab.max,v,pieces);Check(n==4,"four pieces round the opening"+at);
                    double area=0;for(int i=0;i<n;i++){var a=pieces[i*2];var b=pieces[i*2+1];area+=(b.x-a.x)*(double)(b.z-a.z);Check(a.x>=slab.min.x-1e-4f&&b.x<=slab.max.x+1e-4f&&a.z>=slab.min.z-1e-4f&&b.z<=slab.max.z+1e-4f&&a.y==slab.min.y&&b.y==slab.max.y,"each piece inside the slab"+at);
                        for(int j=i+1;j<n;j++){var c=pieces[j*2];var d=pieces[j*2+1];Check(!(a.x<d.x-1e-4f&&c.x<b.x-1e-4f&&a.z<d.z-1e-4f&&c.z<b.z-1e-4f),"pieces do not overlap"+at);}}
                    double whole=(slab.max.x-slab.min.x)*(double)(slab.max.z-slab.min.z),hole=4*NativeVentFall.Half*(double)NativeVentFall.Half;Near(area,whole-hole,"the pieces are the slab less the opening"+at,1e-2);
                    var shaft=new V3[10];NativeVentFall.Shaft(v,shaft);
                    // The opening is clear to the bottom; the floor round it and the walls under it hold a body in; the bottom stops it.
                    for(float y=.2f;y>-NativeVentFall.Depth+.05f;y-=.4f)foreach(var d in new[]{new V3(0,0,0),new V3(NativeVentFall.Half-.05f,0,NativeVentFall.Half-.05f),new V3(-(NativeVentFall.Half-.05f),0,.4f)})
                    {
                        var p=v+d+new V3(0,y,0);bool blocked=false;for(int i=0;i<n;i++)blocked|=Inside(p,pieces[i*2],pieces[i*2+1]);for(int k=0;k<5;k++)blocked|=Inside(p,shaft[k*2],shaft[k*2+1]);
                        Check(!blocked,"the opening is clear at depth "+(-y)+at);
                    }
                    foreach(var d in new[]{new V3(NativeVentFall.Half+.1f,-.1f,0),new V3(-(NativeVentFall.Half+.1f),-3,.5f),new V3(.3f,-6,NativeVentFall.Half+.1f),new V3(-.2f,-1,-(NativeVentFall.Half+.1f)),new V3(.4f,-NativeVentFall.Depth-.1f,.2f)})
                    {var p=v+d;bool held=false;for(int i=0;i<n;i++)held|=Inside(p,pieces[i*2],pieces[i*2+1]);for(int k=0;k<5;k++)held|=Inside(p,shaft[k*2],shaft[k*2+1]);Check(held,"the shaft holds a body in at "+d.x+","+d.y+","+d.z+at);}
                    Check(!NativeVentFall.Hidden(v.y-.5f,v.y)&&NativeVentFall.Hidden(v.y-NativeVentFall.HideBelow-.01f,v.y),"hide depth"+at);
                }
                // A floor away from every vent is not cut, nor one that is not at the vent's floor.
                Check(!NativeVentFall.Cuts(new V3(10,-.5f,0),new V3(12,0,2),vents[0])&&!NativeVentFall.Cuts(new V3(2,1,5),new V3(5,1.6f,8),vents[0]),"only the floor at the opening is cut");
            }
            // A body falling in reaches the hide depth before the drawn pit closes over a fall at the rules' last open moment (the rules'
            // open falls under .5 about .2 s past closeAt; the drawn PvP pit lasts .5 s past it and fades over .4 s).
            float t=0,y0=0,vy=-NativeVentFall.FallSpeed;while(y0>-NativeVentFall.HideBelow&&t<5){vy-=NativeVentFall.BodyGravity/60f;y0+=vy/60f;t+=1f/60;}
            Check(t<.5f+.4f-.2f,"hidden before the drawn pit closes: "+t+" s");
            Check(NativeVentFall.Falls("vent-fall")&&!NativeVentFall.Falls("vent-splat")&&!NativeVentFall.Falls("impact-ragdoll"),"only the fall has a shaft");
            // The splat starts SplatLift over its spot and is back at its spot's height at VENT_PIT.drop, under the body speed cap.
            float drop=NativeVentDeath.Drop,v0=NativeVentFall.SplatSpeed;Near(NativeVentFall.SplatLift-(v0*drop+.5f*NativeVentFall.BodyGravity*drop*drop),0,"the splat lands at VENT_PIT.drop");
            Check(v0>0&&v0+NativeVentFall.BodyGravity*drop<=NativeKillEffectRules.MaxBodySpeed,"the splat lands under the body speed cap: "+(v0+NativeVentFall.BodyGravity*drop));
            Near(NativeVentFall.SplatTip*drop,Math.PI/2,"the splat turns face down over its drop");
            Hooks(root);
            Console.WriteLine("PASS vent fall ("+count+" checks: each song vent's death finds its shaft, the slab laid round the opening, walls and bottom, hide depth, splat drop, runtime hooks)");
        }
        private static void Hooks(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
            string death=R("NativeDeathPresentation.cs"),world=R("NativeCorpseWorld.cs"),fall=R("NativeVentFallPresentation.cs"),vent=R("NativeVentDeathPresentation.cs");
            Check(death.Contains("if(VentStyle(death.style)){if(!VentModel(death,pilot)){PresentVent(")&&death.Contains("VentFresh(death);}"),"a vent death on the model takes the corpse (the stale fall dropped), the voxel victim otherwise");
            Check(fall.Contains("private bool VentModel(NativeDeath death,bool pilot)=>pilot&&VisualDeathFor(death);"),"the model's ragdoll wherever the pilot's death wears the model");
            Check(death.Contains("if(VentStyle(death.style))VentLaunch(corpse,death);")&&death.Contains("if(VentHidden(corpse,center))alpha=0;")&&death.Contains("VentSplatFx(death,fx);"),"launched down the shaft, hidden under it, the splat's dust and pop");
            Check(world.Contains("VentShaftOnce();")&&world.Contains("if(!near||VentFloor(prop,original,bounds))continue;"),"the shaft in the body's physics world");
            // The voxel victim is kept, whole, for the voxel bodies (a voxel map could choose it the same way).
            Check(vent.Contains("NativeVentDeath.Fall(")&&vent.Contains("NativeVentDeath.Splat(")&&vent.Contains("sourceModels.Queue(pilot,root,v.Bone,null")&&vent.Contains("VentSplatExtras(v.Dust,death,v.Since);"),"the scripted voxel victim kept");
            Check(File.Exists(Path.Combine(root,"Assets/ChipArena/Core/NativeVentDeath.cs")),"the voxel victim's Core kept");
        }
    }
}
#endif
