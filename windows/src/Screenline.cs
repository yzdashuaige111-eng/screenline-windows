// Screenline: an independently implemented Windows adaptation of Tendedero.
// MIT. See LICENSE and THIRD-PARTY-NOTICES.txt.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

[assembly: AssemblyTitle("Screenline")]
[assembly: AssemblyDescription("A native Windows screenshot clothesline")]
[assembly: AssemblyVersion("0.4.1.0")]
[assembly: AssemblyFileVersion("0.4.1.0")]

namespace Screenline {
    public sealed class Settings {
        public bool MonitorClipboard = true;
        public bool ReducedMotion = false;
        public bool SystemMaterial = true;
        public bool Pinned = false;
        public string WatchFolder = "";
        public List<string> Files = new List<string>();
    }
    public sealed class Clip {
        public string File, Hash;
        public BitmapSource Thumbnail;
        public int Width, Height;
        internal WeakReference Original;
        public BitmapSource ReadOriginal() {
            var bitmap=Original==null?null:Original.Target as BitmapSource;
            if(bitmap!=null)return bitmap;
            bitmap=ClipStore.ReadImage(File);Original=new WeakReference(bitmap);return bitmap;
        }
    }
    public sealed class ClipStore {
        public readonly string Root, Images;
        public Settings Settings;
        public readonly List<Clip> Items = new List<Clip>();
        public Action Changed;
        readonly JavaScriptSerializer json = new JavaScriptSerializer();
        readonly SemaphoreSlim writes=new SemaphoreSlim(1,1);
        int pending;
        TaskCompletionSource<bool> idle;
        public bool IsBusy {get{return pending>0;}}
        public Task WhenIdle {get{return idle==null?Task.FromResult(true):idle.Task;}}
        public ClipStore(string root) {
            Root = Path.GetFullPath(root); Images = Path.Combine(Root, "images");
            Directory.CreateDirectory(Images);
            Settings = new Settings();
            var cfg = Path.Combine(Root, "settings.json");
            if (File.Exists(cfg)) try { Settings = json.Deserialize<Settings>(File.ReadAllText(cfg)) ?? Settings; }
                catch (Exception) { File.Copy(cfg, cfg + ".invalid-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true); }
            if (Settings.Files == null) Settings.Files = new List<string>();
            foreach (string name in Settings.Files.ToArray()) {
                if (!ValidName(name)) continue;
                string path = Path.Combine(Images, name);
                if (!File.Exists(path)) continue;
                try { Items.Add(LoadClip(path)); } catch (Exception) { }
            }
            Save();
        }
        public static BitmapSource ReadImage(string path, int decode = 0) {
            using (var s = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
                var b = new BitmapImage(); b.BeginInit(); b.CacheOption = BitmapCacheOption.OnLoad;
                if (decode > 0) b.DecodePixelWidth = decode;
                b.StreamSource = s; b.EndInit(); b.Freeze(); return b;
            }
        }
        public static byte[] Png(BitmapSource source) {
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(source));
            using (var ms = new MemoryStream()) { enc.Save(ms); return ms.ToArray(); }
        }
        static string Digest(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
        static string PixelDigest(BitmapSource source) {
            var normalized=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);
            int stride=checked(source.PixelWidth*4);byte[] pixels=new byte[checked(stride*source.PixelHeight+8)];
            normalized.CopyPixels(new Int32Rect(0,0,source.PixelWidth,source.PixelHeight),pixels,stride,8);
            Array.Copy(BitConverter.GetBytes(source.PixelWidth),0,pixels,0,4);Array.Copy(BitConverter.GetBytes(source.PixelHeight),0,pixels,4,4);
            return Digest(pixels);
        }
        Clip LoadClip(string path) {
            var full = ReadImage(path);
            return new Clip { File = path, Hash = PixelDigest(full), Width = full.PixelWidth,
                Height = full.PixelHeight, Original=new WeakReference(full), Thumbnail = ReadImage(path, Math.Max(1,(int)Math.Round(full.PixelWidth*Math.Min(1,560.0/Math.Max(full.PixelWidth,full.PixelHeight))))) };
        }
        static bool Validate(BitmapSource source) {
            if(source==null||source.PixelWidth<1||source.PixelHeight<1)return false;
            if((long)source.PixelWidth*source.PixelHeight>100000000)throw new InvalidOperationException("图片超过 1 亿像素，请先缩小后导入。");
            return true;
        }
        Clip Prepare(BitmapSource source,string hash) {
            string path=Path.Combine(Images,Guid.NewGuid().ToString("N")+".png");
            // Copy only the small thumbnail; a TransformedBitmap alone would retain the full image.
            double ratio=Math.Min(1,560.0/Math.Max(source.PixelWidth,source.PixelHeight));
            BitmapSource scaled=ratio<1?new TransformedBitmap(source,new ScaleTransform(ratio,ratio)):source;
            var normalized=new FormatConvertedBitmap(scaled,PixelFormats.Pbgra32,null,0);
            int width=normalized.PixelWidth,height=normalized.PixelHeight,stride=width*4;
            var pixels=new byte[stride*height];normalized.CopyPixels(pixels,stride,0);
            var thumbnail=BitmapSource.Create(width,height,96,96,PixelFormats.Pbgra32,null,pixels,stride);thumbnail.Freeze();
            File.WriteAllBytes(path,Png(source));
            return new Clip{File=path,Hash=hash,Width=source.PixelWidth,Height=source.PixelHeight,Thumbnail=thumbnail,Original=new WeakReference(source)};
        }
        Clip Commit(Clip clip) {
            Items.Insert(0,clip);
            while(Items.Count>60)Items.RemoveAt(Items.Count-1);
            Save();if(Changed!=null)Changed();return clip;
        }
        public Clip Add(BitmapSource source) {
            if(!Validate(source))return null;
            if(!source.IsFrozen){source=source.Clone();source.Freeze();}
            string hash=PixelDigest(source);
            var duplicate = Items.FirstOrDefault(x => x.Hash == hash);
            if (duplicate != null) return duplicate;
            return Commit(Prepare(source,hash));
        }
        public Task<Clip> AddAsync(BitmapSource source) {
            if(source!=null&&!source.IsFrozen){source=source.Clone();source.Freeze();}
            return AddAsyncCore(delegate{return source;});
        }
        public Task<Clip> ImportAsync(string path){return AddAsyncCore(delegate{return ReadImage(path);});}
        async Task<Clip> AddAsyncCore(Func<BitmapSource> read) {
            if(pending++==0)idle=new TaskCompletionSource<bool>();
            await writes.WaitAsync();
            try {
                var source=await Task.Run(read);if(!Validate(source))return null;
                string hash=await Task.Run(delegate{return PixelDigest(source);});
                var duplicate=Items.FirstOrDefault(x=>x.Hash==hash);if(duplicate!=null)return duplicate;
                var clip=await Task.Run(delegate{return Prepare(source,hash);});
                // A synchronous editor save can finish while a background import is preparing.
                duplicate=Items.FirstOrDefault(x=>x.Hash==hash);
                if(duplicate!=null){File.Delete(clip.File);return duplicate;}
                return Commit(clip);
            }finally{writes.Release();if(--pending==0)idle.TrySetResult(true);}
        }
        public Clip Import(string path) { return Add(ReadImage(path)); }
        static bool ValidName(string name) {
            if (name == null || name.Length != 36 || !name.EndsWith(".png", StringComparison.Ordinal)) return false;
            return name.Substring(0, 32).All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));
        }
        public void Remove(Clip clip) {
            Items.Remove(clip); Save();
            // Imported originals are never touched; only our own generated cache names can be deleted.
            string path = Path.GetFullPath(clip.File);
            if (Path.GetDirectoryName(path).Equals(Images, StringComparison.OrdinalIgnoreCase) &&
                ValidName(Path.GetFileName(path)) && File.Exists(path) &&
                (File.GetAttributes(Images) & FileAttributes.ReparsePoint) == 0 &&
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0) File.Delete(path);
            if (Changed != null) Changed();
        }
        public void PruneMoved() {
            if (Items.RemoveAll(c => !File.Exists(c.File)) > 0) { Save(); if (Changed != null) Changed(); }
        }
        public void Save() {
            Settings.Files = Items.Select(c => Path.GetFileName(c.File)).ToList();
            string target = Path.Combine(Root, "settings.json"), temp = target + ".tmp";
            File.WriteAllText(temp, json.Serialize(Settings), new UTF8Encoding(false));
            if (File.Exists(target)) File.Replace(temp, target, target + ".bak"); else File.Move(temp, target);
        }
    }
    static class Native {
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X,Y; }
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left,Top,Right,Bottom; }
        [DllImport("user32.dll")] public static extern bool AddClipboardFormatListener(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint mod, uint key);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT r);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr insert, int x, int y, int w, int h, uint flags);
        [DllImport("user32.dll")] public static extern uint GetClipboardSequenceNumber();
        [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr hwnd,int index);
        [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr hwnd,int index,IntPtr value);
        public static void SetStyle(IntPtr hwnd,long bits,bool enabled) {
            long current=GetWindowLongPtr(hwnd,-20).ToInt64(),next=enabled?current|bits:current&~bits;
            if(next!=current)SetWindowLongPtr(hwnd,-20,new IntPtr(next));
        }
        public static double Scale(IntPtr hwnd) { try { return Math.Max(1, GetDpiForWindow(hwnd) / 96.0); } catch (EntryPointNotFoundException) { return 1; } }
    }
    public sealed class ShelfWindow : Window {
        public readonly ClipStore Store;
        readonly bool qa;
        readonly string qaDir;
        readonly Canvas stage;
        readonly RopeView rope;
        readonly TextBlock status;
        readonly Border notice;
        readonly DispatcherTimer noticeTimer;
        readonly Dictionary<string,HangingPhoto> photos=new Dictionary<string,HangingPhoto>(StringComparer.OrdinalIgnoreCase);
        readonly TranslateTransform reveal=new TranslateTransform();
        readonly Button empty,older,newer;
        readonly DispatcherTimer cursorTimer, clipboardTimer;
        Forms.NotifyIcon tray;
        HwndSource hwndSource;
        IntPtr handle;
        FileSystemWatcher watcher;
        readonly HashSet<string> pendingFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        DateTime lastInside = DateTime.Now, edgeSince = DateTime.MinValue, lastKeyboard = DateTime.MinValue, peekUntil=DateTime.MinValue, nextBreeze=DateTime.Now.AddSeconds(8);
        bool dragging, closing, clipboardRegistered, retracting, intentional, passThrough, clipboardReading, captureProcessing;
        int page;
        long revealRevision;
        int qaFrame;
        DateTime lastQaFrame=DateTime.MinValue;
        uint clipboardSequence;
        int clipboardRetries;
        Window viewer;
        RegionCapture capture;
        CaptureFlight flight;
        DesktopMaterial material;
        long materialUntil;
        readonly Stopwatch motionClock=Stopwatch.StartNew();
        DateTime lastMaterialCheck=DateTime.MinValue;
        Rect lastCapture=Rect.Empty;
        int flights;
        public ShelfWindow(ClipStore store, bool isQa, string evidence) {
            Store=store; qa=isQa; qaDir=evidence;
            Title="Screenline · 截图晾台"; Width=1200; Height=210; WindowStyle=WindowStyle.None;
            ResizeMode=ResizeMode.NoResize; AllowsTransparency=true; Background=Brushes.Transparent;
            Topmost=true; ShowInTaskbar=false; ShowActivated=false; FontFamily=new FontFamily("Microsoft YaHei UI");
            using(var icon=MakeIcon())Icon=Imaging.CreateBitmapSourceFromHIcon(icon.Handle,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());
            FontSize=13; Foreground=B("#30343B");UseLayoutRounding=true;
            stage=new Canvas {Background=null,RenderTransform=reveal};Content=stage;
            rope=new RopeView {Width=Width,Height=80};stage.Children.Add(rope);
            var invitation=new StackPanel{Orientation=Orientation.Horizontal};invitation.Children.Add(new System.Windows.Shapes.Path{Data=Geometry.Parse("M4,1 L4,5 L1,5 M11,1 L11,5 L14,5 M1,10 L4,10 L4,14 M14,10 L11,10 L11,14"),Stroke=B("#49574D"),StrokeThickness=1.1,Width=14,Height=14,Margin=new Thickness(0,0,9,0),VerticalAlignment=VerticalAlignment.Center});invitation.Children.Add(new TextBlock{Text="截取一块屏幕",VerticalAlignment=VerticalAlignment.Center});invitation.Children.Add(new TextBlock{Text="Ctrl Alt S",FontSize=11,Foreground=B("#626F60"),Margin=new Thickness(18,0,0,0),VerticalAlignment=VerticalAlignment.Center});
            empty=new Button{Content=invitation,FontSize=12,FontWeight=FontWeights.Medium,
                Background=B("#F4F8FAF6"),Foreground=B("#3B453D"),BorderBrush=B("#BFFFFFFF"),BorderThickness=new Thickness(.5),
                Padding=new Thickness(14,9,14,9),Template=ButtonTemplate(),Cursor=Cursors.Hand};
            AutomationProperties.SetName(empty,"开始区域截图，也可以把图片拖到这里");AutomationProperties.SetAutomationId(empty,"EmptyCapture");
            empty.Click+=delegate{Snip();};stage.Children.Add(empty);
            newer=PageButton("‹","Newer","较新的截图",delegate{page=Math.Max(0,page-1);Rebuild();});
            older=PageButton("›","Older","较早的截图",delegate{page++;Rebuild();});stage.Children.Add(newer);stage.Children.Add(older);
            status=new TextBlock{Text="",Foreground=B("#434B58"),FontSize=12,TextTrimming=TextTrimming.CharacterEllipsis,MaxWidth=560};AutomationProperties.SetAutomationId(status,"Status");
            notice=new Border{Child=status,Padding=new Thickness(14,7,14,7),CornerRadius=new CornerRadius(14),Background=B("#F8FAFBFD"),BorderBrush=B("#F0FFFFFF"),BorderThickness=new Thickness(.75),Visibility=Visibility.Hidden,IsHitTestVisible=false};
            Panel.SetZIndex(notice,20);stage.Children.Add(notice);noticeTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(4)};noticeTimer.Tick+=delegate{noticeTimer.Stop();notice.Visibility=Visibility.Hidden;};
            AllowDrop=true; DragOver+=OnDragOver; Drop+=OnDrop;
            PreviewKeyDown+=delegate(object sender, KeyEventArgs e) {
                lastKeyboard=DateTime.Now;
                if(e.Key==Key.Escape) {HideShelf(); e.Handled=true;}
                if(e.Key==Key.V && Keyboard.Modifiers==ModifierKeys.Control) {ReadClipboard();e.Handled=true;}
                if(e.Key==Key.Left||e.Key==Key.Right){var buttons=photos.Values.Where(p=>p.Root.Visibility==Visibility.Visible).OrderBy(p=>Canvas.GetLeft(p.Root)).Select(p=>p.Button).ToList();int at=buttons.FindIndex(b=>b.IsKeyboardFocused);if(buttons.Count>0)buttons[(at+(e.Key==Key.Right?1:buttons.Count-1)+buttons.Count)%buttons.Count].Focus();e.Handled=true;}
            };
            PreviewMouseWheel+=delegate(object sender,MouseWheelEventArgs e){if(Store.Items.Count>Capacity){page+=e.Delta<0?1:-1;Rebuild();e.Handled=true;}};
            Store.Changed=delegate{if(Store.Items.Count>0&&!photos.ContainsKey(Store.Items[0].File))page=0;Rebuild();};
            cursorTimer=new DispatcherTimer(DispatcherPriority.Normal) {Interval=TimeSpan.FromMilliseconds(33)}; cursorTimer.Tick+=delegate(object sender,EventArgs e){
                if(motionClock.ElapsedMilliseconds<materialUntil||(DateTime.Now-lastMaterialCheck).TotalMilliseconds>=250){lastMaterialCheck=DateTime.Now;RenderMaterial(sender,e);}
                CursorTick(sender,e);
            };
            Motion.Started+=TrackMaterial;
            clipboardTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(110)};
            clipboardTimer.Tick+=delegate {clipboardTimer.Stop(); ReadClipboard();};
            SourceInitialized+=OnReady;
            Closed+=OnClosed;
            SizeChanged+=delegate{rope.Width=ActualWidth;Rebuild();};
            Loaded+=delegate {Rebuild();SetupWatcher();peekUntil=DateTime.Now.AddSeconds(Store.Items.Count==0?5:2.5);if(qa){WriteQaState();Dispatcher.BeginInvoke(new Action(WriteQaRender),DispatcherPriority.ContextIdle);}else ShowShelf(false);};
        }
        bool Animate {get{return !Store.Settings.ReducedMotion&&SystemParameters.ClientAreaAnimation;}}
        int Capacity {get{return Math.Max(1,Math.Min(12,(int)((Width-160)/HangingStyle.Spacing)));}}
        Button PageButton(string glyph,string id,string name,Action action) {
            var b=new Button{Content=glyph,Width=28,Height=28,FontSize=22,FontFamily=new FontFamily("Segoe UI"),Padding=new Thickness(0),Background=B("#EDF5F6F8"),Foreground=B("#454B55"),BorderBrush=B("#EBFFFFFF"),BorderThickness=new Thickness(.75),Template=HangingPhoto.RoundTemplate(),Visibility=Visibility.Collapsed,Cursor=Cursors.Hand,ToolTip=name};
            AutomationProperties.SetAutomationId(b,id);AutomationProperties.SetName(b,name);b.Click+=delegate{action();};return b;
        }
        public static SolidColorBrush B(string hex) { var brush=(SolidColorBrush)new BrushConverter().ConvertFromString(hex); brush.Freeze(); return brush; }
        public static Button ActionButton(string text,string id,Action action) {
            var b=new Button {Content=text, Padding=new Thickness(12,7,12,7), MinHeight=34, Margin=new Thickness(3,0,0,0),
                Foreground=B("#354332"), Background=B("#FBFCF8"), BorderBrush=B("#D5DDD1"), BorderThickness=new Thickness(.75), Cursor=Cursors.Hand};
            b.Template=ButtonTemplate();AutomationProperties.SetName(b,text); AutomationProperties.SetAutomationId(b,id); b.Click+=delegate{action();}; return b;
        }
        public static ControlTemplate ButtonTemplate(bool primary=false) {
            var border=new FrameworkElementFactory(typeof(Border));border.Name="Chrome";
            border.SetValue(Border.CornerRadiusProperty,new CornerRadius(7));
            border.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty,new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty,new TemplateBindingExtension(Control.BorderThicknessProperty));
            border.SetValue(Border.PaddingProperty,new TemplateBindingExtension(Control.PaddingProperty));
            var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(ContentPresenter.ContentProperty,new TemplateBindingExtension(ContentControl.ContentProperty));
            presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);presenter.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);
            border.AppendChild(presenter);var template=new ControlTemplate(typeof(Button)){VisualTree=border};
            var hover=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(Border.BackgroundProperty,B(primary?"#43644D":"#E9EEE5"),"Chrome"));template.Triggers.Add(hover);
            var press=new Trigger{Property=Button.IsPressedProperty,Value=true};press.Setters.Add(new Setter(Border.BackgroundProperty,B(primary?"#293E30":"#DAE3D5"),"Chrome"));template.Triggers.Add(press);
            var focus=new Trigger{Property=UIElement.IsKeyboardFocusedProperty,Value=true};focus.Setters.Add(new Setter(Border.BorderBrushProperty,B("#3F7350"),"Chrome"));template.Triggers.Add(focus);return template;
        }
        void OnReady(object sender,EventArgs e) {
            handle=new WindowInteropHelper(this).Handle; hwndSource=HwndSource.FromHwnd(handle); hwndSource.AddHook(Hook);
            Native.SetStyle(handle,0x80|0x08000000,true);
            clipboardRegistered=Native.AddClipboardFormatListener(handle);
            clipboardSequence=Native.GetClipboardSequenceNumber();
            bool toggle=Native.RegisterHotKey(handle,1,0x4000|0x2|0x1,0x54);
            bool snip=Native.RegisterHotKey(handle,2,0x4000|0x2|0x1,0x53);
            if (!toggle || !snip) SetStatus("快捷键已被占用，仍可使用托盘菜单和顶部边缘");
            if (!clipboardRegistered) SetStatus("剪贴板监听注册失败，可用 Ctrl+V 手动导入");
            SetupTray(); Place(); cursorTimer.Start();
            material=new DesktopMaterial(handle);
        }
        void RenderMaterial(object sender,EventArgs e) {
            if(material==null)return;
            try{bool enabled=Store.Settings.SystemMaterial&&DesktopMaterial.UserAllowsTransparency&&material.Applied;
                foreach(var photo in photos.Values)photo.Material(enabled);
                bool visible=enabled&&IsVisible&&capture==null&&viewer==null;
                if(!visible){material.Hide();return;}
                double dpi=Native.Scale(handle);
                var shapes=photos.Values.Where(p=>p.Root.Visibility==Visibility.Visible&&p.Root.Opacity>.95&&p.Button.ActualWidth>0).Select(p=>p.MaterialOutline(this,dpi));
                material.Update(shapes,true);
            }catch(Exception ex){Log(ex);material.Dispose();material=null;foreach(var photo in photos.Values)photo.Material(false);}
        }
        void TrackMaterial(double milliseconds) {
            if(closing)return;materialUntil=Math.Max(materialUntil,motionClock.ElapsedMilliseconds+(long)milliseconds+50);
        }
        IntPtr Hook(IntPtr h,int msg,IntPtr w,IntPtr l,ref bool handled) {
            if(msg==0x0021){handled=true;return new IntPtr(3);} // Photos do not take focus from the destination app.
            if(msg==0x0084&&!dragging) {
                long packed=l.ToInt64();Point at=PointFromScreen(new Point((short)(packed&0xffff),(short)((packed>>16)&0xffff)));
                handled=true;return new IntPtr(Interactive(at)?1:-1);
            }
            if(msg==0x007E||msg==0x02E0)Dispatcher.BeginInvoke(new Action(Place),DispatcherPriority.Loaded);
            if(msg==0x031D && Store.Settings.MonitorClipboard) {
                clipboardRetries=0;clipboardTimer.Stop();clipboardTimer.Start();
            }
            if(msg==0x0312) { if(w.ToInt32()==1) {if(IsVisible)HideShelf();else ShowShelf(true);} else if(w.ToInt32()==2)Snip(); handled=true; }
            return IntPtr.Zero;
        }
        void SetupTray() {
            tray=new Forms.NotifyIcon {Text="Screenline · 截图晾台", Icon=MakeIcon(), Visible=true};
            var menu=new Forms.ContextMenuStrip();
            menu.Items.Add("展开 / 收起  Ctrl+Alt+T",null,delegate {Dispatcher.Invoke(new Action(delegate {if(IsVisible)HideShelf();else ShowShelf(true);}));});
            menu.Items.Add("区域截图  Ctrl+Alt+S",null,delegate {Dispatcher.Invoke(new Action(Snip));});
            menu.Items.Add("Windows 系统截图  Win+Shift+S",null,delegate {Dispatcher.Invoke(new Action(SystemSnip));});
            menu.Items.Add("导入图片…",null,delegate {Dispatcher.Invoke(new Action(delegate{ShowShelf(true);ImportDialog();}));});
            var pin=new Forms.ToolStripMenuItem("保持展开"){Checked=Store.Settings.Pinned,CheckOnClick=true};
            pin.Click+=delegate{Dispatcher.Invoke(new Action(delegate{Store.Settings.Pinned=pin.Checked;Store.Save();if(pin.Checked)ShowShelf(false);}));};menu.Items.Add(pin);
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add("设置",null,delegate {Dispatcher.Invoke(new Action(ShowSettings));});
            menu.Items.Add("打开图片缓存",null,delegate {Process.Start(new ProcessStartInfo(Store.Images){UseShellExecute=true});});
            menu.Items.Add("退出",null,delegate {Dispatcher.Invoke(new Action(Close));});
            tray.ContextMenuStrip=menu;tray.DoubleClick+=delegate{Dispatcher.Invoke(new Action(delegate{ShowShelf(true);}));};
        }
        static Drawing.Icon MakeIcon() {
            try{return Drawing.Icon.ExtractAssociatedIcon(typeof(Program).Assembly.Location);}catch(Exception){return (Drawing.Icon)Drawing.SystemIcons.Application.Clone();}
        }
        void Place() {
            Native.POINT p;Native.GetCursorPos(out p);var screen=Forms.Screen.FromPoint(new Drawing.Point(p.X,p.Y));
            double scale=Native.Scale(handle);Width=Math.Max(320,screen.WorkingArea.Width/scale);
            int width=(int)Math.Round(Width*scale),height=(int)Math.Round(Height*scale);
            Native.SetWindowPos(handle,new IntPtr(-1),screen.WorkingArea.Left,screen.WorkingArea.Top,width,height,0x10);
        }
        bool Fullscreen() {
            var fg=Native.GetForegroundWindow(); if(fg==IntPtr.Zero || fg==handle)return false;
            var cls=new StringBuilder(100);Native.GetClassName(fg,cls,100);
            if(new[]{"Progman","WorkerW","Shell_TrayWnd"}.Contains(cls.ToString()))return false;
            Native.RECT rect;if(!Native.GetWindowRect(fg,out rect))return false;
            var screen=Forms.Screen.FromHandle(fg).Bounds;
            return rect.Left<=screen.Left && rect.Top<=screen.Top && rect.Right>=screen.Right && rect.Bottom>=screen.Bottom;
        }
        void CursorTick(object sender,EventArgs e) {
            if(qa)QaRequests();
            if(dragging || viewer!=null || capture!=null || flight!=null || Mouse.Captured!=null || IsContextMenuOpen()) {lastInside=DateTime.Now;return;}
            Native.POINT p;Native.GetCursorPos(out p);var screen=Forms.Screen.FromPoint(new Drawing.Point(p.X,p.Y));
            if(qa){if(IsVisible&&!retracting)UpdatePassThrough(p);return;}
            if(Fullscreen()) {if(IsVisible)HideShelf();return;}
            if(!IsVisible || retracting) {
                if(p.Y<=screen.Bounds.Top+2) {if(edgeSince==DateTime.MinValue)edgeSince=DateTime.Now;else if((DateTime.Now-edgeSince).TotalMilliseconds>280)ShowShelf(false);}
                else edgeSince=DateTime.MinValue;
            } else {
                UpdatePassThrough(p);
                Native.RECT r;Native.GetWindowRect(handle,out r);
                bool inside=p.X>=r.Left&&p.X<=r.Right&&p.Y>=r.Top-5&&p.Y<=r.Bottom;
                if(inside){lastInside=DateTime.Now;intentional=false;}
                else if(!Store.Settings.Pinned&&!intentional&&DateTime.Now>peekUntil&&(DateTime.Now-lastInside).TotalMilliseconds>500)HideShelf();
                if(Animate&&DateTime.Now>nextBreeze){nextBreeze=DateTime.Now.AddSeconds(8);int i=0;foreach(var photo in photos.Values.Where(v=>v.Root.Visibility==Visibility.Visible))photo.Nudge((i++%2==0?1:-1)*1.8);}
            }
        }
        void UpdatePassThrough(Native.POINT point){bool through=!Interactive(PointFromScreen(new Point(point.X,point.Y)));if(through!=passThrough){passThrough=through;Native.SetStyle(handle,0x20,through);}}
        bool Interactive(Point point) {
            if(retracting||!IsVisible)return false;
            foreach(var photo in photos.Values)if(photo.Root.Visibility==Visibility.Visible&&photo.HitRect(stage).Contains(point))return true;
            foreach(var button in new[]{empty,newer,older})if(button.Visibility==Visibility.Visible&&new Rect(Canvas.GetLeft(button),Canvas.GetTop(button),button.ActualWidth,button.ActualHeight).Contains(point))return true;
            return false;
        }
        bool IsContextMenuOpen() { return photos.Values.Any(c=>c.Button.ContextMenu!=null&&c.Button.ContextMenu.IsOpen); }
        public void ShowShelf(bool activate) {
            if(closing)return;bool coming=!IsVisible||retracting;retracting=false;revealRevision++;Place();Show();lastInside=DateTime.Now;edgeSince=DateTime.MinValue;intentional=activate;
            cursorTimer.Interval=TimeSpan.FromMilliseconds(16);
            passThrough=false;Native.SetStyle(handle,0x20,false);
            if(coming&&Animate)Motion.To(reveal,TranslateTransform.YProperty,-Height-12,0,420,true);
            else {reveal.BeginAnimation(TranslateTransform.YProperty,null);reveal.Y=0;}
            if(activate){Native.SetStyle(handle,0x08000000,false);Activate();var first=photos.Values.FirstOrDefault(v=>v.Root.Visibility==Visibility.Visible);if(first!=null)first.Button.Focus();}
            else Native.SetStyle(handle,0x08000000,true);
            TrackMaterial(1);WriteQaState();
        }
        public void HideShelf() {
            if(retracting||!IsVisible)return;intentional=false;edgeSince=DateTime.MinValue;peekUntil=DateTime.MinValue;
            if(!Animate){HideNow();return;}
            retracting=true;long revision=++revealRevision;passThrough=true;Native.SetStyle(handle,0x20,true);
            TrackMaterial(240);var animation=new DoubleAnimation(reveal.Y,-Height-12,TimeSpan.FromMilliseconds(220)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseIn}};
            animation.Completed+=delegate{if(revision==revealRevision){Hide();retracting=false;cursorTimer.Interval=TimeSpan.FromMilliseconds(80);WriteQaState();}};reveal.BeginAnimation(TranslateTransform.YProperty,animation);
        }
        void HideNow(){revealRevision++;retracting=false;reveal.BeginAnimation(TranslateTransform.YProperty,null);reveal.Y=0;if(material!=null)material.Hide();Hide();cursorTimer.Interval=TimeSpan.FromMilliseconds(80);edgeSince=DateTime.MinValue;WriteQaState();}
        void SetStatus(string text) {
            status.Text=text;
            if(text.Contains("失败")||text.Contains("占用")||text.Contains("尚未")||text.Contains("未完成")||text.StartsWith("请")) {
                notice.Visibility=Visibility.Visible;notice.Measure(new Size(Width,80));Canvas.SetLeft(notice,(Width-notice.DesiredSize.Width)/2);Canvas.SetTop(notice,170);noticeTimer.Stop();noticeTimer.Start();
                if(Animate)Motion.Fade(notice,0,1,150);
            }
            WriteQaState();
        }
        async void ReadClipboard() {
            if(clipboardReading||closing)return;clipboardReading=true;uint requested=Native.GetClipboardSequenceNumber();
            try {
                if(Clipboard.ContainsData("Screenline.Origin"))return;
                if(!Clipboard.ContainsImage())return;
                var clip=await Store.AddAsync(Clipboard.GetImage());clipboardSequence=requested;if(closing)return;
                if(clip!=null) {SetStatus("已收纳图片 · "+clip.Width+" × "+clip.Height);peekUntil=DateTime.Now.AddSeconds(2.5);if(viewer==null&&!Fullscreen())ShowShelf(false);}
            } catch(ExternalException) { if(clipboardRetries++<6)clipboardTimer.Start();else SetStatus("剪贴板暂时被占用，请稍后按 Ctrl+V"); }
            catch(Exception ex) {Log(ex);SetStatus("图片读取失败："+ex.Message);}
            finally{clipboardReading=false;if(!closing&&Store.Settings.MonitorClipboard&&Native.GetClipboardSequenceNumber()!=requested){clipboardTimer.Stop();clipboardTimer.Start();}}
        }
        public async void CopyClip(Clip clip) {
            try {var data=await DragDataAsync(clip);if(closing)return;Clipboard.SetDataObject(data,true);SetStatus("已复制图片，可以粘贴到聊天、文档或编辑器");HangingPhoto photo;if(photos.TryGetValue(clip.File,out photo))photo.Copied();}
            catch(ExternalException) {SetStatus("剪贴板被占用，请稍后重试");}
            catch(Exception ex){Log(ex);SetStatus("图片读取失败，请重试");}
        }
        public static DataObject DragData(Clip clip) {
            return DragData(clip,clip.ReadOriginal(),File.ReadAllBytes(clip.File));
        }
        static async Task<DataObject> DragDataAsync(Clip clip) {
            var bitmap=await Task.Run(delegate{return clip.ReadOriginal();});
            var png=await Task.Run(delegate{return File.ReadAllBytes(clip.File);});
            return DragData(clip,bitmap,png);
        }
        static DataObject DragData(Clip clip,BitmapSource bitmap,byte[] png) {
            var data=new DataObject(); data.SetImage(bitmap);
            var files=new System.Collections.Specialized.StringCollection();files.Add(clip.File);data.SetFileDropList(files);
            data.SetData("PNG",new MemoryStream(png,false));
            data.SetData("Screenline.Origin",clip.Hash);return data;
        }
        void SystemSnip() {
            HideNow();try {Process.Start(new ProcessStartInfo("ms-screenclip:"){UseShellExecute=true});}
            catch(Exception ex){Log(ex);ShowShelf(true);SetStatus("请按 Win+Shift+S 使用系统截图");}
        }
        void Snip() {
            if(capture!=null||flight!=null||captureProcessing)return;
            if(viewer!=null)viewer.Close();IntPtr previous=Native.GetForegroundWindow();HideNow();
            capture=new RegionCapture(async delegate(BitmapSource image,Rect region) {
                captureProcessing=true;
                try {
                    lastCapture=region;var clip=await Store.AddAsync(image);if(clip==null||closing)return;
                    try{var data=await DragDataAsync(clip);if(closing)return;Clipboard.SetDataObject(data,true);}catch(ExternalException){SetStatus("截图已保留，剪贴板被占用，请单击图片复制");}
                    peekUntil=DateTime.Now.AddSeconds(3);ShowShelf(false);UpdateLayout();
                    HangingPhoto photo;if(Animate&&photos.TryGetValue(clip.File,out photo)) {
                        photo.Flying(true);flights++;flight=new CaptureFlight(image,region,photo,delegate{photo.Flying(false);flight=null;WriteQaState();});flight.Show();
                    }
                    WriteQaState();
                }catch(Exception ex){Log(ex);ShowShelf(false);SetStatus("截图读取失败："+ex.Message);}
                finally{captureProcessing=false;}
            },delegate(Exception ex){Log(ex);ShowShelf(false);SetStatus("截图失败，请尝试 Win+Shift+S");});
            var current=capture;current.Closed+=delegate{if(capture==current)capture=null;Native.SetForegroundWindow(previous);WriteQaState();};capture.Show();
        }
        void ImportDialog() {
            var dlg=new Microsoft.Win32.OpenFileDialog {Filter="图片|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff",Multiselect=true};
            if(dlg.ShowDialog(this)==true) foreach(string path in dlg.FileNames)TryImport(path);
        }
        async void TryImport(string path) {try {int before=Store.Items.Count;await Store.ImportAsync(path);if(closing)return;SetStatus("已导入副本，原文件保留");if(Store.Items.Count>before){peekUntil=DateTime.Now.AddSeconds(2.5);if(viewer==null&&!Fullscreen())ShowShelf(false);}} catch(Exception ex){Log(ex);SetStatus("导入失败："+Path.GetFileName(path));}}
        void OnDragOver(object sender,DragEventArgs e){e.Effects=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;}
        void OnDrop(object sender,DragEventArgs e){ if(e.Data.GetDataPresent(DataFormats.FileDrop))foreach(string path in (string[])e.Data.GetData(DataFormats.FileDrop))if(File.Exists(path))TryImport(path);e.Handled=true; }
        void Rebuild() {
            if(stage==null||empty==null)return;
            foreach(string key in photos.Keys.Where(k=>!Store.Items.Any(c=>c.File==k)).ToArray()){var removed=photos[key];removed.Dispose();stage.Children.Remove(removed.Root);photos.Remove(key);}
            int capacity=Capacity,total=Store.Items.Count;page=Math.Max(0,Math.Min(page,Math.Max(0,(total-1)/capacity)));
            int first=page*capacity,shown=Math.Min(capacity,Math.Max(0,total-first));
            for(int index=0;index<total;index++) {
                var clip=Store.Items[index];HangingPhoto photo;bool added=!photos.TryGetValue(clip.File,out photo);
                if(added){photo=MakePhoto(clip,index);photos.Add(clip.File,photo);stage.Children.Add(photo.Root);}
                photo.Renumber(index);bool visible=index>=first&&index<first+shown;photo.Root.Visibility=visible?Visibility.Visible:Visibility.Collapsed;
                if(visible){double center=Width/2-(shown-1)*HangingStyle.Spacing/2+(index-first)*HangingStyle.Spacing;photo.Move(center,RopeView.Y(center,Width),added);}
            }
            empty.Visibility=total==0?Visibility.Visible:Visibility.Collapsed;empty.Measure(new Size(Width,100));
            Canvas.SetLeft(empty,(Width-empty.DesiredSize.Width)/2);Canvas.SetTop(empty,RopeView.Y(Width/2,Width)+23);
            newer.Visibility=page>0?Visibility.Visible:Visibility.Collapsed;older.Visibility=first+shown<total?Visibility.Visible:Visibility.Collapsed;
            Canvas.SetLeft(newer,35);Canvas.SetTop(newer,45);Canvas.SetLeft(older,Width-63);Canvas.SetTop(older,45);
            TrackMaterial(1);WriteQaState();if(qa)Dispatcher.BeginInvoke(new Action(WriteQaRender),DispatcherPriority.ContextIdle);
        }
        HangingPhoto MakePhoto(Clip clip,int index) {
            var photo=new HangingPhoto(clip,index,delegate{return Animate;},delegate{CopyClip(clip);},delegate{RemovePhoto(clip);});var card=photo.Button;
            var menu=new ContextMenu();card.ContextMenu=menu;
            AddMenu(menu,"复制",delegate{CopyClip(clip);});AddMenu(menu,"预览",delegate{OpenViewer(clip,false);});
            AddMenu(menu,"标注",delegate{OpenViewer(clip,true);});AddMenu(menu,"另存为…",delegate{SaveClip(clip);});
            AddMenu(menu,"移除副本",delegate{RemovePhoto(clip);});menu.Items.Add(new Separator());AddMenu(menu,"收起晾台",HideShelf);AddMenu(menu,"设置…",ShowSettings);
            bool down=false,held=false,inDrag=false;Point start=new Point();FloatingPhoto dragGhost=null;
            card.GiveFeedback+=delegate{if(dragGhost!=null)dragGhost.FollowCursor();};
            var hold=new DispatcherTimer(DispatcherPriority.Normal) {Interval=TimeSpan.FromMilliseconds(460)};
            hold.Tick+=delegate{hold.Stop();if(down&&!inDrag){held=true;down=false;photo.Press(false);card.ReleaseMouseCapture();OpenViewer(clip,true);}};
            card.PreviewMouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e) {
                if(e.ClickCount==2){down=false;hold.Stop();OpenViewer(clip,false);e.Handled=true;return;}
                down=true;held=false;inDrag=false;start=card.PointToScreen(e.GetPosition(card));photo.Press(true);hold.Start();
            };
            card.PreviewMouseLeftButtonUp+=delegate(object s,MouseButtonEventArgs e) {hold.Stop();photo.Press(false);if(held||inDrag)e.Handled=true;down=false;};
            card.LostMouseCapture+=delegate{if(!inDrag){hold.Stop();down=false;photo.Press(false);}};
            card.PreviewMouseMove+=delegate(object s,MouseEventArgs e) {
                if(!down || held || inDrag || e.LeftButton!=MouseButtonState.Pressed)return;
                Point now=card.PointToScreen(e.GetPosition(card));double dpi=Native.Scale(handle);
                if(Math.Abs(now.X-start.X)<SystemParameters.MinimumHorizontalDragDistance*dpi && Math.Abs(now.Y-start.Y)<SystemParameters.MinimumVerticalDragDistance*dpi)return;
                hold.Stop();inDrag=true;down=false;dragging=true;photo.Press(false);card.ReleaseMouseCapture();FloatingPhoto ghost=null;
                try {ghost=new FloatingPhoto(photo,false);dragGhost=ghost;ghost.Show();photo.Root.BeginAnimation(OpacityProperty,null);photo.Root.Opacity=.35;TrackMaterial(1);
                    var effect=DragDrop.DoDragDrop(card,DragData(clip),DragDropEffects.Copy|DragDropEffects.Move);SetStatus(effect==DragDropEffects.None?"未放入目标，截图仍保留":"分享完成 · "+effect);}
                catch(Exception ex){Log(ex);SetStatus("拖拽未完成，图片仍保留");}
                finally {dragGhost=null;if(ghost!=null)ghost.Close();photo.Root.Opacity=1;TrackMaterial(1);inDrag=false;dragging=false;lastInside=DateTime.Now;Store.PruneMoved();
                    var prune=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(700)};prune.Tick+=delegate{prune.Stop();Store.PruneMoved();};prune.Start();}
            };
            return photo;
        }
        void RemovePhoto(Clip clip){HangingPhoto photo;if(Animate&&photos.TryGetValue(clip.File,out photo)&&photo.Root.Visibility==Visibility.Visible){var fall=new FloatingPhoto(photo,true);fall.Show();}Store.Remove(clip);SetStatus("已移除副本，原文件保留");}
        static void AddMenu(ContextMenu menu,string name,Action action){var item=new MenuItem{Header=name};item.Click+=delegate{action();};menu.Items.Add(item);}
        void SaveClip(Clip clip) {
            var dlg=new Microsoft.Win32.SaveFileDialog {Filter="PNG 图片|*.png",FileName="截图-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".png",InitialDirectory=Path.Combine(Store.Root,"exports")};
            Directory.CreateDirectory(dlg.InitialDirectory);
            if(dlg.ShowDialog(this)==true)try{if(!Path.GetFullPath(dlg.FileName).Equals(Path.GetFullPath(clip.File),StringComparison.OrdinalIgnoreCase))File.Copy(clip.File,dlg.FileName,true);SetStatus("已保存到 "+dlg.FileName);}catch(Exception ex){Log(ex);SetStatus("保存失败："+ex.Message);}
        }
        void OpenViewer(Clip clip,bool editing) {
            if(viewer!=null){viewer.Activate();return;}
            var edit=new ImageWindow(clip,Store,editing,qa?qaDir:null);viewer=edit;HideNow();
            edit.Closed+=delegate{viewer=null;lastInside=DateTime.Now;Rebuild();ShowShelf(false);};edit.Show();edit.Activate();
        }
        void SetupWatcher() {
            if(watcher!=null){watcher.Dispose();watcher=null;}
            string folder=Store.Settings.WatchFolder;if(String.IsNullOrWhiteSpace(folder)||!Directory.Exists(folder))return;
            watcher=new FileSystemWatcher(folder) {IncludeSubdirectories=false,NotifyFilter=NotifyFilters.FileName|NotifyFilters.LastWrite};
            FileSystemEventHandler changed=delegate(object sender,FileSystemEventArgs e){QueueFile(e.FullPath);};
            watcher.Created+=changed;watcher.Changed+=changed;watcher.Renamed+=delegate(object s,RenamedEventArgs e){QueueFile(e.FullPath);};
            watcher.Error+=delegate {Dispatcher.BeginInvoke(new Action(delegate{SetStatus("目录监听中断，请在设置中重新选择目录");}));};watcher.EnableRaisingEvents=true;
        }
        void QueueFile(string path) {
            string ext=Path.GetExtension(path).ToLowerInvariant();if(!new[]{".png",".jpg",".jpeg",".bmp",".gif",".tif",".tiff"}.Contains(ext))return;
            Dispatcher.BeginInvoke(new Action(delegate{
                if(!pendingFiles.Add(path))return;int attempt=0;long previousLength=-1;
                var wait=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(250)};
                wait.Tick+=delegate{try{var info=new FileInfo(path);if(!info.Exists){if(attempt++<10)return;}
                    else if(info.Length==previousLength && info.Length>0){wait.Stop();pendingFiles.Remove(path);TryImport(path);return;}
                    else previousLength=info.Length;
                }catch(IOException){ }catch(Exception ex){Log(ex);wait.Stop();pendingFiles.Remove(path);return;}
                    if(attempt++>14){wait.Stop();pendingFiles.Remove(path);SetStatus("文件尚未写完，可稍后手动导入");}};wait.Start();
            }));
        }
        void ShowSettings() {
            var w=new Window {Title="Screenline 设置",Width=500,Height=510,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterScreen,Topmost=true,
                Background=B("#F8F9FB"),Foreground=B("#303744"),FontFamily=new FontFamily("Segoe UI, Microsoft YaHei UI"),FontSize=13};
            w.Icon=Icon;
            var p=new StackPanel{Margin=new Thickness(25)};w.Content=p;
            p.Children.Add(new TextBlock{Text="截图留在本机，操作保持简单",FontSize=20,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,20)});
            var monitor=new CheckBox{Content="收纳剪贴板中的新图片（包括系统截图）",IsChecked=Store.Settings.MonitorClipboard,Margin=new Thickness(0,0,0,16)};
            var motion=new CheckBox{Content="减少动画",IsChecked=Store.Settings.ReducedMotion,Margin=new Thickness(0,0,0,16)};p.Children.Add(monitor);p.Children.Add(motion);
            var glass=new CheckBox{Content="系统玻璃材质（支持时启用，遵循系统透明度设置）",IsChecked=Store.Settings.SystemMaterial,Margin=new Thickness(0,0,0,16)};p.Children.Add(glass);
            p.Children.Add(new TextBlock{Text="额外监听的图片目录（可选）",Margin=new Thickness(0,4,0,8)});
            var folder=new TextBox{Text=Store.Settings.WatchFolder,Padding=new Thickness(7),IsReadOnly=true};p.Children.Add(folder);
            var select=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,8,0,20)};
            select.Children.Add(ActionButton("选择目录","ChooseFolder",delegate{using(var d=new Forms.FolderBrowserDialog()){if(d.ShowDialog()==Forms.DialogResult.OK)folder.Text=d.SelectedPath;}}));
            select.Children.Add(ActionButton("停用目录监听","ClearFolder",delegate{folder.Text="";}));p.Children.Add(select);
            p.Children.Add(new TextBlock{Text="Ctrl+Alt+T  展开 / 收起\nCtrl+Alt+S  区域截图 · Esc 取消\nWin+Shift+S  继续使用 Windows 系统截图\n图片与设置保存在程序旁的 data 文件夹。\n导入、监听和移除操作都不会删除原始图片。",TextWrapping=TextWrapping.Wrap,LineHeight=25,Foreground=B("#626B78")});
            var save=ActionButton("保存设置","SaveSettings",delegate{Store.Settings.MonitorClipboard=monitor.IsChecked==true;Store.Settings.ReducedMotion=motion.IsChecked==true;Store.Settings.SystemMaterial=glass.IsChecked==true;Store.Settings.WatchFolder=folder.Text;Store.Save();SetupWatcher();w.Close();SetStatus("设置已保存");});save.Margin=new Thickness(0,20,0,0);p.Children.Add(save);
            viewer=w;HideNow();w.Closed+=delegate{viewer=null;lastInside=DateTime.Now;Rebuild();ShowShelf(false);};w.Show();
        }
        void Log(Exception ex){try{File.AppendAllText(Path.Combine(Store.Root,"errors.log"),DateTime.Now.ToString("s")+" "+ex.GetType().Name+": "+ex.Message+Environment.NewLine);}catch(Exception){}}
        protected override async void OnClosing(CancelEventArgs e) {
            if(Store.IsBusy){e.Cancel=true;base.OnClosing(e);await Store.WhenIdle;Close();return;}
            base.OnClosing(e);
        }
        void OnClosed(object sender,EventArgs e) {
            closing=true;Motion.Started-=TrackMaterial;cursorTimer.Stop();clipboardTimer.Stop();noticeTimer.Stop();if(watcher!=null)watcher.Dispose();
            foreach(var photo in photos.Values)photo.Dispose();
            if(material!=null)material.Dispose();if(capture!=null)capture.Close();if(flight!=null)flight.Close();
            if(clipboardRegistered)Native.RemoveClipboardFormatListener(handle);Native.UnregisterHotKey(handle,1);Native.UnregisterHotKey(handle,2);
            if(tray!=null){tray.Visible=false;tray.Icon.Dispose();tray.Dispose();}if(viewer!=null)viewer.Close();Store.Save();Application.Current.Shutdown();
        }
