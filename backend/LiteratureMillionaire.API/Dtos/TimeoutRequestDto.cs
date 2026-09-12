using System.ComponentModel.DataAnnotations;

namespace LiteratureMillionaire.API.Dtos;

/// <summary>Identifies the question the client believes is current, so stale timeouts are rejected.</summary>
public class TimeoutRequestDto
{
    [Required, Range(1, int.MaxValue)]
    public int QuestionId { get; set; }
}
