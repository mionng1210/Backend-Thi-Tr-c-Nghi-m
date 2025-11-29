using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExamsService.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExamsService.Controllers
{
    [ApiController]
    [Route("api/Statistics")]
    public class StatisticsController : ControllerBase
    {
        private readonly ExamsDbContext _db;

        public StatisticsController(ExamsDbContext db)
        {
            _db = db;
        }

        public class OverallStatistics
        {
            public long TotalExams { get; set; }
            public long TotalUsers { get; set; }
            public long TotalStudents { get; set; }
            public long TotalTeachers { get; set; }
            public long TotalQuestions { get; set; }
            public decimal AverageScore { get; set; }
            public decimal PassRate { get; set; }
            public long ActiveExams { get; set; }
            public long CompletedExams { get; set; }
        }

        public class ScoreBucket
        {
            public string Range { get; set; } = string.Empty;
            public int Count { get; set; }
            public decimal Percentage { get; set; }
        }

        public class ExamStatistic
        {
            public string Id { get; set; } = string.Empty;
            public string ExamName { get; set; } = string.Empty;
            public string? Subject { get; set; }
            public int TotalParticipants { get; set; }
            public decimal AverageScore { get; set; }
            public decimal HighestScore { get; set; }
            public decimal LowestScore { get; set; }
            public decimal PassRate { get; set; }
            public int? Duration { get; set; }
            public string? Date { get; set; }
            public List<ScoreBucket> ScoreDistribution { get; set; } = new();
        }

        [HttpGet("overall")]
        public async Task<IActionResult> GetOverall()
        {
            var now = DateTime.UtcNow;

            var totalExams = await _db.Exams.AsNoTracking().LongCountAsync(e => !e.HasDelete);
            var totalUsers = await _db.Users.AsNoTracking().LongCountAsync(u => !u.HasDelete);
            var totalStudents = await _db.Users.AsNoTracking().Include(u => u.Role).LongCountAsync(u => !u.HasDelete && u.Role != null && u.Role.Name == "Student");
            var totalTeachers = await _db.Users.AsNoTracking().Include(u => u.Role).LongCountAsync(u => !u.HasDelete && u.Role != null && u.Role.Name == "Teacher");
            var totalQuestions = await _db.Questions.AsNoTracking().LongCountAsync(q => !q.HasDelete);

            var examsInfo = await _db.Exams.AsNoTracking().Select(e => new { e.ExamId, e.TotalMarks, e.PassingMark, e.Status, e.StartAt, e.EndAt }).ToListAsync();
            var activeExams = examsInfo.LongCount(e => (e.Status != null && e.Status.Equals("Active", StringComparison.OrdinalIgnoreCase)) || (e.StartAt.HasValue && e.EndAt.HasValue && e.StartAt.Value <= now && e.EndAt.Value >= now));
            var completedExams = examsInfo.LongCount(e => (e.Status != null && e.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase)) || (e.EndAt.HasValue && e.EndAt.Value < now));

            var attempts = await _db.ExamAttempts.AsNoTracking().Select(a => new { a.Score, a.MaxScore, a.ExamId }).ToListAsync();
            var scores10 = attempts.Where(a => a.Score.HasValue && a.MaxScore.HasValue && a.MaxScore.Value > 0).Select(a => (a.Score!.Value / a.MaxScore!.Value) * 10m).ToList();
            var avgScore = scores10.Any() ? scores10.Average() : 0m;

            var passCount = await _db.ExamAttempts
                .AsNoTracking()
                .Join(_db.Exams.AsNoTracking(), a => a.ExamId, e => e.ExamId, (a, e) => new { a.Score, e.PassingMark })
                .CountAsync(x => x.Score.HasValue && x.PassingMark.HasValue && x.Score.Value >= x.PassingMark.Value);
            var totalAttempts = await _db.ExamAttempts.AsNoTracking().CountAsync();
            var passRate = totalAttempts > 0 ? (decimal)passCount * 100m / (decimal)totalAttempts : 0m;

            var payload = new OverallStatistics
            {
                TotalExams = totalExams,
                TotalUsers = totalUsers,
                TotalStudents = totalStudents,
                TotalTeachers = totalTeachers,
                TotalQuestions = totalQuestions,
                AverageScore = Math.Round(avgScore, 2),
                PassRate = Math.Round(passRate, 2),
                ActiveExams = activeExams,
                CompletedExams = completedExams
            };

            return Ok(payload);
        }

        [HttpGet("exams")]
        public async Task<IActionResult> GetExamsStats([FromQuery] string? subject, [FromQuery] DateTime? dateFrom, [FromQuery] DateTime? dateTo, [FromQuery] string? status)
        {
            try
            {
            var examsQuery = _db.Exams
                .AsNoTracking()
                .Where(e => !e.HasDelete)
                .AsQueryable();

            // Subject filter will be applied after fetching, using Subjects dictionary
            if (dateFrom.HasValue)
            {
                examsQuery = examsQuery.Where(e => e.StartAt.HasValue && e.StartAt.Value.Date >= dateFrom.Value.Date);
            }
            if (dateTo.HasValue)
            {
                examsQuery = examsQuery.Where(e => e.EndAt.HasValue && e.EndAt.Value.Date <= dateTo.Value.Date);
            }
            if (!string.IsNullOrWhiteSpace(status))
            {
                examsQuery = examsQuery.Where(e => e.Status != null && e.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
            }

            var exams = await examsQuery.ToListAsync();
            var subjectIds = exams.Where(e => e.SubjectId.HasValue).Select(e => e.SubjectId!.Value).Distinct().ToList();
            var subjectsById = await _db.Subjects.AsNoTracking().Where(s => subjectIds.Contains(s.SubjectId)).ToDictionaryAsync(s => s.SubjectId, s => s.Name);

            if (!string.IsNullOrWhiteSpace(subject))
            {
                exams = exams.Where(e => e.SubjectId.HasValue && subjectsById.TryGetValue(e.SubjectId!.Value, out var name) && string.Equals(name, subject, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            var examIds = exams.Select(e => e.ExamId).ToList();

            var attemptsAll = await _db.ExamAttempts
                .AsNoTracking()
                .Where(a => examIds.Contains(a.ExamId))
                .Select(a => new { a.ExamId, a.Score, a.MaxScore })
                .ToListAsync();
            var attemptsByExam = attemptsAll
                .GroupBy(a => a.ExamId)
                .Select(g => new { ExamId = g.Key, Items = g.Select(a => new { a.Score, a.MaxScore }).ToList(), Count = g.Count() })
                .ToList();

            var stats = new List<ExamStatistic>();
            foreach (var e in exams)
            {
                var group = attemptsByExam.FirstOrDefault(x => x.ExamId == e.ExamId);
                var scores = new List<decimal>();
                if (group != null)
                {
                    foreach (var item in group.Items)
                    {
                        if (item.Score.HasValue && item.MaxScore.HasValue && item.MaxScore.Value > 0)
                        {
                            scores.Add((item.Score.Value / item.MaxScore.Value) * 10m);
                        }
                    }
                }

                decimal avg = scores.Any() ? scores.Average() : 0m;
                decimal highest = scores.Any() ? scores.Max() : 0m;
                decimal lowest = scores.Any() ? scores.Min() : 0m;

                int pass = 0;
                if (group != null)
                {
                    var passMark = e.PassingMark ?? 0m;
                    pass = group.Items.Count(x => x.Score.HasValue && x.Score.Value >= passMark);
                }
                var totalRes = group?.Items.Count ?? 0;
                var passRate = totalRes > 0 ? (decimal)pass * 100m / (decimal)totalRes : 0m;

                var distribution = BuildDistribution(scores);

                stats.Add(new ExamStatistic
                {
                    Id = e.ExamId.ToString(),
                    ExamName = e.Title,
                    Subject = (e.SubjectId.HasValue && subjectsById.TryGetValue(e.SubjectId.Value, out var sname)) ? sname : null,
                    TotalParticipants = group?.Count ?? 0,
                    AverageScore = Math.Round(avg, 2),
                    HighestScore = Math.Round(highest, 2),
                    LowestScore = Math.Round(lowest, 2),
                    PassRate = Math.Round(passRate, 2),
                    Duration = e.DurationMinutes,
                    Date = e.StartAt?.ToString("yyyy-MM-dd"),
                    ScoreDistribution = distribution
                });
            }

            return Ok(stats);
            }
            catch (Exception ex)
            {
                try
                {
                    var fallbackExams = await _db.Exams.AsNoTracking().Where(e => !e.HasDelete).ToListAsync();
                    var subjectIds = fallbackExams.Where(e => e.SubjectId.HasValue).Select(e => e.SubjectId!.Value).Distinct().ToList();
                    var subjectsById = await _db.Subjects.AsNoTracking().Where(s => subjectIds.Contains(s.SubjectId)).ToDictionaryAsync(s => s.SubjectId, s => s.Name);

                    var basic = fallbackExams.Select(e => new ExamStatistic
                    {
                        Id = e.ExamId.ToString(),
                        ExamName = e.Title,
                        Subject = (e.SubjectId.HasValue && subjectsById.TryGetValue(e.SubjectId.Value, out var sname)) ? sname : null,
                        TotalParticipants = 0,
                        AverageScore = 0,
                        HighestScore = 0,
                        LowestScore = 0,
                        PassRate = 0,
                        Duration = e.DurationMinutes,
                        Date = e.StartAt?.ToString("yyyy-MM-dd"),
                        ScoreDistribution = new List<ScoreBucket>()
                    }).ToList();

                    return Ok(basic);
                }
                catch
                {
                    return StatusCode(500, new { message = ex.Message });
                }
            }
        }

        private static List<ScoreBucket> BuildDistribution(List<decimal> normalizedScores)
        {
            var ranges = new List<(string name, decimal min, decimal max)>
            {
                ("9-10", 9m, 10m),
                ("8-8.9", 8m, 8.9m),
                ("7-7.9", 7m, 7.9m),
                ("6-6.9", 6m, 6.9m),
                ("5-5.9", 5m, 5.9m),
                ("0-4.9", 0m, 4.9m)
            };

            var total = normalizedScores.Count;
            var buckets = new List<ScoreBucket>();
            foreach (var r in ranges)
            {
                var count = normalizedScores.Count(s => s >= r.min && s <= r.max);
                var percentage = total > 0 ? ((decimal)count * 100m / (decimal)total) : 0m;
                buckets.Add(new ScoreBucket { Range = r.name, Count = count, Percentage = Math.Round(percentage, 2) });
            }
            return buckets;
        }
    }
}
