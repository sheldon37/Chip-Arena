using System;
using System.Text;

namespace ChipArena.UnityConversion
{
    // The pilot's body (Astra's brief 2026-11-24 section 1; owner 2026-11-25: "lets get work started"): today's pilot or the
    // female preset, picked in the Locker (Appearance, BODY: Male / Female), worn with every hairstyle and outfit. It rides the
    // look map (Core/NativeLook.cs) as one optional key, "body:female", the brief's proposed contract: omitted (or "male", never
    // written) the body is today's pilot, so every existing look and account draws as before. The key is written after the five
    // slots and before the costume and the skin tone ("upper:tee,body:female,skin:c48a62").
    // Carrying it through NativeLook's normalisation, the DEV gate, the account Worker and the peers is Astra's part of the
    // contract (docs/DEV_LOG.md 2026-11-25, B1). Until it lands, NativeLook drops the key, so the presentation keeps it at its own
    // edges with Keep: the Locker's writers, the shown look (LookShown) and this client's own pilot's bodies; a peer draws today's
    // pilot. Presentation only: both bodies have the same hit volumes, reach, shot origins and movement; no rule reads it.
    // The female preset is the character pack's authored female reference (BoZo CustomCharacters/Anime/Default_Girl, the pack's
    // own values): its body shapes (Chest 60, Curvy 100; BodyType 0 like today's pilot), its face shape (EyeRoundness 57) and its
    // one bone modifier (the pack's Chest Size: spine_04 scaled .971, the head counter-scaled so it keeps its size), set on a body
    // when it is dressed (Runtime/NativeVisualBodyPresentation.cs VisualFemale). Today's pilot is the bake's rest body: every
    // shape 0, no bone modifiers. The pack's stance (.95) is its own Animator's idle blend; the game's poses come from its motion
    // bank, which is the same for both bodies.
    public static class NativeBodyKind
    {
        public const string Key="body";
        /// <summary>The bodies: today's pilot (the default: never written) and the female preset.</summary>
        public static readonly string[] Ids={"male","female"};
        public static readonly string[] Labels={"Male","Female"};
        public const int Male=0,Female=1;
        /// <summary>The female preset's body shapes (by name, on every worn mesh that has them, so the clothes follow the body) and
        /// their weights: the pack's Default_Girl.</summary>
        public static readonly string[] BodyShapes={"Chest","Curvy"};
        public static readonly float[] BodyWeights={60,100};
        /// <summary>Its face shapes (the head and what follows the face: lashes, brows) and their weights.</summary>
        public static readonly string[] FaceShapes={"EyeRoundness"};
        public static readonly float[] FaceWeights={57};
        /// <summary>Its bone modifier: the pack's Chest Size (the bone scaled, the head counter-scaled to its own size).</summary>
        public const string ChestBone="spine_04",HeadBone="head";
        public const float ChestScale=.97103566f;
        /// <summary>The pack's stance for it (its Animator's female idle blend; not used by the game's motion).</summary>
        public const float Stance=.95f;
        /// <summary>Its face pieces in the bake's look data (slot FaceSlot, never a wardrobe slot, so no outfit wears them): the pack's
        /// long lashes and thin brows, each worn in place of the head's own of its kind.</summary>
        public const string FaceSlot="face";
        public static readonly string[] FacePieces={"female-lashes","female-brows"};

        /// <summary>The map's body (Male when absent, unknown or malformed; the last valid entry wins). Allocation free.</summary>
        public static int Of(string map)
        {
            if(string.IsNullOrEmpty(map))return Male;
            int i=0,n=map.Length,result=Male;
            while(i<n)
            {
                int end=map.IndexOf(',',i);if(end<0)end=n;int colon=map.IndexOf(':',i,end-i);
                if(colon>i&&colon-i==Key.Length&&string.CompareOrdinal(map,i,Key,0,Key.Length)==0)
                {
                    int len=end-colon-1;
                    for(int k=0;k<Ids.Length;k++)if(Ids[k].Length==len&&string.CompareOrdinal(map,colon+1,Ids[k],0,len)==0){result=k;break;}
                }
                i=end+1;
            }
            return result;
        }
        public static bool IsFemale(string map)=>Of(map)==Female;
        /// <summary>The map with its body set to `body` (Male removes the entry): every other entry kept as it is and in its order,
        /// the body's entry before the costume and the skin tone. The same string when nothing changes.</summary>
        public static string With(string map,int body)
        {
            map??="";if(body<Male||body>=Ids.Length)body=Male;
            if(Of(map)==body&&Count(map)==(body==Male?0:1))return map;
            var sb=new StringBuilder(map.Length+12);bool placed=body==Male;int i=0,n=map.Length;
            while(i<n)
            {
                int end=map.IndexOf(',',i);if(end<0)end=n;int colon=map.IndexOf(':',i,end-i);
                bool mine=colon>i&&colon-i==Key.Length&&string.CompareOrdinal(map,i,Key,0,Key.Length)==0;
                bool after=colon>i&&(colon-i==NativeLook.Costume.Length&&string.CompareOrdinal(map,i,NativeLook.Costume,0,NativeLook.Costume.Length)==0
                    ||colon-i==NativeLook.Skin.Length&&string.CompareOrdinal(map,i,NativeLook.Skin,0,NativeLook.Skin.Length)==0);
                if(!placed&&after){if(sb.Length>0)sb.Append(',');sb.Append(Key).Append(':').Append(Ids[body]);placed=true;}
                if(!mine&&end>i){if(sb.Length>0)sb.Append(',');sb.Append(map,i,end-i);}
                i=end+1;
            }
            if(!placed){if(sb.Length>0)sb.Append(',');sb.Append(Key).Append(':').Append(Ids[body]);}
            return sb.ToString();
        }
        /// <summary>`map` (as a normaliser or another writer returned it) with the body `from` picks: how the presentation keeps the
        /// body until NativeLook carries the key itself (then this changes nothing). The same string when they agree.</summary>
        public static string Keep(string map,string from)=>Of(map)==Of(from)&&Count(map)<=1?map??"":With(map,Of(from));
        // The map's body entries (any value). Allocation free.
        private static int Count(string map)
        {
            if(string.IsNullOrEmpty(map))return 0;int i=0,n=map.Length,count=0;
            while(i<n){int end=map.IndexOf(',',i);if(end<0)end=n;int colon=map.IndexOf(':',i,end-i);if(colon>i&&colon-i==Key.Length&&string.CompareOrdinal(map,i,Key,0,Key.Length)==0)count++;i=end+1;}
            return count;
        }
    }
}
