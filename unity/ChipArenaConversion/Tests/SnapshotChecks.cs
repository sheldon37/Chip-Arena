using System;
using System.Collections.Generic;
using ChipArena.UnityConversion;

internal static class SnapshotChecks
{
    private static int sink;
    public static void Run()
    {
        void Check(bool ok, string why) { if (!ok) throw new Exception("Chip snapshots: " + why); }
        var source = new List<int> { 1, 2, 3 };
        var visited = new List<int>();
        foreach (int item in NativeSnapshots.Capture(source))
        {
            visited.Add(item);
            if (item == 1) { source.Clear(); source.Add(4); }
        }
        Check(string.Join(",", visited) == "1,2,3", "removals and spawns must not alter current snapshot");
        visited.Clear();
        foreach (int item in NativeSnapshots.Capture(source)) visited.Add(item);
        Check(string.Join(",", visited) == "4", "spawn appears on next enumeration");
        source = new List<int> { 1, 2, 3 };
        int total = 0;
        foreach (int outer in NativeSnapshots.Capture(source))
        {
            foreach (int inner in NativeSnapshots.Capture(source)) total += outer * inner;
        }
        Check(total == 36, "nested same-type blasts must own independent buffers");
        foreach (int item in NativeSnapshots.Capture(new List<int>())) throw new Exception("Empty snapshot yielded an item");
        var bodies = new List<object>();
        for (int i = 0; i < 300; i++) bodies.Add(new object());
        // Warm the pool, including its exceptional and early-exit disposal paths.
        for (int i = 0; i < 20; i++) Exercise(bodies);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) Exercise(bodies);
        long pooled = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(pooled == 0, "warmed mutation-safe loops allocate " + pooled + " bytes");
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) foreach (var body in bodies.ToArray()) if (body != null) sink++;
        long original = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(original > 2000000, "baseline allocation measurement");
        // Dispose also runs when a gameplay callback throws; the next loop remains valid.
        try { foreach (var body in NativeSnapshots.Capture(bodies)) throw new InvalidOperationException(); }
        catch (InvalidOperationException) { }
        int seen = 0;
        foreach (var body in NativeSnapshots.Capture(bodies)) { Check(ReferenceEquals(body, bodies[seen]), "reference/order changed"); seen++; }
        Check(seen == 300, "pool capacity must not leak extra entries");
        Console.WriteLine("PASS chip snapshots: mutation, deferred spawns, nested blasts, empty/early/exception paths; 1000 x 300-item copies " + original + " -> " + pooled + " allocated bytes (managed microbenchmark, not game FPS)");
    }

    private static void Exercise(List<object> bodies)
    {
        foreach (var body in NativeSnapshots.Capture(bodies)) if (body != null) sink++;
        foreach (var outer in NativeSnapshots.Capture(bodies))
        {
            foreach (var inner in NativeSnapshots.Capture(bodies)) { if (inner != null) sink++; break; }
            break;
        }
    }
}
