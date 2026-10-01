#!/usr/bin/env python3
"""One-time import: page blocks from the static prototype.

Reads the built prototype pages (../../prototype/*.html), and writes
  src/KieliWeb/Setup/BlockRegistry.Generated.cs  - block + field definitions (admin labels, types)
  db/seed/blocks.json                            - the prototype texts as initial values

After the first import the C# registry and the database are the source of truth:
add or change fields in BlockRegistry.Generated.cs (and a default in blocks.json) by hand.
Re-running this script overwrites both files.
"""
import html as htmllib
import json
import os
import re
import lxml.html

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
PROTO = os.path.normpath(os.path.join(ROOT, 'prototype'))

PAGES = {'index.html': '/', 'author.html': '/kz/author', 'services.html': '/kz/services', 'service.html': '/kz/service/atazholy',
         'articles.html': '/kz/news', 'article.html': '/kz/news/apostille', 'kazakhstan.html': '/kz/kazakhstan',
         'kaztest.html': '/kz/kaztest'}


def url(h):
    """Prototype link -> site link."""
    if not h or re.match(r'^(https?:|/|#|mailto:|tel:|javascript:)', h):
        return h or ''
    page, _, frag = h.partition('#')
    if page == 'place.html':
        return '/kz/place/' + frag
    if page not in PAGES:
        return h
    return PAGES[page] + ('#' + frag if frag else '')


def load(name):
    return lxml.html.fromstring(open(os.path.join(PROTO, name), encoding='utf-8').read())


DOCS = {n: load(n) for n in ['index.html', 'author.html', 'services.html', 'service.html', 'articles.html', 'article.html', 'kazakhstan.html', 'place.html', 'kaztest.html']}


def css(sel):
    """Tiny CSS -> XPath: 'section.ahero p.eyebrow', 'div#consult h2', 'a > b'."""
    parts, xp, child = sel.split(), '', False
    for part in parts:
        if part == '>':
            child = True
            continue
        m = re.match(r'^([a-z0-9]*)((?:[.#][\w-]+)*)$', part)
        tag, rest = m.group(1) or '*', m.group(2)
        conds = []
        for kind, val in re.findall(r'([.#])([\w-]+)', rest):
            if kind == '.':
                conds.append("contains(concat(' ',normalize-space(@class),' '),' %s ')" % val)
            else:
                conds.append("@id='%s'" % val)
        xp += ('/' if child else ('.//' if not xp else '//')) + tag + ''.join('[%s]' % c for c in conds)
        child = False
    return xp


def one(doc, sel):
    found = doc.xpath(css(sel)) if not sel.startswith('/') and not sel.startswith('.') else doc.xpath(sel)
    if not found:
        raise SystemExit('not found: ' + sel)
    return found[0]


def many(doc, sel):
    return doc.xpath(css(sel))


def text(el):
    return re.sub(r'\s+', ' ', el.text_content()).strip()


def lines(el):
    s = lxml.html.tostring(el, encoding='unicode', with_tail=False)
    s = re.sub(r'^<[^>]+>|</[^>]+>$', '', s)
    s = re.sub(r'<br\s*/?>', '\n', s)
    return htmllib.unescape(re.sub(r'<[^>]+>', '', s)).strip()


def inner(el):
    s = (el.text or '')
    for c in el:
        s += lxml.html.tostring(c, encoding='unicode', with_tail=True)
    s = re.sub(r'href="([^"]*)"', lambda m: 'href="%s"' % url(m.group(1)), s)
    return re.sub(r'\s+', ' ', s).strip()


def icon(el):
    u = el.xpath('.//*[local-name()="use"]/@href')
    return u[0].lstrip('#') if u else ''


def img(el):
    s = el.get('src', '')
    s = re.sub(r'\?v=[^"]*$', '', s)
    if s.startswith('assets/img/'):
        return '/kieli/img/' + s[len('assets/img/'):]
    return s


# ---------------------------------------------------------------------------
# field constructors: (name, label, kind[, extra])
def F(name, label, kind='text', **kw):
    d = {'name': name, 'label': label, 'kind': kind}
    d.update(kw)
    return d


def LIST(name, label, *fields):
    return F(name, label, 'list', fields=list(fields))


BLOCKS = []


def row_ids(fields, values):
    """Every list row gets a stable _id (r1, r2…): translations of a row follow it when rows move."""
    for f in fields:
        if f['kind'] == 'list' and isinstance(values.get(f['name']), list):
            for i, row in enumerate(values[f['name']]):
                row_ids(f['fields'], row)
                row['_id'] = 'r%d' % (i + 1)


def block(key, page, title, fields, values, help=None):
    names = {f['name'] for f in fields}
    missing = [k for k in values if k not in names]
    if missing:
        raise SystemExit(f'{key}: values without field: {missing}')
    row_ids(fields, values)
    BLOCKS.append({'key': key, 'page': page, 'title': title, 'help': help, 'fields': fields, 'values': values})


def seo(key, page, title, description, image=''):
    block('seo.' + key, page, 'SEO: браузер тақырыбы мен сипаттама', [
        F('title', 'Бет тақырыбы (браузерде)'), F('description', 'Сипаттама (іздеу жүйелері үшін)', 'textarea'),
        F('image', 'Бөлісу суреті (og:image)', 'image')], {'title': title, 'description': description, 'image': image})


def cross(key, page, doc, sel='div.cross'):
    c = one(doc, sel)
    btns = many(c, 'div.cross__cta a') + many(c, 'div.cross__cta button')
    a = many(c, 'div.cross__cta a')
    block(key, page, 'Төменгі шақыру жолағы', [
        F('image', 'Фон суреті', 'image'), F('eyebrow', 'Үстіңгі жол'), F('title', 'Тақырып'), F('text', 'Мәтін', 'textarea'),
        F('button', 'Негізгі батырма («Кеңес алу» терезесін ашады)'), F('serviceKey', 'Терезеде таңдалып тұратын қызмет (бос болса — жалпы кеңес)', 'service'),
        F('linkText', 'Екінші батырма мәтіні'), F('linkUrl', 'Екінші батырма сілтемесі', 'url')], {
        'image': img(one(c, 'div.cross__bg img')), 'eyebrow': text(one(c, 'p.eyebrow')), 'title': text(one(c, 'h2')),
        'text': text(c.xpath('./div[2]/p[not(@class)]')[0]), 'button': text(one(c, 'div.cross__cta button')),
        'serviceKey': one(c, 'div.cross__cta button').get('data-consult') or '',
        'linkText': text(a[0]) if a else '', 'linkUrl': url(a[0].get('href')) if a else ''})


def faq(key, page, doc, sec_sel):
    sec = one(doc, sec_sel)
    lead = many(sec, 'p.lead')
    items = [{'q': text(one(d, 'summary')), 'a': inner(one(d, 'div.faq__a'))} for d in many(sec, 'details')]
    block(key, page, 'Сұрақ-жауап', [
        F('eyebrow', 'Үстіңгі жол'), F('title', 'Тақырып'), F('lead', 'Түсініктеме'),
        LIST('items', 'Сұрақтар', F('q', 'Сұрақ'), F('a', 'Жауап (HTML рұқсат)', 'textarea'))], {
        'eyebrow': text(one(sec, 'p.eyebrow')), 'title': text(one(sec, 'h2')), 'lead': text(lead[0]) if lead else '', 'items': items})


