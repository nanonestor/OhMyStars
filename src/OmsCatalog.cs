namespace OhMyStars;

internal static class OmsCatalog {
    private static readonly Lazy<MultipleStarCatalog> Data = new(() =>
        MultipleStarCatalog.Load(ModPaths.Combine("catalogs", "oms_catalog.csv")));

    internal static MultipleStarCatalog Value => Data.Value;
}
