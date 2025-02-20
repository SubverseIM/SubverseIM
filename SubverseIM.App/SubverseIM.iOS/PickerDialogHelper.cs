using UIKit;

namespace SubverseIM.iOS
{
    internal class PickerDialogHelper
    {
        private readonly string[] pickerItems;

        private readonly UIPickerView pickerView;

        private readonly UITextField textField;

        public PickerDialogHelper(UITextField textField, params string[] pickerItems) 
        {
            this.textField = textField;
            this.textField.InputView = pickerView = new()
            {
                DataSource = new DataSource(this),
                Delegate = new Delegate(this),
            };
            this.pickerItems = pickerItems;
        }

        public static implicit operator UIPickerView(PickerDialogHelper helper) 
        {
            return helper.pickerView;
        }

        public class DataSource : UIPickerViewDataSource
        {
            private readonly PickerDialogHelper helper;

            public DataSource(PickerDialogHelper helper)
            {
                this.helper = helper;
            }

            public override nint GetComponentCount(UIPickerView pickerView)
            {
                return 1;
            }

            public override nint GetRowsInComponent(UIPickerView pickerView, nint component)
            {
                return helper.pickerItems.Length;
            }
        }

        public class Delegate : UIPickerViewDelegate
        {
            private readonly PickerDialogHelper helper;

            public Delegate(PickerDialogHelper helper) 
            {
                this.helper = helper;
            }

            public override nfloat GetRowHeight(UIPickerView pickerView, nint component)
            {
                return 25.0f;
            }

            public override string GetTitle(UIPickerView pickerView, nint row, nint component)
            {
                return helper.pickerItems[row];
            }

            public override void Selected(UIPickerView pickerView, nint row, nint component)
            {
                helper.textField.Text = helper.pickerItems[row];
            }
        }
    }
}