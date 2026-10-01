# KieliSite — kieli.kz（BASTAU LINE · Омар Бекмұрат）

kieli.kz 的正式网站：前台是已确认的原型（`prototype/`），后台沿用 NiceGirlSite 的管理框架。
**网站上的所有文字、图片、链接、服务、文章、景点、ҚАЗТЕСТ 题目都可以在后台修改**，保存后前台立即生效。

- .NET 10 MVC + MySQL/MariaDB + Dapper（结构与 NiceGirlSite 相同：MODEL → COMMON → DBHelper → KieliWeb）
- 前台：`src/KieliWeb/Views/Themes/Kieli/`，样式和脚本在 `wwwroot/kieli/`
- 后台：`/kz/admin/login/`（界面为哈萨克语）
- 前台语言：Қазақша（默认，带 Кирилл / Төте 切换）、Русский、简体中文、English、Türkçe（地址 `/`、`/ru`、`/zh-cn`、`/en`、`/tr`）

## 目录

```
KieliSite.sln
db/kieli_schema.sql         所有表（CREATE TABLE IF NOT EXISTS，可重复执行）
db/seed/*.json              首次启动时导入的内容（原型里的全部文字、32 项服务、10 篇文章、18 个景点、3 套 ҚАЗТЕСТ、自动新闻的来源）
src/KieliWeb/Setup/         数据库初始化、页面区块定义、内容缓存、后台表格定义（Cms/）
src/KieliWeb/Controllers/   Home（前台）、Consult（前台提交咨询）、Content/Catalog/Press/Atlas/Exam/Lead（后台）
src/KieliWeb/wwwroot/kieli/img/   Logo、图标、分享图（见「品牌与 Logo」）
src/KieliWeb/wwwroot/kieli/audio/kaztest/   ҚАЗТЕСТ 听力音频（Piper TTS 生成，CC BY 4.0，需署名）
docs/kaztest-audio/         听力录音脚本（男女分角色，给真人重录用）和 Azure 配音用的 SSML
tools/kaztest-audio/        重新生成听力音频的工具（Piper TTS）
tools/blocks/generate.py    一次性工具：从原型 HTML 提取页面区块（已执行过，一般不需要再运行）
prototype/                  已确认的静态原型（设计参照：前台的 HTML 结构和 class 与它保持一致），直接用浏览器打开即可
```

## 连接 kieli_db（服务器上）

真实的数据库连接 **不要写进仓库**。`appsettings.json` 里只有 `REPLACE_ME`，发布（publish）时也不会带上任何 `appsettings*.json`。
在服务器的网站目录里新建 `appsettings.Production.json`：

```json
{
  "Site": {
    "Theme": "Kieli",
    "SiteUrl": "https://kieli.kz",
    "Port": 51850,
    "ConnectionString": "Server=数据库地址;Port=端口;Database=kieli_db;Uid=用户名;Pwd=密码;charset=utf8mb4;",
    "AutoInitDatabase": true,
    "InitialAdmin": { "Email": "你的邮箱", "Password": "第一次登录用的密码" }
  }
}
```

第一次启动时（`AutoInitDatabase: true`）程序会自动：

1. 在 kieli_db 里建全部表；
2. 建后台菜单、两个角色（«Әкімші» 全部权限、«Контент редакторы» 只管内容）和第一个管理员；
3. 导入原型的全部内容。

每一步只补缺的部分，已有的数据不会被覆盖，所以一直保持 `true` 也没问题。
没写 `InitialAdmin.Password` 的话，会随机生成密码并写到 `logs/initial-admin.txt`（只在服务器上）——登录后请在后台右上角的「Профиль」（`/kz/admin/profile`）里改密码，然后删掉这个文件。

> kieli_db 必须是空库（或者是本程序建的库）。如果里面已经有同名但结构不同的表，程序会停止并在日志里列出缺少的字段，不会乱写数据。
> 旧网站用的是另一个库 `kieliqazaqstan_db`，不受影响。

## 本地运行

需要 .NET 10 SDK 和 MySQL 8 / MariaDB 10.4+。建一个空库，然后在 `src/KieliWeb/appsettings.Development.json`（已被 .gitignore 忽略）里写本地连接和 `InitialAdmin`，格式同上。

```bash
dotnet build KieliSite.sln
```

```bash
cd src/KieliWeb && dotnet run --no-launch-profile -e ASPNETCORE_ENVIRONMENT=Development -e ASPNETCORE_URLS=http://127.0.0.1:51850
```

