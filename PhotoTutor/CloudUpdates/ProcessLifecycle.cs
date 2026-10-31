using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
namespace ClassroomUpdater {
 internal static class ProcessLifecycle {
  internal static async Task WaitForOriginalExit(string pid,string expected,int waitMs=15000){if(!int.TryParse(pid,out int id))return;try{using(var process=Process.GetProcessById(id)){string actual=null;for(int attempt=0;attempt<10;attempt++){if(process.HasExited)return;try{actual=process.MainModule?.FileName;}catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}catch(NullReferenceException){}if(!string.IsNullOrEmpty(actual))break;await Task.Delay(50).ConfigureAwait(false);}if(!string.Equals(actual,expected,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("目标程序身份无法核验或不匹配，未关闭或替换任何程序。");process.CloseMainWindow();if(!await Task.Run(()=>process.WaitForExit(waitMs)).ConfigureAwait(false))throw new IOException("原程序尚未退出，未替换文件，请关闭程序后重试。");}}catch(ArgumentException){}}
  internal static async Task<Process> StartAndCheck(string file,string arguments=""){var process=Process.Start(new ProcessStartInfo(file,arguments){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(file)});if(process==null)throw new IOException("文件已更新，但没有取得新进程，请手动启动。");await Task.Delay(200).ConfigureAwait(false);if(process.HasExited){process.Dispose();throw new IOException("文件已更新，但新进程已退出，请手动启动并核对版本。");}return process;}
 }
}