def sechead(el):
    aside = many(el, 'div.sec-head__aside p')
    link = many(el, 'div.sec-head__aside a')
    return {'eyebrow': text(one(el, 'p.eyebrow')), 'title': text(one(el, 'h2')), 'aside': text(aside[0]) if aside else '',
            'linkText': text(link[0]) if link else '', 'linkUrl': url(link[0].get('href')) if link else ''}


SECHEAD = [F('eyebrow', 'Үстіңгі жол'), F('title', 'Тақырып'), F('aside', 'Оң жақтағы түсініктеме', 'textarea')]
SECHEAD_LINK = SECHEAD + [F('linkText', 'Сілтеме мәтіні'), F('linkUrl', 'Сілтеме', 'url')]

# ===========================================================================
# Site-wide
d = DOCS['index.html']
block('site.brand', 'site', 'Логотип және атау', [
    F('name', 'Сайт атауы (логотип жанында)', notranslate=True), F('logo', 'Логотип (SVG не PNG, шаршы)', 'image'),
    F('footerText', 'Футердегі қысқа сипаттама', 'textarea')], {
    'name': text(one(d, 'header.site-header span.brand__word')), 'logo': '/kieli/img/logo.svg',
    'footerText': text(one(d, 'div.footer-brand > p'))})

nav = [{'label': text(a), 'url': url(a.get('href')), 'key': a.get('data-nav', '')} for a in many(d, 'nav.nav > a')]
mnotes = {url(a.get('href')): text(one(a, 'small')) for a in many(d, 'nav.mnav__links > a')}
for n in nav:
    n['note'] = mnotes.get(n['url'], '')
block('site.nav', 'site', 'Негізгі мәзір', [
    LIST('items', 'Мәзір тармақтары', F('label', 'Атауы'), F('url', 'Сілтеме', 'url'),
         F('note', 'Мобильді мәзірдегі қосымша жазу'), F('key', 'Белгі (белсенді бетті көрсету үшін: author, services, kaztest, articles, kazakhstan)', notranslate=True))],
    {'items': nav})

wa = one(d, 'section#contact a.channel')
block('site.contact', 'site', 'Байланыс деректері', [
    F('phone', 'Телефон (көрінетін түрі)', notranslate=True), F('whatsapp', 'WhatsApp нөмірі (тек цифр, мыс. 77000000000)', notranslate=True),
    F('telegram', 'Telegram (@ белгісінсіз)', notranslate=True), F('wechat', 'WeChat ID', notranslate=True), F('wechatQr', 'WeChat QR-коды', 'image'),
    F('email', 'Электрондық пошта', notranslate=True), F('address', 'Кеңсе мекенжайы'), F('hours', 'Жұмыс уақыты (футерде)'),
    F('timeNote', 'Уақыт айырмасы туралы жазу'), F('legalName', 'Заңды атауы (футер)'), F('bin', 'БСН', notranslate=True)], {
    'phone': '+7 700 000 00 00', 'whatsapp': re.sub(r'\D', '', wa.get('href')), 'telegram': 'kieli_kz', 'wechat': 'kieli_kz', 'wechatQr': '',
    'email': 'info@kieli.kz', 'address': 'Астана қ., Айнакөл көшесі, 66', 'hours': 'Дс–Жм 10:00–19:00, Сб 10:00–15:00',
    'timeNote': text(many(d, 'p.contact__meta > span')[1]),
    'legalName': '«BASTAU LINE» жауапкершілігі шектеулі серіктестігі', 'bin': ''},
    help='Бұл деректер тақырыпта, футерде, «Кеңес алу» терезесінде және байланыс бөлімінде бірден өзгереді.')

soc = []
for a in many(d, 'div.social > a'):
    soc.append({'icon': icon(a), 'label': a.get('aria-label', ''), 'url': a.get('href')})
block('site.social', 'site', 'Әлеуметтік желілер', [
    LIST('items', 'Желілер', F('icon', 'Белгіше', 'icon'), F('label', 'Атауы', notranslate=True), F('url', 'Сілтеме', 'url'))], {'items': soc},
    help='WeChat батырмасы әрқашан «Кеңес алу» терезесін ашады, оны мұнда қосудың қажеті жоқ.')

cols = many(d, 'nav.footer-cols > div')
def col_links(c):
    return [{'label': text(a), 'url': url(a.get('href'))} for a in many(c, 'a') if not a.get('href', '').startswith(('tel:', 'mailto:'))]
fb = many(d, 'div.footer-bottom p')
block('site.footer', 'site', 'Футер', [
    F('col1Title', '1-баған тақырыбы'), LIST('col1Links', '1-баған сілтемелері', F('label', 'Атауы'), F('url', 'Сілтеме', 'url')),
    F('col2Title', '2-баған тақырыбы'), LIST('col2Links', '2-баған сілтемелері', F('label', 'Атауы'), F('url', 'Сілтеме', 'url')),
    F('col3Title', 'Байланыс бағанының тақырыбы'), F('copyright', 'Төменгі жол (© жылы өзі қойылады)', 'html'), F('credit', 'Әзірлеуші', 'html')], {
    'col1Title': text(one(cols[0], 'h3')), 'col1Links': col_links(cols[0]),
    'col2Title': text(one(cols[1], 'h3')), 'col2Links': col_links(cols[1]), 'col3Title': text(one(cols[2], 'h3')),
    'copyright': re.sub(r'^©\s*<span data-year(?:="")?>\d{4}</span>\s*', '', inner(fb[0])), 'credit': inner(fb[1])})

dr = one(d, 'div#consult')
block('site.consult', 'site', '«Кеңес алу» терезесі', [
    F('title', 'Тақырып'), F('lead', 'Түсініктеме', 'html'), F('pickHint', 'Өтінім коды туралы кеңес'),
    F('wechatHint', 'WeChat туралы ескерту', 'html'), F('separator', 'Бөлгіш жазу'),
    F('msgPlaceholder', '«Жағдайыңыз» өрісіндегі мысал', 'textarea'), F('consent', 'Келісім мәтіні'), F('submit', 'Жіберу батырмасы'),
    F('doneText', 'Жіберілгеннен кейінгі мәтін', 'textarea'), F('fab', 'Экран бұрышындағы батырма')], {
    'title': text(one(dr, 'h2')), 'lead': inner(one(dr, 'div.drawer__head p')), 'pickHint': text(one(dr, 'span.pick__hint')),
    'wechatHint': inner(one(dr, 'div.wechat-qr p')), 'separator': text(one(dr, 'div.drawer__sep')),
    'msgPlaceholder': one(dr, 'textarea').get('placeholder'), 'consent': text(one(dr, 'label.check')),
    'submit': text(one(dr, 'form button')), 'doneText': text(many(dr, 'div.form-done p')[0]), 'fab': text(one(d, 'button.fab'))})

# ===========================================================================
# Home
seo('home', 'home', 'BASTAU LINE · Омар Бекмұрат',
    'Омар Бекмұрат және «BASTAU LINE» ЖШС: Қазақстанға көшу, оқу, құжат аудармасы, нотариат, апостиль — қазақ, қытай және орыс тілдерінде. Астана, Айнакөл көшесі, 66.',
    'https://kieli.kz/uploads/thumbnail/20200830123414870_big.jpg')