打开 http://127.0.0.1:51850/ （前台）和 http://127.0.0.1:51850/kz/admin/login/ （后台）。

## 部署

和 NiceGirlSite 一样：supervisor 运行 `dotnet KieliWeb.dll`，nginx 反向代理到 `127.0.0.1:51850`。

```bash
dotnet publish src/KieliWeb -c Release -o out
```

把 `out/` 复制到服务器网站目录（例如 `/usr/share/nginx/kieli.kz`），**保留服务器上的 `appsettings.Production.json`**，然后 `supervisorctl restart KieliWeb`（supervisor 里设置 `ASPNETCORE_ENVIRONMENT=Production`，工作目录就是网站目录）。

网站目录里这几样是运行时数据，更新程序时不要删：

| 路径 | 内容 |
|---|---|
| `wwwroot/uploads/` | 后台上传的图片（`images/`）和 ҚАЗТЕСТ 听力音频（`audio/`） |
| `App_Data/consult/` | 访客随咨询上传的证件照片/PDF（个人数据，只有登录的管理员能打开） |
| `logs/`、`language_pack.txt` | 日志、后台界面语言包缓存 |

nginx 里需要：

```nginx
client_max_body_size 60m;                     # 咨询表单最多 5 个文件，每个 10 MB
proxy_set_header X-Real-IP $remote_addr;      # 防刷限制按访客 IP 计算
```

## 后台怎么用

| 菜单 | 管什么 |
|---|---|
| **Беттер мен мәтіндер → Бет блоктары** | 每个页面的每一块：标题、段落、按钮文字、图片、菜单、页脚、联系方式、SEO 标题与描述、FAQ……按页面分组。每块都有「Бастапқы мәтінге қайтару」可恢复原型文字 |
| **Қызметтер** | 服务分类（页签）、每项服务（说明、需准备的材料、申请代码 AUD-01 等）、单独的服务页（如 «Ата жолы»，地址 `/kz/service/…`） |
| **Жаңалықтар** | 文章和新闻（富文本编辑器，H2 标题自动生成目录）、分类。没有正文的新闻卡片会跳到「Сілтеме」里填的页面。**Жаңалық көздері**：自动采集新闻的来源（见下面「自动新闻」） |
| **«Киелі» · Қазақстан** | 景点（照片、正文、坐标、360° 全景）、17 个州（和地图绑定，只能改不能加）、景点分类、ЮНЕСКО 文化遗产 |
| **ҚАЗТЕСТ** | 模拟测试的套题（Нұсқалар）、题目（Сұрақтар：答案、对应的录音或阅读文章）、阅读文章（Оқылым мәтіндері）、听力录音（Тыңдалым жазбалары：台词 + 上传 MP3）。考试时间（70 分钟）和及格线（每部分 70%）在「Бет блоктары → ҚАЗТЕСТ → Нұсқалар бөлімі және тест баптаулары」 |
| **Өтінімдер** | 网站上提交的咨询：姓名、联系方式、所选服务及代码、附件；可以标记状态（Жаңа / Жұмыста / Аяқталды / Жабылды）并写内部备注 |
| **Беттер мен мәтіндер → Интерфейс жазулары** | 按钮、表单提示、错误信息、ҚАЗТЕСТ 考试界面等固定文字（也能翻译） |
| **Сайт баптаулары / Әкімшілер** | 网站图标、统计代码、清缓存；**Сайт тілдері**：哪些语言在前台显示；管理员和角色权限（可以细到每个菜单的查看/新增/修改/删除） |

图片：每个图片字段旁边有「Жүктеу」按钮，支持 JPG/PNG/WEBP/GIF/AVIF，10 MB 以内，宽于 2400 px 的照片会自动缩小。

## 多语言

