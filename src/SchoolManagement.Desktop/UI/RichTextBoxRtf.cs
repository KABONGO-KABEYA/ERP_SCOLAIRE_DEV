using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace SchoolManagement.Desktop.UI;

public static class RichTextBoxRtf
{
    private static readonly DependencyProperty IsUpdatingProperty = DependencyProperty.RegisterAttached(
        "IsUpdating", typeof(bool), typeof(RichTextBoxRtf));

    public static readonly DependencyProperty ContentProperty = DependencyProperty.RegisterAttached(
        "Content", typeof(string), typeof(RichTextBoxRtf),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnContentChanged));

    public static string GetContent(DependencyObject element) => (string)element.GetValue(ContentProperty);
    public static void SetContent(DependencyObject element, string value) => element.SetValue(ContentProperty, value);

    private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RichTextBox editor) return;
        if ((bool)editor.GetValue(IsUpdatingProperty)) return;
        editor.TextChanged -= EditorOnTextChanged;
        try
        {
            editor.SetValue(IsUpdatingProperty, true);
            editor.Document.Blocks.Clear();
            if (e.NewValue is string value && !string.IsNullOrWhiteSpace(value))
            {
                using var stream = new MemoryStream(Convert.FromBase64String(value));
                new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Load(stream, DataFormats.Rtf);
            }
            else
            {
                editor.Document.Blocks.Add(new Paragraph());
            }
        }
        catch (FormatException)
        {
            editor.Document.Blocks.Clear();
            editor.Document.Blocks.Add(new Paragraph());
        }
        finally
        {
            editor.SetValue(IsUpdatingProperty, false);
            editor.TextChanged += EditorOnTextChanged;
        }
    }

    private static void EditorOnTextChanged(object sender, TextChangedEventArgs e)
    {
        var editor = (RichTextBox)sender;
        if ((bool)editor.GetValue(IsUpdatingProperty)) return;
        using var stream = new MemoryStream();
        new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Save(stream, DataFormats.Rtf);
        editor.SetValue(IsUpdatingProperty, true);
        try
        {
            editor.SetCurrentValue(ContentProperty, Convert.ToBase64String(stream.ToArray()));
            editor.GetBindingExpression(ContentProperty)?.UpdateSource();
        }
        finally
        {
            editor.SetValue(IsUpdatingProperty, false);
        }
    }
}
