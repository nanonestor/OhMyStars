using System.Globalization;
using System.Text;
using Microsoft.VisualBasic.FileIO;

namespace OhMyStars;

internal sealed class DeepSkyCatalog {
    public enum CatalogKind { All, NGC, IC, Messier, Other }

    public static readonly (string Code, string Name)[] Types = [
        ("G", "Galaxy"), ("GPair", "Galaxy pair"), ("GTrpl", "Galaxy triplet"),
        ("GGroup", "Group of galaxies"), ("OCl", "Open cluster"), ("GCl", "Globular cluster"),
        ("*Ass", "Association of stars"), ("Cl+N", "Star cluster + nebula"),
        ("PN", "Planetary nebula"), ("HII", "HII ionized region"), ("DrkN", "Dark nebula"),
        ("EmN", "Emission nebula"), ("Neb", "Nebula"), ("RfN", "Reflection nebula"),
        ("SNR", "Supernova remnant"), ("Nova", "Nova star"), ("*", "Star"),
        ("**", "Double star"), ("Other", "Other classification"),
        ("NonEx", "Nonexistent object"), ("Dup", "Duplicate entry")
    ];

    private static readonly string[] Header = [
        "Name", "Type", "RA", "Dec", "Const", "MajAx", "MinAx", "PosAng",
        "B-Mag", "V-Mag", "J-Mag", "H-Mag", "K-Mag", "SurfBr", "Hubble",
        "Pax", "Pm-RA", "Pm-Dec", "RadVel", "Redshift", "Cstar U-Mag",
        "Cstar B-Mag", "Cstar V-Mag", "M", "NGC", "IC", "Cstar Names",
        "Identifiers", "Common names", "NED notes", "OpenNGC notes", "Sources"
    ];

    internal sealed class Entry {
        private readonly IReadOnlyDictionary<string, string> _fields;
        private readonly int _catalogMask;
        private Details? _details;
        public long Id { get; }
        public bool IsAddendum { get; }
        public string this[string field] => _fields[field];
        public string Name => this["Name"];
        public string Type => this["Type"];
        public string TypeName { get; }
        public double? RaHours { get; }
        public double? DecDegrees { get; }
        public double? Magnitude { get; }
        public string MagnitudeBand { get; }
        public double? DistanceLightYears { get; }
        public bool CanDraw => RaHours.HasValue && DecDegrees.HasValue && Type is not ("Dup" or "NonEx");
        public string[] Identifiers { get; }
        public string Label { get; }
        public string SelectableLabel { get; }
        public int? MessierNumber { get; }
        public string MessierLabel { get; }
        public string MessierSelectableLabel { get; }
        public Details DisplayDetails => _details ??= new(this);
        internal string SearchText { get; }

