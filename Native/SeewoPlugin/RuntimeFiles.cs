using System.IO;
namespace ClassroomAI.Seewo;
internal static class RuntimeFiles {
 internal static string Root {get{string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NailongClassroom","Cache");Directory.CreateDirectory(root);return root;}}
 internal static string Snapshots {get{string folder=Path.Combine(Root,"页面快照");Directory.CreateDirectory(folder);return folder;}}
 internal static void Clean(){Prune(Snapshots,"*.png",60,40*1024*1024);Prune(Root,"voice_*.wav",30,60*1024*1024);string legacy=Path.Combine(ClassroomAiBridge.BudgetGate.Root,"页面快照");if(Directory.Exists(legacy))Prune(legacy,"*.png",10,15*1024*1024);}
 static void Prune(string folder,string pattern,int count,long bytes){long total=0;int index=0;foreach(var file in new DirectoryInfo(folder).GetFiles(pattern).OrderByDescending(f=>f.LastWriteTimeUtc)){total+=file.Length;bool remove=file.LastWriteTimeUtc<DateTime.UtcNow.AddDays(-1)||index++>=count||total>bytes;
  if(remove&&(file.Attributes&FileAttributes.ReparsePoint)==0)try{file.Delete();}catch(IOException){}catch(UnauthorizedAccessException){}
 }}
}
