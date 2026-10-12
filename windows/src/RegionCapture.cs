using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Drawing=System.Drawing;
using Forms=System.Windows.Forms;

namespace Screenline {
    // Selection is measured in physical desktop pixels, including negative monitor origins.
    sealed class RegionCapture : Window {
        readonly Action<BitmapSource,Rect> complete;
        readonly Action<Exception> failed;
        readonly CaptureSurface surface;
        readonly Drawing.Rectangle desktop;
        Native.POINT start;
        bool selecting,committing;
        public RegionCapture(Action<BitmapSource,Rect> done,Action<Exception> error) {
            complete=done;failed=error;desktop=Forms.SystemInformation.VirtualScreen;
            Title="Screenline 区域截图";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
            AllowsTransparency=true;Background=Brushes.Transparent;Topmost=true;ShowInTaskbar=false;Cursor=Cursors.Cross;
            surface=new CaptureSurface();Content=surface;
            SourceInitialized+=delegate {var h=new WindowInteropHelper(this).Handle;double s=Native.Scale(h);Width=desktop.Width/s;Height=desktop.Height/s;Native.SetStyle(h,0x80,true);Native.SetWindowPos(h,new IntPtr(-1),desktop.Left,desktop.Top,desktop.Width,desktop.Height,0x40);};
            PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Escape){Close();e.Handled=true;}};
            MouseLeftButtonDown+=delegate {Native.GetCursorPos(out start);selecting=true;CaptureMouse();UpdateSelection();};
            MouseMove+=delegate {if(selecting)UpdateSelection();};
            MouseLeftButtonUp+=delegate {
                if(!selecting)return;selecting=false;Native.POINT end;Native.GetCursorPos(out end);ReleaseMouseCapture();
                var bounds=new Drawing.Rectangle(Math.Min(start.X,end.X),Math.Min(start.Y,end.Y),Math.Abs(end.X-start.X),Math.Abs(end.Y-start.Y));
                bounds=Drawing.Rectangle.Intersect(bounds,desktop);
                if(bounds.Width<3||bounds.Height<3){Close();return;}
                committing=true;Hide();
                // Give DWM a composition tick to remove our selection overlay before reading the selected pixels.
                var wait=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(90)};
                wait.Tick+=delegate {wait.Stop();try{var image=ReadPixels(bounds);Close();complete(image,new Rect(bounds.X,bounds.Y,bounds.Width,bounds.Height));}catch(Exception ex){Close();failed(ex);}};wait.Start();
            };
            Loaded+=delegate{Activate();Focus();};
        }
        public bool Committing {get{return committing;}}
        void UpdateSelection(){Native.POINT end;Native.GetCursorPos(out end);Point a=PointFromScreen(new Point(start.X,start.Y)),b=PointFromScreen(new Point(end.X,end.Y));surface.Selection=new Rect(a,b);surface.PixelSize=Math.Abs(end.X-start.X)+" × "+Math.Abs(end.Y-start.Y);surface.InvalidateVisual();}
        static BitmapSource ReadPixels(Drawing.Rectangle bounds) {
            using(var bitmap=new Drawing.Bitmap(bounds.Width,bounds.Height,Drawing.Imaging.PixelFormat.Format32bppRgb)) {
                using(var graphics=Drawing.Graphics.FromImage(bitmap))graphics.CopyFromScreen(bounds.Location,Drawing.Point.Empty,bounds.Size,Drawing.CopyPixelOperation.SourceCopy);
                var lockBits=bitmap.LockBits(new Drawing.Rectangle(0,0,bitmap.Width,bitmap.Height),Drawing.Imaging.ImageLockMode.ReadOnly,Drawing.Imaging.PixelFormat.Format32bppRgb);
                try{var source=BitmapSource.Create(bitmap.Width,bitmap.Height,96,96,PixelFormats.Bgr32,null,lockBits.Scan0,lockBits.Stride*bitmap.Height,lockBits.Stride);source.Freeze();return source;}finally{bitmap.UnlockBits(lockBits);}
            }
        }
        sealed class CaptureSurface : FrameworkElement {
            public Rect Selection=Rect.Empty;public string PixelSize="";
            protected override void OnRender(DrawingContext dc) {
                var entire=new Rect(0,0,ActualWidth,ActualHeight);var shade=new GeometryGroup{FillRule=FillRule.EvenOdd};shade.Children.Add(new RectangleGeometry(entire));
                if(!Selection.IsEmpty)shade.Children.Add(new RectangleGeometry(Selection));dc.DrawGeometry(ShelfWindow.B("#650E1623"),null,shade);
                string label="拖动选择截图区域    ·    Esc 取消";Point position=new Point(Math.Max(24,ActualWidth/2-150),30);
                if(!Selection.IsEmpty){dc.DrawRectangle(null,new Pen(ShelfWindow.B("#EFFFFFFF"),1),Selection);dc.DrawRectangle(null,new Pen(ShelfWindow.B("#60557EAC"),.5),new Rect(Selection.X-1,Selection.Y-1,Selection.Width+2,Selection.Height+2));label=PixelSize;position=new Point(Math.Min(Math.Max(8,Selection.Left),ActualWidth-170),Math.Min(ActualHeight-44,Selection.Bottom+10));}
                var text=new FormattedText(label,System.Globalization.CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface("Segoe UI, Microsoft YaHei UI"),12,Brushes.White,VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawRoundedRectangle(ShelfWindow.B("#E526303E"),new Pen(ShelfWindow.B("#28FFFFFF"),.75),new Rect(position.X-12,position.Y-7,text.Width+24,text.Height+14),8,8);dc.DrawText(text,position);
            }
        }
    }
    sealed class CaptureFlight : Window {
        readonly Stopwatch elapsed=new Stopwatch();
        readonly Canvas canvas=new Canvas();
        readonly FlightPhoto frame;
        readonly System.Windows.Controls.Border peg;
        readonly RotateTransform rotation=new RotateTransform(),pegRotation=new RotateTransform();
        readonly TranslateTransform position=new TranslateTransform(),pegPosition=new TranslateTransform();
        readonly HangingPhoto target;
        readonly Rect from;
        readonly Action finished;
        double scale;bool done;IntPtr handle;
        TimeSpan lastFrame=TimeSpan.MinValue;
        public CaptureFlight(BitmapSource bitmap,Rect capture,HangingPhoto photo,Action landed) {
            target=photo;from=capture;finished=landed;
            Title="Screenline 截图飞入";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;
            Topmost=true;ShowInTaskbar=false;ShowActivated=false;IsHitTestVisible=false;Content=canvas;
            frame=new FlightPhoto(bitmap);var transform=new TransformGroup();transform.Children.Add(rotation);transform.Children.Add(position);frame.RenderTransform=transform;
            canvas.Children.Add(frame);peg=HangingPhoto.Peg();var pegTransform=new TransformGroup();pegTransform.Children.Add(pegRotation);pegTransform.Children.Add(pegPosition);peg.RenderTransform=pegTransform;canvas.Children.Add(peg);
            SourceInitialized+=delegate {
                handle=new WindowInteropHelper(this).Handle;scale=Native.Scale(handle);
                // A moving, bounded surface replaces the full virtual-desktop transparent window.
                Width=Math.Max(from.Width/scale,target.Root.Width)+88;Height=Math.Max(from.Height/scale,target.Button.Height)+70;
                frame.Width=Width;frame.Height=Height;
                Native.SetStyle(handle,0x08000000|0x80|0x20,true);
                Native.SetWindowPos(handle,new IntPtr(-1),(int)(from.X-44*scale),(int)(from.Y-24*scale),(int)Math.Ceiling(Width*scale),(int)Math.Ceiling(Height*scale),0x10|0x40);
            };
            Loaded+=delegate{elapsed.Start();Draw(0);CompositionTarget.Rendering+=RenderFrame;};
            Closed+=delegate{CompositionTarget.Rendering-=RenderFrame;if(!done){done=true;finished();}};
        }
        void RenderFrame(object sender,EventArgs e) {
            var rendering=e as RenderingEventArgs;if(rendering!=null&&rendering.RenderingTime==lastFrame)return;if(rendering!=null)lastFrame=rendering.RenderingTime;
            Draw(Math.Min(1,elapsed.Elapsed.TotalMilliseconds/650));
        }
        void Draw(double t) {
            double p=t*t*(3-2*t);Point end=target.Button.PointToScreen(new Point(target.Button.ActualWidth/2,0));
            double width=(from.Width+(target.Button.ActualWidth*scale-from.Width)*p)/scale,height=(from.Height+(target.Button.ActualHeight*scale-from.Height)*p)/scale;
            double center=from.X+from.Width/2+(end.X-from.X-from.Width/2)*p;
            double top=from.Y+(end.Y-from.Y)*p-30*scale*4*p*(1-p);
            Native.SetWindowPos(handle,IntPtr.Zero,(int)Math.Round(center-Width*scale/2),(int)Math.Round(top-24*scale),0,0,0x1|0x4|0x10);
            frame.Draw(width,height,p);
            position.X=(Width-width)/2;position.Y=24;rotation.Angle=target.Tilt*p;rotation.CenterX=width/2;
            peg.Opacity=Math.Max(0,(p-.5)*2);pegPosition.X=(Width-HangingStyle.PegWidth)/2;pegPosition.Y=24-HangingStyle.CardTop;
            pegRotation.Angle=target.Tilt*p;pegRotation.CenterX=HangingStyle.PegWidth/2;pegRotation.CenterY=HangingStyle.CardTop;
            if(t>=1)Close();
        }
        sealed class FlightPhoto : FrameworkElement {
            readonly BitmapSource bitmap;readonly Brush glass=HangingPhoto.Glass(),edge=HangingPhoto.Edge();readonly RectangleGeometry clip=new RectangleGeometry();
            readonly DrawingVisual picture=new DrawingVisual();
            protected override int VisualChildrenCount{get{return 1;}}
            protected override Visual GetVisualChild(int index){if(index!=0)throw new ArgumentOutOfRangeException("index");return picture;}
            public FlightPhoto(BitmapSource source){bitmap=source;AddVisualChild(picture);RenderOptions.SetBitmapScalingMode(this,BitmapScalingMode.LowQuality);Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=16,ShadowDepth=4,Opacity=.16,Color=Color.FromRgb(30,36,46)};}
            public void Draw(double width,double height,double progress) {
                double inset=HangingStyle.Inset*progress,radius=HangingStyle.Radius*progress;
                using(var dc=picture.RenderOpen()) {
                    dc.DrawRoundedRectangle(glass,new Pen(edge,HangingStyle.EdgeWidth*progress),new Rect(0,0,width,height),radius,radius);
                    clip.Rect=new Rect(inset,inset,Math.Max(1,width-2*inset),Math.Max(1,height-2*inset));clip.RadiusX=clip.RadiusY=(HangingStyle.Radius-HangingStyle.Inset)*progress;
                    dc.PushClip(clip);dc.DrawImage(bitmap,clip.Rect);dc.Pop();
                }
            }
        }
    }
}
