using Cvte.EasiNote;

namespace ClassroomAI.Seewo;

internal static class SeewoIdentity {
 // Read only the host's public account wrapper. Never read passwords or tokens.
 internal static (string Uid,string Name)? Read(){
  try{
   var account=EN.Account;
   if(account is null||!account.IsAuth)return null;
   // CurrentUserId is the login account (it can be a phone number).
   // The opaque user resource ID identifies the teacher across computers.
   string uid=account.CurrentUserResourceId?.Trim()??"";
   string name=account.CurrentUserNickname??"";
   if(uid.Length is 0 or >128||uid.Any(char.IsControl)||!account.IsAuth||account.CurrentUserResourceId?.Trim()!=uid)return null;
   return(uid,name.Length>80?name[..80]:name);
  }catch(Exception error){System.Diagnostics.Trace.WriteLine("Seewo identity unavailable: "+error.GetType().Name);return null;}
 }
}
