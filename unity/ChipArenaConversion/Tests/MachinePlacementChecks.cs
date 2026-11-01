#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // Where the PvE machines stand (owner, 2026-10-15: "some of the machines don't work (I think one of the chip machines are
    // backwards and that one works?)"). Before this, every machine model was drawn at y 0, so the upper floor's two chip
    // dispensers and two med stations stood on the ground floor under their authority spots (6 m below: they never served),
    // while the dressed look drew each machine's collider as a solid block over its model (the working machines read as
    // blank backs), and the Expedition town drew and priced the Ward's eight machines it does not have.
    // On both Ward maps (UnityMaps "siren" and the Expedition's "expedition-ward-zero") every PveContent machine: its model's
    // root is the authority's spot on its own floor; the model's front (+z: the slot, display and screen side) points at the
    // authority's front point (0.7 m out); its collider is the hidden box round the model, at the same yaw; it backs onto a
    // wall and nothing (map block or Ward kit part) stands in front of it; its front point is on its floor, where a body passes
    // the authority's use test (PvE and Expedition) and the same spot one floor away does not; the Expedition's line from every
    // walkable node in front of it is clear. The town draws and prices none. The draws, the label, the prompt and the dressing
    // follow these rules (source checks).
    internal static class MachinePlacementChecks
    {
        private static int count;private static readonly List<string> facts=new List<string>();
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Machine placement: "+what);count++;}
        // An upright box (yaw about +y; Unity's turn: x' = x cos + z sin, z' = -x sin + z cos).
        private struct Box{public string Id;public float Cx,Cy,Cz,Hx,Hy,Hz,Yaw;}
        private static float YawOf(ContentRotation r)=>r==null?0:2*(float)Math.Atan2(r.y,r.w);
        private static bool Upright(ContentRotation r)=>r==null||Math.Abs(r.x)<1e-3f&&Math.Abs(r.z)<1e-3f;
        private static Box Of(ContentBlock b)=>new Box{Id=b.id,Cx=b.pos.x,Cy=b.pos.y,Cz=b.pos.z,Hx=b.size.x/2,Hy=b.size.y/2,Hz=b.size.z/2,Yaw=YawOf(b.rotation)};
        // A box in a machine's model frame (x0..x1, y0..y1, z0..z1) in the world.
        private static Box Local(PveMachine m,float x0,float x1,float y0,float y1,float z0,float z1)
        {var c=NativeMachineRules.World(m,(x0+x1)/2,(y0+y1)/2,(z0+z1)/2);return new Box{Id="local",Cx=c.x,Cy=c.y,Cz=c.z,Hx=(x1-x0)/2,Hy=(y1-y0)/2,Hz=(z1-z0)/2,Yaw=m.yaw};}
        private static bool Overlap(Box a,Box b)
        {
            if(a.Cy+a.Hy<=b.Cy-b.Hy||b.Cy+b.Hy<=a.Cy-a.Hy)return false;
            foreach(var t in new[]{a.Yaw,a.Yaw+Math.PI/2,b.Yaw,b.Yaw+Math.PI/2})
            {
                double ux=Math.Sin(t),uz=Math.Cos(t);double R(Box o)=>o.Hx*Math.Abs(Math.Cos(o.Yaw)*ux-Math.Sin(o.Yaw)*uz)+o.Hz*Math.Abs(Math.Sin(o.Yaw)*ux+Math.Cos(o.Yaw)*uz);
                if(Math.Abs((a.Cx-b.Cx)*ux+(a.Cz-b.Cz)*uz)>=R(a)+R(b)-1e-4)return false;
            }
            return true;
        }
        private static bool Inside(Box b,float x,float y,float z)=>Segment(b,x,y,z,x,y,z);
        private static bool Segment(Box b,float ax,float ay,float az,float bx,float by,float bz)
        {
            float c=(float)Math.Cos(b.Yaw),s=(float)Math.Sin(b.Yaw);
            void L(float x,float y,float z,out float lx,out float ly,out float lz){float dx=x-b.Cx,dz=z-b.Cz;lx=dx*c-dz*s;lz=dx*s+dz*c;ly=y-b.Cy;}
            L(ax,ay,az,out float px,out float py,out float pz);L(bx,by,bz,out float qx,out float qy,out float qz);
            float t0=0,t1=1;float[] p={px,py,pz},d={qx-px,qy-py,qz-pz},h={b.Hx,b.Hy,b.Hz};
            for(int i=0;i<3;i++){if(Math.Abs(d[i])<1e-9f){if(p[i]<-h[i]||p[i]>h[i])return false;continue;}float u=(-h[i]-p[i])/d[i],v=(h[i]-p[i])/d[i];if(u>v){var w=u;u=v;v=w;}t0=Math.Max(t0,u);t1=Math.Min(t1,v);if(t0>t1)return false;}
            return true;
        }
        private static double Angle(double a,double b){double d=Math.IEEERemainder(a-b,Math.PI*2);return Math.Abs(d)*180/Math.PI;}
        public static void Run(string root)
        {
            count=0;facts.Clear();var o=new JsonSerializerOptions{IncludeFields=true};string U(string f)=>Path.Combine(root,"Assets/Conversion/Resources",f);
            var maps=JsonSerializer.Deserialize<UnityMapContent>(File.ReadAllText(U("UnityMaps.json")),o);var pve=JsonSerializer.Deserialize<NativePveContent>(File.ReadAllText(U("PveContent.json")),o);
            var entries=JsonSerializer.Deserialize<NativeWardEntrySet>(File.ReadAllText(U("PveContent.json")),o).entries;
            Rules(maps,pve);Models();
            foreach(var id in new[]{"siren","expedition-ward-zero"})Placed(maps.maps.Single(m=>m.id==id),pve,entries);
            Sources(root);
            Console.WriteLine("PASS machine placement ("+count+" checks: on both Ward maps every machine drawn at its authority spot on its own floor, its front (slot and screen side) on the authority's front point, its collider the hidden box round it at its yaw, its back on a wall, nothing in front, the use test passing at its front and failing a floor away, a clear Expedition line from every walkable node in front; none drawn or priced in the town; the draws, label, prompt and dressing follow; "+string.Join(", ",facts)+")");
        }
        // The rules: where machines stand at all, and which blocks are their colliders.
        private static void Rules(UnityMapContent maps,NativePveContent pve)
        {
            Check(NativeMachineRules.Present("siren",false)&&NativeMachineRules.Present("siren",true)&&NativeMachineRules.Present("expedition-ward-zero",true)&&!NativeMachineRules.Present("expedition-city",true),"machines stand in every PvE round and in Ward instances, not in the Expedition town");
            Check(NativeMachineRules.Present("expedition-transit",true)&&NativeMachineRules.Present("expedition-foundry",true)&&!NativeMachineRules.Present("expedition-unknown",true),"new authored instances permit their own machines; unknown maps fail closed");
            Check(pve.machines.Length==8&&pve.machineBlocks.Length==pve.machines.Length&&pve.machines.Select((m,i)=>m.m==i).All(x=>x),"eight machines, each with its collider at its index");
            Check(pve.machineBlocks.All(b=>NativeMachineModels.IsCollider(b.id)&&b.material=="foundation"),"every machine collider is named as one (the dressing hides them)");
            Check(maps.maps.All(m=>m.blocks.All(b=>!NativeMachineModels.IsCollider(b?.id))),"no map block is mistaken for a machine collider");
            Check(pve.machines.Count(m=>m.y>3)==4&&pve.machines.Count(m=>m.y==0)==4,"four machines on the ground floor, four on the upper floor (y 6)");
        }
        // The model's front is +z: its front face (the slot, the display, the pod door, the cross and screen) carries its lights,
        // its back face (against the wall) none; the slot and the flap, door and pulse pivots sit in front.
        private static void Models()
        {
            foreach(var kind in NativeMachineModels.Kinds)
            {
                var mesh=NativeMachineModels.Build(kind);double ahead=0,behind=0;
                foreach(var s in mesh.Sections)
                {
                    if(s.Bone!=NativeMachineModels.Lights)continue;
                    for(int i=0;i<s.Indices.Count;i+=3){var a=s.Positions[s.Indices[i]];var b=s.Positions[s.Indices[i+1]];var c=s.Positions[s.Indices[i+2]];double area=System.Numerics.Vector3.Cross(b-a,c-a).Length()/2,z=(a.Z+b.Z+c.Z)/3;if(z>NativeMachineModels.BlockMax.Z-.1f)ahead+=area;else if(z<NativeMachineModels.BlockMin.Z+.1f)behind+=area;}
                }
                Check(ahead>.05&&behind<.01,kind+": its front face (+z) carries its lights, its back none ("+ahead.ToString("F3")+" m2 in front, "+behind.ToString("F3")+" at the back)");
            }
            Check(NativeMachineModels.Slot.Z>0&&NativeMachineModels.FlapPivot.Z>0&&NativeMachineModels.DoorPivot.Z>0&&NativeMachineModels.PulsePivot.Z>0&&NativeMachineModels.BlockMin.Z<-.5f,"the slot and the flap, door and pulse pivots are in front, the collider deep behind");
        }
        private static void Placed(ContentMap map,NativePveContent pve,NativeWardEntry[] entries)
        {
            string id=map.id;var blocks=map.blocks.Where(b=>b?.id!=null).ToArray();var walls=blocks.Where(b=>Upright(b.rotation)).Select(Of).ToList();
            var colliders=pve.machineBlocks.Select(Of).ToList();var solid=walls.Concat(colliders).ToList();
            var kit=NativeWardZeroKit.Build(map.blocks,map.hazards,entries);var parts=new List<Box>();
            foreach(var p in kit.Parts){var parent=map.blocks[p.Parent];if(!Upright(parent.rotation))continue;float y=YawOf(parent.rotation),c=(float)Math.Cos(y),s=(float)Math.Sin(y);parts.Add(new Box{Id=parent.id+" kit",Cx=parent.pos.x+p.X*c+p.Z*s,Cy=parent.pos.y+p.Y,Cz=parent.pos.z-p.X*s+p.Z*c,Hx=p.Hx,Hy=p.Hy,Hz=p.Hz,Yaw=y});}
            int nodes=0,blockedBeside=0;
            foreach(var m in pve.machines)
            {
                string w=id+" "+m.id+" ("+m.kind+")";var f=m.front;var rootAt=NativeMachineRules.Root(m);
                // Drawn where the authority stands it, on its own floor.
                Check(rootAt.x==m.x&&rootAt.y==m.y&&rootAt.z==m.z,w+": the model's root is the authority's spot ("+m.x+", "+m.y+", "+m.z+")");
                bool Floor(float x,float y,float z)=>walls.Any(b=>Inside(b,x,y-.05f,z)&&Math.Abs(b.Cy+b.Hy-y)<.06f);
                Check(Floor(m.x,m.y,m.z)&&Floor(f.x,f.y,f.z)&&f.y==m.y,w+": it and its front point stand on the floor at y "+m.y);
                // Its front (+z turned by the yaw) is the authority's front.
                var ahead=NativeMachineRules.World(m,0,0,1);double fx=ahead.x-m.x,fz=ahead.z-m.z,ox=f.x-m.x,oz=f.z-m.z,ol=Math.Sqrt(ox*ox+oz*oz);
                Check(ol>.6&&ol<.8&&Math.Acos(Math.Max(-1,Math.Min(1,(fx*ox+fz*oz)/ol)))*180/Math.PI<.5,w+": the model's front faces the authority's front point ("+ol.ToString("F2")+" m out)");
                var slot=NativeMachineRules.Slot(m);var want=NativeMachineRules.World(m,NativeMachineModels.Slot.X,NativeMachineModels.Slot.Y,NativeMachineModels.Slot.Z);var label=NativeMachineRules.Label(m);
                Check(Math.Abs(slot.x-want.x)<1e-4f&&Math.Abs(slot.y-want.y)<1e-4f&&Math.Abs(slot.z-want.z)<1e-4f&&slot.y>m.y+1&&Math.Abs(label.y-m.y-2.62f)<1e-4f,w+": the chip leaves its slot and the price hangs over it on its own floor");
                // The collider is the hidden box round the model, at the machine's yaw.
                var col=pve.machineBlocks[m.m];var cc=NativeMachineRules.World(m,0,1.1f,-.2f);
                Check(Upright(col.rotation)&&Angle(YawOf(col.rotation),m.yaw)<.5&&Math.Abs(col.pos.x-cc.x)<.01f&&Math.Abs(col.pos.y-cc.y)<.01f&&Math.Abs(col.pos.z-cc.z)<.01f
                    &&Math.Abs(col.size.x-(NativeMachineModels.BlockMax.X-NativeMachineModels.BlockMin.X))<1e-3f&&Math.Abs(col.size.y-NativeMachineModels.BlockMax.Y)<1e-3f&&Math.Abs(col.size.z-(NativeMachineModels.BlockMax.Z-NativeMachineModels.BlockMin.Z))<1e-3f,w+": its collider is the model's box at its yaw");
                // It backs onto a wall; nothing stands in front of it (map blocks, Ward kit trim and dressing).
                Check(walls.Any(b=>Overlap(b,Local(m,-.4f,.4f,.5f,1.5f,-.95f,-.62f))),w+": its back is on a wall");
                var front=Local(m,-.6f,.6f,.1f,2f,NativeMachineModels.BlockMax.Z+.01f,1.6f);
                var inFront=walls.Where(b=>Overlap(b,front)).Select(b=>b.Id).Concat(parts.Where(b=>Overlap(b,front)).Select(b=>b.Id)).ToArray();
                Check(inFront.Length==0,w+": nothing stands in front of it ("+string.Join(", ",inFront)+")");
                // The authority's use test at its front point, and the same spot one floor away (the old draw put the upper four there).
                float body=f.y+NativeLocomotionRules.HalfHeight+.04f;bool Pve(float by)=>Math.Abs(f.y-by)<=1.7f;bool Exp(float feetY)=>Math.Abs(f.y-feetY)<=2.2f;
                float away=m.y>3?m.y-6:m.y+6;
                Check(Pve(body)&&Exp(f.y+.04f)&&!Pve(away+NativeLocomotionRules.HalfHeight+.04f)&&!Exp(away+.04f),w+": a body at its front is served, the same spot a floor away is not");
                // The Expedition's line from every walkable node in front of it within the use range is clear.
                foreach(var n in map.nav.nodes)
                {
                    float feet=n.pos.y-.9f+.04f;double d=Math.Sqrt((f.x-n.pos.x)*(f.x-n.pos.x)+(f.y-feet)*(f.y-feet)+(f.z-n.pos.z)*(f.z-n.pos.z));if(d>2.2)continue;
                    float dx=n.pos.x-m.x,dz=n.pos.z-m.z,lz=dx*(float)Math.Sin(m.yaw)+dz*(float)Math.Cos(m.yaw);
                    bool clear=!solid.Any(b=>Segment(b,n.pos.x,feet+.6f,n.pos.z,f.x,f.y+.6f,f.z));
                    if(lz<NativeMachineModels.BlockMax.Z){if(!clear)blockedBeside++;continue;}
                    nodes++;Check(clear,w+": the Expedition's line from "+n.id+" is clear");
                }
            }
            Check(nodes>=pve.machines.Length*4,id+": walkable nodes in front of every machine ("+nodes+")");
            facts.Add(id+" "+nodes+" approach lines clear ("+blockedBeside+" from beside a machine meet its own body; the prompt mirrors that)");
        }
        // The draws, the price label, the use prompt and the dressing follow the rules; the authority's tests are the ones these
        // checks mirror (a change there is a change here).
        private static void Sources(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));var pres=R("NativeMachinePresentation.cs");var hud=R("NativeCombatHud.cs");var dress=R("NativeArenaDressing.cs");
            Check(pres.Contains("if(arena.Pve==null||!NativeMachineRules.Present(arena.MapId,arena.Expedition!=null))return;")&&pres.Contains("var root=Matrix4x4.TRS(NativeArena.Vec(NativeMachineRules.Root(m)),Quaternion.Euler(0,m.yaw*Mathf.Rad2Deg,0),Vector3.one);")&&pres.Contains("var from=NativeMachineRules.Slot(machine);")&&!pres.Contains("new Vector3(m.x,0,m.z)"),"the models stand at their spots on their own floors, only where machines stand");
            Check(hud.Contains("bool machines=NativeMachineRules.Present(arena.MapId,arena.Expedition!=null);")&&hud.Contains("if(machines)foreach(var m in arena.Pve.Content.machines)if(ScreenPoint(NativeArena.Vec(NativeMachineRules.Label(m)),24,out var point))")&&!hud.Contains("new Vector3(m.x,2.62f,m.z)"),"the prices hang over the machines on their own floors, none in the town");
            Check(hud.Contains("if(MachineInReach(arena,local,m))GUI.Label(")&&hud.Contains("if(arena.Expedition==null)return Math.Abs(front.y-body.y)<=1.7f&&new Vector2(front.x-body.x,front.z-body.z).magnitude<=2.2f;")&&hud.Contains("return (front-feet).magnitude<=2.2f&&arena.ExpeditionVisible(NativeArena.Vec(feet),m.front.Position());"),"the use prompt mirrors the authority's tests");
            Check(dress.Contains("e.Machine=prop.LocalFixture&&!prop.Fragment&&NativeMachineModels.IsCollider(b.id);")&&dress.Contains("!entries.TryGetValue(p,out var e)||e.Machine)continue;")&&dress.IndexOf("e.Machine=",StringComparison.Ordinal)<dress.IndexOf("r.forceRenderingOff=true",dress.IndexOf("private Entry Create(",StringComparison.Ordinal),StringComparison.Ordinal),"the dressing never draws a machine collider (its cube stays off)");
            string pveSrc=R("NativePve.cs"),exp=R("NativeExpeditionM1Arena.cs");
            Check(pveSrc.Contains("float Flat(Vector3 q)=>Math.Abs(q.y-p.Body.position.y)>1.7f?float.PositiveInfinity:new Vector2(q.x-p.Body.position.x,q.z-p.Body.position.z).magnitude;")&&pveSrc.Contains("Pve.Content.machines.Where(x=>Flat(Vec(x.front.Position()))<=2.2f)"),"the PvE use test is the one the prompt mirrors");
            Check(exp.Contains("Pve.Content.machines.Where(m=>(m.front.Position()-feet).Length<=2.2f&&ExpeditionVisible(feet,m.front.Position()))")&&exp.Contains("if(NativeMapRules.ExpeditionInstance(MapId))BuildPveMachines(data);")&&exp.Contains("public bool ExpeditionVisible(V3 from,V3 to)=>!SceneLinecast(Vec(from)+Vector3.up*.6f,Vec(to)+Vector3.up*.6f,WorldMask);"),"the Expedition builds and serves machines only in recognized instances, with the test the prompt mirrors");
        }
    }
}
#endif
