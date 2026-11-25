using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Meridian's landmarks and doors on screen (Core NativeMeridianLandmarks places them; the bank ChipArenaCityLandmarks, built by
    /// tools/stages/meridian_landmarks.py, holds their meshes): the 20 exit landmarks, the final court with City Hall and its clock
    /// tower, each dealt hall's door front and airlock leaves, the far layer's instance landmarks and the WMRD 64.7 mast.
    /// <para>Each piece is one renderer whose mesh follows the eye's distance: the Fine tier within 38 m, the Low tier beyond (and
    /// always on the Low graphics tier), and past the distance where the look's fog would take more than
    /// NativeMeridianLandmarks.FarFogCap (or for a piece that has only a far mesh) the far mesh or the Low one in the far material
    /// (ChipArena/MeridianFar), whose fog stops at the cap, so the far layer reads through the night's fog by its lights.</para>
    /// <para>While it draws, the map blocks its pieces replace (Layout.Covers: City Hall, the court, the halls, the hero lots) are
    /// left out of the block dressing (NativeArenaDressing.Hidden), so their boxes never show through. Without the bank (CI, a build
    /// without the Drive zip, --ca-no-pack-banks) nothing is created and the greybox draws exactly as before. Presentation only:
    /// never a collider, never sim data. Allocation free per frame once built.</para>
    /// </summary>
    public sealed class NativeMeridianLandmarksDraw : MonoBehaviour
    {
        private const float FineReach = 38f, Hysteresis = 3f;
        private sealed class Draw
        {
            public NativeMeridianLandmarks.Piece Piece; public MeshRenderer Renderer; public MeshFilter Filter;
            public Mesh Fine, Low, Far; public Bounds Box; public int Shown = -1; public bool Shadow;
        }
        private readonly List<Draw> draws = new List<Draw>();
        private readonly List<string> covered = new List<string>();
        private Material solid, glow, farSolid, farGlow;
        private Material[] one = new Material[1], two = new Material[2], farOne = new Material[1], farTwo = new Material[2];
        private Camera eye;
        private GameObject root;
        /// <summary>The pieces built, and the frame's counts at each tier (the walk's budget line and the captures read them).</summary>
        public int Pieces => draws.Count;
        public int ShownFine { get; private set; }
        public int ShownLow { get; private set; }
        public int ShownFar { get; private set; }

        /// <summary>Builds a layout's pieces (any earlier build is released first). False without the bank or the shaders: nothing
        /// draws then and the greybox stays. bankName: the bank the pieces' keys name (the landmarks' own by default; Meridian's
        /// route draws its pieces from ChipArenaCityRoute through this drawer, NativeMeridianRoute).</summary>
        public bool Build(NativeMeridianLandmarks.Layout layout, Camera camera, string bankName = null)
        {
            Release();
            if (layout == null) return false;
            bankName = bankName ?? NativeMeridianLandmarks.Bank;
            var bank = NativePackBanks.Bank(bankName);
            if (bank == null) return false;
            var stage = Resources.Load<Shader>("NativeStageLit");
            var far = Resources.Load<Shader>("NativeMeridianFar");
            if (stage == null || !stage.isSupported || far == null || !far.isSupported) { Debug.LogWarning("Meridian landmarks: a shader is missing or unsupported"); return false; }
            try
            {
                solid = new Material(stage) { name = "Meridian landmark" }; solid.SetFloat("_Glow", 0);
                glow = new Material(stage) { name = "Meridian landmark glow" }; glow.SetFloat("_Glow", 1);
                farSolid = new Material(far) { name = "Meridian far" }; farSolid.SetFloat("_Glow", 0); farSolid.SetFloat("_FogCap", NativeMeridianLandmarks.FarFogCap);
                farGlow = new Material(far) { name = "Meridian far glow" }; farGlow.SetFloat("_Glow", 1); farGlow.SetFloat("_FogCap", NativeMeridianLandmarks.FarFogCap);
                one[0] = solid; two[0] = solid; two[1] = glow; farOne[0] = farSolid; farTwo[0] = farSolid; farTwo[1] = farGlow;
                eye = camera;
                root = new GameObject("Meridian landmarks " + layout.Plan);
                root.transform.SetParent(transform, false);
                foreach (var piece in layout.Pieces)
                {
                    Mesh fine = MeshOf(bank, bankName, piece.Key + "/0"), low = MeshOf(bank, bankName, piece.Key + "/2"), farMesh = MeshOf(bank, bankName, piece.Key + "/far");
                    if (fine == null && low == null && farMesh == null) continue;
                    var go = new GameObject(piece.Key);
                    go.transform.SetParent(root.transform, false);
                    Place(go.transform, piece.Place);
                    var d = new Draw { Piece = piece, Fine = fine, Low = low, Far = farMesh, Filter = go.AddComponent<MeshFilter>(), Renderer = go.AddComponent<MeshRenderer>() };
                    d.Renderer.lightProbeUsage = LightProbeUsage.Off;
                    d.Renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    d.Renderer.receiveShadows = true;
                    d.Shadow = piece.Kind != NativeMeridianLandmarks.Kind.Far && piece.Kind != NativeMeridianLandmarks.Kind.Mast;
                    var any = fine ?? low ?? farMesh;
                    d.Box = WorldBounds(go.transform, any.bounds);
                    d.Renderer.enabled = false;
                    draws.Add(d);
                }
                foreach (var c in layout.Covers) covered.Add(c);
                return draws.Count > 0;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Meridian landmarks disabled: " + e.Message);
                Release();
                return false;
            }
        }

        private static Mesh MeshOf(NativePackBank.Bank bank, string bankName, string name)
        {
            var prop = bank.Find(name);
            if (prop == null || prop.Parts.Length == 0) return null;
            return NativePackBanks.Mesh(bankName, prop.Parts[0].Mesh);
        }

        // The piece's place (Core's 3 x 4 rows: a turn about +y, the scales along its local x and z, a translation).
        private static void Place(Transform t, float[] m)
        {
            var x = new Vector3(m[0], m[4], m[8]); var y = new Vector3(m[1], m[5], m[9]); var z = new Vector3(m[2], m[6], m[10]);
            t.localPosition = new Vector3(m[3], m[7], m[11]);
            t.localRotation = Quaternion.LookRotation(z.normalized, y.normalized);
            t.localScale = new Vector3(x.magnitude, y.magnitude, z.magnitude);
        }

        private static Bounds WorldBounds(Transform t, Bounds b)
        {
            var w = new Bounds(t.TransformPoint(b.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                w.Encapsulate(t.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return w;
        }

        private void LateUpdate()
        {
            if (draws.Count == 0) return;
            // the replaced blocks' boxes stay out (the dressing clears its hidden set when a new live arena binds)
            for (int i = 0; i < covered.Count; i++) NativeArenaDressing.Hidden.Add(covered[i]);
            var cam = eye != null ? eye : Camera.main;
            if (cam == null) return;
            var at = cam.transform.position;
            bool low = NativeAdvancedLighting.Tier == NativeGraphicsTier.Low;
            float farFrom = float.MaxValue;
            if (RenderSettings.fog)
                farFrom = RenderSettings.fogMode == FogMode.Linear
                    ? NativeMeridianLandmarks.FarFrom(RenderSettings.fogStartDistance, RenderSettings.fogEndDistance)
                    : Mathf.Log(1f / (1f - NativeMeridianLandmarks.FarFogCap)) / Mathf.Max(1e-5f, RenderSettings.fogDensity);
            int fine = 0, lowN = 0, farN = 0;
            for (int i = 0; i < draws.Count; i++)
            {
                var d = draws[i];
                float dist = Mathf.Sqrt(d.Box.SqrDistance(at));
                // 0 Fine, 1 Low, 2 far (the far material)
                int t = d.Fine == null && d.Low == null ? 2 : dist > farFrom ? 2 : (low || dist > FineReach) ? 1 : 0;
                if (d.Shown >= 0 && t > d.Shown && dist < (d.Shown == 0 ? FineReach : farFrom) + Hysteresis) t = d.Shown;
                if (t == 0 && d.Fine == null) t = 1;
                if (t == 1 && d.Low == null && d.Fine == null) t = 2;
                if (t != d.Shown) Show(d, t);
                if (t == 0) fine++; else if (t == 1) lowN++; else farN++;
            }
            ShownFine = fine; ShownLow = lowN; ShownFar = farN;
        }

        private void Show(Draw d, int t)
        {
            Mesh mesh = t == 0 ? d.Fine : t == 1 ? (d.Low ?? d.Fine) : (d.Far ?? d.Low ?? d.Fine);
            d.Filter.sharedMesh = mesh;
            bool farMat = t == 2;
            d.Renderer.sharedMaterials = mesh.subMeshCount > 1 ? (farMat ? farTwo : two) : (farMat ? farOne : one);
            d.Renderer.shadowCastingMode = d.Shadow && !farMat ? ShadowCastingMode.On : ShadowCastingMode.Off;
            d.Renderer.enabled = true;
            d.Shown = t;
        }

        /// <summary>Lets everything go: the renderers, the materials, the hidden blocks back to the dressing.</summary>
        public void Release()
        {
            foreach (var c in covered) NativeArenaDressing.Hidden.Remove(c);
            covered.Clear();
            draws.Clear();
            if (root != null) Destroy(root);
            root = null;
            if (solid != null) Destroy(solid); if (glow != null) Destroy(glow); if (farSolid != null) Destroy(farSolid); if (farGlow != null) Destroy(farGlow);
            solid = glow = farSolid = farGlow = null;
            ShownFine = ShownLow = ShownFar = 0;
        }

        private void OnDestroy() => Release();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public sealed partial class ConversionGame
    {
        private NativeMeridianLandmarksDraw meridianLandmarks;
        /// <summary>The Developer walk's landmarks (the one line in OpenCityWalk): the layout's pieces for the walk's deal, rebuilt on
        /// every open (a new layout or a new deal). The walk's mouths have no closure collider, so the airlocks are drawn open.</summary>
        private void MeridianLandmarksOpen(NativeCityLayout.Plan plan, NativeLayeredCityWalk walk, string[] slots)
        {
            try
            {
                if (meridianLandmarks == null) meridianLandmarks = new GameObject("Meridian landmarks").AddComponent<NativeMeridianLandmarksDraw>();
                var doors = new V3[walk.Routes.Count];
                for (int i = 0; i < doors.Length; i++) doors[i] = walk.Routes[i].Destination;
                var layout = NativeMeridianLandmarks.Place(plan.Seed, walk.Map, doors, slots, 1f);
                bool drawn = meridianLandmarks.Build(layout, cameraView);
                Debug.Log("Meridian landmarks " + plan.Name + ": " + (drawn ? meridianLandmarks.Pieces + " pieces, " + layout.Covers.Count + " blocks replaced" : "no bank, the greybox stays"));
            }
            catch (Exception e) { Debug.LogException(e); meridianLandmarks?.Release(); }
        }
        /// <summary>The walk closed (the one line in CloseCityWalk): the landmarks go and the boxes come back.</summary>
        private void MeridianLandmarksClose() => meridianLandmarks?.Release();
    }
#endif
}
