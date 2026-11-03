#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Threading;

/// <summary>Progress reports remain readable while the editor writes the next result.</summary>
public static class RecoveryReport
{
    public static void WriteLines(string path,IEnumerable<string> lines)
    {
        for(int attempt=0;;attempt++)
        {
            try
            {
                using(var file=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.ReadWrite))
                using(var writer=new StreamWriter(file))foreach(var line in lines)writer.WriteLine(line);
                return;
            }
            catch(IOException)when(attempt<5){Thread.Sleep(10);}
        }
    }
}
#endif
