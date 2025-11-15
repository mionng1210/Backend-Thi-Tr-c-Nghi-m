using API_ThiTracNghiem.Contracts;
using API_ThiTracNghiem.Models;

namespace API_ThiTracNghiem.Services
{
    public class ExamsService : IExamsService
    {
        private readonly IExamsRepository _examsRepository;

        public ExamsService(IExamsRepository examsRepository)
        {
            _examsRepository = examsRepository;
        }

        public async Task<PagedResponse<ExamListItemDto>> GetExamsAsync(int pageIndex, int pageSize, int? courseId = null, int? teacherId = null, int? subjectId = null)
        {
            return await _examsRepository.GetExamsAsync(pageIndex, pageSize, courseId, teacherId, subjectId);
        }

        public async Task<ExamDetailDto?> GetExamByIdAsync(int id)
        {
            return await _examsRepository.GetByIdAsync(id);
        }

        public async Task<ExamDetailDto> CreateExamAsync(CreateExamRequest request, int createdBy)
        {
            // Validate course exists
            if (request.CourseId.HasValue && !await _examsRepository.CourseExistsAsync(request.CourseId.Value))
            {
                throw new ArgumentException("Course không tồn tại");
            }

            var exam = new Exam
            {
                Title = request.Title,
                Description = request.Description,
                CourseId = request.CourseId,
                DurationMinutes = request.DurationMinutes,
                TotalQuestions = request.TotalQuestions,
                TotalMarks = request.TotalMarks,
                PassingMark = request.PassingMark,
                ExamType = request.ExamType,
                StartAt = request.StartAt,
                EndAt = request.EndAt,
                RandomizeQuestions = request.RandomizeQuestions,
                AllowMultipleAttempts = request.AllowMultipleAttempts,
                Status = request.Status ?? "Draft",
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow,
                HasDelete = false
            };

            var createdExam = await _examsRepository.CreateAsync(exam);

            // Add questions to exam if provided
            if (request.Questions != null && request.Questions.Any())
            {
                foreach (var questionRequest in request.Questions)
                {
                    // Validate question exists
                    if (!await _examsRepository.QuestionExistsAsync(questionRequest.QuestionId))
                    {
                        throw new ArgumentException($"Question với ID {questionRequest.QuestionId} không tồn tại");
                    }

                    var examQuestion = new ExamQuestion
                    {
                        ExamId = createdExam.ExamId,
                        QuestionId = questionRequest.QuestionId,
                        SequenceIndex = questionRequest.SequenceIndex,
                        Marks = questionRequest.Marks,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _examsRepository.AddQuestionToExamAsync(examQuestion);
                }
            }

            // Return the created exam with basic details (avoiding complex query for now)
            return new ExamDetailDto
            {
                Id = createdExam.ExamId,
                Title = createdExam.Title,
                Description = createdExam.Description,
                CourseId = createdExam.CourseId,
                DurationMinutes = createdExam.DurationMinutes,
                TotalQuestions = createdExam.TotalQuestions,
                TotalMarks = createdExam.TotalMarks,
                PassingMark = createdExam.PassingMark,
                ExamType = createdExam.ExamType,
                StartAt = createdExam.StartAt,
                EndAt = createdExam.EndAt,
                RandomizeQuestions = createdExam.RandomizeQuestions,
                AllowMultipleAttempts = createdExam.AllowMultipleAttempts,
                Status = createdExam.Status,
                CreatedAt = createdExam.CreatedAt,
                UpdatedAt = createdExam.UpdatedAt,
                Questions = new List<ExamQuestionDto>()
            };
        }

        public async Task<ExamDetailDto?> UpdateExamAsync(int id, UpdateExamRequest request, int userId)
        {
            // Check if exam exists
            if (!await _examsRepository.ExamExistsAsync(id))
            {
                return null;
            }

            // Check if user is the teacher who owns this exam
            if (!await _examsRepository.IsTeacherOwnerOfExamAsync(id, userId))
            {
                throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa bài thi này");
            }

            // Validate course exists if provided
            if (request.CourseId.HasValue && !await _examsRepository.CourseExistsAsync(request.CourseId.Value))
            {
                throw new ArgumentException("Course không tồn tại");
            }

            var exam = new Exam
            {
                Title = request.Title,
                Description = request.Description,
                CourseId = request.CourseId ?? 0, // This will be handled in repository
                DurationMinutes = request.DurationMinutes ?? 0,
                TotalQuestions = request.TotalQuestions ?? 0,
                TotalMarks = request.TotalMarks ?? 0,
                PassingMark = request.PassingMark ?? 0,
                ExamType = request.ExamType,
                StartAt = request.StartAt,
                EndAt = request.EndAt,
                RandomizeQuestions = request.RandomizeQuestions ?? false,
                AllowMultipleAttempts = request.AllowMultipleAttempts ?? false,
                Status = request.Status ?? "Draft"
            };

            var updatedExam = await _examsRepository.UpdateAsync(id, exam);
            if (updatedExam == null)
            {
                return null;
            }

            return await _examsRepository.GetByIdAsync(id);
        }

        public async Task<bool> DeleteExamAsync(int id, int userId)
        {
            // Check if exam exists
            if (!await _examsRepository.ExamExistsAsync(id))
            {
                return false;
            }

            // Check if user is the teacher who owns this exam
            if (!await _examsRepository.IsTeacherOwnerOfExamAsync(id, userId))
            {
                throw new UnauthorizedAccessException("Bạn không có quyền xóa bài thi này");
            }

            return await _examsRepository.DeleteAsync(id);
        }

        public async Task<bool> AddQuestionToExamAsync(int examId, CreateExamQuestionRequest request, int userId)
        {
            // Check if exam exists
            if (!await _examsRepository.ExamExistsAsync(examId))
            {
                throw new ArgumentException("Bài thi không tồn tại");
            }

            // Check if user is the teacher who owns this exam
            if (!await _examsRepository.IsTeacherOwnerOfExamAsync(examId, userId))
            {
                throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa bài thi này");
            }

            // Check if question exists
            if (!await _examsRepository.QuestionExistsAsync(request.QuestionId))
            {
                throw new ArgumentException("Câu hỏi không tồn tại");
            }

            var examQuestion = new ExamQuestion
            {
                ExamId = examId,
                QuestionId = request.QuestionId,
                SequenceIndex = request.SequenceIndex,
                Marks = request.Marks,
                CreatedAt = DateTime.UtcNow
            };

            await _examsRepository.AddQuestionToExamAsync(examQuestion);
            return true;
        }

        public async Task<bool> RemoveQuestionFromExamAsync(int examId, int questionId, int userId)
        {
            // Check if exam exists
            if (!await _examsRepository.ExamExistsAsync(examId))
            {
                throw new ArgumentException("Bài thi không tồn tại");
            }

            // Check if user is the teacher who owns this exam
            if (!await _examsRepository.IsTeacherOwnerOfExamAsync(examId, userId))
            {
                throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa bài thi này");
            }

            return await _examsRepository.RemoveQuestionFromExamAsync(examId, questionId);
        }

        public async Task<MixQuestionsResponse> MixQuestionsAsync(int examId, MixQuestionsRequest request, int userId)
        {
            // Check if exam exists
            if (!await _examsRepository.ExamExistsAsync(examId))
            {
                throw new ArgumentException("Bài thi không tồn tại");
            }

            // Check if user is the teacher who owns this exam
            if (!await _examsRepository.IsTeacherOwnerOfExamAsync(examId, userId))
            {
                throw new UnauthorizedAccessException("Bạn không có quyền trộn câu hỏi cho bài thi này");
            }

            // Validate total questions match difficulty distribution
            var totalRequestedQuestions = request.DifficultyDistribution.Sum(d => d.QuestionCount);
            if (totalRequestedQuestions != request.TotalQuestions)
            {
                throw new ArgumentException("Tổng số câu hỏi theo độ khó không khớp với tổng số câu hỏi yêu cầu");
            }

            var variants = new List<ExamVariant>();

            // Generate variants
            for (int i = 0; i < request.NumberOfVariants; i++)
            {
                var variantCode = $"V{i + 1:D2}";
                var variantQuestions = new List<ExamQuestionDto>();
                decimal totalMarks = 0;
                int sequenceIndex = 1;

                // Get questions for each difficulty level
                foreach (var difficultyDist in request.DifficultyDistribution)
                {
                    var questionsForDifficulty = await _examsRepository.GetRandomQuestionsByDifficultyAsync(
                        examId, difficultyDist.Difficulty, difficultyDist.QuestionCount);

                    foreach (var question in questionsForDifficulty)
                    {
                        // Get answer options for this question
                        var answerOptions = await _examsRepository.GetAnswerOptionsForQuestionAsync(question.QuestionId);

                        var examQuestion = new ExamQuestionDto
                        {
                            QuestionId = question.QuestionId,
                            Content = question.Content,
                            QuestionType = question.QuestionType,
                            Difficulty = question.Difficulty,
                            Marks = difficultyDist.MarksPerQuestion,
                            SequenceIndex = sequenceIndex++,
                            Options = answerOptions.Select(o => new AnswerOptionDto
                            {
                                OptionId = o.OptionId,
                                Content = o.Content,
                                IsCorrect = o.IsCorrect,
                                SequenceIndex = o.OrderIndex
                            }).ToList()
                        };

                        variantQuestions.Add(examQuestion);
                        totalMarks += difficultyDist.MarksPerQuestion;
                    }
                }

                // Shuffle questions if randomization is enabled
                var exam = await _examsRepository.GetByIdAsync(examId);
                if (exam?.RandomizeQuestions == true)
                {
                    variantQuestions = variantQuestions.OrderBy(x => Guid.NewGuid()).ToList();
                    // Update sequence indices after shuffling
                    for (int j = 0; j < variantQuestions.Count; j++)
                    {
                        variantQuestions[j].SequenceIndex = j + 1;
                    }
                }

                variants.Add(new ExamVariant
                {
                    VariantCode = variantCode,
                    Questions = variantQuestions,
                    TotalMarks = totalMarks
                });
            }

            // Save variants to database (you might want to add a new table for exam variants)
            // For now, we'll return the generated variants

            return new MixQuestionsResponse
            {
                ExamId = examId,
                Variants = variants,
                Message = $"Đã tạo thành công {request.NumberOfVariants} mã đề với {request.TotalQuestions} câu hỏi mỗi đề"
            };
        }

        public async Task<StartExamResponse> StartExamAsync(int examId, StartExamRequest request, int userId)
        {
            // Check if exam exists
            var exam = await _examsRepository.GetByIdAsync(examId);
            if (exam == null)
            {
                throw new ArgumentException("Bài thi không tồn tại");
            }

            // Check if exam is active and within time range
            var now = DateTime.UtcNow;
            if (exam.StartAt.HasValue && now < exam.StartAt.Value)
            {
                throw new InvalidOperationException("Bài thi chưa bắt đầu");
            }

            if (exam.EndAt.HasValue && now > exam.EndAt.Value)
            {
                throw new InvalidOperationException("Bài thi đã kết thúc");
            }

            if (exam.Status != "Active" && exam.Status != "Published")
            {
                throw new InvalidOperationException("Bài thi không ở trạng thái có thể làm bài");
            }

            // Check if user already has an active attempt
            var existingAttempt = await _examsRepository.GetActiveExamAttemptAsync(examId, userId);
            if (existingAttempt != null)
            {
                throw new InvalidOperationException("Bạn đã có một lần thi đang diễn ra");
            }

            // Check if multiple attempts are allowed
            if (!exam.AllowMultipleAttempts)
            {
                var previousAttempts = await _examsRepository.GetUserExamAttemptsAsync(examId, userId);
                if (previousAttempts.Any())
                {
                    throw new InvalidOperationException("Bài thi này chỉ cho phép làm một lần");
                }
            }

            // Create new exam attempt
            var examAttempt = new ExamAttempt
            {
                ExamId = examId,
                UserId = userId,
                StartedAt = now,
                Status = "InProgress",
                CreatedAt = now
            };

            var createdAttempt = await _examsRepository.CreateExamAttemptAsync(examAttempt);

            // Get exam questions (by variant if specified, otherwise get default questions)
            List<ExamQuestion> examQuestions;
            if (!string.IsNullOrEmpty(request.VariantCode))
            {
                examQuestions = await _examsRepository.GetExamQuestionsByVariantAsync(examId, request.VariantCode);
            }
            else
            {
                // Get default exam questions
                examQuestions = await _examsRepository.GetExamQuestionsAsync(examId);
            }
            
            var questions = new List<ExamQuestionDto>();
            foreach (var eq in examQuestions.OrderBy(eq => eq.SequenceIndex))
            {
                // Get answer options for this question
                var answerOptions = await _examsRepository.GetAnswerOptionsForQuestionAsync(eq.QuestionId);

                var questionDto = new ExamQuestionDto
                {
                    QuestionId = eq.QuestionId,
                    Content = eq.Question?.Content ?? "",
                    QuestionType = eq.Question?.QuestionType ?? "",
                    Difficulty = eq.Question?.Difficulty ?? "",
                    Marks = eq.Marks ?? 0,
                    SequenceIndex = eq.SequenceIndex ?? 0,
                    Options = answerOptions.Select(o => new AnswerOptionDto
                    {
                        OptionId = o.OptionId,
                        Content = o.Content,
                        IsCorrect = false, // Don't expose correct answers to candidates
                        SequenceIndex = o.OrderIndex
                    }).ToList()
                };

                questions.Add(questionDto);
            }

            // Randomize if enabled and not using variant
            if (string.IsNullOrEmpty(request.VariantCode) && exam.RandomizeQuestions)
            {
                questions = questions.OrderBy(x => Guid.NewGuid()).ToList();
                // Update sequence indices after shuffling
                for (int i = 0; i < questions.Count; i++)
                {
                    questions[i].SequenceIndex = i + 1;
                }
            }

            // Calculate end time
            DateTime? endTime = null;
            if (exam.DurationMinutes.HasValue)
            {
                endTime = now.AddMinutes(exam.DurationMinutes.Value);
                
                // Don't exceed exam end time if specified
                if (exam.EndAt.HasValue && endTime > exam.EndAt.Value)
                {
                    endTime = exam.EndAt.Value;
                }
            }

            return new StartExamResponse
            {
                ExamAttemptId = createdAttempt.AttemptId,
                ExamId = examId,
                ExamTitle = exam.Title,
                VariantCode = request.VariantCode,
                StartTime = now,
                EndTime = endTime,
                DurationMinutes = exam.DurationMinutes ?? 0,
                Questions = questions,
                TotalMarks = exam.TotalMarks ?? 0,
                PassingMark = exam.PassingMark ?? 0,
                Instructions = exam.Description ?? "Hãy đọc kỹ câu hỏi và chọn đáp án đúng nhất."
            };
        }
    }
}