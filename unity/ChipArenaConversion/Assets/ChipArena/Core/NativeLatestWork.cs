using System;
using System.Threading.Tasks;
namespace ChipArena.UnityConversion
{
    // Main-thread owner, one worker at a time. Rerolls replace queued work; reset invalidates an in-flight result.
    public sealed class NativeLatestWork<T>
    {
        private Task<T> active;private Func<T> queued;private long version,activeVersion;
        public bool Busy=>active!=null||queued!=null;
        public void Request(Func<T> build){queued=build??throw new ArgumentNullException(nameof(build));version++;Start();}
        public void Reset(){queued=null;version++;}
        private void Start(){if(active!=null||queued==null)return;var build=queued;queued=null;activeVersion=version;active=Task.Run(build);}
        public bool Poll(out T result,out Exception error)
        {
            result=default;error=null;if(active==null||!active.IsCompleted)return false;
            bool current=activeVersion==version;
            if(active.IsFaulted){if(current)error=active.Exception.GetBaseException();else _=active.Exception;}
            else if(active.IsCanceled){if(current)error=new OperationCanceledException();}
            else if(current)result=active.Result;
            active=null;Start();return current;
        }
    }
}
