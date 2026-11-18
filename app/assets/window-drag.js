/* Hold Alt to make the page a native title-bar region. No pointer or data bridge. */
(() => {
  let root, previous, priority, active = false;
  const release = () => {
    if (!active) return;
    if (previous) root.style.setProperty('-webkit-app-region', previous, priority);
    else root.style.removeProperty('-webkit-app-region');
    active = false;
  };
  window.addEventListener('keydown', event => {
    if (event.key !== 'Alt') { release(); return; }
    if (active || event.ctrlKey || event.metaKey) return;
    root = document.documentElement;
    if (!root) return;
    previous = root.style.getPropertyValue('-webkit-app-region');
    priority = root.style.getPropertyPriority('-webkit-app-region');
    root.style.setProperty('-webkit-app-region', 'drag', 'important');
    active = true;
  }, true);
  window.addEventListener('keyup', release, true);
  window.addEventListener('blur', release, true);
  window.addEventListener('pagehide', release, true);
  document.addEventListener('visibilitychange', () => { if (document.hidden) release(); });
})();
