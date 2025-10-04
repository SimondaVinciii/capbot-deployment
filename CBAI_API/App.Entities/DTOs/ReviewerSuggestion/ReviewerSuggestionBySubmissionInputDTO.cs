using System.ComponentModel.DataAnnotations;

namespace App.Entities.DTOs.ReviewerSuggestion
{
    /// <summary>
    /// Input DTO for suggesting reviewers by SubmissionId
    /// </summary>
    public class ReviewerSuggestionBySubmissionInputDTO
    {
        [Required]
        public int SubmissionId { get; set; }

        [Range(1, 1000)]
        public int MaxSuggestions { get; set; } = 5;

        public bool UsePrompt { get; set; } = true;
        // Optional deadline to apply when auto-assigning suggested reviewers
        public DateTime? Deadline { get; set; }
    }
}