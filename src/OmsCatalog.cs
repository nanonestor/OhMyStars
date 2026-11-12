namespace OhMyStars;

internal static class OmsCatalog {
    private static readonly Lazy<MultipleStarCatalog> Data = new(() =>
        MultipleStarCatalog.Load(ModPaths.Combine("catalogs", "oms_catalog.csv")
#if DEBUG
            , ModPaths.Combine("catalogs", "definitively-correct-systems.csv"),
            ModPaths.Combine("dev", "system-review-overrides.csv")
#endif
        ));

    internal static MultipleStarCatalog Value => Data.Value;
}
