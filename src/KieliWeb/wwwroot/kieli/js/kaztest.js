/* ҚАЗТЕСТ байқау тестілеуі — the practice exam on /kz/kaztest.
   Variants, questions, listening recordings and reading passages come from the admin («ҚАЗТЕСТ»), as
   window.KAZTEST; a new variant gets a card. A recording whose audio is not uploaded yet shows a notice and
   its section is not judged. After the test the recordings' scripts can be read under the player.
   The exam always stays in Cyrillic (data-no-tote), like the official test.
   Progress is kept in this browser, so a reload (WeChat's browser often reloads) loses nothing. */
(function () {
  "use strict";
  var K = window.KAZTEST, root = document.querySelector("[data-kaztest]");
  if (!K || !K.tests || !root) return;
  var L = "ABCD", MIN = K.minutes || 70, RUN = "kieli-kaztest-run-", RES = "kieli-kaztest-results";
  var PASS = typeof K.pass === "number" ? K.pass : 0.7, PCT = Math.round(PASS * 100);   // official: at least 70% in every section
  /* interface texts in the page language (admin: «Интерфейс жазулары» → ҚАЗТЕСТ); the questions stay Kazakh */
  var UI = (window.KIELI_UI_KAZTEST || {}).kaztest || {};
  function tr(k, vars) {
    var s = UI[k] != null ? UI[k] : k;
    if (vars) Object.keys(vars).forEach(function (v) { s = s.split("{" + v + "}").join(vars[v]); });
    return s;
  }
  function tx(k, vars) { return esc(tr(k, vars)); }
  var store = {
    get: function (k) { try { return JSON.parse(localStorage.getItem(k) || "null"); } catch (e) { return null; } },
    set: function (k, v) { try { localStorage.setItem(k, JSON.stringify(v)); } catch (e) {} },
    del: function (k) { try { localStorage.removeItem(k); } catch (e) {} }
  };
  function esc(s) { return String(s == null ? "" : s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;"); }
  function rich(s) { return esc(s).replace(/&lt;(\/?)u&gt;/g, "<$1u>"); }
  function ico(id) { return '<svg aria-hidden="true"><use href="#' + id + '"/></svg>'; }
  /* which recording a listening question belongs to: its clip (admin «Тыңдалым жазбалары»), else its audio link */
  function clipOf(q) { return q.a != null ? "c" + q.a : q.audio ? "u" + q.audio : null; }
  /* who made the audio (a voice actor, or the synthetic voice and its licence), under the player */
  function credit(clip) {
    if (!clip || !clip.credit) return "";
    return '<p class="qaudio__credit">' + (clip.creditUrl ? '<a href="' + esc(clip.creditUrl) + '" target="_blank" rel="noopener">' + esc(clip.credit) + '</a>' : esc(clip.credit)) + '</p>';
  }
  /* saved progress follows the variant, not its place in the list (the admin may reorder them),
     and its questions: when they are created anew (rev), answers to the old ones are not reused */
  function key(i) { var t = K.tests[i]; return t && t.id != null ? "v" + t.id + (t.rev ? "-" + t.rev : "") : String(i); }
  function pad(n) { return (n < 10 ? "0" : "") + n; }
  function mmss(ms) { var s = Math.max(0, Math.round(ms / 1000)); return Math.floor(s / 60) + ":" + pad(s % 60); }
  function today() { var d = new Date(); return pad(d.getDate()) + "." + pad(d.getMonth() + 1) + "." + d.getFullYear(); }
  function blank(t) { return t.sections.map(function (s) { return s.questions.map(function () { return null; }); }); }
  function isListening(s) { return !!s.listen || s.questions.some(function (q) { return q.audio || q.a != null; }); }
  /* a listening question whose recording has no audio yet can be answered, but is not scored */
  K.tests.forEach(function (t) {
    t.clips = t.clips || [];
    t.sections.forEach(function (s) {
      var l = isListening(s);
      s.questions.forEach(function (q) {
        if (!q.audio && q.a != null && t.clips[q.a]) q.audio = t.clips[q.a].audio || null;   /* the audio of the question's recording */
        q.wait = l && !q.audio;
      });
    });
  });
  function graded(q) { return !!q.ans && !q.wait; }
  function waits(s) { return s.questions.some(function (q) { return q.wait; }); }
  function score(t, answers) {
    var out = { total: 0, known: 0, answered: 0, count: 0, secs: [] };
    t.sections.forEach(function (s, si) {
      var sc = 0, kn = 0, an = 0;
      s.questions.forEach(function (q, qi) { var a = answers[si][qi]; if (a) an++; if (graded(q)) { kn++; if (a === q.ans) sc++; } });
      out.secs.push({ name: s.name, score: sc, known: kn, answered: an, count: s.questions.length, wait: waits(s) });
      out.total += sc; out.known += kn; out.answered += an; out.count += s.questions.length;
    });
    return out;
  }
  function needFor(n) { return Math.ceil(n * PASS - 1e-9); }
  // pass/fail per section; a section can only be judged when its whole answer key is in
  function judge(t, answers) {
    var r = score(t, answers);
    r.secs.forEach(function (s) { s.need = needFor(s.count); s.full = s.known === s.count; s.ok = s.full && s.score >= s.need; });
    r.allFull = r.secs.every(function (s) { return s.full; });
    r.allPass = r.allFull && r.secs.every(function (s) { return s.ok; });
    return r;
  }
  function remember(ti, answers) {
    var r = judge(K.tests[ti], answers), all = store.get(RES) || {};
    all[key(ti)] = { score: r.total, known: r.known, answered: r.answered, date: today(), pass: r.allFull ? r.allPass : null };
    store.set(RES, all);
  }
  // a saved, unfinished run for variant i (null if none); a run whose time ran out while away is scored
  function liveRun(i) {
    var r = store.get(RUN + key(i)), t = K.tests[i];
    if (!r || !r.answers || r.answers.length !== t.sections.length ||
        r.answers.some(function (a, si) { return a.length !== t.sections[si].questions.length; })) { store.del(RUN + key(i)); return null; }
    if (r.end <= Date.now()) { remember(i, r.answers); store.del(RUN + key(i)); return null; }
    return r;
  }

  /* ---------- the list of variants ---------------------------------------- */
  function renderList() {
    var runs = K.tests.map(function (t, i) { return liveRun(i); });   // first: this may score an expired run
    var res = store.get(RES) || {};
    root.innerHTML = '<div class="kt-list">' + K.tests.map(function (t, i) {
      var total = 0, known = 0, run = runs[i], last = res[key(i)], soon = t.sections.some(waits);
      t.sections.forEach(function (s) { total += s.questions.length; s.questions.forEach(function (q) { if (q.ans) known++; }); });
      var meta = t.sections.map(function (s) {
        return '<div><dt>' + ico(isListening(s) ? "i-headphones" : "i-book") + '<span lang="kk">' + esc(s.name) + '</span></dt><dd>' + tx("questions", { n: s.questions.length }) + '</dd></div>';
      }).join("") + '<div><dt>' + ico("i-clock") + tx("time") + '</dt><dd>' + tx("minutes", { n: MIN }) + '</dd></div>' +
        '<div><dt>' + ico("i-award") + tx("passMark") + '</dt><dd>' + tx("passEach", { p: PCT }) + '</dd></div>';
      return '<article class="kt-card" id="nusqa-' + (i + 1) + '">' +
        '<h3 class="kt-card__t">' + esc(t.title) + '</h3>' +
        '<dl class="kt-card__meta">' + meta + '</dl>' +
        (known < total ? '<p class="kt-key"><span>' + tx("key") + '</span><b>' + known + ' / ' + total + '</b><i style="--p:' + Math.round(known / total * 100) + '%"></i></p>' : '') +
        (soon ? '<p class="kt-card__soon">' + ico("i-headphones") + '<span>' + tx("audioSoon") + '</span></p>' : '') +
        (last ? '<p class="kt-card__last">' + ico("i-check-circle") + '<span>' + tx("last") + ': <b>' + last.score + ' / ' + last.known + '</b> · ' + esc(last.date) +
          (last.pass === true ? ' · <b class="is-pass">' + tx("passed") + '</b>' : last.pass === false ? ' · <b class="is-fail">' + tx("failed") + '</b>' : '') + '</span></p>' : "") +
        '<div class="kt-card__cta">' + (run
          ? '<button type="button" class="btn btn--sun" data-kt-resume="' + i + '">' + tx("resume") + ' · ' + mmss(run.end - Date.now()) + '</button>' +
            '<button type="button" class="quiet" data-kt-start="' + i + '">' + tx("restartFromStart") + '</button>'
          : '<button type="button" class="btn btn--sun" data-kt-start="' + i + '">' + tx("start") + '</button>' +
            '<button type="button" class="quiet" data-kt-review="' + i + '">' + tx("review") + '</button>') +
        '</div></article>';
    }).join("") + '</div>';
  }
  root.addEventListener("click", function (e) {
    var b = e.target.closest("[data-kt-start],[data-kt-review],[data-kt-resume]");
    if (!b) return;
    if (b.hasAttribute("data-kt-resume")) start(+b.getAttribute("data-kt-resume"), "resume");
    else if (b.hasAttribute("data-kt-review")) start(+b.getAttribute("data-kt-review"), "review");
    else start(+b.getAttribute("data-kt-start"), "run");
  });

  /* ---------- the exam (full screen) ------------------------------------- */
  var ex = document.createElement("div");
  ex.className = "exam"; ex.hidden = true; ex.dir = "ltr"; ex.lang = document.documentElement.lang || "kk";
  ex.setAttribute("data-no-tote", ""); ex.setAttribute("role", "dialog"); ex.setAttribute("aria-modal", "true"); ex.setAttribute("aria-labelledby", "exam-t");
  ex.innerHTML =
    '<header class="exam__bar">' +
      '<button type="button" class="icon-btn" data-kt-exit aria-label="' + tx("exit") + '">' + ico("i-close") + '</button>' +
      '<p class="exam__title"><b id="exam-t"></b><span data-kt-where></span></p>' +
      '<span class="exam__timer" data-kt-timer role="timer" aria-label="' + tx("timeLeft") + '"></span>' +
      '<button type="button" class="btn btn--sm exam__finish" data-kt-finish>' + tx("finish") + '</button>' +
    '</header>' +
    '<div class="exam__body" data-kt-body></div>' +
    '<div class="exam__dialog" data-kt-dialog hidden><div class="exam__dialog-card" role="alertdialog" aria-modal="true" aria-labelledby="kt-dlg-t" data-kt-card></div></div>';
  document.body.appendChild(ex);
  function $(s) { return ex.querySelector(s); }
  var st = null, tick = null, audio = null;

  function start(ti, mode) {
    var t = K.tests[ti], saved = mode === "resume" ? liveRun(ti) : null;
    if (mode === "resume" && !saved) mode = "run";
    st = saved
      ? { ti: ti, t: t, sec: saved.sec || 0, idx: saved.idx || 0, answers: saved.answers, end: saved.end, done: false }
      : { ti: ti, t: t, sec: 0, idx: 0, answers: blank(t), end: Date.now() + MIN * 60000, done: mode === "review", reviewOnly: mode === "review" };
    if (mode === "run") save();
    $("#exam-t").textContent = t.title;
    $("[data-kt-timer]").hidden = st.done; $("[data-kt-finish]").hidden = st.done;
    ex.hidden = false; document.body.classList.add("is-exam"); document.body.style.overflow = "hidden";
    if (!st.done) startTimer();
    render(true);
  }
  function closeExam() {
    stopTimer(); if (audio) audio.pause();
    closeDialog(); ex.hidden = true; st = null;
    document.body.classList.remove("is-exam"); document.body.style.overflow = "";
    renderList();
  }
  function save() { if (st && !st.done) store.set(RUN + key(st.ti), { answers: st.answers, end: st.end, sec: st.sec, idx: st.idx }); }
  function startTimer() {
    stopTimer();
    var el = $("[data-kt-timer]");
    function upd() {
      if (!st || st.done) return;
      var left = st.end - Date.now();
      el.textContent = mmss(left);
      el.classList.toggle("is-low", left < 5 * 60000);
      if (left <= 0) { stopTimer(); doFinish(true); }
    }
    upd(); tick = setInterval(upd, 500);
  }
  function stopTimer() { if (tick) clearInterval(tick); tick = null; }

  function render(moved) {
    var t = st.t, S = t.sections[st.sec], q = S.questions[st.idx], a = st.answers[st.sec][st.idx], review = st.done;
    $("[data-kt-where]").textContent = S.name + " · " + (st.idx + 1) + " / " + S.questions.length;
    var tabs = t.sections.map(function (s, i) {
      var n = st.answers[i].filter(Boolean).length;
      return '<button type="button" role="tab" aria-selected="' + (i === st.sec) + '" data-kt-go="' + i + ',0">' + ico(isListening(s) ? "i-headphones" : "i-book") + esc(s.name) +
        (review ? "" : '<span>' + n + '/' + s.questions.length + '</span>') + '</button>';
    }).join("") + (review && !st.reviewOnly ? '<button type="button" class="exam__res" data-kt-results>' + ico("i-award") + tx("result") + '</button>' : "");
    var grid = S.questions.map(function (qq, i) {
      var ua = st.answers[st.sec][i], c = "qn";
      if (review) c += !graded(qq) ? " is-unk" : ua === qq.ans ? " is-right" : ua ? " is-wrong" : st.reviewOnly ? "" : " is-miss";
      else if (ua) c += " is-done";
      if (i === st.idx) c += " is-cur";
      return '<button type="button" class="' + c + '" data-kt-go="' + st.sec + ',' + i + '" aria-label="' + tx("questionN", { n: i + 1 }) + '"' + (i === st.idx ? ' aria-current="true"' : "") + '>' + (i + 1) + '</button>';
    }).join("");
    var passage = q.p != null && t.passages[q.p]
      ? '<section class="qpass" aria-label="' + tx("passage") + '"><p class="qpass__k">' + tx("passageN", { n: q.p + 1 }) + '</p><div lang="kk">' + t.passages[q.p].map(function (p) { return '<p>' + esc(p) + '</p>'; }).join("") + '</div></section>' : "";
    var au = "", me = clipOf(q);
    if (me) {
      var list = [], nums = [], clip = q.a != null ? t.clips[q.a] : null;
      S.questions.forEach(function (x, i) { var c = clipOf(x); if (c && list.indexOf(c) < 0) list.push(c); if (c === me) nums.push(i + 1); });
      au = '<div class="qaudio">' + ico("i-headphones") + '<p><b>' + tx("audioN", { n: list.indexOf(me) + 1, total: list.length }) + '</b><span>' +
        (nums.length > 1 ? tx("audioMany", { from: nums[0], to: nums[nums.length - 1] }) : tx("audioOne", { n: nums[0] })) + '</span></p>' +
        (q.audio ? '<div class="qaudio__player" data-kt-audio></div>' + credit(clip) : '<p class="qaudio__wait">' + tx("audioPending") + '</p>') +
        (review && clip && clip.lines && clip.lines.length ? '<details class="qscript"><summary>' + tx("transcript") + '</summary><div lang="kk">' + clip.lines.map(function (l) {
          return '<p>' + (l.who ? '<b>' + esc(l.who) + ':</b> ' : '') + esc(l.text) + '</p>';
        }).join("") + '</div></details>' : '') + '</div>';
    }
    var opts = q.opts.map(function (o, i) {
      var c = "qopt", k = L.charAt(i);
      if (review) { if (q.ans === k) c += " is-right"; else if (a === k) c += " is-wrong"; }
      else if (a === k) c += " is-sel";
      return '<button type="button" class="' + c + '" role="radio" aria-checked="' + (a === k) + '" data-kt-pick="' + i + '"' + (review ? ' aria-disabled="true"' : "") + '><b>' + k + '</b><span lang="kk">' + esc(o) + '</span></button>';
    }).join("");
    var fb = !review ? "" : '<p class="qfb' + (q.ans && a ? (a === q.ans ? " is-right" : " is-wrong") : "") + '">' +
      (q.ans ? tx("correct") + ': <b>' + q.ans + '</b>' : tx("noKey")) +
      (a ? ' · ' + tx("yours") + ': <b>' + a + '</b>' : st.reviewOnly ? "" : ' · ' + tx("unanswered")) + '</p>';
    var lastQ = st.idx === S.questions.length - 1, lastS = st.sec === t.sections.length - 1;
    var next = !lastQ ? '<button type="button" class="btn btn--primary btn--sm" data-kt-next>' + tx("next") + ico("i-arrow") + '</button>'
      : !lastS ? '<button type="button" class="btn btn--primary btn--sm" data-kt-go="' + (st.sec + 1) + ',0">' + tx("nextSection", { name: t.sections[st.sec + 1].name }) + ico("i-arrow") + '</button>'
      : review ? (st.reviewOnly ? "" : '<button type="button" class="btn btn--primary btn--sm" data-kt-results>' + tx("backToResult") + '</button>')
      : '<button type="button" class="btn btn--sun btn--sm" data-kt-finish>' + tx("finishTest") + '</button>';
    $("[data-kt-body]").innerHTML =
      '<div class="exam__wrap">' +
        '<div class="exam__tabs" role="tablist" aria-label="' + tx("sectionsAria") + '">' + tabs + '</div>' +
        '<div class="qgrid" role="group" aria-label="' + tx("gridAria", { name: S.name }) + '">' + grid + '</div>' +
        '<div class="qview' + (passage ? ' qview--split' : '') + '">' + passage +
          '<section class="qbox">' + au +
            '<p class="qbox__n"><span lang="kk">' + esc(S.name) + '</span> · ' + tx("questionN", { n: st.idx + 1 }) + '</p>' +
            '<h2 class="qbox__q" tabindex="-1" lang="kk">' + esc(q.q) + '</h2>' + (q.sub ? '<p class="qbox__sub" lang="kk">' + rich(q.sub) + '</p>' : '') +
            '<div class="qopts" role="radiogroup" aria-label="' + tx("optionsAria") + '">' + opts + '</div>' + fb +
            '<div class="qnav"><button type="button" class="btn btn--ghost btn--sm qnav__prev" data-kt-prev' + (st.sec === 0 && st.idx === 0 ? ' disabled' : '') + '>' + ico("i-arrow") + tx("prev") + '</button>' + next + '</div>' +
          '</section>' +
        '</div>' +
        (review ? '' : '<p class="exam__hint">' + tx("hint") + '</p>') +
      '</div>';
    // one player for the whole section, so moving between questions of the same recording keeps it playing
    var slot = $("[data-kt-audio]");
    if (slot) {
      if (!audio) { audio = document.createElement("audio"); audio.controls = true; audio.preload = "none"; }
      if (audio.getAttribute("src") !== q.audio) { audio.pause(); audio.setAttribute("src", q.audio); }
      slot.appendChild(audio);
    } else if (audio) audio.pause();
    var cur = $(".qn.is-cur"); if (cur && cur.scrollIntoView) cur.scrollIntoView({ block: "nearest", inline: "center" });
    if (moved) { ex.scrollTop = 0; var h = $(".qbox__q"); if (h) h.focus({ preventScroll: true }); }
  }
  function go(sec, idx) { st.sec = sec; st.idx = idx; save(); render(true); }
  function next() { var S = st.t.sections[st.sec]; if (st.idx < S.questions.length - 1) go(st.sec, st.idx + 1); else if (st.sec < st.t.sections.length - 1) go(st.sec + 1, 0); }
  function prev() { if (st.idx > 0) go(st.sec, st.idx - 1); else if (st.sec > 0) go(st.sec - 1, st.t.sections[st.sec - 1].questions.length - 1); }
  function pick(i) {
    if (st.done) return;
    var k = L.charAt(i), cur = st.answers[st.sec][st.idx];
    st.answers[st.sec][st.idx] = cur === k ? null : k;
    save(); render(false);
  }

  function dialog(html) { $("[data-kt-card]").innerHTML = html; $("[data-kt-dialog]").hidden = false; var b = $("[data-kt-card] .btn:last-child"); if (b) b.focus(); }
  function closeDialog() { $("[data-kt-dialog]").hidden = true; }
  function askFinish() {
    if (!st || st.done) return;
    var rows = st.t.sections.map(function (s, i) {
      var n = st.answers[i].filter(Boolean).length;
      return '<tr><th scope="row">' + esc(s.name) + '</th><td>' + s.questions.length + '</td><td>' + n + '</td><td>' + (s.questions.length - n) + '</td></tr>';
    }).join("");
    dialog('<h2 id="kt-dlg-t">' + tx("askFinish") + '</h2>' +
      '<div class="kt-table-wrap"><table class="kt-table"><thead><tr><th scope="col">' + tx("colSection") + '</th><th scope="col">' + tx("colTotal") + '</th><th scope="col">' + tx("colAnswered") + '</th><th scope="col">' + tx("colMissing") + '</th></tr></thead><tbody>' + rows + '</tbody></table></div>' +
      '<div class="exam__dialog-cta"><button type="button" class="btn btn--ghost btn--sm" data-kt-dialog-close>' + tx("backToTest") + '</button><button type="button" class="btn btn--sun btn--sm" data-kt-confirm>' + tx("finish") + '</button></div>');
  }
  function doFinish(timeUp) {
    stopTimer(); if (audio) audio.pause();
    st.done = true; st.reviewOnly = false; st.timeUp = !!timeUp;
    remember(st.ti, st.answers); store.del(RUN + key(st.ti));
    $("[data-kt-timer]").hidden = true; $("[data-kt-finish]").hidden = true;
    closeDialog(); results();
  }
  function askExit() {
    if (st && !st.done) dialog('<h2 id="kt-dlg-t">' + tx("askExit") + '</h2><p>' + tx("exitText") + '</p>' +
      '<div class="exam__dialog-cta"><button type="button" class="btn btn--ghost btn--sm" data-kt-dialog-close>' + tx("backToTest") + '</button><button type="button" class="btn btn--primary btn--sm" data-kt-leave>' + tx("leave") + '</button></div>');
    else closeExam();
  }

  function results() {
    var t = st.t, r = judge(t, st.answers);
    $("[data-kt-where]").textContent = tr("result");
    if (audio) audio.pause();
    var partial = r.secs.filter(function (s) { return s.known < s.count && !s.wait; }).map(function (s) { return s.name; });
    var waiting = r.secs.filter(function (s) { return s.wait; }).map(function (s) { return s.name; });
    var secs = r.secs.map(function (s) {
      var p = s.full ? Math.round(s.score / s.count * 100) : (s.known ? Math.round(s.score / s.known * 100) : 0);
      var badge = !s.full ? '<span class="kt-verdict is-na">' + tx("notJudged") + '</span>'
        : s.ok ? '<span class="kt-verdict is-pass">' + ico("i-check") + tx("pass") + '</span>' : '<span class="kt-verdict is-fail">' + tx("fail") + '</span>';
      return '<div class="kt-sec"><div class="kt-sec__h"><h3>' + esc(s.name) + '</h3>' + badge + '</div>' +
        '<p class="kt-sec__v"><b>' + s.score + '</b> / ' + (s.full ? s.count : s.known) + '</p>' +
        '<i class="kt-bar' + (s.full ? ' kt-bar--t' : '') + '" style="--p:' + p + '%;--t:' + PCT + '%"></i><p class="kt-sec__m">' +
        (s.full ? tx("needLine", { need: s.need, count: s.count, p: PCT, mine: p })
                : s.wait ? tx("waitLine", { p: PCT })
                : tx("partialLine", { known: s.known, count: s.count, p: PCT })) +
        ' · ' + tx("answeredLine", { n: s.answered, count: s.count }) + '</p></div>';
    }).join("");
    var failed = r.secs.filter(function (s) { return s.full && !s.ok; }).map(function (s) { return s.name; });
    var verdict = r.allFull
      ? (r.allPass ? '<p class="kt-result__verdict is-pass">' + ico("i-check-circle") + tx("congrats", { p: PCT }) + '</p>'
                   : '<p class="kt-result__verdict is-fail">' + ico("i-info") + tx("notYet", { sections: failed.join(", "), p: PCT }) + '</p>')
      : '<p class="kt-result__verdict is-na">' + ico("i-info") + r.secs.map(function (s) {
          return esc(s.name) + ' — ' + (!s.full ? tx(s.wait ? "waitShort" : "notJudgedShort") : s.ok ? tx("passed") : tx("failed"));
        }).join(' · ') + '</p>';
    var maps = t.sections.map(function (s, si) {
      return '<div class="kt-map"><h3>' + esc(s.name) + '</h3><div class="qgrid qgrid--wrap">' + s.questions.map(function (q, i) {
        var a = st.answers[si][i];
        return '<button type="button" class="qn ' + (!graded(q) ? "is-unk" : a === q.ans ? "is-right" : a ? "is-wrong" : "is-miss") + '" data-kt-go="' + si + ',' + i + '" aria-label="' + tx("questionN", { n: i + 1 }) + '">' + (i + 1) + '</button>';
      }).join("") + '</div></div>';
    }).join("");
    $("[data-kt-body]").innerHTML = '<div class="exam__wrap"><section class="kt-result" aria-labelledby="kt-res-t">' +
      (st.timeUp ? '<p class="kt-result__flag">' + ico("i-clock") + tx("timeUp") + '</p>' : '') +
      '<div class="kt-result__head"><div><p class="eyebrow">' + tx("result") + ' · ' + esc(t.title) + '</p><h2 id="kt-res-t" class="kt-result__score"><b>' + r.total + '</b><span>/ ' + r.known + ' ' + tx("points") + '</span></h2>' +
      verdict + '<p class="kt-result__note">' + tx("note", { p: PCT, known: r.known, count: r.count }) + (partial.length ? ' ' + tx("notePartial", { sections: partial.join(", ") }) : '') +
        (waiting.length ? ' ' + tx("noteWait", { sections: waiting.join(", ") }) : '') + '</p></div>' +
      '<div class="kt-result__cta"><button type="button" class="btn btn--primary btn--sm" data-kt-go="0,0">' + tx("reviewAnswers") + '</button><button type="button" class="btn btn--ghost btn--sm" data-kt-restart>' + tx("retake") + '</button><button type="button" class="quiet" data-kt-exit>' + tx("toList") + '</button></div></div>' +
      '<div class="kt-result__secs">' + secs + '</div>' +
      '<div class="kt-maps">' + maps + '<p class="kt-legend"><span><i class="qn is-right"></i>' + tx("legendRight") + '</span><span><i class="qn is-wrong"></i>' + tx("legendWrong") + '</span><span><i class="qn is-miss"></i>' + tx("legendMiss") + '</span><span><i class="qn is-unk"></i>' + tx(waiting.length ? "legendWait" : "legendUnknown") + '</span></p></div>' +
      '<div class="kt-result__course"><p><b>' + tx("courseTitle") + '</b> ' + tx("courseText") + '</p><button type="button" class="btn btn--sun btn--sm" data-consult="education:kazakh">' + tx("courseButton") + '</button></div>' +
      '</section></div>';
    ex.scrollTop = 0;
    var h = $("#kt-res-t"); if (h) { h.setAttribute("tabindex", "-1"); h.focus({ preventScroll: true }); }
  }

  ex.addEventListener("click", function (e) {
    var b = e.target.closest("button"); if (!b || !st) return;
    if (b.hasAttribute("data-kt-pick")) pick(+b.getAttribute("data-kt-pick"));
    else if (b.hasAttribute("data-kt-go")) { var p = b.getAttribute("data-kt-go").split(","); go(+p[0], +p[1]); }
    else if (b.hasAttribute("data-kt-next")) next();
    else if (b.hasAttribute("data-kt-prev")) prev();
    else if (b.hasAttribute("data-kt-finish")) askFinish();
    else if (b.hasAttribute("data-kt-confirm")) doFinish(false);
    else if (b.hasAttribute("data-kt-results")) results();
    else if (b.hasAttribute("data-kt-restart")) start(st.ti, "run");
    else if (b.hasAttribute("data-kt-exit")) askExit();
    else if (b.hasAttribute("data-kt-leave")) closeExam();
    else if (b.hasAttribute("data-kt-dialog-close")) closeDialog();
  });
  // capture phase: runs before the site's own Esc handler, and stays out of the way of the consult drawer
  document.addEventListener("keydown", function (e) {
    if (ex.hidden || !st) return;
    var drawer = document.getElementById("consult"); if (drawer && !drawer.hidden) return;
    var dlgOpen = !$("[data-kt-dialog]").hidden;
    if (e.key === "Escape") { e.preventDefault(); e.stopPropagation(); if (dlgOpen) closeDialog(); else askExit(); return; }
    if (dlgOpen || e.ctrlKey || e.metaKey || e.altKey) return;
    var tg = e.target; if (tg && /^(INPUT|TEXTAREA|SELECT|AUDIO)$/.test(tg.tagName)) return;
    var k = String(e.key).toUpperCase(), i = L.indexOf(k); if (i < 0) i = "1234".indexOf(k);
    if (i >= 0 && k.length === 1 && !st.done && $(".qopts")) { e.preventDefault(); pick(i); }
    else if (e.key === "ArrowRight" && $(".qopts")) { e.preventDefault(); next(); }
    else if (e.key === "ArrowLeft" && $(".qopts")) { e.preventDefault(); prev(); }
  }, true);

  renderList();
  var hm = (location.hash || "").match(/^#nusqa-(\d+)$/), card = hm && document.getElementById("nusqa-" + hm[1]);
  if (card) card.scrollIntoView({ block: "center" });
})();
