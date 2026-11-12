using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// A startup bank decoded off the main thread while Awake goes on and the logo intro plays (NativeSourceMotion,
    /// NativeSceneMotion, NativeSourceModels' JSON; docs/BOOT-INTRO.md). The main thread only loads the resource and copies its
    /// bytes (a few milliseconds); the envelope, base64, gzip, the JSON and the bank's own checks run on a worker (all of it plain
    /// C#: JsonUtility may run off the main thread, and the build touches no Unity object). Join waits for it and rethrows its
    /// failure where the synchronous load threw, so whoever reads the bank first reads it complete or not at all.
    /// </summary>
    internal sealed class NativeBankLoad<T> where T:class
    {
        public readonly string Resource;
        private readonly Task<T> task;
        /// <summary>What the build had started to fill when it failed (null if it failed before), as the synchronous load left it.</summary>
        public T Partial{get{lock(this)return partial;}}
        private T partial;
        /// <summary>Main thread: loads the resource and starts build(json, begin) on a worker; begin is called with the bank it
        /// fills, before it fills it.</summary>
        public NativeBankLoad(string resource,Func<string,Action<T>,T> build)
        {
            Resource=resource;var asset=Resources.Load<TextAsset>(resource);
            if(asset==null){task=Task.FromException<T>(new InvalidOperationException(resource+" is missing"));return;}
            byte[] raw=asset.bytes;
            task=Task.Run(()=>build(NativeSourceModels.Unwrap(resource,Text(raw)),Begin));
        }
        /// <summary>The worker has finished (or failed): Join no longer waits.</summary>
        public bool Done=>task.IsCompleted;
        private void Begin(T filling){lock(this)partial=filling;}
        // TextAsset.text reads the file as UTF-8 (a byte order mark skipped); the same off the main thread.
        private static string Text(byte[] raw){using(var reader=new StreamReader(new MemoryStream(raw,false),System.Text.Encoding.UTF8,true))return reader.ReadToEnd();}
        /// <summary>Main thread: the decoded bank, waiting for the worker if it has not finished; its failure is rethrown here.</summary>
        public T Join()=>task.GetAwaiter().GetResult();
    }
}
