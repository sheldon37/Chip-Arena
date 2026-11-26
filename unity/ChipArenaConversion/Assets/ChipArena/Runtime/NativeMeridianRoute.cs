using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Meridian's described route on screen (Core NativeMeridianRoute places it; the local bank ChipArenaCityRoute, built by
    /// tools/stages/meridian_route.py, holds its meshes; docs/expedition/MERIDIAN-ROUTE.md): Crown Hill's crossing, footways,
    /// balustrades, corner shop, sign and roofs, and the Ironworks' loading bay, through the landmarks' drawer (the same tiers, far
    /// material and hidden blocks); and the smoke over the Ironworks' far stacks, one mesh of low-poly puffs rebuilt each frame in the
    /// far material (fogged no further than the far layer, so the plumes read past the night's fog). Without the bank nothing is
    /// created and every drawer keeps its look. Presentation only: never a collider, never sim data. The smoke allocates nothing per
    /// frame once built.
    /// </summary>
    public sealed class NativeMeridianRouteDraw : MonoBehaviour
    {
        private NativeMeridianLandmarksDraw art, fronts;
        private NativeMeridianRoute.Plan plan;
        private Mesh puff, smoke;
        private Vector3[] puffV, puffN, smokeV, smokeN;
        private Color32[] puffC, smokeC;
        private int[] puffT, solidT, glowT;
        private Material smokeSolid, smokeGlow;
        private Material[] smokeMats;
        private GameObject smokeRoot;
        private float started;

        /// <summary>Whether the route's bank is present (the plan's facades, keep-outs and replaced pieces apply only then).</summary>
        public static bool HasBank => NativePackBanks.Bank(NativeMeridianRoute.Bank) != null;
        /// <summary>Whether the bank carries a piece (the corner shop's storefront variant: without it the street dress keeps the
        /// lot's ground storey).</summary>
        public static bool Has(string key) => NativePackBanks.Bank(NativeMeridianRoute.Bank)?.Find(key + "/0") != null;
        /// <summary>The storefront the live city places: Core's choice when the bank carries it, else the release-27 front when the
        /// bank carries that (a code build ahead of the Drive zip keeps today's shop).</summary>
        public static string StorefrontInBank()
            => Has(NativeMeridianRoute.CornerKey) || !Has(NativeMeridianRoute.Corner) ? NativeMeridianRoute.Storefront : "";
        /// <summary>The pieces drawn and the plumes smoking (the captures' log line).</summary>
        public int Pieces => (art != null ? art.Pieces : 0) + (fronts != null ? fronts.Pieces : 0);
        public int Plumes => smoke != null && plan != null ? plan.Plumes.Count : 0;

        /// <summary>Builds the plan (any earlier build is released first). False without the bank: nothing draws.</summary>
        public bool Build(NativeMeridianRoute.Plan route, Camera camera)
        {
            Release();
            plan = route;
            if (route == null || !HasBank) return false;
            try
            {
                // the street's pieces fog as the street does; a door front's piece takes the far material with the front it stands in
                var street = new NativeMeridianLandmarks.Layout { Plan = route.Art.Plan }; var front = new NativeMeridianLandmarks.Layout { Plan = route.Art.Plan };
                foreach (var p in route.Art.Pieces) (p.Kind == NativeMeridianLandmarks.Kind.Front ? front : street).Pieces.Add(p);
                foreach (var c in route.Art.Covers) street.Covers.Add(c);
                art = gameObject.AddComponent<NativeMeridianLandmarksDraw>();
                bool drawn = art.Build(street, camera, NativeMeridianRoute.Bank, false);
                fronts = gameObject.AddComponent<NativeMeridianLandmarksDraw>();
                drawn |= fronts.Build(front, camera, NativeMeridianRoute.Bank);
                bool smoking = route.Plumes.Count > 0 && BuildSmoke();
                return drawn || smoking;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Meridian route disabled: " + e.Message);
                Release();
                return false;
            }
        }

        private bool BuildSmoke()
        {
            var bank = NativePackBanks.Bank(NativeMeridianRoute.Bank);
            var prop = bank?.Find(NativeMeridianRoute.Puff + "/0");
            if (prop == null || prop.Parts.Length == 0) return false;
            puff = NativePackBanks.Mesh(NativeMeridianRoute.Bank, prop.Parts[0].Mesh);
            var far = Resources.Load<Shader>("NativeMeridianFar");
            if (puff == null || far == null || !far.isSupported) return false;
            // the puff's own corners, read back once (its mesh was uploaded non-readable, so from the bank's record)
            var src = bank.Meshes[prop.Parts[0].Mesh];
            int n = src.Positions.Length;
            puffV = new Vector3[n]; puffN = new Vector3[n]; puffC = new Color32[n];
            for (int i = 0; i < n; i++)
            {
                puffV[i] = new Vector3(src.Positions[i].X, src.Positions[i].Y, src.Positions[i].Z);
                puffN[i] = new Vector3(src.Normals[i].X, src.Normals[i].Y, src.Normals[i].Z);
                puffC[i] = src.Colors != null ? new Color32(src.Colors[i * 4], src.Colors[i * 4 + 1], src.Colors[i * 4 + 2], src.Colors[i * 4 + 3]) : new Color32(128, 128, 128, 255);
            }
            int tris = 0; foreach (var s in src.Submeshes) tris += s.Indices.Length;
            puffT = new int[tris]; int k = 0; foreach (var s in src.Submeshes) foreach (var t in s.Indices) puffT[k++] = t;
            int puffs = plan.Plumes.Count * NativeMeridianRoute.PuffsPerPlume;
            smokeV = new Vector3[n * puffs]; smokeN = new Vector3[n * puffs]; smokeC = new Color32[n * puffs];
            solidT = new int[tris * puffs]; glowT = new int[tris * puffs];
            smoke = new Mesh { name = "Meridian smoke", indexFormat = smokeV.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            smoke.MarkDynamic();
            smokeSolid = new Material(far) { name = "Meridian smoke" }; smokeSolid.SetFloat("_Glow", 0); smokeSolid.SetFloat("_FogCap", NativeMeridianLandmarks.FarFogCap);
            smokeGlow = new Material(far) { name = "Meridian smoke glow" }; smokeGlow.SetFloat("_Glow", 1); smokeGlow.SetFloat("_FogCap", NativeMeridianLandmarks.FarFogCap);
            smokeMats = new[] { smokeSolid, smokeGlow };
            smokeRoot = new GameObject("Meridian smoke");
            smokeRoot.transform.SetParent(transform, false);
            smokeRoot.AddComponent<MeshFilter>().sharedMesh = smoke;
            var r = smokeRoot.AddComponent<MeshRenderer>();
            r.sharedMaterials = smokeMats; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            started = Time.time;
            Smoke(0, true);
            return true;
        }

        private void LateUpdate()
        {
            if (smoke != null) Smoke(Time.time - started, false);
        }

        // The puffs at time t, one mesh: each puff the bank's puff scaled to its radius, turned slowly about its rise, tinted from the
        // flame's orange (the youngest, on the glow sub-mesh: lit from below) to the soot's grey as it ages and thins.
        private void Smoke(float t, bool first)
        {
            int n = puffV.Length, tris = puffT.Length, solid = 0, glow = 0, v = 0;
            var warm = new Color(1f, .46f, .16f); var soot = new Color(.34f, .32f, .34f); var pale = new Color(.62f, .6f, .62f);
            for (int p = 0; p < plan.Plumes.Count; p++)
            {
                var plume = plan.Plumes[p];
                for (int i = 0; i < NativeMeridianRoute.PuffsPerPlume; i++)
                {
                    NativeMeridianRoute.PuffAt(plume, i, t, out var c, out float radius);
                    float age = NativeMeridianRoute.AgeOf(plume, i, t);
                    var rot = Quaternion.Euler(0, (p * 53 + i * 37) % 360 + t * (6 + i % 3 * 2), 0);
                    var centre = new Vector3(c.x, c.y, c.z);
                    bool hot = age < .16f;
                    var tint = hot ? Color.Lerp(warm, soot, age / .16f) : Color.Lerp(soot, pale, Mathf.Clamp01((age - .16f) / .6f));
                    int baseV = v;
                    for (int k = 0; k < n; k++)
                    {
                        smokeV[v] = centre + rot * (puffV[k] * radius);
                        smokeN[v] = rot * puffN[k];
                        var pc = puffC[k];
                        smokeC[v] = new Color32((byte)(pc.r * tint.r), (byte)(pc.g * tint.g), (byte)(pc.b * tint.b), pc.a);
                        v++;
                    }
                    var into = hot ? glowT : solidT; int at = hot ? glow : solid;
                    for (int k = 0; k < tris; k++) into[at + k] = baseV + puffT[k];
                    if (hot) glow += tris; else solid += tris;
                }
            }
            smoke.SetVertices(smokeV, 0, v);
            smoke.SetNormals(smokeN, 0, v);
            smoke.SetColors(smokeC, 0, v);
            // the glow sub-mesh's gain (uv1.y, as the bank's glow faces carry it): the flame's light on the youngest puffs
            if (first)
            {
                var uv1 = new Vector2[smokeV.Length]; for (int k = 0; k < uv1.Length; k++) uv1[k] = new Vector2(0, .9f);
                smoke.SetUVs(1, uv1);
                smoke.subMeshCount = 2;
            }
            smoke.SetTriangles(solidT, 0, solid, 0, false);
            smoke.SetTriangles(glowT, 0, glow, 1, false);
            smoke.bounds = new Bounds(new Vector3(0, 0, 0), new Vector3(4000, 800, 4000));
        }

        /// <summary>Lets everything go: the pieces (their hidden blocks back to the dressing), the smoke.</summary>
        public void Release()
        {
            if (art != null) { art.Release(); Destroy(art); }
            if (fronts != null) { fronts.Release(); Destroy(fronts); }
            art = fronts = null;
            if (smokeRoot != null) Destroy(smokeRoot);
            if (smoke != null) Destroy(smoke);
            if (smokeSolid != null) Destroy(smokeSolid);
            if (smokeGlow != null) Destroy(smokeGlow);
            smokeRoot = null; smoke = null; smokeSolid = smokeGlow = null; smokeMats = null; puff = null; plan = null;
        }

        private void OnDestroy() => Release();
    }
}
