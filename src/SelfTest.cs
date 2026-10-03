using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace ReeFont;
public static class SelfTest {
 public static void Run(string[] args) {
  var output=args[0]; Directory.CreateDirectory(output); var report=new List<string>();
  void Check(bool condition,string name) { if(!condition) throw new Exception(name); report.Add("PASS "+name); }
  var games=Engine.Catalog(Path.Combine(AppContext.BaseDirectory,"Projects")); Check(games.Length>=8,"catalog mappings exist in lists");
  var font=args[1]; var encoded=Engine.Encode(Engine.ReadFont(font)); File.WriteAllBytes(Path.Combine(output,"converted.oft.1"),encoded);
  var g=games[0]; var root=Path.Combine(output,"sandbox-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
  File.WriteAllBytes(Path.Combine(root,g.Executables[0]),[]); File.WriteAllBytes(Path.Combine(root,"re_chunk_000.pak"),[]); File.WriteAllBytes(Path.Combine(root,"dinput8.dll"),[]); File.WriteAllText(Path.Combine(root,"re2_fw_config.txt"),"LooseFileLoader_Enabled=true");
  Check(FrameworkSupport.Inspect(root).Ready,"framework enabled detected");
  File.WriteAllText(Path.Combine(root,"re2_fw_config.txt"),"LooseFileLoader_Enabled = false"); Check(FrameworkSupport.Inspect(root).Loose==false&&!FrameworkSupport.Inspect(root).Ready,"framework disabled detected");
  File.WriteAllText(Path.Combine(root,"re2_fw_config.txt"),"Other=true"); Check(FrameworkSupport.Inspect(root).Loose==null,"unknown loader setting detected");
  File.Delete(Path.Combine(root,"re2_fw_config.txt")); Check(!FrameworkSupport.Inspect(root).Config,"DLL alone is not confirmed installation");
  File.Delete(Path.Combine(root,"dinput8.dll")); Check(!FrameworkSupport.Inspect(root).Dll,"missing framework detected");
  File.WriteAllBytes(Path.Combine(root,"dinput8.dll"),[]); File.WriteAllText(Path.Combine(root,"re2_fw_config.txt"),"LooseFileLoader_Enabled=true");
  var seen=Path.Combine(root,"settings","seen.txt"); Check(!FrameworkSupport.WasSeen(seen),"first launch shows help"); FrameworkSupport.MarkSeen(seen); Check(FrameworkSupport.WasSeen(seen),"help acknowledgement persists");
  var target=Engine.Safe(root,g.Fonts["sc"][0]); Directory.CreateDirectory(Path.GetDirectoryName(target)!); var original=new byte[]{1,2,3,4,5}; File.WriteAllBytes(target,original);
  Engine.Apply(g,root,font,["sc"],_=>{}); Check(File.ReadAllBytes(target).SequenceEqual(encoded),"replacement bytes");
  File.WriteAllBytes(target,[9]); bool conflict=false; try {Engine.Restore(g,root,_=>{});} catch(IOException){conflict=true;} Check(conflict&&File.ReadAllBytes(target).SequenceEqual(new byte[]{9}),"external modification preserved");
  File.WriteAllBytes(target,encoded); Engine.Restore(g,root,_=>{}); Check(File.ReadAllBytes(target).SequenceEqual(original),"existing file restored exactly");
  File.Delete(target); Engine.Apply(g,root,font,["sc"],_=>{}); Engine.Restore(g,root,_=>{}); Check(!File.Exists(target),"new file removed on restore");
  bool traversal=false; try {Engine.Safe(root,"../escape");} catch(IOException){traversal=true;} Check(traversal,"path traversal rejected");
  var bad=Path.Combine(root,"bad.ttf"); File.WriteAllBytes(bad,new byte[100]); bool invalid=false; try {Engine.ReadFont(bad);} catch(IOException){invalid=true;} Check(invalid,"invalid font rejected");
  if(args.Length>2) { Engine.ValidateDirectory(g,args[2]); report.Add("PASS installed Onimusha directory (read-only)"); }
  var window=new MainWindow(false); window.Show(); window.UpdateLayout();
  var preview=ModExporter.Preview(g,font,["sc"]);
  var modPath=Path.Combine(output,ModExporter.FileName(g,font,["sc"])); ModExporter.Export(g,font,["sc"],modPath,preview);
  using(var zip=System.IO.Compression.ZipFile.OpenRead(modPath)) {
   Check(zip.Entries.Count==g.Fonts["sc"].Distinct().Count()+2,"ZIP has only metadata, preview and mapped fonts");
   using var reader=new StreamReader(zip.GetEntry("modinfo.ini")!.Open()); var ini=reader.ReadToEnd();
   Check(ini.Contains("author=caicaiying\r\n")&&ini.Contains("homepage=https://space.bilibili.com/8480063\r\n")&&ini.Contains("screenshot=screenshot.jpg\r\n")&&ini.Contains("version=1.0\r\n"),"Fluffy metadata values");
   using var bytes=new MemoryStream(); using(var source=zip.GetEntry(g.Fonts["sc"][0])!.Open()) source.CopyTo(bytes);
   Check(bytes.ToArray().SequenceEqual(encoded),"ZIP font matches original converter");
   using var jpg=zip.GetEntry("screenshot.jpg")!.Open(); using var jpgBuffer=new MemoryStream(); jpg.CopyTo(jpgBuffer); jpgBuffer.Position=0; Check(new JpegBitmapDecoder(jpgBuffer,BitmapCreateOptions.None,BitmapCacheOption.OnLoad).Frames[0].PixelWidth==960,"ZIP preview is valid JPEG");
  }
  bool noLanguage=false; try {ModExporter.Export(g,font,[],modPath,preview);} catch(IOException){noLanguage=true;}
  Check(noLanguage&&File.Exists(modPath),"invalid export preserves existing archive");
  Check(Math.Abs(window.ExportButton.FontSize/window.RestoreButton.FontSize-.8)<.001,"export text 20 percent smaller");
  Check(Math.Abs(window.ExportButton.TranslatePoint(new Point(window.ExportButton.ActualWidth,0),window).X-window.LogBox.TranslatePoint(new Point(window.LogBox.ActualWidth,0),window).X)<1,"export right edge aligned with log");
  window.SearchBox.Text="鬼武者"; window.UpdateLayout(); Capture(window,"search-typed-150.png",144);
  var caret=window.SearchBox.GetRectFromCharacterIndex(0);
  Check(!caret.IsEmpty&&caret.Top>=0&&caret.Bottom<=window.SearchBox.ActualHeight,"search character fits vertically");
  window.SearchBox.Clear();
  Check(Math.Abs(window.SearchBox.ActualHeight/window.DirectoryBox.ActualHeight-.8)<.01,"search height is 80% of directory input");
  Check(Math.Abs(window.SearchBox.ActualWidth-window.Games.ActualWidth)<1,"search width matches game list");
  window.SearchBox.Text="鬼武者"; Check(window.Games.Items.Count==1,"Chinese search");
  window.SearchBox.Text="resident 7"; Check(window.Games.Items.Count>=1,"multi-term English search");
  window.SearchBox.Text="no-such-game-xyz"; Check(window.Games.Items.Count==0&&!window.ReplaceButton.IsEnabled,"empty results disable replacement");
  window.SearchBox.Clear(); Check(window.Games.Items.Count==games.Length,"clear search restores catalog");
  var fontSize=window.DirectoryBox.FontSize; window.WindowState=WindowState.Maximized; window.UpdateLayout();
  Check(window.DirectoryBox.FontSize==fontSize&&Math.Abs(window.DirectoryBox.ActualHeight-50)<1,"maximized layout retains font and field size");
  Check(WindowBounds.InsideWorkArea(new System.Windows.Interop.WindowInteropHelper(window).Handle),"native maximized bounds exclude taskbar");
  Capture(window,"ui-maximized.png",96); window.WindowState=WindowState.Normal; window.UpdateLayout();
  var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32); bitmap.Render(window);
  var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using(var stream=File.Create(Path.Combine(output,"ui-preview.png"))) encoder.Save(stream);
  window.LanguageButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); window.UpdateLayout();
  Check(window.DirectoryLabel.HorizontalAlignment==HorizontalAlignment.Right&&window.DirectoryLabel.Margin==window.FontLabel.Margin,"Chinese directory label right aligned");
  Capture(window,"ui-chinese-150.png",144);
  window.DirectoryBox.Text="previous directory"; window.FontBox.Text="previous font"; window.Games.SelectedIndex=1;
  Check(window.DirectoryBox.Text==""&&window.FontBox.Text==""&&window.TC.Visibility==Visibility.Visible,"game switch clears inputs and exposes mapped languages");
  window.Width=760; window.Height=500; window.UpdateLayout(); Capture(window,"ui-small-200.png",192);
  var help=FrameworkSupport.Dialog(window,true); help.Show(); help.UpdateLayout(); Capture(help,"framework-help.png",96); help.Close(); window.Close();
  File.WriteAllLines(Path.Combine(output,"test-results.txt"),report);
  void Capture(Window target,string filename,double dpi) { var b=new RenderTargetBitmap((int)Math.Ceiling(target.ActualWidth*dpi/96),(int)Math.Ceiling(target.ActualHeight*dpi/96),dpi,dpi,PixelFormats.Pbgra32); b.Render(target); var e=new PngBitmapEncoder(); e.Frames.Add(BitmapFrame.Create(b)); using var f=File.Create(Path.Combine(output,filename)); e.Save(f); }
 }
}