h = one(d, 'section.ahero')
cta = one(h, 'div.ahero__cta')
block('home.hero', 'home', 'Бірінші экран (автор)', [
    F('eyebrow', 'Үстіңгі жол'), F('name', 'Аты-жөні (әр жол жаңа жолда)', 'lines'), F('role', 'Лауазымы'),
    F('slogan', 'Ұран'), F('lead', 'Қысқа мәтін', 'textarea'), F('ctaConsult', '«Кеңес алу» батырмасы'),
    F('ctaServices', 'Қызметтер батырмасы'), F('ctaAuthor', 'Автор сілтемесі'),
    F('photo', 'Фото (фоны мөлдір PNG/WebP, 4:5)', 'image'), F('photoAlt', 'Фото сипаттамасы'), F('city', 'Фондағы қала суреті', 'image')], {
    'eyebrow': text(one(h, 'p.eyebrow')), 'name': lines(one(h, 'h1')), 'role': text(one(h, 'p.ahero__role')),
    'slogan': text(one(h, 'p.ahero__slogan')), 'lead': text(one(h, 'p.ahero__lead')), 'ctaConsult': text(one(cta, 'button')),
    'ctaServices': text(one(cta, 'a.btn')), 'ctaAuthor': text(one(cta, 'a.link-arrow')),
    'photo': '/kieli/img/omar-cutout.webp', 'photoAlt': one(h, 'img.ahero__photo').get('alt'), 'city': img(one(h, 'img.ahero__city'))})

strip = [{'icon': icon(it), 'title': inner(one(it, 'b')), 'text': text(one(it, 'span'))} for it in many(d, 'div.strip__item')]
block('home.strip', 'home', 'Үш артықшылық жолағы', [
    LIST('items', 'Элементтер', F('icon', 'Белгіше', 'icon'), F('title', 'Тақырып (HTML рұқсат)'), F('text', 'Түсініктеме'))], {'items': strip})

hp = one(d, 'section#help')
block('home.help', 'home', '«Сізге қандай көмек керек?» тақырыбы', [
    F('eyebrow', 'Үстіңгі жол'), F('title', 'Тақырып'), F('lead', 'Түсініктеме', 'textarea'), F('foot', 'Төменгі жол', 'html')], {
    'eyebrow': text(one(hp, 'p.eyebrow')), 'title': text(one(hp, 'h2')), 'lead': text(hp.xpath('.//header/p[not(@class)]')[0]),
    'foot': inner(one(hp, 'p.help-foot'))}, help='Қызмет бөлімдері мен түрлері «Қызметтер» мәзірінде өзгертіледі.')

kp = one(d, 'div.ktpromo')
mock = one(kp, 'div.ktmock')
block('home.kaztest', 'home', 'ҚАЗТЕСТ жарнама блогы', [
    F('eyebrow', 'Үстіңгі жол'), F('title', 'Тақырып'), F('lead', 'Мәтін', 'textarea'),
    LIST('facts', 'Сандар', F('value', 'Мән'), F('label', 'Жазу')), F('ctaStart', 'Негізгі батырма'), F('ctaMore', 'Сілтеме мәтіні'),
    F('mockVariant', 'Үлгі: нұсқа', notranslate=True), F('mockSection', 'Үлгі: бөлім', notranslate=True), F('mockTimer', 'Үлгі: таймер', notranslate=True),
    F('mockKicker', 'Үлгі: тапсырма', notranslate=True), F('mockQuestion', 'Үлгі: сұрақ', notranslate=True),
    LIST('mockOptions', 'Үлгі: жауаптар', F('text', 'Жауап', notranslate=True)), F('mockSelected', 'Үлгі: таңдалған жауап (1–4)', 'number')], {
    'eyebrow': text(one(kp, 'p.eyebrow')), 'title': text(one(kp, 'h2')), 'lead': text(one(kp, 'p.ktpromo__lead')),
    'facts': [{'value': text(one(li, 'b')), 'label': text(li)[len(text(one(li, 'b'))):].strip()} for li in many(kp, 'ul.ktpromo__facts > li')],
    'ctaStart': text(one(kp, 'div.ktpromo__cta a.btn')), 'ctaMore': text(one(kp, 'div.ktpromo__cta a.link-arrow')),
    'mockVariant': text(one(mock, 'div.ktmock__bar b')), 'mockSection': text(many(mock, 'div.ktmock__bar span')[0]),
    'mockTimer': text(one(mock, 'span.ktmock__timer')), 'mockKicker': text(one(mock, 'p.ktmock__k')), 'mockQuestion': text(one(mock, 'p.ktmock__q')),
    'mockOptions': [{'text': text(li)[1:].strip()} for li in many(mock, 'ul.ktmock__opts > li')], 'mockSelected': 4})

rt = d.xpath("//section[@aria-labelledby='route-title']")[0]
block('home.route', 'home', '«Түсінікті қадамдар»', SECHEAD + [LIST('steps', 'Қадамдар', F('title', 'Атауы'), F('text', 'Түсініктеме', 'textarea'))],
      dict({k: v for k, v in sechead(rt).items() if k in ('eyebrow', 'title', 'aside')},
           steps=[{'title': text(one(li, 'h3')), 'text': text(one(li, 'p'))} for li in many(rt, 'li.route__step')]))

ps = d.xpath("//section[@aria-labelledby='posts-title']")[0]
block('home.posts', 'home', 'Жаңалықтар бөлімі', SECHEAD + [F('moreText', '«Толығырақ» батырмасы'), F('moreNote', 'Батырма жанындағы жазу')],
      dict({k: v for k, v in sechead(ps).items() if k in ('eyebrow', 'title', 'aside')},
           moreText=text(one(ps, 'a.btn')), moreNote=text(one(ps, 'div.posts-more > span'))),
      help='Мақалалардың өзі «Жаңалықтар» мәзірінде. Басты бетте «Маңызды» белгісі бар және ең жаңа мақалалар шығады.')

kz = d.xpath("//section[@aria-labelledby='kz-title']")[0]
TABS = {'tabigat': ['kolsai', 'sharyn', 'muztau', 'khantengri', 'alakol'], 'tarih': ['yassawi', 'tanbaly', 'otyrar', 'beket', 'berel'],
        'dastur': ['Күй', 'Киіз үй', 'Айтыс', 'Құсбегілік', 'Наурыз']}
block('home.kazakhstan', 'home', '«Қазақстанды танып-біліңіз» бөлімі', SECHEAD_LINK + [
    LIST('tabs', 'Қойындылар', F('label', 'Атауы'), F('kind', 'Түрі', 'select', options=[['places', 'Нысандар'], ['heritage', 'Мәдени мұра']]),
         F('items', 'Нысандар (әр жолға бір: нысанның сілтеме белгісі не мұраның қазақша атауы)', 'textarea', notranslate=True))],
      dict(sechead(kz), tabs=[{'label': text(b), 'kind': 'heritage' if b.get('data-kt') == 'dastur' else 'places', 'items': '\n'.join(TABS[b.get('data-kt')])}
                              for b in many(kz, 'button.chip')]))

