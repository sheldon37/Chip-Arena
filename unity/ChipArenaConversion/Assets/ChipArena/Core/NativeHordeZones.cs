using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// One zone horde kind as the presentation draws it (owner 2026-10-16: "we can tweak the instances to have specific zombies /
    /// enemies"): a kind an Expedition instance adds beside the Siren's seven (NativeHordeKinds). Data only, presentation side:
    /// its content id (the network's NativeMinionFrame.addKind), its body (the Siren kind whose gait, tongues and procedural
    /// fallbacks it shares), its looks, its capsule in the world (the authority's, or the zone contract's proposal until the
    /// authority has the row), how many times the rig it is drawn, its weight, its default attack, the zone contract's proposed
    /// stats row (the DEV preview's timing), its glow, and the zombie mocap takes it walks, runs and stands on.
    /// </summary>
    public sealed class HordeZoneKind
    {
        /// <summary>Whether the content ships it (Astra sets it with the kind's PveContent row): until then the authority and the
        /// wire do not know its id (NativeHordeKinds.Known) and only the presentation draws it.</summary>
        public bool Shipped;
        public string Id,Name,Zone;public int Body,Looks=1,Seam,SeamClass=1;public float Radius=NativeHordeKinds.AddRadius,Height=NativeHordeKinds.AddHeight,Scale=1,Mass=1,Tempo=1;
        public NativeHordeKinds.Attack Attack;public PveAddStats Proposed;
        /// <summary>The ZOMBIE PRO takes (Core/NativeZombieClips keys) it walks, runs, stands and wanders on.</summary>
        public string[] Walks,Runs,Idles,Wanders;
    }

    /// <summary>
    /// The zone kinds, appended after the Siren's seven (NativeHordeKinds.Count): kind index NativeHordeKinds.Count + i for zone
    /// kind i (the City Ironworks' five, then the Central Station's four). Each zone keeps its kinds in its own files and adds its list to <see cref="Kinds"/> (the one registration line);
    /// NativeHordeKinds' tables grow by them once (its static constructor), and the shared horde systems ask
    /// <see cref="NativeHordeKinds.Base"/> for the Siren kind whose tables a zone kind borrows. Nothing here is read by the
    /// authority: a content id the rules do not know still draws as the shambler, and the Siren's kinds keep their indices and
    /// tables exactly.
    /// </summary>
    public static class NativeHordeZones
    {
        /// <summary>Every zone kind in index order (the registration line: one entry per zone).</summary>
        public static readonly HordeZoneKind[] Kinds=Join(NativeIronworksKinds.Kinds,NativeStationKinds.Kinds);
        public static int Count=>Kinds.Length;
        /// <summary>The zone kind at a horde kind index (null for the Siren's kinds or an index out of range).</summary>
        public static HordeZoneKind Of(int kind){int i=kind-NativeHordeKinds.Count;return i>=0&&i<Kinds.Length?Kinds[i]:null;}
        /// <summary>The horde kind index of a zone list's first kind (-1 when it is not registered).</summary>
        public static int First(HordeZoneKind[] zone){if(zone!=null&&zone.Length>0)for(int i=0;i<Kinds.Length;i++)if(ReferenceEquals(Kinds[i],zone[0]))return NativeHordeKinds.Count+i;return -1;}
        private static HordeZoneKind[] Join(params HordeZoneKind[][] zones){int n=0;foreach(var z in zones)n+=z.Length;var all=new HordeZoneKind[n];int at=0;foreach(var z in zones){Array.Copy(z,0,all,at,z.Length);at+=z.Length;}return all;}
        /// <summary>A Siren table grown by the zone kinds (the Siren's entries first, unchanged).</summary>
        public static T[] Extend<T>(T[] siren,Func<HordeZoneKind,T> pick)
        {
            var all=new T[siren.Length+Kinds.Length];Array.Copy(siren,all,siren.Length);for(int i=0;i<Kinds.Length;i++)all[siren.Length+i]=pick(Kinds[i]);return all;
        }
        /// <summary>The zombie motion's take sets grown by the zone kinds' own (Core/NativeZombieMotion.Warm): each set the kind
        /// names, else its body's.</summary>
        public static int[][] Takes(int[][] siren,Func<HordeZoneKind,string[]> pick)
        {
            var all=new int[siren.Length+Kinds.Length][];Array.Copy(siren,all,siren.Length);
            for(int i=0;i<Kinds.Length;i++)
            {
                var keys=pick(Kinds[i]);if(keys==null){all[siren.Length+i]=siren[Math.Max(0,Math.Min(siren.Length-1,Kinds[i].Body))];continue;}
                var set=new int[keys.Length];for(int k=0;k<keys.Length;k++)set[k]=NativeZombieClips.Need(keys[k]);all[siren.Length+i]=set;
            }
            return all;
        }
        /// <summary>A kind's motion tempo (Core/NativeZombieMotion.Rate): the Siren's table, or the zone kind's own.</summary>
        public static float Tempo(int kind,float[] siren){var z=Of(kind);return z!=null?z.Tempo:siren[Math.Max(0,Math.Min(siren.Length-1,kind))];}
    }
}
