using System;
using System.Text;

namespace ChipArena.UnityConversion
{
    // The model's outfit (owner 2026-10-26: "integrate switching the clothes on the model"; docs/CHARACTER-LOWPOLY.md section i,
    // the client half of the proposed look contract). One compact, normalized map: "slot:id" pairs in Slots order, only the
    // non-default picks ("upper:hard-jacket,feet:work-boots"), unknown slots and ids dropped, "" for the default outfit (the
    // concept's: spiky hair, the open hoodie, belted pants, fingerless gloves and mid-top sneakers). Each id names a piece of
    // the local visual-body bake's wardrobe; the bake's look data maps it onto its meshes (Runtime/NativeVisualBodyPresentation.cs
    // VisualDress). It is kept in prefs.look; carrying it to other clients and the account (and any gate) is Astra's part of
    // the contract. Presentation only: no rule, hit volume, timing or network authority reads it.
    public static class NativeLook
    {
        public const string Hair="hair",Upper="upper",Lower="lower",Hands="hands",Feet="feet";
        /// <summary>The slots in canonical order, and their Locker labels.</summary>
        public static readonly string[] Slots={Hair,Upper,Lower,Hands,Feet};
        public static readonly string[] SlotLabels={"Hair","Top","Bottoms","Hands","Shoes"};
        /// <summary>Each slot's ids in Slots order (the first is the default) and their labels.</summary>
        public static readonly string[][] Ids={
            new[]{"spiky","wild","side-swept","tied-back","fringe-tail"},
            new[]{"open-hoodie","hard-jacket","hoodie","tee","tank","sweater"},
            new[]{"belted-pants","baggy-pants","skinny-jeans","cuffed-shorts","khakis"},
            new[]{"fingerless","gloves","arm-bands","sweat-bands","bare"},
            new[]{"mid-tops","work-boots","sneakers","lace-ups"}};
        public static readonly string[][] Labels={
            new[]{"Spiky","Wild locks","Side swept","Tied back","Fringe and tail"},
            new[]{"Open hoodie","Hard jacket","Hoodie","T-shirt","Tank top","Sweater"},
            new[]{"Belted pants","Baggy pants","Skinny jeans","Cuffed shorts","Khakis"},
            new[]{"Fingerless gloves","Gloves","Arm bands","Sweat bands","Bare hands"},
            new[]{"Mid-tops","Work boots","Sneakers","Lace-ups"}};
        /// <summary>The contract's limit; the longest normalized map is far under it.</summary>
        public const int MaxLength=512;
        public static int SlotIndex(string slot)=>Array.IndexOf(Slots,slot);

        /// <summary>The pick for `slot` in a map: its index in that slot's Ids (0, the default, when absent, unknown or
        /// malformed; the last entry wins). Allocation free.</summary>
        public static int Index(string map,string slot)
        {
            int s=SlotIndex(slot);if(s<0||string.IsNullOrEmpty(map))return 0;
            int i=0,n=map.Length,result=0;var ids=Ids[s];
            while(i<n)
            {
                int end=map.IndexOf(',',i);if(end<0)end=n;int colon=map.IndexOf(':',i,end-i);
                if(colon>i&&colon-i==slot.Length&&string.CompareOrdinal(map,i,slot,0,slot.Length)==0)
                {
                    int len=end-colon-1;
                    for(int k=0;k<ids.Length;k++)if(ids[k].Length==len&&string.CompareOrdinal(map,colon+1,ids[k],0,len)==0){result=k;break;}
                }
                i=end+1;
            }
            return result;
        }
        public static string Of(string map,string slot){int s=SlotIndex(slot);return s<0?null:Ids[s][Index(map,slot)];}

        /// <summary>The canonical form of a map (see the class comment); never longer than MaxLength.</summary>
        public static string Normalize(string map)
        {
            if(string.IsNullOrEmpty(map))return "";
            if(map.Length>4096)map=map.Substring(0,4096);
            var picks=new int[Slots.Length];
            foreach(var entry in map.Split(','))
            {
                int colon=entry.IndexOf(':');if(colon<=0)continue;int s=SlotIndex(entry.Substring(0,colon).Trim());if(s<0)continue;
                int k=Array.IndexOf(Ids[s],entry.Substring(colon+1).Trim());if(k<0)continue;picks[s]=k;
            }
            return Join(picks);
        }
        /// <summary>The map with `slot` set to `id` (normalized; an unknown slot or id leaves the map as it was, normalized).</summary>
        public static string With(string map,string slot,string id)
        {
            int s=SlotIndex(slot),k=s<0?-1:Array.IndexOf(Ids[s],id);if(k<0)return Normalize(map);
            var picks=new int[Slots.Length];for(int i=0;i<Slots.Length;i++)picks[i]=Index(map,Slots[i]);picks[s]=k;return Join(picks);
        }
        private static string Join(int[] picks)
        {
            var sb=new StringBuilder();
            for(int s=0;s<Slots.Length;s++)if(picks[s]!=0){if(sb.Length>0)sb.Append(',');sb.Append(Slots[s]).Append(':').Append(Ids[s][picks[s]]);}
            return sb.ToString();
        }
    }
}
