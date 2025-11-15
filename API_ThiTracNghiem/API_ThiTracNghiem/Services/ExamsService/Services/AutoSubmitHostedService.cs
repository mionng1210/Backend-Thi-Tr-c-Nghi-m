using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ExamsService.Data;
using ExamsService.Models;
using ExamsService.DTOs;

namespace ExamsService.Services
{
    public class AutoSubmitHostedService : BackgroundService
    {
        private readonly ILogger<AutoSubmitHostedService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _config;

        public AutoSubmitHostedService(ILogger<AutoSubmitHostedService> logger, IServiceProvider serviceProvider, IConfiguration config)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var intervalSeconds = _config.GetSection("AutoSubmit").GetValue<int>("PollSeconds", 30);
            _logger.LogInformation("AutoSubmitHostedService started. Interval: {Interval}s", intervalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ExamsDbContext>();
                    var cache = scope.ServiceProvider.GetRequiredService<IExamProgressCache>();

                    await ProcessExpiredAttemptsAsync(db, cache, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in AutoSubmitHostedService loop");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("AutoSubmitHostedService stopped.");
        }

        private async Task ProcessExpiredAttemptsAsync(ExamsDbContext db, IExamProgressCache cache, CancellationToken ct)
        {
            var now = DateTime.UtcNow;

            var expiredAttempts = await db.ExamAttempts
                .Include(ea => ea.Exam)
                .Where(ea => ea.Status == "InProgress" && ea.EndTime != null && ea.EndTime <= now)
                .OrderBy(ea => ea.EndTime)
                .Take(100)
                .ToListAsync(ct);

            if (expiredAttempts.Count == 0)
            {
                return;
            }

            _logger.LogInformation("Auto-submitting {Count} expired attempts", expiredAttempts.Count);

            foreach (var examAttempt in expiredAttempts)
            {
                try
                {
                    var exam = examAttempt.Exam;
                    if (exam == null)
                    {
                        _logger.LogWarning("Exam not found for attempt {AttemptId}", examAttempt.ExamAttemptId);
                        // Still lock attempt to avoid endless loop
                        examAttempt.Status = "Completed";
                        examAttempt.IsSubmitted = true;
                        examAttempt.SubmittedAt = now;
                        examAttempt.TimeSpentMinutes = (int)(now - examAttempt.StartTime).TotalMinutes;
                        await db.SaveChangesAsync(ct);
                        continue;
                    }

                    var cached = await cache.GetAllAsync(examAttempt.ExamAttemptId);
                    var answersToGrade = cached.Values.Select(v => new SubmittedAnswerDto
                    {
                        QuestionId = v.QuestionId,
                        SelectedOptionIds = v.SelectedOptionIds ?? new List<int>(),
                        TextAnswer = v.TextAnswer
                    }).ToList();

                    decimal totalScore = 0;

                    foreach (var submittedAnswer in answersToGrade)
                    {
                        var question = await db.Questions
                            .Include(q => q.AnswerOptions)
                            .FirstOrDefaultAsync(q => q.QuestionId == submittedAnswer.QuestionId, ct);

                        if (question == null) continue;

                        var examQuestion = await db.ExamQuestions
                            .FirstOrDefaultAsync(eq => eq.ExamId == examAttempt.ExamId && eq.QuestionId == submittedAnswer.QuestionId, ct);

                        var questionMarks = examQuestion?.Marks ?? 0;
                        var earnedMarks = 0m;
                        var isCorrect = false;

                        var submittedAnswerEntity = new SubmittedAnswer
                        {
                            ExamAttemptId = examAttempt.ExamAttemptId,
                            QuestionId = submittedAnswer.QuestionId,
                            TextAnswer = submittedAnswer.TextAnswer,
                            EarnedMarks = 0,
                            IsCorrect = false,
                            CreatedAt = now
                        };

                        db.SubmittedAnswers.Add(submittedAnswerEntity);
                        await db.SaveChangesAsync(ct);

                        if (submittedAnswer.SelectedOptionIds?.Any() == true)
                        {
                            var correctOptions = question.AnswerOptions.Where(ao => ao.IsCorrect).ToList();
                            var selectedOptions = question.AnswerOptions.Where(ao => submittedAnswer.SelectedOptionIds.Contains(ao.OptionId)).ToList();

                            foreach (var optionId in submittedAnswer.SelectedOptionIds)
                            {
                                var submittedOption = new SubmittedAnswerOption
                                {
                                    SubmittedAnswerId = submittedAnswerEntity.SubmittedAnswerId,
                                    AnswerOptionId = optionId,
                                    CreatedAt = now
                                };
                                db.SubmittedAnswerOptions.Add(submittedOption);
                            }

                            var correctOptionIds = correctOptions.Select(co => co.OptionId).ToHashSet();
                            var selectedOptionIds = submittedAnswer.SelectedOptionIds.ToHashSet();

                            isCorrect = correctOptionIds.SetEquals(selectedOptionIds);
                            earnedMarks = isCorrect ? questionMarks : 0;
                        }
                        else if (!string.IsNullOrEmpty(submittedAnswer.TextAnswer))
                        {
                            isCorrect = true;
                            earnedMarks = questionMarks;
                        }

                        submittedAnswerEntity.IsCorrect = isCorrect;
                        submittedAnswerEntity.EarnedMarks = earnedMarks;
                        totalScore += earnedMarks;
                    }

                    examAttempt.Score = totalScore;
                    examAttempt.MaxScore = exam.TotalMarks ?? 0;
                    examAttempt.Status = "Completed";
                    examAttempt.IsSubmitted = true;
                    examAttempt.SubmittedAt = now;
                    examAttempt.TimeSpentMinutes = (int)(now - examAttempt.StartTime).TotalMinutes;

                    await db.SaveChangesAsync(ct);

                    await cache.DeleteAsync(examAttempt.ExamAttemptId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to auto-submit attempt {AttemptId}", examAttempt.ExamAttemptId);
                }
            }
        }
    }
}