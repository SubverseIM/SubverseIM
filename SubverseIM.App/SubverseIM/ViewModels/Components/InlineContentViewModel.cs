namespace SubverseIM.ViewModels.Components
{
    public class InlineContentViewModel : ViewModelBase
    {
        private readonly InlineStyle inlineStyle;

        public string Content { get; }

        public bool HasNewline => Content.EndsWith('\n');

        public bool IsEmphasis => inlineStyle.HasFlag(InlineStyle.Emphasis);

        public bool IsItalics => inlineStyle.HasFlag(InlineStyle.Italics);

        public bool IsUnderline => inlineStyle.HasFlag(InlineStyle.Underline);

        public bool IsStrike => inlineStyle.HasFlag(InlineStyle.Strike);

        public bool IsCode => inlineStyle.HasFlag(InlineStyle.Code);

        public InlineContentViewModel(string content, InlineStyle inlineStyle)
        {
            Content = content;
            this.inlineStyle = inlineStyle;
        }
    }
}
