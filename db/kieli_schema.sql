-- KieliSite database schema (MySQL 8 / MariaDB 10.4+). Safe to run repeatedly:
-- every table is CREATE TABLE IF NOT EXISTS, nothing is dropped or altered.
-- The app runs this file itself on start when Site:AutoInitDatabase is true.
-- Keep this file in sync with src/MODEL when a column changes (no migrations, same as NiceGirlSite).

-- ---------------------------------------------------------------------------
-- Framework tables (from NiceGirlSite): admins, roles, permissions, admin menu,
-- languages, site settings.
-- ---------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS `admin` (
  `id` int NOT NULL AUTO_INCREMENT,
  `email` varchar(64) NOT NULL,
  `phone` varchar(16) NOT NULL DEFAULT '',
  `password` varchar(32) NOT NULL,
  `name` varchar(64) NOT NULL,
  `avatarUrl` varchar(255) NOT NULL DEFAULT '',
  `description` longtext NOT NULL,
  `isSuper` tinyint unsigned NOT NULL DEFAULT '0',
  `hiddenColumnJson` longtext NOT NULL,
  `skinName` varchar(16) NOT NULL DEFAULT 'light',
  `reLogin` tinyint unsigned NOT NULL DEFAULT '0',
  `addTime` int NOT NULL,
  `updateTime` int NOT NULL,
  `qStatus` tinyint unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  KEY `idx_admin_status` (`qStatus`,`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `adminloginerrorlog` (
  `id` int unsigned NOT NULL AUTO_INCREMENT,
  `adminId` int NOT NULL,
  `lastErrorTime` int NOT NULL,
  `lastErrorIP` varchar(50) DEFAULT NULL,
  `errorCount` int NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `adminrole` (
  `id` int NOT NULL AUTO_INCREMENT,
  `adminId` int NOT NULL,
  `roleId` int NOT NULL,
  `addTime` int NOT NULL,
  `updateTime` int NOT NULL,
  `qStatus` tinyint NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `role` (
  `id` int NOT NULL AUTO_INCREMENT,
  `name` varchar(255) NOT NULL,
  `description` varchar(1000) NOT NULL,
  `addTime` int NOT NULL,
  `updateTime` int NOT NULL,
  `qStatus` tinyint unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `permission` (
  `id` int NOT NULL AUTO_INCREMENT,
  `tableName` varchar(32) NOT NULL,
  `localKey` varchar(64) NOT NULL,
  `manageType` varchar(32) NOT NULL,
  `addTime` int NOT NULL,
  `updateTime` int NOT NULL,
  `qStatus` tinyint NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `rolepermission` (
  `id` int NOT NULL AUTO_INCREMENT,
  `roleId` int NOT NULL,
  `permissionId` int NOT NULL,
  `addTime` int NOT NULL,
  `updateTime` int NOT NULL,
  `qStatus` tinyint NOT NULL DEFAULT '0',
  `tableName` varchar(32) NOT NULL,
  `columnId` int NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `navigation` (
  `id` int NOT NULL AUTO_INCREMENT,
  `navigationTypeId` int NOT NULL,
  `parentId` int NOT NULL DEFAULT '0',
  `navTitle` varchar(64) NOT NULL,
  `navUrl` varchar(128) NOT NULL,
  `target` varchar(16) NOT NULL,
  `hasIcon` tinyint unsigned NOT NULL DEFAULT '0',
  `icon` varchar(64) NOT NULL,
  `description` varchar(1500) NOT NULL,
  `addTime` int NOT NULL,
  `updateTime` int NOT NULL,
  `displayOrder` int NOT NULL DEFAULT '0',
  `noChild` tinyint unsigned NOT NULL DEFAULT '0',
  `isLock` tinyint unsigned NOT NULL DEFAULT '0',
  `qStatus` tinyint unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `language` (
  `id` int unsigned NOT NULL AUTO_INCREMENT,
  `shortName` varchar(8) NOT NULL,
  `fullName` varchar(32) NOT NULL,
  `languageCulture` varchar(24) NOT NULL,
  `uniqueSeoCode` varchar(8) NOT NULL,
  `ISOCode` varchar(8) NOT NULL,
  `languageFlagImageUrl` varchar(128) NOT NULL,
  `displayOrder` tinyint unsigned NOT NULL,
  `isSubLanguage` tinyint unsigned NOT NULL DEFAULT '0',
  `isDefault` tinyint unsigned NOT NULL DEFAULT '0',
  `frontendDisplay` tinyint unsigned NOT NULL DEFAULT '0',
  `backendDisplay` tinyint unsigned NOT NULL DEFAULT '0',
  `qStatus` tinyint unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `multilanguage` (
  `id` int NOT NULL AUTO_INCREMENT,
  `tableName` varchar(255) NOT NULL,
  `columnName` varchar(255) NOT NULL,
  `language` varchar(32) NOT NULL DEFAULT 'kz',
  `qStatus` tinyint NOT NULL DEFAULT '0',
  `columnValue` longtext NOT NULL,
  `columnId` int NOT NULL,
  PRIMARY KEY (`id`),
  KEY `idx_multilanguage_table` (`tableName`,`columnId`,`language`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `sitesetting` (
  `id` int NOT NULL AUTO_INCREMENT,
  `aboutSize` varchar(255) DEFAULT NULL,
  `aboutUs` longtext,
  `address` varchar(500) DEFAULT NULL,
  `adminLogoUrl` varchar(255) DEFAULT NULL,
  `analyticsHtml` longtext,
  `analyticsScript` longtext,
  `contactus` longtext,
  `copyright` varchar(1000) DEFAULT NULL,
  `deliveryMethods` longtext,
  `description` varchar(500) DEFAULT NULL,
  `disabledTime` int NOT NULL DEFAULT '0',
  `email` varchar(128) DEFAULT NULL,
  `facebook` varchar(255) DEFAULT NULL,
  `favicon` varchar(255) DEFAULT NULL,
  `instagram` varchar(255) DEFAULT NULL,
  `keywords` varchar(500) DEFAULT NULL,
  `loginPath` varchar(64) DEFAULT NULL,
  `logoUrl` varchar(255) DEFAULT NULL,
  `darkLogo` varchar(255) DEFAULT NULL,
  `lightLogo` varchar(255) DEFAULT NULL,
  `mallDiscount` int NOT NULL DEFAULT '0',
  `mallHiddenCategoryIds` varchar(1000) DEFAULT NULL,
  `mallMinimumPurchaseQuantity` int NOT NULL DEFAULT '0',
  `mallMoneyTransfer` int NOT NULL DEFAULT '0',
  `mallPriceType` varchar(64) DEFAULT NULL,
  `mallShippingCosts` int NOT NULL DEFAULT '0',
  `mallStockQuantity` int NOT NULL DEFAULT '0',
  `mallStoreIds` varchar(1000) DEFAULT NULL,
  `mapEmbed` longtext,
  `maxErrorCount` int NOT NULL DEFAULT '0',
  `mobileLogoUrl` varchar(255) DEFAULT NULL,
  `mobileDarkLogo` varchar(255) DEFAULT NULL,
  `mobileLightLogo` varchar(255) DEFAULT NULL,
  `paymentMethods` longtext,
  `phone` varchar(64) DEFAULT NULL,
  `qStatus` tinyint unsigned NOT NULL DEFAULT '0',
  `siteTypeId` int NOT NULL DEFAULT '0',
  `telegram` varchar(255) DEFAULT NULL,
  `title` varchar(150) DEFAULT NULL,
  `twitter` varchar(255) DEFAULT NULL,
  `vk` varchar(255) DEFAULT NULL,
  `warranty` longtext,
  `whatsapp` varchar(255) DEFAULT NULL,
  `tiktok` varchar(255) DEFAULT NULL,
  `mStartTime` int NOT NULL DEFAULT '0',
  `mEndTime` int NOT NULL DEFAULT '0',
  `youtube` varchar(255) DEFAULT NULL,
  `aiEnabled` tinyint unsigned NOT NULL DEFAULT '0',
  `aiOpenAiApiKey` varchar(255) DEFAULT NULL,
  `aiOpenAiModel` varchar(64) DEFAULT NULL,
  `aiCustomPrompt` longtext,
  `aiMaxArticlesPerRun` int NOT NULL DEFAULT '3',
  `aiJobCronExpression` varchar(64) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `additionalcontent` (
  `id` int NOT NULL AUTO_INCREMENT,
  `additionalType` varchar(64) NOT NULL,
  `title` varchar(255) NOT NULL,
  `videoEmbed` varchar(255) NOT NULL,
  `shortDescription` longtext NOT NULL,
  `fullDescription` longtext NOT NULL,
  `iconUrl` varchar(255) NOT NULL,
  `color` varchar(64) NOT NULL,
  `backgroundColor` varchar(64) NOT NULL,
  `backgroundImageUrl` varchar(255) NOT NULL,
  `displayOrder` int NOT NULL DEFAULT '0',
  `addTime` int NOT NULL,
  `updateTime` int NOT NULL,
  `qStatus` tinyint unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `latynurl` (
  `id` int NOT NULL AUTO_INCREMENT,
  `itemId` int NOT NULL,
  `language` varchar(8) NOT NULL,
  `latynUrl` varchar(255) NOT NULL,
  `tableName` varchar(32) NOT NULL,
  `qStatus` tinyint unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  KEY `idx_latynurl` (`latynUrl`,`tableName`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------------------------------------------------------------
-- KieliSite content. Same conventions as the framework tables: lowerCamelCase
-- columns, unix-int times, qStatus = soft delete (0 live, 1 deleted).
-- ---------------------------------------------------------------------------

-- Page texts and images: one row per block, fields as JSON.
-- Field definitions (labels, types) live in src/KieliWeb/Setup/BlockRegistry.cs.
CREATE TABLE IF NOT EXISTS `pageblock` (
  `id` int NOT NULL AUTO_INCREMENT,
  `blockKey` varchar(64) NOT NULL,
  `dataJson` longtext NOT NULL,
  `addTime` int NOT NULL,
  `updateTime` int NOT NULL,
  `qStatus` tinyint unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_pageblock_blockKey` (`blockKey`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- "Сізге қандай көмек керек?" — service tabs (Аударма, Нотариат, ...) and their items.
-- A request is stored with the item code (AUD-02 ...), so codes should stay stable.
CREATE TABLE IF NOT EXISTS `servicetab` (
  `id` int NOT NULL AUTO_INCREMENT,
  `slug` varchar(32) NOT NULL,
  `code` varchar(8) NOT NULL,
  `name` varchar(64) NOT NULL,
  `icon` varchar(32) NOT NULL DEFAULT '',
  `cta` varchar(64) NOT NULL DEFAULT '',
  `visual` varchar(8) NOT NULL DEFAULT 'illo',
  `stepsJson` longtext NOT NULL,
  `footnote` varchar(1000) NOT NULL DEFAULT '',
  `displayOrder` int NOT NULL DEFAULT '0',
  `addTime` int NOT NULL,
  `updateTime` int NOT NULL,
  `qStatus` tinyint unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  KEY `idx_servicetab_status` (`qStatus`,`displayOrder`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `serviceitem` (
  `id` int NOT NULL AUTO_INCREMENT,
  `tabId` int NOT NULL,
  `slug` varchar(32) NOT NULL,
  `code` varchar(16) NOT NULL,
  `name` varchar(64) NOT NULL,
  `icon` varchar(32) NOT NULL DEFAULT '',
  `title` varchar(255) NOT NULL,
  `dir` varchar(255) NOT NULL DEFAULT '',
  `who` varchar(1000) NOT NULL DEFAULT '',
  `bringJson` longtext NOT NULL,
  `note` varchar(1000) NOT NULL DEFAULT '',
  `linkUrl` varchar(255) NOT NULL DEFAULT '',
  `linkText` varchar(128) NOT NULL DEFAULT '',
  `cta` varchar(64) NOT NULL DEFAULT '',
  `displayOrder` int NOT NULL DEFAULT '0',
  `addTime` int NOT NULL,
  `updateTime` int NOT NULL,
  `qStatus` tinyint unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  KEY `idx_serviceitem_tab` (`tabId`,`qStatus`,`displayOrder`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Detail pages such as «Ата жолы» картасы (/kz/service/atazholy); sections as JSON.
CREATE TABLE IF NOT EXISTS `servicepage` (
  `id` int NOT NULL AUTO_INCREMENT,
  `slug` varchar(64) NOT NULL,
  `title` varchar(255) NOT NULL,
  `seoDescription` varchar(500) NOT NULL DEFAULT '',
  `dataJson` longtext NOT NULL,
  `displayOrder` int NOT NULL DEFAULT '0',
  `addTime` int NOT NULL,
  `updateTime` int NOT NULL,
  `qStatus` tinyint unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  KEY `idx_servicepage_slug` (`slug`,`qStatus`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- News and articles (/kz/news, /kz/news/{slug}).
CREATE TABLE IF NOT EXISTS `articlecategory` (
  `id` int NOT NULL AUTO_INCREMENT,
  `slug` varchar(32) NOT NULL,
  `name` varchar(64) NOT NULL,
  `displayOrder` int NOT NULL DEFAULT '0',
  `addTime` int NOT NULL,
  `updateTime` int NOT NULL,
  `qStatus` tinyint unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `article` (
  `id` int NOT NULL AUTO_INCREMENT,
  `categoryId` int NOT NULL DEFAULT '0',
  `slug` varchar(128) NOT NULL,
  `title` varchar(255) NOT NULL,
  `excerpt` varchar(1000) NOT NULL DEFAULT '',
  `bodyHtml` longtext NOT NULL,
  `linkUrl` varchar(255) NOT NULL DEFAULT '',
  `coverImageUrl` varchar(255) NOT NULL DEFAULT '',
  `coverTone` varchar(16) NOT NULL DEFAULT 'sky',
  `coverIcon` varchar(32) NOT NULL DEFAULT 'i-doc',
  `dateText` varchar(32) NOT NULL DEFAULT '',
  `publishTime` int NOT NULL DEFAULT '0',
  `readMinutes` int NOT NULL DEFAULT '0',
  `sourceName` varchar(128) NOT NULL DEFAULT '',
  `sourceUrl` varchar(255) NOT NULL DEFAULT '',
  `newsSourceId` int NOT NULL DEFAULT '0',
  `authorName` varchar(64) NOT NULL DEFAULT '',
  `serviceKey` varchar(64) NOT NULL DEFAULT '',
  `isImportant` tinyint unsigned NOT NULL DEFAULT '0',
  `isPublished` tinyint unsigned NOT NULL DEFAULT '1',
  `seoDescription` varchar(500) NOT NULL DEFAULT '',
  `viewCount` int NOT NULL DEFAULT '0',
  `displayOrder` int NOT NULL DEFAULT '0',
  `addTime` int NOT NULL,
  `updateTime` int NOT NULL,
  `qStatus` tinyint unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  KEY `idx_article_list` (`qStatus`,`isPublished`,`publishTime`),
  KEY `idx_article_slug` (`slug`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Automatic news (Setup/NewsCollector.cs): the RSS/Atom feeds read every hour. A collected item is an article row
-- with newsSourceId set: headline, the feed's short summary, source name, date and a link to the original.
CREATE TABLE IF NOT EXISTS `newssource` (
  `id` int NOT NULL AUTO_INCREMENT,
  `name` varchar(128) NOT NULL,
  `siteUrl` varchar(255) NOT NULL DEFAULT '',
  `feedUrl` varchar(255) NOT NULL,
  `keywords` varchar(2000) NOT NULL DEFAULT '',
  `excludeWords` varchar(1000) NOT NULL DEFAULT '',
  `categoryId` int NOT NULL DEFAULT '0',
  `autoPublish` tinyint unsigned NOT NULL DEFAULT '1',
  `maxPerRun` int NOT NULL DEFAULT '5',
  `isEnabled` tinyint unsigned NOT NULL DEFAULT '1',
  `note` varchar(1000) NOT NULL DEFAULT '',
  `lastCheckTime` int NOT NULL DEFAULT '0',
  `lastStatus` varchar(500) NOT NULL DEFAULT '',
  `etag` varchar(255) NOT NULL DEFAULT '',
  `lastModified` varchar(64) NOT NULL DEFAULT '',
  `addedCount` int NOT NULL DEFAULT '0',
  `displayOrder` int NOT NULL DEFAULT '0',
  `addTime` int NOT NULL,
  `updateTime` int NOT NULL,
  `qStatus` tinyint unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- «Киелі» · Қазақстан: regions (map ids from the SVG atlas), categories, places, heritage.
CREATE TABLE IF NOT EXISTS `region` (
  `id` int NOT NULL AUTO_INCREMENT,
  `mapId` int NOT NULL,
  `name` varchar(64) NOT NULL,
  `shortName` varchar(64) NOT NULL DEFAULT '',
  `placeCount` int NOT NULL DEFAULT '0',
  `isCity` tinyint unsigned NOT NULL DEFAULT '0',
  `displayOrder` int NOT NULL DEFAULT '0',
  `addTime` int NOT NULL,
  `updateTime` int NOT NULL,
  `qStatus` tinyint unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `placecategory` (
  `id` int NOT NULL AUTO_INCREMENT,
  `slug` varchar(32) NOT NULL,
  `name` varchar(64) NOT NULL,
  `pluralName` varchar(64) NOT NULL DEFAULT '',
  `displayOrder` int NOT NULL DEFAULT '0',
  `addTime` int NOT NULL,
  `updateTime` int NOT NULL,
  `qStatus` tinyint unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `place` (
  `id` int NOT NULL AUTO_INCREMENT,
  `slug` varchar(64) NOT NULL,
  `regionId` int NOT NULL DEFAULT '0',
  `categoryId` int NOT NULL DEFAULT '0',
  `name` varchar(128) NOT NULL,
  `fact` varchar(255) NOT NULL DEFAULT '',
  `lead` varchar(1000) NOT NULL DEFAULT '',
  `bodyHtml` longtext NOT NULL,
  `factsJson` longtext NOT NULL,
  `imageUrl` varchar(255) NOT NULL DEFAULT '',
  `panoJson` longtext NOT NULL,
  `lat` decimal(10,6) NOT NULL DEFAULT '0.000000',
  `lon` decimal(10,6) NOT NULL DEFAULT '0.000000',
  `legacyId` int NOT NULL DEFAULT '0',
  `isFeatured` tinyint unsigned NOT NULL DEFAULT '0',
  `displayOrder` int NOT NULL DEFAULT '0',
  `addTime` int NOT NULL,
  `updateTime` int NOT NULL,
  `qStatus` tinyint unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  KEY `idx_place_slug` (`slug`,`qStatus`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `heritage` (
  `id` int NOT NULL AUTO_INCREMENT,
  `name` varchar(255) NOT NULL,
  `kind` varchar(64) NOT NULL DEFAULT '',
  `year` varchar(16) NOT NULL DEFAULT '',
  `imageUrl` varchar(255) NOT NULL DEFAULT '',
  `linkUrl` varchar(255) NOT NULL DEFAULT '',
  `displayOrder` int NOT NULL DEFAULT '0',
  `addTime` int NOT NULL,
  `updateTime` int NOT NULL,
  `qStatus` tinyint unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ҚАЗТЕСТ practice tests: variants, reading passages, questions (answer '' = key not known yet).
CREATE TABLE IF NOT EXISTS `kaztestvariant` (
  `id` int NOT NULL AUTO_INCREMENT,
  `title` varchar(64) NOT NULL,
  `isPublished` tinyint unsigned NOT NULL DEFAULT '1',
  `displayOrder` int NOT NULL DEFAULT '0',
  `addTime` int NOT NULL,
  `updateTime` int NOT NULL,
  `qStatus` tinyint unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `kaztestpassage` (
  `id` int NOT NULL AUTO_INCREMENT,
  `variantId` int NOT NULL,
  `passageNo` int NOT NULL,
  `bodyText` longtext NOT NULL,
  `addTime` int NOT NULL,
  `updateTime` int NOT NULL,
  `qStatus` tinyint unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  KEY `idx_kaztestpassage_variant` (`variantId`,`passageNo`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `kaztestquestion` (
  `id` int NOT NULL AUTO_INCREMENT,
  `variantId` int NOT NULL,
  `sectionNo` int NOT NULL,
  `sectionName` varchar(32) NOT NULL,
  `questionNo` int NOT NULL,
  `questionText` varchar(1000) NOT NULL,
  `subText` varchar(1000) NOT NULL DEFAULT '',
  `optionA` varchar(500) NOT NULL DEFAULT '',
  `optionB` varchar(500) NOT NULL DEFAULT '',
  `optionC` varchar(500) NOT NULL DEFAULT '',
  `optionD` varchar(500) NOT NULL DEFAULT '',
  `answer` varchar(1) NOT NULL DEFAULT '',
  `audioUrl` varchar(255) NOT NULL DEFAULT '',
  `audioNo` int NOT NULL DEFAULT '0',
  `passageNo` int NOT NULL DEFAULT '0',
  `addTime` int NOT NULL,
  `updateTime` int NOT NULL,
  `qStatus` tinyint unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  KEY `idx_kaztestquestion_variant` (`variantId`,`sectionNo`,`questionNo`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Listening recordings of a variant: the script (one "Name: words" line per turn) and the uploaded audio.
CREATE TABLE IF NOT EXISTS `kaztestaudio` (
  `id` int NOT NULL AUTO_INCREMENT,
  `variantId` int NOT NULL,
  `audioNo` int NOT NULL,
  `title` varchar(255) NOT NULL DEFAULT '',
  `voices` varchar(1000) NOT NULL DEFAULT '',
  `script` longtext NOT NULL,
  `audioUrl` varchar(255) NOT NULL DEFAULT '',
  `credit` varchar(255) NOT NULL DEFAULT '',
  `creditUrl` varchar(255) NOT NULL DEFAULT '',
  `addTime` int NOT NULL,
  `updateTime` int NOT NULL,
  `qStatus` tinyint unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  KEY `idx_kaztestaudio_variant` (`variantId`,`audioNo`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Requests from the «Кеңес алу» / «Құжатты жіберу» forms. Files live outside wwwroot.
CREATE TABLE IF NOT EXISTS `consultrequest` (
  `id` int NOT NULL AUTO_INCREMENT,
  `name` varchar(128) NOT NULL,
  `contact` varchar(128) NOT NULL,
  `country` varchar(16) NOT NULL DEFAULT '',
  `language` varchar(8) NOT NULL DEFAULT 'kz',
  `serviceKey` varchar(64) NOT NULL DEFAULT '',
  `code` varchar(16) NOT NULL DEFAULT '',
  `serviceTitle` varchar(255) NOT NULL DEFAULT '',
  `message` longtext NOT NULL,
  `source` varchar(255) NOT NULL DEFAULT '',
  `status` tinyint unsigned NOT NULL DEFAULT '0',
  `staffNote` longtext NOT NULL,
  `ip` varchar(64) NOT NULL DEFAULT '',
  `userAgent` varchar(255) NOT NULL DEFAULT '',
  `addTime` int NOT NULL,
  `updateTime` int NOT NULL,
  `qStatus` tinyint unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  KEY `idx_consultrequest_list` (`qStatus`,`status`,`addTime`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `consultfile` (
  `id` int NOT NULL AUTO_INCREMENT,
  `requestId` int NOT NULL,
  `fileName` varchar(255) NOT NULL,
  `storedName` varchar(128) NOT NULL,
  `contentType` varchar(128) NOT NULL DEFAULT '',
  `fileSize` int NOT NULL DEFAULT '0',
  `addTime` int NOT NULL,
  `qStatus` tinyint unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`id`),
  KEY `idx_consultfile_request` (`requestId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