faq('home.faq', 'home', d, "//section[@aria-labelledby='faq-title']")

ct = one(d, 'section#contact')
fc = one(ct, 'div.form-card')
block('home.contact', 'home', 'Байланыс бөлімі (соңғы)', [
    F('image', 'Фон суреті', 'image'), F('eyebrow', 'Үстіңгі жол'), F('title', 'Тақырып'), F('lead', 'Мәтін', 'textarea'),
    F('wechatLabel', 'WeChat жазуы'), F('formTitle', 'Өтінім формасының тақырыбы'), F('formLead', 'Форма түсініктемесі'),
    F('doneText', 'Жіберілгеннен кейінгі мәтін')], {
    'image': img(one(ct, 'div.band__bg img')), 'eyebrow': text(one(ct, 'p.eyebrow')), 'title': text(one(ct, 'h2')),
    'lead': text(one(ct, 'p.contact__lead')), 'wechatLabel': text(one(ct, 'span.channel__k')),
    'formTitle': text(one(fc, 'h3')), 'formLead': text(fc.xpath('./p')[0]), 'doneText': text(many(fc, 'div.form-done p')[0])})

# ===========================================================================
# Author
d = DOCS['author.html']
seo('author', 'author', 'Омар Бекмұрат · BASTAU LINE',
    'Омар Бекмұрат — өнер ғылымдарының магистрі, режиссер-хореограф, қоғам белсендісі және кәсіпкер, «Киелі» жобасының авторы, «BASTAU LINE» ЖШС негізін қалаушы.')
ah = one(d, 'section.author-hero')
block('author.hero', 'author', 'Бірінші экран', [
    F('photo', 'Фото (фоны мөлдір, 4:5)', 'image'), F('eyebrow', 'Үстіңгі жол'), F('name', 'Аты-жөні'), F('role', 'Лауазымы', 'textarea'),
    F('lead', 'Кіріспе', 'textarea'), F('quoteLabel', 'Ұстаным белгісі'), F('quote', 'Ұстаным'),
    LIST('stats', 'Көрсеткіштер', F('label', 'Атауы'), F('value', 'Мәні'), F('small', 'Қосымша жазу')),
    F('ctaConsult', '«Кеңес алу» батырмасы'), F('linkText', 'Екінші батырма'), F('linkUrl', 'Екінші батырма сілтемесі', 'url')], {
    'photo': '/kieli/img/omar-cutout.webp', 'eyebrow': text(one(ah, 'p.eyebrow')), 'name': text(one(ah, 'h1')),
    'role': text(one(ah, 'p.author__role')), 'lead': text(one(ah, 'p.lead')), 'quoteLabel': text(one(ah, 'p.quote-k')),
    'quote': text(ah.xpath(".//blockquote/p[not(@class)]")[0]),
    'stats': [{'label': text(one(x, 'dt')), 'value': (one(x, 'dd').text or '').strip(), 'small': text(many(x, 'small')[0]) if many(x, 'small') else ''} for x in many(ah, 'dl.stats > div')],
    'ctaConsult': text(one(ah, 'div.hero__cta button')), 'linkText': text(one(ah, 'div.hero__cta a')), 'linkUrl': url(one(ah, 'div.hero__cta a').get('href'))})

so = d.xpath("//section[@aria-labelledby='soc-title']")[0]
block('author.social', 'author', 'Қоғамдық жұмыс', [
    F('eyebrow', 'Үстіңгі жол'), F('title', 'Тақырып'), F('lead', 'Кіріспе', 'textarea'),
    LIST('issues', 'Көтеріп жүрген мәселелері', F('icon', 'Белгіше', 'icon'), F('text', 'Мәтін')), F('body', 'Негізгі мәтін', 'html')], {
    'eyebrow': text(one(so, 'p.eyebrow')), 'title': text(one(so, 'h2')), 'lead': text(one(so, 'p.lead')),
    'issues': [{'icon': icon(li), 'text': text(li)} for li in many(so, 'ul.issues > li')], 'body': inner(one(so, 'div.prose'))})

st = d.xpath("//section[@aria-labelledby='art-title']")[0]
bg = many(st, 'div.bill__group')
block('author.stage', 'author', 'Би өнері (қара фон)', [
    F('eyebrow', 'Үстіңгі жол'), F('title', 'Тақырып'), F('lead', 'Мәтін', 'textarea'),
    F('studioLabel', 'Студия белгісі'), F('studioName', 'Студия атауы'), F('studioText', 'Студия туралы', 'textarea'),
    F('groupsTitle', 'Би топтары тақырыбы'), LIST('groups', 'Би топтары', F('name', 'Атауы')), F('groupsNote', 'Топтар туралы', 'textarea'),
    F('projectsTitle', 'Жобалар тақырыбы'), LIST('projects', 'Жобалар', F('name', 'Атауы')), F('projectsNote', 'Жобалар туралы', 'textarea')], {
    'eyebrow': text(one(st, 'p.eyebrow')), 'title': text(one(st, 'h2')), 'lead': text(one(st, 'p.lead')),
    'studioLabel': text(one(st, 'span.studio__k')), 'studioName': text(one(st, 'span.studio__name')), 'studioText': text(one(st, 'div.studio p')),
    'groupsTitle': (one(bg[0], 'h3').text or '').strip(), 'groups': [{'name': text(li)} for li in many(bg[0], 'li')], 'groupsNote': text(one(bg[0], 'p.bill__note')),
    'projectsTitle': (one(bg[1], 'h3').text or '').strip(), 'projects': [{'name': text(li)} for li in many(bg[1], 'li')], 'projectsNote': text(one(bg[1], 'p.bill__note'))})

bz = d.xpath("//section[@aria-labelledby='biz-title']")[0]
cc = one(bz, 'div.company-card')
cps = cc.xpath('./p')
block('author.business', 'author', 'Кәсіпкерлік және компания', [
    F('eyebrow', 'Үстіңгі жол'), F('title', 'Тақырып'), F('lead', 'Мәтін', 'textarea'), F('cardLabel', 'Карточка белгісі'),
    F('cardName', 'Компания атауы'), F('cardSmall', 'Ұйымдық-құқықтық нысаны'), F('cardText', 'Компания туралы', 'textarea'),
    LIST('dirs', 'Қызмет бағыттары', F('name', 'Атауы')), F('cardNote', 'Қорытынды', 'textarea'), F('linkText', 'Сілтеме мәтіні'), F('linkUrl', 'Сілтеме', 'url')], {
    'eyebrow': text(one(bz, 'p.eyebrow')), 'title': text(one(bz, 'h2')), 'lead': text(one(bz, 'p.lead')),
    'cardLabel': text(one(cc, 'span.company-card__k')), 'cardName': (one(cc, 'span.company-card__name').text or '').strip(),
    'cardSmall': text(one(cc, 'span.company-card__name small')), 'cardText': text(cps[0]),
    'dirs': [{'name': text(li)} for li in many(cc, 'ul.dirs > li')], 'cardNote': text(cps[1]),
    'linkText': text(one(cc, 'a.link-arrow')), 'linkUrl': url(one(cc, 'a.link-arrow').get('href'))})

