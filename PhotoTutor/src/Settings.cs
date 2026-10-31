using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace PhotoTutor {
 internal sealed class Settings {
  internal static string? IsolatedTestRoot,IsolatedLegacyPath;
  internal static string UserDataRoot(string localAppData)=>Path.Combine(localAppData,"PhotoTutor");
  public string Provider {get;set;}="DeepSeek";
  public string DeepSeekApiKey {get;set;}="";
  public string DeepSeekModel {get;set;}="deepseek-flash";
  public string DoubaoApiKey {get;set;}="";
  public string DoubaoModel {get;set;}="doubao-seed-2-0-mini-260428";
  public string VoiceApiKey {get;set;}="";
  public string VoiceSpeaker {get;set;}="zh_female_vv_uranus_bigtts";
  public int DailyLimit {get;set;}=80;
  public int BodySize {get;set;}=32;
  public string PreferredCamera {get;set;}="";
  public bool HighQuality {get;set;}=false;
  public static string DataRoot {
   get {
    string root=IsolatedTestRoot??UserDataRoot(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
    Directory.CreateDirectory(root);return root;
   }
  }
  static string FilePath=>Path.Combine(DataRoot,"settings.protected");
  public static Settings Load() {
   // One-time, current-user DPAPI migration; the old file is never changed or deleted.
   if((IsolatedTestRoot==null||IsolatedLegacyPath!=null)&&!File.Exists(FilePath)){string legacy=IsolatedLegacyPath??"";if(File.Exists(legacy))try{var existing=JsonConvert.DeserializeObject<Settings>(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(legacy),null,DataProtectionScope.CurrentUser)));if(existing!=null){existing.Save();return existing;}}catch{}}
   if(File.Exists(FilePath))try{return JsonConvert.DeserializeObject<Settings>(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(FilePath),null,DataProtectionScope.CurrentUser)))??new Settings();}catch{ }
   string portable=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"接口配置_仅自用.json");
   if(File.Exists(portable))try{var s=JsonConvert.DeserializeObject<Settings>(File.ReadAllText(portable,Encoding.UTF8))??new Settings();s.Save();return s;}catch{ }
   return new Settings();
  }
  public void Save() {
   var bytes=ProtectedData.Protect(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(this)),null,DataProtectionScope.CurrentUser);
   File.WriteAllBytes(FilePath+".tmp",bytes);
   if(File.Exists(FilePath))File.Replace(FilePath+".tmp",FilePath,null);else File.Move(FilePath+".tmp",FilePath);
  }
  public static void CleanOldVoice() {
   try{QuestionWorkspace.Prune(DataRoot);}catch(IOException){}catch(UnauthorizedAccessException){}
   try{foreach(var file in new DirectoryInfo(DataRoot).GetFiles("voice_*.wav"))if(file.LastWriteTimeUtc<DateTime.UtcNow.AddHours(-1))try{file.Delete();}catch{ }}catch{ }
  }
 }
 internal static class DailyUse {
  static string PathName=>Path.Combine(Settings.DataRoot,"daily-use.json");
  public static int Count() {try{var s=JsonConvert.DeserializeObject<Counter>(File.ReadAllText(PathName));return s!=null&&s.Date==DateTime.Today.ToString("yyyy-MM-dd")?s.Count:0;}catch{return 0;}}
  public static void Begin(int limit) {
   int count=Count();if(count>=limit)throw new InvalidOperationException("今天已达到本机解题次数上限，请老师在设置中调整。");
   File.WriteAllText(PathName,JsonConvert.SerializeObject(new Counter{Date=DateTime.Today.ToString("yyyy-MM-dd"),Count=count+1}));
  }
  sealed class Counter {public string Date="";public int Count;}
 }
}
