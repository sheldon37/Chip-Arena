# Shared Editor command wrapper

From a checkout, run:

```
python tools/editor_lock.py --root "F:/Chip Arena Development" --who Astra --task "native checks" -- COMMAND ARGUMENTS
python tools/editor_lock.py --root "F:/Chip Arena Development" --status
```

The wrapper acquires the existing `.editor-lock` directory and writes `holder.json`
with agent, task, UTC acquisition time, hostname and PID. It holds the lock until
the command exits. Commands must wait for their Editor work to complete; do not
launch a detached job then return. All agents need to use this wrapper for FIFO
ordering; legacy mkdir callers still respect exclusion but can bypass the queue.

Tickets live in `.editor-queue`, ordered by enqueue time then ticket name. To
promote a known ticket, place its exact printed filename in `.editor-priority`.
An absent or no-longer-waiting priority ticket is ignored. `--timeout SECONDS`
bounds a wait (exit 124). A waiter whose parent dies exits 125 and removes its
ticket; `--parent-pid PID` can identify the actual agent when a persistent shell
is the immediate parent. The command is terminated if that parent exits too.

`--reap` permits recovery only when holder.json identifies a provably dead process
on this host. Age alone is never evidence. Missing/invalid metadata, remote hosts,
access-denied process probes and reused live PIDs are conservative: leave the lock
alone and inspect it. A crash between mkdir and writing holder.json leaves an
unknown lock requiring inspection. Old orphan polling scripts are not automatically
killed. This tool was tested with real subprocesses on Linux; Windows process
probing and the owner's shared Editor remain local acceptance checks.
