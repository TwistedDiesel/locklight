// Locklight — Windows lock screen image browser.
// Copyright (C) 2026 Lawrence Shafer — https://shaferlabs.com
// SPDX-License-Identifier: GPL-3.0-or-later
// This program is free software: you can redistribute it and/or modify it
// under the terms of the GNU General Public License as published by the
// Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// This program is distributed WITHOUT ANY WARRANTY; without even the
// implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU General Public License in COPYING for details.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Security.Principal;
using System.Reflection;

[assembly: AssemblyTitle("Locklight")]
[assembly: AssemblyVersion("1.0.0.0")]
class Photo { public string Path, Kind; public int Width, Height; public DateTime Date; public string Hash; public override string ToString() { return Kind + "   ·   " + Width + " × " + Height; } }
static class Catalog {
 public static string CurrentPath;
 public static string Reg(RegistryKey root,string key,string name) { try { using(var k=root.OpenSubKey(key)) return k==null?null:k.GetValue(name) as string; } catch { return null; } }
 public static List<Photo> Scan() {
  var paths=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase); CurrentPath=null;
  Action<string,string> add=(p,k)=>{if(!String.IsNullOrEmpty(p)&&File.Exists(p)&&!paths.ContainsKey(p))paths.Add(p,k);};
  try { var t=Type.GetType("Windows.System.UserProfile.LockScreen, Windows.System.UserProfile, ContentType=WindowsRuntime"); var uri=t==null?null:t.GetProperty("OriginalImageFile").GetValue(null,null) as Uri; if(uri!=null&&uri.IsFile&&File.Exists(uri.LocalPath))CurrentPath=uri.LocalPath; } catch {}
  if(CurrentPath==null) CurrentPath=Reg(Registry.CurrentUser,@"Software\Microsoft\Windows\CurrentVersion\Lock Screen\Creative","LandscapeAssetPath");
  if(!File.Exists(CurrentPath))CurrentPath=null;
  bool spotlight=false;try{using(var c=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager"))spotlight=c!=null&&Convert.ToInt32(c.GetValue("RotatingLockScreenEnabled",0))==1;}catch{}
  if(spotlight)CurrentPath=null;
  add(CurrentPath,"Current lock screen");
  string sid=WindowsIdentity.GetCurrent().User.Value;
  try { using(var k=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\LogonUI\Creative\"+sid)) { if(k!=null)foreach(var n in k.GetSubKeyNames().OrderByDescending(x=>x,StringComparer.Ordinal)) using(var sub=k.OpenSubKey(n)) { add(sub.GetValue("landscapeImage") as string,"Recent Spotlight"); add(sub.GetValue("portraitImage") as string,"Recent Spotlight"); } } } catch {}
  string assets=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),@"Packages\Microsoft.Windows.ContentDeliveryManager_cw5n1h2txyewy\LocalState\Assets");
  try { foreach(var p in Directory.GetFiles(assets).OrderByDescending(File.GetLastWriteTimeUtc))add(p,"Spotlight cache"); } catch {}
  try {foreach(var p in Directory.GetFiles(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),@"Web\Screen")))add(p,"Windows collection");} catch {}
  var result=new List<Photo>();var hashes=new HashSet<string>();
  foreach(var kv in paths)try { using(var stream=File.OpenRead(kv.Key))using(var im=Image.FromStream(stream)) {if(im.Width<600||im.Height<600)continue;stream.Position=0;string hash;using(var sha=System.Security.Cryptography.SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();if(!hashes.Add(hash))continue; result.Add(new Photo{Path=kv.Key,Kind=kv.Value,Width=im.Width,Height=im.Height,Date=File.GetLastWriteTime(kv.Key),Hash=hash});} }catch{}
  return result;
 }
 public static Image Load(string p) {using(var s=File.OpenRead(p))using(var i=Image.FromStream(s))return new Bitmap(i);}
 public static void Save(Photo p,string target) {using(var im=Load(p.Path))im.Save(target,ImageFormat.Jpeg);}
}
class MainWindow:Form {
 List<Photo> all; ListBox list=new ListBox(); PictureBox preview=new PictureBox(); Label info=new Label(),status=new Label(); ComboBox filter=new ComboBox(); Button save,saveAll; bool busy;
 public MainWindow(){
  Text="Locklight · Lock screen image browser"; Size=new Size(1180,780); MinimumSize=new Size(840,560); StartPosition=FormStartPosition.CenterScreen; BackColor=Color.FromArgb(19,24,33); ForeColor=Color.White; Font=new Font("Segoe UI",10); AutoScaleMode=AutoScaleMode.Dpi;
  var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(22)};layout.RowStyles.Add(new RowStyle(SizeType.Absolute,76));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,46));Controls.Add(layout);
  var heading=new Label{Text="LOCKLIGHT\nYour lock screen, worth keeping.",AutoSize=true,Font=new Font("Segoe UI",16,FontStyle.Bold)};layout.Controls.Add(heading,0,0);
  var toolbar=new FlowLayoutPanel{Dock=DockStyle.Fill};layout.Controls.Add(toolbar,0,1);
  toolbar.Controls.Add(Btn("↻  Refresh",(s,e)=>Reload()));toolbar.Controls.Add(Btn("Go to current",(s,e)=>{filter.SelectedIndex=0;if(list.Items.Count>0)list.SelectedIndex=0;}));
  filter.DropDownStyle=ComboBoxStyle.DropDownList;filter.Width=150;filter.Items.AddRange(new object[]{"All images","Landscape","Portrait"});filter.SelectedIndex=0;filter.SelectedIndexChanged+=(s,e)=>Fill();toolbar.Controls.Add(filter);
  save=Btn("Save image…",SaveOne);save.BackColor=Color.FromArgb(42,108,210);toolbar.Controls.Add(save);saveAll=Btn("Save all…",SaveAll);toolbar.Controls.Add(saveAll);
  var body=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,275));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.Controls.Add(body,0,2);
  list.Dock=DockStyle.Fill;list.BackColor=Color.FromArgb(28,35,47);list.ForeColor=Color.White;list.BorderStyle=BorderStyle.None;list.IntegralHeight=false;list.DrawMode=DrawMode.OwnerDrawFixed;list.ItemHeight=94;list.DrawItem+=DrawItem;list.SelectedIndexChanged+=(s,e)=>ShowPhoto();body.Controls.Add(list,0,0);
  var right=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Padding=new Padding(16,0,0,0)};right.RowStyles.Add(new RowStyle(SizeType.Percent,100));right.RowStyles.Add(new RowStyle(SizeType.Absolute,60));body.Controls.Add(right,1,0);preview.Dock=DockStyle.Fill;preview.SizeMode=PictureBoxSizeMode.Zoom;preview.BackColor=Color.FromArgb(11,15,22);right.Controls.Add(preview,0,0);info.Dock=DockStyle.Fill;info.Padding=new Padding(0,12,0,0);right.Controls.Add(info,0,1);status.Dock=DockStyle.Fill;status.Padding=new Padding(0,12,0,0);status.ForeColor=Color.Silver;layout.Controls.Add(status,0,3);
  Shown+=(s,e)=>Reload();KeyPreview=true;KeyDown+=(s,e)=>{if(e.KeyCode==Keys.F5)Reload();if(e.Control&&e.KeyCode==Keys.S){SaveOne(s,e);e.SuppressKeyPress=true;}};
 }
 Button Btn(string text,EventHandler click){var b=new Button{Text=text,AutoSize=true,Height=34,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(37,46,61),ForeColor=Color.White,Padding=new Padding(8,2,8,2),Margin=new Padding(0,0,10,0)};b.FlatAppearance.BorderSize=0;b.Click+=click;return b;}
 Dictionary<string,Image> thumbs=new Dictionary<string,Image>();
 void DrawItem(object sender,DrawItemEventArgs e){if(e.Index<0)return;var p=(Photo)list.Items[e.Index];e.Graphics.FillRectangle(new SolidBrush((e.State&DrawItemState.Selected)!=0?Color.FromArgb(42,70,105):list.BackColor),e.Bounds);try{Image im;if(!thumbs.TryGetValue(p.Path,out im)){using(var src=Catalog.Load(p.Path)){im=new Bitmap(100,66);using(var g=Graphics.FromImage(im)){g.Clear(Color.FromArgb(11,15,22));float scale=Math.Min(100f/src.Width,66f/src.Height);g.DrawImage(src,(100-src.Width*scale)/2,(66-src.Height*scale)/2,src.Width*scale,src.Height*scale);}}thumbs[p.Path]=im;}e.Graphics.DrawImage(im,e.Bounds.X+8,e.Bounds.Y+12,100,66);}catch{}TextRenderer.DrawText(e.Graphics,p.Kind+"\n"+p.Width+" × "+p.Height,Font,new Rectangle(e.Bounds.X+116,e.Bounds.Y+20,e.Bounds.Width-120,64),Color.White,TextFormatFlags.WordBreak);e.DrawFocusRectangle();}
 async void Reload(){if(busy)return;busy=true;save.Enabled=saveAll.Enabled=false;status.Text="Reading Windows image cache…";try{all=await System.Threading.Tasks.Task.Run(()=>Catalog.Scan());foreach(var im in thumbs.Values)im.Dispose();thumbs.Clear();Fill();status.Text=all.Count+" images found. "+(Catalog.CurrentPath!=null?"Opened the Windows-reported current image.":"Current image could not be confirmed; showing the latest available Spotlight image.");}catch(Exception ex){status.Text="Could not read images: "+ex.Message;}finally{busy=false;save.Enabled=saveAll.Enabled=list.Items.Count>0;}}
 void Fill(){if(all==null)return;list.Items.Clear();foreach(var p in all)if(filter.SelectedIndex==0||(filter.SelectedIndex==1&&p.Width>=p.Height)||(filter.SelectedIndex==2&&p.Height>p.Width))list.Items.Add(p);if(list.Items.Count>0)list.SelectedIndex=0;else{if(preview.Image!=null)preview.Image.Dispose();preview.Image=null;info.Text="No images in this view. Try All images or Refresh.";}save.Enabled=saveAll.Enabled=list.Items.Count>0;}
 void ShowPhoto(){var p=list.SelectedItem as Photo;if(p==null)return;try{var im=Catalog.Load(p.Path);var old=preview.Image;preview.Image=im;if(old!=null)old.Dispose();info.Text=p.Kind+"  ·  "+p.Width+" × "+p.Height+"  ·  "+(new FileInfo(p.Path).Length/1024).ToString("N0")+" KB\nSave a full-resolution JPEG to any folder.";}catch(Exception ex){status.Text="This image is no longer available: "+ex.Message;}}
 string NameFor(Photo p){return "Locklight-"+p.Hash.Substring(0,12)+".jpg";}
 void SaveOne(object s,EventArgs e){var p=list.SelectedItem as Photo;if(p==null)return;using(var d=new SaveFileDialog{Title="Save lock screen image",Filter="JPEG image (*.jpg)|*.jpg",FileName=NameFor(p),InitialDirectory=Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),AddExtension=true,OverwritePrompt=true})if(d.ShowDialog(this)==DialogResult.OK)try{Catalog.Save(p,d.FileName);status.Text="Saved to "+d.FileName;}catch(Exception ex){MessageBox.Show(this,ex.Message,"Could not save image",MessageBoxButtons.OK,MessageBoxIcon.Error);}}
 void SaveAll(object s,EventArgs e){using(var d=new FolderBrowserDialog{Description="Save all images in the current view. Existing Locklight files will be kept.",SelectedPath=Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)})if(d.ShowDialog(this)==DialogResult.OK){int saved=0,skipped=0,failed=0;foreach(Photo p in list.Items)try{string target=Path.Combine(d.SelectedPath,NameFor(p));if(File.Exists(target)){skipped++;continue;}using(var stream=new FileStream(target,FileMode.CreateNew)){using(var im=Catalog.Load(p.Path))im.Save(stream,ImageFormat.Jpeg);}saved++;}catch{failed++;}status.Text=String.Format("Saved {0} · Already present {1} · Failed {2} — {3}",saved,skipped,failed,d.SelectedPath);}}
 protected override void Dispose(bool disposing){if(disposing){if(preview.Image!=null)preview.Image.Dispose();foreach(var im in thumbs.Values)im.Dispose();}base.Dispose(disposing);}
 [STAThread] static void Main(string[] args){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);if(args.Length>0&&args[0]=="--self-test"){var photos=Catalog.Scan();string dir=args.Length>1?args[1]:Path.GetTempPath();Directory.CreateDirectory(dir);if(photos.Count==0)Environment.Exit(2);string file=Path.Combine(dir,"export-test.jpg");Catalog.Save(photos[0],file);using(var im=Image.FromFile(file)){if(im.Width!=photos[0].Width||im.Height!=photos[0].Height)Environment.Exit(3);}File.WriteAllText(Path.Combine(dir,"test-results.txt"),"Images: "+photos.Count+"\r\nCurrent: "+(Catalog.CurrentPath??"unconfirmed")+"\r\nFirst: "+photos[0].Path+"\r\nJPEG export: passed");return;}Application.Run(new MainWindow());}
}
