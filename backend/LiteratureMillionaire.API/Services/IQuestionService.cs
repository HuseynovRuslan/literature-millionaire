using LiteratureMillionaire.API.Dtos;
using LiteratureMillionaire.API.Entities;

namespace LiteratureMillionaire.API.Services;

public interface IQuestionService
{
    Task<IReadOnlyList<QuestionDto>> GetAllAsync(Difficulty? difficulty, string? category, int? bookId, CancellationToken ct = default);
    Task<QuestionDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<QuestionDto> CreateAsync(CreateQuestionDto dto, CancellationToken ct = default);
    Task<QuestionDto?> UpdateAsync(int id, UpdateQuestionDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}
