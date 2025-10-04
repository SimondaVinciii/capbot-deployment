using AutoMapper;
using App.BLL.Interfaces;
using App.Commons.ResponseModel;
using App.DAL.UnitOfWork;
using App.DAL.Queries;
using App.Entities.DTOs.Review;
using App.Entities.Entities.App;
using App.Entities.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace App.BLL.Implementations;

public class SubmissionReviewService : ISubmissionReviewService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public SubmissionReviewService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<BaseResponseModel<ReviewResponseDTO>> CreateSubmissionReviewAsync(CreateReviewDTO createDTO, int reviewerId)
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync();

            var assignmentRepo = _unitOfWork.GetRepo<ReviewerAssignment>();
            var reviewRepo = _unitOfWork.GetRepo<Review>();

            // Kiểm tra assignment tồn tại và thuộc về reviewer
            var assignment = await assignmentRepo.GetSingleAsync(new QueryOptions<ReviewerAssignment>
            {
                Predicate = x => x.Id == createDTO.AssignmentId && x.ReviewerId == reviewerId,
                IncludeProperties = new List<System.Linq.Expressions.Expression<Func<ReviewerAssignment, object>>>
                {
                    x => x.Submission
                }
            });

            if (assignment == null)
            {
                await _unitOfWork.RollBackAsync();
                return new BaseResponseModel<ReviewResponseDTO>
                {
                    IsSuccess = false,
                    StatusCode = StatusCodes.Status404NotFound,
                    Message = "Không tìm thấy assignment hoặc bạn không có quyền review"
                };
            }

            // Tạo review
            var review = new Review
            {
                AssignmentId = createDTO.AssignmentId,
                OverallComment = createDTO.OverallComment,
                Recommendation = createDTO.Recommendation,
                Status = ReviewStatus.Draft,
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow,
                IsActive = true
            };

            // Tính điểm tổng từ các tiêu chí
            if (createDTO.CriteriaScores?.Any() == true)
            {
                decimal totalWeightedScore = 0;
                decimal totalWeight = 0;

                var criteriaRepo = _unitOfWork.GetRepo<EvaluationCriteria>();
                var criteriaIds = createDTO.CriteriaScores.Select(x => x.CriteriaId).ToList();
                var criteria = await criteriaRepo.GetAllAsync(new QueryOptions<EvaluationCriteria>
                {
                    Predicate = x => criteriaIds.Contains(x.Id) && x.IsActive,
                    Tracked = false
                });

                foreach (var scoreDTO in createDTO.CriteriaScores)
                {
                    var criteriaItem = criteria.FirstOrDefault(x => x.Id == scoreDTO.CriteriaId);
                    if (criteriaItem != null && criteriaItem.MaxScore > 0)
                    {
                        var normalizedScore = (scoreDTO.Score / criteriaItem.MaxScore) * 10;
                        totalWeightedScore += normalizedScore * criteriaItem.Weight;
                        totalWeight += criteriaItem.Weight;
                    }
                }

                review.OverallScore = totalWeight > 0 ? totalWeightedScore / totalWeight : 0;
            }

            await reviewRepo.CreateAsync(review);
            var saveResult = await _unitOfWork.SaveAsync();

            if (!saveResult.IsSuccess)
            {
                await _unitOfWork.RollBackAsync();
                return new BaseResponseModel<ReviewResponseDTO>
                {
                    IsSuccess = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = saveResult.Message
                };
            }

            // Tạo criteria scores nếu có
            if (createDTO.CriteriaScores?.Any() == true)
            {
                var scoreRepo = _unitOfWork.GetRepo<ReviewCriteriaScore>();
                var scores = createDTO.CriteriaScores.Select(scoreDTO => new ReviewCriteriaScore
                {
                    ReviewId = review.Id,
                    CriteriaId = scoreDTO.CriteriaId,
                    Score = scoreDTO.Score,
                    Comment = scoreDTO.Comment,
                    CreatedAt = DateTime.UtcNow,
                    LastModifiedAt = DateTime.UtcNow,
                    IsActive = true
                }).ToList();

                await scoreRepo.CreateAllAsync(scores);
                await _unitOfWork.SaveAsync();
            }

            await _unitOfWork.CommitTransactionAsync();

            // Kiểm tra và xử lý logic 2 reviewer
            await ProcessSubmissionReviewLogicAsync(assignment.SubmissionId);

            var responseDTO = _mapper.Map<ReviewResponseDTO>(review);
            return new BaseResponseModel<ReviewResponseDTO>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status201Created,
                Message = "Tạo review thành công",
                Data = responseDTO
            };
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollBackAsync();
            return new BaseResponseModel<ReviewResponseDTO>
            {
                IsSuccess = false,
                StatusCode = StatusCodes.Status500InternalServerError,
                Message = $"Lỗi hệ thống: {ex.Message}"
            };
        }
    }

    private async Task ProcessSubmissionReviewLogicAsync(int submissionId)
    {
        var submissionRepo = _unitOfWork.GetRepo<Submission>();
        var assignmentRepo = _unitOfWork.GetRepo<ReviewerAssignment>();

        // Lấy tất cả assignments và reviews của submission
        var assignments = await assignmentRepo.GetAllAsync(new QueryOptions<ReviewerAssignment>
        {
            Predicate = x => x.SubmissionId == submissionId,
            IncludeProperties = new List<System.Linq.Expressions.Expression<Func<ReviewerAssignment, object>>>
            {
                x => x.Reviews
            }
        });

        var assignmentList = assignments.ToList();
        var submittedReviews = assignmentList
            .SelectMany(a => a.Reviews ?? new List<Review>())
            .Where(r => r.Status == ReviewStatus.Submitted && r.IsActive)
            .ToList();

        // Kiểm tra nếu có ít nhất 2 review đã nộp
        if (submittedReviews.Count >= 2)
        {
            var submission = await submissionRepo.GetSingleAsync(new QueryOptions<Submission>
            {
                Predicate = x => x.Id == submissionId
            });

            if (submission != null)
            {
                var approveCount = submittedReviews.Count(r => r.Recommendation == ReviewRecommendations.Approve);
                var rejectCount = submittedReviews.Count(r => r.Recommendation == ReviewRecommendations.Reject);
                var revisionCount = submittedReviews.Count(r =>
                    r.Recommendation == ReviewRecommendations.MinorRevision ||
                    r.Recommendation == ReviewRecommendations.MajorRevision);

                // Priority rules:
                // - Any revision recommendation -> RevisionRequired
                // - All submitted reviews are Approve (and at least 2) -> Approved
                // - All submitted reviews are Reject (and at least 2) -> Rejected
                // - Mixed Approve + Reject -> EscalatedToModerator (conflict)

                if (revisionCount > 0)
                {
                    submission.Status = SubmissionStatus.RevisionRequired;
                }
                else if (approveCount >= 2)
                {
                    submission.Status = SubmissionStatus.Approved;
                }
                else if (rejectCount >= 2)
                {
                    submission.Status = SubmissionStatus.Rejected;
                }
                else if (approveCount > 0 && rejectCount > 0)
                {
                    submission.Status = SubmissionStatus.EscalatedToModerator;
                }

                await submissionRepo.UpdateAsync(submission);
                await _unitOfWork.SaveAsync();
            }
        }
    }

    public async Task<BaseResponseModel<SubmissionReviewSummaryDTO>> GetSubmissionReviewSummaryAsync(int submissionId)
    {
        try
        {
            var submissionRepo = _unitOfWork.GetRepo<Submission>();

            var submission = await submissionRepo.GetSingleAsync(new QueryOptions<Submission>
            {
                Predicate = x => x.Id == submissionId,
                IncludeProperties = new List<System.Linq.Expressions.Expression<Func<Submission, object>>>
                {
                    // load the primary Topic navigation (use Topic.EN_Title instead of TopicVersion)
                    x => x.Topic,
                    x => x.SubmittedByUser,
                    x => x.ReviewerAssignments
                },
                AdvancedIncludes = new List<App.DAL.Queries.Interfaces.IIncludeSpecification<Submission>>
                {
                    // ensure nested reviewer assignment reviews/reviewer are loaded
                    new App.DAL.Queries.Implementations.ComplexIncludeSpecification<Submission>("ReviewerAssignments.Reviews"),
                    new App.DAL.Queries.Implementations.ComplexIncludeSpecification<Submission>("ReviewerAssignments.Reviewer")
                },
                Tracked = false
            });

            if (submission == null)
            {
                return new BaseResponseModel<SubmissionReviewSummaryDTO>
                {
                    IsSuccess = false,
                    StatusCode = StatusCodes.Status404NotFound,
                    Message = "Không tìm thấy submission"
                };
            }

            var summary = new SubmissionReviewSummaryDTO
            {
                SubmissionId = submission.Id,
                // Use Submission.Topic as the canonical title source
                TopicTitle = submission.Topic?.EN_Title ?? "N/A",
                StudentName = submission.SubmittedByUser?.UserName ?? "N/A",
                SubmissionStatus = submission.Status,
                RequiredReviewerCount = 2,
                CompletedReviewCount = submission.ReviewerAssignments
                    .SelectMany(ra => ra.Reviews ?? new List<Review>())
                    .Count(r => r.Status == ReviewStatus.Submitted && r.IsActive)
            };

            // Lấy thông tin reviews
            var reviewRepo = _unitOfWork.GetRepo<Review>();
            var reviews = await reviewRepo.GetAllAsync(new QueryOptions<Review>
            {
                Predicate = x => submission.ReviewerAssignments.Select(ra => ra.Id).Contains(x.AssignmentId) && x.IsActive,
                IncludeProperties = new List<System.Linq.Expressions.Expression<Func<Review, object>>>
                {
                    x => x.Assignment
                },
                AdvancedIncludes = new List<App.DAL.Queries.Interfaces.IIncludeSpecification<Review>>
                {
                    new App.DAL.Queries.Implementations.ComplexIncludeSpecification<Review>("Assignment.Reviewer")
                },
                Tracked = false
            });

            summary.Reviews = reviews.Select(r => new ReviewSummaryDTO
            {
                ReviewId = r.Id,
                ReviewerId = r.Assignment.ReviewerId,
                ReviewerName = r.Assignment.Reviewer?.UserName ?? "N/A",
                Status = r.Status,
                Recommendation = r.Recommendation,
                OverallScore = r.OverallScore,
                SubmittedAt = r.SubmittedAt,
                RevisionDeadline = r.Assignment.Deadline
            }).ToList();

            // Tính điểm trung bình
            var submittedReviews = summary.Reviews.Where(r => r.Status == ReviewStatus.Submitted).ToList();
            if (submittedReviews.Any())
            {
                var scores = submittedReviews.Where(r => r.OverallScore.HasValue).Select(r => r.OverallScore!.Value).ToList();
                if (scores.Any())
                {
                    summary.FinalScore = scores.Average();
                }
            }

            // Kiểm tra xung đột
            if (submittedReviews.Count >= 2)
            {
                var recommendations = submittedReviews.Select(r => r.Recommendation).ToList();
                var approveCount = recommendations.Count(r => r == ReviewRecommendations.Approve);
                var rejectCount = recommendations.Count(r => r == ReviewRecommendations.Reject);

                summary.IsConflicted = (approveCount > 0 && rejectCount > 0);
            }

            // Kiểm tra quá hạn
            summary.IsOverdue = summary.Reviews.Any(r =>
                r.RevisionDeadline.HasValue &&
                r.RevisionDeadline < DateTime.UtcNow &&
                submission.Status == SubmissionStatus.RevisionRequired);

            return new BaseResponseModel<SubmissionReviewSummaryDTO>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Data = summary
            };
        }
        catch (Exception ex)
        {
            return new BaseResponseModel<SubmissionReviewSummaryDTO>
            {
                IsSuccess = false,
                StatusCode = StatusCodes.Status500InternalServerError,
                Message = $"Lỗi hệ thống: {ex.Message}"
            };
        }
    }

    public async Task<BaseResponseModel<List<SubmissionReviewSummaryDTO>>> GetConflictedSubmissionsAsync()
    {
        try
        {
            var submissionRepo = _unitOfWork.GetRepo<Submission>();

            var submissions = await submissionRepo.GetAllAsync(new QueryOptions<Submission>
            {
                // We consider submissions escalated to moderator (EscalatedToModerator) as conflicted
                Predicate = x => x.Status == SubmissionStatus.EscalatedToModerator,
                Tracked = false
            });

            var summaries = new List<SubmissionReviewSummaryDTO>();

            foreach (var submission in submissions)
            {
                var summary = await GetSubmissionReviewSummaryAsync(submission.Id);
                // Consider any submission with status EscalatedToModerator as conflicted
                if (summary.IsSuccess && summary.Data != null)
                {
                    summaries.Add(summary.Data);
                }
            }

            return new BaseResponseModel<List<SubmissionReviewSummaryDTO>>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Data = summaries
            };
        }
        catch (Exception ex)
        {
            return new BaseResponseModel<List<SubmissionReviewSummaryDTO>>
            {
                IsSuccess = false,
                StatusCode = StatusCodes.Status500InternalServerError,
                Message = $"Lỗi hệ thống: {ex.Message}"
            };
        }
    }

    public async Task<BaseResponseModel<SubmissionReviewSummaryDTO>> ModeratorFinalReviewAsync(ModeratorFinalReviewDTO moderatorDTO, int moderatorId)
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync();

            var submissionRepo = _unitOfWork.GetRepo<Submission>();

            var submission = await submissionRepo.GetSingleAsync(new QueryOptions<Submission>
            {
                Predicate = x => x.Id == moderatorDTO.SubmissionId
            });

            if (submission == null)
            {
                await _unitOfWork.RollBackAsync();
                return new BaseResponseModel<SubmissionReviewSummaryDTO>
                {
                    IsSuccess = false,
                    StatusCode = StatusCodes.Status404NotFound,
                    Message = "Không tìm thấy submission"
                };
            }

            // Cập nhật submission status dựa trên quyết định của moderator
            switch (moderatorDTO.FinalRecommendation)
            {
                case ReviewRecommendations.Approve:
                    submission.Status = SubmissionStatus.Approved;
                    break;
                case ReviewRecommendations.MinorRevision:
                case ReviewRecommendations.MajorRevision:
                    submission.Status = SubmissionStatus.RevisionRequired;
                    break;
                case ReviewRecommendations.Reject:
                    submission.Status = SubmissionStatus.Rejected; // Coi như failed
                    break;
            }

            await submissionRepo.UpdateAsync(submission);
            var result = await _unitOfWork.SaveAsync();

            if (!result.IsSuccess)
            {
                await _unitOfWork.RollBackAsync();
                return new BaseResponseModel<SubmissionReviewSummaryDTO>
                {
                    IsSuccess = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = result.Message
                };
            }

            await _unitOfWork.CommitTransactionAsync();

            var summaryResult = await GetSubmissionReviewSummaryAsync(moderatorDTO.SubmissionId);
            return new BaseResponseModel<SubmissionReviewSummaryDTO>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Moderator review thành công",
                Data = summaryResult.Data
            };
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollBackAsync();
            return new BaseResponseModel<SubmissionReviewSummaryDTO>
            {
                IsSuccess = false,
                StatusCode = StatusCodes.Status500InternalServerError,
                Message = $"Lỗi hệ thống: {ex.Message}"
            };
        }
    }

    public async Task<BaseResponseModel<SubmissionReviewSummaryDTO>> SetRevisionDeadlineAsync(int reviewId, DateTime deadline)
    {
        try
        {
            var reviewRepo = _unitOfWork.GetRepo<Review>();

            var review = await reviewRepo.GetSingleAsync(new QueryOptions<Review>
            {
                Predicate = x => x.Id == reviewId && x.IsActive,
                IncludeProperties = new List<System.Linq.Expressions.Expression<Func<Review, object>>>
                {
                    x => x.Assignment
                }
            });

            if (review == null)
            {
                return new BaseResponseModel<SubmissionReviewSummaryDTO>
                {
                    IsSuccess = false,
                    StatusCode = StatusCodes.Status404NotFound,
                    Message = "Không tìm thấy review"
                };
            }

            // Lưu deadline trong Assignment
            review.Assignment.Deadline = deadline;

            var assignmentRepo = _unitOfWork.GetRepo<ReviewerAssignment>();
            await assignmentRepo.UpdateAsync(review.Assignment);
            var result = await _unitOfWork.SaveAsync();

            if (!result.IsSuccess)
            {
                return new BaseResponseModel<SubmissionReviewSummaryDTO>
                {
                    IsSuccess = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = result.Message
                };
            }

            var summaryResult = await GetSubmissionReviewSummaryAsync(review.Assignment.SubmissionId);
            return new BaseResponseModel<SubmissionReviewSummaryDTO>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Đặt deadline thành công",
                Data = summaryResult.Data
            };
        }
        catch (Exception ex)
        {
            return new BaseResponseModel<SubmissionReviewSummaryDTO>
            {
                IsSuccess = false,
                StatusCode = StatusCodes.Status500InternalServerError,
                Message = $"Lỗi hệ thống: {ex.Message}"
            };
        }
    }

    public async Task<BaseResponseModel> ProcessOverdueRevisionsAsync()
    {
        try
        {
            var assignmentRepo = _unitOfWork.GetRepo<ReviewerAssignment>();
            var submissionRepo = _unitOfWork.GetRepo<Submission>();

            // Step 1: load assignments that have a deadline (avoid including "now" in EF predicate)
            var assignmentsWithDeadline = await assignmentRepo.GetAllAsync(new QueryOptions<ReviewerAssignment>
            {
                Predicate = x => x.Deadline.HasValue,
                IncludeProperties = new List<System.Linq.Expressions.Expression<Func<ReviewerAssignment, object>>>
                {
                    x => x.Submission
                },
                Tracked = false // load as no-tracking and then use UpdateAsync on submissions to persist changes
            });

            var now = DateTime.UtcNow;

            // Step 2: filter in-memory for overdue assignments where the submission currently requires revision
            var overdueAssignments = assignmentsWithDeadline
                .Where(a => a.Deadline.HasValue && a.Deadline.Value < now && a.Submission != null && a.Submission.Status == SubmissionStatus.RevisionRequired)
                .ToList();

            if (!overdueAssignments.Any())
            {
                return new BaseResponseModel
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Không có submission quá hạn để xử lý"
                };
            }

            // Deduplicate submissions so we only update each one once
            var updatedSubmissionIds = new HashSet<int>();
            foreach (var assignment in overdueAssignments)
            {
                var submission = assignment.Submission;
                if (submission == null) continue;

                if (updatedSubmissionIds.Contains(submission.Id)) continue;

                submission.Status = SubmissionStatus.Pending; // mark overdue revisions as pending
                await submissionRepo.UpdateAsync(submission);
                updatedSubmissionIds.Add(submission.Id);
            }

            var result = await _unitOfWork.SaveAsync();

            return new BaseResponseModel
            {
                IsSuccess = result.IsSuccess,
                StatusCode = result.IsSuccess ? StatusCodes.Status200OK : StatusCodes.Status500InternalServerError,
                Message = result.IsSuccess ?
                    $"Xử lý {updatedSubmissionIds.Count} submission quá hạn" :
                    result.Message
            };
        }
        catch (Exception ex)
        {
            return new BaseResponseModel
            {
                IsSuccess = false,
                StatusCode = StatusCodes.Status500InternalServerError,
                Message = $"Lỗi hệ thống: {ex.Message}"
            };
        }
    }
}