gl = d.xpath("//section[@aria-labelledby='goal-title']")[0]
block('author.goals', 'author', 'Бастамаларының өзегі', [F('eyebrow', 'Үстіңгі жол'), F('title', 'Тақырып'), F('lead', 'Мәтін', 'textarea'),
                                                      LIST('items', 'Мақсаттар', F('text', 'Мәтін'))], {
    'eyebrow': text(one(gl, 'p.eyebrow')), 'title': text(one(gl, 'h2')), 'lead': text(one(gl, 'p.lead')),
    'items': [{'text': text(li)} for li in many(gl, 'ul.goals > li')]})

pj = d.xpath("//section[@aria-labelledby='proj-title']")[0]
block('author.project', 'author', '«Киелі» жобасы', [
    F('image', 'Сурет', 'image'), F('imageAlt', 'Сурет сипаттамасы'), F('caption', 'Сурет астындағы жазу'), F('eyebrow', 'Үстіңгі жол'),
    F('title', 'Тақырып'), F('lead', 'Мәтін', 'textarea'), LIST('bullets', 'Тізім', F('text', 'Мәтін')), F('linkText', 'Сілтеме мәтіні'), F('linkUrl', 'Сілтеме', 'url')], {
    'image': img(one(pj, 'figure img')), 'imageAlt': one(pj, 'figure img').get('alt'), 'caption': text(one(pj, 'figcaption')),
    'eyebrow': text(one(pj, 'p.eyebrow')), 'title': text(one(pj, 'h2')), 'lead': text(one(pj, 'p.lead')),
    'bullets': [{'text': text(li)} for li in many(pj, 'div.prose li')], 'linkText': text(one(pj, 'a.link-arrow')), 'linkUrl': url(one(pj, 'a.link-arrow').get('href'))})

pq = one(d, 'div.path-quote')
block('author.path', 'author', 'Автордың жолы (дәйексөз)', [LIST('lines', 'Жолдар', F('text', 'Мәтін')), F('final', 'Қорытынды жол', 'textarea')], {
    'lines': [{'text': text(p)} for p in pq.xpath('./p[not(@class)]')], 'final': text(one(pq, 'p.final'))})

aa = d.xpath("//section[@aria-labelledby='aa-title']")[0]
block('author.articles', 'author', 'Пайдалы нұсқаулықтар', [F('eyebrow', 'Үстіңгі жол'), F('title', 'Тақырып'), F('linkText', 'Сілтеме мәтіні'),
                                                          F('count', 'Қанша мақала көрсету', 'number')], {
    'eyebrow': text(one(aa, 'p.eyebrow')), 'title': text(one(aa, 'h2')), 'linkText': text(one(aa, 'a.link-arrow')), 'count': 3},
      help='Жаңалықтар бөлімінен автор аты жазылған ең соңғы мақалалар шығады.')
cross('author.cross', 'author', d)

# ===========================================================================
# Services
d = DOCS['services.html']
seo('services', 'services', 'Қызметтер · BASTAU LINE',
    '«BASTAU LINE» ЖШС — Астанадағы халықаралық аударма және құжат рәсімдеу орталығы: аударма, нотариат, апостиль, азаматтық пен ықтиярхат, «Ата жолы» картасы, білім, бизнес және жылжымайтын мүлік.',
    'https://kieli.kz/uploads/thumbnail/20200830123414870_big.jpg')
sh = one(d, 'section.svc-hero')
block('services.hero', 'services', 'Бет басы', [
    F('eyebrow', 'Үстіңгі жол'), F('title', 'Тақырып'), F('lead', 'Мәтін', 'textarea'),
    LIST('trust', 'Сенім белгілері', F('icon', 'Белгіше', 'icon'), F('text', 'Мәтін')), F('provider', 'Қызмет көрсетуші жолы', 'html'),
    F('cta', 'Батырма'), F('ctaNote', 'Батырма астындағы жазу'), F('image', 'Кең сурет', 'image'), F('imageAlt', 'Сурет сипаттамасы')], {
    'eyebrow': text(one(sh, 'p.eyebrow')), 'title': text(one(sh, 'h1')), 'lead': text(one(sh, 'p.lead')),
    'trust': [{'icon': icon(li), 'text': text(li)} for li in many(sh, 'ul.trust > li')], 'provider': inner(one(sh, 'p.provider')),
    'cta': text(one(sh, 'div.service-head__cta button')), 'ctaNote': text(one(sh, 'div.service-head__cta p')),
    'image': img(one(sh, 'figure img')), 'imageAlt': one(sh, 'figure img').get('alt')})
hp = one(d, 'section#help')
block('services.help', 'services', '«Сізге қандай көмек керек?» тақырыбы', [
    F('eyebrow', 'Үстіңгі жол'), F('title', 'Тақырып'), F('lead', 'Түсініктеме', 'textarea'), F('foot', 'Төменгі жол', 'html')], {
    'eyebrow': text(one(hp, 'p.eyebrow')), 'title': text(one(hp, 'h2')), 'lead': text(hp.xpath('.//header/p[not(@class)]')[0]), 'foot': inner(one(hp, 'p.help-foot'))})
co = one(d, 'section#bastau')
block('services.company', 'services', '«BASTAU LINE» ЖШС (қара фон)', [
    F('eyebrow', 'Үстіңгі жол'), F('title', 'Тақырып'), F('tagline', 'Ұран'), F('lead', 'Мәтін', 'textarea'),
    F('bridgeLeft', 'Көпір: сол жақ'), F('bridgeRight', 'Көпір: оң жақ'), F('bridgeLangs', 'Көпір: тілдер', 'html'), F('address', 'Мекенжай'),
    F('whyTitle', '«Неліктен» тақырыбы'), LIST('why', 'Артықшылықтар', F('text', 'Мәтін')), LIST('motto', 'Ұстаным сөздері', F('word', 'Сөз'))], {
    'eyebrow': text(one(co, 'p.eyebrow')), 'title': text(one(co, 'h2')), 'tagline': text(one(co, 'p.company__tag')), 'lead': text(one(co, 'p.lead')),
    'bridgeLeft': text(many(co, 'div.bridge__ends span')[0]), 'bridgeRight': text(many(co, 'div.bridge__ends span')[1]),
    'bridgeLangs': inner(one(co, 'p.bridge__langs')), 'address': text(one(co, 'p.company__addr')), 'whyTitle': text(one(co, 'div.company__why h3')),
    'why': [{'text': text(li)} for li in many(co, 'ul.why > li')],
    'motto': [{'word': w.strip()} for w in text(one(co, 'p.motto')).split('.') if w.strip()]})
cm = d.xpath("//section[@aria-labelledby='cmp-title']")[0]
block('services.compare', 'services', 'Салыстыру кестесі', SECHEAD + [F('table', 'Кесте', 'html'), F('note', 'Кесте астындағы ескерту', 'textarea')],
      dict({k: v for k, v in sechead(cm).items() if k in ('eyebrow', 'title', 'aside')},
           table=lxml.html.tostring(one(cm, 'table'), encoding='unicode').strip(), note=text(one(cm, 'p.table-note'))))
faq('services.faq', 'services', d, "//section[@aria-labelledby='faq-title']")
cross('services.cross', 'services', d)

