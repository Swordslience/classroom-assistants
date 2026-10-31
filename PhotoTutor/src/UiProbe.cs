using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using WF=System.Windows.Forms;
namespace PhotoTutor {
 internal sealed class UiProbe:WF.IMessageFilter {
  readonly Dictionary<int,int> counts=new Dictionary<int,int>();readonly WF.Timer timer=new WF.Timer{Interval=2000};readonly Stopwatch clock=Stopwatch.StartNew();double cpu=Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds;int sample;
  readonly Dictionary<string,int> windowCounts=new Dictionary<string,int>();
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr hwnd,StringBuilder name,int count);
  public UiProbe(){WF.Application.AddMessageFilter(this);timer.Tick+=(_,__)=>{
   var p=Process.GetCurrentProcess();double now=p.TotalProcessorTime.TotalMilliseconds;
   var result=new{time=DateTime.Now,sample=++sample,cpuCorePercent=(now-cpu)/clock.Elapsed.TotalMilliseconds*100,workingSetMB=p.WorkingSet64/1048576.0,messages=counts.OrderByDescending(x=>x.Value).Take(10).Select(x=>new{id="0x"+x.Key.ToString("X"),count=x.Value}).ToArray(),windows=windowCounts.OrderByDescending(x=>x.Value).Take(8).ToArray()};
   try{File.WriteAllText(Path.Combine(Settings.DataRoot,"ui-probe.json"),JsonConvert.SerializeObject(result));}catch{ }cpu=now;clock.Restart();counts.Clear();windowCounts.Clear();if(sample>=8){timer.Stop();WF.Application.RemoveMessageFilter(this);timer.Dispose();}
  };timer.Start();}
  public bool PreFilterMessage(ref WF.Message m){int count;counts.TryGetValue(m.Msg,out count);counts[m.Msg]=count+1;if(m.Msg==0x400||m.Msg==0x406){var name=new StringBuilder(256);GetClassName(m.HWnd,name,name.Capacity);string key="0x"+m.Msg.ToString("X")+" "+name;windowCounts.TryGetValue(key,out count);windowCounts[key]=count+1;}return false;}
 }
}