- 哈萨克语是底稿，其他语言是它的翻译。每个编辑页面上方有语言页签（Қазақша · Русский · 简体中文 · English · Türkçe），后面显示已翻译的百分比；列表里也有 RU/ZH/EN/TR 小标签（绿=已译，黄=部分，灰=未译）。
- 翻译页只显示文字字段，每个字段下面显示哈萨克语原文。**没翻译的地方前台自动显示哈萨克语。**
- 图片、链接、图标、服务选择等对所有语言通用，只在「Қазақша」页修改；列表的行（新增、排序、删除）也只在哈萨克语里调整，翻译会跟着对应的行走。
- ҚАЗТЕСТ 的题目和阅读文章在所有语言里都保持哈萨克语（这是哈萨克语考试），只翻译界面和说明。
- 站内链接会自动换成当前语言（`/kz/services` 在俄语页变成 `/ru/services`）。
- 初始翻译（机器翻译草稿，约 6,800 词 × 4 种语言）在 `db/seed/i18n/{ru,zh-cn,en,tr}.json`，首次启动时导入；**请懂该语言的人在后台校对**，尤其是法规、证件名称。已在后台改过的翻译不会被覆盖。
- 暂时不想公开某种语言：在「Сайт баптаулары → Сайт тілдері」取消「Алдыңғы бетте көрсету」，它会从语言菜单、hreflang 和 sitemap 里消失（直接输入网址仍能打开，方便先内部校对）。
- 咨询申请会记录访客用的语言，收件箱里显示（«Қай елде · тілі»），便于用同一语言回复。

## 品牌与 Logo

网站名是 **kieli.kz**，公司仍是 «BASTAU LINE» ЖШС（文章、服务说明、结构化数据里照常写公司名）。网站名在「Бет блоктары → Барлық беттер → Логотип және атау」，最后一个点后面的部分（`.kz`）自动显示成天蓝色。

Logo 是一座毡房（киіз үй）：金色的 шаңырақ（天窗）、白色屋顶上的 уық（撑杆）、墙上的 кереге（网格）和金色的门；蓝色取自国旗的天蓝（`#0A7EA3`），金色是 `#F3C33A`。意思是“在哈萨克斯坦安一个新家”，也和 «Киелі» 的传统主题相合。

| 文件（`wwwroot/kieli/img/`） | 用途 |
|---|---|
| `logo.svg` | 页头、页脚的图形（36 px） |
| `favicon.svg`、`/favicon.ico` | 浏览器标签图标（去掉网格，小尺寸更清楚）；`.ico` 给旧浏览器和搜索结果 |
| `logo-wordmark.svg`、`logo-wordmark-light.svg` | 横版（图形 + kieli.kz 字标），后台左上角和登录页；`-light` 用在深色背景上 |
| `kieli-logo.png` | 横版透明 PNG（1200×360），给印刷品、合作方、社交媒体 |
| `kieli-avatar-1024.png` | Instagram、Telegram、WeChat、YouTube 头像 |
| `apple-touch-icon.png`、`icon-192.png`、`icon-512.png`、`icon-maskable-512.png` | 手机“添加到主屏幕”的图标（`/manifest.webmanifest`） |
| `og-kieli.jpg` | 链接分享到微信、Telegram、WhatsApp、Facebook 时的预览图（1200×630） |
| `qar-logo.svg` | 页脚最后一行「Сайтты жасаған」后面的 QAR Solutions logo（和 alash.kz 页脚一样，链接到 qar.kz）。它跟随文字颜色，深色模式也看得清；前面的文字（5 种语言）和链接在「Барлық беттер → Футер」里改 |

换 Logo：在「Логотип және атау」里上传新图，或者同名替换上面的文件（网址自动带内容哈希，浏览器会取新文件）。后台的 Logo 和图标在「Сайт баптаулары」里。

## SEO 与 GEO（搜索引擎和 AI 助手）

全部由程序根据后台内容生成，内容一改就跟着变：

- **每个页面**：标题和描述来自各页的「SEO: браузер тақырыбы мен сипаттама」块（5 种语言都有）；canonical、hreflang（含 `x-default`）、Open Graph / Twitter 分享卡片（默认用 `og-kieli.jpg`，文章用封面，景点用照片）。不存在的地址返回真正的 404（`noindex`）。
- **结构化数据**（schema.org JSON-LD，`Setup/StructuredData.cs`）：公司（LocalBusiness：地址、营业时间、电话、邮箱、社交账号，取自「Байланыс деректері」）、网站、创始人 Омар Бекмұрат（各语言的写法）、服务列表、文章（Article）、景点（TouristAttraction，带坐标）、ҚАЗТЕСТ（LearningResource）、问答（FAQPage，取自各页的 FAQ）、面包屑。可以用 https://search.google.com/test/rich-results 检查。
- **`/sitemap.xml`**：全部页面 × 5 种语言，带 hreflang 和最后修改日期。**`/robots.txt`**：欢迎所有搜索引擎和 AI 爬虫，屏蔽后台和 `/api/`。
- **`/llms.txt`、`/llms-full.txt`**：给 ChatGPT、Claude、Perplexity、豆包、Kimi 等 AI 助手读的英文简介（公司、联系方式、服务、ҚАЗТЕСТ、文章、景点；full 版另有每项服务的说明和全部问答）。
- **地理信息**：`geo.region`（KZ-AST）、`geo.placename`（Астана），公司地址和营业时间也在结构化数据里。

