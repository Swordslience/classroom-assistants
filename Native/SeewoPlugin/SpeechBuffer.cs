using System.Text;
using System.Text.RegularExpressions;

namespace ClassroomAI.Seewo;
internal sealed class SpeechBuffer {
 readonly StringBuilder text=new();
 public void Append(string value)=>text.Append(value);
 public IEnumerable<string> Drain(bool complete){
  while(text.Length>0){
   string current=text.ToString();int take=0,maximum=Math.Min(800,current.Length);
   if(current.Length>=25){var end=Regex.Match(current[..maximum],@"[。！？!?；;]|\.(?:\s|$)");if(end.Success)take=end.Index+end.Length;}
   if(take==0&&current.Length>=800){take=800;if(char.IsHighSurrogate(current[take-1]))take--;}
   if(take==0&&complete)take=current.Length;
   if(take==0)yield break;
   string part=current[..take];text.Remove(0,take);yield return part;
  }
 }
}
