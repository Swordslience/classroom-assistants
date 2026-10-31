using System.Text.Json;
using System.Windows.Controls;

namespace ClassroomAI.Seewo;
// The input viewport has its own identity; scrolling must never edit the text.
internal sealed class InputScrollSync {
 readonly TextBox box;string context="",text="",viewId=Guid.NewGuid().ToString("N");
 readonly Dictionary<string,long> sequences=new();
 internal InputScrollSync(TextBox box)=>this.box=box;
 void Refresh(string mode,string page){string next=mode+"|"+page;if(context==next&&text==box.Text)return;context=next;text=box.Text;viewId=Guid.NewGuid().ToString("N");sequences.Clear();}
 internal object State(string mode,string page){Refresh(mode,page);double height=Math.Max(0,box.ExtentHeight-box.ViewportHeight);return new{mode,viewId,ratio=height<=0?0:Math.Clamp(box.VerticalOffset/height,0,1)};}
 internal bool Apply(JsonElement message,string mode,string page){Refresh(mode,page);if(!message.TryGetProperty("viewId",out var id)||id.GetString()!=viewId)return false;
  if(!message.TryGetProperty("ratio",out var value)||!value.TryGetDouble(out var ratio)||!double.IsFinite(ratio)||ratio<0||ratio>1)return false;
  if(message.TryGetProperty("seq",out var seq)&&seq.TryGetInt64(out long n)){string origin=message.TryGetProperty("origin",out var o)?o.GetString()??"":"";if(sequences.TryGetValue(origin,out var previous)&&n<=previous)return false;if(sequences.Count>64)sequences.Clear();sequences[origin]=n;}
  box.UpdateLayout();box.ScrollToVerticalOffset(ratio*Math.Max(0,box.ExtentHeight-box.ViewportHeight));return true;
 }
}
