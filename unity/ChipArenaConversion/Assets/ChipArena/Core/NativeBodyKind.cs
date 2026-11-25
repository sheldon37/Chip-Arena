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
    // The female body is the character pack's own female direction on the bake's one rig. The first preset was its authored
    // Default_Girl (Chest 60, Curvy 100, EyeRoundness 57, Chest Size .971); owner 2026-11-25: "I think the body needs to look more
    // feminine for the female body; Take your time to analyze this one". Today's pilot is the pack's neutral middle (every shape 0,
    // no bone modifiers), not its male: the pack's male presets push BodyType +26..+67, Weight +15, NeckThickness +19..+100, the
    // clavicles 1.07 and the root 1.03-1.04, its female presets BodyType 0..-15, Weight to -24, Curvy 100-115, Chest 42-60, the root
    // .92-.98 and the upper arm's girth (1, .92, .73). So Default_Girl on the neutral base barely moved the silhouette, and a level
    // (Levels: Subtle, Medium, Strong) now carries the pack's female directions further, on every worn mesh by name (the clothes
    // follow): the body shapes (BodyType and Weight down, Curvy, WaistSize, which narrows the waist, ButtSize and Chest up,
    // NeckThickness down), the face (EyeRoundness, Roundness), and the bones: the chest (Chest Size, the head counter-scaled), the
    // shoulders' span (the upper arms in along the collarbones), the upper arms' girth, the hands and the whole body (root; never
    // under .95). A weight outside 0..100 (the project clamps blend shape weights: legacyClampBlendShapeWeights) is baked into the
    // mesh's vertices by the presentation (Runtime/NativeBodyKindPresentation.cs VisualFemale). Set on a body when it is dressed.
    // The pack's female stance (BMAC_F_Idle against BMAC_M_Idle: legs closer, knees turned in, the chest up) is laid on the lobby,
    // Locker and portrait poses (Stance*); the match idles keep the pack's (owner, docs match-idles), with a light match stance
    // only as the owner's option (MatchStance*, off).
    public static class NativeBodyKind
    {
        public const string Key="body";
        /// <summary>The bodies: today's pilot (the default: never written) and the female preset.</summary>
        public static readonly string[] Ids={"male","female"};
        public static readonly string[] Labels={"Male","Female"};
        public const int Male=0,Female=1;
        /// <summary>A level of the female body: the shapes by name (on every worn mesh that has them: the body's on the body and its
        /// clothes, the face's on the head, lashes and brows) and their weights, which may pass 0..100; the chest bone's scale (the head
        /// counter-scaled), the shoulders' span (each upper arm along its collarbone), the upper arms' girth (their shoulder twist bones'
        /// cross-section: along the bone's y and z), the hands' scale, the whole body's (root) and the chin's width.</summary>
        public sealed class Level
        {
            public string Name;public string[] Shapes;public float[] Weights;public float Chest,Span,ArmY,ArmZ,Hand,Root,Chin;
        }
        public const int Subtle=0,Medium=1,Strong=2;
        /// <summary>The level the female body wears (the owner picks; Medium until then).</summary>
        public const int DefaultLevel=Medium;
        public const string ChestBone="spine_04",HeadBone="head";
        private static readonly string[] Shape={"BodyType","Weight","Chest","Curvy","WaistSize","ButtSize","NeckThickness","EyeRoundness","Roundness"};
        public static readonly Level[] Levels=
        {
            new Level{Name="Subtle",Shapes=Shape,Weights=new float[]{-10,-10,60,100,25,20,-15,57,15},Chest=.971f,Span=.96f,ArmY=.96f,ArmZ=.88f,Hand=.96f,Root=.98f,Chin=.95f},
            new Level{Name="Medium",Shapes=Shape,Weights=new float[]{-20,-20,70,115,45,35,-30,62,30},Chest=.971f,Span=.93f,ArmY=.92f,ArmZ=.8f,Hand=.93f,Root=.96f,Chin=.9f},
            new Level{Name="Strong",Shapes=Shape,Weights=new float[]{-32,-28,85,130,65,55,-45,68,45},Chest=.96f,Span=.9f,ArmY=.9f,ArmZ=.73f,Hand=.9f,Root=.95f,Chin=.86f},
        };
        public static Level LevelOf(int level)=>Levels[level<0||level>=Levels.Length?DefaultLevel:level];
        /// <summary>The female stance on the lobby, Locker and portrait poses (degrees): the thighs in toward each other, the knees turned
        /// in, the chest up and back, the hips rolled toward the weight leg with the chest against them (the pack's BMAC_F_Idle against
        /// BMAC_M_Idle: Upper Leg In-Out -.14..-.17, Twist In-Out -.36..-.52, UpperChest Front-Back +.1 and Left-Right +.23).</summary>
        public const float StanceLegsIn=6,StanceKneesIn=10,StanceChestUp=4,StanceHipRoll=3;
        /// <summary>The owner's option on the match idles only (off: the pack's match idles stay, owner 2026-11-05/10): lighter legs and
        /// knees, more chest (the idles stoop), standing still only.</summary>
        public const float MatchStanceLegsIn=4,MatchStanceKneesIn=6,MatchStanceChestUp=7;
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
