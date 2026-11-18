#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private bool walkPicker = Array.IndexOf(Environment.GetCommandLineArgs(), "--ca-layered-city-walk") >= 0;
        private string walkSeed = "647";
        private ConversionContent walkOriginalContent;
        private NativeLayeredCityWalk cityWalk;
        private float walkFarClip;
        private string walkError;
        private Texture2D walkPlan;

        private Texture2D BuildWalkPlan(ContentMap map)
        {
            var b = map.bounds;
            int w = (int)(b.maxX - b.minX), h = (int)(b.maxZ - b.minZ);
            var pixels = new Color32[w * h];
            void Paint(float x, float z, float sx, float sz, Color32 colour)
            {
                int x0 = Mathf.Clamp(Mathf.FloorToInt(x - sx / 2 - b.minX), 0, w - 1);
                int x1 = Mathf.Clamp(Mathf.CeilToInt(x + sx / 2 - b.minX), x0 + 1, w);
                int z0 = Mathf.Clamp(Mathf.FloorToInt(z - sz / 2 - b.minZ), 0, h - 1);
                int z1 = Mathf.Clamp(Mathf.CeilToInt(z + sz / 2 - b.minZ), z0 + 1, h);
                for (int row = z0; row < z1; row++)
                    for (int col = x0; col < x1; col++) pixels[row * w + col] = colour;
            }
            foreach (var block in map.blocks)
            {
                float top = block.pos.y + block.size.y / 2;
                byte tone = (byte)Mathf.Clamp(64 + (top + 21) * 3, 64, 235);
                Paint(block.pos.x, block.pos.z, block.size.x, block.size.z, new Color32(tone, tone, tone, 255));
            }
            foreach (var node in map.nav.nodes) Paint(node.pos.x, node.pos.z, 2, 2, new Color32(30, 210, 160, 255));
            var image = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            image.SetPixels32(pixels);
            image.Apply(false, true);
            return image;
        }

        private bool LayeredCityWalkKeys()
        {
            if (Online || dedicated || deploymentRoom != null || expeditionSession != null) return false;
            if (cityWalk != null)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || PadBack)
                {
                    CloseCityWalk();
                    return true;
                }
                if (arena?.Local?.Hp <= 0) arena.Reset();
                return false;
            }
            if (arena != null || !menu || previewArena != null) return false;
            if (!walkPicker) return false;
            if (Input.GetKeyDown(KeyCode.Escape) || PadBack) walkPicker = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return true;
        }

        private void OpenCityWalk(bool large, uint seed)
        {
            if (arena != null || Online || dedicated || deploymentRoom != null || expeditionSession != null) return;
            try
            {
                cityWalk = new NativeLayeredCityWalk(large, seed);
                walkOriginalContent = content;
                walkFarClip = cameraView.farClipPlane;
                content = content.WithGeneratedMap(cityWalk.Map);
                walkPlan = BuildWalkPlan(cityWalk.Map);
                EndPreview();
                ClearReplay();
                endFlow.Reset();
                match = new NativeMatch(content, seed) { Practice = true };
                match.Seats.Add(new NativeSeat { id = 1, name = "Survey pilot", spec = "pistol" });
                match.ApplyNetwork(new NativeMatchFrame { phase = NativePhase.Fight, cooperative = true, round = 1, set = 1 });
                arena = new NativeArena(content, match, cityWalk.Map.id) { LocalId = 1 };
                arena.Reset();
                generation = arena.Generation;
                visualClock.Reset(match.Time);
                presentation.ResetPresentation();
                pending = new InputFrame();
                yaw = arena.Local.Yaw;
                pitch = 12;
                sprint = false;
                menu = false;
                walkError = null;
                NativeLighting.Apply(cityWalk.Map, cameraView);
                cameraView.farClipPlane = 800;
            }
            catch (Exception ex)
            {
                CloseCityWalk();
                walkError = ex.Message;
                Debug.LogException(ex);
            }
        }

        private void CloseCityWalk()
        {
            arena?.Dispose();
            arena = null;
            match = null;
            cityWalk = null;
            if (walkPlan != null) Destroy(walkPlan);
            walkPlan = null;
            if (walkOriginalContent != null)
            {
                content = walkOriginalContent;
                walkOriginalContent = null;
                cameraView.farClipPlane = walkFarClip;
                NativeLighting.Apply(content.Map(PreviewMapId), cameraView);
            }
            presentation.ResetPresentation();
            pending = new InputFrame();
            menu = true;
            walkPicker = true;
        }

        private bool LayeredCityWalkGui(float width, float height)
        {
            if (Online || dedicated || deploymentRoom != null || expeditionSession != null) return false;
            if (cityWalk != null && arena?.Local != null)
            {
                var pos = arena.Local.Body.position;
                GUILayout.BeginArea(new Rect(20, 20, 410, 225), GUI.skin.box);
                GUILayout.Label(cityWalk.Map.name + "   |   seed " + walkSeed);
                string[] gates = { "North", "East", "South", "West" };
                for (int i = 0; i < cityWalk.Routes.Count; i++)
                {
                    var route = cityWalk.Routes[i];
                    GUILayout.Label(gates[i] + "   " + route.Name + "   " + route.Length.ToString("0") + " m from gate");
                }
                GUILayout.Label(string.Format("East {0:0}   North {1:0}   Elevation {2:0.0} m", pos.x, pos.z, pos.y - NativeLocomotionRules.HalfHeight));
                GUILayout.Label("Hold Tab   Street plan     Esc   Switch size or seed");
                GUILayout.EndArea();
                if (walkPlan != null && Input.GetKey(KeyCode.Tab))
                {
                    float scale = Mathf.Min((width - 60) / walkPlan.width, (height - 100) / walkPlan.height);
                    var r = new Rect((width - walkPlan.width * scale) / 2, (height - walkPlan.height * scale) / 2,
                        walkPlan.width * scale, walkPlan.height * scale);
                    GUI.DrawTexture(r, walkPlan);
                    float x = r.center.x + pos.x * scale, z = r.center.y - pos.z * scale;
                    NativeUi.Fill(new Rect(x - 4, z - 4, 8, 8), Color.yellow);
                    GUI.Label(new Rect(r.x, r.yMax + 4, r.width, 30), "North up   ·   Yellow pilot   ·   Gate routes in green   ·   Brighter = higher");
                }
                return true;
            }
            if (!walkPicker || !menu || arena != null || previewArena != null) return false;
            GUILayout.BeginArea(new Rect(width / 2 - 200, height / 2 - 140, 400, 280), GUI.skin.box);
            GUILayout.Label("Meridian   /   solo scale walk", heading);
            GUILayout.Label("World seed");
            walkSeed = GUILayout.TextField(walkSeed, 10);
            bool valid = uint.TryParse(walkSeed, NumberStyles.None, CultureInfo.InvariantCulture, out uint seed);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && valid;
            if (GUILayout.Button("Walk A   ·   420 × 340 m", button)) OpenCityWalk(false, seed);
            if (GUILayout.Button("Walk B   ·   600 × 480 m", button)) OpenCityWalk(true, seed);
            GUI.enabled = enabled;
            if (!valid) GUILayout.Label("Enter a whole number from 0 to 4294967295.");
            if (walkError != null) GUILayout.Label(walkError);
            if (GUILayout.Button("Back to menu")) walkPicker = false;
            GUILayout.EndArea();
            return true;
        }
    }
}
#endif
