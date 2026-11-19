using System;
using System.IO;
using System.Linq;
using ChipArena.UnityConversion;

/// <summary>
/// The DEV voxel draw counter (Core NativeVoxelDraws, Runtime NativeVoxelDrawCount and NativeVoxelOverlay; docs/VOXEL-STYLE.md;
/// docs/VOXEL-RETIREMENT.md V0): the bank's id grammar sorts into the plan's kinds (the voxel bodies, weapons, hats, enemies, cubes and
/// pieces; the models' own hats, gear, helmets, suits, pods, chips and the Toy Soldier's figure are not voxel); the tally keeps the last
/// whole frame and names the unexpected; and its hooks are where the draws are, off unless the counter is on.
/// </summary>
internal static class VoxelDrawChecks
{
    private static int checks;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Voxel draws: "+what);checks++;}
    public static void Run(string root)
    {
        checks=0;
        // 1. The kinds, by the bank's ids.
        void Is(string id,NativeVoxelDraws.Kind k,string part=null)=>Check(NativeVoxelDraws.Of(id,part)==k,id+(part!=null?" ("+part+")":"")+" is "+k+", not "+NativeVoxelDraws.Of(id,part));
        for(int skin=0;skin<NativeBodySkins.Count;skin++)for(int t=0;t<NativeBodySkins.Tiers;t++)Is(NativeBodySkins.Model(skin,t),NativeVoxelDraws.Kind.PilotBody);
        Is("wounded-12",NativeVoxelDraws.Kind.PilotBody);Is("wounded-12/pilot-std-low",NativeVoxelDraws.Kind.PilotBody);
        Is("corpse-4/pilot-sheldon/1",NativeVoxelDraws.Kind.PilotBody);Is("corpse-4/pilot-sheldon/2",NativeVoxelDraws.Kind.Hat);Is("corpse-4/expedition-pod/0",NativeVoxelDraws.Kind.None);
        foreach(var id in new[]{"pilot-gear-core","pilot-gear-device","pilot-helm","pilot-half-mask","pilot-helm-broken-0","pilot-suit","expedition-pod","expedition-pod-ghost","pve-pickup-std",NativeToySoldier.Model,"native-tnt-round-1","kill-drop","kill-pool","revive-ring","revive-column","hat-cap+model","hat-cap+model+tint","iw-prop-rake-0","station-lantern-full"})
            Is(id,NativeVoxelDraws.Kind.None);
        foreach(var spec in NativeWeaponSkins.Weapons.Where(NativeWeaponSkins.Modelled))
        {
            Is(NativeWeaponSkins.PrototypeHeld(spec),NativeVoxelDraws.Kind.Weapon,"blade");
            if(NativeWeaponModels.Designed(spec))for(int t=0;t<NativeWeaponSkins.Tiers;t++)Is(NativeWeaponSkins.StandardModel(spec,t),NativeVoxelDraws.Kind.FrameSculpt);
        }
        Is("weapon-katana",NativeVoxelDraws.Kind.FrameSculpt,"std-katana");Is("loose-fangs",NativeVoxelDraws.Kind.FrameSculpt,"std-loose-fangs");Is("katana-saya",NativeVoxelDraws.Kind.FrameSculpt);
        Is("weapon-dev-greatsword",NativeVoxelDraws.Kind.Weapon,"greatsword-blade");Is("afterimage-weapon-sword",NativeVoxelDraws.Kind.Weapon);Is("packimage-weapon-std-sword",NativeVoxelDraws.Kind.FrameSculpt);
        Is(NativeBowModels.PartId(false,0,0),NativeVoxelDraws.Kind.Weapon);Is(NativeBowModels.PartId(true,1,2),NativeVoxelDraws.Kind.FrameSculpt);Is(NativeBowModels.ArrowId(true,0),NativeVoxelDraws.Kind.FrameSculpt);Is(NativeBowModels.StringProtoId,NativeVoxelDraws.Kind.Weapon);
        Is("hat-cap",NativeVoxelDraws.Kind.Hat);Is("hat-cap+tint",NativeVoxelDraws.Kind.Hat);Is("hat-"+NativeSheldonHead.Hat,NativeVoxelDraws.Kind.Hat);Is("ghost-pilot",NativeVoxelDraws.Kind.PilotBody);Is("hologram-pilot-std",NativeVoxelDraws.Kind.PilotBody);
        for(int k=0;k<NativeHordeKinds.Count;k++)for(int l=0;l<NativeHordeKinds.Looks[k];l++)for(int t=0;t<NativeHordeModels.Tiers;t++){Is(NativeHordeModels.Id(k,l,t),NativeVoxelDraws.Kind.Enemy);Is(NativeHordeModels.JawId(k,l,t),NativeVoxelDraws.Kind.Enemy);}
        foreach(var id in new[]{"horde-0","horde-tongue","summon-toy","summon-zombie","slender","iw-slagback-crust-0-full","entrance-ghost-summon-toy~blend"})Is(id,NativeVoxelDraws.Kind.Enemy);
        Is("kill-cube",NativeVoxelDraws.Kind.KillCube);Is("kill-ghost",NativeVoxelDraws.Kind.KillCube);Is("kill-cap",NativeVoxelDraws.Kind.KillGib);Is("kill-chunk-3",NativeVoxelDraws.Kind.KillGib);
        Is("wound-cube",NativeVoxelDraws.Kind.WoundCube);Is("repair-cube",NativeVoxelDraws.Kind.ReviveCell);Is("native-weapon-piece-0",NativeVoxelDraws.Kind.Piece);Is("siren",NativeVoxelDraws.Kind.Siren);
        Is("map-foundry",NativeVoxelDraws.Kind.Scene);Is("stage-neon-drop",NativeVoxelDraws.Kind.Scene);Is("death-confetti",NativeVoxelDraws.Kind.Scene);Is("ward-motion-0",NativeVoxelDraws.Kind.Scene);Is("scenery-snow",NativeVoxelDraws.Kind.Scene);Is("machine-chip",NativeVoxelDraws.Kind.Scene);Is("pve-pickup",NativeVoxelDraws.Kind.Scene);Is(NativeMachineModels.BodyId("chip"),NativeVoxelDraws.Kind.None);Is(NativeInstanceMachineDress.BackroomsId,NativeVoxelDraws.Kind.None);
        Check(NativeVoxelDraws.Merge(NativeVoxelDraws.Kind.Weapon,NativeVoxelDraws.Kind.FrameSculpt)==NativeVoxelDraws.Kind.FrameSculpt&&NativeVoxelDraws.Merge(NativeVoxelDraws.Kind.None,NativeVoxelDraws.Kind.Hat)==NativeVoxelDraws.Kind.Hat,"an aliased part takes the Standard sculpt's kind");
        Check(NativeVoxelDraws.Labels.Length==NativeVoxelDraws.Kinds&&NativeVoxelDraws.Packages.Length==NativeVoxelDraws.Kinds&&Enum.GetValues(typeof(NativeVoxelDraws.Kind)).Length==NativeVoxelDraws.Kinds,"every kind has its label and package");
        // 2. The tally: off counts nothing; on, the last whole frame; the unexpected leave the allowed and the informational out.
        bool was=NativeVoxelDraws.On;
        try
        {
            NativeVoxelDraws.On=false;NativeVoxelDraws.Reset();NativeVoxelDraws.Add(NativeVoxelDraws.Kind.Weapon,3,1);NativeVoxelDraws.Frame(2);Check(NativeVoxelDraws.Last(NativeVoxelDraws.Kind.Weapon)==0,"off, nothing counts");
            NativeVoxelDraws.On=true;NativeVoxelDraws.Reset();
            NativeVoxelDraws.Add(NativeVoxelDraws.Kind.Weapon,3,10);NativeVoxelDraws.Add(NativeVoxelDraws.Kind.Weapon,2,10);NativeVoxelDraws.Add(NativeVoxelDraws.Kind.Piece,40,10);NativeVoxelDraws.Add(NativeVoxelDraws.Kind.FrameSculpt,2,10);NativeVoxelDraws.Add(NativeVoxelDraws.Kind.None,9,10);
            Check(NativeVoxelDraws.Last(NativeVoxelDraws.Kind.Weapon)==0,"a frame's counts show once it is whole");
            NativeVoxelDraws.Frame(11);Check(NativeVoxelDraws.Last(NativeVoxelDraws.Kind.Weapon)==5&&NativeVoxelDraws.Last(NativeVoxelDraws.Kind.Piece)==40&&NativeVoxelDraws.Unexpected==5,"the last whole frame: 5 voxel weapons unexpected, the pieces allowed, the sculpts information");
            NativeVoxelDraws.Frame(11);Check(NativeVoxelDraws.Unexpected==5,"a frame starts once");
            NativeVoxelDraws.Frame(12);Check(NativeVoxelDraws.Unexpected==0&&NativeVoxelDraws.Last(NativeVoxelDraws.Kind.Piece)==0,"a frame with no draws reads 0");
            var line=NativeVoxelDraws.Summary();Check(line.StartsWith("voxel draws: unexpected 0 | pilot bodies 0,")&&line.Contains("primitive pieces 0 (allowed)")&&line.Contains("frame sculpts 0 (info)"),"the capture log's line");
            Check(NativeVoxelDraws.Allowed(NativeVoxelDraws.Kind.Siren)&&NativeVoxelDraws.Allowed(NativeVoxelDraws.Kind.Piece)&&!NativeVoxelDraws.Allowed(NativeVoxelDraws.Kind.Weapon)&&!NativeVoxelDraws.Allowed(NativeVoxelDraws.Kind.PilotBody),"allowed: the Siren and the primitive pieces only");
        }
        finally{NativeVoxelDraws.On=was;NativeVoxelDraws.Reset();}
        // 3. The hooks: the bank counts every flushed list, the three draws outside it count at their calls, all behind On; the panel
        //    over every screen; the DEV toggle and the switch (the Editor and Development builds only).
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f)).Replace("\r\n","\n");
        var bank=R("Runtime/NativeSourceModels.cs");int flush=bank.IndexOf("private void FlushAll<K>",StringComparison.Ordinal);
        Check(flush>0&&bank.IndexOf("if(NativeVoxelDraws.On)foreach(var d in lists.Values)VoxelCount(d.Part,d.Matrices.Count);",flush,StringComparison.Ordinal)>flush,"the bank counts every list it flushes, only while the counter is on");
        Check(R("Runtime/NativeHordeBreachPresentation.cs").Contains("if(NativeVoxelDraws.On)NativeVoxelDraws.Add(NativeVoxelDraws.Kind.BreachCube,1,Time.frameCount);")&&R("Runtime/NativeGorePresentation.cs").Contains("if(NativeVoxelDraws.On)NativeVoxelDraws.Add(NativeVoxelDraws.Kind.GoreCube,count,Time.frameCount);")
            &&R("Runtime/NativeStatusPresentation.cs").Contains("if(NativeVoxelDraws.On&&(list==statusWorld||list==statusView))"),"the breach, gore and status cubes count at their own draws");
        // Off costs nothing: every count call in Core and Runtime (but the counter's own two files, where Add and VoxelCount are made)
        // sits behind a test of On on its own line, so a frame with the counter off pays one static read per draw site and no call.
        int sites=0;
        foreach(var dir in new[]{"Core","Runtime"})foreach(var f in Directory.GetFiles(Path.Combine(root,"Assets/ChipArena",dir),"*.cs",SearchOption.AllDirectories))
        {
            string name=Path.GetFileName(f);if(name=="NativeVoxelDraws.cs"||name=="NativeVoxelDrawCount.cs")continue;
            foreach(var l in File.ReadAllLines(f))foreach(var call in new[]{"NativeVoxelDraws.Add(","VoxelCount("})
                for(int at=l.IndexOf(call,StringComparison.Ordinal);at>=0;at=l.IndexOf(call,at+call.Length,StringComparison.Ordinal))
                {sites++;Check(l.LastIndexOf("if(NativeVoxelDraws.On",at,StringComparison.Ordinal)>=0,dir+"/"+name+" counts voxel draws without testing NativeVoxelDraws.On first: "+l.Trim());}
        }
        Check(sites==4,"the bank's flush and the breach, gore and status draws are the counter's only call sites ("+sites+"; a new one belongs in this check)");
        var count=R("Runtime/NativeVoxelDrawCount.cs");Check(count.Contains("if(voxelKindsFrame!=Time.frameCount){voxelKindsFrame=Time.frameCount;VoxelKindsFill();}")&&count.Contains("NativeVoxelDraws.Merge(was,k)"),"the part map fills at most once a frame, an alias merged");
        Check(R("Runtime/ConversionGame.cs").Contains("ControllerCursor();VoxelDrawsGui();}finally")&&R("Runtime/NativeVoxelOverlay.cs").Contains("Debug.isDebugBuild&&Array.IndexOf(Environment.GetCommandLineArgs(),\"--ca-voxel-counter\")>=0"),"the panel draws over every screen; --ca-voxel-counter only in the Editor and Development builds");
        Check(NativeDevActions.Find("overlay-voxel")!=null&&R("Runtime/NativeDevPanel.cs").Contains("case \"overlay-voxel\":VoxelCounterToggle();break;")&&R("Runtime/NativeDevPanel.cs").Contains("DevToggleAct(new Rect(x,y,w,32),\"overlay-voxel\",NativeVoxelDraws.Shown);"),"the DEV panel's Camera tab toggles it (a read-only view tool)");
        Console.WriteLine("PASS voxel draw counter: the bank's ids sort into "+(NativeVoxelDraws.Kinds-1)+" kinds, the last whole frame tallied, its hooks behind On; "+checks+" checks");
    }
}