        internal Entry(long id, string[] fields, bool addendum) {
            Id = id;
            IsAddendum = addendum;
            _fields = Header.Select((name, index) => (name, value: fields[index]))
                .ToDictionary(pair => pair.name, pair => pair.value, StringComparer.Ordinal);
            if(Name.Length == 0 || !Types.Any(t => t.Code == Type))
                throw new InvalidDataException($"Invalid OpenNGC name/type: '{Name}' / '{Type}'.");
            TypeName = Types.Single(t => t.Code == Type).Name;
            if(Name.StartsWith("NGC", StringComparison.Ordinal) || this["NGC"].Length > 0)
                _catalogMask |= 1 << (int)CatalogKind.NGC;
            if(Name.StartsWith("IC", StringComparison.Ordinal) || this["IC"].Length > 0)
                _catalogMask |= 1 << (int)CatalogKind.IC;
            if(this["M"].Length > 0 || Name.StartsWith("M", StringComparison.Ordinal) && Name.Length > 1 && char.IsDigit(Name[1]))
                _catalogMask |= 1 << (int)CatalogKind.Messier;
            if(IsAddendum)
                _catalogMask |= 1 << (int)CatalogKind.Other;
            RaHours = ParseAngle(this["RA"], true);
            DecDegrees = ParseAngle(this["Dec"], false);
            double? visual = Number(this["V-Mag"]);
            Magnitude = visual ?? Number(this["B-Mag"]);
            MagnitudeBand = visual.HasValue ? "V" : Magnitude.HasValue ? "B" : "";
            if(Number(this["Pax"], "parallax") is > 0 and var parallax) {
                double distance = 1000d / parallax * 3.26156377695d;
                if(!double.IsFinite(distance))
                    throw new InvalidDataException($"OpenNGC parallax '{this["Pax"]}' for {Name} produces a nonfinite distance.");
                DistanceLightYears = distance;
            }
            List<string> identifiers = [Name];
            AddIdentifiers(identifiers, "M", this["M"]);
            AddIdentifiers(identifiers, "NGC", this["NGC"]);
            AddIdentifiers(identifiers, "IC", this["IC"]);
            identifiers.AddRange(this["Identifiers"].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            Identifiers = identifiers.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            Label = Name + (this["M"].Length > 0 ? $" / M{this["M"]}" : "") +
                (this["Common names"].Length > 0 ? $" - {this["Common names"]}" : "");
            SelectableLabel = $"{Label}##DeepSky{Id}";
            string messier = Name.StartsWith("M", StringComparison.Ordinal) && Name.Length > 1 && char.IsAsciiDigit(Name[1])
                ? Name[1..] : this["M"];
            if(messier.Length > 0) {
                if(!int.TryParse(messier, NumberStyles.None, CultureInfo.InvariantCulture, out int number) || number <= 0)
                    throw new InvalidDataException($"Invalid OpenNGC Messier number '{messier}' for {Name}.");
                MessierNumber = number;
            }
            MessierLabel = MessierNumber is int m ? $"M{m} - {Label}" : Label;
            MessierSelectableLabel = $"{MessierLabel}##DeepSky{Id}";
            SearchText = Normalize(string.Join(" ", Identifiers) + " " + this["Common names"]);
        }

        public bool InCatalog(CatalogKind kind) {
            if(kind is < CatalogKind.All or > CatalogKind.Other)
                throw new ArgumentOutOfRangeException(nameof(kind));
            return kind == CatalogKind.All || (_catalogMask & (1 << (int)kind)) != 0;
        }
    }

