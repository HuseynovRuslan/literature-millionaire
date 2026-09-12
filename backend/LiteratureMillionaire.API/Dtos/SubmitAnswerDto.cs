using System.ComponentModel.DataAnnotations;

namespace LiteratureMillionaire.API.Dtos;

public class SubmitAnswerDto
{
    [Required, Range(1, int.MaxValue)]
    public int QuestionId { get; set; }

    [Required, RegularExpression("^[ABCD]$", ErrorMessage = "SelectedOption must be A, B, C or D.")]
    public string SelectedOption { get; set; } = string.Empty;
}
