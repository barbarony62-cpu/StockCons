using System.Data;
using System.Windows.Data;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace AskCons;
public partial class MainWindow {
 public static string GridText(DataGrid grid) {
  var sb=new StringBuilder();var columns=grid.Columns.Where(c=>c.Visibility==Visibility.Visible).OrderBy(c=>c.DisplayIndex).ToList();
  sb.AppendLine(string.Join("\t",columns.Select(c=>Convert.ToString(c.Header))));
  foreach(var item in grid.Items){if(item==CollectionView.NewItemPlaceholder)continue;sb.AppendLine(string.Join("\t",columns.Select(c=>{
   if(c is not DataGridBoundColumn bound||bound.Binding is not System.Windows.Data.Binding b)return "";
   object? value=item;
   if(item is DataRowView row){var key=b.Path.Path.Trim('[',']','\'','"');if(!row.DataView.Table!.Columns.Contains(key))key=Convert.ToString(c.Header)??key;value=row.DataView.Table.Columns.Contains(key)?row[key]:null;}
   else foreach(var name in b.Path.Path.Split('.'))value=value?.GetType().GetProperty(name)?.GetValue(value);
   if(value!=null&&!string.IsNullOrWhiteSpace(b.StringFormat)){try{value=string.Format(System.Globalization.CultureInfo.CurrentCulture,b.StringFormat.Contains('{')?b.StringFormat:"{0:"+b.StringFormat+"}",value);}catch(FormatException){}}
   return Convert.ToString(value,System.Globalization.CultureInfo.CurrentCulture)?.Replace("\t"," ").Replace("\r"," ").Replace("\n"," ")??"";
  })));}return sb.ToString();
 }
 public static string VisibleText(DependencyObject node) {
  if(node is UIElement ui&&ui.Visibility!=Visibility.Visible)return "";
  if(node is TextBox text)return text.Text;
  if(node is TextBlock label)return label.Text;
  if(node is DataGrid grid)return GridText(grid);
  if(node is ComboBox combo)return Convert.ToString(combo.SelectedItem is Account a?a.Name:combo.Text)??"";
  if(node is Image image)return image.ToolTip?.ToString()??"AskCons logo";
  if(node is not Visual && node is not System.Windows.Media.Media3D.Visual3D)return node is System.Windows.Documents.Run run?run.Text:"";
  var parts=new List<string>();for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++){var t=VisibleText(VisualTreeHelper.GetChild(node,i));if(!string.IsNullOrWhiteSpace(t))parts.Add(t);}return string.Join("\n",parts);
 }
 private void CopyText(string text){try{Clipboard.SetText(string.IsNullOrEmpty(text)?" ":text);}catch(Exception ex){AiStatus.Text="Clipboard unavailable: "+ex.Message;}}
 private void InstallCopySupport() {
  SecuritiesList.PreviewMouseRightButtonDown+=(_,e)=>e.Handled=true;
  ResultsGrid.ClipboardCopyMode=DataGridClipboardCopyMode.IncludeHeader;ResultsGrid.SelectionMode=DataGridSelectionMode.Extended;ResultsGrid.SelectionUnit=DataGridSelectionUnit.CellOrRowHeader;
  PreviewMouseRightButtonUp+=(_,e)=>{
   var node=e.OriginalSource as DependencyObject;if(node==null)return;
   for(var p=node;p!=null;p=CopyParent(p))if(p is TextBox)return; // Native select/copy menu remains intact.
   var text=VisibleText(node);if(string.IsNullOrWhiteSpace(text))for(var p=CopyParent(node);p!=null&&string.IsNullOrWhiteSpace(text);p=CopyParent(p))text=VisibleText(p);
   var menu=new ContextMenu();var copy=new MenuItem{Header="Copy this text"};copy.Click+=(_,_)=>CopyText(text);menu.Items.Add(copy);
   for(var p=node;p!=null;p=CopyParent(p))if(p is DataGrid table){var copyTable=new MenuItem{Header="Copy full table"};copyTable.Click+=(_,_)=>CopyText(GridText(table));menu.Items.Add(copyTable);break;}
   var all=new MenuItem{Header="Copy screen as text"};all.Click+=(_,_)=>CopyScreenClick(this,new RoutedEventArgs());menu.Items.Add(all);
   var image=new MenuItem{Header="Copy screen as image"};image.Click+=(_,_)=>CopyScreenImageClick(this,new RoutedEventArgs());menu.Items.Add(image);
   menu.PlacementTarget=this;menu.IsOpen=true;e.Handled=true;
  };
 }
 private static DependencyObject? CopyParent(DependencyObject node)=>node is Visual||node is System.Windows.Media.Media3D.Visual3D?VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node);
 private string TranscriptText()=>string.Join("\n\n",(AccountBox.SelectedItem is Account a?Session(a.Id).Turns:new()).Select(t=>t.Role+": "+t.Text));
 public string ScreenText()=>Title+"\nAccount: "+(AccountBox.SelectedItem as Account)?.Name+"\n\n"+AnswerHeading.Text+"\n"+AnswerSubtitle.Text+"\n"+(ResultsGrid.Visibility==Visibility.Visible?GridText(ResultsGrid):AnswerText.Text)+"\n"+AnswerNote.Text+"\n\nCONVERSATION\n"+TranscriptText()+"\n\nCONVERSATION LIST\n"+string.Join("\n\n",archive.Conversations.Where(c=>c.AccountId==(AccountBox.SelectedItem as Account)?.Id).Select(c=>c.Title+"\n"+string.Join("\n",c.Messages.Where(m=>m.Role=="You").Select(m=>m.Text))))+"\n\nSECURITIES\n"+string.Join("\n",portfolioSecurities.Select(s=>s.Display))+"\n\nSTATUS\n"+AiStatus.Text+"\n"+ElapsedText.Text+"\n\nDRAFT\n"+QuestionBox.Text+"\nPublic securities: "+PublicSecurityBox.Text+"\n\nVISIBLE CONTROLS\n"+VisibleText((DependencyObject)Content);
 private void CopyScreenClick(object sender,RoutedEventArgs e)=>CopyText(ScreenText());
 private void CopyScreenImageClick(object sender,RoutedEventArgs e){try{var target=(FrameworkElement)Content;var bitmap=new RenderTargetBitmap((int)Math.Ceiling(target.ActualWidth),(int)Math.Ceiling(target.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(target);Clipboard.SetImage(bitmap);}catch(Exception ex){AiStatus.Text="Unable to copy screen image: "+ex.Message;}}
 private void FullAnswerClick(object sender,RoutedEventArgs e)=>OpenReadingWindow("Current answer",()=>AnswerHeading.Text+"\n"+AnswerSubtitle.Text+"\n\n"+(ResultsGrid.Visibility==Visibility.Visible?GridText(ResultsGrid):AnswerText.Text)+"\n\n"+AnswerNote.Text);
 private void FullConversationClick(object sender,RoutedEventArgs e)=>OpenReadingWindow("Conversation",TranscriptText);
 private void OpenReadingWindow(string title,Func<string> read) {var window=BuildReadingWindow(title,read);window.Owner=this;window.Show();}
 public Window BuildReadingWindow(string title,Func<string> read) {
  var body=new TextBox{Text=read(),IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,FontSize=18,Padding=new Thickness(24),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto};
  var window=new Window{Title="AskCons — "+title,WindowState=WindowState.Maximized,MinWidth=600,MinHeight=400};
  var panel=new DockPanel();var bar=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(10)};
  var copy=new Button{Content="Copy all",Padding=new Thickness(14,7,14,7),Margin=new Thickness(0,0,10,0)};copy.Click+=(_,_)=>CopyText(body.Text);
  var close=new Button{Content="Close (Esc)",Padding=new Thickness(14,7,14,7)};close.Click+=(_,_)=>window.Close();bar.Children.Add(copy);bar.Children.Add(close);
  DockPanel.SetDock(bar,Dock.Top);panel.Children.Add(bar);panel.Children.Add(body);window.Content=panel;
  var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};timer.Tick+=(_,_)=>{var next=read();if(body.Text!=next){var offset=body.VerticalOffset;body.Text=next;body.ScrollToVerticalOffset(offset);}};
  window.Closed+=(_,_)=>timer.Stop();window.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape)window.Close();};window.Loaded+=(_,_)=>timer.Start();return window;
 }
}


