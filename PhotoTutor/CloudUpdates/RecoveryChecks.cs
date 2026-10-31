using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
namespace ClassroomUpdater {
 internal static class RecoveryChecks {
  internal static int Run(string report){
   string root=Path.Combine(Path.GetTempPath(),"classroom-recovery-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var rows=new List<object>();int failures=0;
   Action<string,Action> check=(name,test)=>{try{test();rows.Add(new{name,pass=true});}catch(Exception error){failures++;rows.Add(new{name,pass=false,error=error.ToString()});}};
   try {
    check("部分替换失败后恢复旧文件与接口配置",()=>{string target=Path.Combine(root,"target"),stage=Path.Combine(root,"stage");Directory.CreateDirectory(target);Directory.CreateDirectory(stage);
     foreach(string file in new[]{"拍照解题助教.exe","WpfMath.dll"}){File.WriteAllText(Path.Combine(target,file),"old");File.WriteAllText(Path.Combine(stage,file),"new");}File.WriteAllText(Path.Combine(target,"接口配置_仅自用.json"),"keep");
     using(var held=File.Open(Path.Combine(target,"WpfMath.dll"),FileMode.Open,FileAccess.Read,FileShare.Read)){bool rejected=false;try{Program.Apply(stage,target,new List<string>{"拍照解题助教.exe","WpfMath.dll"});}catch(IOException error){rejected=error.Message.Contains("已逐项核验恢复");}if(!rejected)throw new Exception("未正确报告替换失败");}
     if(File.ReadAllText(Path.Combine(target,"拍照解题助教.exe"))!="old"||File.ReadAllText(Path.Combine(target,"WpfMath.dll"))!="old"||File.ReadAllText(Path.Combine(target,"接口配置_仅自用.json"))!="keep"||Directory.GetFiles(target,"*.update-new").Any())throw new Exception("回退不完整");
    });
    check("后续成功更新保留配置和最近两份备份",()=>{string target=Path.Combine(root,"target"),stage=Path.Combine(root,"stage");for(int i=0;i<3;i++)Program.Apply(stage,target,new List<string>{"拍照解题助教.exe","WpfMath.dll"});Program.TrimBackups(target);if(Directory.GetFiles(Path.Combine(target,"更新回退"),"verified.success",SearchOption.AllDirectories).Length!=2||Directory.GetFiles(Path.Combine(target,"更新回退"),"preserve.failed",SearchOption.AllDirectories).Length!=1||File.ReadAllText(Path.Combine(target,"接口配置_仅自用.json"))!="keep")throw new Exception("备份或配置错误");});
    check("缓存仅清理过期托管目录并保留新缓存及其他文件",()=>{string target=Path.Combine(root,"target"),cache=Path.Combine(target,".update-cache"),old=Path.Combine(cache,Guid.NewGuid().ToString("N")),fresh=Path.Combine(cache,Guid.NewGuid().ToString("N")),other=Path.Combine(cache,"teacher-files");foreach(var dir in new[]{old,fresh,other})Directory.CreateDirectory(dir);Directory.SetLastWriteTimeUtc(old,DateTime.UtcNow.AddDays(-3));Directory.SetLastWriteTimeUtc(other,DateTime.UtcNow.AddDays(-3));Program.CleanCache(target);if(Directory.Exists(old)||!Directory.Exists(fresh)||!Directory.Exists(other))throw new Exception("缓存清理越界或未清理");});
   }finally{try{Directory.Delete(root,true);}catch{}}
   Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report)));File.WriteAllText(report,Program.Json.Serialize(new{failures,checks=rows}),Encoding.UTF8);return failures==0?0:1;
  }
 }
}