上线后要做的见下面清单（验证站点、提交 sitemap、地图商家资料）。

## 自动新闻（Жаңалық көздері）

网站每小时（每小时第 17 分）读取后台「Жаңалықтар → Жаңалық көздері」里各来源的 RSS / Atom 订阅源，把**与移民有关**的新闻自动加到「Жаңалықтар」页面和首页的新闻区（`Setup/NewsCollector.cs`，Hangfire 定时任务，只在正式环境运行；本地用列表上方的「Қазір тексеру」手动运行）。

为了不引起转载投诉，每条只保存：
- 原标题；
- 订阅源自带的简短导语（最多 280 字）；
- 来源名称和发布日期；
- 原文链接。

不复制正文和图片。卡片上写着「Дереккөз: 来源名」，点击在新窗口打开原文（「Түпнұсқасын оқу ↗」）。

抓取时也尽量守规矩：
- 遵守对方网站的 robots.txt；
- 请求里写明 `KieliNewsBot (+https://kieli.kz)`；
- 订阅源没更新就不重复下载（ETag / Last-Modified）；
- 不访问内网地址；
- 后台删掉的新闻不会再被加回来。

**关键词**：标题或导语里出现任意一个就收录；用逗号或换行分隔；「мигрант + Қазақстан」表示两个词都要有；写词根就行（қандас 能匹配 қандастар）。新来源默认带一套移民相关的哈萨克语关键词；关键词留空表示收录该源的全部新闻，只适合专门发移民新闻的官方来源。「Алып тастайтын сөздер」里的词出现就不收。

**默认来源**（2026-10-01 检查过：都能访问，robots.txt 允许）：

| 来源 | 状态 | 转载条款 |
|---|---|---|
| Tengrinews.kz（哈萨克语版） | 开启 | 允许部分引用，条件是带上指向原文的有效链接；全文转载需书面许可 |
| Akorda.kz（总统府） | 开启 | 官方信息，注明来源 |
| Egemen Qazaqstan | **关闭** | 条款写明材料只限个人、非商业使用；先联系编辑部，同意后再在后台勾选「Қосулы」 |

没有接的来源：
- **Kazinform**：有 Cloudflare 防机器人验证，不能绕过。
- **gov.kz**（劳动部移民委员会、内务部移民局等）：开发环境从境外连不上，没法测试。上线后如果服务器在哈萨克斯坦境内、能访问 gov.kz，再看它有没有 RSS，有的话直接在后台添加。

**加新来源之前，请先看对方网站的使用条款**；对方不同意转载就不要加。想先审核再发布，就取消「Бірден жариялау」：新闻会以「Жасырын」（隐藏）状态加入，在「Мақалалар мен жаңалықтар」里检查后勾选「Сайтта көрсету」。自动加入的新闻在文章列表里带「Автоматты」标记。

## 正式上线前必须做

