using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Core
{
    public sealed class FoodHistoryEntry
    {
        public string Id { get; }
        public string CreatedUtc { get; }
        public IReadOnlyList<IngredientBlock> Ingredients { get; }

        internal FoodHistoryEntry(string id, string createdUtc, IngredientBlock[] ingredients)
        {
            Id = id;
            CreatedUtc = createdUtc;
            Ingredients = Array.AsReadOnly(ingredients);
        }
    }

    public sealed class FoodHistoryPage
    {
        public List<FoodHistoryEntry> Entries { get; } = new List<FoodHistoryEntry>();
        public int TotalCount { get; internal set; }
        public int InvalidCount { get; internal set; }
    }

    /// <summary>디스크에 최근 12장의 PNG·재료를 보관한다. 인덱스 순서는 오래된 것부터 최신 순서다.</summary>
    public sealed class GeneratedFoodHistory
    {
        public const string DirectoryName = "Thai03FoodHistory";
        public const int Capacity = 12;
        public const int MaxImageBytes = 16 * 1024 * 1024;
        private static readonly SemaphoreSlim Writer = new SemaphoreSlim(1, 1);
        private readonly string _directory;
        private string IndexPath => Path.Combine(_directory, "index.jsonl");

        public GeneratedFoodHistory(string directory)
        {
            _directory = Path.GetFullPath(directory);
        }

        public Task SaveAsync(string promptId, IngredientBlock[] ingredients, byte[] png, CancellationToken token)
        {
            if (!Guid.TryParseExact(promptId, "D", out _)) throw new ArgumentException("A prompt UUID is required.");
            ValidateIngredients(ingredients);
            if (png == null || png.Length < 8 || png.Length > MaxImageBytes ||
                png[0] != 137 || png[1] != 80 || png[2] != 78 || png[3] != 71 ||
                png[4] != 13 || png[5] != 10 || png[6] != 26 || png[7] != 10)
                throw new ArgumentException("A PNG image within the history size limit is required.");
            var snapshot = (IngredientBlock[])ingredients.Clone();
            return Task.Run(async () =>
            {
                await Writer.WaitAsync(token).ConfigureAwait(false);
                string imageTemp = null;
                string indexTemp = null;
                string imagePath = Path.Combine(_directory, promptId + ".png");
                bool newImage = false;
                bool committed = false;
                var retained = new List<JObject>(Capacity);
                var evicted = new List<string>();
                try
                {
                    Directory.CreateDirectory(_directory);
                    if (File.Exists(IndexPath))
                    {
                        using (var reader = OpenIndex())
                        {
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                token.ThrowIfCancellationRequested();
                                if (!TryParse(line, out var entry)) continue;
                                if (entry.Id == promptId) return;
                                if (!File.Exists(Path.Combine(_directory, entry.Id + ".png"))) continue;
                                var record = JObject.Parse(line);
                                record["ingredientNames"] = GetIngredientNames(entry.Ingredients);
                                retained.Add(record);
                                if (retained.Count >= Capacity)
                                {
                                    evicted.Add((string)retained[0]["id"]);
                                    retained.RemoveAt(0);
                                }
                            }
                        }
                    }
                    string suffix = Guid.NewGuid().ToString("N") + ".tmp";
                    imageTemp = Path.Combine(_directory, promptId + suffix);
                    indexTemp = Path.Combine(_directory, "index." + suffix);
                    File.WriteAllBytes(imageTemp, png);
                    token.ThrowIfCancellationRequested();
                    newImage = !File.Exists(imagePath);
                    if (newImage) File.Move(imageTemp, imagePath);
                    else await CommitAsync(imageTemp, imagePath, token).ConfigureAwait(false);
                    imageTemp = null;

                    // 최근 11개 뒤에 새 사진을 붙인 인덱스를 한 번에 교체한다.
                    using (var output = new FileStream(indexTemp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        using (var writer = new StreamWriter(output, new UTF8Encoding(false), 1024, true))
                        {
                            foreach (var record in retained) writer.WriteLine(record.ToString(Formatting.None));
                            writer.WriteLine(new JObject
                            {
                                ["id"] = promptId,
                                ["createdUtc"] = DateTime.UtcNow.ToString("O"),
                                ["ingredients"] = JArray.FromObject(snapshot),
                                ["ingredientNames"] = GetIngredientNames(snapshot)
                            }.ToString(Formatting.None));
                        }
                        output.Flush(true);
                    }
                    token.ThrowIfCancellationRequested();
                    await CommitAsync(indexTemp, IndexPath, token).ConfigureAwait(false);
                    indexTemp = null;
                    committed = true;
                    foreach (string id in evicted)
                    {
                        // 저장소 자신이 소유한 UUID PNG만 제거한다. 외부 ComfyUI 원본은 건드리지 않는다.
                        string obsolete = Path.GetFullPath(Path.Combine(_directory, id + ".png"));
                        if (!obsolete.StartsWith(_directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                                StringComparison.OrdinalIgnoreCase))
                            throw new IOException("History cleanup path is outside its directory.");
                        if (File.Exists(obsolete)) File.Delete(obsolete);
                    }
                }
                finally
                {
                    try
                    {
                        if (imageTemp != null && File.Exists(imageTemp)) File.Delete(imageTemp);
                        if (indexTemp != null && File.Exists(indexTemp)) File.Delete(indexTemp);
                        if (newImage && !committed && File.Exists(imagePath)) File.Delete(imagePath);
                    }
                    finally { Writer.Release(); }
                }
            }, token);
        }

        public Task<FoodHistoryPage> ReadPageAsync(int offset, int pageSize, CancellationToken token)
        {
            if (offset < 0 || pageSize < 1 || pageSize > 64) throw new ArgumentOutOfRangeException(nameof(pageSize));
            return Task.Run(() =>
            {
                var page = new FoodHistoryPage();
                if (!File.Exists(IndexPath)) return page;
                using (var reader = OpenIndex())
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        token.ThrowIfCancellationRequested();
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        if (!TryParse(line, out var entry) || !File.Exists(Path.Combine(_directory, entry.Id + ".png")))
                        {
                            page.InvalidCount++;
                            continue;
                        }
                        if (page.TotalCount >= offset && page.Entries.Count < pageSize) page.Entries.Add(entry);
                        page.TotalCount++;
                    }
                }
                return page;
            }, token);
        }

        public Task<byte[]> ReadImageAsync(FoodHistoryEntry entry, CancellationToken token)
        {
            return Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                string path = Path.Combine(_directory, entry.Id + ".png");
                long size = new FileInfo(path).Length;
                if (size < 8 || size > MaxImageBytes) throw new IOException("History image exceeds the size limit or is empty.");
                var bytes = File.ReadAllBytes(path);
                token.ThrowIfCancellationRequested();
                return bytes;
            }, token);
        }

        private StreamReader OpenIndex()
        {
            return new StreamReader(new FileStream(IndexPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete), Encoding.UTF8);
        }

        private static JArray GetIngredientNames(IReadOnlyList<IngredientBlock> ingredients)
        {
            var names = new JArray();
            for (int i = 0; i < ingredients.Count; i++)
                names.Add(IngredientBlockNames.GetDisplayName(ingredients[i]));
            return names;
        }

        private static async Task CommitAsync(string temporary, string destination, CancellationToken token)
        {
            for (int attempt = 0; ; attempt++)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    if (File.Exists(destination)) File.Replace(temporary, destination, null);
                    else File.Move(temporary, destination);
                    return;
                }
                catch (IOException) when (attempt < 3 && File.Exists(temporary))
                {
                    // Windows 파일 교체가 일시적으로 거절되어도 기존 인덱스는 지우지 않는다.
                    await Task.Delay(40, token).ConfigureAwait(false);
                }
            }
        }

        private static bool TryParse(string line, out FoodHistoryEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(line) || line.Length > 4096) return false;
            try
            {
                var json = JObject.Parse(line);
                string id = (string)json["id"];
                if (!Guid.TryParseExact(id, "D", out _)) return false;
                var ingredients = json["ingredients"]?.ToObject<IngredientBlock[]>();
                ValidateIngredients(ingredients);
                entry = new FoodHistoryEntry(id, (string)json["createdUtc"], ingredients);
                return true;
            }
            catch (JsonException) { return false; }
            catch (ArgumentException) { return false; }
            catch (FormatException) { return false; }
            catch (InvalidCastException) { return false; }
            catch (OverflowException) { return false; }
        }

        private static void ValidateIngredients(IngredientBlock[] ingredients)
        {
            if (ingredients == null || ingredients.Length < 1 || ingredients.Length > 6)
                throw new ArgumentException("One to six ingredient blocks are required.");
            int seen = 0;
            foreach (var block in ingredients)
            {
                int index = (int)block;
                if (index < 0 || index > (int)IngredientBlock.CoconutMilk || (seen & (1 << index)) != 0)
                    throw new ArgumentException("Ingredients must be valid and unique.");
                seen |= 1 << index;
            }
        }
    }
}
