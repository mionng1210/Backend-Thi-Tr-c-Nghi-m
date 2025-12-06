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

            // Get all attempts with valid scores (same logic as GetExamsStats)
            var attempts = await _db.ExamAttempts
                .AsNoTracking()
                .Where(a => !a.HasDelete && a.Score.HasValue && a.MaxScore.HasValue && a.MaxScore.Value > 0)
                .Select(a => new { a.Score, a.MaxScore, a.ExamId })
                .ToListAsync();
            var scores10 = attempts.Select(a => (a.Score!.Value / a.MaxScore!.Value) * 10m).ToList();
            var avgScore = scores10.Any() ? scores10.Average() : 0m;

            // Calculate pass count - need to do in memory because of complex logic
            var attemptsWithExams = await _db.ExamAttempts
                .AsNoTracking()
                .Where(a => !a.HasDelete && a.Score.HasValue && a.MaxScore.HasValue && a.MaxScore.Value > 0)
                .Join(_db.Exams.AsNoTracking(), a => a.ExamId, e => e.ExamId, (a, e) => new { a.Score, a.MaxScore, e.PassingMark })
                .ToListAsync();
            
            var passCount = attemptsWithExams.Count(x => {
                if (!x.Score.HasValue || !x.MaxScore.HasValue || x.MaxScore.Value <= 0) return false;
                var normalizedScore = (x.Score.Value / x.MaxScore.Value) * 10m;
                return x.PassingMark.HasValue && (normalizedScore >= x.PassingMark.Value || x.Score.Value >= x.PassingMark.Value);
            });
            var totalAttempts = await _db.ExamAttempts
                .AsNoTracking()
                .Where(a => !a.HasDelete && a.Score.HasValue && a.MaxScore.HasValue && a.MaxScore.Value > 0)
                .CountAsync();
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
            // Build query - execute early to avoid SQL syntax issues
            var examsQuery = _db.Exams
                .AsNoTracking()
                .Where(e => !e.HasDelete);

            // Apply filters before executing
            if (dateFrom.HasValue)
            {
                var start = dateFrom.Value.Date;
                examsQuery = examsQuery.Where(e => e.StartAt.HasValue && e.StartAt.Value >= start);
            }
            if (dateTo.HasValue)
            {
                var end = dateTo.Value.Date.AddDays(1).AddTicks(-1);
                examsQuery = examsQuery.Where(e => e.EndAt.HasValue && e.EndAt.Value <= end);
            }
            if (!string.IsNullOrWhiteSpace(status))
            {
                examsQuery = examsQuery.Where(e => e.Status != null && e.Status.Equals(status, StringComparison.OrdinalIgnoreCase));
            }

            // Execute query first to get exam list
            var exams = await examsQuery
                .Select(e => new 
                { 
                    ExamId = e.ExamId, 
                    Title = e.Title, 
                    SubjectId = e.SubjectId, 
                    TotalMarks = e.TotalMarks, 
                    PassingMark = e.PassingMark, 
                    DurationMinutes = e.DurationMinutes, 
                    StartAt = e.StartAt 
                })
                .ToListAsync();
            
            // Get subject IDs from exams (in memory)
            var subjectIds = exams
                .Where(e => e.SubjectId.HasValue)
                .Select(e => e.SubjectId!.Value)
                .Distinct()
                .ToList();
            
            // Load all subjects and filter in memory to avoid SQL syntax issues
            Dictionary<int, string> subjectsById = new Dictionary<int, string>();
            if (subjectIds.Any())
            {
                var allSubjects = await _db.Subjects
                    .AsNoTracking()
                    .Select(s => new { s.SubjectId, s.Name })
                    .ToListAsync();
                
                subjectsById = allSubjects
                    .Where(s => subjectIds.Contains(s.SubjectId))
                    .ToDictionary(x => x.SubjectId, x => x.Name);
            }

            // Apply subject filter
            if (!string.IsNullOrWhiteSpace(subject))
            {
                exams = exams.Where(e => e.SubjectId.HasValue && subjectsById.TryGetValue(e.SubjectId!.Value, out var name) && string.Equals(name, subject, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            
            var examIds = exams.Select(e => e.ExamId).ToList();
            
            // Debug: Check total attempts first
            var totalAttemptsCheck = await _db.ExamAttempts
                .AsNoTracking()
                .Where(a => examIds.Contains(a.ExamId))
                .CountAsync();
            Console.WriteLine($"[DEBUG] Total attempts for {examIds.Count} exams: {totalAttemptsCheck}");
            
            // Get all attempts first to debug
            var allAttemptsRaw = await _db.ExamAttempts
                .AsNoTracking()
                .Where(a => examIds.Contains(a.ExamId))
                .Select(a => new { a.ExamId, a.Score, a.MaxScore, a.HasDelete, a.IsSubmitted })
                .ToListAsync();
            
            Console.WriteLine($"[DEBUG] Raw attempts: {allAttemptsRaw.Count}");
            Console.WriteLine($"[DEBUG] HasDelete=false: {allAttemptsRaw.Count(a => !a.HasDelete)}");
            Console.WriteLine($"[DEBUG] Has Score: {allAttemptsRaw.Count(a => a.Score.HasValue)}");
            Console.WriteLine($"[DEBUG] Has MaxScore>0: {allAttemptsRaw.Count(a => a.MaxScore.HasValue && a.MaxScore.Value > 0)}");
            Console.WriteLine($"[DEBUG] IsSubmitted=true: {allAttemptsRaw.Count(a => a.IsSubmitted)}");
            
            // Match the logic from GetOverall() - filter by HasDelete and valid scores
            var attemptsAll = allAttemptsRaw
                .Where(a => !a.HasDelete
                    && a.Score.HasValue 
                    && a.MaxScore.HasValue 
                    && a.MaxScore.Value > 0)
                .Select(a => new { a.ExamId, a.Score, a.MaxScore })
                .ToList();
            
            Console.WriteLine($"[DEBUG] Filtered attempts with valid scores: {attemptsAll.Count}");
            
            var attemptsByExam = attemptsAll
                .GroupBy(a => a.ExamId)
                .Select(g => new { ExamId = g.Key, Items = g.Select(a => new { a.Score, a.MaxScore }).ToList(), Count = g.Count() })
                .ToList();
            
            Console.WriteLine($"[DEBUG] Exams with attempts: {attemptsByExam.Count}");
            foreach (var examGroup in attemptsByExam.Take(5))
            {
                Console.WriteLine($"[DEBUG] Exam {examGroup.ExamId}: {examGroup.Count} attempts");
            }

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
                var totalRes = group?.Items.Count ?? 0;
                
                if (group != null && totalRes > 0)
                {
                    var passMark = e.PassingMark ?? 0m;
                    // Calculate pass based on normalized score (0-10 scale)
                    // But also check raw score against PassingMark if it's in raw score format
                    pass = group.Items.Count(x => 
                    {
                        if (!x.Score.HasValue || !x.MaxScore.HasValue || x.MaxScore.Value <= 0) return false;
                        
                        // Try normalized score first (0-10 scale)
                        var normalizedScore = (x.Score.Value / x.MaxScore.Value) * 10m;
                        if (normalizedScore >= passMark) return true;
                        
                        // Also check if raw score meets passing mark (in case PassingMark is in raw format)
                        if (x.Score.Value >= passMark) return true;
                        
                        return false;
                    });
                }
                
                var passRate = totalRes > 0 ? (decimal)pass * 100m / (decimal)totalRes : 0m;

                var distribution = BuildDistribution(scores);
                
                // Debug logging for first few exams
                if (stats.Count < 3)
                {
                    string? subjectName = null;
                    if (e.SubjectId.HasValue && subjectsById.TryGetValue(e.SubjectId.Value, out var subjectNameValue))
                    {
                        subjectName = subjectNameValue;
                    }
                    
                    Console.WriteLine($"[DEBUG] Exam {e.ExamId} ({e.Title}):");
                    Console.WriteLine($"  SubjectId: {e.SubjectId}, Subject: {subjectName ?? "null"}");
                    Console.WriteLine($"  Participants: {group?.Count ?? 0}, Scores count: {scores.Count}");
                    Console.WriteLine($"  Average: {avg}, Highest: {highest}, Lowest: {lowest}");
                    Console.WriteLine($"  PassRate: {passRate}%, Distribution buckets: {distribution.Count}");
                }

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
            
            Console.WriteLine($"[DEBUG] Total stats returned: {stats.Count}");
            Console.WriteLine($"[DEBUG] Stats with participants: {stats.Count(s => s.TotalParticipants > 0)}");
            Console.WriteLine($"[DEBUG] Stats with scores: {stats.Count(s => s.AverageScore > 0)}");
            Console.WriteLine($"[DEBUG] Stats with distribution: {stats.Count(s => s.ScoreDistribution.Any(d => d.Count > 0))}");

            return Ok(stats);
            }
            catch (Exception ex)
            {
                try
                {
                    var fallbackExams = await _db.Exams
                        .AsNoTracking()
                        .Where(e => !e.HasDelete)
                        .Select(e => new 
                        { 
                            ExamId = e.ExamId, 
                            Title = e.Title, 
                            SubjectId = e.SubjectId, 
                            TotalMarks = e.TotalMarks, 
                            PassingMark = e.PassingMark, 
                            DurationMinutes = e.DurationMinutes, 
                            StartAt = e.StartAt 
                        })
                        .ToListAsync();
                    
                    var subjectIds = fallbackExams
                        .Where(e => e.SubjectId.HasValue)
                        .Select(e => e.SubjectId!.Value)
                        .Distinct()
                        .ToList();
                    
                    // Load all subjects and filter in memory
                    Dictionary<int, string> subjectsById = new Dictionary<int, string>();
                    if (subjectIds.Any())
                    {
                        var allSubjects = await _db.Subjects
                            .AsNoTracking()
                            .Select(s => new { s.SubjectId, s.Name })
                            .ToListAsync();
                        
                        subjectsById = allSubjects
                            .Where(s => subjectIds.Contains(s.SubjectId))
                            .ToDictionary(x => x.SubjectId, x => x.Name);
                    }

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
                    return StatusCode(500, new { message = ex.ToString() });
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
