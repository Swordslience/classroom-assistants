using System;
using System.Collections.Generic;
using System.Globalization;
namespace PhotoTutor {
 internal static class TextElements {
  // .NET Framework's StringInfo predates extended emoji clusters. Join complete elements,
  // never UTF-16 code units, for ZWJ, skin tone and regional-indicator sequences.
  internal static IEnumerable<string> Split(string text){var e=StringInfo.GetTextElementEnumerator(text);string pending="";int regional=0;while(e.MoveNext()){string next=e.GetTextElement();int code=char.ConvertToUtf32(next,0);bool join=pending.Length>0&&(pending.EndsWith("\u200d",StringComparison.Ordinal)||code==0x200d||code>=0x1f3fb&&code<=0x1f3ff||code>=0xfe00&&code<=0xfe0f||code>=0x1f1e6&&code<=0x1f1ff&&regional==1);if(join){pending+=next;if(code>=0x1f1e6&&code<=0x1f1ff)regional++;continue;}if(pending.Length>0)yield return pending;pending=next;regional=code>=0x1f1e6&&code<=0x1f1ff?1:0;}if(pending.Length>0)yield return pending;}
 }
}
