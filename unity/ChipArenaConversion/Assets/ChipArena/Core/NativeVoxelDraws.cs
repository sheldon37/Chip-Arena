using System;
using System.Text;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The DEV voxel draw counter (owner 2026-11-18: "I want to essentially remove voxel entirely BUT keep its data"; docs/VOXEL-STYLE.md,
    /// docs/VOXEL-RETIREMENT.md V0): how many voxel bodies, parts and props were drawn in the last whole frame, by kind. The source-model
    /// bank counts every instance it flushes by the model id the part was registered under (Of); the breach, gore and status pieces,
    /// which draw outside the bank, count at their own draw calls. It runs only while On (the DEV overlay's "Voxel draws", the
    /// --ca-voxel-counter switch in the Editor and Development builds, or a capture), so play pays nothing for it.
    /// With the licensed bake, no costume and no Prototype picks, every kind reads 0 but the allowed ones (Allowed): the Siren until
    /// she has her own model (V11) and the primitive pieces (chip modules, enchants, muzzle flashes, the techniques' and the
    /// Bombadier's props, the Cyber Lord's data cubes) until V3 and V5 model them. The Standard sculpts drawn in the voxel weapon frame
    /// where no life-size model exists (the Katana, the Fangs, the Glaive, the Top Dog, the Bombadier, the bow's arrows and string)
    /// are shown for information (V10), not counted as voxel. Presentation only: nothing here is read by a rule, the wire or a save.
    /// </summary>
    public static class NativeVoxelDraws
    {
        public enum Kind : byte { None, PilotBody, Weapon, Hat, Enemy, KillCube, KillGib, WoundCube, ReviveCell, BreachCube, GoreCube, StatusMarker, Piece, Siren, Scene, FrameSculpt }
        public const int Kinds=16;
        public static readonly string[] Labels={"","pilot bodies","voxel weapons","voxel hats","voxel enemies","kill cubes","box-sized gibs","wound cubes","revive cells","breach cubes","gore cubes","status markers","primitive pieces","the Siren","voxel scene props","frame sculpts"};
        /// <summary>Where each kind leaves the model path (the plan's package), for the overlay's and the capture log's notes.</summary>
        public static readonly string[] Packages={"","V1, V6","V0 (P9), V2","V3, V2","V7","V6","V6","V6","V6","V13","V6","V3","V3, V5","V11","V5, V14","V10, V12"};
        /// <summary>Kinds that may read above 0 with the bake today: the Siren (V11) and the primitive pieces (V3, V5).</summary>
        public static bool Allowed(Kind k)=>k==Kind.Siren||k==Kind.Piece;
        /// <summary>Kinds shown for information only: Standard sculpts in the voxel weapon frame (not a voxel model).</summary>
        public static bool Informational(Kind k)=>k==Kind.FrameSculpt;
        /// <summary>Counting on (the overlay, the switch or a capture).</summary>
        public static bool On;
        /// <summary>The overlay's panel shown outside the DEV panel (the --ca-voxel-counter switch, a capture).</summary>
        public static bool Shown;
        private static readonly int[] now=new int[Kinds],last=new int[Kinds];private static int frame=int.MinValue;
        /// <summary>Starts frame `f` (the bank's Begin, the overlay): the frame before's counts become Last.</summary>
        public static void Frame(int f){if(f==frame)return;Array.Copy(now,last,Kinds);Array.Clear(now,0,Kinds);frame=f;}
        /// <summary>`n` instances of `k` drawn in frame `f`.</summary>
        public static void Add(Kind k,int n,int f){if(!On||k==Kind.None||n<=0)return;Frame(f);now[(int)k]+=n;}
        /// <summary>The last whole frame's count of `k`.</summary>
        public static int Last(Kind k)=>last[(int)k];
        /// <summary>The last whole frame's voxel draws that are neither allowed nor informational (0 is the target).</summary>
        public static int Unexpected{get{int n=0;for(int k=1;k<Kinds;k++)if(!Allowed((Kind)k)&&!Informational((Kind)k))n+=last[k];return n;}}
        /// <summary>Every count cleared (a capture's start, the switch turned on).</summary>
        public static void Reset(){Array.Clear(now,0,Kinds);Array.Clear(last,0,Kinds);frame=int.MinValue;}
        /// <summary>The last whole frame as one log line: "voxel draws: unexpected 0 | pilot bodies 0, voxel weapons 0, ...".</summary>
        public static string Summary()
        {
            var sb=new StringBuilder("voxel draws: unexpected ").Append(Unexpected).Append(" |");
            for(int k=1;k<Kinds;k++){sb.Append(k==1?" ":", ").Append(Labels[k]).Append(' ').Append(last[k]);if(Allowed((Kind)k))sb.Append(" (allowed)");else if(Informational((Kind)k))sb.Append(" (info)");}
            return sb.ToString();
        }

        // ---- What a bank id is (the bank's id grammar: Runtime/NativeSourceModels*.cs and its partials) ----
        // Ids derived from another model (its ghost, afterimage, ink, hologram or entrance twin) are the model they copy.
        private static readonly string[] Wrappers={"entrance-ghost-","afterimage-","packimage-","hologram-","ghost-","ink-"};
        /// <summary>
        /// The kind of a draw of bank model `id` (its part named `part`): None for everything that is not voxel (the models' hats, gear,
        /// machines, pods, chips, the robot's helmet and suit, effects, blood, the boss and stage models). Voxel: the pilot bodies (the
        /// Prototype `pilot`, the Standard and Sheldon voxel skins, their wound carves and bisect halves), the Prototype Series weapons
        /// (`weapon-`/`loose-` exports, their DEV aliases, the bow's `bow-proto-` parts), the voxel hats (`hat-` but not `+model`),
        /// the voxel enemies (`minion-` kinds, the legacy `horde-` bank, summons, the Slenderman), Voxel breakup's cubes and the box
        /// ghosts, the gibs cut to the voxel boxes, the wound cubes, the revive cells, the primitive pieces, the Siren, the Electron
        /// scene props (`map-`, `stage-`, `death-`, `ward-`); Standard weapon sculpts are FrameSculpt. The bank caches each part's kind,
        /// so this runs once per part, never per frame.
        /// </summary>
        public static Kind Of(string id,string part=null)
        {
            if(string.IsNullOrEmpty(id))return Kind.None;
            // a bisect half ("<key>/<model>/<k>": k 0-1 the body's, 2-3 the hat's) or a wound carve ("wounded-<id>/<basis>")
            int slash=id.IndexOf('/');
            if(slash>=0)
            {
                if(id.StartsWith("wounded-",StringComparison.Ordinal))return Of(id.Substring(slash+1),part)==Kind.None?Kind.None:Kind.PilotBody;
                int lastSlash=id.LastIndexOf('/');string model=lastSlash>slash?id.Substring(slash+1,lastSlash-slash-1):id.Substring(slash+1);
                var body=Of(model,part);if(body==Kind.None)return Kind.None;
                string tail=id.Substring(lastSlash+1);return tail=="2"||tail=="3"?Kind.Hat:body;
            }
            if(id.EndsWith("~blend",StringComparison.Ordinal))return Of(id.Substring(0,id.Length-6),part);
            foreach(var w in Wrappers)if(id.StartsWith(w,StringComparison.Ordinal)&&id.Length>w.Length)return Of(id.Substring(w.Length),part);
            if(id.StartsWith("native-weapon-piece-",StringComparison.Ordinal))return Kind.Piece;
            if(id=="siren")return Kind.Siren;
            // the voxel bodies' bank ids ("pilot", the Prototype; "pilot-std*", "pilot-sheldon*" the skins; their wound carves), not "pilot-gear-"
            if(id=="pilot"||id.StartsWith("pilot-std",StringComparison.Ordinal)||id.StartsWith("pilot-sheldon",StringComparison.Ordinal)||id.StartsWith("wounded-",StringComparison.Ordinal))return Kind.PilotBody;
            if(id.StartsWith("weapon-std-",StringComparison.Ordinal)||id.StartsWith("loose-std-",StringComparison.Ordinal)||id=="katana-saya")return Kind.FrameSculpt;
            if(id.StartsWith("weapon-",StringComparison.Ordinal)||id.StartsWith("loose-",StringComparison.Ordinal))
                // the five one-finish classes' plain ids are their Standard sculpt's parts ("std-<spec>"); a DEV class's is its base's
                return part!=null&&part.StartsWith("std-",StringComparison.Ordinal)?Kind.FrameSculpt:Kind.Weapon;
            if(id.StartsWith("bow-proto-",StringComparison.Ordinal))return Kind.Weapon;
            if(id.StartsWith("bow-",StringComparison.Ordinal))return Kind.FrameSculpt;
            if(id.StartsWith("hat-",StringComparison.Ordinal))return id.IndexOf("+model",StringComparison.Ordinal)>=0?Kind.None:Kind.Hat;
            // (the Toy Soldier's sculpted figure is the model look of the voxel "summon-toy" blocks)
            if(id==NativeToySoldier.Model)return Kind.None;
            if(id.StartsWith("minion-",StringComparison.Ordinal)||id.StartsWith("horde-",StringComparison.Ordinal)||id.StartsWith("summon-",StringComparison.Ordinal)||id=="slender"||id.StartsWith("iw-slagback-crust-",StringComparison.Ordinal))return Kind.Enemy;
            if(id=="kill-cube"||id=="kill-ghost")return Kind.KillCube;
            if(id=="kill-cap"||id.StartsWith("kill-chunk-",StringComparison.Ordinal))return Kind.KillGib;
            if(id=="wound-cube")return Kind.WoundCube;
            if(id=="repair-cube")return Kind.ReviveCell;
            if(id.StartsWith("map-",StringComparison.Ordinal)||id.StartsWith("stage-",StringComparison.Ordinal)||id.StartsWith("death-",StringComparison.Ordinal)||id.StartsWith("ward-",StringComparison.Ordinal)||id.StartsWith("scenery-",StringComparison.Ordinal))return Kind.Scene;
            // the Electron PvE machines and pickup (the modelled ones are "machine-std-", "machine-dress-" and "pve-pickup-std")
            if(id=="pve-pickup"||id.StartsWith("machine-",StringComparison.Ordinal)&&!id.StartsWith("machine-std-",StringComparison.Ordinal)&&!id.StartsWith("machine-dress-",StringComparison.Ordinal))return Kind.Scene;
            return Kind.None;
        }
        /// <summary>Of for a part registered under several ids (an alias: a DEV class's weapon, the one-finish classes' plain ids):
        /// the kinds agree but where one is the Standard sculpt, which wins (it is the same mesh).</summary>
        public static Kind Merge(Kind a,Kind b)=>a==b?a:a==Kind.None?b:b==Kind.None?a:a==Kind.FrameSculpt||b==Kind.FrameSculpt?Kind.FrameSculpt:a<b?a:b;
    }
}
