using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using GatherBuddy;
using GatherBuddy.Classes;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;
using OtterGui.Log;
using Action = System.Action;

namespace Scrounger;

public class WorldData : GameData
{
    private string WorldLocationsPath =
        Path.Combine(Svc.PluginInterface.ConfigDirectory.FullName, "world_locations.json");

    private string NodeOffsetsPath =
        Path.Combine(Svc.PluginInterface.ConfigDirectory.FullName, "node_offsets.json");

    public WorldData(IDataManager gameData, Logger log)
        : base(gameData, log, Path.Combine(Svc.PluginInterface.ConfigDirectory.FullName, "fish_overrides.json"))
    {
        LoadLocationsFromFile();
        LoadOffsetsFromFile();
        LoadIlvConvertTableFromFile();
        GetGatheringPointPositions();
    }

    private void GetGatheringPointPositions()
    {
        var sheet = Svc.Data.GetExcelSheet<GatheringPoint>().Where(row => row.PlaceName.RowId > 0)
            .GroupBy(row => row.GatheringPointBase.RowId).ToFrozenDictionary(group => group.Key,
                group => group.Select(g => g.RowId).Distinct().ToList());

        foreach (var node in GatheringNodes)
        {
            var nodeList = sheet.TryGetValue(node.Value.BaseNodeData.RowId, out var nl)
                ? (IReadOnlyList<uint>)nl
                : Array.Empty<uint>();
            foreach (var nodeRow2 in nodeList)
            {
                var worldCoords = WorldLocationsByNodeId.TryGetValue(nodeRow2, out var wc) ? wc : new List<Vector3>();

                if (NodeLocations.TryGetValue(node.Value.Id, out var nodeLocs))
                {
                    if (!nodeLocs.TryAdd(nodeRow2, worldCoords))
                        nodeLocs[nodeRow2].AddRange(worldCoords);
                }
                else
                {
                    NodeLocations[node.Value.Id] = new Dictionary<uint, List<Vector3>> { { nodeRow2, worldCoords } };
                }
            }

            Svc.Log.Debug(
                $"Loaded {NodeLocations[node.Value.Id].Count} locations for node {node.Value.BaseNodeData.RowId} ({node.Value.Name})");
        }
    }

    public Dictionary<uint, List<Vector3>> WorldLocationsByNodeId { get; set; } = new();
    public Dictionary<Vector3, Vector3> NodeOffsets { get; set; } = new();

    public Dictionary<uint, Dictionary<uint, List<Vector3>>> NodeLocations { get; set; } = new();

    public ReadOnlyCollection<(ushort BaseGathering, ushort BasePerception)> IlvConvertTable { get; private set; }

    private void LoadOffsetsFromFile()
    {
        var settings = new JsonSerializerSettings();
        var resourceName = "Scrounger.Data.node_offsets.json";
        NodeOffsets = LoadFromFile<List<OffsetPair>>(NodeOffsetsPath, resourceName, settings).GroupBy(x => x.Original)
            .ToDictionary(x => x.Key, x => x.First().Offset);
    }

    private void LoadLocationsFromFile()
    {
        var resourceName = "Scrounger.Data.world_locations.json";
        WorldLocationsByNodeId =
            LoadFromFile<Dictionary<uint, List<Vector3>>>(WorldLocationsPath, resourceName,
                new JsonSerializerSettings());
    }

    private T LoadFromFile<T>(string path, string resourceName, JsonSerializerSettings settings)
    {
        var assembly = typeof(Scrounger).Assembly;
        T defaultObj;

        // Load the embedded resource
        using (var stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream == null)
                throw new FileNotFoundException("Embedded resource not found.", resourceName);

            using (var reader = new StreamReader(stream))
            {
                var defaultContent = reader.ReadToEnd();
                defaultObj = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(defaultContent, settings);
            }
        }

        // Check if the file exists
        if (!File.Exists(path))
        {
            File.WriteAllText(path,
                Newtonsoft.Json.JsonConvert.SerializeObject(defaultObj, Newtonsoft.Json.Formatting.Indented, settings));
        }
        else
        {
            var fileContent = File.ReadAllText(path);
            var existingObj = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(fileContent, settings)
                              ?? Activator.CreateInstance<T>();

            // Depending on the type of T, you might need to perform different operations here
            // In this case we assume T is a Dictionary<uint, List<Vector3>>, but for other types T you might need other merge operations
            if (defaultObj is Dictionary<uint, List<Vector3>> defaultDict1 &&
                existingObj is Dictionary<uint, List<Vector3>> existingDict1)
            {
                File.WriteAllText(path,
                    Newtonsoft.Json.JsonConvert.SerializeObject(MergeData(defaultDict1, existingDict1),
                        Newtonsoft.Json.Formatting.Indented, settings));
            }
            else if (defaultObj is List<OffsetPair> defaultDict2 && existingObj is List<OffsetPair> existingDict2)
            {
                File.WriteAllText(path,
                    Newtonsoft.Json.JsonConvert.SerializeObject(MergeData(defaultDict2, existingDict2),
                        Newtonsoft.Json.Formatting.Indented, settings));
            }
        }

