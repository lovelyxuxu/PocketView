using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace PocketView {
    class FavoriteDialog : Form {
        readonly TextBox title=new TextBox(),url=new TextBox(); readonly ComboBox group=new ComboBox();
        public string FavoriteTitle { get { return title.Text; } }
        public string FavoriteUrl { get { return url.Text; } }
        public string FavoriteGroup { get { return group.Text; } }
        public FavoriteDialog(Favorite existing,string pageTitle,string pageUrl,IEnumerable<string> groups) {
            Text=existing==null?"收藏网页":"编辑收藏";ClientSize=new Size(480,398);FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.CenterParent;Font=new Font("Microsoft YaHei UI",9);BackColor=Color.White;ForeColor=UiStyle.Ink;
            var heading=new Label{Text=Text,Font=new Font("Microsoft YaHei UI",15,FontStyle.Bold),AutoSize=true,Location=new Point(28,24)};Controls.Add(heading);UiStyle.Drag(heading,this);UiStyle.Drag(this,this);
            title.Text=existing==null?pageTitle:existing.Title;url.Text=existing==null?pageUrl:existing.Url;
            group.Items.AddRange(groups.Where(g=>!String.IsNullOrWhiteSpace(g)).Distinct().Cast<object>().ToArray());group.Text=existing==null?"":existing.Group;group.FlatStyle=FlatStyle.Flat;
            AddField("名称",title,84);AddField("网址",url,158);AddField("分组",group,232);
            var error=new Label{Bounds=new Rectangle(28,306,424,25),ForeColor=Color.FromArgb(187,77,77),AutoEllipsis=true};Controls.Add(error);
            var save=UiStyle.Button("保存",true);save.Location=new Point(362,340);save.Click+=delegate{try{Store.Normalize(url.Text,false);DialogResult=DialogResult.OK;}catch(Exception e){error.Text=e.Message;}};
            var cancel=UiStyle.Button("取消",false);cancel.Location=new Point(262,340);cancel.DialogResult=DialogResult.Cancel;
            var close=UiStyle.Button("×",false);close.Size=new Size(30,30);close.Location=new Point(422,24);close.AccessibleName="关闭收藏编辑";close.DialogResult=DialogResult.Cancel;
            Controls.Add(save);Controls.Add(cancel);Controls.Add(close);AcceptButton=save;CancelButton=cancel;UiStyle.Round(this,14);
        }
        void AddField(string label,Control input,int top) {
            Controls.Add(new Label{Text=label,Location=new Point(28,top),AutoSize=true,ForeColor=UiStyle.Muted});
            var surface=new Panel{Bounds=new Rectangle(28,top+25,424,34),BackColor=UiStyle.Surface};
            var box=input as TextBox;if(box!=null){box.BorderStyle=BorderStyle.None;box.MaxLength=label=="名称"?160:8192;}
            input.Bounds=new Rectangle(10,7,404,24);input.BackColor=UiStyle.Surface;input.ForeColor=UiStyle.Ink;surface.Controls.Add(input);Controls.Add(surface);UiStyle.Round(surface,7);
        }
    }
}
