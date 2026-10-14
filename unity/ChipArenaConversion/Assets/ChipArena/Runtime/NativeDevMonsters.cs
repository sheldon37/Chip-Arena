using System;
using UnityEngine;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using H=ChipArena.UnityConversion.NativeHudChrome;
using U=ChipArena.UnityConversion.NativeUi;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// DEV monsters, the presentation half (owner 2026-10-14: "Allow options to spawn monsters in the dev menu"). The authority
    /// half is Astra's, from the proposed contract in docs/DEV-TOOLS.md "Monsters": spawn-monster [kind, count, place,
    /// behaviour] and clear-monsters, offered when the host advertises devProtocol 2, and a replicated NativeMinionFrame.devMonster.
    ///
    /// THE ADAPTER (bind here). Everything this file and the panel show about DEV monsters goes through this class: the DEV
    /// MONSTER tag, the summon-in rift and the panel's live count. Until Astra's flag exists nothing is a DEV monster.
    /// </summary>
    public static class NativeDevMonsterAdapter
    {
        // ---- BIND HERE: one line, once NativeMinion carries the replicated devMonster flag -----------------------------------
        // Replace the body with:  m!=null&&m.DevMonster   (or the field name Astra gives it).
        public static bool DevMonsterOf(NativeMinion m)=>false;
        // ------------------------------------------------------------------------------------------------------------------------
        /// <summary>An entry spawn (vent, door, drop, breach, cell) keeps its own entrance animation instead of the rift.</summary>
        public static bool Entered(NativeMinion m)=>m?.Pve?.Entry!=null;
        /// <summary>Seconds since the monster spawned, where the authority says (a PvE add's SpawnAt, replicated); 0 if unknown.</summary>
        public static float SpawnAge(NativeMinion m,float now)=>m?.Pve!=null?now-m.Pve.SpawnAt:0;
    }

    public sealed partial class ConversionGame
    {
        private string devMonsterKind;private int devMonsterCount=3,devMonsterPlace,devMonsterBehaviour,devEntryFilter;
        private string[] devMonsterKinds,devMonsterNames,devMonsterInfo,devMonsterHp,devMonsterCaps;private string devMonsterRange,devMonsterCapNote;
        private static readonly string[] DevEntryFilters={"all","vent","door","drop","breach","cell"},DevEntryFilterNames={"ALL","VENT","DOOR","DROP","BREACH","CELL"};
        private static readonly string[] DevMonsterActs={"spawn-monster","clear-monsters"};
        private const string DevMonstersProposed="Proposed commands (docs/DEV-TOOLS.md \"Monsters\"): live once the match's host offers them (devProtocol 2).";
        private const string DevMonstersProposedSandbox="Proposed commands (docs/DEV-TOOLS.md \"Monsters\"): live once the sandbox's DEV rules carry them (the same command a host runs).";
        private int DevMonsterCount(){int n=0;if(arena==null)return 0;foreach(var m in arena.Minions)if(m!=null&&m.Hp>0&&NativeDevMonsterAdapter.DevMonsterOf(m))n++;return n;}
        // The kinds from the content (PveContent adds), with their names and a one-line HP / speed, built once.
        private void DevMonsterLists()
        {
            if(devMonsterKinds!=null)return;var adds=NativeArena.StageContent.adds;devMonsterKinds=NativeDevMonsters.Kinds(adds);
            devMonsterNames=new string[devMonsterKinds.Length];devMonsterInfo=new string[devMonsterKinds.Length];devMonsterHp=new string[devMonsterKinds.Length];devMonsterCaps=new string[devMonsterKinds.Length];
            devMonsterRange=NativeDevText.Int(NativeDevMonsters.CountMin)+"-"+NativeDevText.Int(NativeDevMonsters.CountMax)+" a spawn";
            devMonsterCapNote="The host caps DEV monsters at "+NativeDevText.Int(NativeDevMonsters.RoomCap)+" a room, "+NativeDevText.Int(NativeDevMonsters.HulkCap)+" hulks and "+NativeDevText.Int(NativeDevMonsters.SpitterCap)+" spitters; its reply says how many spawned.";
            for(int i=0;i<devMonsterKinds.Length;i++)
            {
                PveAddStats st=null;foreach(var a in adds)if(a!=null&&a.id==devMonsterKinds[i]){st=a;break;}
                int cap=NativeDevMonsters.KindCap(devMonsterKinds[i]);devMonsterCaps[i]=cap<NativeDevMonsters.RoomCap?"MAX "+NativeDevText.Int(cap):null;
                devMonsterNames[i]=devMonsterKinds[i].ToUpperInvariant();devMonsterHp[i]=st==null?"":"HP "+NativeDevText.Int(Mathf.RoundToInt(st.hp));devMonsterInfo[i]=st==null?"":devMonsterHp[i]+" · "+NativeDevText.Value(st.run,1," m/s");
            }
            if(devMonsterKind==null&&devMonsterKinds.Length>0)devMonsterKind=devMonsterKinds[0];
        }
        private float DevMonstersTab(float x,float y,float w)
        {
            DevMonsterLists();var spawn=NativeDevActions.Resolve(NativeDevActions.Find(NativeDevMonsters.Spawn),devCtx,out string why);
            if(spawn==NativeDevState.NeedsHost)y=DevNote(x,y,w,devMode==NativeDevMode.Sandbox?DevMonstersProposedSandbox:DevMonstersProposed,C.Warn());
            else y=DevNote(x,y,w,"Match-only: DEV monsters give no XP, drops or credit, and clear with the match.",C.Muted());
            // KIND: a card per content add, with its HP and speed; hulks and spitters show their caps.
            y=DevHead(x,y,w,"KIND");
            {
                int columns=Mathf.Max(2,Mathf.Min(3,Mathf.FloorToInt((w+8)/150)));float gap=8,cw=(w-gap*(columns-1))/columns,ch=54;
                for(int i=0;i<devMonsterKinds.Length;i++)
                {
                    int col=i%columns;var r=new Rect(x+col*(cw+gap),y,cw,ch);bool on=devMonsterKinds[i]==devMonsterKind;int id=9000+devWid++;float hv=devBodyHot&&DevVisible(r)?U.HoverAmount(id,r):0;
                    if(hv>.5f){devHoverLabel=devMonsterNames[i];devHoverReason=devMonsterInfo[i];}
                    if(Event.current.type==EventType.Repaint&&DevVisible(r))
                    {
                        H.Cut(r,on?C.Magenta(.2f):U.Mix(C.Indigo(.7f),C.Indigo(),hv),6,H.Corners.Diagonal);H.Outline(r,on?C.Magenta(.85f):C.Edge(.6f+.4f*hv),6);
                        NativeIcons.Draw(new Rect(r.x+10,r.y+(ch-22)*.5f,22,22),NativeDevMonsters.Icon(devMonsterKinds[i]),on?C.Magenta():C.CyanSoft());
                        U.Text(new Rect(r.x+40,r.y+8,r.width-46,18),devMonsterNames[i],12,C.Ink(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.06f);
                        // HP and speed; HP alone where the card is too narrow for both.
                        string info=U.Measure(devMonsterInfo[i],10,false,true).x<=r.width-46?devMonsterInfo[i]:devMonsterHp[i];
                        U.Text(new Rect(r.x+40,r.y+27,r.width-46,16),info,10,C.Muted(),TextAnchor.MiddleLeft,mono:true);
                        if(devMonsterCaps[i]!=null)U.Text(new Rect(r.xMax-60,r.y+6,52,14),devMonsterCaps[i],8,C.Warn(),TextAnchor.MiddleRight,bold:true,mono:true);
                    }
                    if(DevClick(r))devMonsterKind=devMonsterKinds[i];
                    if(col==columns-1||i==devMonsterKinds.Length-1)y+=ch+gap;
                }
            }
            // COUNT: a stepper, 1-8 (the host clamps to its caps and says how many spawned).
            y=DevHead(x,y,w,"COUNT");
            {
                var minus=new Rect(x,y,40,32);var plus=new Rect(x+120,y,40,32);
                if(DevButton(minus,"−",NativeDevState.Live,"One fewer"))devMonsterCount=NativeDevMonsters.ClampCount(devMonsterCount-1);
                if(Event.current.type==EventType.Repaint&&DevVisible(new Rect(x,y,w,32))){var v=new Rect(x+46,y,68,32);H.Cut(v,C.Void(.8f),4,H.Corners.All);H.Outline(v,C.Magenta(.6f),4,H.Corners.All);U.Text(v,NativeDevText.Int(devMonsterCount),16,C.Ink(),TextAnchor.MiddleCenter,bold:true,mono:true);}
                if(DevButton(plus,"+",NativeDevState.Live,"One more"))devMonsterCount=NativeDevMonsters.ClampCount(devMonsterCount+1);
                if(Event.current.type==EventType.Repaint&&DevVisible(new Rect(x,y,w,32)))U.Text(new Rect(x+172,y,w-172,32),devMonsterRange,10,C.Muted(),TextAnchor.MiddleLeft,mono:true);
                y+=40;y=DevNote(x,y,w,devMonsterCapNote,C.Muted());
            }
            y=DevHead(x,y,w,"PLACE");devMonsterPlace=DevChoice(x,ref y,w,NativeDevMonsters.PlaceNames,devMonsterPlace,NativeDevState.Live,"Where they appear",4,"Place");
            y=DevNote(x,y,w,NativeDevMonsters.PlaceHelp[Mathf.Clamp(devMonsterPlace,0,3)],C.Muted());
            y=DevHead(x,y,w,"BEHAVIOUR");devMonsterBehaviour=DevChoice(x,ref y,w,NativeDevMonsters.BehaviourNames,devMonsterBehaviour,NativeDevState.Live,"What they do",3,"Behaviour");
            y=DevNote(x,y,w,NativeDevMonsters.BehaviourHelp[Mathf.Clamp(devMonsterBehaviour,0,2)],C.Muted());
            y=DevHead(x,y,w,"TARGET (WHO THEY HUNT, WHERE NEAR AND DROP PLACE THEM)");y=DevTargetPicker(x,y,w);
            y=DevActs(x,y,w,2,DevMonsterActs);
            y=DevStat(x,y,w,"DEV MONSTERS",NativeDevText.Count(DevMonsterCount(),NativeDevMonsters.RoomCap));
            // Ward Zero's authored entries, in the offline sandbox only (arena.SandboxWardEntry, NativeSandbox.cs DevRun "ward-spawn").
            if(devMode!=NativeDevMode.Sandbox)return y;
            y=DevHead(x,y,w,"WARD ZERO ENTRIES (SANDBOX)");
            var entries=NativeArena.StageContent.entries;
            if(arena.Map.look!="quarantine"||entries.Length==0)y=DevNote(x,y,w,"Ward Zero's vents, doors, drops and breaches spawn here in the Developer Sandbox (Arena · Load map · Ward Zero).",C.Muted());
            else
            {
                var st=NativeDevActions.Resolve(NativeDevActions.Find("ward-spawn"),devCtx,out string sw);
                int f=DevChoice(x,ref y,w,DevEntryFilterNames,devEntryFilter,NativeDevState.Live,"Which entrances the picker walks",6,"Entry kind");if(f!=devEntryFilter){devEntryFilter=f;wardSandboxEntry=DevEntryStep(entries,wardSandboxEntry-1,1);}
                wardSandboxEntry=Mathf.Clamp(wardSandboxEntry,0,entries.Length-1);if(!DevEntryShown(entries[wardSandboxEntry]))wardSandboxEntry=DevEntryStep(entries,wardSandboxEntry,1);
                var e=entries[wardSandboxEntry];float bw=40;
                if(DevButton(new Rect(x,y,bw,40),"‹",NativeDevState.Live,"Previous entry"))wardSandboxEntry=DevEntryStep(entries,wardSandboxEntry,-1);
                if(Event.current.type==EventType.Repaint&&DevVisible(new Rect(x,y,w,40)))
                {
                    U.Text(new Rect(x+bw+10,y+2,w*.55f,18),e.id,12,C.Ink(),TextAnchor.MiddleLeft,bold:true,mono:true);
                    U.Text(new Rect(x+bw+10,y+21,w*.55f,16),DevEntryLine(e),9,C.Muted(),TextAnchor.MiddleLeft,bold:true,mono:true,tracking:.08f);
                }
                if(DevButton(new Rect(x+w-bw,y,bw,40),"›",NativeDevState.Live,"Next entry"))wardSandboxEntry=DevEntryStep(entries,wardSandboxEntry,1);y+=48;
                float half=(w-8)/2;
                if(DevButton(new Rect(x,y,half,32),"Nearest to me",NativeDevState.Live,"The entrance nearest your pilot (of this kind)"))wardSandboxEntry=DevNearestEntry(entries);
                DevAct(new Rect(x+half+8,y,half,32),"ward-spawn");y+=40;
            }
            return y;
        }
        private bool DevEntryShown(PveEntry e)=>devEntryFilter<=0||e!=null&&e.kind==DevEntryFilters[devEntryFilter];
        private int DevEntryStep(PveEntry[] entries,int from,int step)
        {
            int n=entries.Length;if(n==0)return 0;for(int k=1;k<=n;k++){int i=((from+step*k)%n+n)%n;if(DevEntryShown(entries[i]))return i;}return Mathf.Clamp(from,0,n-1);
        }
        private int DevNearestEntry(PveEntry[] entries)
        {
            var me=DevLocal();if(me==null)return wardSandboxEntry;int best=wardSandboxEntry;float bestD=float.MaxValue;var at=me.Body.position;
            for(int i=0;i<entries.Length;i++){var e=entries[i];if(e?.from==null||!DevEntryShown(e))continue;var p=e.from.Position();float dx=p.x-at.x,dy=p.y-at.y,dz=p.z-at.z,d=dx*dx+dy*dy+dz*dz;if(d<bestD){bestD=d;best=i;}}return best;
        }
        // "VENT · LEVEL 1 · SPAWNS SHAMBLER": what SandboxWardEntry spawns at it (a breach or a hulk entry: a hulk; a drop: a brute).
        private static readonly System.Collections.Generic.Dictionary<int,string> devEntryLines=new System.Collections.Generic.Dictionary<int,string>();
        private static string DevEntryLine(PveEntry e)
        {
            if(devEntryLines.TryGetValue(e.e,out var s))return s;
            s=(e.kind??"entry").ToUpperInvariant()+"  ·  LEVEL "+NativeDevText.Int(e.level)+"  ·  SPAWNS "+(e.kind=="breach"||e.hulk?"HULK":e.kind=="drop"?"BRUTE":"SHAMBLER");devEntryLines[e.e]=s;return s;
        }

        // ---- The DEV MONSTER tag over every DEV monster (every client; through the adapter) -----------------------------------
        private void DevMonsterTags()
        {
            if(arena.Minions.Count==0)return;var physics=arena.Root.scene.GetPhysicsScene();var eye=cameraView.transform.position;float now=Time.unscaledTime;
            float tw=U.Measure(NativeDevTags.DevMonster,8,true,true,.12f).x+16;
            foreach(var m in arena.Minions)
            {
                if(m==null||m.Hp<=0||m.Body==null||!NativeDevMonsterAdapter.DevMonsterOf(m))continue;
                var at=m.Body.position+Vector3.up*(m.Collider!=null?m.Collider.height*.5f+.45f:1.3f);float metres=Vector3.Distance(eye,at);if(metres>40||!DevScreen(at,out var head))continue;
                Vector3 delta=at-eye;if(physics.Raycast(eye,delta.normalized,out _,delta.magnitude-.08f,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))continue;
                float a=Mathf.Clamp01((40-metres)/8);var r=new Rect(Mathf.Round(head.x-tw*.5f),Mathf.Round(head.y-18),tw,15);
                float breath=prefs.reducedEffects?.5f:.35f+.25f*Mathf.Sin(now*3.2f+m.Id);U.Glow(r,C.Magenta(breath*a*.5f),6);
                H.Cut(r,C.Navy(.85f*a),NativeHudCyber.TagCut,H.Corners.Diagonal);H.Outline(r,C.Magenta(.8f*a),NativeHudCyber.TagCut);
                U.Text(r,NativeDevTags.DevMonster,8,C.Magenta(a),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.12f,shadow:C.Void(.6f*a));
            }
        }
    }

    public sealed partial class ArenaPresentation
    {
        private readonly NativeDevMonsterSeen devMonstersSeen=new NativeDevMonsterSeen();
        /// <summary>The DEV summon-in rift for each DEV monster the first time it is seen (through NativeDevMonsterAdapter), unless
        /// it came through an entry (its own entrance animation plays) or it is already old (a late join). Presentation only.</summary>
        public void DevMonsterRifts(NativeArena arena,bool reduced)
        {
            if(arena==null)return;float now=arena.Time;
            foreach(var m in arena.Minions)
            {
                if(m==null||m.Hp<=0||m.Body==null)continue;
                if(!devMonstersSeen.Observe(arena.Generation,m.Id,NativeDevMonsterAdapter.DevMonsterOf(m),now))continue;
                if(NativeDevMonsterAdapter.Entered(m)||NativeDevMonsterAdapter.SpawnAge(m,now)>NativeDevMonsterLook.FreshAge||blasts==null)continue;
                float tall=m.Collider!=null?m.Collider.height:1.8f;blasts.DevMonsterRift(m.Body.position-Vector3.up*(tall*.5f),tall*1.5f,reduced,BlastQuality);
            }
        }
    }
}
