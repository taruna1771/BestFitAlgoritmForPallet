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

            var result = algorithm.BestPalletFit(items, pallets, rule);

            Console.WriteLine("===== USED PALLETS =====");
            foreach (var pallet in result.OrderBy(x => x.PalletId))
            {
                Console.WriteLine($"PalletId={pallet.PalletId}, Type={pallet.PalletType}");
                Console.WriteLine($"  UsedVolume = {pallet.Volume:N2}/{pallet.PalletVolume:N2}");
                Console.WriteLine($"  UsedWeight = {pallet.Weight:N2}/{pallet.PalletWeight:N2}");
                Console.WriteLine($"  UsedHeight = {pallet.Height:N2}/{pallet.PalletHeight:N2}");
                Console.WriteLine($"  ItemsCount = {pallet.Items.Count}");
                Console.WriteLine($"  Placements = {pallet.Placements.Count}");
                Console.WriteLine($"  TrxBIds = {string.Join(", ", pallet.Items.Select(i => i.TrxB_id).Distinct().OrderBy(x => x))}");

                Console.WriteLine("  Layers:");
                foreach (var layer in pallet.Placements
                    .GroupBy(p => p.Z)
                    .OrderBy(g => g.Key)
                    .Select((g, index) => new { LayerNo = index + 1, BaseZ = g.Key, Items = g.ToList() }))
                {
                    Console.WriteLine($"    Layer {layer.LayerNo} (Z={layer.BaseZ:N2}, Count={layer.Items.Count})");

                    foreach (var g in layer.Items
                        .GroupBy(p => new { p.ItemNo, p.Length, p.Width, p.Height, p.TrxBId })
                        .OrderBy(g => g.Key.ItemNo)
                        .ThenBy(g => g.Key.TrxBId)
                        .ThenBy(g => g.Key.Length)
                        .ThenBy(g => g.Key.Width))
                    {
                        Console.WriteLine(
                            $"      ItemNo={g.Key.ItemNo}, TrxBId={g.Key.TrxBId}, Qty={g.Count()}, Size={g.Key.Length}x{g.Key.Width}x{g.Key.Height}");
                    }

                    Console.WriteLine("      Positions:");
                    foreach (var placement in layer.Items
                        .OrderBy(p => p.Y)
                        .ThenBy(p => p.X)
                        .ThenBy(p => p.TrxBId)
                        .ThenBy(p => p.ItemNo))
                    {
                        Console.WriteLine(
                            $"        ItemNo={placement.ItemNo}, TrxBId={placement.TrxBId} at X={placement.X}, Y={placement.Y}, Z={placement.Z}, Size={placement.Length}x{placement.Width}x{placement.Height}");
                    }

                    Console.WriteLine("      Layout:");
                    foreach (var row in layer.Items
                        .GroupBy(p => p.Y)
                        .OrderBy(g => g.Key))
                    {
                        var rowText = string.Join(" | ", row
                            .OrderBy(p => p.X)
                            .Select(p => $"[{p.ItemNo}/T{p.TrxBId}@X{p.X}:{p.Length}x{p.Width}]"));
                        Console.WriteLine($"        Y={row.Key}: {rowText}");
                    }
                }

                foreach (var g in pallet.Items
                    .GroupBy(i => new { i.ItemNo, i.ItemType, i.ProdBatchNo, i.ProdDate, i.ExpDate, i.ReceiptDate, i.TrxB_id })
                    .OrderBy(g => g.Key.ItemNo))
                {
                    Console.WriteLine(
                        $"    ItemNo={g.Key.ItemNo}, TrxBId={g.Key.TrxB_id}, Qty={g.Count()}, Type={g.Key.ItemType}, Batch={g.Key.ProdBatchNo}, " +
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
                .GroupBy(x => new { x.ItemNo, x.ItemType, x.ProdBatchNo, x.ProdDate, x.ExpDate, x.ReceiptDate, x.TrxB_id })
                .OrderBy(g => g.Key.ItemNo))
            {
                Console.WriteLine(
                    $"ItemNo={u.Key.ItemNo}, TrxBId={u.Key.TrxB_id}, Qty={u.Count()}, Type={u.Key.ItemType}, Batch={u.Key.ProdBatchNo}, " +
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

        public decimal Volume { get; set; }
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
        public int TrxBId { get; set; }
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

        public List<SelectedPalletResult> BestPalletFit(List<Items> items, List<Pallets> pallets, Rule rule)
        {
            var result = _engine.Run(items, pallets, rule);
            LastUnplacedItems = result.UnplacedItems;
            LastRejectStatistics = result.RejectStatistics;
            return result.UsedPallets;
        }

        public List<SelectedPalletResult> BestPaalletFit(List<Items> items, List<Pallets> pallets, Rule rule) =>
            BestPalletFit(items, pallets, rule);
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
                var remainingUnits = group.Units
                    .OrderByDescending(x => x.Volume)
                    .ThenByDescending(x => Math.Max(x.Length, Math.Max(x.Width, x.Height)))
                    .ThenByDescending(x => x.Weight)
                    .ThenBy(x => x.ItemNo)
                    .ToList();

                while (remainingUnits.Count > 0)
                {
                    PlacementCandidate? bestCandidate = null;
                    Items? selectedItem = null;
                    var rejectMap = new Dictionary<Items, Dictionary<string, int>>();
                    List<Items> itemsToEvaluate = [];

                    foreach (var evaluationBatch in GetAdaptiveEvaluationBatches(remainingUnits))
                    {
                        itemsToEvaluate = evaluationBatch;

                        foreach (var item in itemsToEvaluate)
                        {
                            if (!rejectMap.TryGetValue(item, out var itemRejects))
                            {
                                itemRejects = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                                rejectMap[item] = itemRejects;
                            }

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

                                var candidate = FindBestPlacement(item, pallet, itemRejects, remainingUnits.Count);
                                if (candidate == null)
                                    continue;

                                if (IsBetterCandidate(candidate, bestCandidate))
                                {
                                    bestCandidate = candidate;
                                    selectedItem = item;
                                }
                            }
                        }

                        if (bestCandidate != null && selectedItem != null)
                            break;
                    }

                    if (bestCandidate == null || selectedItem == null)
                    {
                        if (!rule.IsMixedStock)
                        {
                            foreach (var item in itemsToEvaluate)
                            {
                                var fallbackCandidate = TryFindFallbackPlacementOnEmptyPallet(item, statePallets, group, rule);
                                if (fallbackCandidate == null)
                                    continue;

                                ApplyPlacement(fallbackCandidate.Pallet, fallbackCandidate.Placement, rule, item, "EmptyPalletFallback");

                                if (ShouldClosePallet(fallbackCandidate.Pallet))
                                    fallbackCandidate.Pallet.IsClosed = true;

                                remainingUnits.Remove(item);
                                bestCandidate = fallbackCandidate;
                                selectedItem = item;
                                break;
                            }
                        }

                        if (bestCandidate != null && selectedItem != null)
                            continue;

                        foreach (var item in itemsToEvaluate)
                        {
                            unplaced.Add(item);

                            if (!rejectMap.TryGetValue(item, out var itemRejects))
                                continue;

                            foreach (var kv in itemRejects)
                            {
                                if (!rejectStats.ContainsKey(kv.Key))
                                    rejectStats[kv.Key] = 0;

                                rejectStats[kv.Key] += kv.Value;
                            }
                        }

                        break;
                    }

                    ApplyPlacement(bestCandidate.Pallet, bestCandidate.Placement, rule, selectedItem, bestCandidate.DecisionNote);

                    if (ShouldClosePallet(bestCandidate.Pallet))
                        bestCandidate.Pallet.IsClosed = true;

                    remainingUnits.Remove(selectedItem);
                }
            }

            RetryUnplacedOnEmptyPallets(unplaced, statePallets, rule);

            if (unitItems.Count <= _config.MaxItemsForLocalOptimization)
            {
                _localOptimizationService.Optimize(statePallets, rule, rejectStats);
                ReopenEmptyPallets(statePallets);
                _localOptimizationService.Optimize(statePallets, rule, rejectStats);
                ReopenEmptyPallets(statePallets);
                RetryUnplacedItems(unplaced, statePallets, rule, rejectStats);
            }

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

        private void RetryUnplacedOnEmptyPallets(List<Items> unplaced, List<WorkingPallet> pallets, Rule rule)
        {
            if (unplaced.Count == 0)
                return;

            for (int i = unplaced.Count - 1; i >= 0; i--)
            {
                var item = unplaced[i];
                var candidate = TryFindFallbackPlacementOnEmptyPallet(item, pallets, new PlacementGroup
                {
                    LockedItemNo = item.ItemNo,
                    LockedLotKey = item.ItemType == 2 ? BusinessRuleService.GetLotKey(item, rule) : null
                }, rule);

                if (candidate == null)
                    continue;

                ApplyPlacement(candidate.Pallet, candidate.Placement, rule, item, "RetryUnplacedEmptyPallet");

                if (ShouldClosePallet(candidate.Pallet))
                    candidate.Pallet.IsClosed = true;

                unplaced.RemoveAt(i);
            }
        }

        private static void ReopenEmptyPallets(List<WorkingPallet> pallets)
        {
            foreach (var pallet in pallets)
            {
                if (pallet.Placements.Count == 0)
                    pallet.IsClosed = false;
            }
        }

        private void RetryUnplacedItems(
            List<Items> unplaced,
            List<WorkingPallet> pallets,
            Rule rule,
            Dictionary<string, int> rejectStats)
        {
            if (unplaced.Count == 0)
                return;

            var retrySource = unplaced.ToList();
            unplaced.Clear();

            var groups = _groupingService.BuildPlacementGroups(retrySource, rule);

            foreach (var group in groups)
            {
                var remainingUnits = group.Units
                    .OrderByDescending(x => x.Volume)
                    .ThenByDescending(x => Math.Max(x.Length, Math.Max(x.Width, x.Height)))
                    .ThenByDescending(x => x.Weight)
                    .ThenBy(x => x.ItemNo)
                    .ToList();

                while (remainingUnits.Count > 0)
                {
                    PlacementCandidate? bestCandidate = null;
                    Items? selectedItem = null;
                    var rejectMap = new Dictionary<Items, Dictionary<string, int>>();
                    List<Items> itemsToEvaluate = [];

                    foreach (var evaluationBatch in GetAdaptiveEvaluationBatches(remainingUnits))
                    {
                        itemsToEvaluate = evaluationBatch;

                        foreach (var item in itemsToEvaluate)
                        {
                            if (!rejectMap.TryGetValue(item, out var itemRejects))
                            {
                                itemRejects = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                                rejectMap[item] = itemRejects;
                            }

                            foreach (var pallet in pallets)
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

                                var candidate = FindBestPlacement(item, pallet, itemRejects, remainingUnits.Count);
                                if (candidate == null)
                                    continue;

                                if (IsBetterCandidate(candidate, bestCandidate))
                                {
                                    bestCandidate = candidate;
                                    selectedItem = item;
                                }
                            }
                        }

                        if (bestCandidate != null && selectedItem != null)
                            break;
                    }

                    if (bestCandidate == null || selectedItem == null)
                    {
                        if (!rule.IsMixedStock)
                        {
                            foreach (var item in itemsToEvaluate)
                            {
                                var fallbackCandidate = TryFindFallbackPlacementOnEmptyPallet(item, pallets, group, rule);
                                if (fallbackCandidate == null)
                                    continue;

                                ApplyPlacement(fallbackCandidate.Pallet, fallbackCandidate.Placement, rule, item, "RetryUnplacedGeneral");

                                if (ShouldClosePallet(fallbackCandidate.Pallet))
                                    fallbackCandidate.Pallet.IsClosed = true;

                                remainingUnits.Remove(item);
                                bestCandidate = fallbackCandidate;
                                selectedItem = item;
                                break;
                            }
                        }

                        if (bestCandidate != null && selectedItem != null)
                            continue;

                        foreach (var item in itemsToEvaluate)
                        {
                            unplaced.Add(item);

                            if (!rejectMap.TryGetValue(item, out var itemRejects))
                                continue;

                            foreach (var kv in itemRejects)
                            {
                                if (!rejectStats.ContainsKey(kv.Key))
                                    rejectStats[kv.Key] = 0;

                                rejectStats[kv.Key] += kv.Value;
                            }
                        }

                        break;
                    }

                    ApplyPlacement(bestCandidate.Pallet, bestCandidate.Placement, rule, selectedItem, "RetryUnplacedGeneral");

                    if (ShouldClosePallet(bestCandidate.Pallet))
                        bestCandidate.Pallet.IsClosed = true;

                    remainingUnits.Remove(selectedItem);
                }
            }
        }

        private List<Items> GetItemsToEvaluate(List<Items> remainingUnits)
        {
            if (remainingUnits.Count <= _config.MaxItemsToEvaluatePerStep)
                return remainingUnits;

            var result = new List<Items>(_config.MaxItemsToEvaluatePerStep + _config.SmallItemLookaheadCount);
            var seen = new HashSet<Items>();

            int headCount = Math.Min(_config.MaxItemsToEvaluatePerStep, remainingUnits.Count);
            for (int i = 0; i < headCount; i++)
            {
                result.Add(remainingUnits[i]);
                seen.Add(remainingUnits[i]);
            }

            int tailStart = Math.Max(headCount, remainingUnits.Count - _config.SmallItemLookaheadCount);
            for (int i = tailStart; i < remainingUnits.Count; i++)
            {
                if (seen.Add(remainingUnits[i]))
                    result.Add(remainingUnits[i]);
            }

            return result;
        }

        private IEnumerable<List<Items>> GetAdaptiveEvaluationBatches(List<Items> remainingUnits)
        {
            if (remainingUnits.Count == 0)
                yield break;

            int passes = 0;
            int limit = _config.MaxItemsToEvaluatePerStep;
            List<Items>? lastBatch = null;

            while (passes < _config.MaxAdaptiveEvaluationPasses)
            {
                var batch = GetItemsToEvaluate(remainingUnits, limit);
                if (lastBatch != null && batch.Count == lastBatch.Count)
                    yield break;

                yield return batch;
                lastBatch = batch;

                if (batch.Count >= remainingUnits.Count)
                    yield break;

                limit = Math.Min(remainingUnits.Count, limit + _config.EvaluateStepGrowth);
                passes++;
            }

            if (lastBatch == null || lastBatch.Count < remainingUnits.Count)
                yield return remainingUnits;
        }

        private List<Items> GetItemsToEvaluate(List<Items> remainingUnits, int limit)
        {
            if (remainingUnits.Count <= limit)
                return remainingUnits;

            var safeLimit = Math.Min(limit, remainingUnits.Count);
            var result = new List<Items>(safeLimit + _config.SmallItemLookaheadCount);
            var seen = new HashSet<Items>();

            for (int i = 0; i < safeLimit; i++)
            {
                result.Add(remainingUnits[i]);
                seen.Add(remainingUnits[i]);
            }

            int tailStart = Math.Max(safeLimit, remainingUnits.Count - _config.SmallItemLookaheadCount);
            for (int i = tailStart; i < remainingUnits.Count; i++)
            {
                if (seen.Add(remainingUnits[i]))
                    result.Add(remainingUnits[i]);
            }

            return result;
        }

        private PlacementCandidate? FindBestPlacement(
            Items item,
            WorkingPallet pallet,
            Dictionary<string, int> rejectCollector,
            int remainingGroupCount)
        {
            var candidatePoints = _candidatePointService.GetCandidatePoints(pallet).ToList();

            if (candidatePoints.Count == 0)
            {
                AddReject(rejectCollector, RejectReason.NoCandidatePoint);
                return null;
            }

            PlacementCandidate? best = null;
            var emptyPalletPlan = pallet.Placements.Count == 0
                ? EvaluateEmptyPalletPlan(pallet, item, remainingGroupCount)
                : null;

            foreach (var orientation in _orientationService.GetAllowedOrientations(item))
            {
                if (orientation.L > pallet.Template.Lenght ||
                    orientation.W > pallet.Template.Width ||
                    orientation.H > pallet.AvailableHeight)
                {
                    AddReject(rejectCollector, RejectReason.DimensionOverflow);
                    continue;
                }

                var pointsToTry = GetPointsToTry(candidatePoints, pallet, orientation);
                foreach (var point in pointsToTry)
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
                        DecisionNote = validation.Note,
                        LayerFillRatioAfter = pallet.FootprintArea <= 0
                            ? 0m
                            : (pallet.GetUsedFootprintAtLevel(placement.Z) + (placement.L * placement.W)) / pallet.FootprintArea,
                        LayerFreeAreaAfter = Math.Max(0m, pallet.FootprintArea - (pallet.GetUsedFootprintAtLevel(placement.Z) + (placement.L * placement.W))),
                        OpensNewLayer = placement.Z >= pallet.CurrentTop && pallet.NonPreloadedPlacementCount > 0,
                        UsesExistingPallet = pallet.Placements.Count > 0,
                        ExistingItemCount = pallet.PlacedItems.Count,
                        EstimatedPalletsNeededForGroup = emptyPalletPlan?.EstimatedPalletsNeeded ?? 0,
                        EstimatedUnusedVolumeAfterGroup = emptyPalletPlan?.EstimatedUnusedVolumeAfterGroup ?? decimal.MaxValue
                    };

                    if (IsBetterCandidate(candidate, best))
                        best = candidate;
                }
            }

            return best;
        }

        private PlacementCandidate? TryFindFallbackPlacementOnEmptyPallet(
            Items item,
            List<WorkingPallet> pallets,
            PlacementGroup group,
            Rule rule)
        {
            PlacementCandidate? best = null;

            foreach (var pallet in pallets)
            {
                if (pallet.Placements.Count > 0)
                    continue;

                if (pallet.TotalWeight + item.Weight > pallet.Template.MaxWeight)
                    continue;

                if (pallet.Template.UseValume + item.Volume > pallet.Template.MaxValume)
                    continue;

                foreach (var orientation in _orientationService.GetAllowedOrientations(item))
                {
                    if (orientation.L > pallet.Template.Lenght ||
                        orientation.W > pallet.Template.Width ||
                        orientation.H > pallet.AvailableHeight)
                        continue;

                    var centeredPlacement = new Placement3D
                    {
                        Item = item,
                        X = Math.Max(0m, (pallet.Template.Lenght - orientation.L) / 2m),
                        Y = Math.Max(0m, (pallet.Template.Width - orientation.W) / 2m),
                        Z = pallet.BaseZ,
                        L = orientation.L,
                        W = orientation.W,
                        H = orientation.H
                    };

                    var plan = EvaluateEmptyPalletPlan(pallet, item, group.Units.Count);
                    var candidate = new PlacementCandidate
                    {
                        Pallet = pallet,
                        Placement = centeredPlacement,
                        Score = _scoringService.CalculateScore(centeredPlacement, pallet),
                        DecisionNote = "Fallback: centered first placement",
                        UsesExistingPallet = false,
                        ExistingItemCount = pallet.PlacedItems.Count,
                        EstimatedPalletsNeededForGroup = plan.EstimatedPalletsNeeded,
                        EstimatedUnusedVolumeAfterGroup = plan.EstimatedUnusedVolumeAfterGroup
                    };

                    if (IsBetterCandidate(candidate, best))
                        best = candidate;
                }
            }

            return best;
        }

        private List<Point3D> GetPointsToTry(
            IReadOnlyList<Point3D> candidatePoints,
            WorkingPallet pallet,
            Orientation orientation)
        {
            var result = new List<Point3D>(candidatePoints.Count + 24);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            void AddPoint(Point3D point)
            {
                if (point.X < 0 || point.Y < 0 || point.Z < pallet.BaseZ)
                    return;

                if (point.X > pallet.Template.Lenght || point.Y > pallet.Template.Width || point.Z > pallet.Template.MaxHeight)
                    return;

                var key = $"{point.X:F4}|{point.Y:F4}|{point.Z:F4}";
                if (seen.Add(key))
                    result.Add(point);
            }

            foreach (var point in candidatePoints)
                AddPoint(point);

            decimal frontMargin = Math.Max(
                pallet.Template.Lenght * _config.CogMarginLengthRatio,
                pallet.Template.Lenght * _config.ForkliftFrontBackMarginRatio);
            decimal sideMargin = Math.Max(
                pallet.Template.Width * _config.CogMarginWidthRatio,
                pallet.Template.Width * _config.ForkliftSideMarginRatio);

            var xAnchors = new[]
            {
                0m,
                frontMargin,
                Math.Max(0m, (pallet.Template.Lenght - orientation.L) / 2m),
                Math.Max(0m, pallet.Template.Lenght - orientation.L - frontMargin),
                Math.Max(0m, pallet.Template.Lenght - orientation.L)
            };

            var yAnchors = new[]
            {
                0m,
                sideMargin,
                Math.Max(0m, (pallet.Template.Width - orientation.W) / 2m),
                Math.Max(0m, pallet.Template.Width - orientation.W - sideMargin),
                Math.Max(0m, pallet.Template.Width - orientation.W)
            };

            foreach (var x in xAnchors)
                foreach (var y in yAnchors)
                    AddPoint(new Point3D(x, y, pallet.BaseZ));

            return result
                .OrderBy(pt => pt.Z)
                .ThenBy(pt => pt.X + pt.Y)
                .ThenBy(pt => pt.X)
                .ThenBy(pt => pt.Y)
                .ToList();
        }

        private EmptyPalletPlan EvaluateEmptyPalletPlan(WorkingPallet pallet, Items item, int remainingGroupCount)
        {
            int capacity = EstimateItemCapacity(pallet, item);
            if (capacity <= 0)
            {
                return new EmptyPalletPlan
                {
                    EstimatedPalletsNeeded = int.MaxValue,
                    EstimatedUnusedVolumeAfterGroup = decimal.MaxValue
                };
            }

            int palletsNeeded = (int)Math.Ceiling(remainingGroupCount / (decimal)capacity);
            int plannedLoad = Math.Min(remainingGroupCount, capacity);
            decimal unusedVolumeAfterGroup = pallet.Template.MaxValume - (plannedLoad * item.Volume);

            return new EmptyPalletPlan
            {
                EstimatedPalletsNeeded = palletsNeeded,
                EstimatedUnusedVolumeAfterGroup = unusedVolumeAfterGroup
            };
        }

        private int EstimateItemCapacity(WorkingPallet pallet, Items item)
        {
            int best = 0;

            foreach (var orientation in _orientationService.GetAllowedOrientations(item))
            {
                if (orientation.L <= 0 || orientation.W <= 0 || orientation.H <= 0)
                    continue;

                if (orientation.L > pallet.Template.Lenght ||
                    orientation.W > pallet.Template.Width ||
                    orientation.H > pallet.AvailableHeight)
                    continue;

                int perRow = (int)Math.Floor(pallet.Template.Lenght / orientation.L);
                int perColumn = (int)Math.Floor(pallet.Template.Width / orientation.W);
                int layers = (int)Math.Floor(pallet.AvailableHeight / orientation.H);

                if (perRow <= 0 || perColumn <= 0 || layers <= 0)
                    continue;

                int capacity = perRow * perColumn * layers;
                if (capacity > best)
                    best = capacity;
            }

            return best;
        }

        private static bool IsBetterCandidate(PlacementCandidate candidate, PlacementCandidate? currentBest)
        {
            if (currentBest == null)
                return true;

            if (candidate.UsesExistingPallet != currentBest.UsesExistingPallet)
                return candidate.UsesExistingPallet;

            if (candidate.UsesExistingPallet)
            {
                if (candidate.ExistingItemCount != currentBest.ExistingItemCount)
                    return candidate.ExistingItemCount > currentBest.ExistingItemCount;
            }
            else
            {
                if (candidate.EstimatedPalletsNeededForGroup != currentBest.EstimatedPalletsNeededForGroup)
                    return candidate.EstimatedPalletsNeededForGroup < currentBest.EstimatedPalletsNeededForGroup;

                if (candidate.EstimatedUnusedVolumeAfterGroup != currentBest.EstimatedUnusedVolumeAfterGroup)
                    return candidate.EstimatedUnusedVolumeAfterGroup < currentBest.EstimatedUnusedVolumeAfterGroup;
            }

            if (candidate.Placement.Z != currentBest.Placement.Z)
                return candidate.Placement.Z < currentBest.Placement.Z;

            if (candidate.OpensNewLayer != currentBest.OpensNewLayer)
                return !candidate.OpensNewLayer;

            if (candidate.LayerFillRatioAfter != currentBest.LayerFillRatioAfter)
                return candidate.LayerFillRatioAfter > currentBest.LayerFillRatioAfter;

            if (candidate.LayerFreeAreaAfter != currentBest.LayerFreeAreaAfter)
                return candidate.LayerFreeAreaAfter < currentBest.LayerFreeAreaAfter;

            if (candidate.Score != currentBest.Score)
                return candidate.Score < currentBest.Score;

            if (candidate.Placement.Y != currentBest.Placement.Y)
                return candidate.Placement.Y < currentBest.Placement.Y;

            return candidate.Placement.X < currentBest.Placement.X;
        }

        private static void ApplyPlacement(WorkingPallet pallet, Placement3D placement, Rule rule, Items item, string debugNote)
        {
            BusinessRuleService.LockPalletForItem(pallet, rule, item);
            pallet.AddPlacement(placement, isPreloaded: false, debugNote: debugNote);
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
            collector.TryGetValue(key, out var count);
            collector[key] = count + 1;
        }

        private static SelectedPalletResult ToResult(WorkingPallet pallet)
        {
            return new SelectedPalletResult
            {
                PalletId = pallet.Template.PalletId,
                PalletType = pallet.Template.PalletType,
                Volume = pallet.Template.UseValume,
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
                        TrxBId = x.Item.TrxB_id,
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
        public int MaxItemsToEvaluatePerStep { get; set; } = 96;
        public int SmallItemLookaheadCount { get; set; } = 24;
        public int MaxItemsForLocalOptimization { get; set; } = 800;
        public int EvaluateStepGrowth { get; set; } = 96;
        public int MaxAdaptiveEvaluationPasses { get; set; } = 6;

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
                    IsLotGroup = true,
                    MixedMode = true
                });
            }

            if (freeItems.Count > 0)
            {
                groups.Add(new PlacementGroup
                {
                    GroupKey = "MIXED:FREE",
                    Units = freeItems,
                    IsLotGroup = false,
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

            if (group.IsLotGroup)
            {
                if (pallet.PlacedItems.Any(x => x.ItemType != 2))
                    return false;

                if (group.LockedLotKey == null)
                    return false;

                if (pallet.LockedLotKey == null)
                    return true;

                return pallet.LockedLotKey == group.LockedLotKey;
            }

            if (pallet.PlacedItems.Any(x => x.ItemType == 2))
                return false;

            return true;
        }

        public static void LockPalletForGroup(WorkingPallet pallet, PlacementGroup group, Rule rule, Items item)
        {
            if (!rule.IsMixedStock)
            {
                pallet.LockedItemNo ??= group.LockedItemNo;
                return;
            }

            LockPalletForItem(pallet, rule, item);
        }

        public static void LockPalletForItem(WorkingPallet pallet, Rule rule, Items item)
        {
            if (!rule.IsMixedStock)
            {
                pallet.LockedItemNo ??= item.ItemNo;
                return;
            }

            if (item.ItemType == 2)
                pallet.LockedLotKey ??= GetLotKey(item, rule);
        }

        public static void RefreshPalletLocks(WorkingPallet pallet, Rule rule)
        {
            pallet.LockedItemNo = null;
            pallet.LockedLotKey = null;

            foreach (var item in pallet.PlacedItems)
                LockPalletForItem(pallet, rule, item);
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
        private readonly Dictionary<string, List<Orientation>> _cache = new(StringComparer.Ordinal);

        public IEnumerable<Orientation> GetAllowedOrientations(Items item)
        {
            var key = $"{item.Length:F4}|{item.Width:F4}|{item.Height:F4}";
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            var orientations = new[]
                {
                    new Orientation(item.Length, item.Width, item.Height),
                    new Orientation(item.Width, item.Length, item.Height)
                }
                .GroupBy(x => $"{x.L:F4}|{x.W:F4}|{x.H:F4}")
                .Select(g => g.First())
                .OrderByDescending(x => x.L * x.W)
                .ThenBy(x => x.W)
                .ThenBy(x => x.L)
                .ToList();

            _cache[key] = orientations;
            return orientations;
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
            if (pallet.TryGetCachedCandidatePoints(out var cachedPoints))
                return cachedPoints;

            List<Point3D> result;
            var xAnchors = new HashSet<decimal>
            {
                0m,
                pallet.Template.Lenght * _config.CogMarginLengthRatio,
                pallet.Template.Lenght * _config.ForkliftFrontBackMarginRatio
            };
            var yAnchors = new HashSet<decimal>
            {
                0m,
                pallet.Template.Width * _config.CogMarginWidthRatio,
                pallet.Template.Width * _config.ForkliftSideMarginRatio
            };
            var zAnchors = new HashSet<decimal> { pallet.BaseZ };

            if (pallet.Placements.Count == 0)
            {
                result = xAnchors
                    .SelectMany(x => yAnchors.Select(y => new Point3D(x, y, pallet.BaseZ)))
                    .Where(pt => pt.X >= 0 && pt.Y >= 0 && pt.X <= pallet.Template.Lenght && pt.Y <= pallet.Template.Width)
                    .OrderBy(pt => pt.Z)
                    .ThenBy(pt => pt.X + pt.Y)
                    .ThenBy(pt => pt.X)
                    .ThenBy(pt => pt.Y)
                    .Take(_config.MaxCandidatePointsPerPallet)
                    .ToList();

                pallet.SetCachedCandidatePoints(result);
                return result;
            }

            foreach (var p in pallet.Placements)
            {
                xAnchors.Add(p.X);
                xAnchors.Add(p.Right);

                yAnchors.Add(p.Y);
                yAnchors.Add(p.Back);

                zAnchors.Add(p.Z);
                zAnchors.Add(p.Top);
            }

            var points = new List<Point3D>();
            foreach (var x in xAnchors)
            {
                foreach (var y in yAnchors)
                {
                    foreach (var z in zAnchors)
                    {
                        if (x < 0 || y < 0 || z < pallet.BaseZ)
                            continue;

                        if (x > pallet.Template.Lenght || y > pallet.Template.Width || z > pallet.Template.MaxHeight)
                            continue;

                        points.Add(new Point3D(x, y, z));
                    }
                }
            }

            result = points
                .GroupBy(pt => $"{pt.X:F4}|{pt.Y:F4}|{pt.Z:F4}")
                .Select(g => g.First())
                .OrderBy(pt => pt.Z)
                .ThenBy(pt => pt.X + pt.Y)
                .ThenBy(pt => pt.X)
                .ThenBy(pt => pt.Y)
                .Take(_config.MaxCandidatePointsPerPallet)
                .ToList();

            pallet.SetCachedCandidatePoints(result);
            return result;
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
            if (pallet.Placements.Count == 0)
            {
                note = "Initial placement: COG rule relaxed.";
                return true;
            }

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
            if (pallet.Placements.Count == 0)
            {
                note = "Initial placement: forklift rule relaxed.";
                return true;
            }

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
        NoCandidatePoint,
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
            decimal layerAreaAfter = pallet.GetUsedFootprintAtLevel(placement.Z) + (placement.L * placement.W);
            decimal layerFreeAreaAfter = Math.Max(0m, pallet.FootprintArea - layerAreaAfter);
            decimal fillRatioAfter = pallet.FootprintArea <= 0 ? 0m : layerAreaAfter / pallet.FootprintArea;
            bool opensNewLayer = placement.Z >= pallet.CurrentTop && pallet.NonPreloadedPlacementCount > 0;

            decimal centerX = pallet.Template.Lenght / 2m;
            decimal centerY = pallet.Template.Width / 2m;

            decimal distToCenterPenalty =
                Math.Abs(placement.CenterX - centerX) +
                Math.Abs(placement.CenterY - centerY);

            decimal emptyPalletPenalty = pallet.NonPreloadedPlacementCount == 0 ? 100000m : 0m;
            decimal zPenalty = placement.Z * 40m;
            decimal topPenalty = newTop * 8m;
            decimal travelPenalty = (placement.X + placement.Y) * 2m;
            decimal remainPenalty = remainVolume * 0.00001m;
            decimal utilizationReward = pallet.NonPreloadedPlacementCount * 250m;
            decimal newLayerPenalty = opensNewLayer ? 5000m : 0m;
            decimal layerWastePenalty = layerFreeAreaAfter * 0.02m;
            decimal layerFillReward = fillRatioAfter * 3500m;
            decimal footprintReward = placement.L * placement.W * 0.05m;

            return emptyPalletPenalty
                + zPenalty
                + topPenalty
                + travelPenalty
                + distToCenterPenalty
                + remainPenalty
                + newLayerPenalty
                + layerWastePenalty
                - layerFillReward
                - footprintReward
                - utilizationReward;
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

                    var candidatePoints = _candidatePointService.GetCandidatePoints(target).ToList();
                    if (candidatePoints.Count == 0)
                        continue;

                    foreach (var orientation in _orientationService.GetAllowedOrientations(oldPlacement.Item))
                    {
                        if (orientation.L > target.Template.Lenght ||
                            orientation.W > target.Template.Width ||
                            orientation.H > target.AvailableHeight)
                            continue;

                        foreach (var point in candidatePoints)
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
                        BusinessRuleService.RefreshPalletLocks(m.Target, rule);
                    }
                    BusinessRuleService.RefreshPalletLocks(source, rule);
                    return false;
                }

                source.RemovePlacement(oldPlacement);
                BusinessRuleService.RefreshPalletLocks(source, rule);
                BusinessRuleService.LockPalletForItem(best.Pallet, rule, oldPlacement.Item);
                best.Pallet.AddPlacement(best.Placement, isPreloaded: false, debugNote: "LocalOptimization");
                BusinessRuleService.RefreshPalletLocks(best.Pallet, rule);
                moved.Add((oldPlacement, best.Pallet, best.Placement));
            }

            // source bo'shagan bo'lsa uni yopamiz
            source.IsClosed = true;
            BusinessRuleService.RefreshPalletLocks(source, rule);
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

            return BusinessRuleService.CanPalletAcceptGroup(target, new PlacementGroup
            {
                IsLotGroup = item.ItemType == 2,
                LockedLotKey = item.ItemType == 2 ? BusinessRuleService.GetLotKey(item, rule) : null
            }, rule);
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
        public bool HasPreloadedGeometry => Template.ExistingPlacements.Count > 0;

        public List<Placement3D> Placements { get; } = [];
        public List<Items> PlacedItems { get; } = [];
        public List<string> DebugNotes { get; } = [];

        public SpatialHash3D SpatialIndex { get; }
        public Dictionary<decimal, List<Placement3D>> TopSurfaceIndex { get; } = [];
        public Dictionary<decimal, decimal> LayerFootprintAreaIndex { get; } = [];

        public decimal PrecomputedWeightedCenterSumX { get; private set; }
        public decimal PrecomputedWeightedCenterSumY { get; private set; }
        public decimal PrecomputedWeightedCenterSumZ { get; private set; }
        public decimal CurrentTopValue { get; private set; }

        private readonly PackingConfig _config;
        private List<Point3D>? _cachedCandidatePoints;
        private bool _candidatePointsDirty = true;

        private WorkingPallet(Pallets pallet, PackingConfig config)
        {
            Template = pallet;
            _config = config;
            BaseZ = pallet.ExistingPlacements.Count > 0 ? 0m : pallet.UseHeight;
            CurrentTopValue = BaseZ;
            SpatialIndex = new SpatialHash3D(config);
        }

        public static WorkingPallet CreateFromInput(Pallets pallet, PackingConfig config)
        {
            var wp = new WorkingPallet(ClonePallet(pallet), config);

            if (pallet.ExistingPlacements != null && pallet.ExistingPlacements.Count > 0)
            {
                wp.Template.UseValume = 0;
                wp.Template.UseWeight = 0;
                wp.Template.UseHeight = 0;

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

        public decimal BaseZ { get; }
        public decimal AvailableHeight => Template.MaxHeight - BaseZ;
        public decimal CurrentTop => CurrentTopValue;
        public decimal TotalWeight => Template.UseWeight;
        public int NonPreloadedPlacementCount => Placements.Count(x => !x.IsPreloaded);
        public decimal FootprintArea => Template.Lenght * Template.Width;

        public decimal GetUsedFootprintAtLevel(decimal z) =>
            LayerFootprintAreaIndex.TryGetValue(z, out var area) ? area : 0m;

        public decimal GetFillRatioAtLevel(decimal z)
        {
            if (FootprintArea <= 0)
                return 0m;

            return GetUsedFootprintAtLevel(z) / FootprintArea;
        }

        public void AddPlacement(Placement3D placement, bool isPreloaded, string debugNote)
        {
            placement.IsPreloaded = isPreloaded;
            _candidatePointsDirty = true;

            Placements.Add(placement);
            SpatialIndex.Insert(placement);

            if (!TopSurfaceIndex.TryGetValue(placement.Top, out var list))
            {
                list = [];
                TopSurfaceIndex[placement.Top] = list;
            }
            list.Add(placement);

            LayerFootprintAreaIndex.TryGetValue(placement.Z, out var layerArea);
            LayerFootprintAreaIndex[placement.Z] = layerArea + (placement.L * placement.W);

            if (!isPreloaded)
                PlacedItems.Add(placement.Item);

            Template.UseValume += placement.Item.Volume;
            Template.UseWeight += placement.Item.Weight;
            Template.UseHeight = Math.Max(Template.UseHeight, placement.Top);
            CurrentTopValue = Math.Max(CurrentTopValue, placement.Top);

            PrecomputedWeightedCenterSumX += placement.CenterX * placement.Item.Weight;
            PrecomputedWeightedCenterSumY += placement.CenterY * placement.Item.Weight;
            PrecomputedWeightedCenterSumZ += placement.CenterZ * placement.Item.Weight;

            DebugNotes.Add($"{debugNote}: ItemNo={placement.Item.ItemNo} at ({placement.X},{placement.Y},{placement.Z}) size ({placement.L},{placement.W},{placement.H})");
        }

        public void RemovePlacement(Placement3D placement)
        {
            _candidatePointsDirty = true;
            Placements.Remove(placement);
            SpatialIndex.Remove(placement);

            if (TopSurfaceIndex.TryGetValue(placement.Top, out var list))
            {
                list.Remove(placement);
                if (list.Count == 0)
                    TopSurfaceIndex.Remove(placement.Top);
            }

            if (LayerFootprintAreaIndex.TryGetValue(placement.Z, out var layerArea))
            {
                layerArea -= placement.L * placement.W;
                if (layerArea <= 0)
                    LayerFootprintAreaIndex.Remove(placement.Z);
                else
                    LayerFootprintAreaIndex[placement.Z] = layerArea;
            }

            if (!placement.IsPreloaded)
                PlacedItems.Remove(placement.Item);

            Template.UseValume -= placement.Item.Volume;
            Template.UseWeight -= placement.Item.Weight;

            CurrentTopValue = Placements.Count == 0 ? BaseZ : Placements.Max(x => x.Top);
            Template.UseHeight = CurrentTopValue;

            PrecomputedWeightedCenterSumX -= placement.CenterX * placement.Item.Weight;
            PrecomputedWeightedCenterSumY -= placement.CenterY * placement.Item.Weight;
            PrecomputedWeightedCenterSumZ -= placement.CenterZ * placement.Item.Weight;

            DebugNotes.Add($"Removed: ItemNo={placement.Item.ItemNo}");
        }

        public bool TryGetCachedCandidatePoints(out IReadOnlyList<Point3D> candidatePoints)
        {
            if (!_candidatePointsDirty && _cachedCandidatePoints != null)
            {
                candidatePoints = _cachedCandidatePoints;
                return true;
            }

            candidatePoints = Array.Empty<Point3D>();
            return false;
        }

        public void SetCachedCandidatePoints(List<Point3D> candidatePoints)
        {
            _cachedCandidatePoints = candidatePoints;
            _candidatePointsDirty = false;
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
                UseValume = p.ExistingPlacements.Count > 0 ? 0 : p.UseValume,
                UseHeight = p.ExistingPlacements.Count > 0 ? 0 : p.UseHeight,
                UseWeight = p.ExistingPlacements.Count > 0 ? 0 : p.UseWeight,
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
        public bool IsLotGroup { get; set; }
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
        public decimal LayerFillRatioAfter { get; set; }
        public decimal LayerFreeAreaAfter { get; set; }
        public bool OpensNewLayer { get; set; }
        public bool UsesExistingPallet { get; set; }
        public int ExistingItemCount { get; set; }
        public int EstimatedPalletsNeededForGroup { get; set; }
        public decimal EstimatedUnusedVolumeAfterGroup { get; set; }
    }

    internal class EmptyPalletPlan
    {
        public int EstimatedPalletsNeeded { get; set; }
        public decimal EstimatedUnusedVolumeAfterGroup { get; set; }
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
                //new Pallets
                //{
                //    PalletId = 2,
                //    PalletType = 102,
                //    Lenght = 140,
                //    Width = 120,
                //    MaxHeight = 180,
                //    MaxValume = 140m * 120m * 180m,
                //    MaxWeight = 1000,
                //    UseValume = 0,
                //    UseHeight = 0,
                //    UseWeight = 0,

                //},
                //new Pallets
                //{
                //    PalletId = 3,
                //    PalletType = 103,

                //    Lenght = 100,
                //    Width = 80,
                //    MaxHeight = 140,

                //    MaxValume = 100m * 80m * 140m,
                //    MaxWeight = 450,
                //    UseValume = 0,
                //    UseHeight = 0,
                //    UseWeight = 0
                //}
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

                    Length = 50,
                    Width = 50,
                    Height = 20,

                    Volume = 101m * 50m * 20m,
                    Quantity = 102,
                    ItemType = 1,
                    ProdDate = new DateOnly(2026, 3, 1),
                    ExpDate = new DateOnly(2027, 3, 1),
                    ReceiptDate = new DateOnly(2026, 3, 4),
                    ProdBatchNo = "A1",
                    TrxB_id = 1,
                    CDO_name = "STD1"
                },
                // new Items
                //{
                //    ItemNo = 11,
                //    Weight = 12,

                //    Length = 20,
                //    Width = 50,
                //    Height = 20,

                //    Volume = 20m * 50m * 20m,
                //    Quantity = 10,
                //    ItemType = 3,
                //    ProdDate = new DateOnly(2026, 3, 1),
                //    ExpDate = new DateOnly(2027, 3, 1),
                //    ReceiptDate = new DateOnly(2026, 3, 4),
                //    ProdBatchNo = "A2",
                //    TrxB_id = 2,
                //    CDO_name = "STD2"
                //},
                //new Items
                //{
                //    ItemNo = 11,
                //    Weight = 10,
                //    Length = 35,
                //    Width = 25,
                //    Height = 15,
                //    Volume = 35m * 25m * 15m,
                //    Quantity = 24,
                //    ItemType = 1,
                //    ProdDate = new DateOnly(2026, 3, 1),
                //    ExpDate = new DateOnly(2027, 3, 1),
                //    ReceiptDate = new DateOnly(2026, 3, 4),
                //    ProdBatchNo = "A1",
                //    TrxB_id = 2,
                //    CDO_name = "STD"
                //},
                //new Items
                //{
                //    ItemNo = 10,
                //    Weight = 12,
                //    Length = 40,
                //    Width = 30,
                //    Height = 20,
                //    Volume = 40m * 30m * 20m,
                //    Quantity = 5,
                //    ItemType = 1,
                //    ProdDate = new DateOnly(2026, 3, 1),
                //    ExpDate = new DateOnly(2027, 3, 1),
                //    ReceiptDate = new DateOnly(2026, 3, 4),
                //    ProdBatchNo = "A1",
                //    TrxB_id = 3,
                //    CDO_name = "STD"
                //},
                //new Items
                //{
                //    ItemNo = 20,
                //    Weight = 8,
                //    Length = 25,
                //    Width = 20,
                //    Height = 18,
                //    Volume = 25m * 20m * 18m,
                //    Quantity = 16,
                //    ItemType = 2,
                //    ProdDate = new DateOnly(2026, 2, 10),
                //    ExpDate = new DateOnly(2026, 9, 1),
                //    ReceiptDate = new DateOnly(2026, 2, 15),
                //    ProdBatchNo = "LOT-X",
                //    TrxB_id = 4,
                //    CDO_name = "LOT"
                //},
                //new Items
                //{
                //    ItemNo = 21,
                //    Weight = 9,
                //    Length = 26,
                //    Width = 22,
                //    Height = 16,
                //    Volume = 26m * 22m * 16m,
                //    Quantity = 12,
                //    ItemType = 2,
                //    ProdDate = new DateOnly(2026, 2, 10),
                //    ExpDate = new DateOnly(2026, 10, 1),
                //    ReceiptDate = new DateOnly(2026, 2, 16),
                //    ProdBatchNo = "LOT-X",
                //    TrxB_id = 5,
                //    CDO_name = "LOT"
                //},
                //new Items
                //{
                //    ItemNo = 22,
                //    Weight = 9,
                //    Length = 26,
                //    Width = 22,
                //    Height = 16,
                //    Volume = 26m * 22m * 16m,
                //    Quantity = 10,
                //    ItemType = 2,
                //    ProdDate = new DateOnly(2026, 2, 11),
                //    ExpDate = new DateOnly(2026, 10, 1),
                //    ReceiptDate = new DateOnly(2026, 2, 16),
                //    ProdBatchNo = "LOT-Y",
                //    TrxB_id = 6,
                //    CDO_name = "LOT"
                //},
                //new Items
                //{
                //    ItemNo = 30,
                //    Weight = 7,
                //    Length = 20,
                //    Width = 20,
                //    Height = 20,
                //    Volume = 20m * 20m * 20m,
                //    Quantity = 25,
                //    ItemType = 3,
                //    ProdDate = new DateOnly(2026, 1, 1),
                //    ExpDate = new DateOnly(2028, 1, 1),
                //    ReceiptDate = new DateOnly(2026, 3, 1),
                //    ProdBatchNo = "FREE-1",
                //    TrxB_id = 7,
                //    CDO_name = "FREE"
                //}
            ];
        }
    }

    #endregion
}
