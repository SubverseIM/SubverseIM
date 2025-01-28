using Foundation;
using LinkPresentation;
using UIKit;

namespace SubverseIM.iOS
{
    internal class CustomActivityItemSource : UIActivityItemSource
    {
        private readonly string title, typeIdentifier;
        private readonly NSObject item;

        public CustomActivityItemSource(string title, NSObject item, string typeIdentifier) 
        {
            this.title = title;

            this.item = item;
            this.typeIdentifier = typeIdentifier;
        }

        public override NSObject GetPlaceholderData(UIActivityViewController activityViewController)
        {
            return item;
        }

        public override NSObject GetItemForActivity(UIActivityViewController activityViewController, NSString? activityType)
        {
            return item;
        }

        public override string GetSubjectForActivity(UIActivityViewController activityViewController, NSString? activityType)
        {
            return (NSString)title;
        }

        public override string GetDataTypeIdentifierForActivity(UIActivityViewController activityViewController, NSString? activityType)
        {
            return typeIdentifier;
        }

        public override LPLinkMetadata? GetLinkMetadata(UIActivityViewController activityViewController)
        {
            return new LPLinkMetadata
            {
                Title = title,
                OriginalUrl = item as NSUrl,
            };
        }
    }
}
