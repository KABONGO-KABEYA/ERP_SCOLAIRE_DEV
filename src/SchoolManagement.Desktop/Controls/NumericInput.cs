using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SchoolManagement.Desktop.Controls;

/// <summary>Modes de saisie numérique pour TextBox / ErpTextField.</summary>
public enum NumericInputMode
{
    None = 0,
    /// <summary>Chiffres uniquement (0-9).</summary>
    Integer = 1,
    /// <summary>Chiffres + un séparateur décimal (, ou .).</summary>
    Decimal = 2
}

/// <summary>
/// Filtre la saisie clavier / collage pour n'autoriser que des caractères numériques.
/// Usage : <c>controls:NumericInput.Mode="Decimal"</c> sur un TextBox.
/// </summary>
public static class NumericInput
{
    public static readonly DependencyProperty ModeProperty =
        DependencyProperty.RegisterAttached(
            "Mode",
            typeof(NumericInputMode),
            typeof(NumericInput),
            new PropertyMetadata(NumericInputMode.None, OnModeChanged));

    public static NumericInputMode GetMode(DependencyObject element) =>
        (NumericInputMode)element.GetValue(ModeProperty);

    public static void SetMode(DependencyObject element, NumericInputMode value) =>
        element.SetValue(ModeProperty, value);

    private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox)
        {
            return;
        }

        textBox.PreviewTextInput -= OnPreviewTextInput;
        textBox.PreviewKeyDown -= OnPreviewKeyDown;
        DataObject.RemovePastingHandler(textBox, OnPasting);
        textBox.TextChanged -= OnTextChanged;

        if (e.NewValue is NumericInputMode mode && mode != NumericInputMode.None)
        {
            textBox.PreviewTextInput += OnPreviewTextInput;
            textBox.PreviewKeyDown += OnPreviewKeyDown;
            DataObject.AddPastingHandler(textBox, OnPasting);
            textBox.TextChanged += OnTextChanged;
            InputMethod.SetIsInputMethodEnabled(textBox, false);
        }
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            e.Handled = true;
        }
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        var mode = GetMode(textBox);
        var proposed = BuildProposedText(textBox, e.Text);
        if (!IsValidNumericText(proposed, mode))
        {
            e.Handled = true;
        }
    }

    private static void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox || !e.DataObject.GetDataPresent(DataFormats.UnicodeText))
        {
            return;
        }

        var pasted = e.DataObject.GetData(DataFormats.UnicodeText) as string ?? string.Empty;
        var mode = GetMode(textBox);
        var proposed = BuildProposedText(textBox, pasted);
        if (!IsValidNumericText(proposed, mode))
        {
            e.CancelCommand();
        }
    }

    private static void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        var mode = GetMode(textBox);
        if (mode == NumericInputMode.None)
        {
            return;
        }

        var sanitized = Sanitize(textBox.Text, mode);
        if (sanitized == textBox.Text)
        {
            return;
        }

        var caret = textBox.CaretIndex;
        textBox.Text = sanitized;
        textBox.CaretIndex = Math.Clamp(caret, 0, sanitized.Length);
    }

    private static string BuildProposedText(TextBox textBox, string incoming)
    {
        var text = textBox.Text ?? string.Empty;
        var start = textBox.SelectionStart;
        var length = textBox.SelectionLength;
        return text.Remove(start, length).Insert(start, incoming);
    }

    public static bool IsValidNumericText(string? text, NumericInputMode mode)
    {
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        return mode switch
        {
            NumericInputMode.Integer => text.All(char.IsDigit),
            NumericInputMode.Decimal => IsValidDecimalText(text),
            _ => true
        };
    }

    public static string Sanitize(string? text, NumericInputMode mode)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return mode switch
        {
            NumericInputMode.Integer => new string(text.Where(char.IsDigit).ToArray()),
            NumericInputMode.Decimal => SanitizeDecimal(text),
            _ => text
        };
    }

    private static bool IsValidDecimalText(string text)
    {
        var separatorSeen = false;
        foreach (var c in text)
        {
            if (char.IsDigit(c))
            {
                continue;
            }

            if ((c is ',' or '.') && !separatorSeen)
            {
                separatorSeen = true;
                continue;
            }

            return false;
        }

        return true;
    }

    private static string SanitizeDecimal(string text)
    {
        var sb = new StringBuilder(text.Length);
        var separatorSeen = false;
        foreach (var c in text)
        {
            if (char.IsDigit(c))
            {
                sb.Append(c);
                continue;
            }

            if ((c is ',' or '.') && !separatorSeen)
            {
                sb.Append(c);
                separatorSeen = true;
            }
        }

        return sb.ToString();
    }

    /// <summary>Convertit le mode de validation champ ERP vers le filtre numérique.</summary>
    public static NumericInputMode FromValidationMode(ErpFieldValidationMode validationMode) =>
        validationMode switch
        {
            ErpFieldValidationMode.Integer => NumericInputMode.Integer,
            ErpFieldValidationMode.Decimal => NumericInputMode.Decimal,
            _ => NumericInputMode.None
        };
}
