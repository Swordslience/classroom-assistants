using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;

namespace PhotoTutor {
 internal static class LocalOcrRuntime {
  static string? prepared,preparedRoot;static bool unsupported;
  internal static string? Resolve(){if(unsupported||!Environment.Is64BitOperatingSystem)return null;string root=Path.Combine(Settings.DataRoot,"ocr-runtime");if(prepared!=null&&preparedRoot==root&&File.Exists(prepared))return prepared;
   string? staging=null;
   try{var assembly=typeof(Entry).Assembly;using var metadata=assembly.GetManifestResourceStream("PhotoTutor.LocalOcr.manifest.json");if(metadata==null)return null;JObject manifest;using(var reader=new StreamReader(metadata))manifest=JObject.Parse(reader.ReadToEnd());string digest=(string)manifest["zipSha256"]!;if(!System.Text.RegularExpressions.Regex.IsMatch(digest,"^[a-f0-9]{64}$"))throw new InvalidDataException("OCR资源标识无效");Directory.CreateDirectory(root);string destination=Path.Combine(root,digest.Substring(0,16));
    bool Verified(string directory){if(!Directory.Exists(directory))return false;foreach(var item in manifest["files"]!){string path=SafePath(directory,(string)item["name"]!);if(!File.Exists(path)||new FileInfo(path).Length!=(long)item["bytes"]!||Hash(path)!=(string)item["sha256"]!)return false;}return true;}
    if(!Verified(destination)){if(Directory.Exists(destination))Directory.Move(destination,Path.Combine(root,"invalid_"+Guid.NewGuid().ToString("N")));staging=Path.Combine(root,"prepare_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(staging);using var stream=assembly.GetManifestResourceStream("PhotoTutor.LocalOcr.zip")??throw new InvalidDataException("OCR资源缺失");using(var sha=SHA256.Create()){string actual=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();if(actual!=digest)throw new InvalidDataException("OCR资源校验失败");}stream.Position=0;using(var zip=new ZipArchive(stream,ZipArchiveMode.Read)){if(zip.Entries.Count>20)throw new InvalidDataException("OCR资源数量异常");foreach(var entry in zip.Entries){var info=System.Linq.Enumerable.FirstOrDefault(manifest["files"]!,v=>(string?)v["name"]==entry.FullName);if(info==null||entry.Length!=(long)info["bytes"]!)throw new InvalidDataException("OCR资源清单不符");string file=SafePath(staging,entry.FullName);Directory.CreateDirectory(Path.GetDirectoryName(file)!);using(var input=entry.Open())using(var output=File.Create(file))input.CopyTo(output);}}if(!Verified(staging))throw new InvalidDataException("OCR运行文件校验失败");Directory.Move(staging,destination);staging=null;}
    prepared=Path.Combine(destination,"PhotoTutor.LocalOcr.exe");preparedRoot=root;SessionDiagnostics.Record("local-ocr-ready",new{embedded=true,networkRequired=false});return prepared;
   }catch(Exception error){unsupported=true;SessionDiagnostics.Record("local-ocr-unavailable",null,error);return null;}
   finally{if(staging!=null)try{string full=Path.GetFullPath(staging),parent=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);if(string.Equals(Path.GetDirectoryName(full),parent,StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(full).StartsWith("prepare_")&&(File.GetAttributes(full)&FileAttributes.ReparsePoint)==0)Directory.Delete(full,true);}catch{} }
  }
  internal static void MarkUnavailable(){unsupported=true;}
  static string SafePath(string root,string relative){if(relative.Contains("..")||relative.Contains(":")||relative.StartsWith("/")||relative.StartsWith("\\"))throw new InvalidDataException("OCR资源路径无效");string full=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar))),prefix=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;if(!full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("OCR资源超出目录");return full;}
  static string Hash(string file){using(var sha=SHA256.Create())using(var stream=File.OpenRead(file))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
 }
}
