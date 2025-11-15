using System.Text.Json;
using StackExchange.Redis;

namespace ExamsService.Services
{
    public interface IExamProgressCache
    {
        Task SaveAnswerAsync(int examAttemptId, int questionId, AttemptAnswerCache value, TimeSpan? ttl = null);
        Task SaveBatchAsync(int examAttemptId, IEnumerable<AttemptAnswerCache> answers, TimeSpan? ttl = null);
        Task<Dictionary<int, AttemptAnswerCache>> GetAllAsync(int examAttemptId);
        Task<bool> ExistsAsync(int examAttemptId);
        Task DeleteAsync(int examAttemptId);
        string BuildKey(int examAttemptId);
    }

    public class AttemptAnswerCache
    {
        public int QuestionId { get; set; }
        public List<int> SelectedOptionIds { get; set; } = new();
        public string? TextAnswer { get; set; }
        public DateTime SavedAt { get; set; } = DateTime.UtcNow;
    }

    public class ExamProgressCache : IExamProgressCache
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _db;
        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

        public ExamProgressCache(IConnectionMultiplexer redis)
        {
            _redis = redis;
            _db = _redis.GetDatabase();
        }

        public string BuildKey(int examAttemptId) => $"exam:attempt:{examAttemptId}:answers";

        public async Task SaveAnswerAsync(int examAttemptId, int questionId, AttemptAnswerCache value, TimeSpan? ttl = null)
        {
            var key = BuildKey(examAttemptId);
            value.QuestionId = questionId;
            var json = JsonSerializer.Serialize(value, _jsonOptions);
            await _db.HashSetAsync(key, questionId, json);
            if (ttl.HasValue)
            {
                await _db.KeyExpireAsync(key, ttl);
            }
        }

        public async Task SaveBatchAsync(int examAttemptId, IEnumerable<AttemptAnswerCache> answers, TimeSpan? ttl = null)
        {
            var key = BuildKey(examAttemptId);
            var entries = answers.Select(a => new HashEntry(a.QuestionId, JsonSerializer.Serialize(a, _jsonOptions))).ToArray();
            if (entries.Length > 0)
            {
                await _db.HashSetAsync(key, entries);
            }
            if (ttl.HasValue)
            {
                await _db.KeyExpireAsync(key, ttl);
            }
        }

        public async Task<Dictionary<int, AttemptAnswerCache>> GetAllAsync(int examAttemptId)
        {
            var key = BuildKey(examAttemptId);
            var all = await _db.HashGetAllAsync(key);
            var result = new Dictionary<int, AttemptAnswerCache>();
            foreach (var entry in all)
            {
                if (int.TryParse(entry.Name.ToString(), out var qid))
                {
                    try
                    {
                        var dto = JsonSerializer.Deserialize<AttemptAnswerCache>(entry.Value!, _jsonOptions);
                        if (dto != null)
                        {
                            result[qid] = dto;
                        }
                    }
                    catch
                    {
                        // ignore malformed entries
                    }
                }
            }
            return result;
        }

        public async Task<bool> ExistsAsync(int examAttemptId)
        {
            var key = BuildKey(examAttemptId);
            return await _db.KeyExistsAsync(key);
        }

        public async Task DeleteAsync(int examAttemptId)
        {
            var key = BuildKey(examAttemptId);
            await _db.KeyDeleteAsync(key);
        }
    }
}