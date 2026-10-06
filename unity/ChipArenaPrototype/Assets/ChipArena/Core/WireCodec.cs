using System;
using System.IO;

namespace ChipArena.UnityPrototype
{
    // Length framing is in LanLink. Binary protocol is private to this prototype, never the Electron protocol.
    public static class WireCodec
    {
        public const int MaxPacket = 256 * 1024;
        private const int Magic = 0x43415031;
        public static byte[] Input(InputFrame input)
        {
            using (var stream = new MemoryStream()) using (var w = new BinaryWriter(stream))
            {
                w.Write(Magic); w.Write((byte)1); w.Write(input.Sequence); w.Write(input.X); w.Write(input.Z); w.Write(input.Yaw);
                int flags = (input.Attack ? 1 : 0) | (input.Block ? 2 : 0) | (input.Roll ? 4 : 0) | (input.Shoot ? 8 : 0) | (input.Jump ? 16 : 0) | (input.Sprint ? 32 : 0);
                w.Write((byte)flags); return stream.ToArray();
            }
        }
        public static InputFrame ReadInput(byte[] data)
        {
            using (var r = Reader(data, 1))
            {
                InputFrame i = new InputFrame { Sequence = r.ReadInt32(), X = Float(r), Z = Float(r), Yaw = Float(r) };
                byte f = r.ReadByte(); if (f > 63 || !i.Valid) throw new InvalidDataException("Invalid input");
                i.Attack = (f & 1) != 0; i.Block = (f & 2) != 0; i.Roll = (f & 4) != 0;
                i.Shoot = (f & 8) != 0; i.Jump = (f & 16) != 0; i.Sprint = (f & 32) != 0;
                End(r); return i.Sanitized();
            }
        }
        public static byte[] State(Snapshot s)
        {
            using (var stream = new MemoryStream()) using (var w = new BinaryWriter(stream))
            {
                w.Write(Magic); w.Write((byte)2); w.Write(s.Tick); w.Write(s.Actors.Length);
                foreach (Actor a in s.Actors)
                {
                    w.Write(a.Id); w.Write(a.Team); w.Write(a.Bot); w.Write(a.Combo); w.Write(a.Action); w.Write(a.Kills); w.Write(a.LastInput);
                    w.Write(a.Hp); w.Write(a.Stamina); w.Write(a.Yaw); Vec(w, a.Position); Vec(w, a.Velocity); Vec(w, a.RollDirection);
                    w.Write(a.AttackAt); w.Write(a.AttackUntil); w.Write(a.BlockUntil); w.Write(a.BlockReady); w.Write(a.RollAt); w.Write(a.RollReady);
                    w.Write(a.StunUntil); w.Write(a.FlashUntil); w.Write(a.RespawnAt); w.Write(a.Sprinting);
                }
                w.Write(s.Covers.Length);
                foreach (Cover c in s.Covers) { w.Write(c.Id); Vec(w, c.Position); w.Write(c.HalfX); w.Write(c.HalfZ); w.Write(c.Height); w.Write(c.Hp); }
                w.Write(s.Shots.Length);
                foreach (Shot p in s.Shots) { w.Write(p.Id); w.Write(p.Owner); w.Write(p.Team); w.Write(p.Reflections); Vec(w, p.Position); Vec(w, p.Velocity); w.Write(p.Damage); w.Write(p.Life); }
                w.Write(s.Events.Length);
                foreach (ArenaEvent e in s.Events) { w.Write(e.Id); w.Write((byte)e.Kind); w.Write(e.Source); w.Write(e.Target); Vec(w, e.Position); w.Write(e.Amount); }
                if (stream.Length > MaxPacket) throw new InvalidDataException("Snapshot too large");
                return stream.ToArray();
            }
        }
        public static Snapshot ReadState(byte[] data)
        {
            using (var r = Reader(data, 2))
            {
                var s = new Snapshot { Tick = r.ReadInt32() }; if (s.Tick < 0) throw new InvalidDataException("Invalid tick");
                s.Actors = new Actor[Count(r, Rules.MaxBots + 2)];
                var seen = new System.Collections.Generic.HashSet<int>();
                for (int n = 0; n < s.Actors.Length; n++)
                {
                    var a = new Actor { Id = r.ReadInt32(), Team = r.ReadInt32(), Bot = r.ReadBoolean(), Combo = r.ReadInt32(), Action = r.ReadInt32(), Kills = r.ReadInt32(), LastInput = r.ReadInt32(),
                        Hp = Float(r), Stamina = Float(r), Yaw = Float(r), Position = Vec(r), Velocity = Vec(r), RollDirection = Vec(r),
                        AttackAt = Float(r), AttackUntil = Float(r), BlockUntil = Float(r), BlockReady = Float(r), RollAt = Float(r), RollReady = Float(r),
                        StunUntil = Float(r), FlashUntil = Float(r), RespawnAt = Float(r), Sprinting = r.ReadBoolean() };
                    if (!seen.Add(a.Id) || a.Id < 1 || a.Hp < 0 || a.Hp > 100 || a.Stamina < 0 || a.Stamina > 100 || a.Combo < 0 || a.Combo > 2) throw new InvalidDataException("Invalid actor");
                    s.Actors[n] = a;
                }
                s.Covers = new Cover[Count(r, 64)];
                for (int n = 0; n < s.Covers.Length; n++) s.Covers[n] = new Cover { Id = r.ReadInt32(), Position = Vec(r), HalfX = Positive(r), HalfZ = Positive(r), Height = Positive(r), Hp = Float(r) };
                s.Shots = new Shot[Count(r, Rules.MaxProjectiles)];
                for (int n = 0; n < s.Shots.Length; n++) s.Shots[n] = new Shot { Id = r.ReadInt32(), Owner = r.ReadInt32(), Team = r.ReadInt32(), Reflections = r.ReadInt32(), Position = Vec(r), Velocity = Vec(r), Damage = Float(r), Life = Float(r) };
                s.Events = new ArenaEvent[Count(r, 4096)];
                for (int n = 0; n < s.Events.Length; n++)
                {
                    var e = new ArenaEvent { Id = r.ReadInt32(), Kind = (EventKind)r.ReadByte(), Source = r.ReadInt32(), Target = r.ReadInt32(), Position = Vec(r), Amount = Float(r) };
                    if (e.Kind > EventKind.Break || e.Id < 1) throw new InvalidDataException("Invalid event"); s.Events[n] = e;
                }
                End(r); return s;
            }
        }
        private static BinaryReader Reader(byte[] data, byte kind)
        {
            if (data == null || data.Length < 5 || data.Length > MaxPacket) throw new InvalidDataException("Invalid packet size");
            var r = new BinaryReader(new MemoryStream(data, false));
            if (r.ReadInt32() != Magic || r.ReadByte() != kind) { r.Dispose(); throw new InvalidDataException("Wrong prototype protocol"); } return r;
        }
        private static void End(BinaryReader r) { if (r.BaseStream.Position != r.BaseStream.Length) throw new InvalidDataException("Trailing packet data"); }
        private static int Count(BinaryReader r, int max) { int n = r.ReadInt32(); if (n < 0 || n > max) throw new InvalidDataException("Invalid collection size"); return n; }
        private static float Float(BinaryReader r) { float v = r.ReadSingle(); if (!MathUtil.Finite(v) || Math.Abs(v) > 10000000) throw new InvalidDataException("Invalid number"); return v; }
        private static float Positive(BinaryReader r) { float v = Float(r); if (v <= 0 || v > 100) throw new InvalidDataException("Invalid size"); return v; }
        private static void Vec(BinaryWriter w, V3 v) { w.Write(v.x); w.Write(v.y); w.Write(v.z); }
        private static V3 Vec(BinaryReader r) { return new V3(Float(r), Float(r), Float(r)); }
    }
}
