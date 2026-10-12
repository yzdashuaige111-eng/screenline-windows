using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Windows.Shell;

namespace Screenline {
    public sealed class ImageWindow : Window {
        sealed class EditState {public BitmapSource Image;public StrokeCollection Strokes;}
        readonly ClipStore store;
        readonly InkCanvas ink;
        readonly Image image;
        readonly Grid drawing;
        readonly Canvas cropLayer;
        readonly System.Windows.Shapes.Rectangle cropBox;
        readonly Button apply,undo,redo,pen,highlight,eraser,crop,arrow,rectangle,ellipse,text;
        readonly Button save;
        bool saving;
        readonly TextBox textEntry;
        readonly System.Windows.Shapes.Path shapePreview;
        readonly TextBlock hint,dimensions;
        readonly string qaDir;
        readonly Stack<EditState> history=new Stack<EditState>(),future=new Stack<EditState>();
        Point cropStart;
        Rect selection;
        bool cropping,selecting;
        bool shaping;
        Point shapeStart;
        string activeTool="view";
        Color color=Color.FromRgb(225,82,67);
        double penSize;
        public ImageWindow(Clip source,ClipStore storage,bool editing,string evidence) {
            store=storage;qaDir=evidence;Title=editing?"Screenline 标注":"Screenline 预览";
            Width=1000;Height=740;MinWidth=740;MinHeight=500;WindowStartupLocation=WindowStartupLocation.CenterScreen;Topmost=true;
            WindowStyle=WindowStyle.None;
            WindowChrome.SetWindowChrome(this,new WindowChrome{CaptionHeight=48,ResizeBorderThickness=new Thickness(6),GlassFrameThickness=new Thickness(0),CornerRadius=new CornerRadius(10),UseAeroCaptionButtons=false});
            Background=ShelfWindow.B("#E9ECE7");Foreground=ShelfWindow.B("#303B32");FontFamily=new FontFamily("Segoe UI, Microsoft YaHei UI");FontSize=12;UseLayoutRounding=true;
            if(Application.Current.MainWindow!=null)Icon=Application.Current.MainWindow.Icon;
            var dock=new DockPanel();Content=dock;
            var caption=new Border{Height=48,Background=ShelfWindow.B("#FAFBF8"),BorderBrush=ShelfWindow.B("#DDE3DA"),BorderThickness=new Thickness(0,0,0,.5),Padding=new Thickness(19,0,10,0)};DockPanel.SetDock(caption,Dock.Top);dock.Children.Add(caption);
            var heading=new DockPanel{LastChildFill=true};caption.Child=heading;var windowActions=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(windowActions,Dock.Right);heading.Children.Add(windowActions);
            windowActions.Children.Add(WindowButton("最大化或还原","MaximizeEditor","M2,2 L12,2 L12,12 L2,12 Z",delegate{WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;}));
            windowActions.Children.Add(WindowButton("关闭预览","CloseEditor","M3,3 L11,11 M11,3 L3,11",Close));
            var titleRow=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};titleRow.Children.Add(new TextBlock{Text="Screenline",FontSize=13,FontWeight=FontWeights.SemiBold,Foreground=ShelfWindow.B("#314337")});titleRow.Children.Add(new Border{Width=1,Height=12,Background=ShelfWindow.B("#D4DBD0"),Margin=new Thickness(14,0,14,0)});titleRow.Children.Add(new TextBlock{Text="预览与标注",Foreground=ShelfWindow.B("#65715E"),FontSize=12,VerticalAlignment=VerticalAlignment.Center});heading.Children.Add(titleRow);
            var top=new Border{Background=ShelfWindow.B("#FAFBF8"),BorderBrush=ShelfWindow.B("#DDE3DA"),BorderThickness=new Thickness(0,0,0,.5),Padding=new Thickness(14,9,14,9)};
            DockPanel.SetDock(top,Dock.Top);dock.Children.Add(top);
            var toolbar=new WrapPanel{VerticalAlignment=VerticalAlignment.Center};top.Child=toolbar;
            pen=Tool("画笔","Pen","M2,14 L4,9 L12,1 L15,4 L7,12 Z M4,9 L7,12",delegate{SetTool("pen");});toolbar.Children.Add(pen);
            highlight=Tool("荧光","Highlight","M2,12 L8,6 L12,10 L6,16 M8,6 L11,2 L16,7 L12,10 M1,17 L7,17",delegate{SetTool("highlight");});toolbar.Children.Add(highlight);
            eraser=Tool("橡皮","Eraser","M2,11 L10,2 L16,8 L9,16 L6,16 Z M6,7 L12,13 M9,16 L17,16",delegate{SetTool("eraser");});toolbar.Children.Add(eraser);
            arrow=Tool("箭头","Arrow","M2,15 L15,2 M6,2 L15,2 L15,11",delegate{SetTool("arrow");});toolbar.Children.Add(arrow);
            rectangle=Tool("矩形","Rectangle","M2,3 L16,3 L16,15 L2,15 Z",delegate{SetTool("rectangle");});toolbar.Children.Add(rectangle);
            ellipse=Tool("椭圆","Ellipse","M9,2 A7,7 0 1 1 8.99,2",delegate{SetTool("ellipse");});toolbar.Children.Add(ellipse);
            text=Tool("文字","Text","M2,3 L16,3 M9,3 L9,16 M5,16 L13,16",delegate{SetTool("text");});toolbar.Children.Add(text);
            crop=Tool("裁剪","Crop","M4,1 L4,13 L17,13 M1,4 L13,4 L13,17",delegate{SetTool("crop");});toolbar.Children.Add(crop);
            toolbar.Children.Add(new Border{Width=1,Height=18,Margin=new Thickness(12,0,9,0),Background=ShelfWindow.B("#D9E0D5")});
            undo=Tool("撤销","Undo","M7,3 L2,8 L7,13 M2,8 L11,8 Q17,8 16,16",Undo);redo=Tool("重做","Redo","M10,3 L15,8 L10,13 M15,8 L6,8 Q0,8 1,16",Redo);toolbar.Children.Add(undo);toolbar.Children.Add(redo);
            var options=new Border{MinHeight=48,Background=ShelfWindow.B("#F3F5F0"),BorderBrush=ShelfWindow.B("#DDE3DA"),BorderThickness=new Thickness(0,0,0,.5),Padding=new Thickness(19,8,19,8)};
            DockPanel.SetDock(options,Dock.Top);dock.Children.Add(options);var row=new StackPanel{Orientation=Orientation.Horizontal};options.Child=row;
            row.Children.Add(new TextBlock{Text="颜色",VerticalAlignment=VerticalAlignment.Center,Foreground=ShelfWindow.B("#66707E"),Margin=new Thickness(0,0,8,0)});
            foreach(string hex in new[]{"#E15243","#4274BF","#343A44","#35886D"}) {
                Color choice=ShelfWindow.B(hex).Color;
                var swatch=new Button{Width=26,Height=26,Margin=new Thickness(2,0,2,0),Background=Brushes.Transparent,BorderBrush=ShelfWindow.B("#D3DBD0"),BorderThickness=new Thickness(.5),Template=HangingPhoto.RoundTemplate(),ToolTip=hex,Content=new System.Windows.Shapes.Ellipse{Width=16,Height=16,Fill=new SolidColorBrush(choice)}};
                AutomationProperties.SetName(swatch,"画笔颜色 "+hex);swatch.Click+=delegate{color=choice;SetTool(activeTool=="view"?"pen":activeTool);};row.Children.Add(swatch);
            }
            row.Children.Add(new TextBlock{Text="粗细",VerticalAlignment=VerticalAlignment.Center,Foreground=ShelfWindow.B("#66707E"),Margin=new Thickness(20,0,10,0)});
            penSize=Math.Max(3,source.Width/250.0);var size=new Slider{Minimum=1,Maximum=20,Value=Math.Min(20,penSize),Width=100,VerticalAlignment=VerticalAlignment.Center,ToolTip="画笔粗细"};
            AutomationProperties.SetName(size,"画笔粗细");size.ValueChanged+=delegate{penSize=size.Value;if(ink!=null&&!ink.DefaultDrawingAttributes.IsHighlighter)ink.DefaultDrawingAttributes.Width=ink.DefaultDrawingAttributes.Height=penSize;};row.Children.Add(size);
            dimensions=new TextBlock{Text=source.Width+" × "+source.Height,Foreground=ShelfWindow.B("#66707E"),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(24,0,0,0)};row.Children.Add(dimensions);
            textEntry=new TextBox{Width=180,MaxLength=200,Padding=new Thickness(7,4,7,4),Margin=new Thickness(16,0,0,0),Visibility=Visibility.Collapsed,Text="标注文字",Background=ShelfWindow.B("#FDFEFB"),BorderBrush=ShelfWindow.B("#C4D0C0"),BorderThickness=new Thickness(1),CaretBrush=ShelfWindow.B("#365D42")};AutomationProperties.SetAutomationId(textEntry,"AnnotationText");AutomationProperties.SetName(textEntry,"输入标注文字后点击画布放置");row.Children.Add(textEntry);
            var footer=new Border{Background=ShelfWindow.B("#FAFBF8"),BorderBrush=ShelfWindow.B("#DDE3DA"),BorderThickness=new Thickness(0,.5,0,0),Padding=new Thickness(19,13,19,13)};
            DockPanel.SetDock(footer,Dock.Bottom);dock.Children.Add(footer);var bottom=new DockPanel();footer.Child=bottom;
            var actions=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(actions,Dock.Right);bottom.Children.Add(actions);
            actions.Children.Add(ShelfWindow.ActionButton("复制","CopyEdited",delegate{var d=new DataObject();d.SetImage(Render());d.SetData("Screenline.Origin","edited");Clipboard.SetDataObject(d,true);hint.Text="已复制，原始截图保留";}));
            actions.Children.Add(ShelfWindow.ActionButton("另存为…","ExportEdited",Export));
            save=ShelfWindow.ActionButton("保存副本","SaveEdited",SaveCopy);save.Background=ShelfWindow.B("#344F3E");save.Foreground=Brushes.White;save.BorderBrush=ShelfWindow.B("#344F3E");save.Template=ShelfWindow.ButtonTemplate(true);save.Padding=new Thickness(18,7,18,7);actions.Children.Add(save);
            hint=new TextBlock{Text="原图保留 · Ctrl+Z 撤销 · Esc 关闭",VerticalAlignment=VerticalAlignment.Center,Foreground=ShelfWindow.B("#697382"),TextWrapping=TextWrapping.Wrap};bottom.Children.Add(hint);
            var full=source.ReadOriginal();drawing=new Grid{Width=full.PixelWidth,Height=full.PixelHeight};
            image=new Image{Source=full,Width=full.PixelWidth,Height=full.PixelHeight,Stretch=Stretch.Fill};drawing.Children.Add(image);
            ink=new InkCanvas{Width=full.PixelWidth,Height=full.PixelHeight,Background=Brushes.Transparent,EditingMode=InkCanvasEditingMode.None};
            AutomationProperties.SetAutomationId(ink,"InkSurface");AutomationProperties.SetName(ink,"标注与裁剪画布");
            ink.DefaultDrawingAttributes=new DrawingAttributes{Color=color,Width=penSize,Height=penSize,FitToCurve=true};drawing.Children.Add(ink);
            cropLayer=new Canvas{Width=full.PixelWidth,Height=full.PixelHeight,IsHitTestVisible=false};drawing.Children.Add(cropLayer);
            cropBox=new System.Windows.Shapes.Rectangle{Stroke=ShelfWindow.B("#3E648E"),StrokeThickness=Math.Max(1.5,full.PixelWidth/350.0),StrokeDashArray=new DoubleCollection(new[]{4.0,3.0}),Fill=ShelfWindow.B("#164276A5"),Visibility=Visibility.Hidden};cropLayer.Children.Add(cropBox);
            shapePreview=new System.Windows.Shapes.Path{Visibility=Visibility.Hidden,IsHitTestVisible=false};cropLayer.Children.Add(shapePreview);
            apply=ShelfWindow.ActionButton("应用裁剪 ↵","ApplyCrop",ApplyCrop);apply.Visibility=Visibility.Collapsed;toolbar.Children.Add(apply);
            ink.PreviewMouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e) {
                if(cropping){selecting=true;cropStart=e.GetPosition(ink);selection=Rect.Empty;cropBox.Visibility=Visibility.Visible;ink.CaptureMouse();e.Handled=true;}
                else if(activeTool=="text"){PlaceText(e.GetPosition(ink));e.Handled=true;}
                else if(activeTool=="arrow"||activeTool=="rectangle"||activeTool=="ellipse"){shaping=true;shapeStart=e.GetPosition(ink);ink.CaptureMouse();e.Handled=true;}
                else if(ink.EditingMode!=InkCanvasEditingMode.None)Remember();
            };
            ink.PreviewMouseMove+=delegate(object s,MouseEventArgs e){var at=Bound(e.GetPosition(ink));if(shaping){var geometry=new StreamGeometry();using(var dc=geometry.Open())foreach(var segment in ShapePoints(at)){dc.BeginFigure(segment[0],false,false);for(int i=1;i<segment.Length;i++)dc.LineTo(segment[i],true,false);}shapePreview.Data=geometry;shapePreview.Stroke=new SolidColorBrush(color);shapePreview.StrokeThickness=penSize;shapePreview.Visibility=Visibility.Visible;e.Handled=true;return;}if(!selecting)return;selection=new Rect(cropStart,at);Canvas.SetLeft(cropBox,selection.Left);Canvas.SetTop(cropBox,selection.Top);cropBox.Width=selection.Width;cropBox.Height=selection.Height;};
            ink.PreviewMouseLeftButtonUp+=delegate(object s,MouseButtonEventArgs e){if(shaping){shaping=false;ink.ReleaseMouseCapture();shapePreview.Visibility=Visibility.Hidden;CommitShape(Bound(e.GetPosition(ink)));e.Handled=true;return;}if(!selecting)return;selecting=false;ink.ReleaseMouseCapture();apply.Visibility=selection.Width>=4&&selection.Height>=4?Visibility.Visible:Visibility.Collapsed;e.Handled=true;};
            var frame=new Border{Child=new Viewbox{Child=drawing,Stretch=Stretch.Uniform},Margin=new Thickness(36,28,36,28),
                Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=16,ShadowDepth=4,Opacity=.13,Color=Color.FromRgb(43,53,43)}};dock.Children.Add(frame);
            SetTool(editing?"pen":"view");UpdateHistory();
            KeyDown+=delegate(object s,KeyEventArgs e){if(e.Key==Key.Escape){if(cropping)SetTool("pen");else Close();e.Handled=true;}if(e.Key==Key.Enter&&cropping){ApplyCrop();e.Handled=true;}if(e.Key==Key.Z&&Keyboard.Modifiers==ModifierKeys.Control){Undo();e.Handled=true;}if(e.Key==Key.Y&&Keyboard.Modifiers==ModifierKeys.Control){Redo();e.Handled=true;}};
            Loaded+=delegate{if(qaDir!=null)Dispatcher.BeginInvoke(new Action(SaveQa),DispatcherPriority.ContextIdle);};
        }
        void SetTool(string tool) {
            activeTool=tool;shaping=false;shapePreview.Visibility=Visibility.Hidden;textEntry.Visibility=tool=="text"?Visibility.Visible:Visibility.Collapsed;
            cropping=tool=="crop";selecting=false;cropBox.Visibility=Visibility.Hidden;apply.Visibility=Visibility.Collapsed;
            ink.EditingMode=tool=="view"||cropping||tool=="arrow"||tool=="rectangle"||tool=="ellipse"||tool=="text"?InkCanvasEditingMode.None:tool=="eraser"?InkCanvasEditingMode.EraseByStroke:InkCanvasEditingMode.Ink;
            ink.DefaultDrawingAttributes.Color=tool=="highlight"?Colors.Gold:color;ink.DefaultDrawingAttributes.IsHighlighter=tool=="highlight";
            ink.DefaultDrawingAttributes.Width=ink.DefaultDrawingAttributes.Height=tool=="highlight"?Math.Max(14,penSize*4):penSize;
            foreach(var b in new[]{pen,highlight,eraser,crop,arrow,rectangle,ellipse,text}){b.Background=Brushes.Transparent;b.BorderBrush=Brushes.Transparent;}
            var active=tool=="pen"?pen:tool=="highlight"?highlight:tool=="eraser"?eraser:tool=="crop"?crop:tool=="arrow"?arrow:tool=="rectangle"?rectangle:tool=="ellipse"?ellipse:tool=="text"?text:null;
            if(active!=null){active.Background=ShelfWindow.B("#E1E9DE");active.BorderBrush=ShelfWindow.B("#BFCFBA");Title="Screenline 标注";}
            hint.Text=cropping?"拖选区域，按 Enter 应用；Esc 取消":tool=="text"?"输入文字后，点击图片放置 · 可撤销":tool=="arrow"||tool=="rectangle"||tool=="ellipse"?"在图片上拖动绘制 · 可撤销":"原图保留 · Ctrl+Z 撤销 · Ctrl+Y 重做";
        }
        static Button Tool(string label,string id,string geometry,Action action) {
            var button=ShelfWindow.ActionButton(label,id,action);button.MinHeight=32;button.Padding=new Thickness(9,6,9,6);button.Background=Brushes.Transparent;button.BorderBrush=Brushes.Transparent;button.ToolTip=label;
            var row=new StackPanel{Orientation=Orientation.Horizontal};row.Children.Add(new System.Windows.Shapes.Path{Data=Geometry.Parse(geometry),Width=16,Height=16,Stretch=Stretch.Uniform,Stroke=ShelfWindow.B("#52644F"),StrokeThickness=1.25,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,6,0)});row.Children.Add(new TextBlock{Text=label,VerticalAlignment=VerticalAlignment.Center});button.Content=row;return button;
        }
        static Button WindowButton(string label,string id,string geometry,Action action){var b=ShelfWindow.ActionButton(label,id,action);b.Width=34;b.Height=30;b.MinHeight=30;b.Padding=new Thickness(9);b.Background=Brushes.Transparent;b.BorderBrush=Brushes.Transparent;b.ToolTip=label;b.Content=new System.Windows.Shapes.Path{Data=Geometry.Parse(geometry),Width=12,Height=12,Stretch=Stretch.Uniform,Stroke=ShelfWindow.B("#596754"),StrokeThickness=1.15,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round};WindowChrome.SetIsHitTestVisibleInChrome(b,true);return b;}
        Point Bound(Point point){return new Point(Math.Max(0,Math.Min(drawing.Width,point.X)),Math.Max(0,Math.Min(drawing.Height,point.Y)));}
        List<Point[]> ShapePoints(Point end) {
            var points=new List<Point[]>();
            if(activeTool=="rectangle"){var bounds=new Rect(shapeStart,end);points.Add(new[]{bounds.TopLeft,bounds.TopRight,bounds.BottomRight,bounds.BottomLeft,bounds.TopLeft});}
            else if(activeTool=="ellipse"){var bounds=new Rect(shapeStart,end);var oval=new Point[65];for(int i=0;i<oval.Length;i++){double angle=i*Math.PI*2/64;oval[i]=new Point(bounds.X+bounds.Width/2+Math.Cos(angle)*bounds.Width/2,bounds.Y+bounds.Height/2+Math.Sin(angle)*bounds.Height/2);}points.Add(oval);}
            else {points.Add(new[]{shapeStart,end});double angle=Math.Atan2(end.Y-shapeStart.Y,end.X-shapeStart.X),length=Math.Min((end-shapeStart).Length*.35,Math.Max(12,penSize*5));points.Add(new[]{new Point(end.X-length*Math.Cos(angle-.5),end.Y-length*Math.Sin(angle-.5)),end,new Point(end.X-length*Math.Cos(angle+.5),end.Y-length*Math.Sin(angle+.5))});}
            return points;
        }
        void CommitShape(Point end){if((end-shapeStart).Length<1)return;Remember();var attributes=ink.DefaultDrawingAttributes.Clone();attributes.IsHighlighter=false;attributes.FitToCurve=false;attributes.Color=color;attributes.Width=attributes.Height=penSize;foreach(var points in ShapePoints(end))ink.Strokes.Add(new Stroke(new StylusPointCollection(points),attributes));UpdateHistory();}
        void PlaceText(Point at) {
            string value=textEntry.Text.Trim();if(value.Length==0)return;if(value.Length>200)value=value.Substring(0,200);Remember();var current=Render();
            var scene=new DrawingVisual();using(var dc=scene.RenderOpen()) {
                dc.DrawImage(current,new Rect(0,0,current.PixelWidth,current.PixelHeight));dc.PushClip(new RectangleGeometry(new Rect(0,0,current.PixelWidth,current.PixelHeight)));
                var label=new FormattedText(value,System.Globalization.CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface("Segoe UI, Microsoft YaHei UI"),Math.Max(18,penSize*5),new SolidColorBrush(color),1);
                label.MaxTextWidth=Math.Max(1,current.PixelWidth-at.X);dc.DrawText(label,at);dc.Pop();
            }
            var output=new RenderTargetBitmap(current.PixelWidth,current.PixelHeight,96,96,PixelFormats.Pbgra32);output.Render(scene);output.Freeze();image.Source=output;ink.Strokes=new StrokeCollection();UpdateHistory();
        }
        EditState Current(){return new EditState{Image=(BitmapSource)image.Source,Strokes=ink.Strokes.Clone()};}
        void Remember(){history.Push(Current());future.Clear();UpdateHistory();}
        void Restore(EditState state){image.Source=state.Image;double w=state.Image.PixelWidth,h=state.Image.PixelHeight;image.Width=ink.Width=drawing.Width=cropLayer.Width=w;image.Height=ink.Height=drawing.Height=cropLayer.Height=h;ink.Strokes=state.Strokes.Clone();dimensions.Text=w+" × "+h;SetTool("pen");UpdateHistory();}
        void Undo(){if(history.Count==0)return;future.Push(Current());Restore(history.Pop());}
        void Redo(){if(future.Count==0)return;history.Push(Current());Restore(future.Pop());}
        void UpdateHistory(){undo.IsEnabled=history.Count>0;redo.IsEnabled=future.Count>0;undo.Opacity=undo.IsEnabled?1:.4;redo.Opacity=redo.IsEnabled?1:.4;}
        void ApplyCrop(){if(selection.IsEmpty||selection.Width<4||selection.Height<4)return;Remember();cropBox.Visibility=Visibility.Hidden;var full=Render();int x=Math.Max(0,(int)selection.X),y=Math.Max(0,(int)selection.Y);var result=new CroppedBitmap(full,new Int32Rect(x,y,Math.Min((int)selection.Width,full.PixelWidth-x),Math.Min((int)selection.Height,full.PixelHeight-y)));result.Freeze();Restore(new EditState{Image=result,Strokes=new StrokeCollection()});}
        BitmapSource Render(){var source=(BitmapSource)image.Source;var scene=new DrawingVisual();using(var dc=scene.RenderOpen()){dc.DrawImage(source,new Rect(0,0,source.PixelWidth,source.PixelHeight));ink.Strokes.Draw(dc);}var b=new RenderTargetBitmap(source.PixelWidth,source.PixelHeight,96,96,PixelFormats.Pbgra32);b.Render(scene);b.Freeze();return b;}
        async void SaveCopy(){if(saving)return;saving=true;save.IsEnabled=false;hint.Text="正在保存副本…";try{await store.AddAsync(Render());if(IsVisible)Close();}catch(Exception ex){hint.Text="保存失败："+ex.Message;}finally{saving=false;save.IsEnabled=true;}}
        async void Export(){var dlg=new Microsoft.Win32.SaveFileDialog{Filter="PNG 图片|*.png",FileName="标注-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".png",InitialDirectory=Path.Combine(store.Root,"exports")};Directory.CreateDirectory(dlg.InitialDirectory);if(dlg.ShowDialog(this)==true){var bitmap=Render();hint.Text="正在导出…";try{await Task.Run(delegate{File.WriteAllBytes(dlg.FileName,ClipStore.Png(bitmap));});hint.Text="已导出，原始截图保留";}catch(Exception ex){hint.Text="导出失败："+ex.Message;}}}
        void SaveQa(){try{var view=(FrameworkElement)Content;double s=Native.Scale(new WindowInteropHelper(this).Handle);var b=new RenderTargetBitmap((int)(view.ActualWidth*s),(int)(view.ActualHeight*s),96*s,96*s,PixelFormats.Pbgra32);var backdrop=new DrawingVisual();using(var dc=backdrop.RenderOpen())dc.DrawRectangle(Background,null,new Rect(0,0,view.ActualWidth,view.ActualHeight));b.Render(backdrop);b.Render(view);File.WriteAllBytes(Path.Combine(qaDir,"annotation.png"),ClipStore.Png(b));}catch(Exception){}}
    }
}
