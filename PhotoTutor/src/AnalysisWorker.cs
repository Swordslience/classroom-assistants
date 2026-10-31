using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json.Linq;

namespace PhotoTutor {
 // WinRT OCR can fail in native code. Its process must never own the camera, UI or the sole copy of a page.
 internal static class AnalysisWorker {
  static readonly SemaphoreSlim single=new SemaphoreSlim(1,1);
  internal static string? FaultForTest;
  internal static int TimeoutForTest;
  internal static int ActiveWorkers,LastWorkerPid;
  internal static async Task<CandidateResult> Detect(QuestionPage page,CancellationToken token){
   var clock=Stopwatch.StartNew();await single.WaitAsync(token).ConfigureAwait(false);clock.Restart();string folder=Path.Combine(Path.GetTempPath(),"PhotoTutor-analysis-"+Guid.NewGuid().ToString("N"));Process? process=null;WorkerJob? job=null;
   try{
    token.ThrowIfCancellationRequested();Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"owned.cache"),"PhotoTutor analysis v1");
    File.WriteAllText(Path.Combine(folder,"request.json"),new JObject{["file"]=page.File,["width"]=page.Width,["height"]=page.Height,["fault"]=FaultForTest}.ToString(),Encoding.UTF8);
    string fallback=typeof(Entry).Assembly.Location,executable=LocalOcrRuntime.Resolve()??fallback;bool bundled=executable!=fallback;
    for(int attempt=0;attempt<2;attempt++){
    try{process=Process.Start(new ProcessStartInfo(executable,"--analysis-worker \""+folder+"\""){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Path.GetDirectoryName(executable)});}catch(System.ComponentModel.Win32Exception error)when(bundled&&FaultForTest==null&&attempt==0){SessionDiagnostics.Record("local-ocr-start-fallback",null,error);LocalOcrRuntime.MarkUnavailable();bundled=false;executable=fallback;continue;}
    if(process==null)throw new IOException("无法启动本地识别进程");LastWorkerPid=process.Id;Interlocked.Increment(ref ActiveWorkers);job=new WorkerJob(process);
    try{process.PriorityClass=ProcessPriorityClass.BelowNormal;}catch{}
    SessionDiagnostics.Record("analysis-start",new{page.Width,page.Height,worker=process.Id,job=job.Attached});
    int limit=TimeoutForTest>0?TimeoutForTest:8500;bool timeout=false,memory=false;
    while(!process.HasExited){token.ThrowIfCancellationRequested();if(clock.ElapsedMilliseconds>=limit){timeout=true;break;}try{if(process.PrivateMemorySize64>480L*1024*1024){memory=true;break;}}catch(InvalidOperationException){}await Task.Delay(50,token).ConfigureAwait(false);}
    token.ThrowIfCancellationRequested();
    if(timeout||memory){Kill(process);SessionDiagnostics.Record("analysis-stopped",new{timeout,memory,ms=clock.ElapsedMilliseconds});return Failure(timeout?"自动识别等待过久，已停止本次识别。原图和选框仍在，可直接拖框选题或重新识别。":"本次识别占用内存过高，已停止识别。原图和选框仍在，可直接拖框选题。",clock);}
    if(process.ExitCode!=0){SessionDiagnostics.Record("analysis-worker-exit",new{exitCode=process.ExitCode,ms=clock.ElapsedMilliseconds,bundled});if(bundled&&FaultForTest==null&&attempt==0){LocalOcrRuntime.MarkUnavailable();Kill(process);process.Dispose();process=null;Interlocked.Decrement(ref ActiveWorkers);job.Dispose();job=null;bundled=false;executable=fallback;SessionDiagnostics.Record("analysis-system-fallback");continue;}return Failure("自动识别进程异常退出，选题窗口和原图已保留；可继续手动框选或点“重新识别题框”。",clock);}
    string output=Path.Combine(folder,"result.json");if(!File.Exists(output)||new FileInfo(output).Length>2*1024*1024)throw new InvalidDataException("识别结果缺失或异常");var result=Decode(JObject.Parse(File.ReadAllText(output)),page);result.Seconds=clock.Elapsed.TotalSeconds;
    SessionDiagnostics.Record("analysis-complete",new{ms=clock.ElapsedMilliseconds,count=result.Regions.Count,result.OcrAvailable,result.Language,result.OcrPasses,result.Rotation,result.DetailHeight});return result;
    }
    throw new IOException("本地识别未完成");
   }catch(OperationCanceledException){SessionDiagnostics.Record("analysis-cancel",new{ms=clock.ElapsedMilliseconds});throw;}
   catch(Exception error){SessionDiagnostics.Record("analysis-error",new{ms=clock.ElapsedMilliseconds},error);return Failure("本地自动识别未完成，原图和已选区域保留，可继续手动框选。",clock);}
   finally{if(process!=null){Kill(process);process.Dispose();Interlocked.Decrement(ref ActiveWorkers);}job?.Dispose();try{if(Directory.Exists(folder)&&File.ReadAllText(Path.Combine(folder,"owned.cache"))=="PhotoTutor analysis v1")Directory.Delete(folder,true);}catch{}single.Release();}
  }
  static CandidateResult Failure(string message,Stopwatch clock)=>new CandidateResult{Message=message,Seconds=clock.Elapsed.TotalSeconds};
  static void Kill(Process process){try{if(!process.HasExited){process.Kill();process.WaitForExit(1500);}}catch{} }
  internal static int Run(string folder){
   try{string full=Path.GetFullPath(folder);Guid id;if(Path.GetDirectoryName(full)!=Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar)||!Path.GetFileName(full).StartsWith("PhotoTutor-analysis-")||!Guid.TryParseExact(Path.GetFileName(full).Substring(20),"N",out id)||File.ReadAllText(Path.Combine(full,"owned.cache"))!="PhotoTutor analysis v1")return 4;
    string request=Path.Combine(full,"request.json");if(new FileInfo(request).Length>65536)return 5;var json=JObject.Parse(File.ReadAllText(request));string? fault=(string?)json["fault"];
    if(fault=="hang"){Thread.Sleep(30000);return 8;}if(fault=="crash")return 77;if(fault=="invalid"){File.WriteAllText(Path.Combine(full,"result.json"),"invalid");return 0;}
    var page=new QuestionPage{File=(string)json["file"]!,Width=(int)json["width"]!,Height=(int)json["height"]!};if(page.Width<32||page.Height<32||(long)page.Width*page.Height>24000000)return 6;
    var result=Task.Run(()=>CandidateDetector.DetectCore(page,CancellationToken.None)).GetAwaiter().GetResult();File.WriteAllText(Path.Combine(full,"result.json"),Encode(result).ToString(),Encoding.UTF8);return 0;
   }catch{return 9;}
  }
  static JObject Encode(CandidateResult result)=>new JObject{["message"]=result.Message,["ocr"]=result.OcrAvailable,["rotation"]=result.Rotation,["detail"]=result.DetailHeight,["language"]=result.Language,["passes"]=result.OcrPasses,["privateBytes"]=result.PrivateBytes,["peak"]=result.PeakWorkingSetBytes,["lines"]=JArray.FromObject(result.AnalysisLines.Select(v=>new{v.Text,v.X,v.Y,v.Width,v.Height})),["regions"]=JArray.FromObject(result.Regions.Select(v=>new{x=v.Bounds.X,y=v.Bounds.Y,width=v.Bounds.Width,height=v.Bounds.Height,score=v.Confidence}))};
  static CandidateResult Decode(JObject json,QuestionPage page){var result=new CandidateResult{Message=(string?)json["message"]??"",OcrAvailable=(bool?)json["ocr"]??false,Rotation=(int?)json["rotation"]??0,DetailHeight=(double?)json["detail"]??0,Language=(string?)json["language"]??"",OcrPasses=(int?)json["passes"]??0,PrivateBytes=(long?)json["privateBytes"]??0,PeakWorkingSetBytes=(long?)json["peak"]??0};foreach(var line in json["lines"]??new JArray())result.AnalysisLines.Add(new OcrLine{Text=(string?)line["Text"]??"",X=(double)line["X"]!,Y=(double)line["Y"]!,Width=(double)line["Width"]!,Height=(double)line["Height"]!});var boxes=json["regions"]??new JArray();if(boxes.Count()>80)throw new InvalidDataException("候选框过多");foreach(var b in boxes){var r=new Int32Rect((int)b["x"]!,(int)b["y"]!,(int)b["width"]!,(int)b["height"]!);if(r.X<0||r.Y<0||r.Width<32||r.Height<32||(long)r.X+r.Width>page.Width||(long)r.Y+r.Height>page.Height)throw new InvalidDataException("题框超出原图");result.Regions.Add(new RegionCandidate{Bounds=r,Confidence=(double)b["score"]!});}return result;}
  sealed class WorkerJob:IDisposable {
   IntPtr handle;internal bool Attached;
   internal WorkerJob(Process process){try{handle=CreateJobObject(IntPtr.Zero,null);if(handle==IntPtr.Zero)return;var info=new Extended{BasicLimitInformation=new Basic{LimitFlags=0x2000|0x100},ProcessMemoryLimit=(UIntPtr)(512UL*1024*1024)};int size=Marshal.SizeOf(typeof(Extended));IntPtr data=Marshal.AllocHGlobal(size);try{Marshal.StructureToPtr(info,data,false);if(SetInformationJobObject(handle,9,data,(uint)size))Attached=AssignProcessToJobObject(handle,process.Handle);}finally{Marshal.FreeHGlobal(data);}}catch{} }
   public void Dispose(){if(handle!=IntPtr.Zero){CloseHandle(handle);handle=IntPtr.Zero;}}
   [StructLayout(LayoutKind.Sequential)]struct Basic{public long PerProcessUserTimeLimit,PerJobUserTimeLimit;public uint LimitFlags;public UIntPtr MinimumWorkingSetSize,MaximumWorkingSetSize;public uint ActiveProcessLimit;public UIntPtr Affinity;public uint PriorityClass,SchedulingClass;}
   [StructLayout(LayoutKind.Sequential)]struct Io{public ulong ReadOperationCount,WriteOperationCount,OtherOperationCount,ReadTransferCount,WriteTransferCount,OtherTransferCount;}
   [StructLayout(LayoutKind.Sequential)]struct Extended{public Basic BasicLimitInformation;public Io IoInfo;public UIntPtr ProcessMemoryLimit,JobMemoryLimit,PeakProcessMemoryUsed,PeakJobMemoryUsed;}
   [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr CreateJobObject(IntPtr attributes,string? name);
   [DllImport("kernel32.dll")]static extern bool SetInformationJobObject(IntPtr job,int info,IntPtr data,uint size);
   [DllImport("kernel32.dll")]static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
   [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
  }
 }
}