void WriteQaState(){if(!qa||qaDir==null)return;try{Directory.CreateDirectory(qaDir);File.WriteAllText(Path.Combine(qaDir,"ui-state.json"),new JavaScriptSerializer().Serialize(new{version="0.4.1",count=Store.Items.Count,visible=IsVisible&&!retracting,status=status.Text,clipboard_listener=clipboardRegistered,files=Store.Items.Select(c=>c.File).ToArray(),capacity=Capacity,page=page,material_applied=material!=null&&material.Applied,material_result=material==null?-1:material.ApplyResult,material_allowed=DesktopMaterial.UserAllowsTransparency,material_handle=material==null?0:material.Handle.ToInt64(),capture_active=capture!=null,flights=flights,capture_rect=lastCapture.IsEmpty?null:new{ x=lastCapture.X,y=lastCapture.Y,width=lastCapture.Width,height=lastCapture.Height },photos=photos.Values.Where(p=>p.Root.Visibility==Visibility.Visible).Select(p=>new{file=p.Clip.File,width=p.PhotoWidth,height=p.PhotoHeight,x=Canvas.GetLeft(p.Root),y=Canvas.GetTop(p.Root),tilt=p.Tilt}).ToArray()}));}catch(Exception){}}
        void WriteQaRender(){if(!qa||stage.ActualWidth<1)return;double scale=Native.Scale(handle);var picture=new RenderTargetBitmap((int)Math.Ceiling(stage.ActualWidth*scale),(int)Math.Ceiling(stage.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32);picture.Render(stage);File.WriteAllBytes(Path.Combine(qaDir,"shelf-clean.png"),ClipStore.Png(picture));}
        void QaRequests() {
            try {
                string request=Path.Combine(qaDir,"render.request");
                if(File.Exists(request)){WriteQaRender();WriteQaState();File.Delete(request);}
                if(!File.Exists(Path.Combine(qaDir,"recording.flag"))||(DateTime.Now-lastQaFrame).TotalMilliseconds<65)return;
                lastQaFrame=DateTime.Now;Directory.CreateDirectory(Path.Combine(qaDir,"frames"));
                // Render only this application's visuals onto an explicitly synthetic backdrop.
                // Never sample or record the desktop, other apps, or wallpaper.
                double cropWidth=Math.Min(900,Width),height=400,offset=(Width-cropWidth)/2;
                var scene=new DrawingVisual();using(var dc=scene.RenderOpen()) {
                    dc.DrawRectangle(new LinearGradientBrush(B("#E7EDF3").Color,B("#F3ECE6").Color,25),null,new Rect(0,0,cropWidth,height));
                    dc.PushTransform(new TranslateTransform(-offset,0));
                    var brush=new VisualBrush(stage){Stretch=Stretch.None,AlignmentX=AlignmentX.Left,AlignmentY=AlignmentY.Top,ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,Width,Height)};
                    dc.DrawRectangle(brush,null,new Rect(0,0,Width,Height));dc.Pop();
                    Point origin=PointToScreen(new Point(0,0));double dpi=Native.Scale(handle);
                    foreach(var floating in Application.Current.Windows.OfType<Window>().Where(f=>(f is FloatingPhoto||f is CaptureFlight)&&f.IsVisible).ToArray()) {
                        var content=(FrameworkElement)floating.Content;Point at=floating.PointToScreen(new Point(0,0));
                        var floatingBrush=new VisualBrush(content){Stretch=Stretch.Fill,ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,content.ActualWidth,content.ActualHeight)};dc.PushOpacity(floating.Opacity);
                        dc.DrawRectangle(floatingBrush,null,new Rect((at.X-origin.X)/dpi-offset,(at.Y-origin.Y)/dpi,content.ActualWidth,content.ActualHeight));dc.Pop();
                    }
                }
                var picture=new RenderTargetBitmap((int)cropWidth,(int)height,96,96,PixelFormats.Pbgra32);picture.Render(scene);
                File.WriteAllBytes(Path.Combine(qaDir,"frames","frame-"+(qaFrame++).ToString("D5")+".png"),ClipStore.Png(picture));
            }catch(Exception ex){Log(ex);}
        }
    }
    public static class Program {
        [STAThread] public static int Main(string[] args) {
            string baseDir=AppDomain.CurrentDomain.BaseDirectory,data=Path.Combine(baseDir,"data"),qaDir=null;
            var imports=new List<string>();
            for(int i=0;i<args.Length;i++){if(args[i]=="--data-dir"&&i+1<args.Length)data=Path.GetFullPath(args[++i]);else if(args[i]=="--qa-dir"&&i+1<args.Length)qaDir=Path.GetFullPath(args[++i]);else if(File.Exists(args[i]))imports.Add(Path.GetFullPath(args[i]));}
            bool created;string key;using(var sha=SHA256.Create())key=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(data.ToUpperInvariant()))).Replace("-","").Substring(0,20);
            using(var mutex=new System.Threading.Mutex(true,"Local\\Screenline-"+key,out created)){
                if(!created){MessageBox.Show("截图晾台已在运行。请从系统托盘展开，或按 Ctrl+Alt+T。","Screenline");return 0;}
                try {
                    var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
                    app.DispatcherUnhandledException+=delegate(object s,DispatcherUnhandledExceptionEventArgs e){MessageBox.Show(e.Exception.Message,"Screenline 操作未完成");e.Handled=true;};
                    var store=new ClipStore(data);foreach(string path in imports)try{store.Import(path);}catch(Exception){ }
                    var shelf=new ShelfWindow(store,qaDir!=null,qaDir);app.Run(shelf);return 0;
                }catch(Exception ex){MessageBox.Show("启动失败："+ex.Message+"\n请把程序放到可写目录。","Screenline");return 1;}
            }
        }
    }
}
