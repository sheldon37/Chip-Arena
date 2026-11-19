using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using ChipArena.UnityConversion;

// Vector drawings of the actual walk fixture. Coordinates, depths and traces are read from the generator.
internal static class LayeredCityWalkPlans
{
    private static readonly string[] Directions = { "N", "E", "S", "W" };
    private static readonly string[] Colours = { "#2256ad", "#a14c77", "#ad531b", "#147a64" };
    private static readonly string[] HeightColours = { "#dfe7e8", "#c5d2d5", "#abbcc0", "#8d9fa5", "#6e838b", "#546872" };
    private static readonly float[] Heights = { -21, -14, -7, 0, 14, 25 };
    private const float Scale = 1.02f;
    private static string F(float n) => n.ToString("0.##", CultureInfo.InvariantCulture);

    public static void Write(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (bool large in new[] { false, true })
        {
            string option = large ? "B" : "A";
            string file = System.IO.Path.Combine(directory, "meridian-scale-" + option.ToLowerInvariant() + ".svg");
            using var w = XmlWriter.Create(file, new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) });
            w.WriteStartElement("svg", "http://www.w3.org/2000/svg");
            w.WriteAttributeString("viewBox", "0 0 1400 3134");
            w.WriteAttributeString("width", "1400"); w.WriteAttributeString("height", "3134");
            w.WriteAttributeString("font-family", "DejaVu Sans, Arial, sans-serif");
            w.WriteElementString("title", "Meridian A4 scale walk, option " + option + ", eight generated seeds");
            w.WriteElementString("desc", "North-up collision footprints and four gate-to-hall routes. Each map has four longitudinal route profiles. Both sheets use the same metres per unit.");
            Rect(w, 0, 0, 1400, 3134, "#f5f4ef");
            Text(w, 40, 42, 15, "CHIP ARENA   /   EXPEDITION   /   A4 SCALE WALK", "#405963", true);
            Text(w, 40, 91, 39, "Meridian   /   Option " + option, "#192c33", true);
            Text(w, 40, 124, 18, (large ? "600 × 480 m" : "420 × 340 m") + "     ·     " +
                (large ? "250–300" : "150–190") + " m gate-to-hall target     ·     8 paired seeds", "#374f57");
            Text(w, 40, 167, 17, "Analytical plan views from generated collision geometry. North is up. Lines trace the four gate routes.", "#243e47");
            Text(w, 40, 195, 15, "Elevation (m)", "#405963", true);
            for (int i = 0; i < Heights.Length; i++)
            {
                float x = 159 + i * 86;
                Rect(w, x, 179, 18, 17, HeightColours[i]);
                Text(w, x + 25, 194, 15, F(Heights[i]) + (i == 5 ? "+" : ""), "#243e47");
            }
            Rect(w, 944, 187, 100 * Scale, 3, "#243e47");
            Rect(w, 944, 181, 2, 10, "#243e47");
            Rect(w, 944 + 100 * Scale - 2, 181, 2, 10, "#243e47");
            Text(w, 1059, 194, 15, "100 m   ·   same scale on A and B", "#243e47");
            for (int index = 0; index < 8; index++) DrawSeed(w, large, index);
            Text(w, 40, 3110, 14, "Profiles use walked distance horizontally (0–300 m) and elevation vertically (−21 to +14 m). A and B use the same seed for each pair.", "#405963");
            w.WriteEndElement();
            Console.WriteLine("Wrote " + file);
        }
    }

    private static void DrawSeed(XmlWriter w, bool large, int index)
    {
        int x = 40 + index % 2 * 670, y = 222 + index / 2 * 714;
        uint seed = (uint)(647 + 137 * index);
        var walk = new NativeLayeredCityWalk(large, seed);
        var b = walk.Map.bounds;
        Rect(w, x, y, 650, 695, "#ffffff");
        Text(w, x + 16, y + 33, 21, "Seed " + seed, "#192c33", true);
        float cx = x + 325, cy = y + 294;
        float X(float v) => cx + v * Scale;
        float Z(float v) => cy - v * Scale;
        // Paint in vertical order, batching coplanar shapes by height band.
        var shapes = new StringBuilder[HeightColours.Length];
        for (int i = 0; i < shapes.Length; i++) shapes[i] = new StringBuilder();
        foreach (var block in walk.Map.blocks.OrderBy(item => item.pos.y + item.size.y / 2))
        {
            float top = block.pos.y + block.size.y / 2;
            int band = Array.FindLastIndex(Heights, h => top >= h);
            var path = shapes[Math.Max(0, band)];
            path.Append("M").Append(F(X(block.pos.x - block.size.x / 2))).Append(' ').Append(F(Z(block.pos.z + block.size.z / 2)))
                .Append('h').Append(F(block.size.x * Scale)).Append('v').Append(F(block.size.z * Scale))
                .Append('h').Append(F(-block.size.x * Scale)).Append('z');
        }
        for (int i = 0; i < shapes.Length; i++) Path(w, shapes[i].ToString(), HeightColours[i]);
        for (int side = 0; side < walk.Routes.Count; side++)
        {
            var route = walk.Routes[side];
            string points = string.Join(" L", route.Points.Select(p => F(X(p.x)) + " " + F(Z(p.z))));
            Path(w, "M" + points, "none", "#ffffff", 6);
            Path(w, "M" + points, "none", Colours[side], 3.6f);
            var end = route.Points[route.Points.Length - 1];
            Rect(w, X(end.x) - 3.5f, Z(end.z) - 3.5f, 7, 7, Colours[side]);
            float pos = x + 16 + 157 * side;
            Text(w, pos, y + 585, 16, Directions[side] + "   " + route.Name, Colours[side], true);
            Text(w, pos, y + 606, 15, F((float)Math.Round(route.Length)) + " m", "#233d45");
            const float profileWidth = 139, profileHeight = 53;
            float baseY = y + 674;
            Rect(w, pos, baseY - profileHeight, profileWidth, profileHeight, "#f0f3f2");
            float Y(float elevation) => baseY - (elevation + 21) / 35 * profileHeight;
            Path(w, "M" + F(pos) + " " + F(Y(0)) + "h" + F(profileWidth), "none", "#c2cdce", 1);
            var profile = new StringBuilder("M" + F(pos) + " " + F(Y(route.Points[0].y)));
            float distance = 0;
            for (int j = 1; j < route.Points.Length; j++)
            {
                distance += (route.Points[j] - route.Points[j - 1]).Length;
                profile.Append(" L").Append(F(pos + distance / 300 * profileWidth)).Append(' ').Append(F(Y(route.Points[j].y)));
            }
            Path(w, profile.ToString(), "none", Colours[side], 2.5f);
        }
        Rect(w, cx - 3, cy - 3, 6, 6, "#243e47");
        Text(w, x + 16, y + 556, 14, "Gate → hall    /    Walked metres and elevation profiles", "#405963");
        Text(w, x + 534, y + 33, 14, F(b.maxX - b.minX) + " × " + F(b.maxZ - b.minZ) + " m", "#405963");
    }

    private static void Rect(XmlWriter w, float x, float y, float width, float height, string fill)
    {
        w.WriteStartElement("rect");
        w.WriteAttributeString("x", F(x)); w.WriteAttributeString("y", F(y));
        w.WriteAttributeString("width", F(width)); w.WriteAttributeString("height", F(height));
        w.WriteAttributeString("fill", fill); w.WriteEndElement();
    }
    private static void Path(XmlWriter w, string d, string fill, string stroke = null, float width = 0)
    {
        w.WriteStartElement("path"); w.WriteAttributeString("d", d); w.WriteAttributeString("fill", fill);
        if (stroke != null) { w.WriteAttributeString("stroke", stroke); w.WriteAttributeString("stroke-width", F(width)); }
        w.WriteEndElement();
    }
    private static void Text(XmlWriter w, float x, float y, float size, string value, string fill, bool bold = false)
    {
        w.WriteStartElement("text"); w.WriteAttributeString("x", F(x)); w.WriteAttributeString("y", F(y));
        w.WriteAttributeString("fill", fill); w.WriteAttributeString("font-size", F(size));
        if (bold) w.WriteAttributeString("font-weight", "bold");
        w.WriteString(value); w.WriteEndElement();
    }
}