        // Read the content of the file
        var locJson = File.ReadAllText(path);
        var obj = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(locJson, settings);
        return obj ?? Activator.CreateInstance<T>();
    }

    private Dictionary<uint, List<Vector3>> MergeData(Dictionary<uint, List<Vector3>> defaultData,
        Dictionary<uint, List<Vector3>> existingData)
    {
        foreach (var kvp in defaultData)
        {
            if (existingData.TryGetValue(kvp.Key, out var existingList))
            {
                foreach (var vector in kvp.Value)
                {
                    if (!existingList.Contains(vector))
                    {
                        existingList.Add(vector);
                    }
                }
            }
            else
            {
                existingData[kvp.Key] = new List<Vector3>(kvp.Value);
            }
        }

        return existingData;
    }

    private List<OffsetPair> MergeData(List<OffsetPair> defaultData, List<OffsetPair> existingData)
    {
        return existingData.Concat(defaultData).GroupBy(v => v.Original).Select(v => v.First()).ToList();
    }

    public void AddOffset(Vector3 original, Vector3 offset)
    {
        if (NodeOffsets.ContainsKey(original))
        {
            Scrounger.Log.Error(
                $"{original} already has an offset of {NodeOffsets[original]}. Unable to add new offset.");
            return;
        }

        NodeOffsets[original] = offset;
        Task.Run(SaveOffsetsToFile);
    }

    public void SaveOffsetsToFile()
    {
        var settings = new JsonSerializerSettings();
        var offsetJson = Newtonsoft.Json.JsonConvert.SerializeObject(
            NodeOffsets.Select(x => new OffsetPair(x.Key, x.Value)).ToList(), Formatting.Indented, settings);

        File.WriteAllText(NodeOffsetsPath, offsetJson);
    }

    public void AddLocation(uint nodeId, Vector3 location)
    {
        if (!WorldLocationsByNodeId.TryGetValue(nodeId, out var list))
        {
            lock (WorldLocationsByNodeId)
                WorldLocationsByNodeId[nodeId] = list = [];
            // foreach (var node in GameData.GatheringNodes.Values.Where(v =>
            //              v.WorldPositions.ContainsKey(nodeId)))
            //     node.WorldPositions[nodeId] = list;
        }

        if (!list.Contains(location))
        {
            lock (WorldLocationsByNodeId)
                list.Add(location);

            Task.Run(() =>
            {
                lock (WorldLocationsByNodeId) SaveLocationsToFile();
            });
            Scrounger.Log.Debug($"Added location {location} to node {nodeId}");
            WorldLocationsChanged?.Invoke();
        }
    }

    public event Action? WorldLocationsChanged;

    public void SaveLocationsToFile()
    {
        var locJson =
            Newtonsoft.Json.JsonConvert.SerializeObject(WorldLocationsByNodeId, Newtonsoft.Json.Formatting.Indented);

        File.WriteAllText(WorldLocationsPath, locJson);
    }

    [MemberNotNull(nameof(IlvConvertTable))]
    private unsafe void LoadIlvConvertTableFromFile()
    {
        var resourceName = "Scrounger.Data.IlvConvertTable.csv";
        var assembly = typeof(Scrounger).Assembly;
        var resource = assembly.GetManifestResourceStream(resourceName) ??
                       throw new FileNotFoundException("Embedded resource not found.", resourceName);
        var stream = new StreamReader(resource);

        stream.ReadLine();
        var list = new List<(ushort, ushort)>(1000);
        while (stream.ReadLine() is string line and { Length: > 5 })
        {
            Span<ushort> values = [.. line.Split(',').Select(ushort.Parse)];
            while (list.Count < values[0] + 1) list.Add((0, 0));
            list[values[0]] = (values[1], values[2]);
        }

        IlvConvertTable = new([.. list]);
    }
}

public record struct OffsetPair(Vector3 Original, Vector3 Offset)
{
}