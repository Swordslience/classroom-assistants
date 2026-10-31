using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace PhotoTutor {
 // Diagnostic metadata only: never persist photographs, OCR text, questions, keys or exception messages.
 internal static class SessionDiagnostics {
  static readonly object gate=new object();
  internal static void Record(string stage,object? detail=null,Exception? error=null){try{lock(gate){string folder=Path.Combine(Settings.DataRoot,"diagnostics");Directory.CreateDirectory(folder);string file=Path.Combine(folder,"runtime.jsonl");if(File.Exists(file)&&new FileInfo(file).Length>1024*1024){string old=file+".1";if(File.Exists(old))File.Delete(old);File.Move(file,old);}var methods=error==null?null:new StackTrace(error,false).GetFrames()?.Take(6).Select(f=>f.GetMethod()?.DeclaringType?.FullName+"."+f.GetMethod()?.Name).ToArray();File.AppendAllText(file,JsonConvert.SerializeObject(new{utc=DateTime.UtcNow.ToString("o"),version=typeof(Entry).Assembly.GetName().Version.ToString(),stage,pid=Process.GetCurrentProcess().Id,detail,errorType=error?.GetType().FullName,hresult=error?.HResult,methods})+Environment.NewLine,Encoding.UTF8);}}catch{} }
  internal static void Register(){AppDomain.CurrentDomain.UnhandledException+=(_,e)=>Record("unhandled",new{terminating=e.IsTerminating},e.ExceptionObject as Exception);System.Threading.Tasks.TaskScheduler.UnobservedTaskException+=(_,e)=>{Record("unobserved-task",null,e.Exception);e.SetObserved();};}
 }
}
