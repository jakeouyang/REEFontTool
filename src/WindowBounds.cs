using System.Runtime.InteropServices;
namespace ReeFont;
internal static class WindowBounds {
 [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X,Y; }
 [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left,Top,Right,Bottom; }
 [StructLayout(LayoutKind.Sequential)] struct MonitorInfo { public int Size; public Rect Monitor,Work; public uint Flags; }
 [StructLayout(LayoutKind.Sequential)] struct MinMax { public Point Reserved,MaxSize,MaxPosition,MinTrack,MaxTrack; }
 [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd,uint flags);
 [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd,out Rect rect);
 internal static bool InsideWorkArea(IntPtr hwnd) { var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()}; return GetMonitorInfo(MonitorFromWindow(hwnd,2),ref info)&&GetWindowRect(hwnd,out var r)&&r.Left>=info.Work.Left&&r.Top>=info.Work.Top&&r.Right<=info.Work.Right&&r.Bottom<=info.Work.Bottom; }
 public static IntPtr Hook(IntPtr hwnd,int msg,IntPtr wParam,IntPtr lParam,ref bool handled) {
  if(msg==0x24) { var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()}; if(GetMonitorInfo(MonitorFromWindow(hwnd,2),ref info)) {
   var limits=Marshal.PtrToStructure<MinMax>(lParam);
   limits.MaxPosition=new Point{X=info.Work.Left-info.Monitor.Left,Y=info.Work.Top-info.Monitor.Top};
   limits.MaxSize=new Point{X=info.Work.Right-info.Work.Left,Y=info.Work.Bottom-info.Work.Top};
   Marshal.StructureToPtr(limits,lParam,false); handled=true;
  }} return IntPtr.Zero;
 }
}
