using System;
using System.IO;
namespace ChipArena.UnityConversion
{
    public static class NativeFileStore
    {
        public static T Read<T>(string path,Func<string,T> parse,Func<T> defaults,out string warning) where T:class
        {
            warning=null;if(!File.Exists(path))return defaults();
            try{return parse(File.ReadAllText(path))??throw new InvalidDataException("Empty preferences");}
            catch(Exception e)when(!(e is OutOfMemoryException)){warning="Saved preferences could not be read; defaults loaded. The original file is preserved.";try{File.Copy(path,path+".corrupt",true);}catch(Exception backup)when(backup is IOException||backup is UnauthorizedAccessException){}return defaults();}
        }
        public static void Write(string path,string value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));string temp=path+".new";
            try{File.WriteAllText(temp,value);if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}
        }
    }
}
