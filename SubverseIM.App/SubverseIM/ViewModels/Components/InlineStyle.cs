using System;

namespace SubverseIM.ViewModels.Components
{
    [Flags]
    public enum InlineStyle 
    {
        Plain = 0x00,
        Emphasis = 0x01,
        Italics = 0x02,
        Strikeout = 0x04,
        Underline = 0x08,
        Code = 0x10,
    }
}
