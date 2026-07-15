using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SchoolManagement.Application.Students.DTOs;
using SchoolManagement.Domain.Enums;

namespace SchoolManagement.Desktop.UI;

public sealed class NullableDateOnlyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateOnly date ? date.ToDateTime(TimeOnly.MinValue) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return null;
        }

        return value is DateTime dateTime ? DateOnly.FromDateTime(dateTime) : null;
    }
}

public sealed class DateOnlyDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateOnly date ? date.ToString("dd/MM/yyyy", culture) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class GenderDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            Gender.Masculin => "Masculin",
            Gender.Feminin => "Féminin",
            _ => "—"
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class StudentStatusLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not StudentDto student)
        {
            return "—";
        }

        if (student.IsArchived)
        {
            return "Archivé";
        }

        if (student.CurrentYearStatus == EnrollmentStatus.Exclusion)
        {
            return "Exclu";
        }

        if (student.CurrentYearStatus == EnrollmentStatus.Abandon)
        {
            return "Abandonné";
        }

        return student.IsEnrolledCurrentYear ? "Inscrit" : "Non inscrit";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class StudentStatusIsActiveConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is StudentDto student && !student.IsArchived && student.IsEnrolledCurrentYear;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class NullToDashConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null or "" ? "—" : value;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
