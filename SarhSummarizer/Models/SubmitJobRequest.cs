using System.ComponentModel.DataAnnotations;

namespace SarhSummarizer.Models;

public class SubmitJobRequest
{
    [Required]
    public string Text { get; set; } = string.Empty;
    public string Priority { get; set; } = "normal";
}