using Foundation;
using LinkPresentation;
using UIKit;

namespace SubverseIM.iOS
{
    internal class CustomActivityItemSource : UIActivityItemSource
    {
        private readonly string title, urlString, typeIdentifier;

        public CustomActivityItemSource(string title, string urlString, string typeIdentifier) 
        {
            this.title = title;
            this.urlString = urlString;
            this.typeIdentifier = typeIdentifier;
        }

        public override NSObject GetPlaceholderData(UIActivityViewController activityViewController)
        {
            return (NSString)urlString;
        }

        public override NSObject GetItemForActivity(UIActivityViewController activityViewController, NSString? activityType)
        {
            return (NSString)urlString;
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
                OriginalUrl = new NSUrl(urlString),
            };
        }
    }
}
