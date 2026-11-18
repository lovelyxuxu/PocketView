using System;
using System.IO;
using System.Linq;
using PocketView;
class StoreTests {
 static int count;
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);count++;}
 static void Main(string[] args){
  string root=Path.Combine(args[0],"store-tests-"+DateTime.Now.ToString("yyyyMMddHHmmssfff"));var s=new Store(root);
  Check(s.Favorites.Count==0,"new profile starts with empty favorites");
  Check(Store.Normalize("localhost:8080/a",false)=="http://localhost:8080/a","localhost uses http");
  Check(Store.Normalize("example.com",false)=="https://example.com/","bare hostname normalized");
  Check(Store.Normalize("阅读测试",true).StartsWith("https://www.google.com/search?q="),"address search uses Google");
  bool blocked=false;try{Store.Normalize("javascript://alert(1)",false);}catch{blocked=true;}Check(blocked,"non-web schemes rejected");
  var a=s.Upsert(null,"文章","https://example.com/a","阅读");var b=s.Upsert(null,"视频","https://example.com/b","视频");
  a.Window=new WindowStateData{X=10,Y=20,Width=800,Height=600,Zoom=1.2,Pinned=true};s.SaveFavorites();
  s.Reorder(new[]{b.Id,b.Id,"missing",a.Id});Check(s.Favorites[0].Id==b.Id&&s.Favorites.Count==2,"reorder rejects duplicates and unknown IDs");
  var reload=new Store(root);Check(reload.Favorites[1].Window.Pinned&&reload.Favorites[1].Window.Width==800,"window state round trips");
  reload.Upsert(a.Id,"改名","https://example.com/a","工作");Check(reload.Favorites.Count==2,"edit does not duplicate");
  reload.Visit("文章","https://example.com/a");Check(File.Exists(Path.Combine(root,"浏览数据","历史.json")),"history kept with browsing data");
  Check(!File.Exists(Path.Combine(root,"个人数据","历史.json")),"personal folder contains no history");
  reload.Remove(b.Id);Check(new Store(root).Favorites.Count==1,"delete persisted");
  Check(File.Exists(Path.Combine(root,"个人数据","收藏.json.bak")),"atomic write retains previous backup");
  string portable=Path.Combine(root,"relocated");Directory.CreateDirectory(Path.Combine(portable,"个人数据"));foreach(string f in Directory.GetFiles(Path.Combine(root,"个人数据")))File.Copy(f,Path.Combine(portable,"个人数据",Path.GetFileName(f)));
  var moved=new Store(portable);Check(moved.Favorites.Count==1&&moved.History.Count==0,"copying personal data preserves favorites without browsing history");
  File.WriteAllText(Path.Combine(root,"个人数据","收藏.json"),"broken json");var damaged=new Store(root);Check(damaged.Warnings.Count==1&&Directory.GetFiles(damaged.Personal,"*.damaged-*").Length==1,"corrupt data is preserved and reported");
  Console.WriteLine("SUCCESS "+count+" storage checks");
 }
}
