/* KieliSite admin: «Сайт параметрлері» and «Кэшті тазалау» (framework pages whose scripts were lost).
   Text values are edited in place and saved one by one (POST Site/Setting: name, value, pk);
   logos and the favicon upload on click; the cache button empties every cache. */
(function () {
  "use strict";
  var lang = (window.$qar && $qar.getCurrentLanguage()) || "kz";
  function post(path, data) {
    return fetch("/" + lang + path, { method: "POST", body: data }).then(function (r) { return r.json(); });
  }
  function toast(res) { if (window.$qar) $qar.showMessage(res.status, res.message); }

  /* what this site still takes from here; contacts, social links and SEO texts are page blocks now */
  var USED = ["title", "description", "analyticsHtml", "analyticsScript"];
  var table = document.querySelector("a[data-pk]") && document.querySelector("a[data-pk]").closest("table");
  if (table) {
    var note = document.createElement("div");
    note.className = "alert alert-light-primary kf-help";
    note.textContent = "Байланыс деректері, әлеуметтік желілер және беттердің SEO мәтіндері — «Беттер мен мәтіндер» бөлімінде. Мұнда: сайт белгішесі (favicon), әкімші панелінің логотиптері, әкімші бетінің атауы мен аналитика коды (Яндекс Метрика, Google Analytics). Мәнді өзгерту үшін оны басыңыз.";
    table.parentElement.insertBefore(note, table);
  }
  document.querySelectorAll("a[data-pk]").forEach(function (a) {
    var m = a.className.match(/siteSetting-([A-Za-z]+)/), name = m && m[1];
    var row = a.closest("tr");
    if (!name) return;
    if (USED.indexOf(name) < 0) { if (row) row.hidden = true; return; }
    a.classList.add("kf-inline");
    if (!a.textContent.trim()) { a.textContent = "—"; a.setAttribute("data-empty", "1"); }
    a.addEventListener("click", function () { edit(a, name); });
  });
  var mourning = document.getElementById("mourning-day");
  if (mourning && mourning.closest("tr")) mourning.closest("tr").hidden = true;

  function edit(a, name) {
    if (a.hidden) return;
    var box = document.createElement("div"), ta = document.createElement("textarea");
    box.className = "kf-inline-form";
    ta.className = "form-control"; ta.rows = /analytics/i.test(name) ? 8 : 2; ta.spellcheck = !/analytics/i.test(name);
    ta.value = a.getAttribute("data-empty") ? "" : a.textContent.trim();
    var save = document.createElement("button"), cancel = document.createElement("button");
    save.type = cancel.type = "button";
    save.className = "btn btn-primary btn-sm"; save.textContent = "Сақтау";
    cancel.className = "btn btn-outline-secondary btn-sm"; cancel.textContent = "Бас тарту";
    var bar = document.createElement("div"); bar.className = "d-flex gap-2 mt-2"; bar.append(save, cancel);
    box.append(ta, bar);
    a.hidden = true; a.after(box); ta.focus();
    cancel.addEventListener("click", function () { box.remove(); a.hidden = false; });
    save.addEventListener("click", function () {
      var data = new FormData();
      data.append("name", name); data.append("value", ta.value); data.append("pk", a.getAttribute("data-pk"));
      save.disabled = true;
      post("/Site/Setting", data).then(function (res) {
        toast(res);
        if (res.status === "success") {
          a.textContent = ta.value.trim() || "—";
          if (ta.value.trim()) a.removeAttribute("data-empty"); else a.setAttribute("data-empty", "1");
          box.remove(); a.hidden = false;
        }
      }).catch(function (e) { toast({ status: "error", message: e.message }); }).then(function () { save.disabled = false; });
    });
  }

  document.querySelectorAll("img.siteSetting-logo, img.siteSetting-icon").forEach(function (img) {
    var input = img.parentElement.querySelector("input[type='file']");
    if (!input) return;
    img.style.cursor = "pointer"; img.title = "Жаңа сурет жүктеу үшін басыңыз";
    img.addEventListener("click", function () { input.click(); });
    input.addEventListener("change", function () {
      var file = input.files && input.files[0]; if (!file) return;
      var data = new FormData(), path;
      if (input.name === "iconFile") { data.append("iconFile", file); path = "/Site/UploadSiteIcon"; }
      else { data.append("logoImage", file); data.append("type", input.getAttribute("data-type")); path = "/Site/UploadSiteLogo"; }
      post(path, data).then(function (res) {
        toast(res);
        if (res.status === "success") img.src = URL.createObjectURL(file);
      }).catch(function (e) { toast({ status: "error", message: e.message }); }).then(function () { input.value = ""; });
    });
  });

  var flush = document.getElementById("btn-flush-cache");
  if (flush) flush.addEventListener("click", function () {
    if (window.$qar) $qar.setBtnStatus(flush, "loading");
    post("/Site/FlushCache", new FormData()).then(toast)
      .catch(function (e) { toast({ status: "error", message: e.message }); })
      .then(function () { if (window.$qar) $qar.setBtnStatus(flush, "reset"); });
  });
})();
