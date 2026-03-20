namespace pallet_algoritms
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var items = DemoData.BuildItems();
            var pallets = DemoData.BuildPallets();

            var rule = new Rule
            {
                IsMixedStock = true,
                LotTypeId = 4 // 1=ProdDate, 2=ExpDate, 3=ReceiptDate, 4=ProdBatchNo
            };

            var config = PackingConfig.Default();
            var algorithm = new PalletsAlgorithms(config);

            var result = algorithm.BestPaalletFit(items, pallets, rule);

            Console.WriteLine("===== USED PALLETS =====");
            foreach (var pallet in result.OrderBy(x => x.PalletId))
            {
                Console.WriteLine($"PalletId={pallet.PalletId}, Type={pallet.PalletType}");
                Console.WriteLine($"  UsedVolume = {pallet.Valume:N2}/{pallet.PalletVolume:N2}");
                Console.WriteLine($"  UsedWeight = {pallet.Weight:N2}/{pallet.PalletWeight:N2}");
                Console.WriteLine($"  UsedHeight = {pallet.Height:N2}/{pallet.PalletHeight:N2}");
                Console.WriteLine($"  ItemsCount = {pallet.Items.Count}");
                Console.WriteLine($"  Placements = {pallet.Placements.Count}");

                foreach (var g in pallet.Items
                    .GroupBy(i => new { i.ItemNo, i.ItemType, i.ProdBatchNo, i.ProdDate, i.ExpDate, i.ReceiptDate })
                    .OrderBy(g => g.Key.ItemNo))
                {
                    Console.WriteLine(
                        $"    ItemNo={g.Key.ItemNo}, Qty={g.Count()}, Type={g.Key.ItemType}, Batch={g.Key.ProdBatchNo}, " +
                        $"Prod={g.Key.ProdDate:yyyy-MM-dd}, Exp={g.Key.ExpDate:yyyy-MM-dd}, Rec={g.Key.ReceiptDate:yyyy-MM-dd}");
                }

                Console.WriteLine("  First placements:");
                foreach (var p in pallet.Placements.Take(5))
                {
                    Console.WriteLine($"    ItemNo={p.ItemNo}, X={p.X}, Y={p.Y}, Z={p.Z}, L={p.Length}, W={p.Width}, H={p.Height}");
                }

                Console.WriteLine();
            }

            Console.WriteLine("===== UNPLACED ITEMS =====");
            foreach (var u in algorithm.LastUnplacedItems
                .GroupBy(x => new { x.ItemNo, x.ItemType, x.ProdBatchNo, x.ProdDate, x.ExpDate, x.ReceiptDate })
                .OrderBy(g => g.Key.ItemNo))
            {
                Console.WriteLine(
                    $"ItemNo={u.Key.ItemNo}, Qty={u.Count()}, Type={u.Key.ItemType}, Batch={u.Key.ProdBatchNo}, " +
                    $"Prod={u.Key.ProdDate:yyyy-MM-dd}, Exp={u.Key.ExpDate:yyyy-MM-dd}, Rec={u.Key.ReceiptDate:yyyy-MM-dd}");
            }

            Console.WriteLine();
            Console.WriteLine("===== REJECTION SUMMARY =====");
            foreach (var kv in algorithm.LastRejectStatistics.OrderByDescending(x => x.Value))
            {
                Console.WriteLine($"{kv.Key}: {kv.Value}");
            }
        }
    }

    #region Contracts / Input Models

    record Items
    {
        public int ItemNo { get; set; }
        public decimal Weight { get; set; }
        public decimal Volume { get; set; }
        public decimal Length { get; set; }
        public decimal Width { get; set; }
        public decimal Height { get; set; }
        public int Quantity { get; set; }
        public int ItemType { get; set; }
        public DateOnly ProdDate { get; set; }
        public DateOnly ExpDate { get; set; }
        public DateOnly ReceiptDate { get; set; }
        public string ProdBatchNo { get; set; } = "";
        public int TrxB_id { get; set; }
        public string CDO_name { get; set; } = "";

        public int LengthQual_id { get; set; }
        public int VolumeQual_id { get; set; }
        public int WeightQual_id { get; set; }
    }

    record Pallets
    {
        public int PalletId { get; set; }
        public int PalletType { get; set; }
        public decimal MaxValume { get; set; }
        public decimal MaxHeight { get; set; }
        public decimal MaxWeight { get; set; }

        public decimal Lenght { get; set; }
        public decimal Width { get; set; }

        public decimal UseValume { get; set; }
        public decimal UseHeight { get; set; }
        public decimal UseWeight { get; set; }

        public int LengthQual_id { get; set; }
        public int VolumeQual_id { get; set; }
        public int WeightQual_id { get; set; }

        // Optional: pallet ichida oldindan aniq geometry bo'lsa shu yerga beriladi
        public List<ExistingPlacementInput> ExistingPlacements { get; set; } = [];
    }

    record ExistingPlacementInput
    {
        public int ItemNo { get; set; }
        public decimal Weight { get; set; }
        public decimal X { get; set; }
        public decimal Y { get; set; }
        public decimal Z { get; set; }
        public decimal Length { get; set; }
        public decimal Width { get; set; }
        public decimal Height { get; set; }
    }

    record Rule
    {
        public bool IsMixedStock { get; set; }
        public int LotTypeId { get; set; }
    }

    record SelectedPalletResult
    {
        public int PalletId { get; set; }
        public int PalletType { get; set; }

        public decimal Valume { get; set; }
        public decimal PalletVolume { get; set; }

        public decimal PalletWeight { get; set; }
        public decimal Weight { get; set; }

        public decimal PalletHeight { get; set; }
        public decimal Height { get; set; }

        public decimal Length { get; set; }
        public decimal Width { get; set; }

        public int LengthQual_id { get; set; }
        public int VolumeQual_id { get; set; }
        public int WeightQual_id { get; set; }

        public List<Items> Items { get; set; } = [];
        public List<PlacedItemResult> Placements { get; set; } = [];
        public List<string> DebugNotes { get; set; } = [];
    }

    record PlacedItemResult
    {
        public int ItemNo { get; set; }
        public decimal X { get; set; }
        public decimal Y { get; set; }
        public decimal Z { get; set; }
        public decimal Length { get; set; }
        public decimal Width { get; set; }
        public decimal Height { get; set; }
        public decimal Weight { get; set; }
    }

    #endregion

    #region Public Entry Class

    class PalletsAlgorithms
    {
        private readonly PackingEngine _engine;
        public List<Items> LastUnplacedItems { get; private set; } = [];
        public Dictionary<string, int> LastRejectStatistics { get; private set; } = [];

        public PalletsAlgorithms(PackingConfig? config = null)
        {
            _engine = new PackingEngine(config ?? PackingConfig.Default());
        }

        public List<SelectedPalletResult> BestPaalletFit(List<Items> items, List<Pallets> pallets, Rule rule)
        {
            var result = _engine.Run(items, pallets, rule);
            LastUnplacedItems = result.UnplacedItems;
            LastRejectStatistics = result.RejectStatistics;
            return result.UsedPallets;
        }
    }

    #endregion

    #region Engine

    internal class PackingEngine
    {
        private readonly PackingConfig _config;
        private readonly InputNormalizer _normalizer;
        private readonly GroupingService _groupingService;
        private readonly OrientationService _orientationService;
        private readonly CandidatePointService _candidatePointService;
        private readonly PlacementValidator _validator;
        private readonly ScoringService _scoringService;
        private readonly LocalOptimizationService _localOptimizationService;

        public PackingEngine(PackingConfig config)
        {
            _config = config;
            _normalizer = new InputNormalizer();
            _groupingService = new GroupingService();
            _orientationService = new OrientationService();
            _candidatePointService = new CandidatePointService(config);
            _validator = new PlacementValidator(config);
            _scoringService = new ScoringService(config);
            _localOptimizationService = new LocalOptimizationService(config, _orientationService, _candidatePointService, _validator, _scoringService);
        }

        public PackingRunResult Run(List<Items> items, List<Pallets> pallets, Rule rule)
        {
            if (items == null || items.Count == 0)
                return new PackingRunResult();

            if (pallets == null || pallets.Count == 0)
            {
                return new PackingRunResult
                {
                    UnplacedItems = _normalizer.ExpandItems(items)
                };
            }

            InputValidator.ValidateItems(items);
            InputValidator.ValidatePallets(pallets);

            var normalizedItems = _normalizer.NormalizeItems(items);
            var unitItems = _normalizer.ExpandItems(normalizedItems);
            var groups = _groupingService.BuildPlacementGroups(unitItems, rule);

            var statePallets = pallets
                .OrderBy(p => p.MaxValume)
                .ThenBy(p => p.MaxHeight)
                .ThenBy(p => p.MaxWeight)
                .ThenBy(p => p.Lenght * p.Width)
                .Select(p => WorkingPallet.CreateFromInput(p, _config))
                .ToList();

            var unplaced = new List<Items>();
            var rejectStats = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in groups)
            {
                var orderedUnits = group.Units
                    .OrderByDescending(x => x.Volume)
                    .ThenByDescending(x => Math.Max(x.Length, Math.Max(x.Width, x.Height)))
                    .ThenByDescending(x => x.Weight)
                    .ThenBy(x => x.ItemNo)
                    .ToList();

                foreach (var item in orderedUnits)
                {
                    PlacementCandidate? bestCandidate = null;
                    var itemRejects = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                    foreach (var pallet in statePallets)
                    {
                        if (pallet.IsClosed)
                            continue;

                        if (!BusinessRuleService.CanPalletAcceptGroup(pallet, group, rule))
                            continue;

                        if (pallet.TotalWeight + item.Weight > pallet.Template.MaxWeight)
                        {
                            AddReject(itemRejects, RejectReason.WeightCapacity);
                            continue;
                        }

                        if (pallet.Template.UseValume + item.Volume > pallet.Template.MaxValume)
                        {
                            AddReject(itemRejects, RejectReason.VolumeCapacity);
                            continue;
                        }

                        var candidate = FindBestPlacement(item, pallet, group, rule, itemRejects);
                        if (candidate == null)
                            continue;

                        if (bestCandidate == null || candidate.Score < bestCandidate.Score)
                            bestCandidate = candidate;
                    }

                    if (bestCandidate == null)
                    {
                        unplaced.Add(item);
                        foreach (var kv in itemRejects)
                        {
                            if (!rejectStats.ContainsKey(kv.Key))
                                rejectStats[kv.Key] = 0;

                            rejectStats[kv.Key] += kv.Value;
                        }
                        continue;
                    }

                    BusinessRuleService.LockPalletForGroup(bestCandidate.Pallet, group, rule, item);
                    ApplyPlacement(bestCandidate);

                    if (ShouldClosePallet(bestCandidate.Pallet))
                        bestCandidate.Pallet.IsClosed = true;
                }
            }

            _localOptimizationService.Optimize(statePallets, rule, rejectStats);

            return new PackingRunResult
            {
                UsedPallets = statePallets
                    .Where(p => p.PlacedItems.Count > 0)
                    .OrderBy(p => p.Template.PalletId)
                    .Select(ToResult)
                    .ToList(),
                UnplacedItems = unplaced,
                RejectStatistics = rejectStats
            };
        }

        private PlacementCandidate? FindBestPlacement(
            Items item,
            WorkingPallet pallet,
            PlacementGroup group,
            Rule rule,
            Dictionary<string, int> rejectCollector)
        {
            PlacementCandidate? best = null;

            foreach (var orientation in _orientationService.GetAllowedOrientations(item))
            {
                if (orientation.L > pallet.Template.Lenght ||
                    orientation.W > pallet.Template.Width ||
                    orientation.H > pallet.AvailableHeight)
                {
                    AddReject(rejectCollector, RejectReason.DimensionOverflow);
                    continue;
                }

                foreach (var point in _candidatePointService.GetCandidatePoints(pallet))
                {
                    var placement = new Placement3D
                    {
                        Item = item,
                        X = point.X,
                        Y = point.Y,
                        Z = point.Z,
                        L = orientation.L,
                        W = orientation.W,
                        H = orientation.H
                    };

                    var validation = _validator.ValidatePlacement(placement, pallet);
                    if (!validation.IsValid)
                    {
                        AddReject(rejectCollector, validation.Reason);
                        continue;
                    }

                    var score = _scoringService.CalculateScore(placement, pallet);

                    var candidate = new PlacementCandidate
                    {
                        Pallet = pallet,
                        Placement = placement,
                        Score = score,
                        DecisionNote = validation.Note
                    };

                    if (best == null || candidate.Score < best.Score)
                        best = candidate;
                }
            }

            return best;
        }

        private static void ApplyPlacement(PlacementCandidate candidate)
        {
            var pallet = candidate.Pallet;
            var placement = candidate.Placement;

            pallet.AddPlacement(placement, isPreloaded: false, debugNote: candidate.DecisionNote);
        }

        private bool ShouldClosePallet(WorkingPallet pallet)
        {
            if (pallet.Template.UseValume >= pallet.Template.MaxValume)
                return true;

            if (pallet.Template.UseWeight >= pallet.Template.MaxWeight)
                return true;

            if (pallet.CurrentTop >= pallet.Template.MaxHeight)
                return true;

            if (!_candidatePointService.GetCandidatePoints(pallet).Any())
                return true;

            return false;
        }

        private static void AddReject(Dictionary<string, int> collector, RejectReason reason)
        {
            var key = reason.ToString();
            if (!collector.ContainsKey(key))
                collector[key] = 0;

            collector[key]++;
        }

        private static SelectedPalletResult ToResult(WorkingPallet pallet)
        {
            return new SelectedPalletResult
            {
                PalletId = pallet.Template.PalletId,
                PalletType = pallet.Template.PalletType,
                Valume = pallet.Template.UseValume,
                PalletVolume = pallet.Template.MaxValume,
                PalletWeight = pallet.Template.MaxWeight,
                Weight = pallet.Template.UseWeight,
                PalletHeight = pallet.Template.MaxHeight,
                Height = pallet.CurrentTop,
                Length = pallet.Template.Lenght,
                Width = pallet.Template.Width,
                LengthQual_id = pallet.Template.LengthQual_id,
                VolumeQual_id = pallet.Template.VolumeQual_id,
                WeightQual_id = pallet.Template.WeightQual_id,
                Items = pallet.PlacedItems.ToList(),
                Placements = pallet.Placements
                    .Where(x => !x.IsPreloaded)
                    .Select(x => new PlacedItemResult
                    {
                        ItemNo = x.Item.ItemNo,
                        X = x.X,
                        Y = x.Y,
                        Z = x.Z,
                        Length = x.L,
                        Width = x.W,
                        Height = x.H,
                        Weight = x.Item.Weight
                    })
                    .ToList(),
                DebugNotes = pallet.DebugNotes.ToList()
            };
        }
    }

    internal class PackingRunResult
    {
        public List<SelectedPalletResult> UsedPallets { get; set; } = [];
        public List<Items> UnplacedItems { get; set; } = [];
        public Dictionary<string, int> RejectStatistics { get; set; } = [];
    }

    #endregion

    #region Config

    internal class PackingConfig
    {
        public decimal MinSupportAreaRatio { get; set; } = 0.85m;
        public decimal CogMarginLengthRatio { get; set; } = 0.10m;
        public decimal CogMarginWidthRatio { get; set; } = 0.10m;
        public decimal MaxCogHeightRatio { get; set; } = 0.78m;
        public decimal ForkliftSideMarginRatio { get; set; } = 0.08m;
        public decimal ForkliftFrontBackMarginRatio { get; set; } = 0.10m;

        public decimal SpatialCellSizeXY { get; set; } = 20m;
        public decimal SpatialCellSizeZ { get; set; } = 20m;

        public int MaxCandidatePointsPerPallet { get; set; } = 120;
        public int LocalOptimizationMaxRounds { get; set; } = 2;

        public static PackingConfig Default() => new();
    }

    #endregion

    #region Validation / Normalization

    internal static class InputValidator
    {
        public static void ValidateItems(List<Items> items)
        {
            foreach (var i in items)
            {
                if (i.Quantity <= 0)
                    throw new ArgumentException($"ItemNo={i.ItemNo}: Quantity > 0 bo'lishi kerak.");

                if (i.Length <= 0 || i.Width <= 0 || i.Height <= 0)
                    throw new ArgumentException($"ItemNo={i.ItemNo}: Length/Width/Height > 0 bo'lishi kerak.");

                if (i.Weight < 0)
                    throw new ArgumentException($"ItemNo={i.ItemNo}: Weight manfiy bo'lishi mumkin emas.");
            }
        }

        public static void ValidatePallets(List<Pallets> pallets)
        {
            foreach (var p in pallets)
            {
                if (p.Lenght <= 0 || p.Width <= 0 || p.MaxHeight <= 0 || p.MaxValume <= 0 || p.MaxWeight <= 0)
                    throw new ArgumentException($"PalletId={p.PalletId}: capacity noto'g'ri.");

                if (p.UseValume < 0 || p.UseHeight < 0 || p.UseWeight < 0)
                    throw new ArgumentException($"PalletId={p.PalletId}: UseValume/UseHeight/UseWeight manfiy bo'lishi mumkin emas.");

                if (p.UseValume > p.MaxValume || p.UseHeight > p.MaxHeight || p.UseWeight > p.MaxWeight)
                    throw new ArgumentException($"PalletId={p.PalletId}: ishlatilgan capacity max dan katta.");
            }
        }
    }

    internal class InputNormalizer
    {
        public List<Items> NormalizeItems(List<Items> items)
        {
            return items
                .GroupBy(x => new
                {
                    x.ItemNo,
                    x.Weight,
                    x.Length,
                    x.Width,
                    x.Height,
                    x.ItemType,
                    x.ProdDate,
                    x.ExpDate,
                    x.ReceiptDate,
                    x.ProdBatchNo,
                    x.TrxB_id,
                    x.CDO_name,
                    x.LengthQual_id,
                    x.VolumeQual_id,
                    x.WeightQual_id
                })
                .Select(g =>
                {
                    var first = g.First();
                    return new Items
                    {
                        ItemNo = first.ItemNo,
                        Weight = first.Weight,
                        Volume = first.Volume > 0 ? first.Volume : first.Length * first.Width * first.Height,
                        Length = first.Length,
                        Width = first.Width,
                        Height = first.Height,
                        Quantity = g.Sum(x => x.Quantity),
                        ItemType = first.ItemType,
                        ProdDate = first.ProdDate,
                        ExpDate = first.ExpDate,
                        ReceiptDate = first.ReceiptDate,
                        ProdBatchNo = first.ProdBatchNo,
                        TrxB_id = first.TrxB_id,
                        CDO_name = first.CDO_name,
                        LengthQual_id = first.LengthQual_id,
                        VolumeQual_id = first.VolumeQual_id,
                        WeightQual_id = first.WeightQual_id
                    };
                })
                .ToList();
        }

        public List<Items> ExpandItems(List<Items> items)
        {
            var result = new List<Items>();

            foreach (var item in items)
            {
                var volume = item.Volume > 0 ? item.Volume : item.Length * item.Width * item.Height;

                for (int i = 0; i < item.Quantity; i++)
                {
                    result.Add(new Items
                    {
                        ItemNo = item.ItemNo,
                        Weight = item.Weight,
                        Volume = volume,
                        Length = item.Length,
                        Width = item.Width,
                        Height = item.Height,
                        Quantity = 1,
                        ItemType = item.ItemType,
                        ProdDate = item.ProdDate,
                        ExpDate = item.ExpDate,
                        ReceiptDate = item.ReceiptDate,
                        ProdBatchNo = item.ProdBatchNo,
                        TrxB_id = item.TrxB_id,
                        CDO_name = item.CDO_name,
                        LengthQual_id = item.LengthQual_id,
                        VolumeQual_id = item.VolumeQual_id,
                        WeightQual_id = item.WeightQual_id
                    });
                }
            }

            return result;
        }
    }

    #endregion

    #region Grouping / Business Rules

    internal class GroupingService
    {
        public List<PlacementGroup> BuildPlacementGroups(List<Items> unitItems, Rule rule)
        {
            if (!rule.IsMixedStock)
            {
                return unitItems
                    .GroupBy(x => $"ITEMNO:{x.ItemNo}")
                    .Select(g => new PlacementGroup
                    {
                        GroupKey = g.Key,
                        Units = g.ToList(),
                        LockedItemNo = g.First().ItemNo,
                        MixedMode = false
                    })
                    .OrderByDescending(g => g.Units.Sum(x => x.Volume))
                    .ToList();
            }

            var lotItems = unitItems.Where(x => x.ItemType == 2).ToList();
            var freeItems = unitItems.Where(x => x.ItemType != 2).ToList();

            var groups = new List<PlacementGroup>();

            foreach (var lotGroup in lotItems.GroupBy(x => $"LOT:{BusinessRuleService.GetLotKey(x, rule)}"))
            {
                groups.Add(new PlacementGroup
                {
                    GroupKey = lotGroup.Key,
                    Units = lotGroup.ToList(),
                    LockedLotKey = BusinessRuleService.GetLotKey(lotGroup.First(), rule),
                    MixedMode = true
                });
            }

            if (freeItems.Count > 0)
            {
                groups.Add(new PlacementGroup
                {
                    GroupKey = "MIXED:FREE",
                    Units = freeItems,
                    MixedMode = true
                });
            }

            return groups
                .OrderByDescending(g => g.Units.Sum(x => x.Volume))
                .ToList();
        }
    }

    internal static class BusinessRuleService
    {
        public static bool CanPalletAcceptGroup(WorkingPallet pallet, PlacementGroup group, Rule rule)
        {
            if (!rule.IsMixedStock)
            {
                if (pallet.LockedItemNo == null)
                    return true;

                return pallet.LockedItemNo == group.LockedItemNo;
            }

            if (group.LockedLotKey == null)
                return true;

            if (pallet.LockedLotKey == null)
                return true;

            return pallet.LockedLotKey == group.LockedLotKey;
        }

        public static void LockPalletForGroup(WorkingPallet pallet, PlacementGroup group, Rule rule, Items item)
        {
            if (!rule.IsMixedStock)
            {
                pallet.LockedItemNo ??= group.LockedItemNo;
                return;
            }

            if (item.ItemType == 2)
            {
                pallet.LockedLotKey ??= GetLotKey(item, rule);
            }
        }

        public static string GetLotKey(Items item, Rule rule)
        {
            return rule.LotTypeId switch
            {
                1 => $"PROD:{item.ProdDate:yyyyMMdd}",
                2 => $"EXP:{item.ExpDate:yyyyMMdd}",
                3 => $"REC:{item.ReceiptDate:yyyyMMdd}",
                4 => $"BATCH:{item.ProdBatchNo}",
                _ => $"BATCH:{item.ProdBatchNo}"
            };
        }
    }

    #endregion

    #region Orientation / Candidate Points

    internal class OrientationService
    {
        public IEnumerable<Orientation> GetAllowedOrientations(Items item)
        {
            // Faqat chap/o'ng aylantirish.
            // Height o'zgarmaydi.
            return new[]
            {
                new Orientation(item.Length, item.Width, item.Height),
                new Orientation(item.Width, item.Length, item.Height)
            }
            .GroupBy(x => $"{x.L:F4}|{x.W:F4}|{x.H:F4}")
            .Select(g => g.First())
            .OrderByDescending(x => x.L * x.W)
            .ToList();
        }
    }

    internal class CandidatePointService
    {
        private readonly PackingConfig _config;

        public CandidatePointService(PackingConfig config)
        {
            _config = config;
        }

        public IEnumerable<Point3D> GetCandidatePoints(WorkingPallet pallet)
        {
            var points = new List<Point3D>();

            if (pallet.Placements.Count == 0)
            {
                points.Add(new Point3D(0, 0, pallet.BaseZ));
            }
            else
            {
                foreach (var p in pallet.Placements)
                {
                    points.Add(new Point3D(p.Right, p.Y, p.Z));
                    points.Add(new Point3D(p.X, p.Back, p.Z));
                    points.Add(new Point3D(p.X, p.Y, p.Top));
                }

                // base z dagi origin point ba'zan foydali bo'ladi
                points.Add(new Point3D(0, 0, pallet.BaseZ));
            }

            var dedup = points
                .Where(pt => pt.X >= 0 && pt.Y >= 0 && pt.Z >= pallet.BaseZ)
                .Where(pt => pt.X <= pallet.Template.Lenght && pt.Y <= pallet.Template.Width && pt.Z <= pallet.Template.MaxHeight)
                .GroupBy(pt => $"{pt.X:F4}|{pt.Y:F4}|{pt.Z:F4}")
                .Select(g => g.First());

            // Dominated point pruning:
            // agar boshqa point hamma o'qda <= bo'lsa, undan yomon pointni tashlaymiz
            var list = dedup
                .OrderBy(pt => pt.Z)
                .ThenBy(pt => pt.Y)
                .ThenBy(pt => pt.X)
                .ToList();

            var pruned = new List<Point3D>();
            foreach (var p in list)
            {
                bool dominated = pruned.Any(q => q.X <= p.X && q.Y <= p.Y && q.Z <= p.Z);
                if (!dominated)
                    pruned.Add(p);
            }

            return pruned
                .Take(_config.MaxCandidatePointsPerPallet)
                .ToList();
        }
    }

    #endregion

    #region Validator / Physics

    internal class PlacementValidator
    {
        private readonly PackingConfig _config;

        public PlacementValidator(PackingConfig config)
        {
            _config = config;
        }

        public ValidationResult ValidatePlacement(Placement3D placement, WorkingPallet pallet)
        {
            if (placement.X < 0 || placement.Y < 0 || placement.Z < pallet.BaseZ)
                return ValidationResult.Fail(RejectReason.OutOfBounds, "Negative or below base.");

            if (placement.Right > pallet.Template.Lenght ||
                placement.Back > pallet.Template.Width ||
                placement.Top > pallet.Template.MaxHeight)
                return ValidationResult.Fail(RejectReason.OutOfBounds, "Exceeds pallet dimensions.");

            if (pallet.Template.UseValume + placement.Item.Volume > pallet.Template.MaxValume)
                return ValidationResult.Fail(RejectReason.VolumeCapacity, "Volume overflow.");

            if (pallet.TotalWeight + placement.Item.Weight > pallet.Template.MaxWeight)
                return ValidationResult.Fail(RejectReason.WeightCapacity, "Weight overflow.");

            var overlapCandidates = pallet.SpatialIndex.Query(placement);
            foreach (var existing in overlapCandidates)
            {
                if (GeometryService.BoxesOverlap(placement, existing))
                    return ValidationResult.Fail(RejectReason.Overlap, "Overlap detected.");
            }

            if (!HasEnoughSupport(placement, pallet, out string supportNote))
                return ValidationResult.Fail(RejectReason.Support, supportNote);

            if (!PassCenterOfGravityRule(placement, pallet, out string cogNote))
                return ValidationResult.Fail(RejectReason.CenterOfGravity, cogNote);

            if (!PassForkliftRule(placement, pallet, out string forkliftNote))
                return ValidationResult.Fail(RejectReason.Forklift, forkliftNote);

            return ValidationResult.Ok("Placement valid.");
        }

        private bool HasEnoughSupport(Placement3D placement, WorkingPallet pallet, out string note)
        {
            if (placement.Z == pallet.BaseZ)
            {
                note = "On base.";
                return true;
            }

            decimal baseArea = placement.L * placement.W;
            if (baseArea <= 0)
            {
                note = "Invalid base area.";
                return false;
            }

            decimal supportedArea = 0m;

            if (pallet.TopSurfaceIndex.TryGetValue(placement.Z, out var surfaces))
            {
                foreach (var lower in surfaces)
                {
                    decimal overlapX = Math.Min(placement.Right, lower.Right) - Math.Max(placement.X, lower.X);
                    decimal overlapY = Math.Min(placement.Back, lower.Back) - Math.Max(placement.Y, lower.Y);

                    if (overlapX > 0 && overlapY > 0)
                        supportedArea += overlapX * overlapY;
                }
            }

            var ratio = supportedArea / baseArea;
            note = $"Support ratio={ratio:N2}";

            return ratio >= _config.MinSupportAreaRatio;
        }

        private bool PassCenterOfGravityRule(Placement3D candidate, WorkingPallet pallet, out string note)
        {
            decimal totalWeight = pallet.TotalWeight + candidate.Item.Weight;
            if (totalWeight <= 0)
            {
                note = "No weight.";
                return true;
            }

            decimal sx = pallet.PrecomputedWeightedCenterSumX + (candidate.CenterX * candidate.Item.Weight);
            decimal sy = pallet.PrecomputedWeightedCenterSumY + (candidate.CenterY * candidate.Item.Weight);
            decimal sz = pallet.PrecomputedWeightedCenterSumZ + (candidate.CenterZ * candidate.Item.Weight);

            decimal cogX = sx / totalWeight;
            decimal cogY = sy / totalWeight;
            decimal cogZ = sz / totalWeight;

            decimal marginL = pallet.Template.Lenght * _config.CogMarginLengthRatio;
            decimal marginW = pallet.Template.Width * _config.CogMarginWidthRatio;
            decimal totalHeightAfter = Math.Max(pallet.CurrentTop, candidate.Top);

            note = $"COG=({cogX:N2},{cogY:N2},{cogZ:N2})";

            if (cogX < marginL || cogX > pallet.Template.Lenght - marginL)
                return false;

            if (cogY < marginW || cogY > pallet.Template.Width - marginW)
                return false;

            if (totalHeightAfter > 0 && cogZ > totalHeightAfter * _config.MaxCogHeightRatio)
                return false;

            return true;
        }

        private bool PassForkliftRule(Placement3D candidate, WorkingPallet pallet, out string note)
        {
            decimal totalWeight = pallet.TotalWeight + candidate.Item.Weight;
            if (totalWeight <= 0)
            {
                note = "No weight.";
                return true;
            }

            decimal sx = pallet.PrecomputedWeightedCenterSumX + (candidate.CenterX * candidate.Item.Weight);
            decimal sy = pallet.PrecomputedWeightedCenterSumY + (candidate.CenterY * candidate.Item.Weight);

            decimal cogX = sx / totalWeight;
            decimal cogY = sy / totalWeight;

            decimal frontBackMargin = pallet.Template.Lenght * _config.ForkliftFrontBackMarginRatio;
            decimal sideMargin = pallet.Template.Width * _config.ForkliftSideMarginRatio;

            note = $"Forklift COG corridor check=({cogX:N2},{cogY:N2})";

            if (cogX < frontBackMargin || cogX > pallet.Template.Lenght - frontBackMargin)
                return false;

            if (cogY < sideMargin || cogY > pallet.Template.Width - sideMargin)
                return false;

            return true;
        }
    }

    internal static class GeometryService
    {
        public static bool BoxesOverlap(Placement3D a, Placement3D b)
        {
            bool xOverlap = a.X < b.Right && a.Right > b.X;
            bool yOverlap = a.Y < b.Back && a.Back > b.Y;
            bool zOverlap = a.Z < b.Top && a.Top > b.Z;
            return xOverlap && yOverlap && zOverlap;
        }
    }

    internal enum RejectReason
    {
        OutOfBounds,
        DimensionOverflow,
        VolumeCapacity,
        WeightCapacity,
        Overlap,
        Support,
        CenterOfGravity,
        Forklift
    }

    internal class ValidationResult
    {
        public bool IsValid { get; private set; }
        public RejectReason Reason { get; private set; }
        public string Note { get; private set; } = "";

        public static ValidationResult Ok(string note) =>
            new ValidationResult { IsValid = true, Note = note };

        public static ValidationResult Fail(RejectReason reason, string note) =>
            new ValidationResult { IsValid = false, Reason = reason, Note = note };
    }

    #endregion

    #region Scoring

    internal class ScoringService
    {
        private readonly PackingConfig _config;

        public ScoringService(PackingConfig config)
        {
            _config = config;
        }

        public decimal CalculateScore(Placement3D placement, WorkingPallet pallet)
        {
            decimal newTop = Math.Max(pallet.CurrentTop, placement.Top);
            decimal remainVolume = pallet.Template.MaxValume - (pallet.Template.UseValume + placement.Item.Volume);

            decimal centerX = pallet.Template.Lenght / 2m;
            decimal centerY = pallet.Template.Width / 2m;

            decimal distToCenterPenalty =
                Math.Abs(placement.CenterX - centerX) +
                Math.Abs(placement.CenterY - centerY);

            decimal zPenalty = placement.Z * 500m;
            decimal topPenalty = newTop * 100m;
            decimal remainPenalty = remainVolume * 0.00001m;

            return zPenalty + topPenalty + distToCenterPenalty + remainPenalty;
        }
    }

    #endregion

    #region Local Optimization

    internal class LocalOptimizationService
    {
        private readonly PackingConfig _config;
        private readonly OrientationService _orientationService;
        private readonly CandidatePointService _candidatePointService;
        private readonly PlacementValidator _validator;
        private readonly ScoringService _scoringService;

        public LocalOptimizationService(
            PackingConfig config,
            OrientationService orientationService,
            CandidatePointService candidatePointService,
            PlacementValidator validator,
            ScoringService scoringService)
        {
            _config = config;
            _orientationService = orientationService;
            _candidatePointService = candidatePointService;
            _validator = validator;
            _scoringService = scoringService;
        }

        public void Optimize(List<WorkingPallet> pallets, Rule rule, Dictionary<string, int> rejectStats)
        {
            for (int round = 0; round < _config.LocalOptimizationMaxRounds; round++)
            {
                bool improved = TryMergeLeastUtilizedPallet(pallets, rule, rejectStats);
                if (!improved)
                    break;
            }
        }

        private bool TryMergeLeastUtilizedPallet(List<WorkingPallet> pallets, Rule rule, Dictionary<string, int> rejectStats)
        {
            var source = pallets
                .Where(p => p.PlacedItems.Count > 0)
                .OrderBy(p => p.Template.UseValume / p.Template.MaxValume)
                .ThenBy(p => p.PlacedItems.Count)
                .FirstOrDefault();

            if (source == null)
                return false;

            var movable = source.Placements.Where(p => !p.IsPreloaded).OrderByDescending(x => x.Item.Volume).ToList();
            if (movable.Count == 0)
                return false;

            var targetCandidates = pallets.Where(p => p != source).ToList();
            var moved = new List<(Placement3D OldPlacement, WorkingPallet Target, Placement3D NewPlacement)>();

            foreach (var oldPlacement in movable)
            {
                PlacementCandidate? best = null;

                foreach (var target in targetCandidates)
                {
                    if (target.IsClosed)
                        continue;

                    if (!CanMoveBetweenPallets(source, target, oldPlacement.Item, rule))
                        continue;

                    foreach (var orientation in _orientationService.GetAllowedOrientations(oldPlacement.Item))
                    {
                        if (orientation.L > target.Template.Lenght ||
                            orientation.W > target.Template.Width ||
                            orientation.H > target.AvailableHeight)
                            continue;

                        foreach (var point in _candidatePointService.GetCandidatePoints(target))
                        {
                            var candidatePlacement = new Placement3D
                            {
                                Item = oldPlacement.Item,
                                X = point.X,
                                Y = point.Y,
                                Z = point.Z,
                                L = orientation.L,
                                W = orientation.W,
                                H = orientation.H
                            };

                            var validation = _validator.ValidatePlacement(candidatePlacement, target);
                            if (!validation.IsValid)
                                continue;

                            var score = _scoringService.CalculateScore(candidatePlacement, target);
                            var cand = new PlacementCandidate
                            {
                                Pallet = target,
                                Placement = candidatePlacement,
                                Score = score,
                                DecisionNote = "LocalOptimization"
                            };

                            if (best == null || cand.Score < best.Score)
                                best = cand;
                        }
                    }
                }

                if (best == null)
                {
                    // rollback
                    foreach (var m in moved)
                    {
                        m.Target.RemovePlacement(m.NewPlacement);
                        source.AddPlacement(m.OldPlacement, isPreloaded: false, debugNote: "Rollback");
                    }
                    return false;
                }

                source.RemovePlacement(oldPlacement);
                best.Pallet.AddPlacement(best.Placement, isPreloaded: false, debugNote: "LocalOptimization");
                moved.Add((oldPlacement, best.Pallet, best.Placement));
            }

            // source bo'shagan bo'lsa uni yopamiz
            source.IsClosed = true;
            source.DebugNotes.Add("Merged away by local optimization.");
            return true;
        }

        private bool CanMoveBetweenPallets(WorkingPallet source, WorkingPallet target, Items item, Rule rule)
        {
            if (!rule.IsMixedStock)
            {
                if (target.LockedItemNo == null)
                    return true;

                return target.LockedItemNo == item.ItemNo;
            }

            if (item.ItemType != 2)
                return true;

            var lotKey = BusinessRuleService.GetLotKey(item, rule);
            if (target.LockedLotKey == null)
                return true;

            return target.LockedLotKey == lotKey;
        }
    }

    #endregion

    #region State / Spatial Index

    internal class WorkingPallet
    {
        public Pallets Template { get; }
        public bool IsClosed { get; set; }
        public int? LockedItemNo { get; set; }
        public string? LockedLotKey { get; set; }

        public List<Placement3D> Placements { get; } = [];
        public List<Items> PlacedItems { get; } = [];
        public List<string> DebugNotes { get; } = [];

        public SpatialHash3D SpatialIndex { get; }
        public Dictionary<decimal, List<Placement3D>> TopSurfaceIndex { get; } = [];

        public decimal PrecomputedWeightedCenterSumX { get; private set; }
        public decimal PrecomputedWeightedCenterSumY { get; private set; }
        public decimal PrecomputedWeightedCenterSumZ { get; private set; }

        private readonly PackingConfig _config;

        private WorkingPallet(Pallets pallet, PackingConfig config)
        {
            Template = pallet;
            _config = config;
            SpatialIndex = new SpatialHash3D(config);
        }

        public static WorkingPallet CreateFromInput(Pallets pallet, PackingConfig config)
        {
            var wp = new WorkingPallet(ClonePallet(pallet), config);

            if (pallet.ExistingPlacements != null && pallet.ExistingPlacements.Count > 0)
            {
                foreach (var ep in pallet.ExistingPlacements)
                {
                    var fakeItem = new Items
                    {
                        ItemNo = ep.ItemNo,
                        Weight = ep.Weight,
                        Volume = ep.Length * ep.Width * ep.Height,
                        Length = ep.Length,
                        Width = ep.Width,
                        Height = ep.Height,
                        Quantity = 1
                    };

                    var placement = new Placement3D
                    {
                        Item = fakeItem,
                        X = ep.X,
                        Y = ep.Y,
                        Z = ep.Z,
                        L = ep.Length,
                        W = ep.Width,
                        H = ep.Height,
                        IsPreloaded = true
                    };

                    wp.AddPlacement(placement, isPreloaded: true, debugNote: "PreloadedGeometry");
                }
            }

            return wp;
        }

        public decimal BaseZ => Template.ExistingPlacements.Count > 0 ? 0m : Template.UseHeight;
        public decimal AvailableHeight => Template.MaxHeight - BaseZ;
        public decimal CurrentTop => Placements.Count == 0 ? BaseZ : Math.Max(BaseZ, Placements.Max(x => x.Top));
        public decimal TotalWeight => Template.UseWeight;
        public int NonPreloadedPlacementCount => Placements.Count(x => !x.IsPreloaded);

        public void AddPlacement(Placement3D placement, bool isPreloaded, string debugNote)
        {
            placement.IsPreloaded = isPreloaded;

            Placements.Add(placement);
            SpatialIndex.Insert(placement);

            if (!TopSurfaceIndex.TryGetValue(placement.Top, out var list))
            {
                list = [];
                TopSurfaceIndex[placement.Top] = list;
            }
            list.Add(placement);

            if (!isPreloaded)
                PlacedItems.Add(placement.Item);

            Template.UseValume += placement.Item.Volume;
            Template.UseWeight += placement.Item.Weight;
            Template.UseHeight = Math.Max(Template.UseHeight, placement.Top);

            PrecomputedWeightedCenterSumX += placement.CenterX * placement.Item.Weight;
            PrecomputedWeightedCenterSumY += placement.CenterY * placement.Item.Weight;
            PrecomputedWeightedCenterSumZ += placement.CenterZ * placement.Item.Weight;

            DebugNotes.Add($"{debugNote}: ItemNo={placement.Item.ItemNo} at ({placement.X},{placement.Y},{placement.Z}) size ({placement.L},{placement.W},{placement.H})");
        }

        public void RemovePlacement(Placement3D placement)
        {
            Placements.Remove(placement);
            SpatialIndex.Remove(placement);

            if (TopSurfaceIndex.TryGetValue(placement.Top, out var list))
            {
                list.Remove(placement);
                if (list.Count == 0)
                    TopSurfaceIndex.Remove(placement.Top);
            }

            if (!placement.IsPreloaded)
                PlacedItems.Remove(placement.Item);

            Template.UseValume -= placement.Item.Volume;
            Template.UseWeight -= placement.Item.Weight;

            Template.UseHeight = Placements.Count == 0 ? BaseZ : Placements.Max(x => x.Top);

            PrecomputedWeightedCenterSumX -= placement.CenterX * placement.Item.Weight;
            PrecomputedWeightedCenterSumY -= placement.CenterY * placement.Item.Weight;
            PrecomputedWeightedCenterSumZ -= placement.CenterZ * placement.Item.Weight;

            DebugNotes.Add($"Removed: ItemNo={placement.Item.ItemNo}");
        }

        private static Pallets ClonePallet(Pallets p)
        {
            return new Pallets
            {
                PalletId = p.PalletId,
                PalletType = p.PalletType,
                MaxValume = p.MaxValume,
                MaxHeight = p.MaxHeight,
                MaxWeight = p.MaxWeight,
                Lenght = p.Lenght,
                Width = p.Width,
                UseValume = p.UseValume,
                UseHeight = p.UseHeight,
                UseWeight = p.UseWeight,
                LengthQual_id = p.LengthQual_id,
                VolumeQual_id = p.VolumeQual_id,
                WeightQual_id = p.WeightQual_id,
                ExistingPlacements = p.ExistingPlacements.Select(x => new ExistingPlacementInput
                {
                    ItemNo = x.ItemNo,
                    Weight = x.Weight,
                    X = x.X,
                    Y = x.Y,
                    Z = x.Z,
                    Length = x.Length,
                    Width = x.Width,
                    Height = x.Height
                }).ToList()
            };
        }
    }

    internal class SpatialHash3D
    {
        private readonly PackingConfig _config;
        private readonly Dictionary<string, HashSet<Placement3D>> _cells = new(StringComparer.Ordinal);

        public SpatialHash3D(PackingConfig config)
        {
            _config = config;
        }

        public void Insert(Placement3D placement)
        {
            foreach (var key in GetCellKeys(placement))
            {
                if (!_cells.TryGetValue(key, out var set))
                {
                    set = [];
                    _cells[key] = set;
                }
                set.Add(placement);
            }
        }

        public void Remove(Placement3D placement)
        {
            foreach (var key in GetCellKeys(placement))
            {
                if (_cells.TryGetValue(key, out var set))
                {
                    set.Remove(placement);
                    if (set.Count == 0)
                        _cells.Remove(key);
                }
            }
        }

        public IEnumerable<Placement3D> Query(Placement3D placement)
        {
            var result = new HashSet<Placement3D>();
            foreach (var key in GetCellKeys(placement))
            {
                if (_cells.TryGetValue(key, out var set))
                {
                    foreach (var p in set)
                        result.Add(p);
                }
            }
            return result;
        }

        private IEnumerable<string> GetCellKeys(Placement3D placement)
        {
            int minX = ToCell(placement.X, _config.SpatialCellSizeXY);
            int maxX = ToCell(placement.Right, _config.SpatialCellSizeXY);
            int minY = ToCell(placement.Y, _config.SpatialCellSizeXY);
            int maxY = ToCell(placement.Back, _config.SpatialCellSizeXY);
            int minZ = ToCell(placement.Z, _config.SpatialCellSizeZ);
            int maxZ = ToCell(placement.Top, _config.SpatialCellSizeZ);

            for (int x = minX; x <= maxX; x++)
                for (int y = minY; y <= maxY; y++)
                    for (int z = minZ; z <= maxZ; z++)
                        yield return $"{x}|{y}|{z}";
        }

        private static int ToCell(decimal value, decimal size)
        {
            if (size <= 0) return 0;
            return (int)Math.Floor(value / size);
        }
    }

    #endregion

    #region Internal Models

    internal class PlacementGroup
    {
        public string GroupKey { get; set; } = "";
        public List<Items> Units { get; set; } = [];
        public int? LockedItemNo { get; set; }
        public string? LockedLotKey { get; set; }
        public bool MixedMode { get; set; }
    }

    internal record Point3D(decimal X, decimal Y, decimal Z);
    internal record Orientation(decimal L, decimal W, decimal H);

    internal class Placement3D
    {
        public Items Item { get; set; } = default!;
        public decimal X { get; set; }
        public decimal Y { get; set; }
        public decimal Z { get; set; }
        public decimal L { get; set; }
        public decimal W { get; set; }
        public decimal H { get; set; }
        public bool IsPreloaded { get; set; }

        public decimal Right => X + L;
        public decimal Back => Y + W;
        public decimal Top => Z + H;

        public decimal CenterX => X + (L / 2m);
        public decimal CenterY => Y + (W / 2m);
        public decimal CenterZ => Z + (H / 2m);
    }

    internal class PlacementCandidate
    {
        public WorkingPallet Pallet { get; set; } = default!;
        public Placement3D Placement { get; set; } = default!;
        public decimal Score { get; set; }
        public string DecisionNote { get; set; } = "";
    }

    #endregion

    #region Demo Data

    internal static class DemoData
    {
        public static List<Pallets> BuildPallets()
        {
            return
            [
                new Pallets
                {
                    PalletId = 1,
                    PalletType = 101,
                    Lenght = 120,
                    Width = 100,
                    MaxHeight = 160,
                    MaxValume = 120m * 100m * 160m,
                    MaxWeight = 700,
                    UseValume = 0,
                    UseHeight = 0,
                    UseWeight = 0
                },
                new Pallets
                {
                    PalletId = 2,
                    PalletType = 102,
                    Lenght = 140,
                    Width = 120,
                    MaxHeight = 180,
                    MaxValume = 140m * 120m * 180m,
                    MaxWeight = 1000,
                    UseValume = 0,
                    UseHeight = 0,
                    UseWeight = 0,
                    ExistingPlacements =
                    [
                        new ExistingPlacementInput
                        {
                            ItemNo = 9001,
                            Weight = 40,
                            X = 0,
                            Y = 0,
                            Z = 0,
                            Length = 40,
                            Width = 40,
                            Height = 30
                        }
                    ]
                },
                new Pallets
                {
                    PalletId = 3,
                    PalletType = 103,
                    Lenght = 100,
                    Width = 80,
                    MaxHeight = 140,
                    MaxValume = 100m * 80m * 140m,
                    MaxWeight = 450,
                    UseValume = 0,
                    UseHeight = 0,
                    UseWeight = 0
                }
            ];
        }

        public static List<Items> BuildItems()
        {
            return
            [
                new Items
                {
                    ItemNo = 10,
                    Weight = 12,
                    Length = 40,
                    Width = 30,
                    Height = 20,
                    Volume = 40m * 30m * 20m,
                    Quantity = 10,
                    ItemType = 1,
                    ProdDate = new DateOnly(2026, 3, 1),
                    ExpDate = new DateOnly(2027, 3, 1),
                    ReceiptDate = new DateOnly(2026, 3, 4),
                    ProdBatchNo = "A1",
                    TrxB_id = 1,
                    CDO_name = "STD"
                },
                new Items
                {
                    ItemNo = 11,
                    Weight = 10,
                    Length = 35,
                    Width = 25,
                    Height = 15,
                    Volume = 35m * 25m * 15m,
                    Quantity = 24,
                    ItemType = 1,
                    ProdDate = new DateOnly(2026, 3, 1),
                    ExpDate = new DateOnly(2027, 3, 1),
                    ReceiptDate = new DateOnly(2026, 3, 4),
                    ProdBatchNo = "A1",
                    TrxB_id = 2,
                    CDO_name = "STD"
                },
                new Items
                {
                    ItemNo = 10,
                    Weight = 12,
                    Length = 40,
                    Width = 30,
                    Height = 20,
                    Volume = 40m * 30m * 20m,
                    Quantity = 5,
                    ItemType = 1,
                    ProdDate = new DateOnly(2026, 3, 1),
                    ExpDate = new DateOnly(2027, 3, 1),
                    ReceiptDate = new DateOnly(2026, 3, 4),
                    ProdBatchNo = "A1",
                    TrxB_id = 3,
                    CDO_name = "STD"
                },
                new Items
                {
                    ItemNo = 20,
                    Weight = 8,
                    Length = 25,
                    Width = 20,
                    Height = 18,
                    Volume = 25m * 20m * 18m,
                    Quantity = 16,
                    ItemType = 2,
                    ProdDate = new DateOnly(2026, 2, 10),
                    ExpDate = new DateOnly(2026, 9, 1),
                    ReceiptDate = new DateOnly(2026, 2, 15),
                    ProdBatchNo = "LOT-X",
                    TrxB_id = 4,
                    CDO_name = "LOT"
                },
                new Items
                {
                    ItemNo = 21,
                    Weight = 9,
                    Length = 26,
                    Width = 22,
                    Height = 16,
                    Volume = 26m * 22m * 16m,
                    Quantity = 12,
                    ItemType = 2,
                    ProdDate = new DateOnly(2026, 2, 10),
                    ExpDate = new DateOnly(2026, 10, 1),
                    ReceiptDate = new DateOnly(2026, 2, 16),
                    ProdBatchNo = "LOT-X",
                    TrxB_id = 5,
                    CDO_name = "LOT"
                },
                new Items
                {
                    ItemNo = 22,
                    Weight = 9,
                    Length = 26,
                    Width = 22,
                    Height = 16,
                    Volume = 26m * 22m * 16m,
                    Quantity = 10,
                    ItemType = 2,
                    ProdDate = new DateOnly(2026, 2, 11),
                    ExpDate = new DateOnly(2026, 10, 1),
                    ReceiptDate = new DateOnly(2026, 2, 16),
                    ProdBatchNo = "LOT-Y",
                    TrxB_id = 6,
                    CDO_name = "LOT"
                },
                new Items
                {
                    ItemNo = 30,
                    Weight = 7,
                    Length = 20,
                    Width = 20,
                    Height = 20,
                    Volume = 20m * 20m * 20m,
                    Quantity = 25,
                    ItemType = 3,
                    ProdDate = new DateOnly(2026, 1, 1),
                    ExpDate = new DateOnly(2028, 1, 1),
                    ReceiptDate = new DateOnly(2026, 3, 1),
                    ProdBatchNo = "FREE-1",
                    TrxB_id = 7,
                    CDO_name = "FREE"
                }
            ];
        }
    }

    #endregion
}