# ===========================================================================
# News list, article page
d = DOCS['articles.html']
seo('news', 'news', 'Жаңалықтар мен мақалалар · BASTAU LINE',
    'Көші-қон, «Ата жолы», апостиль және оқу туралы жаңалықтар мен нұсқаулықтар, Омар Бекмұраттың жазбалары.')
ph = one(d, 'section.page-head')
block('news.head', 'news', 'Бет басы', [F('eyebrow', 'Үстіңгі жол'), F('title', 'Тақырып'), F('lead', 'Мәтін', 'textarea'),
                                       F('cta', 'Батырма'), F('ctaNote', 'Батырма астындағы жазу'), F('searchPlaceholder', 'Іздеу өрісіндегі мысал'),
                                       F('note', 'Тізім астындағы жазу', 'textarea')], {
    'eyebrow': text(one(ph, 'p.eyebrow')), 'title': text(one(ph, 'h1')), 'lead': text(one(ph, 'p.lead')),
    'cta': text(one(ph, 'div.service-head__cta button')), 'ctaNote': text(one(ph, 'div.service-head__cta p')),
    'searchPlaceholder': one(d, 'input.search__input').get('placeholder'), 'note': ''})
cross('news.cross', 'news', d)

d = DOCS['article.html']
ab = one(d, 'div.author-box')
block('article.author', 'news', 'Мақала: автор блогы', [F('photo', 'Автор фотосы (шаршы)', 'image'), F('name', 'Аты-жөні'), F('org', 'Ұйым'),
                                                       F('bio', 'Қысқа өмірбаяны', 'textarea'), F('linkText', 'Сілтеме мәтіні'), F('linkUrl', 'Сілтеме', 'url')], {
    'photo': '/kieli/img/omar-avatar.webp', 'name': text(one(ab, 'b')), 'org': text(many(d, 'a.byline__who span span')[0]),
    'bio': text(one(ab, 'p')), 'linkText': text(one(ab, 'a.link-arrow')), 'linkUrl': url(one(ab, 'a.link-arrow').get('href'))},
      help='Мақалада «Автор» өрісі толтырылса, осы блок мақала соңында және басында шығады.')
pn = one(d, 'div.panel--night')
block('article.aside', 'news', 'Мақала: оң жақ бағаналар', [F('tocTitle', '«Мазмұны» тақырыбы'), F('ctaEyebrow', 'Шақыру: үстіңгі жол'),
                                                          F('ctaText', 'Шақыру: мәтін', 'textarea'), F('ctaButton', 'Шақыру: батырма'),
                                                          F('shareTitle', '«Бөлісу» тақырыбы'), F('moreEyebrow', '«Тағы оқыңыз»: үстіңгі жол'), F('moreTitle', '«Тағы оқыңыз»: тақырып')], {
    'tocTitle': text(one(d, 'aside.aside div.panel h3')), 'ctaEyebrow': text(one(pn, 'p.eyebrow')), 'ctaText': text(pn.xpath('./p[not(@class)]')[0]),
    'ctaButton': text(one(pn, 'button')), 'shareTitle': text(many(d, 'aside.aside div.panel h3')[1]),
    'moreEyebrow': text(one(d, "//section[@aria-labelledby='more-title']//p[contains(@class,'eyebrow')]")), 'moreTitle': text(one(d, 'h2#more-title'))})

# ===========================================================================
# Kazakhstan, place
d = DOCS['kazakhstan.html']
seo('kazakhstan', 'kazakhstan', 'Қазақстанның киелі жерлері · BASTAU LINE',
    '17 өңірдегі 187 табиғи, тарихи және рухани нысан: карта, санаттар, ЮНЕСКО тізіміндегі мұралар.', 'https://kieli.kz/uploads/thumbnail/20200225102440677_big.jpg')
kh = one(d, 'section.page-head')
block('kazakhstan.head', 'kazakhstan', 'Бет басы', [F('eyebrow', 'Үстіңгі жол'), F('title', 'Тақырып'), F('lead', 'Мәтін', 'textarea'),
                                                   LIST('figures', 'Сандар', F('value', 'Мән'), F('label', 'Жазу')),
                                                   F('epigraph', 'Эпиграф', 'textarea'), F('epigraphCite', 'Эпиграф авторы')], {
    'eyebrow': text(one(kh, 'p.eyebrow')), 'title': text(one(kh, 'h1')), 'lead': text(one(kh, 'p.lead')),
    'figures': [{'value': text(one(x, 'b')), 'label': text(one(x, 'span'))} for x in many(kh, 'div.figures > div')],
    'epigraph': text(one(kh, 'blockquote')), 'epigraphCite': text(one(kh, 'cite'))})
at = one(d, 'section#atlas')
block('kazakhstan.atlas', 'kazakhstan', 'Карта бөлімі', SECHEAD + [F('allRegions', '«Барлық өңір» батырмасы')],
      dict({k: v for k, v in sechead(at).items() if k in ('eyebrow', 'title', 'aside')}, allRegions=text(one(at, 'button.regions__all span'))))
mu = one(d, 'section#mura')
block('kazakhstan.heritage', 'kazakhstan', 'Мәдени мұра бөлімі', SECHEAD, {k: v for k, v in sechead(mu).items() if k in ('eyebrow', 'title', 'aside')})
cross('kazakhstan.cross', 'kazakhstan', d)

d = DOCS['place.html']
pa = one(d, 'aside.aside')
pnl = many(pa, 'div.panel')
block('place.aside', 'kazakhstan', 'Нысан беті: оң жақ бағаналар', [
    F('aboutTitle', '«Нысан туралы» тақырыбы'), F('panoText', '360° батырмасы'), F('shareTitle', '«Бөлісу» тақырыбы'), F('shareNote', 'WeChat туралы ескерту'),
    F('ctaEyebrow', 'Шақыру: үстіңгі жол'), F('ctaText', 'Шақыру: мәтін', 'textarea'), F('ctaButton', 'Шақыру: батырма'),
    F('relatedEyebrow', 'Ұқсас нысандар: үстіңгі жол'), F('relatedTitle', 'Ұқсас нысандар: тақырып'), F('relatedLink', 'Ұқсас нысандар: сілтеме мәтіні'),
    F('panoNote', '360° терезесіндегі ескерту', 'textarea')], {
    'aboutTitle': text(one(pnl[0], 'h3')), 'panoText': '360° панорама', 'shareTitle': text(one(pnl[1], 'h3')), 'shareNote': text(one(pnl[1], 'p.panel__note')),
    'ctaEyebrow': text(one(pnl[2], 'p.eyebrow')), 'ctaText': text(pnl[2].xpath('./p[not(@class)]')[0]), 'ctaButton': text(one(pnl[2], 'button')),
    'relatedEyebrow': text(one(d, "//section[@aria-labelledby='p-related-t']//p[contains(@class,'eyebrow')]")), 'relatedTitle': text(one(d, 'h2#p-related-t')),
    'relatedLink': 'Барлық нысандар', 'panoNote': text(one(DOCS['index.html'], 'p.pano__note'))})

# ===========================================================================
# ҚАЗТЕСТ
d = DOCS['kaztest.html']
seo('kaztest', 'kaztest', 'ҚАЗТЕСТ байқау тестілеуі · BASTAU LINE',
    'ҚАЗТЕСТ жүйесі бойынша тегін байқау тестілеуі: тыңдалым мен оқылым, ресми емтихан форматында, уақыт өлшеумен. Тұрақты тұруға рұқсат пен қандас мәртебесіне дайындық.')
