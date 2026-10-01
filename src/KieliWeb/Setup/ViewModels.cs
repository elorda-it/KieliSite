using System.Collections.Generic;
using MODEL;

namespace KieliWeb.Setup;

/// <summary>A block rendered by a shared partial (_Faq, _SecHead, _Cross).</summary>
public record SectionModel(BlockContent Block, string Css = "section", string TitleId = null);

/// <summary>One news/article card (_Post): «big», «row» or a plain grid card.</summary>
public record PostModel(Article Article, string Kind, string CategorySlug, string CategoryName);

/// <summary>The «Сізге қандай көмек керек?» service picker, first tab/item open.</summary>
public record HelpModel(List<ContentStore.TabWithItems> Tabs, string Uid);

/// <summary>A place card or tile (_PlaceCard, _Tile).</summary>
public record PlaceModel(Place Place, Region Region, Placecategory Category, bool Big = false);
