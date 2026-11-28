using System;
using System.Threading;
namespace ChipArena.UnityConversion
{
    public sealed class NativeSingleInstance:IDisposable
    {
        private readonly Mutex mutex;public bool Acquired{get;private set;}
        public NativeSingleInstance(string name){mutex=new Mutex(false,name);try{Acquired=mutex.WaitOne(0);}catch(AbandonedMutexException){Acquired=true;}}
        public void Dispose(){if(Acquired){Acquired=false;mutex.ReleaseMutex();}mutex.Dispose();}
    }
}