kt = one(d, 'section.kt-head')
block('kaztest.head', 'kaztest', 'Бет басы', [
    F('eyebrow', 'Үстіңгі жол'), F('title', 'Тақырып'), F('lead', 'Мәтін', 'textarea'),
    LIST('facts', 'Белгілер', F('icon', 'Белгіше', 'icon'), F('title', 'Тақырып'), F('text', 'Жазу')),
    F('ctaChoose', 'Негізгі батырма'), F('ctaCourse', 'Курс батырмасы'), F('ctaCourseService', 'Курс батырмасы ашатын қызмет', 'service'), F('ctaNote', 'Батырма астындағы жазу')], {
    'eyebrow': text(one(kt, 'p.eyebrow')), 'title': text(one(kt, 'h1')), 'lead': text(one(kt, 'p.lead')),
    'facts': [{'icon': icon(li), 'title': text(one(li, 'b')), 'text': text(one(li, 'span'))} for li in many(kt, 'ul.kt-facts > li')],
    'ctaChoose': text(one(kt, 'div.service-head__cta a')), 'ctaCourse': text(one(kt, 'div.service-head__cta button')),
    'ctaCourseService': one(kt, 'div.service-head__cta button').get('data-consult'), 'ctaNote': text(one(kt, 'div.service-head__cta p'))})
lw = one(d, 'div.kt-law')
block('kaztest.law', 'kaztest', '2026 жылғы талап туралы блок', [F('eyebrow', 'Үстіңгі жол'), F('title', 'Тақырып'), F('text', 'Мәтін', 'textarea'),
                                                                   F('sources', 'Дереккөздер жолы', 'html')], {
    'eyebrow': text(one(lw, 'p.eyebrow')), 'title': text(one(lw, 'h2')), 'text': text(lw.xpath('.//div[contains(@class,"kt-law__body")]/p[not(@class)]')[0]),
    'sources': inner(one(lw, 'p.kt-law__src'))})
nq = one(d, 'section#nusqalar')
block('kaztest.list', 'kaztest', 'Нұсқалар бөлімі және тест баптаулары', SECHEAD + [
    F('note', 'Тізім астындағы жазу', 'textarea'), F('minutes', 'Тест уақыты (минут)', 'number'), F('passPercent', 'Өту шегі, әр бөлімнен (%)', 'number')],
      dict({k: v for k, v in sechead(nq).items() if k in ('eyebrow', 'title', 'aside')}, note=text(one(nq, 'p.help-foot')), minutes=70, passPercent=70),
      help='Сұрақтар мен нұсқалар «ҚАЗТЕСТ» мәзірінде. Уақыт пен өту шегі барлық нұсқаға ортақ.')
hw = d.xpath("//section[@aria-labelledby='how-title']")[0]
block('kaztest.how', 'kaztest', '«Қалай өтеді»', SECHEAD + [LIST('steps', 'Қадамдар', F('title', 'Атауы'), F('text', 'Түсініктеме', 'textarea'))],
      dict({k: v for k, v in sechead(hw).items() if k in ('eyebrow', 'title', 'aside')},
           steps=[{'title': text(one(li, 'h3')), 'text': text(one(li, 'p'))} for li in many(hw, 'li.route__step')]))
faq('kaztest.faq', 'kaztest', d, "//section[@aria-labelledby='kt-faq-title']")
cross('kaztest.cross', 'kaztest', d)

