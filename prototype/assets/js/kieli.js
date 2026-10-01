/* Киелі · prototype interactions (vanilla JS, no dependencies —
   external CDNs are unreliable for visitors inside China). */
(function () {
  "use strict";

  var $ = function (s, r) { return (r || document).querySelector(s); };
  var $$ = function (s, r) { return Array.prototype.slice.call((r || document).querySelectorAll(s)); };
  var store = {
    get: function (k) { try { return localStorage.getItem(k); } catch (e) { return null; } },
    set: function (k, v) { try { localStorage.setItem(k, v); } catch (e) { /* private mode */ } }
  };
  var D = window.KIELI || { places: [], regions: [], cats: {}, heritage: [], img: function () { return ""; } };
  /* build stamp from this script's own URL, reused for lazily loaded assets */
  var VER = (function () { var c = document.currentScript; var m = c && c.src.match(/[?&]v=([^&]+)/); return m ? m[1] : ""; })();
  var page = document.body.getAttribute("data-page") || "";

  function esc(s) { return String(s == null ? "" : s).replace(/[&<>"]/g, function (c) { return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]; }); }
  function regionById(id) { for (var i = 0; i < D.regions.length; i++) if (D.regions[i].id === id) return D.regions[i]; return null; }
  function placeById(id) { for (var i = 0; i < D.places.length; i++) if (D.places[i].id === id) return D.places[i]; return null; }

  /* ---------- Төте жазу: Cyrillic → Arabic-script Kazakh ---------------- */
  var Script = (function () {
    var MAP = {
      "а": "ا", "ә": "ا", "б": "ب", "в": "ۆ", "г": "گ", "ғ": "ع", "д": "د", "е": "ە", "ё": "يو",
      "ж": "ج", "з": "ز", "и": "ي", "й": "ي", "к": "ك", "қ": "ق", "л": "ل", "м": "م", "н": "ن",
      "ң": "ڭ", "о": "و", "ө": "و", "п": "پ", "р": "ر", "с": "س", "т": "ت", "у": "ۋ", "ұ": "ۇ",
      "ү": "ۇ", "ф": "ف", "х": "ح", "һ": "ھ", "ц": "تس", "ч": "چ", "ш": "ش", "щ": "شش", "ъ": "",
      "ы": "ى", "і": "ى", "ь": "", "э": "ە", "ю": "يۋ", "я": "يا"
    };
    var WORD = /[А-Яа-яЁёӘәҒғҚқҢңӨөҰұҮүҺһІі]+/g;
    var HAS_CYR = /[А-Яа-яЁёӘәҒғҚқҢңӨөҰұҮүҺһІі]/;
    var ATTRS = ["placeholder", "aria-label", "title", "alt"];
    var SKIP = "script,style,noscript,textarea,code,.ltr,[translate='no'],[data-no-tote]";
    var orig = new WeakMap(), conv = new WeakMap(), attrOrig = new WeakMap();
    var mode = "cyr", observer = null, titleOrig = document.title, fontsLoaded = false;

    function word(w) {
      var lw = w.toLowerCase();
      /* hamza ٴ marks a front-vowel word unless е, к, г already signal it */
      var soft = /[әөүі]/.test(lw) && !/[екгэ]/.test(lw);
      var out = "";
      for (var i = 0; i < lw.length; i++) { var c = lw.charAt(i); out += MAP.hasOwnProperty(c) ? MAP[c] : c; }
      return (soft ? "ٴ" : "") + out;
    }
    function text(t) {
      return t
        .replace(WORD, word)
        .replace(/,/g, function (m, i, s) { return /\d/.test(s.charAt(i - 1)) && /\d/.test(s.charAt(i + 1)) ? "," : "،"; })
        .replace(/\?/g, "؟").replace(/;/g, "؛")
        .replace(/(\d)[  ](?=\d)/g, "$1 "); /* keeps "7 010" as one number in RTL */
    }
    function skipped(el) { return !el || !!el.closest(SKIP); }
    function convertNode(n) {
      if (n.nodeType !== 3 || skipped(n.parentElement)) return;
      var v = n.nodeValue;
      if (conv.get(n) === v || !HAS_CYR.test(v)) return;
      orig.set(n, v);
      var t = text(v); conv.set(n, t); n.nodeValue = t;
    }
    function convertAttrs(el) {
      if (el.nodeType !== 1 || skipped(el)) return;
      ATTRS.forEach(function (a) {
        var v = el.getAttribute(a);
        if (!v || !HAS_CYR.test(v)) return;
        var saved = attrOrig.get(el) || {};
        if (saved[a] && saved[a].t === v) return;
        saved[a] = { o: v, t: text(v) }; attrOrig.set(el, saved);
        el.setAttribute(a, saved[a].t);
      });
    }
    function walk(root, fnText, fnEl) {
      if (root.nodeType === 3) { fnText(root); return; }
      if (root.nodeType !== 1) return;
      fnEl(root);
      var tw = document.createTreeWalker(root, NodeFilter.SHOW_ELEMENT | NodeFilter.SHOW_TEXT, null);
      var n; while ((n = tw.nextNode())) { if (n.nodeType === 3) fnText(n); else fnEl(n); }
    }
    function restoreText(n) {
      if (orig.has(n)) { if (conv.get(n) === n.nodeValue) n.nodeValue = orig.get(n); orig.delete(n); conv.delete(n); }
    }
    function restoreAttrs(el) {
      if (el.nodeType !== 1) return;
      var saved = attrOrig.get(el); if (!saved) return;
      Object.keys(saved).forEach(function (a) { if (el.getAttribute(a) === saved[a].t) el.setAttribute(a, saved[a].o); });
      attrOrig.delete(el);
    }
    function loadFonts() {
      /* ALKATIP Tor, embedded as base64 — works offline, from disk and on mobile */
      if (fontsLoaded || document.querySelector("link[data-tote-font]")) { fontsLoaded = true; return; }
      fontsLoaded = true;
      var l = document.createElement("link");
      l.rel = "stylesheet";
      l.href = "assets/css/tote-font.css" + (VER ? "?v=" + VER : "");
      l.setAttribute("data-tote-font", "");
      document.head.appendChild(l);
    }
    function set(m, persist) {
      if (m === mode) return sync();
      mode = m;
      var html = document.documentElement;
      if (m === "tote") {
        loadFonts();
        html.setAttribute("data-script", "tote"); html.lang = "kk-Arab"; html.dir = "rtl";
        titleOrig = document.title; document.title = text(titleOrig);
        walk(document.body, convertNode, convertAttrs);
        observer = observer || new MutationObserver(function (list) {
          if (mode !== "tote") return;
          list.forEach(function (r) {
            if (r.type === "characterData") convertNode(r.target);
            else if (r.type === "attributes") convertAttrs(r.target);
            else r.addedNodes.forEach(function (n) { walk(n, convertNode, convertAttrs); });
          });
        });
        observer.observe(document.body, { subtree: true, childList: true, characterData: true, attributes: true, attributeFilter: ATTRS });
      } else {
        if (observer) observer.disconnect();
        html.removeAttribute("data-script"); html.lang = "kk"; html.dir = "ltr";
        document.title = titleOrig;
        walk(document.body, restoreText, restoreAttrs);
      }
      if (persist) store.set("kieli-script", m);
      sync();
    }
    function sync() {
      $$("[data-script-btn]").forEach(function (b) { b.setAttribute("aria-pressed", String(b.getAttribute("data-script-btn") === mode)); });
    }
    return { set: set, mode: function () { return mode; }, text: text };
  })();
  window.KieliScript = Script;

  $$("[data-script-btn]").forEach(function (b) {
    b.addEventListener("click", function () { Script.set(b.getAttribute("data-script-btn"), true); });
  });

  /* ---------- Day / night ---------------------------------------------- */
  var Theme = (function () {
    var html = document.documentElement;
    function current() { return html.getAttribute("data-theme") === "dark" ? "dark" : "light"; }
    function sync() {
      var dark = current() === "dark";
      var label = dark ? "Күндізгі режимге ауысу" : "Түнгі режимге ауысу";
      $$("[data-theme-toggle]").forEach(function (b) {
        b.setAttribute("aria-pressed", String(dark));
        b.setAttribute("aria-label", label);
        b.setAttribute("title", label);
        var t = $(".theme-toggle__t", b); if (t) t.textContent = dark ? "Күндізгі режим" : "Түнгі режим";
      });
      var meta = $('meta[name="theme-color"]'); if (meta) meta.setAttribute("content", dark ? "#0A1316" : "#F4F6F5");
    }
    function set(t) {
      html.setAttribute("data-theme", t === "dark" ? "dark" : "light");
      store.set("kieli-theme", t === "dark" ? "dark" : "light");
      sync();
    }
    $$("[data-theme-toggle]").forEach(function (b) {
      b.addEventListener("click", function () { set(current() === "dark" ? "light" : "dark"); });
    });
    sync();
    return { set: set, get: current };
  })();
  window.KieliTheme = Theme;

  /* ---------- Prototype note ------------------------------------------ */
  var bar = $("[data-proto-bar]");
  if (bar) {
    if (store.get("kieli-proto-hidden") === "1") bar.hidden = true;
    var x = $("[data-proto-close]", bar);
    if (x) x.addEventListener("click", function () { bar.hidden = true; store.set("kieli-proto-hidden", "1"); });
  }

  /* ---------- Header ---------------------------------------------------- */
  var header = $("[data-header]");
  var fab = $(".fab");
  function onScroll() {
    var y = window.scrollY || 0;
    if (header) header.classList.toggle("is-scrolled", y > 24);
    if (fab) fab.classList.toggle("is-hidden", y < 360);
  }
  window.addEventListener("scroll", onScroll, { passive: true });
  onScroll();

  var here = (location.pathname.split("/").pop() || "index.html").replace(".html", "");
  var navMap = { kazakhstan: "kazakhstan", place: "kazakhstan", services: "services", service: "services", author: "author", article: "articles", articles: "articles", kaztest: "kaztest" };
  $$("[data-nav]").forEach(function (a) { if (a.getAttribute("data-nav") === navMap[here]) a.setAttribute("aria-current", "page"); });

  /* Mobile nav */
  var mnav = $("#mnav"), lastFocus = null;
  function openNav() { if (!mnav) return; lastFocus = document.activeElement; mnav.hidden = false; document.body.style.overflow = "hidden"; $$("[data-nav-toggle]").forEach(function (b) { b.setAttribute("aria-expanded", "true"); }); var f = $("a,button", mnav); if (f) f.focus(); }
  function closeNav() { if (!mnav || mnav.hidden) return; mnav.hidden = true; document.body.style.overflow = ""; $$("[data-nav-toggle]").forEach(function (b) { b.setAttribute("aria-expanded", "false"); }); if (lastFocus) lastFocus.focus(); }
  $$("[data-nav-toggle]").forEach(function (b) { b.addEventListener("click", function () { mnav && mnav.hidden ? openNav() : closeNav(); }); });
  $$("[data-nav-close]").forEach(function (b) { b.addEventListener("click", closeNav); });
  if (mnav) $$("a", mnav).forEach(function (a) { a.addEventListener("click", closeNav); });

  /* ---------- Consultation drawer -------------------------------------- */
  var drawer = $("#consult");
  var drawerLast = null;
  /* Every request is stored with its exact service: "<tab>:<item>" (e.g. translate:license → AUD-02) */
  var LEGACY = { translate: "translate:", docs: "notary:", citizen: "migration:", atazholy: "migration:atazholy", study: "education:", lang: "education:", legal: "business:", realty: "housing:" };
  function normService(v) { v = v || ""; return LEGACY.hasOwnProperty(v) ? LEGACY[v] : v; }
  function serviceInfo(v) {
    var B0 = window.BASTAU; if (!B0 || !v) return null;
    var f = B0.findService(v); if (!f.tab) return null;
    return f.item ? { code: f.item.code, title: f.item.title, group: f.tab.name } : { code: f.tab.code, title: f.tab.name + " — жалпы кеңес", group: f.tab.name };
  }
  function buildServiceSelect(sel) {
    var B0 = window.BASTAU; if (!B0 || sel.getAttribute("data-built")) return;
    sel.setAttribute("data-built", "1");
    var html = '<option value="">Әзірге білмеймін — кеңес керек</option>';
    B0.tabs.forEach(function (t) {
      html += '<optgroup label="' + esc(t.name) + '"><option value="' + t.id + ':">' + esc(t.name) + ' — жалпы кеңес</option>' +
        t.items.map(function (it) { return '<option value="' + t.id + ':' + it.id + '">' + esc(it.title) + '</option>'; }).join("") + '</optgroup>';
    });
    sel.innerHTML = html;
  }
  function paintPick(form) {
    var sel = $("select[data-service-select]", form);
    if (!sel) return;
    var info = serviceInfo(sel.value), code = $("input[name='code']", form), src = $("input[name='source']", form);
    if (code) code.value = info ? info.code : "";
    if (src) src.value = location.pathname.split("/").pop() || "index.html";
    var wa = $("a[data-wa]", form.parentElement);
    if (wa) wa.href = "https://wa.me/" + wa.getAttribute("data-wa") + (info ? "?text=" + encodeURIComponent("Сәлеметсіз бе! Өтінім: " + info.code + " — " + info.title) : "");
    var pick = $("[data-consult-pick]", form.parentElement);
    if (!pick) return;
    pick.hidden = !info;
    if (info) { $("[data-pick-code]", pick).textContent = info.code; $("[data-pick-title]", pick).textContent = info.title; $("[data-pick-group]", pick).textContent = info.group; }
  }
  $$("select[data-service-select]").forEach(function (sel) {
    buildServiceSelect(sel);
    sel.addEventListener("change", function () { paintPick(sel.form); });
  });
  /* A specific request also has its own link: page.html#order-<tab>-<item> (shareable in WeChat) */
  var hashBeforeDrawer = null;
  function setHash(h) { if (window.history && history.replaceState) history.replaceState(null, "", h || location.pathname + location.search); }
  function openConsult(service) {
    if (!drawer) return;
    closeNav();
    if (drawer.hidden) drawerLast = document.activeElement;
    drawer.hidden = false;
    document.body.style.overflow = "hidden";
    var sel = $("#c-service", drawer), v = normService(service);
    var doneBox = $("[data-form-done]", drawer);
    if (sel && doneBox && !doneBox.hidden) {
      /* a new request after a sent one: fresh form, keep who is writing */
      var keep = { name: sel.form.elements.name.value, contact: sel.form.elements.contact.value };
      sel.form.reset(); sel.form.elements.name.value = keep.name; sel.form.elements.contact.value = keep.contact;
      $$("[data-files]", sel.form).forEach(function (el) { el.dispatchEvent(new Event("change")); });
      sel.form.hidden = false; doneBox.hidden = true;
    }
    if (sel) {
      var hit = false;
      for (var i = 0; i < sel.options.length; i++) if (sel.options[i].value === v) { sel.selectedIndex = i; hit = true; }
      if (!hit) sel.selectedIndex = 0;
      paintPick(sel.form);
    }
    var m = v.match(/^([a-z]+):([a-z]+)$/);
    if (m) { if (hashBeforeDrawer === null) hashBeforeDrawer = location.hash; setHash("#order-" + m[1] + "-" + m[2]); }
    /* start at the top: the chosen request type and the messengers come first */
    var panelEl = $(".drawer__panel", drawer); if (panelEl) panelEl.scrollTop = 0;
    setTimeout(function () { var f = $("#c-name", drawer) || $("button", drawer); if (f) f.focus({ preventScroll: true }); }, 40);
  }
  function closeConsult() {
    if (!drawer || drawer.hidden) return;
    drawer.hidden = true; document.body.style.overflow = "";
    if (hashBeforeDrawer !== null) { restoreHash(hashBeforeDrawer); hashBeforeDrawer = null; }
    if (drawerLast) drawerLast.focus();
  }
  function restoreHash(h) {
    /* after closing, #order-… becomes the matching service card (#s-…) or nothing */
    h = (h || "").replace(/^#order-/, "#s-");
    if (/^#s-/.test(h) && !document.querySelector("[data-help]")) h = "";
    setHash(h);
  }
  function fromOrderHash() {
    var m = (location.hash || "").match(/^#order-([a-z]+)-([a-z]+)$/);
    if (!m || !window.BASTAU || !window.BASTAU.findService(m[1] + ":" + m[2]).item) return;
    if (hashBeforeDrawer === null) hashBeforeDrawer = location.hash;
    openConsult(m[1] + ":" + m[2]);
  }
  setTimeout(fromOrderHash, 0);
  window.addEventListener("hashchange", fromOrderHash);
  var drawerSel = drawer && $("#c-service", drawer);
  if (drawerSel) drawerSel.addEventListener("change", function () {
    /* the address always names the request type being filled in */
    if (drawer.hidden) return;
    var m = drawerSel.value.match(/^([a-z]+):([a-z]+)$/);
    if (m) { if (hashBeforeDrawer === null) hashBeforeDrawer = location.hash; setHash("#order-" + m[1] + "-" + m[2]); }
    else if (hashBeforeDrawer !== null) restoreHash(hashBeforeDrawer);
  });
  document.addEventListener("click", function (e) {
    var t = e.target.closest("[data-consult]");
    if (t) { e.preventDefault(); openConsult(t.getAttribute("data-consult")); return; }
    if (e.target.closest("[data-consult-close]")) closeConsult();
  });
  document.addEventListener("keydown", function (e) {
    if (e.key !== "Escape") return;
    closePano(); closeConsult(); closeNav();
  });
  if (drawer) drawer.addEventListener("keydown", function (e) {
    if (e.key !== "Tab") return;
    var f = $$("a[href],button:not([disabled]),input,select,textarea", drawer).filter(function (el) { return el.offsetParent !== null; });
    if (!f.length) return;
    if (e.shiftKey && document.activeElement === f[0]) { e.preventDefault(); f[f.length - 1].focus(); }
    else if (!e.shiftKey && document.activeElement === f[f.length - 1]) { e.preventDefault(); f[0].focus(); }
  });

  /* ---------- Attached documents (prototype: listed, not uploaded) ------- */
  function fmtSize(b) { return b > 1048576 ? (b / 1048576).toFixed(1) + " МБ" : Math.max(1, Math.round(b / 1024)) + " КБ"; }
  $$("input[type='file'][data-files]").forEach(function (inp) {
    var list = inp.parentElement.querySelector("[data-file-list]");
    function paint() {
      var files = Array.prototype.slice.call(inp.files || []);
      list.hidden = !files.length;
      list.innerHTML = files.map(function (f) { return '<li><span class="ltr">' + esc(f.name) + '</span><small>' + fmtSize(f.size) + '</small></li>'; }).join("") +
        (files.length ? '<li class="drop__clear"><button type="button" data-files-clear>Тазарту</button></li>' : "");
    }
    inp.addEventListener("change", paint);
    list.addEventListener("click", function (e) { if (e.target.closest("[data-files-clear]")) { inp.value = ""; paint(); } });
  });

  /* ---------- Forms (prototype: nothing is sent) ------------------------ */
  $$("form[data-lead-form]").forEach(function (form) {
    form.addEventListener("submit", function (e) {
      e.preventDefault();
      var ok = true;
      $$("[required]", form).forEach(function (el) {
        var bad = el.type === "checkbox" ? !el.checked : !String(el.value).trim();
        el.setAttribute("aria-invalid", bad ? "true" : "false");
        var err = el.closest(".field") && $(".field__err", el.closest(".field"));
        if (err) err.hidden = !bad;
        if (bad && ok) { el.focus(); ok = false; }
      });
      if (!ok) return;
      var done = form.parentElement.querySelector("[data-form-done]");
      var nameEl = $("input[name='name']", form);
      if (done) {
        var who = $("[data-done-name]", done);
        if (who && nameEl) who.textContent = nameEl.value.trim().split(" ")[0];
        var sum = $("[data-done-summary]", done);
        if (sum) {
          var sel = $("select[data-service-select]", form), info = sel ? serviceInfo(sel.value) : null;
          var files = $("input[type='file']", form), n = files && files.files ? files.files.length : 0;
          var country = $("input[name='country']:checked", form);
          sum.innerHTML =
            '<div><dt>Өтінім түрі</dt><dd>' + (info ? '<b class="mono">' + info.code + '</b> ' + esc(info.title) : "Жалпы кеңес") + '</dd></div>' +
            (country ? '<div><dt>Қай елде</dt><dd>' + esc(country.nextElementSibling.textContent) + '</dd></div>' : "") +
            (files ? '<div><dt>Құжаттар</dt><dd>' + (n ? n + " файл тіркелді" : "кейін жібересіз") + '</dd></div>' : "");
        }
        form.hidden = true; done.hidden = false;
        var h = $("h3", done); if (h) { h.setAttribute("tabindex", "-1"); h.focus(); }
      }
    });
  });
  $$("[data-form-reset]").forEach(function (b) {
    b.addEventListener("click", function () {
      var done = b.closest("[data-form-done]"); var form = done && done.parentElement.querySelector("form[data-lead-form]");
      if (form) {
        form.reset(); form.hidden = false; done.hidden = true;
        $$("[data-files], select[data-service-select]", form).forEach(function (el) { el.dispatchEvent(new Event("change")); });
        var f = $("input:not([type='hidden'])", form); if (f) f.focus();
      }
    });
  });

  /* ---------- Copy buttons ---------------------------------------------- */
  document.addEventListener("click", function (e) {
    var b = e.target.closest("[data-copy]");
    if (!b) return;
    var val = b.getAttribute("data-copy");
    var label = b.textContent;
    function done(ok) {
      b.classList.toggle("is-done", ok);
      b.textContent = ok ? "Көшірілді" : "Белгілеңіз";
      setTimeout(function () { b.classList.remove("is-done"); b.textContent = label; }, 1600);
    }
    try {
      navigator.clipboard.writeText(val).then(function () { done(true); }, function () { selectNear(b); done(false); });
    } catch (err) { selectNear(b); done(false); }
  });
  function selectNear(b) {
    var t = b.parentElement.querySelector("span"); if (!t) return;
    var r = document.createRange(); r.selectNodeContents(t);
    var s = window.getSelection(); s.removeAllRanges(); s.addRange(r);
  }

  /* ---------- Office clock (Almaty, UTC+5) ------------------------------ */
  function tickClock() {
    var els = $$("[data-clock]"); if (!els.length) return;
    var now = new Date(), hh, mm, dow;
    try {
      var parts = new Intl.DateTimeFormat("en-GB", { timeZone: "Asia/Almaty", hour: "2-digit", minute: "2-digit", weekday: "short", hour12: false }).formatToParts(now);
      parts.forEach(function (p) { if (p.type === "hour") hh = +p.value; if (p.type === "minute") mm = p.value; if (p.type === "weekday") dow = p.value; });
    } catch (e) {
      var u = new Date(now.getTime() + now.getTimezoneOffset() * 60000 + 5 * 3600000);
      hh = u.getHours(); mm = ("0" + u.getMinutes()).slice(-2); dow = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"][u.getDay()];
    }
    var open = (dow === "Sat" ? (hh >= 10 && hh < 15) : dow !== "Sun" && hh >= 10 && hh < 19);
    els.forEach(function (el) { el.textContent = ("0" + hh).slice(-2) + ":" + mm; });
    $$("[data-open-state]").forEach(function (el) { el.textContent = open ? "кеңсе ашық" : "кеңсе жабық — хабарлама қалдырыңыз"; });
    $$("[data-open-dot]").forEach(function (el) { el.classList.toggle("is-off", !open); });
  }
  tickClock(); setInterval(tickClock, 30000);

  /* ---------- 360° panorama modal -------------------------------------- */
  var pano = $("#pano");
  function openPano(url, title) {
    if (!pano || !url) return;
    $("[data-pano-title]", pano).textContent = title || "360° панорама";
    $("[data-pano-frame]", pano).innerHTML = '<iframe src="' + esc(url) + '" title="360° панорама" loading="lazy" allowfullscreen referrerpolicy="no-referrer-when-downgrade"></iframe>';
    pano.hidden = false; document.body.style.overflow = "hidden";
    var c = $("[data-pano-close]", pano); if (c) c.focus();
  }
  function closePano() {
    if (!pano || pano.hidden) return;
    pano.hidden = true; $("[data-pano-frame]", pano).innerHTML = ""; document.body.style.overflow = "";
  }
  document.addEventListener("click", function (e) {
    var t = e.target.closest("[data-pano]");
    if (t) { e.preventDefault(); openPano(t.getAttribute("data-pano"), t.getAttribute("data-pano-name")); }
    if (e.target.closest("[data-pano-close]")) closePano();
  });

  /* ---------- Shared renderers ------------------------------------------ */
  function coords(lat, lon) {
    if (lat == null) return "";
    function dm(v) { var a = Math.abs(v), d = Math.floor(a), m = Math.round((a - d) * 60); if (m === 60) { d++; m = 0; } return d + "°" + ("0" + m).slice(-2) + "′"; }
    return dm(lat) + " с.е. · " + dm(lon) + " ш.б.";
  }
  function placeCard(p) {
    var r = regionById(p.region);
    var badges = p.pano ? '<span class="tag tag--on-photo">360°</span>' : "";
    return '<a class="pcard" href="place.html#' + p.id + '">' +
      '<div class="pcard__img"><img src="' + D.img(p.img, "middle") + '" alt="' + esc(p.name) + '" loading="lazy" width="640" height="360">' +
      (badges ? '<div class="pcard__badges">' + badges + '</div>' : "") + '</div>' +
      '<div class="pcard__meta"><b>' + esc(D.cats[p.cat]) + '</b><span>' + esc(r ? r.name : "") + '</span></div>' +
      '<h3 class="pcard__name">' + esc(p.name) + '</h3>' +
      '<p class="pcard__fact">' + esc(p.fact) + '</p></a>';
  }

  /* ---------- Page: Kazakhstan (atlas + filters) ------------------------ */
  if (page === "kazakhstan") (function () {
    var grid = $("#place-grid"), status = $("#filter-status"), map = $("#kzmap"), tip = $("#map-tip");
    var state = { cat: "all", region: 0 };

    function render() {
      var list = D.places.filter(function (p) {
        return (state.cat === "all" || p.cat === state.cat) && (!state.region || p.region === state.region);
      });
      var r = regionById(state.region);
      if (list.length) {
        grid.innerHTML = list.map(placeCard).join("");
      } else {
        grid.innerHTML = '<div class="empty" style="grid-column:1/-1"><p><b>Прототипте бұл сүзгіге сай нысан жоқ.</b></p><p>Толық нұсқада ' +
          esc(r ? r.name : "таңдалған өңір") + (r ? " бойынша " + r.count + " нысан" : "") + ' деректер қорынан шығады.</p></div>';
      }
      var parts = [];
      if (r) parts.push(r.name);
      if (state.cat !== "all") parts.push(D.cats[state.cat]);
      status.innerHTML = (parts.length ? esc(parts.join(" · ")) + ": " : "") + '<span class="tnum">' + list.length + '</span> нысан' +
        (parts.length ? ' <button type="button" data-reset>Тазарту</button>' : "");
      $$("[data-cat]").forEach(function (c) { c.setAttribute("aria-pressed", String(c.getAttribute("data-cat") === state.cat)); });
      $$("[data-region-btn]").forEach(function (b) { b.setAttribute("aria-pressed", String(+b.getAttribute("data-region-btn") === state.region)); });
      if (map) $$(".rg", map).forEach(function (g) { g.classList.toggle("is-on", +g.getAttribute("data-region") === state.region); });
    }
    function counts() {
      $$("[data-cat]").forEach(function (c) {
        var k = c.getAttribute("data-cat"), n = D.places.filter(function (p) { return k === "all" || p.cat === k; }).length;
        var s = $(".n", c); if (s) s.textContent = n;
      });
    }
    function setRegion(id, scroll) {
      state.region = state.region === id ? 0 : id; render();
      if (scroll) { var t = $("#places"); if (t) t.scrollIntoView({ behavior: "smooth", block: "start" }); }
    }
    document.addEventListener("click", function (e) {
      var c = e.target.closest("[data-cat]"); if (c) { state.cat = c.getAttribute("data-cat"); render(); }
      var b = e.target.closest("[data-region-btn]"); if (b) setRegion(+b.getAttribute("data-region-btn"), false);
      if (e.target.closest("[data-reset]")) { state.cat = "all"; state.region = 0; render(); }
    });
    if (map) {
      /* choropleth: shade each oblast by how many places it holds */
      $$(".rg", map).forEach(function (g) {
        var r = regionById(+g.getAttribute("data-region"));
        if (!r || r.city) return;
        g.classList.add(r.count >= 16 ? "lv4" : r.count >= 11 ? "lv3" : r.count >= 6 ? "lv2" : "lv1");
      });
      var wrap = map.parentElement;
      function hot(id, on) {
        $$('.rg[data-region="' + id + '"]', map).forEach(function (g) { g.classList.toggle("is-hot", on); });
        $$('[data-region-btn="' + id + '"]').forEach(function (g) { g.classList.toggle("is-hot", on); });
      }
      function showTip(g, ev) {
        var id = +g.getAttribute("data-region"), r = regionById(id); if (!r || !tip) return;
        var box = wrap.getBoundingClientRect(), x, y;
        if (ev && ev.clientX) { x = ev.clientX - box.left; y = ev.clientY - box.top; }
        else { var b = g.getBoundingClientRect(); x = b.left + b.width / 2 - box.left; y = b.top + b.height / 2 - box.top; }
        tip.innerHTML = "<b>" + esc(r.name) + "</b> · " + r.count + " нысан";
        tip.style.left = x + "px"; tip.style.top = y + "px"; tip.hidden = false;
      }
      $$(".rg", map).forEach(function (g) {
        var id = +g.getAttribute("data-region");
        g.addEventListener("mousemove", function (ev) { showTip(g, ev); });
        g.addEventListener("mouseenter", function () { hot(id, true); });
        g.addEventListener("mouseleave", function () { hot(id, false); if (tip) tip.hidden = true; });
        g.addEventListener("focus", function () { hot(id, true); showTip(g); });
        g.addEventListener("blur", function () { hot(id, false); if (tip) tip.hidden = true; });
        g.addEventListener("click", function () { setRegion(id, true); });
        g.addEventListener("keydown", function (ev) { if (ev.key === "Enter" || ev.key === " ") { ev.preventDefault(); setRegion(id, true); } });
      });
      $$("[data-region-btn]").forEach(function (b) {
        var id = +b.getAttribute("data-region-btn");
        b.addEventListener("mouseenter", function () { hot(id, true); });
        b.addEventListener("mouseleave", function () { hot(id, false); });
      });
    }
    var hr = $("#heritage-grid");
    if (hr) hr.innerHTML = D.heritage.map(function (h) {
      return '<div class="hcard"><div class="hcard__img"><img src="' + D.img(h.img, "middle") + '" alt="' + esc(h.name) + '" loading="lazy"></div>' +
        '<h3 class="hcard__t">' + esc(h.name) + '</h3><p class="hcard__k"><span>' + esc(h.kind) + '</span>' + (h.year ? '<span class="mono">' + h.year + '</span>' : "") + '</p></div>';
    }).join("");
    counts(); render();
    /* content above the anchor is rendered by script, so re-apply deep links like #mura */
    if (location.hash) { var target = document.getElementById(location.hash.slice(1)); if (target) setTimeout(function () { target.scrollIntoView(); }, 0); }
  })();

  /* ---------- Page: Place detail ---------------------------------------- */
  if (page === "place") (function () {
    function show() {
      var id = (location.hash || "").replace("#", "") || "sharyn";
      var p = placeById(id) || placeById("sharyn");
      var r = regionById(p.region);
      document.title = p.name + " · BASTAU LINE";
      $("#p-img").src = D.img(p.img, "big");
      $("#p-img").alt = p.name;
      $("#p-title").textContent = p.name;
      $("#p-lead").textContent = p.lead;
      $("#p-region").textContent = r ? r.name : "";
      $("#p-cat").textContent = D.cats[p.cat];
      var c = coords(p.lat, p.lon);
      $("#p-coords").hidden = !c; $("#p-coords-t").textContent = c;
      $("#p-facts").innerHTML = p.facts.map(function (f) { return '<div class="fact"><dt>' + esc(f[0]) + '</dt><dd>' + esc(f[1]) + '</dd></div>'; }).join("");
      $("#p-body").innerHTML = p.body.map(function (t, i) { return '<p' + (i === 0 ? ' class="dropcap"' : "") + '>' + esc(t) + '</p>'; }).join("");
      $("#p-dl").innerHTML =
        '<div><dt>Өңір</dt><dd>' + esc(r ? r.name : "") + '</dd></div>' +
        '<div><dt>Санат</dt><dd>' + esc(D.cats[p.cat]) + '</dd></div>' +
        (c ? '<div class="stack"><dt>Координаттар</dt><dd class="mono" dir="ltr">' + esc(c) + '</dd></div>' : "") +
        '<div><dt>Өңірдегі нысан</dt><dd class="tnum">' + (r ? r.count : "—") + '</dd></div>';
      var q = p.lat != null ? p.lon + "," + p.lat : "";
      var y = $("#p-map-yandex"), g = $("#p-map-2gis");
      if (y) { y.hidden = !q; y.href = "https://yandex.kz/maps/?pt=" + q + "&z=11&l=map"; }
      if (g) { g.hidden = !q; g.href = "https://2gis.kz/search/" + (p.lat != null ? p.lat + "%2C" + p.lon : ""); }
      var pb = $("#p-pano");
      if (pb) {
        pb.hidden = !p.pano;
        if (p.pano) { pb.setAttribute("data-pano", p.pano[0]); pb.setAttribute("data-pano-name", p.name + " · 360°"); $("#p-pano-n").textContent = p.pano.length; }
      }
      var rel = D.places.filter(function (x) { return x.id !== p.id && x.region === p.region; });
      if (rel.length < 3) rel = rel.concat(D.places.filter(function (x) { return x.id !== p.id && x.region !== p.region && x.cat === p.cat; }));
      $("#p-related").innerHTML = rel.slice(0, 3).map(placeCard).join("");
      $("#p-related-t").textContent = r && rel[0] && rel[0].region === p.region ? r.name + ": тағы көруге тұрарлық" : "Ұқсас нысандар";
      $("#p-crumb-region").textContent = r ? r.name : "";
      var share = location.href;
      $$("[data-share]").forEach(function (a) {
        var k = a.getAttribute("data-share");
        if (k === "wa") a.href = "https://wa.me/?text=" + encodeURIComponent(p.name + " — " + share);
        if (k === "tg") a.href = "https://t.me/share/url?url=" + encodeURIComponent(share) + "&text=" + encodeURIComponent(p.name);
        if (k === "copy") a.setAttribute("data-copy", share);
      });
      window.scrollTo(0, 0);
    }
    window.addEventListener("hashchange", show);
    show();
  })();

  /* ---------- Page: Service detail (document checklist) ---------------- */
  if (page === "service") (function () {
    var list = $("#checklist"); if (!list) return;
    var KEY = "kieli-atazholy-docs";
    var saved = {}; try { saved = JSON.parse(store.get(KEY) || "{}") || {}; } catch (e) { saved = {}; }
    var boxes = $$("input[type='checkbox']", list);
    boxes.forEach(function (b) { if (saved[b.id]) b.checked = true; });
    function upd() {
      var n = boxes.filter(function (b) { return b.checked; }).length;
      $("#ck-n").textContent = n + " / " + boxes.length;
      $("#ck-bar").style.width = Math.round(n / boxes.length * 100) + "%";
      $("#ck-msg").textContent = n === boxes.length ? "Бәрі дайын — енді құжаттарды бізге тексертіңіз." : n === 0 ? "Қолыңызда бар құжаттарды белгілеңіз." : "Жақсы бастама. Қалғанын жинауға көмектесеміз.";
      var s = {}; boxes.forEach(function (b) { if (b.checked) s[b.id] = 1; }); store.set(KEY, JSON.stringify(s));
    }
    list.addEventListener("change", upd); upd();
  })();

  /* ---------- "Сізге қандай көмек керек?" — needs-based service tabs ----- */
  var B = window.BASTAU || { tabs: [], articles: [], articleCats: {} };
  function ico(id) { return '<svg aria-hidden="true"><use href="#' + id + '"/></svg>'; }
  var SAMPLE_LANG = { notarial: "Аударма + нотариат", apostille: "Apostille", consular: "Заңдастыру", check: "Тексерілді" };
  function docsample(item) {
    var rows = [62, 80, 48, 72, 56].map(function (w) { return '<div class="docsample__row"><i></i><b style="width:' + w + '%"></b></div>'; }).join("");
    return '<div class="docsample" aria-hidden="true"><div class="docsample__top"><span>BASTAU LINE</span><span>ҮЛГІ</span></div>' +
      '<div class="docsample__title">' + esc(item.name) + '</div>' +
      '<span class="docsample__lang" translate="no">' + esc(SAMPLE_LANG[item.id] || "中文 → Қаз / Рус") + '</span>' +
      (item.id === "passport" || item.id === "license" ? '<div class="docsample__photo"></div>' : "") + rows +
      '<div class="docsample__stamp">ҮЛГІ</div></div>';
  }
  function illo(item) {
    return '<div class="illo" aria-hidden="true"><span class="illo__sun"></span><span class="illo__ring"></span><span class="illo__disc">' + ico(item.icon) + '</span></div>';
  }
  function detail(tab, item) {
    var consult = tab.id + ":" + item.id, cta = item.cta || tab.cta;
    var note = [item.note, tab.footnote, "Баға мен мерзім құжатқа және қызметке қарай нақтыланады."].filter(Boolean).join(" ");
    return '<div class="hdetail__visual">' + (tab.visual === "doc" ? docsample(item) : illo(item)) + '</div>' +
      '<div class="hdetail__body">' +
        '<p class="hdetail__k">' + esc(tab.name) + ' <span class="hdetail__code">' + esc(item.code || "") + '</span></p>' +
        '<h3 class="hdetail__t">' + esc(item.title) + '</h3>' +
        '<p class="hdetail__dir">' + esc(item.dir) + '</p>' +
        '<div class="hdetail__cols">' +
          '<div class="hdetail__block"><h4>Кімге керек</h4><p>' + esc(item.who) + '</p></div>' +
          '<div class="hdetail__block"><h4>Не дайындау керек</h4><ul>' + item.bring.map(function (x) { return '<li>' + esc(x) + '</li>'; }).join("") + '</ul></div>' +
        '</div>' +
        '<div class="hdetail__block"><h4>Қалай жүреді</h4><ol class="hsteps">' + tab.steps.map(function (x) { return '<li>' + esc(x) + '</li>'; }).join("") + '</ol></div>' +
        '<div class="hdetail__cta"><a class="btn btn--sun" href="#order-' + tab.id + '-' + item.id + '" data-consult="' + consult + '">' + esc(cta) + '</a>' +
          '<button type="button" class="quiet" data-consult="">Не керек екенін білмеймін</button>' +
          (item.link ? '<a class="link-arrow" href="' + item.link[0] + '">' + esc(item.link[1]) + '</a>' : "") + '</div>' +
        '<p class="hdetail__note">' + ico("i-info") + '<span>' + esc(note) + '</span></p>' +
      '</div>';
  }
  $$("[data-help]").forEach(function (root, n) {
    if (!B.tabs.length) return;
    var state = { tab: B.tabs[0], item: B.tabs[0].items[0] };
    var uid = "hp" + n;
    root.innerHTML =
      '<div class="htabs" role="tablist" aria-label="Қызмет түрлері">' + B.tabs.map(function (t) {
        return '<button type="button" class="htab" role="tab" id="' + uid + '-' + t.id + '" data-tab="' + t.id + '" aria-controls="' + uid + '-panel">' + ico(t.icon) + '<span>' + esc(t.name) + '</span></button>';
      }).join("") + '</div>' +
      '<div class="hpanel" role="tabpanel" id="' + uid + '-panel"><div class="hitems" role="group"></div><article class="hdetail" aria-live="polite"></article></div>';
    var tabsEl = $(".htabs", root), itemsEl = $(".hitems", root), detailEl = $(".hdetail", root), panel = $(".hpanel", root);
    function paint(focusTab) {
      $$(".htab", tabsEl).forEach(function (b) {
        var on = b.getAttribute("data-tab") === state.tab.id;
        b.setAttribute("aria-selected", String(on)); b.tabIndex = on ? 0 : -1;
        if (on && focusTab) { b.focus(); b.scrollIntoView({ block: "nearest", inline: "nearest" }); }
      });
      panel.setAttribute("aria-labelledby", uid + "-" + state.tab.id);
      itemsEl.setAttribute("aria-label", state.tab.name);
      itemsEl.innerHTML = state.tab.items.map(function (it) {
        return '<button type="button" class="hitem" data-item="' + it.id + '" aria-pressed="' + (it === state.item) + '">' + ico(it.icon) + '<span>' + esc(it.name) + '</span><span class="hitem__go">' + ico("i-arrow") + '</span></button>';
      }).join("");
      detailEl.innerHTML = detail(state.tab, state.item);
    }
    function pick(tabId, itemId, focusTab) {
      var t = B.tabs.filter(function (x) { return x.id === tabId; })[0] || B.tabs[0];
      var it = t.items.filter(function (x) { return x.id === itemId; })[0] || t.items[0];
      state.tab = t; state.item = it; paint(focusTab);
    }
    tabsEl.addEventListener("click", function (e) { var b = e.target.closest(".htab"); if (b) pick(b.getAttribute("data-tab")); });
    tabsEl.addEventListener("keydown", function (e) {
      var keys = { ArrowRight: 1, ArrowLeft: -1, Home: "first", End: "last" };
      if (!(e.key in keys)) return;
      e.preventDefault();
      var i = B.tabs.indexOf(state.tab), rtl = document.documentElement.dir === "rtl", k = keys[e.key];
      if (k === "first") i = 0; else if (k === "last") i = B.tabs.length - 1; else i = (i + (rtl ? -k : k) + B.tabs.length) % B.tabs.length;
      pick(B.tabs[i].id, null, true);
    });
    itemsEl.addEventListener("click", function (e) {
      var b = e.target.closest(".hitem"); if (!b) return;
      pick(state.tab.id, b.getAttribute("data-item"));
      var btn = $('.hitem[data-item="' + b.getAttribute("data-item") + '"]', itemsEl); if (btn) btn.focus();
      if (window.matchMedia("(max-width: 960px)").matches) detailEl.scrollIntoView({ behavior: "smooth", block: "start" });
    });
    function fromHash() {
      var m = (location.hash || "").match(/^#(?:s|order)-([a-z]+)(?:-([a-z]+))?$/);
      if (!m) return false;
      pick(m[1], m[2]); root.closest("section").scrollIntoView(); return true;
    }
    if (!fromHash()) paint();
    window.addEventListener("hashchange", fromHash);
  });

  /* ---------- Home: Kazakhstan in three tabs ----------------------------- */
  $$("[data-ktabs]").forEach(function (root) {
    var H = function (name) { return D.heritage.filter(function (h) { return h.name === name; })[0]; };
    var SETS = {
      tabigat: ["kolsai", "sharyn", "muztau", "khantengri", "alakol"].map(placeById),
      tarih: ["yassawi", "tanbaly", "otyrar", "beket", "berel"].map(placeById),
      dastur: ["Күй", "Киіз үй", "Айтыс", "Құсбегілік", "Наурыз"].map(H)
    };
    var grid = $(".kgrid", root);
    function tile(x, isHeritage, big) {
      if (!x) return "";
      if (isHeritage) return '<a class="tile" href="kazakhstan.html#mura"><img src="' + D.img(x.img, big ? "big" : "middle") + '" alt="' + esc(x.name) + '" loading="lazy"><span class="tile__top"><span class="tag tag--on-photo">' + esc(x.kind) + '</span></span><span class="tile__body"><span class="tile__name">' + esc(x.name) + '</span>' + (x.year ? '<span class="tile__fact">ЮНЕСКО · ' + x.year + '</span>' : "") + '</span></a>';
      var r = regionById(x.region);
      return '<a class="tile" href="place.html#' + x.id + '"><img src="' + D.img(x.img, big ? "big" : "middle") + '" alt="' + esc(x.name) + '" loading="lazy"><span class="tile__top"><span class="tag tag--on-photo">' + esc(r ? r.name : "") + '</span></span><span class="tile__body"><span class="tile__name">' + esc(x.name) + '</span><span class="tile__fact">' + esc(x.fact) + '</span></span></a>';
    }
    function show(key) {
      $$("[data-kt]", root).forEach(function (b) { var on = b.getAttribute("data-kt") === key; b.setAttribute("aria-selected", String(on)); b.tabIndex = on ? 0 : -1; });
      grid.innerHTML = SETS[key].map(function (x, i) { return tile(x, key === "dastur", i === 0); }).join("");
    }
    root.addEventListener("click", function (e) { var b = e.target.closest("[data-kt]"); if (b) show(b.getAttribute("data-kt")); });
    show("tabigat");
  });

  /* ---------- Articles: image + text posts with category filter --------- */
  function cover(a) {
    var c = a.cover || {};
    var inner = c.img ? '<img src="' + c.img + '" alt="" loading="lazy">' :
      a.cat === "news" ? '<span class="cover-news tone-' + (c.tone || "sky") + '">' + ico(c.icon || "i-doc") + '<span class="cover-news__date">' + esc(a.date) + '</span></span>' :
      '<span class="cover-illo tone-' + (c.tone || "sky") + '">' + ico(c.icon || "i-doc") + '</span>';
    return '<div class="post__cover">' + inner + (a.important ? '<span class="post__badge">Маңызды</span>' : "") + '</div>';
  }
  function postHTML(a, kind) {
    /* a news item of another site names it and opens the original there, in a new tab */
    var ext = /^https?:\/\//i.test(a.href || "");
    var meta = '<span class="post__m">' + (a.cat === "news" ? (a.source ? "Дереккөз: " + esc(a.source) + " · " : "") : esc(a.date) + " · ") + a.read + ' оқу</span>';
    var k = '<span class="post__k">' + esc(B.articleCats[a.cat] || "") + '</span>';
    var more = ext ? '<span class="post__more post__more--ext">Түпнұсқасын оқу</span>' : '<span class="post__more">Толығырақ</span>';
    var link = ' href="' + esc(a.href) + '"' + (ext ? ' target="_blank" rel="noopener"' : "");
    if (kind === "row") return '<a class="post post--row"' + link + '>' + cover(a) + '<div class="post__body">' + k + '<h3 class="post__t">' + esc(a.title) + '</h3>' + meta + more + '</div></a>';
    return '<a class="post' + (kind === "big" ? " post--big" : "") + '"' + link + '>' + cover(a) + k + '<h3 class="post__t">' + esc(a.title) + '</h3><p class="post__d">' + esc(a.excerpt) + '</p>' +
      '<span class="post__foot">' + meta + more + '</span></a>';
  }
  $$("[data-posts]").forEach(function (root) {
    var mode = root.getAttribute("data-posts"), list = $(".posts-list", root), cat = "all", q = "";
    var chips = $(".chips", root), more = $("[data-posts-more]", root), count = $("[data-posts-count]", root), search = $("[data-posts-q]", root);
    var hm = (location.hash || "").match(/^#cat-([a-z]+)$/);
    if (mode === "page" && hm && B.articleCats[hm[1]]) cat = hm[1];
    function n(k) { return B.articles.filter(function (a) { return k === "all" || a.cat === k; }).length; }
    chips.innerHTML = '<button type="button" class="chip" data-pcat="all" aria-pressed="true">Барлығы' + (mode === "page" ? ' <span class="n">' + n("all") + '</span>' : "") + '</button>' +
      Object.keys(B.articleCats).map(function (k) { return '<button type="button" class="chip" data-pcat="' + k + '" aria-pressed="false">' + esc(B.articleCats[k]) + (mode === "page" ? ' <span class="n">' + n(k) + '</span>' : "") + '</button>'; }).join("");
    function render() {
      var needle = q.toLowerCase().replace(/ٴ/g, "");
      var items = B.articles.filter(function (a) {
        if (cat !== "all" && a.cat !== cat) return false;
        if (!needle) return true;
        var hay = (a.title + " " + a.excerpt).toLowerCase();
        return hay.indexOf(needle) > -1 || Script.text(hay).replace(/ٴ/g, "").indexOf(needle) > -1;
      });
      items.sort(function (x, y) { return ((y.important ? 1 : 0) - (x.important ? 1 : 0)) || (y.sort > x.sort ? 1 : y.sort < x.sort ? -1 : 0); });
      $$(".chip", chips).forEach(function (c) { c.setAttribute("aria-pressed", String(c.getAttribute("data-pcat") === cat)); });
      if (more) more.href = "articles.html" + (cat === "all" ? "" : "#cat-" + cat);
      if (count) count.textContent = items.length + " материал" + (cat !== "all" ? " · " + B.articleCats[cat] : "");
      if (mode === "page" && window.history && history.replaceState) history.replaceState(null, "", cat === "all" ? location.pathname : "#cat-" + cat);
      if (!items.length) { list.className = "posts-list"; list.innerHTML = '<div class="empty">' + (needle ? "«" + esc(q) + "» бойынша ештеңе табылмады." : "Бұл санатта әзірге материал жоқ.") + '</div>'; return; }
      if (mode === "home") {
        list.className = "posts-list posts";
        list.innerHTML = postHTML(items[0], "big") + (items.length > 1 ? '<div class="posts__side">' + items.slice(1, 4).map(function (a) { return postHTML(a, "row"); }).join("") + '</div>' : "");
      } else {
        list.className = "posts-list pgrid-posts";
        list.innerHTML = items.map(function (a, i) { return postHTML(a, i === 0 ? "big" : ""); }).join("");
      }
    }
    chips.addEventListener("click", function (e) { var c = e.target.closest("[data-pcat]"); if (c) { cat = c.getAttribute("data-pcat"); render(); } });
    if (search) search.addEventListener("input", function () { q = search.value.trim(); render(); });
    render();
  });

  /* ---------- Restore script choice last, after dynamic content -------- */
  if (store.get("kieli-script") === "tote") Script.set("tote", false);
})();
