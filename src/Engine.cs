using System.IO;
using System.Text.Json;
using System.Security.Cryptography;
using System.Buffers.Binary;
using System.Diagnostics;
namespace ReeFont;
public record Game(string Id,string Name,string Zh,string List,string[] Executables,Dictionary<string,string[]> Fonts);
public record Entry(string Path,string? Backup,string? OriginalHash,string InstalledHash);
public record Journal(string GameId,List<Entry> Entries);
public static class Engine {
 public static readonly JsonSerializerOptions Json=new(){WriteIndented=true,PropertyNameCaseInsensitive=true};
 public static string Hash(byte[] b)=>Convert.ToHexString(SHA256.HashData(b));
 public static string Safe(string root,string relative) {
  if(Path.IsPathRooted(relative)||relative.Contains(':')||relative.Split('/','\\').Any(x=>x==".."||x==".")) throw new IOException("Unsafe path / 不安全的路径");
  var full=Path.GetFullPath(Path.Combine(root,relative));
  if(!full.StartsWith(Path.GetFullPath(root).TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase)) throw new IOException("Path escapes root");
  for(FileSystemInfo? d=new FileInfo(full);d!=null;d=d is FileInfo f?f.Directory:(d as DirectoryInfo)?.Parent) if(d.Exists&&d.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Linked paths unsupported / 不支持链接目录");
  return full;
 }
 public static Game[] Catalog(string folder) {
  var games=JsonSerializer.Deserialize<Game[]>(File.ReadAllText(Path.Combine(folder,"games.json")),Json)??[];
  if(games.Select(x=>x.Id).Distinct().Count()!=games.Length) throw new IOException("Duplicate game id");
  foreach(var g in games) {
   if(string.IsNullOrWhiteSpace(g.Id)||g.Executables.Length==0||g.Fonts.Count==0||g.Fonts.Any(x=>!new[]{"sc","tc","en"}.Contains(x.Key)||x.Value.Length==0)) throw new IOException("Invalid game configuration / 游戏配置无效: "+g.Id);
   Safe(folder,g.Id); foreach(var exe in g.Executables) Safe(folder,exe);
   var paths=File.ReadLines(Safe(folder,g.List)).Select(x=>x.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
   foreach(var p in g.Fonts.Values.SelectMany(x=>x)) { Safe(folder,p); if(!p.StartsWith("natives/")||!p.EndsWith(".oft.1")||!paths.Contains(p)) throw new IOException("Mapping absent from list: "+p); }
  }
  return games;
 }
 public static void ValidateDirectory(Game g,string root) {
  if(!Directory.Exists(root)||!g.Executables.Any(x=>File.Exists(Safe(root,x)))||!File.Exists(Safe(root,"re_chunk_000.pak"))) throw new IOException("Wrong game directory / 游戏目录与所选游戏不匹配");
  foreach(var exe in g.Executables) if(Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exe)).Length>0) throw new IOException("Close game first / 请先退出游戏");
 }
 public static byte[] ReadFont(string path) {
  if(!new[]{".ttf",".otf"}.Contains(Path.GetExtension(path).ToLowerInvariant())) throw new IOException("Select TTF or OTF / 请选择 TTF 或 OTF");
  var size=new FileInfo(path).Length; if(size<12||size>128*1024*1024) throw new IOException("Invalid font size");
  var b=File.ReadAllBytes(path); var magic=BinaryPrimitives.ReadUInt32BigEndian(b);
  if(magic!=0x00010000&&magic!=0x4F54544F) throw new IOException("Invalid font signature / 字体文件头无效");
  int n=BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(4)); if(n==0||12+n*16>b.Length) throw new IOException("Invalid font table");
  bool cmap=false;
  for(int i=0;i<n;i++) { var t=b.AsSpan(12+i*16,16); uint offset=BinaryPrimitives.ReadUInt32BigEndian(t[8..]),len=BinaryPrimitives.ReadUInt32BigEndian(t[12..]); if((ulong)offset+len>(ulong)b.Length) throw new IOException("Truncated font table"); if(BinaryPrimitives.ReadUInt32BigEndian(t)==0x636D6170) cmap=true; }
  if(!cmap) throw new IOException("No character map"); return b;
 }
 public static byte[] Encode(byte[] font) {
  var b=new byte[font.Length+4]; BinaryPrimitives.WriteUInt32LittleEndian(b,0x4F464246);
  ulong seed=1,delta=0xAE6E39B58A355F45; int shift=font.Length&63;
  unchecked { for(int i=0;i<shift;i++) seed=2*seed+1; }
  ulong key=(delta>>shift)|((seed&delta)<<(64-shift));
  for(int i=0;i<font.Length;i++) b[i+4]=(byte)(font[i]^(byte)(key>>(i%8*8))); return b;
 }
 static string State(string root,Game g)=>Safe(root,$".ree-font-tool/{g.Id}/active.json");
 static FileStream Lock(string root) { var p=Safe(root,".ree-font-tool/operation.lock"); Directory.CreateDirectory(Path.GetDirectoryName(p)!); return new FileStream(p,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None); }
 static void Atomic(string path,byte[] data) {
  Directory.CreateDirectory(Path.GetDirectoryName(path)!); string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
  try { using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)) { f.Write(data); f.Flush(true); } File.Move(temp,path,true); } finally { if(File.Exists(temp)) File.Delete(temp); }
 }
 public static void Apply(Game g,string root,string font,string[] languages,Action<string> log) {
  ValidateDirectory(g,root);
  using var operationLock=Lock(root);
  var framework=FrameworkSupport.Inspect(root);
  if(!framework.Ready) throw new IOException(framework.Message(false)+" / "+framework.Message(true));
  if(File.Exists(State(root,g))) throw new IOException("Restore current patch first / 请先还原当前补丁");
  var paths=languages.SelectMany(x=>g.Fonts.TryGetValue(x,out var p)?p:throw new IOException("Unsupported language")).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
  if(paths.Length==0) throw new IOException("Select a language / 请选择语言");
  var encoded=Encode(ReadFont(font)); var hash=Hash(encoded); var entries=new List<Entry>(); var batch=Guid.NewGuid().ToString("N");
  foreach(var p in paths) { var target=Safe(root,p); string? backup=null,originalHash=null; if(File.Exists(target)) { var original=File.ReadAllBytes(target); originalHash=Hash(original); backup=$".ree-font-tool/{g.Id}/backups/{batch}/{entries.Count}.bin"; Atomic(Safe(root,backup),original); } entries.Add(new(p,backup,originalHash,hash)); }
  Atomic(State(root,g),JsonSerializer.SerializeToUtf8Bytes(new Journal(g.Id,entries),Json));
  try { foreach(var e in entries) { Atomic(Safe(root,e.Path),encoded); log(e.Path); } } catch { log("Interrupted: use Restore / 写入中断，请还原"); throw; }
 }
 public static void Restore(Game g,string root,Action<string> log) {
  ValidateDirectory(g,root); using var operationLock=Lock(root); var state=State(root,g); if(!File.Exists(state)) throw new IOException("No managed patch / 没有本工具的补丁记录");
  var j=JsonSerializer.Deserialize<Journal>(File.ReadAllText(state),Json)??throw new IOException("Invalid journal");
  if(j.GameId!=g.Id) throw new IOException("Journal mismatch"); var allowed=g.Fonts.Values.SelectMany(x=>x).ToHashSet(StringComparer.OrdinalIgnoreCase);
  foreach(var e in j.Entries) { if(!allowed.Contains(e.Path)) throw new IOException("Journal path mismatch"); var target=Safe(root,e.Path); if(File.Exists(target)) { var h=Hash(File.ReadAllBytes(target)); if(h!=e.InstalledHash&&h!=e.OriginalHash) throw new IOException("File changed externally; preserved / 文件被其他工具修改，已保留: "+e.Path); } if(e.Backup!=null&&(!e.Backup.StartsWith($".ree-font-tool/{g.Id}/backups/")||!File.Exists(Safe(root,e.Backup))||Hash(File.ReadAllBytes(Safe(root,e.Backup)))!=e.OriginalHash)) throw new IOException("Backup missing or damaged / 备份缺失或损坏"); }
  foreach(var e in j.Entries) { var target=Safe(root,e.Path); if(e.Backup!=null) Atomic(target,File.ReadAllBytes(Safe(root,e.Backup))); else if(File.Exists(target)) File.Delete(target); log(e.Path); }
  File.Move(state,Safe(root,$".ree-font-tool/{g.Id}/restored-{Guid.NewGuid():N}.json"));
 }
}

