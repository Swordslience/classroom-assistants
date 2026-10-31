using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PhotoTutor {
 internal static class ProductIdentity {
  internal const string Name="深度求索",Version="1.6.4";
  internal static void MigrateDesktopName(string? desktopDirectory=null,string? executable=null){
   object? shell=null,oldLink=null,newLink=null;string? temporary=null;
   try {
    string desktop=desktopDirectory??Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),old=Path.Combine(desktop,"拍照解题助教.lnk"),next=Path.Combine(desktop,Name+".lnk"),exe=executable??typeof(Entry).Assembly.Location;
    if(!File.Exists(old))return;
    shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);dynamic ws=shell!;oldLink=ws.CreateShortcut(old);
    if(!string.Equals(Path.GetFullPath((string)((dynamic)oldLink).TargetPath),Path.GetFullPath(exe),StringComparison.OrdinalIgnoreCase))return;
    if(File.Exists(next)){newLink=ws.CreateShortcut(next);if(!string.Equals((string)((dynamic)newLink).TargetPath,exe,StringComparison.OrdinalIgnoreCase))return;}
    else {temporary=Path.Combine(desktop,"深度求索_"+Guid.NewGuid().ToString("N")+".lnk");newLink=ws.CreateShortcut(temporary);dynamic link=newLink;link.TargetPath=exe;link.WorkingDirectory=Path.GetDirectoryName(exe);link.IconLocation=exe+",0";link.Description=Name+" "+Version;link.Save();File.Move(temporary,next);temporary=null;}
    File.Delete(old);
   }catch(Exception error){SessionDiagnostics.Record("desktop-name-preserved",null,error);}
   finally{if(temporary!=null)try{File.Delete(temporary);}catch{}foreach(var value in new[]{newLink,oldLink,shell})if(value!=null&&Marshal.IsComObject(value))try{Marshal.FinalReleaseComObject(value);}catch{}}
  }
 }
}
