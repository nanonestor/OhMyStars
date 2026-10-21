namespace CatalogBuilder;

internal static class CatalogTests
{
    public static void Run()
    {
        string temp = Path.Combine(Path.GetTempPath(), "oms-catalog-tests-" + Guid.NewGuid().ToString("N"));
        string athyg = Path.Combine(temp, "AT-HYG_40"), wds = Path.Combine(temp, "WDS"), output = Path.Combine(temp, "output");
        Directory.CreateDirectory(athyg);
        Directory.CreateDirectory(wds);
        Directory.CreateDirectory(output);
        Directory.CreateDirectory(Path.Combine(temp, "Henry Draper"));
        string OrbitRow(string id, string pair = "AB", string grade = "2")
        {
            char[] row = Enumerable.Repeat(' ', 245).ToArray();
            void Set(int offset, int length, string value) => value.PadRight(length).CopyTo(0, row, offset, length);
            Set(19, 10, id); Set(30, 14, "TST   1" + pair);
            Set(80, 12, "50"); Set(92, 1, "y"); Set(105, 9, "10"); Set(114, 1, "a");
            Set(125, 8, "30"); Set(143, 8, "45"); Set(162, 12, "2000");
            Set(187, 8, "0.2"); Set(205, 8, "60"); Set(223, 4, "2000"); Set(233, 1, grade);
            return new string(row);
        }
        string orbitPath = Path.Combine(output, "orb6orbits.txt");
        File.WriteAllLines(orbitPath, [OrbitRow("03000+0000", grade: "9")]);
        string[] header = "id,tyc,gaia,hyg,hip,hd,hr,gl,bayer,flam,con,proper,ra,dec,pos_src,dist,x0,y0,z0,dist_src,mag,absmag,ci,mag_src,rv,rv_src,pmra,pmdec,pm_src,vx,vy,vz,spect,spect_src".Split(',');
        string Star(int id, double ra, string gaia, double mag, double motion)
        {
            string[] row = Enumerable.Repeat("", header.Length).ToArray();
            void Set(string column, string value) => row[Array.IndexOf(header, column)] = value;
            Set("id", id.ToString(Program.Invariant)); Set("ra", Program.F(ra)); Set("dec", "0");
            Set("gaia", gaia); Set("mag", Program.F(mag)); Set("pos_src", "T"); Set("hd", "123");
            Set("pmra", Program.F(motion)); Set("pmdec", "0");
            return Csv.Line(row);
        }
        string PairRow(string id, string components, double? rho, int year = 2000, string flags = "", string pm = "+000",
            string pm2 = "", string dm = "")
        {
            char[] row = Enumerable.Repeat(' ', 130).ToArray();
            void Set(int offset, int length, string value) => value.PadRight(length).CopyTo(0, row, offset, length);
            Set(0, 10, id); Set(10, 7, "TST   1"); Set(17, 5, components);
            Set(23, 4, "1991"); Set(28, 4, year.ToString(Program.Invariant)); Set(34, 3, "  1");
            Set(38, 6, "90.00"); Set(46, 7, rho?.ToString("0.000", Program.Invariant) ?? "");
            Set(58, 5, "5.00"); Set(64, 5, "7.00");
            Set(80, 4, pm); Set(84, 4, "+000"); Set(107, 4, flags);
            if (pm2.Length > 0) { Set(89, 4, pm2); Set(93, 4, "+000"); }
            Set(98, 8, dm);
            string coordinate = id switch
            {
                "00000+0000" => "000000.00+000000.0",
                "00300+0000" => "003000.00+000000.0",
                "02000+0000" => "020000.00+000000.0",
                "03000+0000" => "030000.00+000000.0",
                "04000+0000" => "040000.00+000000.0",
                "05000+0000" => "050000.00+000000.0",
                "07000+0000" => components.StartsWith('B') ? "070036.00+000000.0" : "070000.00+000000.0",
                "08000+0000" => "080000.00+000000.0",
                "10000+0000" => "100000.00+000000.0",
                "11000+0000" => "110000.00+000000.0",
                "13000+0000" => "130000.00+000000.0",
                "14000+0000" => "140000.00+000000.0",
                "15000+0000" => "150000.00+000000.0",
                "16000+0000" => "160000.00+000000.0",
                "17000+0000" => "170000.00+000000.0",
                "18000+0000" => "180000.00+000000.0",
                "19000+0000" => "190000.00+000000.0",
                "20000+0000" => "200000.00+000000.0",
                "21000+0000" => "210000.00+000000.0",
                "22000+0000" => "220000.00+000000.0",
                "23000+0000" => "230000.00+000000.0",
                _ => "010000.00+000000.0"
            };
            Set(112, 18, coordinate);
            return new string(row);
        }
        try
        {
            string[] faint = Csv.Parse(Star(5, 12, "faint", 11, 0));
            faint[Array.IndexOf(header, "hip")] = "900";
            string[] tie1 = Csv.Parse(Star(6, 13, "tie1", 5.04, 0));
            string[] tie2 = Csv.Parse(Star(7, 13, "tie2", 5.03, 0));
            tie1[Array.IndexOf(header, "hd")] = "";
            tie2[Array.IndexOf(header, "hd")] = "";
            string[] namedPrimary = Csv.Parse(Star(12, 22 + 4.0 / 54000, "", 5, 0));
            namedPrimary[Array.IndexOf(header, "proper")] = "Fixture Primary";
            string[] identifiedPrimary = Csv.Parse(Star(16, 23 + 4.0 / 54000, "", 5, 0));
            identifiedPrimary[Array.IndexOf(header, "hip")] = "901";
            File.WriteAllLines(Path.Combine(athyg, "athyg_40.csv"),
                new[] { Csv.Line(header), Star(1, 1, "shared", 5, 0), Star(2, 1 + 0.1 / 54000, "shared", 7, 0),
                    Star(3, 3, "motion", 5, 500), Star(4, 4, "", 10, 120), Csv.Line(faint), Csv.Line(tie1), Csv.Line(tie2),
                    Star(8, 19 + 5.0 / 54000, "", 5, 0), Star(9, 19 + 24.0 / 54000, "", 7, 0),
                    Star(10, 20 + 5.0 / 54000, "", 7.01, 0), Star(11, 21 + 5.0 / 54000, "", 7, 0),
                    Csv.Line(namedPrimary), Star(13, 22 + 1.0 / 54000, "", 7, 0),
                    Star(14, 22 + 14.0 / 54000, "", 7, 0), Star(15, 22 + 10.0 / 54000, "", 7, 0),
                    Csv.Line(identifiedPrimary), Star(17, 23, "", 5, 0),
                    Star(22, 0.5, "", 5, 0), Star(23, 0.5 + 20.0 / 54000, "", 7, 0),
                    Star(24, 0.5 + 30.0 / 54000, "", 7, 0),
                    "156575,2837-2311-1,,9618,9640,12533,603,,Gam-1,57,And,Almach,2.064984,42.329725,HIP_X,120.4819,76.3682,45.8409,81.1321,H,2.308,-3.097,1.676,T,-11.5,OTHER,42.32,-49.3,HIP,-3.47,26.11,-28.56,B8V,HYG",
                    "156597,2837-2311-2,346231302441905920,,,12534,,,,,And,,2.06519051,42.33107165,T,82.8336,52.5017,31.5186,55.7812,G_R3,5.021,0.43,,T,-14.0,OTHER,16.64,-47.75,G_R3,-1.41,6.77,-23.29,B 9 V,T",
                    "1462698,2019-1250-1,,,,129988,,,,,Boo,,14.7497723,27.07492778,T_X,67.1926,-44.9842,-39.4454,30.5831,G_R2,4.808,0.671,0.064,T,-23.0,OTHER,-43.52,22.5,G_R2,8.71,26.08,-4.09,,N",
                    "1462701,2019-1251-1,1279798794197267072,71879,72105,129989,5505,,Eps,36,Boo,Izar,14.74978422,27.0742078,T,72.324,-48.4198,-42.4582,32.9178,G_R3,2.578,-1.718,1.359,T,-16.6,OTHER,-50.82,21.02,G_R3,2.09,25.01,-1.14,K0II-III,T" });
            foreach (string name in new[] { "LICENSE", "ACKNOWLEDGMENTS.md" }) File.WriteAllText(Path.Combine(athyg, name), "fixture");
            foreach (string name in new[] { "wdsnewnotes_main.txt", "h.txt", "stf.txt", "stt.txt", "hds.txt", "wdserror.txt", "wdsweb_format.txt", "bf.txt" })
                File.WriteAllText(Path.Combine(wds, name), "");
            File.WriteAllLines(Path.Combine(wds, "bf.txt"), [
                "19000+0000 BU    1    eps 1 Lyr             eps Lyr               4 Lyr",
                "19000+0000 BU    1    eps 1 Lyr",
                "22000+0000 TST   1    Fixture Primary"
            ]);
            string BdsRow(string id, string number, string discoverer = "TST   1", string omit = "") =>
                id.PadRight(13) + "23549+3819".PadRight(13) + "17157".PadRight(7) +
                number.PadLeft(6) + " " + discoverer.PadLeft(9) + omit;
            File.WriteAllLines(Path.Combine(wds, "wdsidsadsbds.txt"),
                [BdsRow("01000+0000", "123"), BdsRow("01000+0000", "124"),
                 BdsRow("01000+0000", "....."), BdsRow("01000+0000", "999", omit: "{1}"),
                 BdsRow("01000+0000", "998", "OTHER 1"), BdsRow("08000+0000", "125")]);
            File.WriteAllLines(Path.Combine(wds, "wdsnewnotes_main.txt"),
                ["09000+0000 TST   1 AB  This pair is optical, not physical.",
                 "                      A spurious detection was reported.",
                 "                      Continued explanation and bibliographic reference.",
                 "10000+0000 TST   1     AC and AD: Optical pairs, based on study of relative motion of the",
                 "                      components using the method of apparent motion parameters.",
                 "                      AF: Optical pair, based on study of relative motion of the components.",
                 "                      AB: Optical pair, based on study of relative motion of the components.",
                 "07000+0000 TST   1     AB-CD: Optical pair, based on study of relative motion of the components.",
                 "11000+0000 TST   1     Optical pair, based on study of relative motion of the components.",
                 "                      AD is probably optical.",
                 "                      AB, AC, and AD: Optical pairs, based on study of relative motion.",
                 "                      AB: H VI 76. Optical pair, based on study of relative motion of the components.",
                 "                      Research says the C component is probably optical, based on motion.",
                 "                      Researchers say the pair is probably optical, based on motion.",
                 "           OTHER 1    Optical pair, based on study of relative motion of the components.",
                 "                      This is a 2+2 hierarchical multiple. C is optical.",
                 "                      The A component is optical.",
                 "05000+0000 TST   1     B is physical.",
                 "14000+0000 TST   1     The A component is the D component of 15000+0000.",
                 "15000+0000 TST   1     The D component is the A component of 14000+0000.",
                 "                      The 1979.7730 measure was a spurious preliminary",
                 "                      measure of the observation on 1979.7703, and has been deleted.",
                 "17000+0000 TST   1     B is HIP 900."]);
            File.WriteAllLines(Path.Combine(wds, "hds.txt"),
                ["08000+0000" + "TST   1" + "     " + "900", "16000+0000" + "TST   1" + "     " + "900",
                 "23000+0000TST   1".PadRight(22) + "901"]);
            foreach (string name in new[] { "catalog.dat", "ReadMe.txt", "errata.htx" })
                File.WriteAllText(Path.Combine(temp, "Henry Draper", name), "");
            char[] hd = Enumerable.Repeat(' ', 48).ToArray();
            "  9876".CopyTo(0, hd, 0, 6);
            "BD+00  123".PadRight(12).CopyTo(0, hd, 6, 12);
            File.WriteAllText(Path.Combine(temp, "Henry Draper", "catalog.dat"), new string(hd) + Environment.NewLine);
            File.WriteAllLines(Path.Combine(wds, "wds_precise.txt"),
            [
                PairRow("01000+0000", "AB", 0.1, pm: "+500", pm2: "+700"),
                PairRow("01000+0000", "AC", 1200),
                PairRow("01000+0000", "AD", null),
                PairRow("01000+0000", "AE", 20, 2020),
                PairRow("01000+0000", "AF", 0.001),
                PairRow("02000+0000", "AB", 0.1, pm2: "+101", dm: "+00  123"),
                PairRow("02000+0000", "AC", 0.2, pm2: "+100"),
                PairRow("07000+0000", "Aa1,2", 0.01),
                PairRow("07000+0000", "Aa,Ab", 0.1),
                PairRow("07000+0000", "Ba,Bb", 0.1),
                PairRow("07000+0000", "AB,CD", 100),
                PairRow("08000+0000", "AB", 10),
                PairRow("03000+0000", "AB", 10, pm2: "-101"),
                PairRow("09000+0000", "AB", 10),
                PairRow("10000+0000", "AB", 10, flags: "I"),
                PairRow("10000+0000", "AC", 20),
                PairRow("10000+0000", "AD", 30),
                PairRow("10000+0000", "AE", 40),
                PairRow("10000+0000", "AF", 50, flags: "S"),
                PairRow("11000+0000", "AB", 10),
                PairRow("11000+0000", "AC", 20),
                PairRow("11000+0000", "AD", 30),
                PairRow("12000+0000", "AB", 10),
                PairRow("13000+0000", "AB", 10),
                PairRow("14000+0000", "AB", 10),
                PairRow("15000+0000", "AB", 10),
                PairRow("15000+0000", "AD", 30),
                PairRow("16000+0000", "AB", 10),
                PairRow("17000+0000", "AB", 10),
                PairRow("17000+0000", "AC", 20),
                PairRow("18000+0000", "AB", 10).Replace("TST   1", "STF 418", StringComparison.Ordinal),
                PairRow("19000+0000", "AB", 20).Replace("TST   1", "BU    1", StringComparison.Ordinal),
                PairRow("20000+0000", "AB", 20),
                PairRow("21000+0000", "AB", 20),
                PairRow("22000+0000", "AB", 10),
                PairRow("23000+0000", "AB", 20),
                PairRow("00300+0000", "AB", 20),
                PairRow("00300+0000", "BC", 10).Remove(58, 5).Insert(58, "7.00 "),
                "02039+4220STF 205A,BC  1777 2024  367  63.30    9.501      2.31  5.02 K3IIb     +042-049 +017-047 +41  395 NS   020353.92+421947.5",
                "02039+4220BAR  22AD    1898 1898    1 244.60   27.940      2.31 15.   K3IIb     +042-049          +41  395 N    020353.92+421947.5",
                "02039+4220STT  38BC    1843 2024  382 117.20    0.378      5.3   6.5  B8V+A0V   +017-047          +41  395 NO   020353.92+421947.5",
                PairRow("11000+0000", "A,S17", 40),
                PairRow("04000+0000", "AB", 10, flags: "P", pm: "+012"),
                PairRow("05000+0000", "AB", 10, flags: "S"),
                PairRow("05000+0000", "AC", 20, flags: "U"),
                PairRow("05000+0000", "AD", 30, flags: "Y"),
                PairRow("06000+0000", "AB", 10, flags: "X")[..112] + "      .         . ",
                "14450+2704STF1877AB    1822 2024  480 346.80    2.910      2.58  4.81 K0II-III  -051+020          +27 2417 N    144459.14+270429.9",
                "14450+2704STF1877AC    1901 2020    7 254.40  174.070      2.58 12.58 K0II-III  -051+020 -006-016          N    144459.14+270429.9"
            ]);
            new Builder(temp, output).Run();
            Validation.Run(temp, output);
            var lines = File.ReadLines(Path.Combine(output, "oms_catalog.csv")).Select(Csv.Parse).ToList();
            var columns = lines[0].Select((value, i) => (value, i)).ToDictionary(p => p.value, p => p.i);
            if (new[] { "hierarchy_path", "parent_component", "relative_to_component", "measurement_components", "wds_notes" }.Any(columns.ContainsKey))
                throw new InvalidOperationException("Obsolete hierarchy or narrative columns exported.");
            var rows = lines.Skip(1).ToList();
            string Get(string[] row, string name) => row[columns[name]];
            void Check(bool value, string message)
            {
                if (!value) throw new InvalidOperationException("Catalog fixture failed: " + message);
            }
            Check(rows.Count(r => Get(r, "gaia") == "shared") == 2, "shared Gaia rows retained");
            Check(rows.Any(r => Get(r, "id") == "23" && Get(r, "wds_id") == "00300+0000" &&
                    Get(r, "component_label") == "B") &&
                rows.Any(r => Get(r, "id") == "24" && Get(r, "wds_id") == "00300+0000" &&
                    Get(r, "component_label") == "C"),
                "General BC matching derives B from AB and C from B, not the repeated WDS system coordinate");
            var almach = rows.Where(r => Get(r, "wds_id") == "02039+4220").ToArray();
            Check(almach.Length == 4 &&
                almach.Any(r => Get(r, "id") == "156575" && Get(r, "component") == "P") &&
                almach.Any(r => Get(r, "id") == "156597" && Get(r, "component_label") == "B" &&
                    Get(r, "component") == "C" && Get(r, "mag") == "5.021" && Get(r, "radec_source") == "GIA") &&
                almach.Any(r => Get(r, "component_label") == "C" && Get(r, "component") == "C" &&
                    Get(r, "id") == "" && Get(r, "ra") == ""),
                "Almach keeps B/C physical despite conflicting A-BC S flag, reuses blended representative, and preserves source fields and unknown positions");
            Check(rows.Any(r => Get(r, "id") == "12" && Get(r, "component") == "P") &&
                rows.Any(r => Get(r, "id") == "13" && Get(r, "component") == "S"),
                "Unique named primary with matching magnitude beats closer incompatible positional neighbor");
            Check(rows.Any(r => Get(r, "id") == "14" && Get(r, "component_label") == "B" &&
                    Get(r, "wds_id") == "22000+0000" && Get(r, "ra") == Program.F(22 + 14.0 / 54000)) &&
                rows.Any(r => Get(r, "id") == "15" && Get(r, "component") == "S"),
                "Companion search is anchored to preserved AT-HYG primary, not raw WDS coordinate");
            Check(rows.Any(r => Get(r, "id") == "16" && Get(r, "component") == "P") &&
                rows.Any(r => Get(r, "id") == "17" && Get(r, "component") == "S"),
                "Unique component-specific HIP identifies primary before nearest-position match");
            var izar = rows.Where(r => Get(r, "wds_id") == "14450+2704").ToArray();
            string izarPrimary = Get(izar.Single(r => Get(r, "id") == "1462701"), "oms_id");
            Check(izar.Length == 3 &&
                izar.Any(r => Get(r, "id") == "1462701" && Get(r, "proper") == "Izar" &&
                    Get(r, "component") == "P" && Get(r, "component_label") == "A") &&
                izar.Any(r => Get(r, "id") == "1462698" && Get(r, "component") == "C" &&
                    Get(r, "component_label") == "B" && Get(r, "primary_oms_id") == izarPrimary &&
                    Get(r, "ra") == "14.7497723" && Get(r, "mag") == "4.808" &&
                    Get(r, "rho_arcsec") == Program.F(2.910)) &&
                izar.Any(r => Get(r, "component_label") == "C" && Get(r, "component") == "C"),
                "Izar identities/measurements preserved without duplicate B; moderate motion alone no longer marks outer C questionable");
            Check(Builder.BayerAlias("eps 1 lyr") == ("Eps-1", "Lyr") &&
                Builder.BayerAlias("EPS-2 LYR") == ("Eps-2", "Lyr") &&
                Builder.BayerAlias("alp 2 CVn") == ("Alp-2", "CVn") &&
                Builder.BayerAlias("not a designation") is null, "numbered Greek alias normalization");
            Check(rows.Any(r => Get(r, "id") == "8" && Get(r, "component") == "P" && Get(r, "bayer") == "Eps-1" &&
                Get(r, "flam") == "4" && Get(r, "con") == "Lyr" && Get(r, "bu") == "1" && Get(r, "bds") == ""),
                "five-arcsecond primary match; scoped numbered Bayer/Flamsteed enrichment; BU does not invent BDS");
            Check(rows.Any(r => Get(r, "id") == "9" && Get(r, "component") == "C"),
                "four-arcsecond derived-companion match within six-arcsecond radius");
            Check(rows.Any(r => Get(r, "wds_id") == "20000+0000" && Get(r, "component") == "P" && Get(r, "id") == "") &&
                rows.Any(r => Get(r, "id") == "10" && Get(r, "component") == "S"),
                "expanded-only search must not assign a primary to an incompatible magnitude (>2 mag)");
            Check(rows.Any(r => Get(r, "id") == "11" && Get(r, "component") == "P"),
                "expanded-only magnitude compatibility includes exact two-magnitude boundary");
            Check(rows.Any(r => Get(r, "id") == "1" && Get(r, "component") == "P"), "primary reused");
            Check(rows.Any(r => Get(r, "id") == "2" && Get(r, "component") == "C"), "nearby companion reused");
            Check(rows.Any(r => Get(r, "id") == "2" && Get(r, "pmra") == "0"), "AT-HYG motion overrides discrepant WDS motion without changing C to V");
            Check(rows.Any(r => Get(r, "id") == "1" && Get(r, "bds") == "123;124"), "BDS aliases, missing IDs, discovery scope and omission markers");
            Check(rows.Where(r => Get(r, "component") != "P").All(r => Get(r, "bds") == ""), "BDS system aliases only on primary");
            Check(rows.Any(r => Get(r, "component_label") == "C" && Get(r, "wds_id") == "01000+0000"), "wide companion retained");
            foreach (string label in new[] { "D", "E" })
                Check(rows.Any(r => Get(r, "wds_id") == "01000+0000" && Get(r, "component_label") == label && Get(r, "ra") == "" && Get(r, "component") == "C"), "unknown geometry companion retained: " + label);
            Check(rows.Count(r => Get(r, "wds_id") == "02000+0000") == 3, "nearby new WDS stars retained");
            Check(rows.Any(r => Get(r, "wds_id") == "02000+0000" && Get(r, "component_label") == "B" && Get(r, "component") == "Q"), "motion-only classification is Questionable");
            Check(rows.Any(r => Get(r, "wds_id") == "02000+0000" && Get(r, "component_label") == "C" && Get(r, "component") == "C"), "exactly one hundred mas/year remains Component");
            Check(rows.Any(r => Get(r, "wds_id") == "02000+0000" && Get(r, "component") == "P" && Get(r, "hd") == "9876"), "unique HD DM cross-reference");
            var nested = rows.Where(r => Get(r, "wds_id") == "07000+0000").ToArray();
            Check(nested.Select(r => Get(r, "component_label")).Order().SequenceEqual(new[] { "Aa1", "Aa2", "Ab", "Ba", "Bb", "C", "D" }), "only individual leaves exported");
            Check(nested.Any(r => Get(r, "component_label") == "Aa1" && Get(r, "component") == "P" &&
                Get(r, "wds_id") == "07000+0000"), "nested primary label");
            Check(nested.Any(r => Get(r, "relationships").Split('|').Contains("AB>CD")), "directional group relationship retained without a group-star row");
            Check(nested.Where(r => Get(r, "component_label") is "C" or "D").All(r => Get(r, "component") == "V"),
                "optical group endpoint expands to companion leaves");
            Check(rows.Any(r => Get(r, "id") == "5" && Get(r, "wds_id") == "08000+0000" &&
                Get(r, "ra") == "12" && Get(r, "hd") == "123" && Get(r, "radec_source") == "GIA"), "ID link reuses full AT-HYG star and retains its positions/HD");
            Check(rows.Count(r => Get(r, "hip") == "900") == 1, "no WDS duplicate after exact ID reconciliation");
            Check(rows.Any(r => Get(r, "id") == "5" && Get(r, "bds") == "125"), "BDS retained after full AT-HYG reconciliation");
            Check(rows.Where(r => Get(r, "wds_id").Length > 0).All(r => !Get(r, "wds_id").Contains('\\') &&
                !Get(r, "wds_id").Contains('[') && !Get(r, "wds_id").Contains('"')), "plain signed WDS IDs");
            Check(rows.Any(r => Get(r, "id") == "4" && Get(r, "pmra") == "120" && Get(r, "component") == "P"), "P-normalized match and unchanged source PM");
            string[] review = File.ReadAllLines(Path.Combine(output, "match-review.csv"));
            Check(review.Length == 1, "excluded unresolved records do not generate pending reviews");
            Check(!rows.Any(r => Get(r, "wds_id") == "09000+0000"), "uncertain-existence WDS group excluded");
            Check(rows.Where(r => Get(r, "wds_id") == "11000+0000").All(r =>
                Get(r, "component_label") != "S17" && !Get(r, "relationships").Contains("S17")),
                "unsupported relationship excluded without removing accepted companions");
            Check(rows.Any(r => Get(r, "id") == "3" && Get(r, "component") == "P" && Get(r, "pmra") == "500"), "discrepant WDS motion does not reject AT-HYG match");
            Check(rows.Any(r => Get(r, "wds_id") == "03000+0000" && Get(r, "component_label") == "B" && Get(r, "component") == "Q"), "companion classified using authoritative primary motion");
            foreach (string label in new[] { "A", "B" })
                Check(rows.Any(r => Get(r, "wds_id") == "10000+0000" && Get(r, "component_label") == label &&
                    Get(r, "component") == "U" && Get(r, "system_id") != ""), "I-flagged endpoints retained Unknown: " + label);
            foreach (string label in new[] { "C", "D" })
                Check(rows.Any(r => Get(r, "wds_id") == "10000+0000" && Get(r, "component_label") == label &&
                    Get(r, "component") == "V"), "scoped optical companion without motion difference: " + label);
            Check(rows.Any(r => Get(r, "wds_id") == "10000+0000" && Get(r, "component_label") == "E" &&
                Get(r, "component") == "C"), "unrelated companion not marked optical");
            Check(rows.Any(r => Get(r, "wds_id") == "10000+0000" && Get(r, "component_label") == "F" && Get(r, "component") == "V"),
                "S-flagged optical companion retained as Visual");
            Check(rows.Any(r => Get(r, "wds_id") == "05000+0000" && Get(r, "component_label") == "B" &&
                Get(r, "component") == "Q"), "physical note conflicts with S flag, so companion is Questionable");
            foreach (string label in new[] { "C", "D" })
                Check(rows.Any(r => Get(r, "wds_id") == "05000+0000" && Get(r, "component_label") == label &&
                    Get(r, "component") == "V"), "S/U/Y companion retained as Visual: " + label);
            Check(!rows.Any(r => Get(r, "wds_id") == "06000+0000"), "X dubious double excluded before coordinate parsing");
            Check(rows.Any(r => Get(r, "wds_id") == "11000+0000" && Get(r, "component_label") == "B" &&
                Get(r, "component") == "Q"), "tentative unscoped optical note resolved by unique discovery designation");
            foreach (string label in new[] { "C", "D" })
                Check(rows.Any(r => Get(r, "wds_id") == "11000+0000" && Get(r, "component_label") == label &&
                    Get(r, "component") == "Q"), "probably-optical evidence takes precedence over definitive optical notes: " + label);
            Check(rows.Any(r => Get(r, "wds_id") == "11000+0000" && Get(r, "component_label") == "A" &&
                Get(r, "component") == "P"), "optical notes leave primary P");
            Check(rows.Any(r => Get(r, "id") == "7" && Get(r, "wds_id") == "13000+0000"),
                "closest magnitude wins even with advantage below 0.1");
            Check(!rows.Any(r => Get(r, "wds_id") == "12000+0000"), "later shared-star system omitted without duplicate");
            Check(!rows.Any(r => Get(r, "wds_id") == "16000+0000"), "duplicate primary ID removes later orphan group");
            Check(!rows.Any(r => Get(r, "wds_id") == "17000+0000" && Get(r, "component_label") == "B") &&
                rows.Any(r => Get(r, "wds_id") == "17000+0000" && Get(r, "component_label") == "C"),
                "duplicate companion ID removes only affected relationship");
            Check(rows.Count(r => Get(r, "wds_id") == "18000+0000") == 2,
                "historical alternative discovery designation alone does not quarantine current records");
            Check(rows.Any(r => Get(r, "wds_id") == "15000+0000" && Get(r, "component_label") == "B") &&
                !rows.Any(r => Get(r, "wds_id") == "15000+0000" && Get(r, "component_label") == "D"),
                "explicit reciprocal cross-WDS aliases omit duplicate component while retaining other relationships");
            Pair[] opticalPairs =
            [
                Pair.Parse(PairRow("11000+0000", "AB", 10), 1),
                Pair.Parse(PairRow("11000+0000", "AC", 20), 2)
            ];
            var scopedOptical = Builder.OpticalNotes(
                ["11000+0000 TST   1     This is a 2+2 hierarchical multiple. C is optical."], opticalPairs);
            Check(scopedOptical.Labels.SetEquals(["C"]) && scopedOptical.Pairs.Count == 0,
                "2+2 narrative marks C alone without inventing hierarchy or making B visual");
            var tentativeOptical = Builder.OpticalNotes(
                ["11000+0000 TST   1     AC: Probably optical.",
                 "                      B is possibly optical.",
                 "                      C is not optical."], opticalPairs);
            Check(tentativeOptical.QuestionablePairs.SetEquals([2]) &&
                tentativeOptical.QuestionableLabels.SetEquals(["B"]) &&
                tentativeOptical.PhysicalLabels.SetEquals(["C"]) && tentativeOptical.Labels.Count == 0,
                "tentative pair, tentative component and negated optical evidence keep their scope");
            var mixedOptical = Builder.OpticalNotes(
                ["11000+0000 TST   1     B is physical. C is optical."], opticalPairs);
            Check(mixedOptical.PhysicalLabels.SetEquals(["B"]) && mixedOptical.Labels.SetEquals(["C"]),
                "physical and optical statements in the same note remain component-scoped");
            var tentativePair = Builder.OpticalNotes(
                ["11000+0000 TST   1     AC is probably optical."], opticalPairs);
            Check(tentativePair.QuestionablePairs.SetEquals([2]) && tentativePair.QuestionableLabels.Count == 0,
                "tentative AC statement does not mark unrelated AB companion Questionable");
            var uncertainComponent = Builder.OpticalNotes(
                ["11000+0000 TST   1     The optical/physical nature of the C component is undefined."], opticalPairs);
            Check(uncertainComponent.QuestionableLabels.SetEquals(["C"]) && uncertainComponent.QuestionablePairs.Count == 0,
                "uncertain nature statement scopes component after optical wording");
            Check(!Builder.DeletedHistoricalMeasure(
                ["15000+0000 TST   1     This pair is spurious and should be deleted."], 0),
                "only already-deleted preliminary measures are exempt, not spurious pair identities");
            foreach (string note in new[]
            {
                "The measure reported for 1976.622 is spurious and has been deleted.",
                "Hipparcos acceleration of 17 mas yr-2 is spurious.",
                "Spurious HIP solution for fast-moving pair.",
                "Star A is SB1, P = 4.0 d, though this may be spurious."
            })
                Check(Builder.InformationalNarrative(["15000+0000 TST   1     " + note], 0),
                    "measurement/model commentary not an identity failure: " + note);
            Check(!Builder.InformationalNarrative(
                ["15000+0000 TST   1     AB: Likely a spurious pair. Last measured 1961."], 0),
                "pair existence warning remains actionable despite measurement wording");
            Check(!Builder.InformationalNarrative(
                ["15000+0000 TST   1     A spurious orbit of this ghost pair has been computed."], 0),
                "ghost-pair identity warning is not dismissed as orbit disagreement");
            Check(Builder.InformationalNarrative(
                ["15000+0000 TST   1     This system has been restored. While perhaps still spurious,",
                 "                      subsequent confirmation makes the discovery measurement more probable."], 0),
                "explicit restored/confirmed status supersedes historical doubt");
            var nonphysical = Builder.OpticalNotes(
                ["11000+0000 TST   1     Find that WDS C and D components of system are not physical."], opticalPairs);
            Check(nonphysical.Labels.SetEquals(["C", "D"]) && nonphysical.Pairs.Count == 0,
                "nonphysical companion list scoped without affecting primary or B");
            Check(review.All(r => !r.Contains("crossmatch_identity_uncertain") && !r.Contains("conflicting_component_identity")),
                "resolved tie and omitted shared-star assignments do not require review");
            Check(review.All(r => !r.Contains("WDS_identification_uncertain_flag_I")), "I classification requires no review");
            File.WriteAllLines(orbitPath, [
                OrbitRow("03000+0000"), OrbitRow("10000+0000"), OrbitRow("10000+0000", "AC"),
                OrbitRow("05000+0000", "AC"), OrbitRow("05000+0000", "AC"),
                OrbitRow("05000+0000", "AD", "9"), OrbitRow("07000+0000", "AB-CD")
            ]);
            new Builder(temp, output).Run();
            Validation.Run(temp, output);
            var orbitalRows = File.ReadLines(Path.Combine(output, "oms_catalog.csv")).Skip(1).Select(Csv.Parse).ToArray();
            Check(orbitalRows.Length == rows.Count &&
                orbitalRows.Select(r => Get(r, "oms_id")).SequenceEqual(rows.Select(r => Get(r, "oms_id"))),
                "Orbital classification preserves all rows and stable IDs.");
            foreach (string[] row in orbitalRows)
            {
                string[] original = rows.Single(r => Get(r, "oms_id") == Get(row, "oms_id"));
                bool promoted = Get(row, "wds_id") == "03000+0000" && Get(row, "component_label") == "B" ||
                    Get(row, "wds_id") == "10000+0000" && Get(row, "component_label") == "C";
                Check(Get(row, "component") == (promoted ? "C" : Get(original, "component")),
                    "Only supported plot-matched Q/V companions promoted; U/P and ambiguous/group/nonvisual matches unchanged.");
                Check(row.Where((_, i) => i != columns["component"]).SequenceEqual(
                    original.Where((_, i) => i != columns["component"])), "Orbital evidence changes classification only.");
            }
            string membershipCatalog = Path.Combine(output, "membership-test.csv");
            string membershipSource = Path.Combine(output, "membership-wds.txt");
            string MembershipRow(string id, string label, string role, string line, string relation)
            {
                string[] row = (string[])orbitalRows[0].Clone();
                foreach (var (name, value) in new[] {
                    ("oms_id", id), ("primary_oms_id", "9001"), ("system_id", "9001"),
                    ("wds_id", "22000+0000"), ("component_label", label), ("component", role),
                    ("wds_source_line", line), ("relationships", relation),
                    ("theta_deg", line.Length > 0 ? "90" : ""), ("rho_arcsec", line.Length > 0 ? "11.1" : ""),
                    ("measurement_year", line.Length > 0 ? "2024" : "")
                }) row[columns[name]] = value;
                return Csv.Line(row);
            }
            File.WriteAllLines(membershipCatalog, [
                Csv.Line(lines[0]), MembershipRow("9001", "A", "P", "", ""),
                MembershipRow("9002", "Ba", "V", "1", "A>B"),
                MembershipRow("9003", "Bb", "V", "2", "Ba>Bb")
            ]);
            File.WriteAllLines(membershipSource, [
                PairRow("22000+0000", "AB", 11.1), PairRow("22000+0000", "Ba,Bb", 1.5, flags: "U")
            ]);
            File.WriteAllLines(orbitPath, [OrbitRow("22000+0000")]);
            var promotions = OrbitalClassification.Apply(membershipCatalog, orbitPath, membershipSource);
            var membershipRows = File.ReadLines(membershipCatalog).Skip(1).Select(Csv.Parse).ToArray();
            Check(promotions.GetValueOrDefault("V") == 1 &&
                membershipRows.Select(r => Get(r, "component")).SequenceEqual(["P", "C", "V"]),
                "Parent-endpoint orbit promotes only the measurement-owning representative, not optical subgroup neighbors.");
            File.WriteAllLines(membershipCatalog, [
                Csv.Line(lines[0]), MembershipRow("9001", "A", "P", "", ""),
                MembershipRow("9002", "Ba", "U", "1", "A>B"),
                MembershipRow("9003", "Bb", "V", "2", "Ba>Bb")
            ]);
            Check(OrbitalClassification.Apply(membershipCatalog, orbitPath, membershipSource).Count == 0,
                "Original-pair evidence preserves identification-uncertain representatives.");
            File.WriteAllLines(membershipCatalog, [
                Csv.Line(lines[0]), MembershipRow("9001", "A", "P", "", ""),
                MembershipRow("9002", "Ba", "V", "1", "A>B"),
                MembershipRow("9003", "Bb", "V", "2", "Ba>Bb")
            ]);
            File.WriteAllLines(orbitPath, [OrbitRow("22000+0000"), OrbitRow("22000+0000")]);
            Check(OrbitalClassification.Apply(membershipCatalog, orbitPath, membershipSource).Count == 0,
                "Ambiguous supported orbits cannot classify parent-endpoint representatives.");
            string[] conflictingAlias = Csv.Parse(Star(18, 22, "", 5, 0));
            conflictingAlias[Array.IndexOf(header, "proper")] = "Fixture Primary";
            File.AppendAllText(Path.Combine(athyg, "athyg_40.csv"), Csv.Line(conflictingAlias) + Environment.NewLine);
            new Builder(temp, output).Run();
            Validation.Run(temp, output);
            var ambiguousRows = File.ReadLines(Path.Combine(output, "oms_catalog.csv")).Skip(1).Select(Csv.Parse).ToArray();
            Check(ambiguousRows.Any(r => Get(r, "id") == "18" && Get(r, "component") == "P") &&
                ambiguousRows.Any(r => Get(r, "id") == "12" && Get(r, "component") == "S"),
                "Shared proper-name alias cannot force an identity; fall back to the original positional core");
            string[] dmPrimary = Csv.Parse(Star(19, 4.0 / 54000, "", 5, 0));
            dmPrimary[Array.IndexOf(header, "hd")] = "9877";
            File.AppendAllLines(Path.Combine(athyg, "athyg_40.csv"), [
                Csv.Line(dmPrimary), Star(20, 1.0 / 54000, "", 7, 0), Star(21, 14.0 / 54000, "", 7, 0)
            ]);
            "  9877".CopyTo(0, hd, 0, 6);
            "BD+00  124".PadRight(12).CopyTo(0, hd, 6, 12);
            File.AppendAllText(Path.Combine(temp, "Henry Draper", "catalog.dat"), new string(hd) + Environment.NewLine);
            File.AppendAllText(Path.Combine(wds, "wds_precise.txt"),
                PairRow("00000+0000", "AB", 10, dm: "+00  124") + Environment.NewLine);
            new Builder(temp, output).Run();
            Validation.Run(temp, output);
            var dmRows = File.ReadLines(Path.Combine(output, "oms_catalog.csv")).Skip(1).Select(Csv.Parse).ToArray();
            Check(dmRows.Any(r => Get(r, "id") == "19" && Get(r, "component") == "P") &&
                dmRows.Any(r => Get(r, "id") == "20" && Get(r, "component") == "S") &&
                dmRows.Any(r => Get(r, "id") == "21" && Get(r, "component_label") == "B"),
                "Unique magnitude-compatible DM-to-HD match identifies and anchors the primary before positional assignment");
            Console.WriteLine("Catalog fixtures passed: BDS aliases, narrative evidence, AT-HYG motion precedence, Visual classification, crowded/wide systems, unknown geometry, shared Gaia and P conversion.");
        }
        finally
        {
            Directory.Delete(temp, true);
        }
    }
}
