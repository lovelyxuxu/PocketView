/* Home is a local, privileged view. All text from files is rendered with textContent. */
const $ = id => document.getElementById(id);
const bridge = window.chrome && window.chrome.webview;
const send = (action, values={}) => bridge && bridge.postMessage({action,...values});
let state={favorites:[],history:[],settings:{}}, activeView='favorites', activeGroup='', editingId='', pendingSave=false, draggedId='', toastTimer, confirmation;
function el(tag, cls, text){const n=document.createElement(tag);if(cls)n.className=cls;if(text!==undefined)n.textContent=text;return n;}
function icon(path,cls=''){const n=document.createElementNS('http://www.w3.org/2000/svg','svg');n.setAttribute('viewBox','0 0 24 24');n.setAttribute('aria-hidden','true');if(cls)n.setAttribute('class',cls);const p=document.createElementNS(n.namespaceURI,'path');p.setAttribute('d',path);n.append(p);return n;}
function toast(text){$('toast').textContent=text;$('toast').hidden=false;clearTimeout(toastTimer);toastTimer=setTimeout(()=>{$('toast').hidden=true},2600);}
function host(url){try{return new URL(url).host}catch{return url}}
function confirmAction(title,text,button,fn){$('confirmTitle').textContent=title;$('confirmText').textContent=text;$('confirmOk').textContent=button;confirmation=fn;$('confirmDialog').showModal();}
function edit(f){editingId=f?f.Id:'';$('editorTitle').textContent=f?'编辑收藏':'添加收藏';$('nameInput').value=f?f.Title:'';$('urlInput').value=f?f.Url:'';$('groupInput').value=f?f.Group||'':'';$('formError').hidden=true;$('editor').showModal();setTimeout(()=>$(f?'nameInput':'urlInput').focus(),50);}
function setView(view,group=''){activeView=view;activeGroup=group;$('search').value='';render();}
function render(){
 $('total').textContent=state.favorites.length;
 const groups=[...new Set(state.favorites.map(f=>f.Group||'').filter(Boolean))];
 $('groups').replaceChildren();$('groupOptions').replaceChildren();
 document.querySelector('.collection-caption').hidden=!groups.length;
 groups.forEach(g=>{const b=el('button','nav'+(activeView==='favorites'&&activeGroup===g?' active':''));b.append(el('i','group-dot'),document.createTextNode(g));b.onclick=()=>setView('favorites',g);$('groups').append(b);const o=el('option');o.value=g;$('groupOptions').append(o)});
 document.querySelectorAll('[data-view]').forEach(b=>b.classList.toggle('active',b.dataset.view===activeView&&!activeGroup));
 $('favoritesView').hidden=activeView!=='favorites';$('historyView').hidden=activeView!=='history';$('settingsView').hidden=activeView!=='settings';$('add').hidden=activeView!=='favorites';
 $('heading').textContent=activeView==='history'?'最近访问':activeView==='settings'?'设置':'收藏';
 $('warning').hidden=!(state.warnings&&state.warnings.length);$('warning').textContent=(state.warnings||[]).join('\n');
 $('hotkey').checked=!!state.settings.GlobalHotkey;
 $('hotkeyStatus').textContent=!state.settings.GlobalHotkey?'已关闭':state.hotkey?'Ctrl+Alt+Space 唤出窗口（运行时有效）':'Ctrl+Alt+Space 已被其他程序占用';
 if(activeView==='favorites')renderCards();if(activeView==='history')renderHistory();
}
function renderCards(){
 const query=$('search').value.trim().toLowerCase();
 const favorites=state.favorites.filter(f=>(!activeGroup||f.Group===activeGroup)&&(!query||[f.Title,f.Url,f.Group||''].join(' ').toLowerCase().includes(query)));
 $('collectionName').textContent=activeGroup||'全部收藏';$('count').textContent=favorites.length+' 项';$('cards').replaceChildren();
 $('empty').hidden=state.favorites.length!==0;$('noResults').hidden=!(state.favorites.length&&!favorites.length);
 favorites.forEach(f=>{
  const card=el('article','card');card.dataset.id=f.Id;card.draggable=true;
  const open=el('button','card-open');open.title='打开 '+f.Title;open.onclick=()=>send('open',{id:f.Id});
  const top=el('div','card-top'),badge=el('div','badge',(f.Title||host(f.Url)).slice(0,1).toUpperCase());top.append(badge,icon('M7 17 17 7M7 7h10v10','card-arrow'));
  const title=el('h3','',f.Title);title.title=f.Title;const url=el('div','card-url',host(f.Url));url.title=f.Url;open.append(top,title,url);
  const bottom=el('div','card-bottom'),actions=el('div','card-actions');
  const modify=el('button','icon-button');modify.title='编辑收藏';modify.setAttribute('aria-label','编辑收藏');modify.append(icon('m15 4 5 5M4 20l5-1L20 8a2 2 0 0 0-5-5L4 14Z'));modify.onclick=()=>edit(f);
  const remove=el('button','icon-button');remove.title='删除收藏';remove.setAttribute('aria-label','删除收藏');remove.append(icon('M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13M10 10v7M14 10v7'));remove.onclick=()=>confirmAction('删除收藏？',f.Title,'删除',()=>send('remove',{id:f.Id}));
  actions.append(modify,remove);bottom.append(el('span','tag',f.Group||'未分组'),actions);card.append(open,bottom);
  card.addEventListener('dragstart',e=>{draggedId=f.Id;card.classList.add('dragging');e.dataTransfer.setData('text/plain',f.Id);e.dataTransfer.effectAllowed='move'});
  card.addEventListener('dragend',()=>{draggedId='';document.querySelectorAll('.card').forEach(c=>c.classList.remove('dragging','drop-target'))});
  card.addEventListener('dragover',e=>{if(draggedId&&draggedId!==f.Id){e.preventDefault();card.classList.add('drop-target')}});
  card.addEventListener('dragleave',()=>card.classList.remove('drop-target'));
  card.addEventListener('drop',e=>{e.preventDefault();card.classList.remove('drop-target');if(!draggedId||draggedId===f.Id)return;const ids=state.favorites.map(x=>x.Id).filter(id=>id!==draggedId);ids.splice(ids.indexOf(f.Id),0,draggedId);send('reorder',{ids})});
  $('cards').append(card);
 });
 if(state.favorites.length&&(!query)){const add=el('button','add-card');add.append(el('span','','＋'),document.createTextNode('添加收藏'));add.onclick=()=>{edit();$('groupInput').value=activeGroup};$('cards').append(add)}
}
function renderHistory(){
 $('historyList').replaceChildren();if(!state.history.length){$('historyList').append(el('div','empty','暂无访问记录'));return}
 state.history.forEach(h=>{const row=el('button','history-row');const detail=el('div','history-detail');detail.append(el('strong','',h.Title||host(h.Url)),el('small','',h.Url));row.append(detail,el('time','',h.Time));row.onclick=()=>send('navigate',{url:h.Url});$('historyList').append(row)});
}
document.querySelectorAll('[data-view]').forEach(b=>b.onclick=()=>setView(b.dataset.view));
$('search').addEventListener('input',renderCards);$('add').onclick=$('firstAdd').onclick=()=>edit();
const closeEditor=()=>{$('editor').close();pendingSave=false;$('saveEditor').disabled=false};$('cancelEditor').onclick=$('closeEditor').onclick=closeEditor;
$('editorForm').onsubmit=e=>{e.preventDefault();pendingSave=true;$('saveEditor').disabled=true;send('save',{id:editingId,title:$('nameInput').value,url:$('urlInput').value,group:$('groupInput').value})};
$('confirmCancel').onclick=()=>{$('confirmDialog').close();confirmation=null};$('confirmOk').onclick=()=>{$('confirmDialog').close();if(confirmation)confirmation();confirmation=null};
$('clearHistory').onclick=()=>confirmAction('清空历史？','将删除全部访问记录。','清空',()=>send('clearHistory'));
$('hotkey').onchange=()=>send('hotkey',{value:$('hotkey').checked});$('folder').onclick=()=>send('folder');$('export').onclick=()=>send('export');$('import').onclick=()=>send('import');
$('help').onclick=()=>send('help');
$('helpSettings').onclick=()=>send('help');
document.addEventListener('keydown',e=>{if(e.key==='/'&&!/INPUT|TEXTAREA/.test(document.activeElement.tagName)&&!$('editor').open&&!$('confirmDialog').open&&activeView==='favorites'){e.preventDefault();$('search').focus()}});
if(bridge){bridge.addEventListener('message',e=>{const m=e.data;if(m.type==='state'){state=m;if(pendingSave){closeEditor();toast('收藏已保存')}render();document.documentElement.dataset.ready='true'}else if(m.type==='error'){if(pendingSave){pendingSave=false;$('saveEditor').disabled=false;$('formError').textContent=m.message;$('formError').hidden=false}else toast(m.message)}});send('ready')}else{$('warning').hidden=false;$('warning').textContent='请通过 PocketView.exe 打开，收藏功能需要本地程序。';render()}
