using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API_ThiTracNghiem.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API_ThiTracNghiem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StatisticsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public StatisticsController(ApplicationDbContext db)
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
            var totalStudents = await _db.Users.AsNoTracking().Include(u => u.Role).LongCountAsync(u => !u.HasDelete && u.Role != null && u.Role.RoleName == "Student");
            var totalTeachers = await _db.Users.AsNoTracking().Include(u => u.Role).LongCountAsync(u => !u.HasDelete && u.Role != null && u.Role.RoleName == "Teacher");
            var totalQuestions = await _db.Questions.AsNoTracking().LongCountAsync(q => !q.HasDelete);

            var exams = await _db.Exams.AsNoTracking().Where(e => !e.HasDelete).Select(e => new { e.ExamId, e.TotalMarks, e.PassingMark, e.Status, e.StartAt, e.EndAt }).ToListAsync();
            var activeExams = exams.LongCount(e => (e.Status != null && e.Status.Equals("Active", StringComparison.OrdinalIgnoreCase)) || (e.StartAt.HasValue && e.EndAt.HasValue && e.StartAt.Value <= now && e.EndAt.Value >= now));
            var completedExams = exams.LongCount(e => (e.Status != null && e.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase)) || (e.EndAt.HasValue && e.EndAt.Value < now));

            var results = await _db.Results.AsNoTracking().Include(r => r.Exam).Select(r => new { r.TotalScore, ExamTotalMarks = r.Exam != null ? r.Exam.TotalMarks : null, PassingMark = r.Exam != null ? r.Exam.PassingMark : null }).ToListAsync();
            var normalizedScores = results.Where(x => x.TotalScore.HasValue && x.ExamTotalMarks.HasValue && x.ExamTotalMarks.Value > 0).Select(x => (x.TotalScore!.Value / x.ExamTotalMarks!.Value) * 10m).ToList();
            var avgScore = normalizedScores.Any() ? normalizedScores.Average() : 0m;
            var passCount = results.Count(x => x.TotalScore.HasValue && x.PassingMark.HasValue && x.TotalScore!.Value >= x.PassingMark!.Value);
            var totalResultCount = results.Count;
            var passRate = totalResultCount > 0 ? (decimal)passCount * 100m / (decimal)totalResultCount : 0m;

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
            var examsQuery = _db.Exams
                .AsNoTracking()
                .Where(e => !e.HasDelete)
                .Include(e => e.Course)
                .ThenInclude(c => c!.Subject)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(subject))
            {
                examsQuery = examsQuery.Where(e => e.Course != null && e.Course.Subject != null && e.Course.Subject.Name == subject);
            }
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
            var examIds = exams.Select(e => e.ExamId).ToList();

            var resultsByExam = await _db.Results
                .AsNoTracking()
                .Where(r => examIds.Contains(r.ExamId))
                .GroupBy(r => r.ExamId)
                .Select(g => new { ExamId = g.Key, Items = g.Select(r => r.TotalScore).ToList() })
                .ToListAsync();

            var attemptsByExam = await _db.ExamAttempts
                .AsNoTracking()
                .Where(a => examIds.Contains(a.ExamId))
                .GroupBy(a => a.ExamId)
                .Select(g => new { ExamId = g.Key, Count = g.Count() })
                .ToListAsync();

            var stats = new List<ExamStatistic>();
            foreach (var e in exams)
            {
                var resGroup = resultsByExam.FirstOrDefault(x => x.ExamId == e.ExamId);
                var attemptGroup = attemptsByExam.FirstOrDefault(x => x.ExamId == e.ExamId);

                var totalMarks = e.TotalMarks ?? 0m;
                var scores = new List<decimal>();
                if (resGroup != null && totalMarks > 0m)
                {
                    foreach (var s in resGroup.Items)
                    {
                        if (s.HasValue)
                        {
                            scores.Add((s.Value / totalMarks) * 10m);
                        }
                    }
                }

                decimal avg = scores.Any() ? scores.Average() : 0m;
                decimal highest = scores.Any() ? scores.Max() : 0m;
                decimal lowest = scores.Any() ? scores.Min() : 0m;

                var passMark = e.PassingMark ?? 0m;
                int pass = 0;
                if (resGroup != null)
                {
                    pass = resGroup.Items.Count(x => x.HasValue && x.Value >= passMark);
                }
                var totalRes = resGroup?.Items.Count ?? 0;
                var passRate = totalRes > 0 ? (decimal)pass * 100m / (decimal)totalRes : 0m;

                var distribution = BuildDistribution(scores);

                stats.Add(new ExamStatistic
                {
                    Id = e.ExamId.ToString(),
                    ExamName = e.Title,
                    Subject = e.Course?.Subject?.Name,
                    TotalParticipants = attemptGroup?.Count ?? 0,
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

