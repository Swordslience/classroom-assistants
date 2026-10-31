using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PhotoTutor {
 internal sealed class ChatTurn {
  public string Role {get;}public string Text {get;}
  public ChatTurn(string role,string text){Role=role;Text=text;}
 }
 internal sealed class Dialogue {
  readonly List<ChatTurn> turns=new List<ChatTurn>();
  public string Pending="";
  public int Count=>turns.Count;
  // Keep all visible turns in memory; send recent turns to keep classroom
  // requests responsive. A new photo or new conversation clears both.
  public ChatTurn[] Context(){var result=new List<ChatTurn>();int length=0;foreach(var item in turns.AsEnumerable().Reverse()){if(result.Count>=12||(length+item.Text.Length>40000&&result.Count>0))break;result.Add(item);length+=item.Text.Length;}result.Reverse();return result.ToArray();}
  public void Commit(string answer){if(Pending.Length==0)return;turns.Add(new ChatTurn("user",Pending));if(answer.Length>0)turns.Add(new ChatTurn("assistant",answer));Pending="";}
  public void Clear(){turns.Clear();Pending="";}
  public string Display(string answer){var text=new StringBuilder();foreach(var turn in turns)text.AppendLine(turn.Role=="user"?"# 你":"# 助教").AppendLine(turn.Text).AppendLine();if(Pending.Length>0)text.AppendLine("# 你").AppendLine(Pending).AppendLine().AppendLine("# 助教").Append(answer.Length>0?answer:"正在思考，回答会逐步显示…");return text.ToString();}
 }
}
