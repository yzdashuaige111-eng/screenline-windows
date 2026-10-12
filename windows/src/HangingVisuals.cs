// Native presentation for the transparent clothesline. No bundled artwork.
using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Interop;

namespace Screenline {
    static class HangingStyle {
        public const double MaxWidth=160,MaxHeight=112,Inset=1.5,Radius=8.5,EdgeWidth=.5;
        public const double PegWidth=5.5,PegHeight=19,CardTop=11,Anchor=7.5,Spacing=190;
        public const double HoverScale=1.018,PressScale=.975;
    }
    static class Motion {
        public static event Action<double> Started;
        sealed class SpringEase : EasingFunctionBase {
            protected override double EaseInCore(double t){return t>=1?1:1-Math.Exp(-7*t)*(Math.Cos(10*t)+.7*Math.Sin(10*t));}
            protected override Freezable CreateInstanceCore(){return new SpringEase();}
        }
        static readonly IEasingFunction springEase=FreezeEase(new SpringEase{EasingMode=EasingMode.EaseIn});
        static readonly IEasingFunction smoothEase=FreezeEase(new CubicEase{EasingMode=EasingMode.EaseOut});
        static IEasingFunction FreezeEase(EasingFunctionBase ease){ease.Freeze();return ease;}
        public static void To(Animatable target, DependencyProperty property, double from, double to, double ms, bool spring) {
            target.SetValue(property, to);
            var animation=new DoubleAnimation(from,to,TimeSpan.FromMilliseconds(ms)){FillBehavior=FillBehavior.Stop,EasingFunction=spring?springEase:smoothEase};
            target.BeginAnimation(property,animation);if(Started!=null)Started(ms);
        }
        public static void Fade(UIElement target, double from, double to, double ms) {
            target.Opacity=to; target.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(from,to,TimeSpan.FromMilliseconds(ms)){FillBehavior=FillBehavior.Stop});if(Started!=null)Started(ms);
        }
    }
    sealed class RopeView : FrameworkElement {
        public static double Y(double x,double width) { double f=width>0?x/width:.5; return 10+4*Math.Min(30,width*.018)*f*(1-f); }
        public RopeView() {
            IsHitTestVisible=false;
            var fade=new LinearGradientBrush();fade.StartPoint=new Point(0,0);fade.EndPoint=new Point(1,0);
            fade.GradientStops.Add(new GradientStop(Colors.Transparent,0));fade.GradientStops.Add(new GradientStop(Colors.Black,.08));
            fade.GradientStops.Add(new GradientStop(Colors.Black,.92));fade.GradientStops.Add(new GradientStop(Colors.Transparent,1));OpacityMask=fade;
        }
        protected override void OnRender(DrawingContext dc) {
            base.OnRender(dc);double w=ActualWidth,sag=Math.Min(30,w*.018);
            var path=new StreamGeometry();using(var g=path.Open()){g.BeginFigure(new Point(-20,10),false,false);g.QuadraticBezierTo(new Point(w/2,10+2*sag),new Point(w+20,10),true,false);}path.Freeze();
            dc.PushTransform(new TranslateTransform(0,.65));dc.DrawGeometry(null,new Pen(ShelfWindow.B("#18303433"),1.2),path);dc.Pop();
            dc.DrawGeometry(null,new Pen(ShelfWindow.B("#90787F7B"),.6),path);
            dc.PushTransform(new TranslateTransform(0,-.25));dc.DrawGeometry(null,new Pen(ShelfWindow.B("#88FFFFFF"),.25),path);dc.Pop();
        }
    }
    sealed class HangingPhoto {
        public readonly Clip Clip;
        public readonly Grid Root;
        public readonly Button Button,Remove;
        public readonly double PhotoWidth,PhotoHeight,Tilt;
        readonly RotateTransform swing;
        readonly TranslateTransform position=new TranslateTransform(),arrival=new TranslateTransform();
        readonly ScaleTransform scale=new ScaleTransform(1,1);
        readonly Border copied;
        readonly DispatcherTimer copiedTimer;
        readonly Func<bool> animate;
        bool placed;
        bool? materialNative;
        double x,y;
        public HangingPhoto(Clip clip,int index,Func<bool> motion,Action copy,Action remove) {
            Clip=clip;animate=motion;
            double fit=Math.Min(HangingStyle.MaxWidth/clip.Width,HangingStyle.MaxHeight/clip.Height);PhotoWidth=clip.Width*fit;PhotoHeight=clip.Height*fit;
            Tilt=(Convert.ToInt32(clip.Hash.Substring(0,4),16)/65535.0-.5)*2.6;
            Root=new Grid{Width=PhotoWidth+2*HangingStyle.Inset,Height=PhotoHeight+46,Background=null};
            swing=new RotateTransform(Tilt,Root.Width/2,HangingStyle.Anchor);
            scale.CenterX=Root.Width/2;scale.CenterY=HangingStyle.CardTop;
            var transforms=new TransformGroup();transforms.Children.Add(scale);transforms.Children.Add(swing);transforms.Children.Add(position);transforms.Children.Add(arrival);Root.RenderTransform=transforms;
            var image=new Image{Source=clip.Thumbnail,Width=PhotoWidth,Height=PhotoHeight,Stretch=Stretch.Fill,
                Clip=new RectangleGeometry(new Rect(0,0,PhotoWidth,PhotoHeight),HangingStyle.Radius-HangingStyle.Inset,HangingStyle.Radius-HangingStyle.Inset)};
            RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);
            Button=new Button{Content=image,Width=Root.Width,Height=PhotoHeight+2*HangingStyle.Inset,Padding=new Thickness(HangingStyle.Inset-HangingStyle.EdgeWidth),
                Background=Glass(),BorderBrush=Edge(),BorderThickness=new Thickness(HangingStyle.EdgeWidth),Cursor=Cursors.Hand,
                HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,HangingStyle.CardTop,0,0),
                Effect=new DropShadowEffect{BlurRadius=12,ShadowDepth=3,Opacity=.16,Color=Color.FromRgb(30,35,33)},Template=PhotoTemplate(),
                ToolTip=clip.Width+" × "+clip.Height+"\n单击复制 · 长按标注 · 双击预览 · 拖拽分享"};
            ToolTipService.SetInitialShowDelay(Button,850);Root.Children.Add(Button);
            AutomationProperties.SetAutomationId(Button,"Clip-"+index);AutomationProperties.SetName(Button,"复制截图 "+(index+1)+"，"+clip.Width+" 乘 "+clip.Height);
            var peg=Peg();peg.HorizontalAlignment=HorizontalAlignment.Center;peg.VerticalAlignment=VerticalAlignment.Top;Panel.SetZIndex(peg,2);Root.Children.Add(peg);
            Remove=new Button{Content=CloseGlyph(),Width=20,Height=20,Padding=new Thickness(0),MinHeight=0,
                Background=ShelfWindow.B("#E6F6F6F8"),BorderBrush=ShelfWindow.B("#EFFFFFFF"),BorderThickness=new Thickness(.75),
                Template=RoundTemplate(),Cursor=Cursors.Hand,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,
                Margin=new Thickness(4,HangingStyle.CardTop+4,0,0),Opacity=0,IsHitTestVisible=false,ToolTip="移除副本"};
            AutomationProperties.SetAutomationId(Remove,"Remove-"+index);AutomationProperties.SetName(Remove,"移除截图 "+(index+1));
            Remove.Click+=delegate{remove();};Panel.SetZIndex(Remove,3);Root.Children.Add(Remove);
            var receipt=new StackPanel{Orientation=Orientation.Horizontal};receipt.Children.Add(new System.Windows.Shapes.Path{Data=Geometry.Parse("M1,5 L4,8 L10,1"),Stroke=ShelfWindow.B("#CDE2D8"),StrokeThickness=1.4,Width=10,Height=9,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,6,0),StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round});receipt.Children.Add(new TextBlock{Text="已复制",Foreground=Brushes.White,FontSize=11,FontWeight=FontWeights.Medium});
            copied=new Border{Background=ShelfWindow.B("#F02B302D"),BorderBrush=ShelfWindow.B("#28FFFFFF"),BorderThickness=new Thickness(.5),CornerRadius=new CornerRadius(6),
                Padding=new Thickness(9,5,9,5),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top,
                Margin=new Thickness(-20,PhotoHeight+HangingStyle.CardTop+2*HangingStyle.Inset+6,-20,0),Visibility=Visibility.Hidden,IsHitTestVisible=false,
                Effect=new DropShadowEffect{BlurRadius=8,ShadowDepth=2,Opacity=.12},Child=receipt};
            Panel.SetZIndex(copied,4);Root.Children.Add(copied);
            copiedTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(1400)};copiedTimer.Tick+=delegate{copiedTimer.Stop();copied.Visibility=Visibility.Hidden;};
            Root.MouseEnter+=delegate{Hover(true);};Root.MouseLeave+=delegate{Hover(false);};
            Root.IsKeyboardFocusWithinChanged+=delegate{Hover(Root.IsKeyboardFocusWithin||Root.IsMouseOver);};
            Button.Click+=delegate{copy();};
        }
        public void Renumber(int index) {
            AutomationProperties.SetAutomationId(Button,"Clip-"+index);AutomationProperties.SetName(Button,"复制截图 "+(index+1)+"，"+Clip.Width+" 乘 "+Clip.Height);
            AutomationProperties.SetAutomationId(Remove,"Remove-"+index);AutomationProperties.SetName(Remove,"移除截图 "+(index+1));
        }
        public void Move(double center,double top,bool newlyAdded) {
            double nextX=center-Root.Width/2,nextY=top-HangingStyle.Anchor,dx=placed?x+position.X-nextX:0,dy=placed?y+position.Y-nextY:0;
            Canvas.SetLeft(Root,nextX);Canvas.SetTop(Root,nextY);x=nextX;y=nextY;
            if(animate()&&placed) {Motion.To(position,TranslateTransform.XProperty,dx,0,550,true);Motion.To(position,TranslateTransform.YProperty,dy,0,550,true);}
            if(!placed&&newlyAdded&&animate()) {Motion.To(arrival,TranslateTransform.YProperty,-46,0,450,true);Motion.To(swing,RotateTransform.AngleProperty,Tilt+16,Tilt,1700,true);Motion.Fade(Root,0,1,180);}
            placed=true;
        }
        public Rect HitRect(Canvas stage) { return Root.TransformToAncestor(stage).TransformBounds(new Rect(0,HangingStyle.CardTop,Root.Width,PhotoHeight+2*HangingStyle.Inset)); }
        public MaterialShape MaterialOutline(Visual ancestor,double dpi) {
            var transform=Button.TransformToAncestor(ancestor);Point a=transform.Transform(new Point()),b=transform.Transform(new Point(1,0)),c=transform.Transform(new Point(0,1));
            return new MaterialShape{Width=Button.ActualWidth,Height=Button.ActualHeight,Dpi=dpi,Transform=new Matrix(b.X-a.X,b.Y-a.Y,c.X-a.X,c.Y-a.Y,a.X,a.Y)};
        }
        public void Material(bool native){if(materialNative==native)return;materialNative=native;Button.Background=native?NativeGlass():Glass();}
        public void Flying(bool value){Root.BeginAnimation(UIElement.OpacityProperty,null);Root.Opacity=value?0:1;if(value){arrival.BeginAnimation(TranslateTransform.YProperty,null);arrival.Y=0;swing.BeginAnimation(RotateTransform.AngleProperty,null);swing.Angle=Tilt;}else Nudge(2.2);}
        void Hover(bool on) {
            Remove.IsHitTestVisible=on;
            if(animate()){Motion.Fade(Remove,Remove.Opacity,on?1:0,150);Motion.To(scale,ScaleTransform.ScaleXProperty,scale.ScaleX,on?HangingStyle.HoverScale:1,220,true);Motion.To(scale,ScaleTransform.ScaleYProperty,scale.ScaleY,on?HangingStyle.HoverScale:1,220,true);}
            else {Remove.Opacity=on?1:0;scale.ScaleX=scale.ScaleY=on?HangingStyle.HoverScale:1;}
            var shadow=(DropShadowEffect)Button.Effect;if(animate()){Motion.To(shadow,DropShadowEffect.OpacityProperty,shadow.Opacity,on?.21:.16,180,false);Motion.To(shadow,DropShadowEffect.BlurRadiusProperty,shadow.BlurRadius,on?16:12,220,false);}else{shadow.Opacity=on?.21:.16;shadow.BlurRadius=on?16:12;}
            Canvas.SetZIndex(Root,on?5:1);
        }
        public void Press(bool down) {
            double next=down?HangingStyle.PressScale:(Root.IsMouseOver?HangingStyle.HoverScale:1);
            if(animate()){Motion.To(scale,ScaleTransform.ScaleXProperty,scale.ScaleX,next,down?140:220,!down);Motion.To(scale,ScaleTransform.ScaleYProperty,scale.ScaleY,next,down?140:220,!down);}
            else scale.ScaleX=scale.ScaleY=next;
        }
        public void Copied() {copied.Visibility=Visibility.Visible;copiedTimer.Stop();copiedTimer.Start();if(animate()){Motion.Fade(copied,0,1,130);Nudge(3);}}
        public void Nudge(double degrees) {if(animate()&&!Root.IsMouseOver&&!Root.IsKeyboardFocusWithin)Motion.To(swing,RotateTransform.AngleProperty,Tilt+degrees,Tilt,1900,true);}
        public void Dispose(){copiedTimer.Stop();}
        public static Brush Glass() {
            var b=new LinearGradientBrush{StartPoint=new Point(0,0),EndPoint=new Point(.25,1)};
            b.GradientStops.Add(new GradientStop(Color.FromArgb(95,255,255,255),0));b.GradientStops.Add(new GradientStop(Color.FromArgb(38,242,244,241),.48));b.GradientStops.Add(new GradientStop(Color.FromArgb(62,250,252,249),1));return b;
        }
        public static Brush NativeGlass(){return new LinearGradientBrush(Color.FromArgb(22,255,255,255),Color.FromArgb(8,243,246,243),new Point(0,0),new Point(0,1));}
        public static Brush Edge(){return new LinearGradientBrush(Color.FromArgb(110,255,255,255),Color.FromArgb(22,255,255,255),new Point(0,0),new Point(0,1));}
        public static Border Peg() {
            var metal=new LinearGradientBrush{StartPoint=new Point(0,0),EndPoint=new Point(1,0)};
            metal.GradientStops.Add(new GradientStop(Color.FromRgb(155,162,157),0));metal.GradientStops.Add(new GradientStop(Color.FromRgb(226,231,225),.28));
            metal.GradientStops.Add(new GradientStop(Color.FromRgb(191,199,191),.72));metal.GradientStops.Add(new GradientStop(Color.FromRgb(134,144,136),1));
            var peg=new Border{Width=HangingStyle.PegWidth,Height=HangingStyle.PegHeight,CornerRadius=new CornerRadius(1.8),Background=metal,BorderThickness=new Thickness(.35),
                BorderBrush=new LinearGradientBrush(ShelfWindow.B("#C0FFFFFF").Color,ShelfWindow.B("#40303830").Color,90),
                IsHitTestVisible=false,Effect=new DropShadowEffect{BlurRadius=2,ShadowDepth=.8,Opacity=.17,Color=Color.FromRgb(31,39,33)}};
            var detail=new Canvas();peg.Child=detail;
            var slot=new Border{Width=3.4,Height=.6,Background=ShelfWindow.B("#69434E45")};Canvas.SetLeft(slot,.7);Canvas.SetTop(slot,HangingStyle.Anchor-.35);detail.Children.Add(slot);
            var lip=new Border{Width=3.1,Height=.35,Background=ShelfWindow.B("#90FFFFFF")};Canvas.SetLeft(lip,.8);Canvas.SetTop(lip,HangingStyle.Anchor+.4);detail.Children.Add(lip);return peg;
        }
        static UIElement CloseGlyph(){return new System.Windows.Shapes.Path{Data=Geometry.Parse("M 1,1 L 7,7 M 7,1 L 1,7"),Stroke=ShelfWindow.B("#454950"),StrokeThickness=1.3,Width=8,Height=8,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round};}
        public static ControlTemplate RoundTemplate(){return Template(20,true);}
        public static ControlTemplate PhotoTemplate(){return Template(HangingStyle.Radius,false);}
        static ControlTemplate Template(double radius,bool round) {
            var border=new FrameworkElementFactory(round?typeof(Border):typeof(PhotoChrome));border.Name="Chrome";border.SetValue(Border.CornerRadiusProperty,new CornerRadius(radius));
            border.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));border.SetValue(Border.BorderBrushProperty,new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty,new TemplateBindingExtension(Control.BorderThicknessProperty));border.SetValue(Border.PaddingProperty,new TemplateBindingExtension(Control.PaddingProperty));
            var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetValue(ContentPresenter.ContentProperty,new TemplateBindingExtension(ContentControl.ContentProperty));content.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);border.AppendChild(content);
            var t=new ControlTemplate(typeof(Button)){VisualTree=border};
            var focus=new Trigger{Property=UIElement.IsKeyboardFocusedProperty,Value=true};focus.Setters.Add(new Setter(Border.BorderBrushProperty,ShelfWindow.B("#28645A"),"Chrome"));t.Triggers.Add(focus);
            if(round){var hover=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(Border.BackgroundProperty,ShelfWindow.B("#FFFFFFFF"),"Chrome"));t.Triggers.Add(hover);}return t;
        }
    }
    public sealed class PhotoChrome : Border {
        protected override void OnRender(DrawingContext dc) {
            base.OnRender(dc);double w=ActualWidth,h=ActualHeight;if(w<3||h<3)return;
            dc.DrawRoundedRectangle(null,new Pen(ShelfWindow.B("#30303932"),.35),new Rect(-.15,-.15,w+.3,h+.3),HangingStyle.Radius+.15,HangingStyle.Radius+.15);
        }
    }
    sealed class FloatingPhoto : Window {
        readonly DispatcherTimer follow;
        readonly bool falling;
        IntPtr handle;
        Native.POINT lastPointer;bool followed;
        public void FollowCursor() {
            if(handle==IntPtr.Zero||falling)return;Native.POINT p;Native.GetCursorPos(out p);
            if(followed&&p.X==lastPointer.X&&p.Y==lastPointer.Y)return;followed=true;lastPointer=p;
            double s=Native.Scale(handle);Native.SetWindowPos(handle,new IntPtr(-1),(int)(p.X-Width*s/2),(int)(p.Y-36*s),0,0,0x1|0x10);
        }
        public FloatingPhoto(HangingPhoto source,bool fall) {
            falling=fall;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;
            Topmost=true;ShowInTaskbar=false;ShowActivated=false;IsHitTestVisible=false;
            Width=source.PhotoWidth+88;Height=source.PhotoHeight+(fall?640:70);
            var canvas=new Canvas();Content=canvas;
            var frame=new Border{Width=source.PhotoWidth+2*HangingStyle.Inset,Height=source.PhotoHeight+2*HangingStyle.Inset,Padding=new Thickness(HangingStyle.Inset-HangingStyle.EdgeWidth),CornerRadius=new CornerRadius(HangingStyle.Radius),
                Background=HangingPhoto.Glass(),BorderBrush=HangingPhoto.Edge(),BorderThickness=new Thickness(HangingStyle.EdgeWidth),Effect=new DropShadowEffect{BlurRadius=12,ShadowDepth=3,Opacity=.16},
                Child=new Image{Source=source.Clip.Thumbnail,Stretch=Stretch.Fill,Clip=new RectangleGeometry(new Rect(0,0,source.PhotoWidth,source.PhotoHeight),HangingStyle.Radius-HangingStyle.Inset,HangingStyle.Radius-HangingStyle.Inset)}};
            Canvas.SetLeft(frame,40);Canvas.SetTop(frame,20);canvas.Children.Add(frame);
            var rotate=new RotateTransform(source.Tilt,(source.PhotoWidth+2*HangingStyle.Inset)/2,0);var translate=new TranslateTransform();var group=new TransformGroup();group.Children.Add(rotate);group.Children.Add(translate);frame.RenderTransform=group;
            Point origin=source.Button.PointToScreen(new Point(0,0));
            SourceInitialized+=delegate{handle=new WindowInteropHelper(this).Handle;Native.SetStyle(handle,0x08000000|0x80|0x20,true);double s=Native.Scale(handle);Native.SetWindowPos(handle,new IntPtr(-1),(int)(origin.X-40*s),(int)(origin.Y-20*s),(int)(Width*s),(int)(Height*s),0x10|0x40);};
            follow=new DispatcherTimer(DispatcherPriority.Normal){Interval=TimeSpan.FromMilliseconds(33)};
            follow.Tick+=delegate{FollowCursor();};
            Loaded+=delegate {
                if(!falling){Opacity=.9;follow.Start();return;}
                var down=new DoubleAnimation(0,520,TimeSpan.FromMilliseconds(550)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseIn}};translate.BeginAnimation(TranslateTransform.YProperty,down);
                rotate.BeginAnimation(RotateTransform.AngleProperty,new DoubleAnimation(source.Tilt,source.Tilt+28,TimeSpan.FromMilliseconds(550)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseIn}});
                var fade=new DoubleAnimation(1,0,TimeSpan.FromMilliseconds(550)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseIn}};fade.Completed+=delegate{Close();};BeginAnimation(OpacityProperty,fade);
            };
            Closed+=delegate{follow.Stop();};
        }
    }
}
