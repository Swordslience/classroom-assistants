using System;
using System.Diagnostics;
using System.IO;
namespace ScienceSupport {
 internal static class CloudUpdateLauncher {
  public static void Open(string product,string current,string folder,bool quiet=false){try{string source=Path.Combine(folder,"课堂助教更新.exe");if(!File.Exists(source))throw new FileNotFoundException("缺少云更新组件，请完整解压安装包");string cache=Path.Combine(folder,".update-cache");Directory.CreateDirectory(cache);if(quiet){string mark=Path.Combine(cache,"last-check.txt");if(File.Exists(mark)&&(DateTime.UtcNow-File.GetLastWriteTimeUtc(mark)).TotalHours<24)return;File.WriteAllText(mark,DateTime.UtcNow.ToString("O"));}string copy=Path.Combine(cache,"updater-"+Guid.NewGuid().ToString("N")+".exe");foreach(var old in Directory.GetFiles(cache,"updater-*.exe"))if(File.GetLastWriteTimeUtc(old)<DateTime.UtcNow.AddDays(-2))try{File.Delete(old);}catch{}File.Copy(source,copy);string args="--product "+product+" --current "+current+" --target \""+folder.TrimEnd('\\')+"\" --pid "+Process.GetCurrentProcess().Id+(quiet?" --quiet":"");Process.Start(new ProcessStartInfo(copy,args){UseShellExecute=true,WorkingDirectory=folder});}catch{if(!quiet)throw;}}
 }
}
