namespace SubverseIM.ViewModels.Components
{
    public class InlineContentViewModel : ViewModelBase
    {
        public string Content { get; }

        public bool IsEmphasized { get; }

        public bool IsItalics { get; }

        public InlineContentViewModel(string content, bool isEmphasized, bool isItalics)
        {
            Content = content;
            IsEmphasized = isEmphasized;
            IsItalics = isItalics;
        }
    }
}
