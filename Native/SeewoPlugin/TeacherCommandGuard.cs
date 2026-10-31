using System;
using System.Text.Json;
namespace ClassroomAI.Seewo;
internal sealed record TeacherIdentity(string Uid,string Name,bool Authenticated,string Epoch);
internal static class TeacherCommandGuard {
 internal static bool Allows(TeacherIdentity identity,JsonElement command,long now){
  bool Matches(string key,string expected)=>command.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String&&value.GetString()==expected;
  return identity.Authenticated&&identity.Uid.Length>0&&command.ValueKind==JsonValueKind.Object&&Matches("teacherUid",identity.Uid)&&Matches("identityEpoch",identity.Epoch)&&command.TryGetProperty("expires",out var expires)&&expires.TryGetInt64(out long until)&&until>now;
 }
}
