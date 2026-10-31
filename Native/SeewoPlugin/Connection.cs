using System.IO;
using System.Text.Json;
using ClassroomAiBridge;
namespace ClassroomAI.Seewo;
internal sealed class Connection {
 public string ApiKey{get;set;}="";public string Model{get;set;}="doubao-seed-2-0-mini-260428";
 public string TextProvider{get;set;}="DeepSeek";public string DeepSeekApiKey{get;set;}="";public string DeepSeekModel{get;set;}="deepseek-flash";
 public bool EnableSearch{get;set;}=true;public string ThinkingEffort{get;set;}="low";
 public string VoiceApiKey{get;set;}="";public string VoiceSpeaker{get;set;}="zh_female_vv_uranus_bigtts";
 public string OutputFolder{get;set;}="";public string Passage{get;set;}="";
 public static Connection Load(){var path=Path.Combine(BudgetGate.Root,"connection.json");return File.Exists(path)?JsonSerializer.Deserialize<Connection>(File.ReadAllText(path))??new():new();}
}
