// Finds every place the running app draws a given icon web resource, and measures the chrome
// around it. Paste into the browser console on a model-driven app page, then call:
//
//     iconProbe('icons/account.svg')
//
// The argument is any distinctive part of the web resource name. Results are printed as a
// table and copied to the clipboard as JSON.
window.iconProbe = function (needle) {
  needle = (needle || '.svg').toLowerCase();
  var rows = [];
  var seen = {};

  // An icon can arrive as an <img>, an <svg><use>, an <object>, a CSS background, or a mask.
  // All five are in use somewhere in Unified Interface, so all five are looked at.
  function urlsOf(el) {
    var out = [];
    if (el.tagName === 'IMG' && el.src) out.push(el.src);
    if (el.tagName === 'OBJECT' && el.data) out.push(el.data);
    if (el.tagName && el.tagName.toLowerCase() === 'use') {
      var h = el.getAttribute('href') || el.getAttribute('xlink:href');
      if (h) out.push(h);
    }
    var cs = getComputedStyle(el);
    if (cs.backgroundImage && cs.backgroundImage !== 'none') out.push(cs.backgroundImage);
    if (cs.webkitMaskImage && cs.webkitMaskImage !== 'none') out.push(cs.webkitMaskImage);
    for (var i = 0; i < el.attributes.length; i++) {
      var v = el.attributes[i].value;
      if (v && v.toLowerCase().indexOf('.svg') !== -1) out.push(v);
    }
    return out;
  }

  // Whatever the markup calls this place. Not pretty, but it is the app's own word for the
  // context, which beats a name we invented.
  function label(el) {
    var parts = [], node = el;
    for (var i = 0; node && i < 12; i++, node = node.parentElement) {
      if (!node.getAttribute) continue;
      var bits = [node.getAttribute('data-id'), node.getAttribute('aria-label'), node.getAttribute('role')]
        .filter(function (b) { return b; });
      if (bits.length) parts.push(bits.join(' '));
    }
    return parts.slice(0, 4).join('  <  ');
  }

  // The label beside the icon: the nearest leaf element with text that is not inside the icon.
  function nearestText(el) {
    var node = el.parentElement;
    for (var i = 0; node && i < 5; i++, node = node.parentElement) {
      var kids = node.querySelectorAll('*');
      for (var j = 0; j < kids.length; j++) {
        var c = kids[j];
        if (c === el || c.contains(el) || c.children.length) continue;
        if ((c.textContent || '').trim()) return c;
      }
    }
    return null;
  }

  // A transparent background tells us nothing; keep walking up until something paints.
  function opaqueBg(el) {
    for (var node = el; node; node = node.parentElement) {
      var c = getComputedStyle(node).backgroundColor;
      if (c && c !== 'transparent' && !/rgba\(0, 0, 0, 0\)/.test(c)) return c;
    }
    return 'unknown';
  }

  function round(n) { return Math.round(n * 10) / 10; }

  function scan(root, where) {
    var all = root.querySelectorAll('*');
    for (var i = 0; i < all.length; i++) {
      var el = all[i], urls;
      try { urls = urlsOf(el); } catch (e) { continue; }
      var hit = false;
      for (var u = 0; u < urls.length; u++) {
        if (String(urls[u]).toLowerCase().indexOf(needle) !== -1) { hit = true; break; }
      }
      if (!hit) continue;

      var r = el.getBoundingClientRect();
      if (!r.width || !r.height) continue;

      // Two elements at the same spot and size are one icon reported twice (an img inside a
      // sized wrapper). Keep the first.
      var key = where + '|' + Math.round(r.x) + ',' + Math.round(r.y) + ',' + Math.round(r.width);
      if (seen[key]) continue;
      seen[key] = 1;

      var row = null;
      try {
        row = el.closest('[role="row"],[role="treeitem"],[role="menuitem"],[role="option"],[role="tab"],li,tr');
      } catch (e) {}
      row = row || el.parentElement;
      var rr = row ? row.getBoundingClientRect() : null;

      var t = nearestText(el);
      var ts = t ? getComputedStyle(t) : null;
      var tr = t ? t.getBoundingClientRect() : null;

      rows.push({
        where: where,
        context: label(el),
        tag: el.tagName.toLowerCase(),
        iconW: round(r.width),
        iconH: round(r.height),
        rowH: rr ? round(rr.height) : null,
        padLeft: rr ? round(r.left - rr.left) : null,
        gapToText: tr ? round(tr.left - r.right) : null,
        text: t ? (t.textContent || '').trim().slice(0, 24) : null,
        font: ts ? ts.fontFamily.split(',')[0].replace(/["']/g, '') : null,
        fontPx: ts ? ts.fontSize : null,
        weight: ts ? ts.fontWeight : null,
        textColor: ts ? ts.color : null,
        bg: opaqueBg(el)
      });
    }
  }

  scan(document, 'top');
  var frames = document.querySelectorAll('iframe');
  for (var f = 0; f < frames.length; f++) {
    try { scan(frames[f].contentDocument, 'iframe#' + f); } catch (e) { /* cross-origin */ }
  }

  var out = {
    url: location.href,
    when: new Date().toISOString(),
    devicePixelRatio: window.devicePixelRatio,
    needle: needle,
    rows: rows
  };

  console.log('devicePixelRatio', window.devicePixelRatio, '- browser zoom should be 100% (Ctrl+0)');
  console.table(rows);
  try { copy(JSON.stringify(out, null, 2)); console.log('Copied ' + rows.length + ' row(s) to the clipboard.'); }
  catch (e) { console.log('Clipboard unavailable; copy the object below.'); console.log(JSON.stringify(out, null, 2)); }
  return out;
};
console.log("iconProbe ready. Call it with part of your web resource name, e.g. iconProbe('icons/account.svg')");