# ===========================================================================
# Service detail pages (servicepage.dataJson) — one schema for every page, «Ата жолы» as the first
d = DOCS['service.html']
sp_head = one(d, 'section.page-head')
crumbs = many(sp_head, 'ol.crumbs a')
SP_FIELDS = [
    F('eyebrow', 'Үстіңгі жол'), F('lead', 'Кіріспе', 'textarea'), F('crumbText', 'Жолсілтемедегі бөлім атауы'), F('crumbUrl', 'Бөлім сілтемесі', 'url'),
    F('cta', 'Негізгі батырма'), F('serviceKey', 'Батырма ашатын қызмет', 'service'), F('ctaNote', 'Батырма астындағы жазу'),
    LIST('keyfacts', 'Негізгі сандар', F('value', 'Мән'), F('text', 'Түсініктеме')),
    F('benefitsEyebrow', 'Артықшылықтар: үстіңгі жол'), F('benefitsTitle', 'Артықшылықтар: тақырып'), F('benefitsLead', 'Артықшылықтар: мәтін', 'textarea'),
    LIST('benefits', 'Артықшылықтар мен шектеулер', F('icon', 'Белгіше', 'icon'), F('title', 'Тақырып'), F('text', 'Мәтін', 'textarea'), F('negative', 'Шектеу (қызыл)', 'bool')),
    F('whoEyebrow', '«Кімге»: үстіңгі жол'), F('whoTitle', '«Кімге»: тақырып'), F('whoAside', '«Кімге»: түсініктеме', 'textarea'),
    LIST('who', 'Санаттар', F('label', 'Белгі (мыс. 1-санат)'), F('icon', 'Белгіше', 'icon'), F('title', 'Тақырып'), F('text', 'Мәтін', 'textarea')),
    F('stepsEyebrow', 'Қадамдар: үстіңгі жол'), F('stepsTitle', 'Қадамдар: тақырып'), F('stepsAside', 'Қадамдар: түсініктеме', 'textarea'),
    LIST('steps', 'Қадамдар', F('title', 'Атауы'), F('text', 'Мәтін', 'textarea'), F('time', 'Қысқа белгі')),
    F('disclaimer', 'Ескерту (қадамдар астында)', 'textarea'),
    F('docsEyebrow', 'Құжаттар: үстіңгі жол'), F('docsTitle', 'Құжаттар: тақырып'), F('docsLead', 'Құжаттар: түсініктеме', 'textarea'),
    LIST('docs', 'Құжаттар тізімі (келушінің белгілері өз құрылғысында сақталады)', F('title', 'Құжат'), F('note', 'Түсініктеме')),
    F('panelTitle', 'Дайындық панелінің тақырыбы'), F('tipTitle', 'Кеңес: тақырып'), F('tipText', 'Кеңес: мәтін', 'textarea'),
    F('tipLinkText', 'Кеңес: сілтеме мәтіні'), F('tipLinkUrl', 'Кеңес: сілтеме', 'url'),
    F('faqTitle', 'Сұрақ-жауап тақырыбы'), LIST('faq', 'Сұрақтар', F('q', 'Сұрақ'), F('a', 'Жауап (HTML рұқсат)', 'textarea')),
    F('relatedEyebrow', 'Ұқсас қызметтер: үстіңгі жол'), F('relatedTitle', 'Ұқсас қызметтер: тақырып'),
    LIST('related', 'Ұқсас қызметтер', F('kicker', 'Белгі'), F('title', 'Тақырып'), F('text', 'Мәтін', 'textarea'), F('url', 'Сілтеме', 'url')),
]
ben = d.xpath("//section[@aria-labelledby='ben-title']")[0]
who = d.xpath("//section[@aria-labelledby='who-title']")[0]
stp = d.xpath("//section[@aria-labelledby='steps-title']")[0]
dcs = d.xpath("//section[@aria-labelledby='docs-title']")[0]
fq = d.xpath("//section[@aria-labelledby='faq-title']")[0]
rel = d.xpath("//section[@aria-labelledby='rel-title']")[0]
tip = one(dcs, 'div.callout')
SP_VALUES = {
    'eyebrow': text(one(sp_head, 'p.eyebrow')), 'lead': text(one(sp_head, 'p.lead')), 'crumbText': text(crumbs[-1]), 'crumbUrl': url(crumbs[-1].get('href')),
    'cta': text(one(sp_head, 'div.service-head__cta button')), 'serviceKey': one(sp_head, 'div.service-head__cta button').get('data-consult'),
    'ctaNote': text(one(sp_head, 'div.service-head__cta p')),
    'keyfacts': [{'value': text(one(k, 'b')), 'text': text(one(k, 'span'))} for k in many(sp_head, 'div.keyfact')],
    'benefitsEyebrow': text(one(ben, 'p.eyebrow')), 'benefitsTitle': text(one(ben, 'h2')), 'benefitsLead': text(one(ben, 'p.lead')),
    'benefits': [{'icon': icon(x), 'title': text(one(x, 'b')), 'text': text(one(x, 'p')), 'negative': 'benefit--no' in x.get('class')} for x in many(ben, 'div.benefit')],
    'whoEyebrow': text(one(who, 'p.eyebrow')), 'whoTitle': text(one(who, 'h2')), 'whoAside': text(one(who, 'div.sec-head__aside p')),
    'who': [{'label': text(one(x, 'span.n')), 'icon': icon(x), 'title': text(one(x, 'h3')), 'text': text(one(x, 'p'))} for x in many(who, 'div.who__card')],
    'stepsEyebrow': text(one(stp, 'p.eyebrow')), 'stepsTitle': text(one(stp, 'h2')), 'stepsAside': text(one(stp, 'div.sec-head__aside p')),
    'steps': [{'title': text(one(li, 'h3')), 'text': text(one(li, 'p')), 'time': text(one(li, 'span.route__time'))} for li in many(stp, 'li.route__step')],
    'disclaimer': text(one(stp, 'p.catalog__note')),
    'docsEyebrow': text(one(dcs, 'p.eyebrow')), 'docsTitle': text(one(dcs, 'h2')), 'docsLead': text(one(dcs, 'p.lead')),
    'docs': [{'title': text(one(l, 'b')), 'note': text(one(l, 'small'))} for l in many(dcs, 'div.checklist > label')],
    'panelTitle': text(one(dcs, 'aside div.panel h3')), 'tipTitle': text(one(tip, 'b')), 'tipText': text(one(tip, 'span')),
    'tipLinkText': text(one(tip, 'a')), 'tipLinkUrl': url(one(tip, 'a').get('href')),
    'faqTitle': text(one(fq, 'h2')), 'faq': [{'q': text(one(x, 'summary')), 'a': inner(one(x, 'div.faq__a'))} for x in many(fq, 'details')],
    'relatedEyebrow': text(one(rel, 'p.eyebrow')), 'relatedTitle': text(one(rel, 'h2')),
    'related': [{'kicker': text(one(a, 'span.rel__k')), 'title': text(one(a, 'span.rel__t')), 'text': text(one(a, 'p')), 'url': url(a.get('href'))} for a in many(rel, 'a.rel')],
}
assert not [k for k in SP_VALUES if k not in {f['name'] for f in SP_FIELDS}]
row_ids(SP_FIELDS, SP_VALUES)
json.dump([{'slug': 'atazholy', 'title': text(one(sp_head, 'h1')),
            'seoDescription': 'Шетелде тұратын этникалық қазақтарға арналған «Ата жолы» картасы: кімге беріледі, не береді және «BASTAU LINE» өтініш беруге қалай көмектеседі.',
            'data': SP_VALUES}], open(os.path.join(ROOT, 'db', 'seed', 'servicepages.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)

# ===========================================================================
# output
PAGES_ORDER = [('site', 'Барлық беттер (тақырып, футер, байланыс)'), ('home', 'Басты бет'), ('author', 'Автор'), ('services', 'Қызметтер'),
               ('news', 'Жаңалықтар мен мақалалар'), ('kazakhstan', 'Қазақстан'), ('kaztest', 'ҚАЗТЕСТ')]


def cs(s):
    return json.dumps(s, ensure_ascii=False)


KIND = {'text': 'Text', 'textarea': 'Textarea', 'lines': 'Lines', 'html': 'Html', 'image': 'Image', 'url': 'Url', 'number': 'Number',
        'bool': 'Bool', 'icon': 'Icon', 'select': 'Select', 'service': 'Service', 'list': 'List'}


def field_cs(f, indent):
    args = f'{cs(f["name"])}, {cs(f["label"])}, FieldKind.{KIND[f["kind"]]}'
    inits = []
    if f.get('notranslate'):
        inits.append('NoTranslate = true')
    if f['kind'] == 'list':
        sub = (',\n').join(indent + '\t' + field_cs(x, indent + '\t') for x in f['fields'])
        return f'new FieldDef({args}) {{ Fields = new[]\n{indent}{{\n{sub}\n{indent}}} }}'
    if f['kind'] == 'select':
        opts = ', '.join(f'({cs(v)}, {cs(l)})' for v, l in f['options'])
        inits.append(f'Options = new[] {{ {opts} }}')
    extra = f' {{ {", ".join(inits)} }}' if inits else ''
    return f'new FieldDef({args}){extra}'


out = ['// <auto-generated> by tools/blocks/generate.py from the prototype; edit by hand from now on. </auto-generated>',
       'namespace KieliWeb.Setup;', '', 'public static partial class BlockRegistry', '{',
       '\tpublic static readonly (string Key, string Title)[] Pages =', '\t{']
out += [f'\t\t({cs(k)}, {cs(t)}),' for k, t in PAGES_ORDER]
out += ['\t};', '', '\tprivate static readonly BlockDef[] Defined =', '\t{']
for b in BLOCKS:
    fields = ',\n'.join('\t\t\t' + field_cs(f, '\t\t\t') for f in b['fields'])
    helpv = cs(b['help']) if b['help'] else 'null'
    out.append(f'\t\tnew BlockDef({cs(b["key"])}, {cs(b["page"])}, {cs(b["title"])}, {helpv}, new[]\n\t\t{{\n{fields}\n\t\t}}),')
out += ['\t};', '']
sp = ',\n'.join('\t\t' + field_cs(f, '\t\t') for f in SP_FIELDS)
out += ['\t/// <summary>Sections of a service detail page (servicepage.dataJson).</summary>', '\tpublic static readonly FieldDef[] ServicePageFields =', '\t{', sp, '\t};', '}', '']
open(os.path.join(ROOT, 'src', 'KieliWeb', 'Setup', 'BlockRegistry.Generated.cs'), 'w', encoding='utf-8').write('\n'.join(out))
json.dump({b['key']: b['values'] for b in BLOCKS}, open(os.path.join(ROOT, 'db', 'seed', 'blocks.json'), 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
print(len(BLOCKS), 'blocks')
