using System;
using System.IO;
namespace ClassroomUpdater {
 internal static class UpdateKey {
  internal static string Public {
   get {
    string path=Environment.GetEnvironmentVariable("CLASSROOM_UPDATE_PUBLIC_KEY_PATH")??"";
    if(string.IsNullOrWhiteSpace(path))throw new InvalidDataException("尚未配置更新验签公钥，请设置 CLASSROOM_UPDATE_PUBLIC_KEY_PATH。不会跳过签名验证。");
    return File.ReadAllText(path);
   }
  }
 }
}
