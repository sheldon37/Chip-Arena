using System;
using System.Text;

namespace ChipArena.UnityConversion
{
    // The model's outfit (owner 2026-10-26: "integrate switching the clothes on the model"; docs/CHARACTER-LOWPOLY.md section i,
    // the client half of the proposed look contract). One compact, normalized map: "slot:id" pairs in Slots order, only the
    // non-default picks ("upper:hard-jacket,feet:work-boots"), unknown slots and ids dropped, "" for the default outfit (spiky
    // hair and the robot's pieces: Core/NativeRobotSuit; owner 2026-10-30: "I think we will make the default outfit the new
    // designed armor"; owner 2026-11-01: "Can we make the armor portion into clothing? Like separate them like we have other
    // clothes currently."). Each other id names a piece of the local visual-body bake's wardrobe; the bake's look data maps it
    // onto its meshes (Runtime/NativeVisualBodyPresentation.cs VisualDress), and a robot piece dresses as the fitted garment it
    // is worn over (NativeRobotSuit.Dress). It is kept in prefs.look; carrying it to other clients and the account is Astra's
    // part of the contract, with the DEV gate (Gate: Sheldon's clothes and bare feet, NativeSheldonWear, need live DEV access,
    // as the Sheldon Clothes body skin does). Presentation only: no rule, hit volume, timing or network authority reads it.
    // The skin tone (owner 2026-10-28: "Give skin color customization, along with a Color selector circle") rides the same map
    // as its last entry, "skin:rrggbb" (six lowercase hex digits, no '#'); absent, the model keeps its own skin. It is not
    // DEV gated. A map that differs only in it is the same outfit (SameOutfit): a body retints in place instead of being made
    // again (Runtime/NativeVisualBodyPresentation.cs VisualRetone).
    public static class NativeLook
    {
        public const string Hair="hair",Upper="upper",Lower="lower",Hands="hands",Feet="feet";
        /// <summary>The slots in canonical order, and their Locker labels.</summary>
        public static readonly string[] Slots={Hair,Upper,Lower,Hands,Feet};
        public static readonly string[] SlotLabels={"Hair","Top","Bottoms","Hands","Shoes"};
        /// <summary>Each slot's ids in Slots order (the first is the default: below the hair, the robot's piece) and their labels.</summary>
        public static readonly string[][] Ids={
            new[]{"spiky","wild","side-swept","tied-back","fringe-tail"},
            new[]{"robot-armour","open-hoodie","hard-jacket","hoodie","tee","tank","sweater",NativeSheldonWear.Robe},
            new[]{"robot-legs","belted-pants","baggy-pants","skinny-jeans","cuffed-shorts","khakis",NativeSheldonWear.Pants},
            new[]{"robot-gauntlets","fingerless","gloves","arm-bands","sweat-bands","bare"},
            new[]{"robot-boots","mid-tops","work-boots","sneakers","lace-ups",NativeSheldonWear.Boots,NativeSheldonWear.Barefoot}};
        public static readonly string[][] Labels={
            new[]{"Spiky","Wild locks","Side swept","Tied back","Fringe and tail"},
            new[]{"Robot armour","Open hoodie","Hard jacket","Hoodie","T-shirt","Tank top","Sweater","Sheldon robe"},
            new[]{"Robot legs","Belted pants","Baggy pants","Skinny jeans","Cuffed shorts","Khakis","Sheldon pants"},
            new[]{"Robot gauntlets","Fingerless gloves","Gloves","Arm bands","Sweat bands","Bare hands"},
            new[]{"Robot boots","Mid-tops","Work boots","Sneakers","Lace-ups","Sheldon boots","Barefoot"}};
        /// <summary>The concept's outfit (the open hoodie, belted pants, fingerless gloves and mid-tops; the bake's rest pieces),
        /// for the bodies that keep their own clothes (the menu rooftop's crowd).</summary>
        public const string Concept="upper:open-hoodie,lower:belted-pants,hands:fingerless,feet:mid-tops";
        /// <summary>The skin tone's entry, after the slots.</summary>
        public const string Skin="skin";
        /// <summary>The Locker's skin tone presets, light to deep (0xRRGGBB), and their names.</summary>
        public static readonly int[] SkinTones={0xf6dfcf,0xf0cdb4,0xe6b796,0xd8a07a,0xc48a62,0xac7250,0x91593c,0x774530,0x5c3523,0x432619};
        public static readonly string[] SkinToneLabels={"Porcelain","Fair","Light","Warm beige","Honey","Tan","Bronze","Chestnut","Umber","Espresso"};
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
        /// <summary>Whether a piece is DEV only (Sheldon's clothes and bare feet; NativeSheldonWear).</summary>
        public static bool DevOnly(string id)=>NativeSheldonWear.DevOnly(id);
        /// <summary>The map (normalized) with every DEV piece back to its slot's default unless `allowed` (live DEV access): the
        /// Locker's picks, the pilot's own bodies and, when the look travels, the host's seat. The same string when nothing drops.</summary>
        public static string Gate(string map,bool allowed)
        {
            map=Normalize(map);if(allowed||map.Length==0)return map;
            var picks=new int[Slots.Length];bool dropped=false;
            for(int s=0;s<Slots.Length;s++){int k=Index(map,Slots[s]);if(DevOnly(Ids[s][k])){k=0;dropped=true;}picks[s]=k;}
            return dropped?Join(picks,SkinOf(map)):map;
        }

