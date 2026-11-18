using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace PocketView {
    public class WindowStateData {
        public int X { get; set; } public int Y { get; set; }
        public int Width { get; set; } public int Height { get; set; }
        public bool Pinned { get; set; } public double Zoom { get; set; }
        public WindowStateData() { Width = 1080; Height = 720; Zoom = 1; X = -32000; Y = -32000; }
    }
    public class Favorite {
        public string Id { get; set; } public string Title { get; set; }
        public string Url { get; set; } public string Group { get; set; }
        public string CreatedAt { get; set; } public WindowStateData Window { get; set; }
    }
    public class HistoryItem {
        public string Title { get; set; } public string Url { get; set; } public string Time { get; set; }
    }
    public class Preferences {
        public int Version { get; set; } public bool GlobalHotkey { get; set; }
        public WindowStateData LastWindow { get; set; }
        public Preferences() { Version = 1; GlobalHotkey = true; LastWindow = new WindowStateData(); }
    }
    public class Store {
        public readonly string Root, Personal, Browsing;
        public readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024 };
        public List<Favorite> Favorites; public List<HistoryItem> History; public Preferences Prefs;
        public readonly List<string> Warnings = new List<string>();
        public Store(string root) {
            Root = root; Personal = Path.Combine(root, "个人数据"); Browsing = Path.Combine(root, "浏览数据");
            Directory.CreateDirectory(Personal); Directory.CreateDirectory(Browsing);
            Favorites = Read<List<Favorite>>(Path.Combine(Personal, "收藏.json"), new List<Favorite>());
            Favorites = Favorites.Where(f => f != null && !String.IsNullOrEmpty(f.Id)).ToList();
            History = Read<List<HistoryItem>>(Path.Combine(Browsing, "历史.json"), new List<HistoryItem>());
            Prefs = Read<Preferences>(Path.Combine(Personal, "设置.json"), new Preferences());
            if(Prefs.LastWindow == null) Prefs.LastWindow = new WindowStateData();
            if(!File.Exists(Path.Combine(Personal, "收藏.json"))) SaveFavorites();
            if(!File.Exists(Path.Combine(Personal, "设置.json"))) SavePrefs();
        }
        T Read<T>(string path, T fallback) where T : class {
            if(!File.Exists(path)) return fallback;
            try { return Json.Deserialize<T>(File.ReadAllText(path, Encoding.UTF8)) ?? fallback; }
            catch(Exception ex) {
                string backup = path + ".damaged-" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
                File.Copy(path, backup, false); Warnings.Add("数据文件无法读取，已保留原文件：" + backup); Log(ex.ToString()); return fallback;
            }
        }
        public void Write(string path, object value) {
            string temp = path + ".tmp";
            File.WriteAllText(temp, Json.Serialize(value), new UTF8Encoding(false));
            if(File.Exists(path)) File.Replace(temp, path, path + ".bak"); else File.Move(temp, path);
        }
        public void SaveFavorites() { Write(Path.Combine(Personal, "收藏.json"), Favorites); }
        public void SavePrefs() { Write(Path.Combine(Personal, "设置.json"), Prefs); }
        public void SaveHistory() { Write(Path.Combine(Browsing, "历史.json"), History); }
        public void Log(string text) { try { File.AppendAllText(Path.Combine(Browsing, "运行.log"), DateTime.Now.ToString("s") + " " + text + Environment.NewLine); } catch { } }
        public static string Normalize(string text, bool allowSearch) {
            text = (text ?? "").Trim(); if(text.Length == 0) throw new ArgumentException("请输入网址。");
            if(text.Length > 8192) throw new ArgumentException("网址太长。");
            if(!text.Contains("://")) {
                if(text.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) || System.Text.RegularExpressions.Regex.IsMatch(text, @"^127\.[0-9.]+(:\d+)?(/|$)")) text = "http://" + text;
                else if(!text.Any(Char.IsWhiteSpace) && (text.Contains(".") || text.Contains(":"))) text = "https://" + text;
                else if(allowSearch) text = "https://www.google.com/search?q=" + Uri.EscapeDataString(text);
                else throw new ArgumentException("请输入完整网站地址，例如 https://example.com。");
            }
            Uri uri;
            if(!Uri.TryCreate(text, UriKind.Absolute, out uri) || (uri.Scheme != "http" && uri.Scheme != "https") || String.IsNullOrEmpty(uri.Host)) throw new ArgumentException("仅支持 http 和 https 网站地址。");
            if(!String.IsNullOrEmpty(uri.UserInfo)) throw new ArgumentException("网址中不能包含账号密码。");
            return uri.AbsoluteUri;
        }
        public Favorite Upsert(string id, string title, string url, string group) {
            string validUrl = Normalize(url, false); title = (title ?? "").Trim(); group = (group ?? "").Trim();
            if(title.Length == 0) title = new Uri(validUrl).Host;
            if(title.Length > 160 || group.Length > 60) throw new ArgumentException("名称最多 160 字，分组最多 60 字。");
            Favorite favorite = Favorites.Find(f => f.Id == id);
            if(favorite == null) { favorite = new Favorite { Id = Guid.NewGuid().ToString("N"), CreatedAt = DateTime.Now.ToString("s") }; Favorites.Add(favorite); }
            if(favorite.Url != validUrl) favorite.Window = null;
            favorite.Title = title; favorite.Url = validUrl; favorite.Group = group; SaveFavorites(); return favorite;
        }
        public void Remove(string id) { Favorites.RemoveAll(f => f.Id == id); SaveFavorites(); }
        public void Reorder(IEnumerable<string> ids) {
            var order = ids.Distinct().ToList(); var map = Favorites.ToDictionary(f => f.Id);
            Favorites = order.Where(map.ContainsKey).Select(id => map[id]).Concat(Favorites.Where(f => !order.Contains(f.Id))).ToList(); SaveFavorites();
        }
        public void Visit(string title, string url) {
            if(History.Count > 0 && History[0].Url == url) History.RemoveAt(0);
            History.Insert(0, new HistoryItem { Title = title, Url = url, Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });
            if(History.Count > 500) History.RemoveRange(500, History.Count - 500); SaveHistory();
        }
    }
}
