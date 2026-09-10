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

  // An icon can arrive as an <img>, an <svg><use>, an <object>, a CSS background or a mask -
  // and, where the app colours it by theme, as an <svg> inlined into the page with no URL at
  // all. The inline case is handled separately in scan(); these are the ones with a URL.
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

      // An SVG the app inlined so it can colour it has no URL to match. Its path data is
      // still distinctive, so a needle taken from d="..." finds it. Measure the <svg>, not
      // the <path>: the path box is the ink, and we want the slot.
      var box = el;
      if (!hit && (el.tagName.toLowerCase() === "svg" || el.tagName.toLowerCase() === "path")) {
        var markup = (el.outerHTML || "").toLowerCase();
        if (markup.indexOf(needle) !== -1) {
          hit = true;
          if (el.tagName.toLowerCase() === "path") box = el.closest("svg") || el;
        }
      }
      if (!hit) continue;

      var r = box.getBoundingClientRect();
      if (!r.width || !r.height) continue;

      // Two elements at the same spot and size are one icon reported twice (an img inside a
      // sized wrapper). Keep the first.
      var key = where + '|' + Math.round(r.x) + ',' + Math.round(r.y) + ',' + Math.round(r.width);
      if (seen[key]) continue;
      seen[key] = 1;

      var row = null;
      try {
        row = box.closest('[role="row"],[role="treeitem"],[role="menuitem"],[role="option"],[role="tab"],li,tr');
      } catch (e) {}
      row = row || box.parentElement;
      var rr = row ? row.getBoundingClientRect() : null;

      var t = nearestText(box);
      var ts = t ? getComputedStyle(t) : null;
      var tr = t ? t.getBoundingClientRect() : null;

      rows.push({
        where: where,
        context: label(el),
        tag: box.tagName.toLowerCase(),
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
        bg: opaqueBg(box)
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
// ---------------------------------------------------------------------------
// navChrome() - the numbers iconProbe() cannot reach.
//
// iconProbe answers "how big is the icon and what is around it" for every place an icon is
// drawn. The navigation mock needs more than that: the width of the pane itself, the height
// and weight of a group header, what selection paints, the hairline between nav and page.
// None of those hang off an icon, so they are measured from the nav pane directly.
//
// Call it with the site map expanded, then again collapsed to the rail:
//
//     navChrome()
//
// It returns the object and prints it. Save both runs into sitemap.json.
window.navChrome = function () {
  function rect(el) { var r = el.getBoundingClientRect(); return { x: r.x, y: r.y, w: r.width, h: r.height }; }
  function css(el, k) { return getComputedStyle(el)[k]; }
  function painted(el) {
    // The row itself is usually transparent and something inside it does the painting.
    var out = [];
    var all = [el].concat([].slice.call(el.querySelectorAll('*')));
    for (var i = 0; i < all.length; i++) {
      var c = getComputedStyle(all[i]).backgroundColor;
      if (c && c !== 'transparent' && !/rgba\(0, 0, 0, 0\)/.test(c)) {
        var r = all[i].getBoundingClientRect();
        if (r.width) out.push({ bg: c, x: r.x, y: r.y, w: r.width, h: r.height });
      }
    }
    return out;
  }
  function leafText(el) {
    var kids = el.querySelectorAll('*');
    for (var i = 0; i < kids.length; i++) {
      var c = kids[i];
      if (c.children.length || !(c.textContent || '').trim()) continue;
      // An inlined icon carries its own <style> block, whose CSS text is a text node too.
      if (c.closest('svg')) continue;
      if (!c.getBoundingClientRect().width) continue;
      return c;
    }
    return null;
  }

  var nav = document.querySelector('[data-id="navbar-container"]');
  if (!nav) {
    var cands = [].slice.call(document.querySelectorAll('[role="navigation"],nav'));
    nav = cands.filter(function (e) { var r = e.getBoundingClientRect(); return r.x < 10 && r.height > 300; })[0];
  }
  if (!nav) { console.log('No navigation pane found.'); return null; }

  var navBox = rect(nav);
  var items = [].slice.call(nav.querySelectorAll('[role="treeitem"]'));
  var leaves = [], headers = [];
  items.forEach(function (it) {
    var group = it.querySelector('[role="group"]');
    var label = leafText(it);
    var svg = it.querySelector('svg, img');
    var row = { text: label ? (label.textContent || '').trim().slice(0, 24) : null };
    if (label) {
      row.font = css(label, 'fontFamily').split(',')[0].replace(/["']/g, '');
      row.fontPx = css(label, 'fontSize');
      row.weight = css(label, 'fontWeight');
      row.color = css(label, 'color');
      row.textX = rect(label).x;
    }
    if (group) {
      // A header treeitem wraps its whole group, so its own height is the group's. The
      // header line is the label's own box.
      row.headerH = label ? rect(label).h : null;
      headers.push(row);
      return;
    }
    var b = rect(it);
    row.rowH = b.h;
    row.rowW = b.w;
    row.rowX = b.x;
    if (svg) {
      var s = rect(svg);
      row.iconW = s.w; row.iconH = s.h;
      row.padLeft = s.x - b.x;
      row.gapToText = row.textX != null ? row.textX - (s.x + s.w) : null;
      row.iconTag = svg.tagName.toLowerCase();
    }
    row.selected = it.getAttribute('aria-selected') === 'true';
    if (row.selected) row.paints = painted(it);
    leaves.push(row);
  });

  // Whatever sits to the right of the nav is the page the icon is seen against.
  var page = [].slice.call(document.querySelectorAll('div')).filter(function (e) {
    var r = e.getBoundingClientRect();
    return r.x >= navBox.w - 2 && r.x <= navBox.w + 30 && r.width > 400 && r.height > 300 &&
      !/rgba\(0, 0, 0, 0\)/.test(getComputedStyle(e).backgroundColor);
  })[0];

  var out = {
    url: location.href,
    when: new Date().toISOString(),
    devicePixelRatio: window.devicePixelRatio,
    innerWidth: window.innerWidth,
    state: navBox.w < 120 ? 'collapsed (rail)' : 'expanded',
    navWidth: navBox.w,
    navBg: css(nav, 'backgroundColor'),
    navBorderRight: [css(nav, 'borderRightWidth'), css(nav, 'borderRightColor')],
    pageBg: page ? getComputedStyle(page).backgroundColor : null,
    headers: headers,
    rows: leaves
  };
  console.log(JSON.stringify(out, null, 2));
  return out;
};
console.log("navChrome ready. Call navChrome() with the site map expanded, then collapsed.");