- [ ] **Бет блоктары → Барлық беттер → Байланыс деректері**：填真实的电话、WhatsApp、Telegram、WeChat、邮箱、БСН，上传 WeChat 二维码（现在是原型里的示例号码）
- [ ] 正式服务器的第一个管理员用强密码（`appsettings.Production.json` 的 `InitialAdmin`，不要用本地测试用的简单密码），登录后在「Профиль」里再确认一次；给内容编辑单独建账号，角色选 «Контент редакторы»
- [ ] 修改数据库账号的密码（它曾在聊天里以明文发出）；旧网站数据库的密码也曾提交进旧仓库，也应该更换
- [ ] ҚАЗТЕСТ 听力：全部音频（每套 3 段）已用 Piper TTS（AI 声音，ISSAI KazakhTTS2，CC BY 4.0）生成并接好，播放器下面有署名，不要删。条件允许的话，请真人按 `docs/kaztest-audio/README.md` 的脚本重录，在后台「ҚАЗТЕСТ → Тыңдалым жазбалары」上传替换，并把「Аудионың авторы」改成配音员。题目是 BASTAU LINE 自编的（2026-10-01），不再使用国家测试中心的材料
- [ ] 核对文章、服务说明里的法规信息（原型内容是按 2026 年 9 月的公开信息写的）
- [ ] 请懂俄语、中文、英语、土耳其语的人校对翻译（后台各页面的语言页签）；不准备公开的语言先在「Сайт тілдері」里隐藏
- [ ] **旧站图片**：「Қазақстан」页和景点页的照片现在直接引用旧站的 `https://kieli.kz/uploads/thumbnail/…`。新程序部署到 kieli.kz 之前，把旧服务器网站目录里的 `wwwroot/uploads/thumbnail/` 整个复制到新站的 `wwwroot/uploads/thumbnail/`，否则换站后这些图片（包括分享预览图）会失效
- [ ] 域名切到新服务器后：在 Google Search Console、Yandex Webmaster、Bing Webmaster（需要的话再加百度资源平台）验证 kieli.kz——它们给的验证 `<meta>` 粘贴到「Сайт баптаулары → Analytics Html」；然后在这些平台提交 `https://kieli.kz/sitemap.xml`
- [ ] 上线后看一眼「Жаңалықтар → Жаңалық көздері」：每个来源的「Соңғы тексеріс」（最近一次检查）应该是一小时以内，没有「Қате」；需要的话联系 Egemen 编辑部取得转载许可
- [ ] 在 Google 商家资料（Google Business Profile）、2GIS、Yandex 地图登记 «BASTAU LINE»：地址、营业时间、电话和网站 kieli.kz 要和网站上一致（本地搜索和 AI 助手回答“阿斯塔纳哪里办…”时最看重这些）

## 技术说明

- **页面区块**：字段定义在 `Setup/BlockRegistry.Generated.cs`（52 块），默认值在 `db/seed/blocks.json`。首次启动时默认值写入 `pageblock` 表，之后以数据库为准。
  要给某块加字段：在 `BlockRegistry.Generated.cs` 里加 `FieldDef`，在 `blocks.json` 里加默认值，在对应的 `.cshtml` 里用 `b["字段名"]` 输出——后台表单自动出现这个字段。
- **内容表**：后台的列表/编辑页由 `Setup/Cms/Entities.cs` 里的定义驱动（字段、列、保存前的检查、删除保护），共用 `Views/Console/Cms/` 的页面和 `wwwroot/console/js/kieli-admin.js`。
- **缓存**：前台读的内容按语言缓存在内存（`Setup/ContentStore.cs`），后台任何保存都会立即清掉；直接改数据库后可用「Кэшті тазалау」。
- **翻译存储**：框架原有的 `multilanguage` 表。页面区块每种语言一条（整块的翻译 JSON），内容表每个字段一条；列表行带固定的 `_id`，翻译按 `_id` 对应。`dotnet KieliWeb.dll --export-translations 文件.json` 可导出全部哈萨克语原文（交给译者），译好的文件放进 `db/seed/i18n/` 后重启即导入（只补缺，不覆盖）。
- **数据库升级**：`db/kieli_schema.sql` 里新增的字段在启动时自动加到已有的表上（例如 `consultrequest.language`）。已有库里仍是旧默认文字的地方（例如网站名、SEO 标题），启动时按 `db/seed/upgrades.json` 换成新文字；后台改过的不动。
- **前台**：页面 HTML 由服务器输出（利于百度/Google/Yandex 收录），`kieli.js` 只负责交互；服务、新闻数据以 `window.BASTAU` / `window.KIELI` / `window.KAZTEST` 的形式嵌在页面里，结构和原型一致。
- **咨询表单**：`POST /api/consult`；有防机器人隐藏字段、每个 IP 10 分钟最多 5 次、文件按内容识别类型（只收 JPG/PNG/WEBP/HEIC/PDF）。附件存在网站外的 `App_Data/`，只能通过后台下载。
- **旧网址**：`/kz/attraction/view?id=…`（按旧 id 找到对应景点）、`/kz/tradition/…`、`/kz/about` 都会 301 跳到新页面。
- 后台富文本会自动去掉 `<script>`、事件属性和 `javascript:` 链接；iframe 只保留 YouTube / Vimeo / Google 地图 / Yandex。

## 后续可以做

- 把旧库 `kieliqazaqstan_db` 里的 187 个景点、照片和 360° 全景导入新的 `place` 表（现在前台只有原型里的 18 个）
- 新咨询到达时通知到 Telegram 或邮箱
- 管理员密码改用更强的哈希（现在沿用框架的 MD5 + 固定盐）
