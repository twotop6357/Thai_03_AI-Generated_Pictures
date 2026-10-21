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
    public sealed class SurveyQuestion
    {
        public string Id { get; }
        public string Text { get; }
        internal SurveyQuestion(string id, string text) { Id = id; Text = text; }
    }

    /// <summary>순서는 Very Dissatisfied → Very Satisfied. 초기 비율은 가상 응답을 저장하지 않는다.</summary>
    public sealed class SurveyQuestionStatistics
    {
        public string QuestionId { get; }
        public IReadOnlyList<long> Counts { get; }
        public IReadOnlyList<double> Percentages { get; }
        public long TotalResponses { get; }

        internal SurveyQuestionStatistics(string id, long[] counts)
        {
            QuestionId = id;
            long total = 0;
            foreach (long count in counts) total = checked(total + count);
            TotalResponses = total;
            var percentages = new double[SurveyStore.OptionCount];
            for (int i = 0; i < percentages.Length; i++)
                percentages[i] = total == 0 ? 20.0 : counts[i] / (double)total * 100.0;
            Counts = Array.AsReadOnly(counts);
            Percentages = Array.AsReadOnly(percentages);
        }
    }

    /// <summary>Windows 키오스크의 로컬 질문·집계 저장. ID로 연결하며 이전 집계 손상 시 덮어쓰지 않는다.</summary>
    public sealed class SurveyStore
    {
        public const string DirectoryName = "Survey";
        public const int OptionCount = 5;
        private const int MaxFileBytes = 1024 * 1024;
        private const int ReceiptCapacity = 128;
        public static IReadOnlyList<string> OptionIds { get; } = Array.AsReadOnly(new[]
            { "very_dissatisfied", "dissatisfied", "neutral", "satisfied", "very_satisfied" });
        public static IReadOnlyList<string> OptionLabels { get; } = Array.AsReadOnly(new[]
            { "Very Dissatisfied", "Dissatisfied", "Neutral", "Satisfied", "Very Satisfied" });

        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        private readonly string _directory;
        public string QuestionsPath => Path.Combine(_directory, "questions.json");
        public string StatisticsPath => Path.Combine(_directory, "statistics.json");

        public SurveyStore(string directory) { _directory = Path.GetFullPath(directory); }

        public Task<IReadOnlyList<SurveyQuestion>> LoadQuestionsAsync(CancellationToken token)
        {
            return Task.Run<IReadOnlyList<SurveyQuestion>>(() =>
            {
                token.ThrowIfCancellationRequested();
                var root = ReadObject(QuestionsPath);
                RequireVersion(root);
                var questions = root["questions"] as JArray;
                if (questions == null || questions.Count < 1 || questions.Count > 64)
                    throw new InvalidDataException("Survey requires between one and 64 questions.");
                var result = new List<SurveyQuestion>(questions.Count);
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in questions)
                {
                    string id = (string)item["id"];
                    string text = (string)item["text"];
                    ValidateId(id);
                    if (!ids.Add(id) || string.IsNullOrWhiteSpace(text) || text.Length > 1000)
                        throw new InvalidDataException("Survey question IDs must be unique and text must contain 1-1000 characters.");
                    result.Add(new SurveyQuestion(id, text));
                }
                token.ThrowIfCancellationRequested();
                return result.AsReadOnly();
            }, token);
        }

        public Task<SurveyQuestionStatistics> GetStatisticsAsync(string questionId, CancellationToken token)
        {
            ValidateId(questionId);
            return Task.Run(async () =>
            {
                await Gate.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    token.ThrowIfCancellationRequested();
                    return Snapshot(ReadStatistics(), questionId);
                }
                finally { Gate.Release(); }
            }, token);
        }

        /// <summary>동일 제출 UUID 재시도는 최근128회 안에서 중복 집계하지 않는다. 취소 검사는 교체 직전까지.</summary>
        public Task<SurveyQuestionStatistics> RecordAnswerAsync(string questionId, int optionIndex,
            string submissionId, CancellationToken token)
        {
            ValidateId(questionId);
            if (optionIndex < 0 || optionIndex >= OptionCount) throw new ArgumentOutOfRangeException(nameof(optionIndex));
            if (!Guid.TryParseExact(submissionId, "D", out _)) throw new ArgumentException("A submission UUID is required.");
            return Task.Run(async () =>
            {
                await Gate.WaitAsync(token).ConfigureAwait(false);
                string temp = null;
                try
                {
                    token.ThrowIfCancellationRequested();
                    var root = ReadStatistics();
                    var receipts = (JArray)root["recentSubmissions"];
                    foreach (var receipt in receipts)
                    {
                        if ((string)receipt["id"] != submissionId) continue;
                        if ((string)receipt["questionId"] != questionId || (string)receipt["optionId"] != OptionIds[optionIndex])
                            throw new InvalidDataException("Submission UUID was already used for a different answer.");
                        return Snapshot(root, questionId);
                    }
                    var questions = (JObject)root["questions"];
                    var before = Snapshot(root, questionId);
                    if (before.TotalResponses == long.MaxValue) throw new OverflowException("Survey response count is full.");
                    var counts = questions[questionId] as JObject;
                    if (counts == null)
                    {
                        if (questions.Count >= 1024) throw new InvalidDataException("Survey question history limit reached.");
                        counts = new JObject();
                        foreach (string id in OptionIds) counts[id] = 0L;
                        questions[questionId] = counts;
                    }
                    counts[OptionIds[optionIndex]] = checked(before.Counts[optionIndex] + 1);
                    receipts.Add(new JObject { ["id"] = submissionId, ["questionId"] = questionId, ["optionId"] = OptionIds[optionIndex] });
                    while (receipts.Count > ReceiptCapacity) receipts.RemoveAt(0);
                    root["updatedUtc"] = DateTime.UtcNow.ToString("O");
                    string json = root.ToString(Formatting.Indented);
                    if (Encoding.UTF8.GetByteCount(json) > MaxFileBytes) throw new InvalidDataException("Survey statistics size limit reached.");
                    Directory.CreateDirectory(_directory);
                    temp = Path.Combine(_directory, "statistics." + Guid.NewGuid().ToString("N") + ".tmp");
                    using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        byte[] bytes = new UTF8Encoding(false).GetBytes(json);
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush(true);
                    }
                    // Windows 파일 감시/백신의 순간 잠금은 최대3회 재시도한다. 같은 임시 파일만 재사용한다.
                    for (int attempt = 0; ; attempt++)
                    {
                        token.ThrowIfCancellationRequested();
                        try
                        {
                            if (File.Exists(StatisticsPath)) File.Replace(temp, StatisticsPath, null);
                            else File.Move(temp, StatisticsPath);
                            break;
                        }
                        catch (IOException) when (attempt < 3 && File.Exists(temp))
                        {
                            await Task.Delay(40, token).ConfigureAwait(false);
                        }
                    }
                    temp = null;
                    // 커밋 이후 취소되어도 저장 성공을 반환하여 같은 응답 재시도를 방지한다.
                    return Snapshot(root, questionId);
                }
                finally
                {
                    try { if (temp != null && File.Exists(temp)) File.Delete(temp); }
                    finally { Gate.Release(); }
                }
            }, token);
        }

        private JObject ReadStatistics()
        {
            if (!File.Exists(StatisticsPath)) return new JObject
            {
                ["version"] = 1, ["questions"] = new JObject(), ["recentSubmissions"] = new JArray()
            };
            var root = ReadObject(StatisticsPath);
            RequireVersion(root);
            var questions = root["questions"] as JObject;
            var receipts = root["recentSubmissions"] as JArray;
            if (questions == null || questions.Count > 1024 || receipts == null || receipts.Count > ReceiptCapacity)
                throw new InvalidDataException("Survey statistics structure is invalid.");
            foreach (var question in questions.Properties())
            {
                ValidateId(question.Name);
                Snapshot(root, question.Name);
            }
            foreach (var receipt in receipts)
            {
                if (!(receipt is JObject) || !Guid.TryParseExact((string)receipt["id"], "D", out _))
                    throw new InvalidDataException("Survey submission receipt is invalid.");
                ValidateId((string)receipt["questionId"]);
                bool valid = false;
                foreach (string optionId in OptionIds) if ((string)receipt["optionId"] == optionId) valid = true;
                if (!valid) throw new InvalidDataException("Survey submission option is invalid.");
            }
            return root;
        }

        private static SurveyQuestionStatistics Snapshot(JObject root, string id)
        {
            var counts = new long[OptionCount];
            var item = root["questions"][id];
            if (item != null)
            {
                if (!(item is JObject values) || values.Count != OptionCount)
                    throw new InvalidDataException("Survey answer counts are invalid.");
                for (int i = 0; i < counts.Length; i++)
                {
                    var value = values[OptionIds[i]];
                    if (value == null || value.Type != JTokenType.Integer || (long)value < 0)
                        throw new InvalidDataException("Survey answer counts must be nonnegative integers.");
                    counts[i] = (long)value;
                }
            }
            return new SurveyQuestionStatistics(id, counts);
        }

        private static JObject ReadObject(string path)
        {
            if (new FileInfo(path).Length > MaxFileBytes) throw new InvalidDataException("Survey JSON exceeds the size limit.");
            using (var reader = new JsonTextReader(new StreamReader(path, Encoding.UTF8)) { MaxDepth = 12 })
            {
                var result = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new InvalidDataException("Survey JSON contains trailing content.");
                return result;
            }
        }

        private static void RequireVersion(JObject root)
        {
            if (root["version"]?.Type != JTokenType.Integer || (int)root["version"] != 1)
                throw new InvalidDataException("Unsupported survey JSON version.");
        }

        private static void ValidateId(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 64) throw new InvalidDataException("Survey question ID must contain 1-64 characters.");
            foreach (char c in id)
                if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '_' && c != '-')
                    throw new InvalidDataException("Survey question ID must use lowercase letters, digits, underscores or hyphens.");
        }
    }
}
