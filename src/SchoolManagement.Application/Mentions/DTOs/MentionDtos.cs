using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Application.Mentions.DTOs;

public sealed record ResultMentionDto(
    Guid Id,
    string Label,
    decimal MinPercentageInclusive,
    decimal MaxPercentageInclusive,
    int SortOrder,
    bool IsActive,
    string RangeDisplay);

public sealed record CreateResultMentionRequest(
    string Label,
    decimal MinPercentageInclusive,
    decimal MaxPercentageInclusive,
    int SortOrder,
    bool IsActive = true);

public sealed record UpdateResultMentionRequest(
    string Label,
    decimal MinPercentageInclusive,
    decimal MaxPercentageInclusive,
    int SortOrder,
    bool IsActive);