    internal sealed class Details {
        private static readonly Dictionary<string, string> Constellations = new(StringComparer.Ordinal) {
            ["And"] = "Andromeda", ["Ant"] = "Antlia", ["Aps"] = "Apus", ["Aqr"] = "Aquarius",
            ["Aql"] = "Aquila", ["Ara"] = "Ara", ["Ari"] = "Aries", ["Aur"] = "Auriga",
            ["Boo"] = "Bootes", ["Cae"] = "Caelum", ["Cam"] = "Camelopardalis", ["Cnc"] = "Cancer",
            ["CVn"] = "Canes Venatici", ["CMa"] = "Canis Major", ["CMi"] = "Canis Minor",
            ["Cap"] = "Capricornus", ["Car"] = "Carina", ["Cas"] = "Cassiopeia", ["Cen"] = "Centaurus",
            ["Cep"] = "Cepheus", ["Cet"] = "Cetus", ["Cha"] = "Chamaeleon", ["Cir"] = "Circinus",
            ["Col"] = "Columba", ["Com"] = "Coma Berenices", ["CrA"] = "Corona Australis",
            ["CrB"] = "Corona Borealis", ["Crv"] = "Corvus", ["Crt"] = "Crater", ["Cru"] = "Crux",
            ["Cyg"] = "Cygnus", ["Del"] = "Delphinus", ["Dor"] = "Dorado", ["Dra"] = "Draco",
            ["Equ"] = "Equuleus", ["Eri"] = "Eridanus", ["For"] = "Fornax", ["Gem"] = "Gemini",
            ["Gru"] = "Grus", ["Her"] = "Hercules", ["Hor"] = "Horologium", ["Hya"] = "Hydra",
            ["Hyi"] = "Hydrus", ["Ind"] = "Indus", ["Lac"] = "Lacerta", ["Leo"] = "Leo",
            ["LMi"] = "Leo Minor", ["Lep"] = "Lepus", ["Lib"] = "Libra", ["Lup"] = "Lupus",
            ["Lyn"] = "Lynx", ["Lyr"] = "Lyra", ["Men"] = "Mensa", ["Mic"] = "Microscopium",
            ["Mon"] = "Monoceros", ["Mus"] = "Musca", ["Nor"] = "Norma", ["Oct"] = "Octans",
            ["Oph"] = "Ophiuchus", ["Ori"] = "Orion", ["Pav"] = "Pavo", ["Peg"] = "Pegasus",
            ["Per"] = "Perseus", ["Phe"] = "Phoenix", ["Pic"] = "Pictor", ["Psc"] = "Pisces",
            ["PsA"] = "Piscis Austrinus", ["Pup"] = "Puppis", ["Pyx"] = "Pyxis", ["Ret"] = "Reticulum",
            ["Sge"] = "Sagitta", ["Sgr"] = "Sagittarius", ["Sco"] = "Scorpius", ["Scl"] = "Sculptor",
            ["Sct"] = "Scutum", ["Ser"] = "Serpens", ["Se1"] = "Serpens Caput", ["Se2"] = "Serpens Cauda",
            ["Sex"] = "Sextans", ["Tau"] = "Taurus", ["Tel"] = "Telescopium", ["Tri"] = "Triangulum",
            ["TrA"] = "Triangulum Australe", ["Tuc"] = "Tucana", ["UMa"] = "Ursa Major", ["UMi"] = "Ursa Minor",
            ["Vel"] = "Vela", ["Vir"] = "Virgo", ["Vol"] = "Volans", ["Vul"] = "Vulpecula"
        };
        private static readonly (string Field, string Label)[] Fields = [
            ("RA", "RA (J2000, h:m:s)"), ("Dec", "Dec (J2000, d:m:s)"), ("Const", "Constellation"),
            ("PosAng", "Position angle (deg)"),
            ("SurfBr", "Surface brightness (B mag/arcsec2)"),
            ("Hubble", "Hubble type"),
            ("Cstar Names", "Central star identifiers")
        ];

        public string Type { get; }
        public string Identifiers { get; }
        public string ApparentMagnitude { get; }
        public string? Distance { get; }
        public string[] FieldLines { get; }
        public string[] NoteLines { get; }

        internal Details(Entry entry) {
            Type = $"{entry.TypeName} ({entry.Type})";
            Identifiers = "Identifiers: " + string.Join(", ", entry.Identifiers);
            Distance = entry.DistanceLightYears is double distance
                ? "Distance: " + distance.ToString("#,0.###", CultureInfo.InvariantCulture) + " light years (from catalog parallax)"
                : null;
            ApparentMagnitude = "Apparent Magnitude: N/A";
            foreach(string band in new[] { "V", "B", "J", "H", "K" }) {
                string text = entry[band + "-Mag"];
                if(Number(text) is not double magnitude)
                    continue;
                ApparentMagnitude = "Apparent Magnitude: " + magnitude.ToString("F2", CultureInfo.InvariantCulture) +
                    (band == "V" ? "" : $" ({band}-band; visual unavailable)");
                break;
            }
            FieldLines = Fields.Where(field => entry[field.Field].Length > 0)
                .Select(field => $"{field.Label}: {(field.Field == "Const" ? ConstellationName(entry[field.Field]) : entry[field.Field])}").ToArray();
            NoteLines = new[] { "NED notes", "OpenNGC notes" }.Where(field => entry[field].Length > 0)
                .Select(field => $"{field}: {entry[field]}").ToArray();
        }

        private static string ConstellationName(string abbreviation) =>
            Constellations.TryGetValue(abbreviation, out string? name) ? name :
                throw new InvalidDataException($"Unknown OpenNGC constellation '{abbreviation}'.");
    }

    public IReadOnlyList<Entry> Entries { get; }
    private readonly Entry[] _messierEntries;

    private DeepSkyCatalog(List<Entry> entries) {
        Entries = entries.AsReadOnly();
        _messierEntries = entries.Where(entry => entry.MessierNumber.HasValue)
            .OrderBy(entry => entry.MessierNumber).ThenBy(entry => entry.Id).ToArray();
    }

