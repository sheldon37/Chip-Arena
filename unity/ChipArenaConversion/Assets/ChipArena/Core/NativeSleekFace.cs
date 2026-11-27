using System;
using System.Numerics;
using Kit=ChipArena.UnityConversion.NativeModelHatMeshes.Kit;

namespace ChipArena.UnityConversion
{
    // The sleek face gear (owner 2026-11-27: "think we need to update the "masks" section now, they look out of place now with the
    // latest robot armor", then "They look to bulky / wide"; docs/PILOT-BODY-HAIR.md section 6): the MASKS row's helmet, half mask,
    // visor and mask remade in the sleek pilot suit's style (designed plates on a surface, bevelled, metal rims, glowing accents),
    // close round the head, and the suit's ear pods. tools/gear/facegear.py makes them; the local hair bank carries them (Core/
    // NativeHairBank.cs version 4) as plates named by their gear ("Helmet: crown", ...), each in the head bone's frame, the
    // robot suit's paints (NativeRobotSuit.PlatePaint: the shell the pilot's body colour, the light its accent). Without the bank
    // (or with an older one) the face gear is the robot helmet's (Core/NativeRobotHelm.cs) and the concept's (NativePilotGear) as
    // before. Presentation only: no rule, hit volume, timing or message reads it.
    public static class NativeSleekFace
    {
        /// <summary>The plates' name prefixes: each gear's, and the suit's ear pods (in place of the robot suit's ear modules).</summary>
        public const string Helmet="Helmet: ",HalfMask="Half mask: ",Visor="Visor: ",Mask="Mask: ",Ears="Ears: ";
        private static readonly string[] Gears={Helmet,HalfMask,Visor,Mask,Ears};
        /// <summary>Whether the sleek face gear is worn (set by the presentation when the local bank carries every gear).</summary>
        public static bool On;
        /// <summary>Whether plate `name` is face gear's (else the suit's).</summary>
        public static bool IsFace(string name){foreach(var g in Gears)if(name!=null&&name.StartsWith(g,StringComparison.Ordinal))return true;return false;}
        /// <summary>Whether `plates` carry gear `gear`'s.</summary>
        public static bool Has(NativeHairBank.Plate[] plates,string gear){if(plates!=null)foreach(var p in plates)if(p.Name.StartsWith(gear,StringComparison.Ordinal))return true;return false;}
        /// <summary>Whether `plates` carry all four gears and the ear pods.</summary>
        public static bool HasAll(NativeHairBank.Plate[] plates){foreach(var g in Gears)if(!Has(plates,g))return false;return true;}
        /// <summary>Gear `gear`'s plates as one part on `bone` (the head: CapturedRig.Head for the gear's own models, the robot suit's
        /// Head for its ear pods), named `name`, each vertex in the suit's paints; none without them.</summary>
        public static SheldonSection[] Sections(NativeHairBank.Plate[] plates,string gear,string name,int bone=CapturedRig.Head)
        {
            if(!Has(plates,gear))return Array.Empty<SheldonSection>();
            var k=new Kit();Add(k,k.Part(name,bone),plates,gear);return k.Parts.ToArray();
        }
        /// <summary>Gear `gear`'s plates added to part `s`.</summary>
        internal static void Add(Kit k,SheldonSection s,NativeHairBank.Plate[] plates,string gear)
        {
            foreach(var pl in plates)
            {
                if(!pl.Name.StartsWith(gear,StringComparison.Ordinal))continue;
                int b=s.Positions.Count,n=pl.Paint.Length;
                for(int i=0;i<n;i++)k.Vertex(s,new Vector3(pl.Positions[i*3],pl.Positions[i*3+1],pl.Positions[i*3+2]),new Vector3(pl.Normals[i*3],pl.Normals[i*3+1],pl.Normals[i*3+2]),NativeRobotSuit.PlatePaint(pl.Paint[i]));
                for(int i=0;i+2<pl.Triangles.Length;i+=3)s.Tri(b+pl.Triangles[i],b+pl.Triangles[i+1],b+pl.Triangles[i+2]);
            }
        }

        // ---- the helmet's shell (facegear.py: the old helmet's superellipsoid fitted close round the head, its front leaning
        // back up the forehead) ----
        public const float AxisZ=.033f,Mid=.07f,Side=.105f,Ahead=.1245f,Behind=.126f,Crown=.221f,Under=.145f,N=2.15f,Nb=2.75f,M=3f,Ml=3.2f,Lean=.3f;
        /// <summary>The helmet's panels' outer face over its shell (their gap and thickness) and the crest's on the crown.</summary>
        public const float Panels=.0065f;
        /// <summary>Whether a point is inside the shell grown by `grow` (its implicit form: under 1 inside, 1 on it).</summary>
        public static float Inside(Vector3 p,float grow=0)
        {
            float dy=p.Y-Mid,dz=p.Z-AxisZ,h=dy>0?Crown-Mid:Under,front=dz>0?Ahead*(1-Lean*Math.Max(0,dy/h)):Behind,m=dy>0?M:Ml,n=dz>0?N:Nb;
            float a=Side+grow,f=front+grow,hh=h+grow;
            return MathF.Pow(MathF.Pow(MathF.Pow(MathF.Abs(p.X/a),n)+MathF.Pow(MathF.Abs(dz/f),n),m/n)+MathF.Pow(MathF.Abs(dy/hh),m),1/m);
        }

        // ---- a hat on the helmet ----
        /// <summary>A hat on the sleek helmet (as on the robot helmet, Core/NativeRobotHelm.HatFit: scaled about its HatPivot, lifted and
        /// moved ahead): each hat sized to the helmet's girth where it sits (the robot helmet's fit times .931, none under its own
        /// size) and lowered until it touches, 2 mm off the panels (tools/gear/facegear.py's shell; measured against the model hats,
        /// Tests/RobotHelmChecks.cs); the halo as on the robot helmet, half a centimetre lower.</summary>
        private static readonly string[] FitHats={"cap","beanie","top","wizard","cowboy","crown","cone","antenna","halo"};
        private static readonly float[] FitScale={1.04f,1.06f,1.06f,1.06f,1.05f,1,1,1,1},FitLift={.016f,.012f,.008f,.019f,.012f,-.012f,.023f,.02f,.015f},FitAhead={-.005f,-.003f,-.006f,-.008f,-.005f,-.007f,-.005f,-.008f,0};
        /// <summary>Hat `hat`'s fit on the sleek helmet: a point p of it (head bone frame) goes to p * scale + move (a hat not made for
        /// the model takes the closed hats' fit).</summary>
        public static void HatFit(string hat,out float scale,out Vector3 move)
        {
            int i=Math.Max(0,Array.IndexOf(FitHats,hat));scale=FitScale[i];move=NativeRobotHelm.HatPivot*(1-scale)+new Vector3(0,FitLift[i],FitAhead[i]);
        }
    }
}
