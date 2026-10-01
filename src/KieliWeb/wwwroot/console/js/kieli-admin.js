/* KieliSite admin: the field editor of page blocks and content rows (Views/Console/Cms/_Fields.cshtml).
   Fields are read back into one JSON value (hidden input "dataJson") just before main.js sends the form. */
(function () {
  "use strict";
  var lang = (window.$qar && $qar.getCurrentLanguage()) || "kz";
  var uid = 0;
  function $$(s, r) { return Array.prototype.slice.call((r || document).querySelectorAll(s)); }
  function kids(el, sel) { return Array.prototype.filter.call(el.children, function (c) { return c.matches(sel); }); }
  function toast(status, text) { if (window.$qar) $qar.showMessage(status, text); else alert(text); }

  /* ---------- reading the fields --------------------------------------------- */
  function collect(container) {
    var out = {};
    kids(container, ".kf-field").forEach(function (f) {
      var name = f.getAttribute("data-kf-name"), kind = f.getAttribute("data-kf-kind");
      if (kind === "list") {
        var rows = kids(f, ".kf-rows")[0];
        out[name] = rows ? kids(rows, ".kf-row").map(function (row) {
          var o = collect(kids(row, ".kf-fields")[0]), id = row.getAttribute("data-kf-rowid");
          if (id) o._id = id;   /* the row keeps its id: translations of the row point to it */
          return o;
        }) : [];
        return;
      }
      var input = f.querySelector("[data-kf-input]");
      if (!input) return;
      if (kind === "bool") { out[name] = input.checked; return; }
      if (kind === "html" && window.tinymce && tinymce.get(input.id)) { out[name] = tinymce.get(input.id).getContent(); return; }
      out[name] = input.value;
    });
    return out;
  }
  /* capture phase: runs before main.js reads the form for sending */
  document.addEventListener("submit", function (e) {
    var form = e.target;
    if (!form.matches || !form.matches("form[data-kf-form]")) return;
    form.querySelector("[data-kf-json]").value = JSON.stringify(window.KieliFields.value(form));
  }, true);

  /* ---------- rich text ------------------------------------------------------------ */
  function upload(file, audio) {
    var data = new FormData();
    data.append("file", file, file.name || (audio ? "audio.mp3" : "image.png"));
    return fetch("/" + lang + (audio ? "/content/uploadaudio" : "/content/upload"), { method: "POST", body: data })
      .then(function (r) { return r.json(); })
      .then(function (res) {
        if (res.status !== "success") throw new Error(res.message || "Жүктелмеді");
        return res.data.url;
      });
  }
  function initHtml(ta) {
    if (!window.tinymce || ta.getAttribute("data-kf-ready")) return;
    ta.setAttribute("data-kf-ready", "1");
    var dark = window.$qar && $qar.getCurrentTheme() === "dark";
    tinymce.init({
      target: ta,
      height: ta.getAttribute("data-kf-large") ? 620 : 240,
      menubar: false, statusbar: true, elementpath: false, branding: false, promotion: false,
      language: lang === "kz" ? "kk" : lang === "ru" ? "ru" : undefined,
      skin: dark ? "oxide-dark" : "oxide", content_css: dark ? "dark" : "default",
      plugins: "link image lists table code autolink media wordcount",
      toolbar: "undo redo | blocks | bold italic underline | bullist numlist blockquote | link image media table | removeformat code",
      block_formats: "Мәтін=p; Тақырып 2=h2; Тақырып 3=h3",
      convert_urls: false, relative_urls: false,
      image_caption: true, image_dimensions: false,
      images_upload_handler: function (blob) { return upload(blob.blob ? new File([blob.blob()], blob.filename()) : blob); },
      content_style: "body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;font-size:16px;line-height:1.65;max-width:760px;margin:12px auto}img{max-width:100%;height:auto}"
    });
  }

  /* ---------- lists ------------------------------------------------------------- */
  function renumber(rowsEl) {
    kids(rowsEl, ".kf-row").forEach(function (row, i) {
      var n = row.querySelector(":scope > .kf-row__bar .kf-row__n"); if (n) n.textContent = (i + 1) + ".";
      summary(row);
    });
  }
  function summary(row) {
    var s = row.querySelector(":scope > .kf-row__bar .kf-row__sum"); if (!s) return;
    var fields = kids(kids(row, ".kf-fields")[0], ".kf-field");
    var text = "";
    fields.some(function (f) {
      var k = f.getAttribute("data-kf-kind"), input = f.querySelector("[data-kf-input]");
      if (input && (k === "text" || k === "textarea" || k === "lines" || k === "url") && input.value.trim()) { text = input.value.trim(); return true; }
      return false;
    });
    s.textContent = text.length > 90 ? text.slice(0, 89) + "…" : text;
  }
  function freshIds(root) {
    $$("[id^='kf']", root).forEach(function (el) {
      var old = el.id, id = "kfn" + (++uid);
      el.id = id;
      $$("label[for='" + old + "']", root).forEach(function (l) { l.setAttribute("for", id); });
      el.removeAttribute("data-kf-ready");
    });
  }
  function setup(root) {
    $$("textarea.kf-html", root).forEach(initHtml);
    $$(".kf-rows", root).forEach(renumber);
  }
  document.addEventListener("click", function (e) {
    var b = e.target.closest("[data-kf-add],[data-kf-up],[data-kf-down],[data-kf-remove],[data-kf-copy],[data-kf-clear]");
    if (!b) return;
    e.preventDefault();
    if (b.hasAttribute("data-kf-clear")) {
      var box = b.closest(".kf-image"), input = box.querySelector("[data-kf-input]");
      input.value = ""; input.dispatchEvent(new Event("input", { bubbles: true }));
      return;
    }
    if (b.hasAttribute("data-kf-add")) {
      var field = b.closest(".kf-field"), rows = kids(field, ".kf-rows")[0], tpl = kids(field, "template")[0];
      var node = tpl.content.firstElementChild.cloneNode(true);
      freshIds(node);
      rows.appendChild(node);
      setup(node); renumber(rows);
      var first = node.querySelector("[data-kf-input]"); if (first) first.focus();
      return;
    }
    var row = b.closest(".kf-row"), list = row.parentElement;
    if (b.hasAttribute("data-kf-remove")) {
      var what = row.querySelector(".kf-row__sum").textContent;
      if (!window.confirm("Жолды өшіресіз бе?" + (what ? "\n\n" + what : ""))) return;
      $$("textarea.kf-html", row).forEach(function (t) { var ed = window.tinymce && tinymce.get(t.id); if (ed) ed.remove(); });
      row.remove();
    } else if (b.hasAttribute("data-kf-copy")) {
      $$("textarea.kf-html", row).forEach(function (t) { var ed = window.tinymce && tinymce.get(t.id); if (ed) ed.save(); });
      var copy = row.cloneNode(true);
      copy.removeAttribute("data-kf-rowid");   /* a copy is a new row */
      $$(".tox-tinymce", copy).forEach(function (x) { x.remove(); });
      $$("textarea.kf-html", copy).forEach(function (t) { t.style.display = ""; t.removeAttribute("aria-hidden"); });
      /* cloneNode keeps attributes, not what was typed: copy the current values over */
      var src = $$("[data-kf-input]", row), dst = $$("[data-kf-input]", copy);
      dst.forEach(function (d, i) { if (d.type === "checkbox") d.checked = src[i].checked; else d.value = src[i].value; });
      freshIds(copy);
      row.after(copy);
      setup(copy);
    } else if (b.hasAttribute("data-kf-up") && row.previousElementSibling) {
      row.previousElementSibling.before(row);
    } else if (b.hasAttribute("data-kf-down") && row.nextElementSibling) {
      row.nextElementSibling.after(row);
    }
    renumber(list);
  });

  /* ---------- previews ------------------------------------------------------------ */
  document.addEventListener("input", function (e) {
    var t = e.target;
    var img = t.closest(".kf-image");
    if (img && t.matches("[data-kf-input]")) {
      var p = img.querySelector(".kf-image__preview, .kf-audio__preview");
      p.hidden = !t.value.trim(); if (t.value.trim()) p.src = t.value.trim(); else if (p.pause) p.pause();
    }
    var row = t.closest(".kf-row"); if (row) summary(row);
  });
  document.addEventListener("change", function (e) {
    var t = e.target;
    var icon = t.closest(".kf-icon");
    if (icon && t.matches("select")) icon.querySelector("use").setAttribute("href", "#" + t.value);
    if (t.matches("input[type='file'][data-kf-upload]") && t.files && t.files[0]) {
      var box = t.closest(".kf-image"), input = box.querySelector("[data-kf-input]"), label = t.closest("label");
      var audio = t.getAttribute("data-kf-upload") === "audio";
      label.classList.add("disabled");
      upload(t.files[0], audio).then(function (url) {
        input.value = url; input.dispatchEvent(new Event("input", { bubbles: true }));
        toast("success", audio ? "Аудио жүктелді" : "Сурет жүктелді");
      }).catch(function (err) { toast("error", err.message); })
        .then(function () { label.classList.remove("disabled"); t.value = ""; });
    }
  });

  /* ---------- page blocks: back to the prototype texts --------------------------------- */
  document.addEventListener("click", function (e) {
    var b = e.target.closest("[data-kf-reset]");
    if (!b) return;
    if (!window.confirm("Блоктың барлық мәтіні мен суреті бастапқы күйіне қайтады. Жалғастырасыз ба?")) return;
    var data = new FormData();
    data.append("manageType", "reset");
    data.append("idList[]", b.getAttribute("data-id"));
    fetch(b.getAttribute("data-kf-reset"), { method: "POST", body: data }).then(function (r) { return r.json(); }).then(function (res) {
      toast(res.status, res.message);
      if (res.status === "success") setTimeout(function () { location.reload(); }, 800);
    }).catch(function (err) { toast("error", err.message); });
  });

  /* for scripts and checks: the JSON a form would send */
  window.KieliFields = {
    value: function (form) {
      var value = {};
      $$("[data-kf-root]", form).forEach(function (root) {
        var key = root.getAttribute("data-kf-key"), v = collect(root);
        if (key) value[key] = v; else Object.keys(v).forEach(function (k) { value[k] = v[k]; });
      });
      return value;
    }
  };

  document.addEventListener("DOMContentLoaded", function () { setup(document); });
  if (document.readyState !== "loading") setup(document);
})();