    public static DeepSkyCatalog Load(string ngcPath, string addendumPath) {
        List<Entry> entries = [];
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach(var (path, addendum) in new[] { (ngcPath, false), (addendumPath, true) }) {
            using TextFieldParser parser = new(path, Encoding.UTF8) {
                TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = true
            };
            parser.SetDelimiters(";");
            if(parser.ReadFields() is not string[] header || !header.SequenceEqual(Header))
                throw new InvalidDataException($"Unexpected OpenNGC header in {path}.");
            while(!parser.EndOfData) {
                long line = parser.LineNumber;
                string[] fields = parser.ReadFields() ?? throw new InvalidDataException($"Empty OpenNGC record in {path}:{line}.");
                if(fields.Length != Header.Length)
                    throw new InvalidDataException($"Expected {Header.Length} OpenNGC fields in {path}:{line}, got {fields.Length}.");
                try {
                    Entry entry = new(entries.Count + 1, fields, addendum);
                    if(!names.Add(entry.Name))
                        throw new InvalidDataException($"Duplicate OpenNGC name '{entry.Name}'.");
                    entries.Add(entry);
                } catch(InvalidDataException ex) {
                    throw new InvalidDataException($"{path}:{line}: {ex.Message}", ex);
                }
            }
        }
        return new(entries);
    }

    public Entry[] Search(string query, string? type = null, CatalogKind catalog = CatalogKind.All) {
        string[] words = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(Normalize).Where(word => word.Length > 0).ToArray();
        List<string> terms = [];
        for(int i = 0; i < words.Length; i++) {
            string word = words[i];
            if(word is "NGC" or "IC" or "M" && i + 1 < words.Length && char.IsAsciiDigit(words[i + 1][0]))
                word += words[++i];
            terms.Add(word);
        }
        IEnumerable<Entry> candidates = catalog == CatalogKind.Messier ? _messierEntries : Entries;
        return candidates.Where(entry => (type is null || entry.Type == type) && entry.InCatalog(catalog) &&
            terms.All(word => entry.SearchText.Contains(word, StringComparison.Ordinal))).ToArray();
    }

    // Ignore catalog padding and separators: NGC 224 and NGC0224 identify the same object.
    internal static string Normalize(string text) {
        StringBuilder result = new();
        for(int i = 0; i < text.Length; i++) {
            char c = text[i];
            if(char.IsAsciiDigit(c)) {
                int end = i + 1;
                while(end < text.Length && char.IsAsciiDigit(text[end]))
                    end++;
                while(i < end - 1 && text[i] == '0')
                    i++;
                result.Append(text.AsSpan(i, end - i));
                i = end - 1;
            } else if(char.IsLetter(c)) {
                result.Append(char.ToUpperInvariant(c));
            }
        }
        return result.ToString();
    }

    private static void AddIdentifiers(List<string> identifiers, string prefix, string value) {
        foreach(string number in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            identifiers.Add(prefix + number);
    }

    private static double? Number(string text, string field = "magnitude") {
        if(text.Length == 0)
            return null;
        if(!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value))
            throw new InvalidDataException($"Invalid OpenNGC {field} '{text}'.");
        return value;
    }

    private static double? ParseAngle(string text, bool ra) {
        if(text.Length == 0)
            return null;
        string[] parts = (text[0] is '+' or '-' ? text[1..] : text).Split(':');
        if(parts.Length != 3 || !double.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out double major) ||
            !double.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out double minutes) ||
            !double.TryParse(parts[2], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double seconds) ||
            !double.IsFinite(seconds) || minutes is < 0 or >= 60 || seconds is < 0 or >= 60)
            throw new InvalidDataException($"Invalid OpenNGC coordinate '{text}'.");
        double value = major + minutes / 60 + seconds / 3600;
        if(ra ? text.StartsWith('-') || value >= 24 : value > 90)
            throw new InvalidDataException($"Out-of-range OpenNGC coordinate '{text}'.");
        return text.StartsWith('-') ? -value : value;
    }
}
