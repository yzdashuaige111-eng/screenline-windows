// Local composition backdrop: the OS samples and blurs pixels behind each photo.
// Screenline never captures the desktop to manufacture glass.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using Composition=Windows.UI.Composition;
using Numerics=System.Numerics;

namespace Screenline {
    sealed class MaterialShape {public double Width,Height,Dpi;public Matrix Transform;}
    sealed class DesktopMaterial : IDisposable {
        [StructLayout(LayoutKind.Sequential)] struct Margins {public int left,right,top,bottom;}
        [StructLayout(LayoutKind.Sequential)] struct QueueOptions {public int size,thread,apartment;}
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h,int attribute,ref int value,int size);
        [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr h,ref Margins margins);
        [DllImport("coremessaging.dll")] static extern int CreateDispatcherQueueController(QueueOptions options,[MarshalAs(UnmanagedType.IUnknown)] out object controller);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h,int command);
        [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr h,IntPtr region,bool redraw);
        [DllImport("gdi32.dll")] static extern IntPtr CreateRectRgn(int left,int top,int right,int bottom);
        [DllImport("gdi32.dll")] static extern IntPtr CreatePolygonRgn(Native.POINT[] points,int count,int mode);
        [DllImport("gdi32.dll")] static extern int CombineRgn(IntPtr result,IntPtr a,IntPtr b,int mode);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
        [ComImport,Guid("29E691FA-4567-4DCA-B319-D0F207EB6807"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IDesktopInterop {void CreateDesktopWindowTarget(IntPtr hwnd,[MarshalAs(UnmanagedType.Bool)] bool topmost,out IntPtr target);}
        [ComImport,Guid("A1BEA8BA-D726-4663-8129-6B5E7927FFA6"),InterfaceType(ComInterfaceType.InterfaceIsIInspectable)] interface ITarget {Composition.Visual Root{get;set;}}
        readonly HwndSource source;
        readonly IntPtr owner;
        Composition.Compositor compositor;
        Composition.ContainerVisual root;
        ITarget target;
        object queue;
        readonly List<Composition.SpriteVisual> sprites=new List<Composition.SpriteVisual>();
        MaterialShape[] lastGeometry;
        Native.RECT lastBounds;
        Native.POINT[] lastRegion;
        bool shown;
        bool disposed;
        static DateTime checkedAt=DateTime.MinValue;
        static bool allowed=true;
        public int ApplyResult {get;private set;}
        public bool Applied {get;private set;}
        public IntPtr Handle {get{return source.Handle;}}
        public static bool UserAllowsTransparency {
            get {if(SystemParameters.HighContrast)return false;if((DateTime.Now-checkedAt).TotalSeconds<1)return allowed;checkedAt=DateTime.Now;try{allowed=Convert.ToInt32(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","EnableTransparency",1))!=0;}catch{allowed=true;}return allowed;}
        }
        public DesktopMaterial(IntPtr shelf) {
            owner=shelf;ApplyResult=-1;
            // A visual-only HWND must never become a mouse or OLE input target.
            source=new HwndSource(new HwndSourceParameters("Screenline · Local backdrop") {WindowStyle=unchecked((int)0x88000000),ExtendedWindowStyle=0x08000000|0x80|0x20,Width=1,Height=1,UsesPerPixelOpacity=false});
            source.CompositionTarget.BackgroundColor=Colors.Transparent;source.RootVisual=new DrawingVisual();source.AddHook(Hook);
            try{Initialize();}catch(Exception ex){ApplyResult=Marshal.GetHRForException(ex);Applied=false;}
        }
        void Initialize() {
            int enabled=1;ApplyResult=DwmSetWindowAttribute(Handle,17,ref enabled,4); // DWMWA_USE_HOSTBACKDROPBRUSH.
            if(ApplyResult!=0)return; // Unsupported OS keeps the refined translucent fallback.
            var margins=new Margins{left=-1,right=-1,top=-1,bottom=-1};DwmExtendFrameIntoClientArea(Handle,ref margins);
            int border=unchecked((int)0xfffffffe);DwmSetWindowAttribute(Handle,34,ref border,4);
            Marshal.ThrowExceptionForHR(CreateDispatcherQueueController(new QueueOptions{size=12,thread=2,apartment=2},out queue));
            compositor=new Composition.Compositor();IntPtr raw;
            ((IDesktopInterop)(object)compositor).CreateDesktopWindowTarget(Handle,true,out raw);
            try{target=(ITarget)Marshal.GetObjectForIUnknown(raw);}finally{Marshal.Release(raw);}
            root=compositor.CreateContainerVisual();target.Root=root;Applied=true;
        }
        IntPtr Hook(IntPtr h,int message,IntPtr w,IntPtr l,ref bool handled) {if(message==0x84){handled=true;return new IntPtr(-1);}if(message==0x21){handled=true;return new IntPtr(3);}return IntPtr.Zero;}
        static bool Same(MaterialShape a,MaterialShape b){return a.Width==b.Width&&a.Height==b.Height&&a.Dpi==b.Dpi&&a.Transform==b.Transform;}
        public void Update(IEnumerable<MaterialShape> shapes,bool visible) {
            if(disposed)return;
            if(!Applied||!UserAllowsTransparency||!visible){Hide();return;}
            var geometry=shapes.ToArray();if(geometry.Length==0){Hide();return;}
            Native.RECT rect;if(!Native.GetWindowRect(owner,out rect))return;
            bool moved=!shown||rect.Left!=lastBounds.Left||rect.Top!=lastBounds.Top||rect.Right!=lastBounds.Right||rect.Bottom!=lastBounds.Bottom;
            if(!moved&&lastGeometry!=null&&lastGeometry.Length==geometry.Length&&geometry.Where((g,i)=>!Same(g,lastGeometry[i])).Any()==false)return;
            while(sprites.Count<geometry.Length){var sprite=compositor.CreateSpriteVisual();sprite.Brush=compositor.CreateHostBackdropBrush();var rounded=compositor.CreateRoundedRectangleGeometry();sprite.Clip=compositor.CreateGeometricClip(rounded);root.Children.InsertAtTop(sprite);sprites.Add(sprite);}
            for(int i=0;i<sprites.Count;i++) {
                var sprite=sprites[i];if(i>=geometry.Length){sprite.IsVisible=false;continue;}sprite.IsVisible=true;
                if(shown&&lastGeometry!=null&&i<lastGeometry.Length&&Same(geometry[i],lastGeometry[i]))continue;
                var g=geometry[i];sprite.Size=new Numerics.Vector2((float)(g.Width*g.Dpi),(float)(g.Height*g.Dpi));
                var rounded=(Composition.CompositionRoundedRectangleGeometry)((Composition.CompositionGeometricClip)sprite.Clip).Geometry;
                rounded.Size=sprite.Size;rounded.CornerRadius=new Numerics.Vector2((float)(HangingStyle.Radius*g.Dpi));
                var m=g.Transform;sprite.TransformMatrix=new Numerics.Matrix4x4((float)m.M11,(float)m.M12,0,0,(float)m.M21,(float)m.M22,0,0,0,0,1,0,(float)(m.OffsetX*g.Dpi),(float)(m.OffsetY*g.Dpi),0,1);
            }
            if(moved)Native.SetWindowPos(Handle,owner,rect.Left,rect.Top,rect.Right-rect.Left,rect.Bottom-rect.Top,0x10|0x40);
            lastBounds=rect;shown=true;
            // Restrict native hit testing as well as the composition visuals.
            var polygons=new List<Native.POINT[]>();var outline=new List<Native.POINT>();
            foreach(var g in geometry) {
                double r=Math.Min(HangingStyle.Radius,Math.Min(g.Width,g.Height)/2);var points=new List<Native.POINT>();
                for(int corner=0;corner<4;corner++)for(int step=0;step<=8;step++){double angle=(-90+corner*90+step*90.0/8)*Math.PI/180,cx=corner<2?g.Width-r:r,cy=corner==0||corner==3?r:g.Height-r;Point p=g.Transform.Transform(new Point(cx+Math.Cos(angle)*r,cy+Math.Sin(angle)*r));points.Add(new Native.POINT{X=(int)Math.Round(p.X*g.Dpi),Y=(int)Math.Round(p.Y*g.Dpi)});}
                var polygon=points.ToArray();polygons.Add(polygon);outline.AddRange(polygon);
            }
            bool sameRegion=lastRegion!=null&&lastRegion.Length==outline.Count;
            if(sameRegion)for(int i=0;i<outline.Count;i++)if(outline[i].X!=lastRegion[i].X||outline[i].Y!=lastRegion[i].Y){sameRegion=false;break;}
            if(!sameRegion){
                IntPtr region=CreateRectRgn(0,0,0,0);
                foreach(var polygon in polygons){IntPtr piece=CreatePolygonRgn(polygon,polygon.Length,1);CombineRgn(region,region,piece,2);DeleteObject(piece);}
                if(SetWindowRgn(Handle,region,true)==0)DeleteObject(region);else lastRegion=outline.ToArray();
            }
            lastGeometry=geometry.Select(g=>new MaterialShape{Width=g.Width,Height=g.Height,Dpi=g.Dpi,Transform=g.Transform}).ToArray();
        }
        public void Hide(){if(!disposed&&shown){ShowWindow(Handle,0);shown=false;lastGeometry=null;}}
        public void Dispose(){if(disposed)return;disposed=true;foreach(var sprite in sprites){if(sprite.Brush!=null)sprite.Brush.Dispose();if(sprite.Clip!=null)sprite.Clip.Dispose();sprite.Dispose();}if(target!=null)target.Root=null;if(root!=null)root.Dispose();if(compositor!=null)compositor.Dispose();source.Dispose();}
    }
}
