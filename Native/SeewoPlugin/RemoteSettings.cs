using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using ClassroomAiBridge;

namespace ClassroomAI.Seewo;

internal static class RemoteDeployment {
 internal static string DefaultServerUrl {
  get {
   string file=Path.Combine(Path.GetDirectoryName(typeof(RemoteDeployment).Assembly.Location)!,"classroom-deployment.json");
   if(!File.Exists(file))return ""; // Open-source builds carry no private deployment.
   using var document=JsonDocument.Parse(File.ReadAllText(file));
   string value=document.RootElement.GetProperty("ServerUrl").GetString()??"";
   if(string.IsNullOrWhiteSpace(value))return "";
   if(!Uri.TryCreate(value,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.AbsolutePath!="/"||
      !string.IsNullOrEmpty(uri.UserInfo)||!string.IsNullOrEmpty(uri.Query)||!string.IsNullOrEmpty(uri.Fragment))
    throw new InvalidDataException("默认课堂服务地址必须是HTTPS根地址。");
   return value.Trim().TrimEnd('/');
  }
 }
 internal static bool IsOldLocalTest(string value)=>Uri.TryCreate(value,UriKind.Absolute,out var uri)&&uri.IsLoopback&&uri.Port==3000;
}

internal sealed class RemoteSettings {
 public string ServerUrl { get; set; } = RemoteDeployment.DefaultServerUrl;
 public string DeviceId { get; set; } = Guid.NewGuid().ToString("N");
 public string DeviceSecret { get; set; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
 public string DeviceName { get; set; } = Environment.MachineName;
 internal static string FilePath => Path.Combine(BudgetGate.Root,"remote.local.json");
 internal void Save(){Directory.CreateDirectory(BudgetGate.Root);File.WriteAllText(FilePath,JsonSerializer.Serialize(this));}
 internal static RemoteSettings Load(){
  Directory.CreateDirectory(BudgetGate.Root);
  using var guard=new Mutex(false,@"Local\ClassroomAI.RemoteSettings");
  if(!guard.WaitOne(TimeSpan.FromSeconds(3)))throw new IOException("设备设置正在被另一个课件读取，请稍后。");
  try {
   var settings=File.Exists(FilePath)?JsonSerializer.Deserialize<RemoteSettings>(File.ReadAllText(FilePath))??new():new();
   string defaultUrl=RemoteDeployment.DefaultServerUrl;
   if(!string.IsNullOrWhiteSpace(defaultUrl)&&(string.IsNullOrWhiteSpace(settings.ServerUrl)||RemoteDeployment.IsOldLocalTest(settings.ServerUrl)))
    settings.ServerUrl=defaultUrl;
   // Save only configuration. Existing device identity and bindings survive migration.
   settings.Save();return settings;
  } finally{guard.ReleaseMutex();}
 }
}