        /// <summary>The canonical form of a map (see the class comment); never longer than MaxLength.</summary>
        public static string Normalize(string map)
        {
            if(string.IsNullOrEmpty(map))return "";
            if(map.Length>4096)map=map.Substring(0,4096);
            var picks=new int[Slots.Length];int skin=-1;
            foreach(var entry in map.Split(','))
            {
                int colon=entry.IndexOf(':');if(colon<=0)continue;string key=entry.Substring(0,colon).Trim(),value=entry.Substring(colon+1).Trim();
                if(key==Skin){int rgb=ParseHex(value,0,value.Length);if(rgb>=0)skin=rgb;continue;}
                int s=SlotIndex(key);if(s<0)continue;
                int k=Array.IndexOf(Ids[s],value);if(k<0)continue;picks[s]=k;
            }
            return Join(picks,skin);
        }
        /// <summary>The map with `slot` set to `id` (normalized; an unknown slot or id leaves the map as it was, normalized).</summary>
        public static string With(string map,string slot,string id)
        {
            int s=SlotIndex(slot),k=s<0?-1:Array.IndexOf(Ids[s],id);if(k<0)return Normalize(map);
            var picks=new int[Slots.Length];for(int i=0;i<Slots.Length;i++)picks[i]=Index(map,Slots[i]);picks[s]=k;return Join(picks,SkinOf(map));
        }

        // ---- the skin tone ----
        /// <summary>The map's skin tone (0xRRGGBB), or -1 when it has none (the model's own skin). Allocation free; the last
        /// valid entry wins.</summary>
        public static int SkinOf(string map)
        {
            if(string.IsNullOrEmpty(map))return -1;
            int i=0,n=map.Length,result=-1;
            while(i<n)
            {
                int end=map.IndexOf(',',i);if(end<0)end=n;int colon=map.IndexOf(':',i,end-i);
                if(colon>i&&colon-i==Skin.Length&&string.CompareOrdinal(map,i,Skin,0,Skin.Length)==0){int rgb=ParseHex(map,colon+1,end-colon-1);if(rgb>=0)result=rgb;}
                i=end+1;
            }
            return result;
        }
        /// <summary>The map (normalized) with the skin tone `rgb` (0xRRGGBB), or without one for a negative `rgb`.</summary>
        public static string WithSkin(string map,int rgb)
        {
            var picks=new int[Slots.Length];for(int i=0;i<Slots.Length;i++)picks[i]=Index(map,Slots[i]);return Join(picks,rgb<0?-1:rgb&0xffffff);
        }
        /// <summary>Whether two maps dress the same pieces (they may differ in the skin tone only). Allocation free.</summary>
        public static bool SameOutfit(string a,string b)
        {
            if(string.Equals(a??"",b??"",StringComparison.Ordinal))return true;
            for(int s=0;s<Slots.Length;s++)if(Index(a,Slots[s])!=Index(b,Slots[s]))return false;
            return true;
        }
        /// <summary>An account's map `saved` with this PC's skin tone when it carries none. Until the account Worker keeps the
        /// skin entry (Astra's part: src/relay/accounts.ts drops keys it does not know) a signed-in pilot would otherwise lose
        /// the tone on every load. Once it does, a saved map without one means DEFAULT and this can go.</summary>
        public static string KeepSkin(string saved,string local)
        {
            if(SkinOf(saved)>=0)return saved??"";int tone=SkinOf(local);return tone<0?saved??"":WithSkin(saved,tone);
        }
        /// <summary>The map's skin tone as "#rrggbb", or "" without one (the Locker's hex label).</summary>
        public static string SkinHex(int rgb)=>rgb<0?"":"#"+(rgb&0xffffff).ToString("x6");
        // Six hex digits at map[start..start+length) as 0xRRGGBB, or -1.
        private static int ParseHex(string map,int start,int length)
        {
            if(length!=6||start<0||start+6>map.Length)return -1;int v=0;
            for(int k=0;k<6;k++)
            {
                char c=map[start+k];int d=c>='0'&&c<='9'?c-'0':c>='a'&&c<='f'?c-'a'+10:c>='A'&&c<='F'?c-'A'+10:-1;if(d<0)return -1;v=v*16+d;
            }
            return v;
        }
        private static string Join(int[] picks,int skin)
        {
            var sb=new StringBuilder();
            for(int s=0;s<Slots.Length;s++)if(picks[s]!=0){if(sb.Length>0)sb.Append(',');sb.Append(Slots[s]).Append(':').Append(Ids[s][picks[s]]);}
            if(skin>=0){if(sb.Length>0)sb.Append(',');sb.Append(Skin).Append(':').Append((skin&0xffffff).ToString("x6"));}
            return sb.ToString();
        }
    }
}
