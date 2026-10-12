using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;
using Forms=System.Windows.Forms;

class MaterialSmoke {
 [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point p);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint process);
 [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h,int message,IntPtr w,IntPtr l);
 [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h,int command);
 [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string type,string title);
 [StructLayout(LayoutKind.Sequential)] struct Rect {public int left,top,right,bottom;}
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out Rect rect);
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h,int index);
 [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h,int index,IntPtr value);
 [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h,int message,IntPtr w,IntPtr l);
 [DllImport("dwmapi.dll")] static extern int DwmFlush();
 [DllImport("gdi32.dll")] static extern IntPtr CreateRectRgn(int a,int b,int c,int d);
 [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr h);
 [DllImport("gdi32.dll")] static extern bool PtInRegion(IntPtr h,int x,int y);
 [DllImport("user32.dll")] static extern int GetWindowRgn(IntPtr h,IntPtr region);
 class Backdrop:Forms.Form {
  public bool Gallery,Dark;
  protected override void OnPaint(Forms.PaintEventArgs e){base.OnPaint(e);if(Gallery){using(var b=new System.Drawing.Drawing2D.LinearGradientBrush(ClientRectangle,Dark?Color.FromArgb(25,34,47):Color.FromArgb(225,234,244),Dark?Color.FromArgb(55,56,62):Color.FromArgb(243,233,227),22))e.Graphics.FillRectangle(b,ClientRectangle);return;}for(int x=0;x<ClientSize.Width;x+=8)using(var b=new SolidBrush((x/8)%2==0?Color.FromArgb(41,92,124):Color.FromArgb(224,175,125)))e.Graphics.FillRectangle(b,x,0,8,ClientSize.Height);}
 }
 static void Pump(int ms){var timer=Stopwatch.StartNew();while(timer.ElapsedMilliseconds<ms){Forms.Application.DoEvents();Thread.Sleep(10);}}
 static void Capture(Rectangle area,string file,int appId) {
  // Every sampled point must belong to this synthetic backdrop or Screenline.
  // Never take an unrestricted desktop screenshot for visual evidence.
  for(int y=area.Top;y<area.Bottom;y+=8)for(int x=area.Left;x<area.Right;x+=8){uint process;GetWindowThreadProcessId(WindowFromPoint(new Point(x,y)),out process);if(process!=Process.GetCurrentProcess().Id&&process!=appId)throw new Exception("Evidence area obscured at "+x+","+y+" by process "+process+"; capture aborted");}
  DwmFlush();using(var b=new Bitmap(area.Width,area.Height))using(var g=Graphics.FromImage(b)){g.CopyFromScreen(area.Location,Point.Empty,area.Size);b.Save(file,System.Drawing.Imaging.ImageFormat.Png);}
 }
 [STAThread] static int Main(string[] args) {
  string root=Path.GetFullPath(args[1]),evidence=Path.Combine(root,"evidence");Directory.CreateDirectory(evidence);Directory.CreateDirectory(Path.Combine(root,"data"));
  bool completed=false;string runId=Guid.NewGuid().ToString("N");
  File.WriteAllText(Path.Combine(evidence,"latest-run.json"),new JavaScriptSerializer().Serialize(new{run_id=runId,status="running"}));
  File.WriteAllText(Path.Combine(root,"data/settings.json"),"{\"ReducedMotion\":true,\"Pinned\":true,\"MonitorClipboard\":false,\"SystemMaterial\":true,\"Files\":[]}");
  string image=Path.Combine(root,"landscape.png");Fixtures.Create(image,1);var screen=Forms.Screen.FromPoint(Forms.Cursor.Position);
  bool gallery=args.Length>2&&(args[2]=="gallery"||args[2]=="render");
  var backdrop=new Backdrop{Gallery=gallery,FormBorderStyle=Forms.FormBorderStyle.None,Bounds=screen.Bounds,StartPosition=Forms.FormStartPosition.Manual,TopMost=true,ShowInTaskbar=false,AutoScaleMode=Forms.AutoScaleMode.None};backdrop.Show();
  Rect backgroundRect;GetWindowRect(backdrop.Handle,out backgroundRect);Console.WriteLine(new JavaScriptSerializer().Serialize(new{fixture=backgroundRect,screen=screen.Bounds.ToString(),test_process=Process.GetCurrentProcess().Id}));
  Process app=null;int result=0;
  try {
   string imports="\""+image+"\"";
   if(gallery){string notes=Path.Combine(root,"notes.png"),portrait=Path.Combine(root,"portrait.png");Fixtures.Create(notes,0);Fixtures.Create(portrait,3);imports="\""+portrait+"\" \""+image+"\" \""+notes+"\"";}
   app=Process.Start(new ProcessStartInfo(args[0],"--data-dir \""+Path.Combine(root,"data")+"\" --qa-dir \""+evidence+"\" "+imports){UseShellExecute=false});
   Pump(1700);File.WriteAllText(Path.Combine(evidence,"render.request"),"");Pump(350);
   var json=new JavaScriptSerializer();var state=json.Deserialize<System.Collections.Generic.Dictionary<string,object>>(File.ReadAllText(Path.Combine(evidence,"ui-state.json")));
   if(gallery&&args[2]=="render"){completed=true;Console.WriteLine("WPF component render saved; no native gallery screenshot requested");return 0;}
   if(gallery){SetWindowPos(backdrop.Handle,new IntPtr(-1),screen.Bounds.Left,screen.Bounds.Top,screen.Bounds.Width,screen.Bounds.Height,0x10);SetWindowPos(new IntPtr(Convert.ToInt64(state["material_handle"])),new IntPtr(-1),0,0,0,0,0x13);SetWindowPos(FindWindow(null,"Screenline · 截图晾台"),new IntPtr(-1),0,0,0,0,0x13);Pump(200);int width=Math.Min(1200,screen.WorkingArea.Width);var galleryArea=new Rectangle(screen.WorkingArea.Left+(screen.WorkingArea.Width-width)/2,screen.WorkingArea.Top+20,width,350);Capture(galleryArea,Path.Combine(evidence,"gallery-light.png"),app.Id);backdrop.Dark=true;backdrop.Invalidate();Pump(300);Capture(galleryArea,Path.Combine(evidence,"gallery-dark.png"),app.Id);completed=true;Console.WriteLine("Native composition gallery saved over synthetic backgrounds");return 0;}
   long h=Convert.ToInt64(state["material_handle"]);var area=new Rectangle(screen.WorkingArea.Left+screen.WorkingArea.Width/2-220,screen.WorkingArea.Top+20,440,320);
   var shelf=FindWindow(null,"Screenline · 截图晾台");long style=GetWindowLongPtr(shelf,-20).ToInt64();
   SetWindowLongPtr(shelf,-20,new IntPtr(style&~0x20));
   var center=new Point(screen.WorkingArea.Left+screen.WorkingArea.Width/2,screen.WorkingArea.Top+160);var hit=WindowFromPoint(center);uint hitPid;GetWindowThreadProcessId(hit,out hitPid);
   Console.WriteLine(json.Serialize(new{input_probe=new{point=center.ToString(),hit=hit.ToInt64(),pid=hitPid,shelf=shelf.ToInt64(),material=h,style=style,shelf_test=SendMessage(shelf,0x84,IntPtr.Zero,new IntPtr((center.Y<<16)|(center.X&65535))).ToInt64()}}));
   SetWindowLongPtr(shelf,-20,new IntPtr(style));
   IntPtr region=CreateRectRgn(0,0,0,0);int regionType=GetWindowRgn(new IntPtr(h),region);Console.WriteLine(json.Serialize(new{region_type=regionType,gap_in_region=PtInRegion(region,100,100),photo_in_region=PtInRegion(region,1600,150)}));DeleteObject(region);
   string on=Path.Combine(evidence,runId+"-on.png"),off=Path.Combine(evidence,runId+"-off.png");
   Capture(area,on,app.Id);ShowWindow(new IntPtr(h),0);Pump(150);Capture(area,off,app.Id);
   File.Copy(on,Path.Combine(evidence,"material-on.png"),true);File.Copy(off,Path.Combine(evidence,"material-off.png"),true);File.Copy(Path.Combine(evidence,"shelf-clean.png"),Path.Combine(evidence,"material-alpha.png"),true);
   state["sample_area"]=new[]{area.X-screen.WorkingArea.Left,area.Y-screen.WorkingArea.Top,area.Width,area.Height};
   state["run_id"]=runId;File.WriteAllText(Path.Combine(evidence,"material-state.json"),json.Serialize(state));completed=true;Console.WriteLine(json.Serialize(new{material_applied=state["material_applied"],result=state["material_result"],allowed=state["material_allowed"],area=area.ToString()}));
  }catch(Exception ex){Console.WriteLine(ex);result=1;}
  finally {File.WriteAllText(Path.Combine(evidence,"latest-run.json"),new JavaScriptSerializer().Serialize(new{run_id=runId,status=completed?"passed":"incomplete",reason=completed?null:"Synthetic test area was obscured; no completed current capture pair."}));if(app!=null&&!app.HasExited){app.Kill();app.WaitForExit();}backdrop.Close();}
  return result;
 }
}
