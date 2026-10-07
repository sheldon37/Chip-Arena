#if UNITY_EDITOR || ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ChipArena.UnityConversion
{
    public static class RegressionTests
    {
        public static List<string> Run()
        {
            var passed = new List<string>();
            Check(passed, "Movement normalized and arena bounded", () => {
                var w = new ArenaWorld(); w.Covers.Clear();
                for (int i = 0; i < 600; i++) w.Step(new InputFrame { X = 100, Z = 100 }, new InputFrame());
                var a = w.Find(1); Assert(a.Position.x <= 23.581f && a.Position.z <= 23.581f, "bounds");
                Assert(new V3(a.Velocity.x, 0, a.Velocity.z).Length <= Rules.Speed + .001f, "diagonal speed");
            });
            Check(passed, "Cover blocks movement and shots", () => {
                var w = Duel(); w.Covers.Add(new Cover { Id = 1, Position = new V3(0, 0, 1), HalfX = 1, HalfZ = .2f, Height = 3, Hp = 85 });
                for (int i = 0; i < 30; i++) w.Step(new InputFrame { Z = 1 }, new InputFrame());
                Assert(w.Find(1).Position.z < .59f, "walked through cover");
                w.Step(new InputFrame { Shoot = true }, new InputFrame()); Advance(w, 15);
                Assert(w.Find(2).Hp == 100 && w.Covers[0].Hp < 85, "shot crossed cover");
            });
            Check(passed, "Sword deals one hit per activation", () => {
                var w = Duel(); w.Step(new InputFrame { Attack = true }, new InputFrame()); Advance(w, 35);
                Assert(w.Find(2).Hp == 70, "repeat damage"); Assert(w.Find(1).Stamina == 86, "cost");
            });
            Check(passed, "Defense is resolved before melee contact", () => {
                var w = Duel(); w.Step(new InputFrame { Attack = true }, new InputFrame()); Advance(w, 7);
                w.Step(new InputFrame(), new InputFrame { Block = true }); Advance(w, 10);
                Assert(w.Find(2).Hp == 100, "parry failed"); Assert(w.Find(1).StunUntil > w.Time, "attacker not stunned");
            });
            Check(passed, "Block cooldown prevents repeated activation", () => {
                var w = Duel(); w.Step(new InputFrame { Block = true }, new InputFrame()); float until = w.Find(1).BlockUntil;
                Advance(w, 24); w.Step(new InputFrame { Block = true }, new InputFrame()); Assert(w.Find(1).BlockUntil == until, "block refreshed");
            });
            Check(passed, "Roll cancels the pending melee hit", () => {
                var w = Duel(); w.Step(new InputFrame { Attack = true }, new InputFrame());
                w.Step(new InputFrame { Roll = true, X = 1 }, new InputFrame()); Advance(w, 20);
                Assert(w.Find(2).Hp == 100, "cancelled attack hit"); Assert(!w.Find(1).Attacking(w.Time), "attack retained");
            });
            Check(passed, "Dodge has bounded invulnerability", () => {
                var a = new Actor { RollAt = 1 };
                Assert(!a.Invulnerable(1.05f) && a.Invulnerable(1.1f) && !a.Invulnerable(1.30f), "iframe window");
            });
            Check(passed, "Insufficient stamina rejects attacks and rolls", () => {
                var w = Duel(); w.Find(1).Stamina = 1;
                w.Step(new InputFrame { Attack = true, Roll = true }, new InputFrame());
                Assert(!w.Find(1).Attacking(w.Time) && !w.Find(1).Rolling(w.Time), "free action");
            });
            Check(passed, "Buffered combo advances once", () => {
                var w = Duel(); w.Step(new InputFrame { Attack = true }, new InputFrame());
                Advance(w, 20); w.Step(new InputFrame { Attack = true }, new InputFrame()); Advance(w, 23);
                Assert(w.Find(1).Action == 2 && w.Find(1).Combo == 1, "buffer"); Advance(w, 60);
                Assert(w.Find(1).Action == 2, "repeated buffer");
            });
            Check(passed, "Sprint drains and exhaustion stays latched", () => {
                var w = Duel(); w.Find(1).Stamina = .1f;
                w.Step(new InputFrame { Z = 1, Sprint = true }, new InputFrame());
                Assert(w.Find(1).Stamina == 0 && w.Find(1).SprintExhausted, "drain");
                for (int i = 0; i < 60; i++) w.Step(new InputFrame { Z = 1, Sprint = true }, new InputFrame());
                Assert(!w.Find(1).Sprinting, "exhaustion bypass");
                w.Step(new InputFrame(), new InputFrame()); Assert(!w.Find(1).SprintExhausted, "release reset");
            });
            Check(passed, "Jump returns to ground", () => {
                var w = Duel(); w.Step(new InputFrame { Jump = true }, new InputFrame()); Advance(w, 10);
                Assert(w.Find(1).Position.y > 0, "no jump"); Advance(w, 90); Assert(w.Find(1).Position.y == 0, "no landing");
            });
            Check(passed, "Projectile reflection transfers owner", () => {
                var w = Duel(); w.Step(new InputFrame { Shoot = true }, new InputFrame { Block = true }); Advance(w, 1);
                Assert(w.Find(2).Hp == 100, "block damage"); Assert(w.Shots.Count == 1 && w.Shots[0].Owner == 2 && w.Shots[0].Velocity.z < 0, "reflection");
                Advance(w, 15); Assert(w.Find(1).Hp == 80, "return hit");
            });
            Check(passed, "Swept collision catches fast projectiles", () => {
                var w = Duel(); w.Shots.Add(new Shot { Id = 1, Owner = 1, Team = 1, Position = new V3(0, .9f, .5f), Velocity = new V3(0, 0, 500) });
                w.Step(new InputFrame(), new InputFrame()); Assert(w.Find(2).Hp == 80, "tunnelling");
            });
            Check(passed, "Destroyed cover emits break and stops blocking", () => {
                var w = Duel(); w.Covers.Add(new Cover { Id = 1, Position = new V3(0, 0, 1), Hp = 10 });
                w.Step(new InputFrame { Attack = true }, new InputFrame()); Advance(w, 15);
                Assert(w.Covers[0].Hp == 0 && w.Events.Exists(e => e.Kind == EventKind.Break), "no break");
            });
            Check(passed, "Death and respawn preserve score", () => {
                var w = Duel(); w.Find(2).Hp = 20;
                w.Step(new InputFrame { Attack = true }, new InputFrame()); Advance(w, 20);
                Assert(!w.Find(2).Alive && w.Find(1).Kills == 1, "death"); Advance(w, 190);
                Assert(w.Find(2).Alive && w.Find(1).Kills == 1, "respawn");
            });
            Check(passed, "Invalid input cannot poison world", () => {
                var w = Duel(); w.Step(new InputFrame { X = float.NaN, Yaw = float.PositiveInfinity, Attack = true }, new InputFrame());
                Assert(MathUtil.Finite(w.Find(1).Position.x) && !w.Find(1).Attacking(w.Time), "invalid input");
            });
            Check(passed, "Snapshot round trip includes combat and events", () => {
                var w = Duel(); w.Step(new InputFrame { Attack = true }, new InputFrame()); Advance(w, 12);
                var s = WireCodec.ReadState(WireCodec.State(w.Capture()));
                Assert(s.Tick == w.Tick && s.Actors[1].Hp == w.Find(2).Hp && s.Events.Length > 0, "snapshot");
                Assert(s.Actors[0].AttackAt == w.Find(1).AttackAt && s.Actors[0].Action == 1, "animation state");
            });
            Check(passed, "Protocol rejects truncated and oversized collections", () => {
                byte[] data = WireCodec.State(Duel().Capture()); Array.Resize(ref data, data.Length - 1);
                Reject(() => WireCodec.ReadState(data));
                data = WireCodec.State(Duel().Capture()); byte[] huge = BitConverter.GetBytes(int.MaxValue); Array.Copy(huge, 0, data, 9, 4);
                Reject(() => WireCodec.ReadState(data)); Reject(() => WireCodec.ReadState(new byte[WireCodec.MaxPacket + 1]));
            });
            Check(passed, "Input flags and normalization survive wire", () => {
                var i = WireCodec.ReadInput(WireCodec.Input(new InputFrame { Sequence = 10, X = 9, Z = 9, Attack = true, Roll = true, Sprint = true }));
                Assert(i.Sequence == 10 && i.Attack && i.Roll && i.Sprint && new V3(i.X, 0, i.Z).Length <= 1.001f, "input wire");
                Reject(() => WireCodec.ReadInput(WireCodec.Input(new InputFrame { X = float.NaN })));
            });
            Check(passed, "192-bot workload stays finite and serializable", () => {
                var w = new ArenaWorld(); w.SetBots(192);
                for (int i = 0; i < 600; i++) w.Step(new InputFrame { Shoot = true, Yaw = i }, new InputFrame());
                foreach (Actor a in w.Actors) Assert(MathUtil.Finite(a.Position.x) && MathUtil.Finite(a.Stamina), "nonfinite state");
                var s = WireCodec.ReadState(WireCodec.State(w.Capture())); Assert(s.Actors.Length == 193, "actor count");
            });
            Check(passed, "Bot reset cannot remove human guest", () => {
                var w = Duel(); w.SetBots(192); w.SetBots(0); Assert(w.Actors.Count == 2 && w.Find(2) != null, "guest removed");
            });
            Check(passed, "LAN exchanges actual input and snapshot frames", () => {
                int port; var reserve = new TcpListener(IPAddress.Loopback, 0); reserve.Start(); port = ((IPEndPoint)reserve.LocalEndpoint).Port; reserve.Stop();
                using (var host = LanLink.Host(port, true)) using (var guest = LanLink.Join("127.0.0.1", port))
                {
                    Wait(() => host.Connected && guest.Connected, "connect");
                    guest.Send(WireCodec.Input(new InputFrame { Sequence = 1, Attack = true }));
                    byte[] input = null; Wait(() => (input = host.Receive()) != null, "input receive");
                    var w = Duel(); w.Step(new InputFrame(), WireCodec.ReadInput(input));
                    host.Send(WireCodec.State(w.Capture())); byte[] state = null;
                    Wait(() => (state = guest.Receive()) != null, "snapshot receive");
                    Assert(WireCodec.ReadState(state).Actors[1].Action == 1, "remote attack");
                    guest.Dispose(); Wait(() => !host.Connected, "disconnect");
                }
            });
            return passed;
        }
        private static ArenaWorld Duel() { var w = new ArenaWorld(); w.Covers.Clear(); w.AddHuman(2); w.Find(1).Position = new V3(); w.Find(2).Position = new V3(0, 0, 2); return w; }
        private static void Advance(ArenaWorld w, int ticks) { for (int i = 0; i < ticks; i++) w.Step(new InputFrame(), new InputFrame()); }
        private static void Check(List<string> passed, string name, Action test) { try { test(); passed.Add(name); } catch (Exception e) { throw new Exception(name + ": " + e.Message, e); } }
        private static void Assert(bool ok, string message) { if (!ok) throw new Exception(message); }
        private static void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } catch (IOException) { return; } throw new Exception("Accepted malformed packet"); }
        private static void Wait(Func<bool> condition, string label) { var until = DateTime.UtcNow.AddSeconds(4); while (!condition()) { if (DateTime.UtcNow > until) throw new Exception("Timeout: " + label); Thread.Sleep(5); } }
    }
}
#endif
